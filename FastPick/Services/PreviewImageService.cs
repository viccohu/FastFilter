using FastPick.Models;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FastPick.Services;

/// <summary>
/// 预览图加载服务
/// 支持 JPG 原图加载和 RAW 内嵌预览提取
/// </summary>
public class PreviewImageService
{
    // 最大预览图尺寸（避免内存占用过大）
    private const int MaxPreviewWidth = 1920;
    private const int MaxPreviewHeight = 1080;

    /// <summary>
    /// 加载预览图
    /// </summary>
    /// <param name="photoItem">图片项</param>
    /// <param name="useEmbeddedPreview">RAW 文件是否优先使用内嵌预览</param>
    /// <returns>预览图 BitmapImage</returns>
    public async Task<BitmapImage?> LoadPreviewAsync(PhotoItem photoItem, bool useEmbeddedPreview = true)
    {
        try
        {
            // 优先加载 JPG
            if (photoItem.HasJpg)
            {
                return await LoadJpgPreviewAsync(photoItem.JpgPath);
            }

            // 如果是 RAW，尝试提取内嵌预览
            if (photoItem.HasRaw && photoItem.RawPath != null)
            {
                if (useEmbeddedPreview)
                {
                    var embeddedPreview = await LoadRawEmbeddedPreviewAsync(photoItem.RawPath);
                    if (embeddedPreview != null)
                        return embeddedPreview;
                }

                // 如果内嵌预览失败，尝试加载完整 RAW（降级方案）
                return await LoadRawFullAsync(photoItem.RawPath);
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载预览图失败: {photoItem.FileName}, 错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 加载 JPG 预览图
    /// </summary>
    private async Task<BitmapImage?> LoadJpgPreviewAsync(string filePath)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(filePath);

            // 使用 BitmapDecoder 获取图片尺寸并限制最大尺寸
            using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);

            var bitmap = new BitmapImage();

            // 如果图片太大，限制解码尺寸
            if (decoder.PixelWidth > MaxPreviewWidth || decoder.PixelHeight > MaxPreviewHeight)
            {
                bitmap.DecodePixelWidth = MaxPreviewWidth;
            }

            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);

            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载 JPG 预览失败: {filePath}, 错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 加载 RAW 内嵌预览图（使用 WIC 解码器）
    /// </summary>
    private async Task<BitmapImage?> LoadRawEmbeddedPreviewAsync(string rawPath)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(rawPath);

            // 使用独立的 stream，让 WIC 优先读取内嵌预览
            using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
            
            // 不限制解码尺寸，让 WIC 自动选择内嵌预览
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            
            Debug.WriteLine($"成功加载 RAW 内嵌预览: {Path.GetFileName(rawPath)}, 尺寸: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载 RAW 内嵌预览失败: {rawPath}, 错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 加载完整 RAW 图片（降级方案）
    /// </summary>
    private async Task<BitmapImage?> LoadRawFullAsync(string rawPath)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(rawPath);

            using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
            var bitmap = new BitmapImage
            {
                DecodePixelWidth = MaxPreviewWidth  // 限制尺寸避免内存占用过大
            };

            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载完整 RAW 失败: {rawPath}, 错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 加载多选预览图（前5张）
    /// </summary>
    public async Task<List<BitmapImage>> LoadMultiPreviewAsync(List<PhotoItem> photoItems, int maxCount = 5)
    {
        var results = new List<BitmapImage>();
        var itemsToLoad = photoItems.Take(maxCount).ToList();

        foreach (var item in itemsToLoad)
        {
            var preview = await LoadPreviewAsync(item, useEmbeddedPreview: true);
            if (preview != null)
            {
                results.Add(preview);
            }

            // 限制同时加载的数量，避免内存占用过大
            if (results.Count >= maxCount)
                break;
        }

        return results;
    }

    /// <summary>
    /// 释放预览图资源
    /// </summary>
    public void ReleasePreview(BitmapImage? bitmap)
    {
        if (bitmap != null)
        {
            // 清理 BitmapImage 资源
            bitmap.UriSource = null;
        }
    }
}
