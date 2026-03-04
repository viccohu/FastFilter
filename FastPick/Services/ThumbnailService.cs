using FastPick.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices;
using Windows.Storage.FileProperties;
using Windows.Storage;

namespace FastPick.Services;

/// <summary>
/// 缩略图生成服务 - 系统缓存优先 → WIC 实时生成 → LRU 内存缓存
/// </summary>
public class ThumbnailService
{
    // LRU 缓存：最多缓存 200 张缩略图
    private readonly LinkedList<string> _lruList = new();
    private readonly Dictionary<string, BitmapImage> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const int MaxCacheSize = 200;
    private const int ThumbnailWidth = 100;
    private const int ThumbnailHeight = 80;

    /// <summary>
    /// 获取缩略图（优先缓存 → 系统缓存 → WIC 生成）
    /// </summary>
    public async Task<BitmapImage?> GetThumbnailAsync(PhotoItem photoItem)
    {
        var filePath = photoItem.DisplayPath;
        
        if (string.IsNullOrEmpty(filePath))
            return null;

        // 1. 检查内存缓存
        await _cacheLock.WaitAsync();
        try
        {
            if (_thumbnailCache.TryGetValue(filePath, out var cachedImage))
            {
                // 更新 LRU 顺序
                UpdateLruOrder(filePath);
                return cachedImage;
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        // 2. 尝试获取系统缩略图缓存
        var systemThumbnail = await GetSystemThumbnailAsync(filePath);
        if (systemThumbnail != null)
        {
            await AddToCacheAsync(filePath, systemThumbnail);
            return systemThumbnail;
        }

        // 3. 使用 WIC 生成缩略图
        var wicThumbnail = await GenerateWicThumbnailAsync(filePath);
        if (wicThumbnail != null)
        {
            await AddToCacheAsync(filePath, wicThumbnail);
            return wicThumbnail;
        }

        return null;
    }

    /// <summary>
    /// 获取系统缩略图缓存（IShellItemImageFactory）
    /// </summary>
    private async Task<BitmapImage?> GetSystemThumbnailAsync(string filePath)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
            var thumbnail = await storageFile.GetThumbnailAsync(
                ThumbnailMode.PicturesView,
                (uint)ThumbnailWidth,
                ThumbnailOptions.UseCurrentScale);

            if (thumbnail != null)
            {
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(thumbnail);
                thumbnail.Dispose();
                return bitmap;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取系统缩略图失败: {filePath}, {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 使用 WIC 生成缩略图
    /// </summary>
    private async Task<BitmapImage?> GenerateWicThumbnailAsync(string filePath)
    {
        return await Task.Run(async () =>
        {
            try
            {
                // 读取文件流
                using var fileStream = File.OpenRead(filePath);
                
                // 创建 BitmapImage
                var bitmap = new BitmapImage();
                bitmap.DecodePixelWidth = ThumbnailWidth;
                bitmap.DecodePixelHeight = ThumbnailHeight;
                
                // 设置源
                await bitmap.SetSourceAsync(fileStream.AsRandomAccessStream());
                
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WIC 生成缩略图失败: {filePath}, {ex.Message}");
                return null;
            }
        });
    }

    /// <summary>
    /// 添加到 LRU 缓存
    /// </summary>
    private async Task AddToCacheAsync(string filePath, BitmapImage thumbnail)
    {
        await _cacheLock.WaitAsync();
        try
        {
            // 如果缓存已满，移除最久未使用的
            while (_thumbnailCache.Count >= MaxCacheSize && _lruList.Count > 0)
            {
                var oldestKey = _lruList.Last!.Value;
                _lruList.RemoveLast();
                if (_thumbnailCache.TryGetValue(oldestKey, out var oldImage))
                {
                    // 释放图片资源
                    oldImage.UriSource = null;
                }
                _thumbnailCache.Remove(oldestKey);
            }

            // 添加到缓存
            _thumbnailCache[filePath] = thumbnail;
            _lruList.AddFirst(filePath);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 更新 LRU 顺序
    /// </summary>
    private void UpdateLruOrder(string filePath)
    {
        _lruList.Remove(filePath);
        _lruList.AddFirst(filePath);
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            foreach (var image in _thumbnailCache.Values)
            {
                image.UriSource = null;
            }
            _thumbnailCache.Clear();
            _lruList.Clear();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 获取缓存统计
    /// </summary>
    public async Task<(int count, int maxSize)> GetCacheStatsAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            return (_thumbnailCache.Count, MaxCacheSize);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// 预加载缩略图（后台任务）
    /// </summary>
    public async Task PreloadThumbnailsAsync(List<PhotoItem> items, int startIndex, int count)
    {
        var endIndex = Math.Min(startIndex + count, items.Count);
        
        for (int i = startIndex; i < endIndex; i++)
        {
            try
            {
                await GetThumbnailAsync(items[i]);
                await Task.Delay(10); // 避免阻塞 UI
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"预加载缩略图失败: {ex.Message}");
            }
        }
    }
}
