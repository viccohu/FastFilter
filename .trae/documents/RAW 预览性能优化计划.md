# RAW 预览性能优化计划

## 问题分析

### 当前 Bug
- 刚开始浏览 RAW 文件时显示内嵌预览
- 多看几张后，每张选中的 RAW 文件都执行"加载完整 RAW"降级方案
- 浏览体验不顺畅，解码慢

### 根本原因

**问题 1：WIC Decoder 状态污染**
```csharp
// 当前代码 - LoadRawEmbeddedPreviewAsync
var decoder = await BitmapDecoder.CreateAsync(stream);
// 如果解码失败，stream 位置已改变
stream.Seek(0);  // 即使 Seek 回来，decoder 可能已失败
await bitmap.SetSourceAsync(stream);  // 使用同一个 stream 再次解码
```

**问题 2：没有缓存解码失败状态**
- 某个 RAW 文件内嵌预览提取失败后，没有记录
- 下次浏览到其他 RAW 文件时，仍然尝试提取内嵌预览
- 但 WIC decoder 可能因为之前的失败而进入错误状态

**问题 3：stream 重用问题**
```csharp
using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
var decoder = await BitmapDecoder.CreateAsync(stream);  // stream 已读取
// ...
stream.Seek(0);  // 试图重置
await bitmap.SetSourceAsync(stream);  // 可能失败
```

## 解决方案

### 方案 1：分离 stream + 并发安全（推荐）

**核心思路**：
1. 内嵌预览和完整 RAW 使用独立的 stream
2. 内嵌预览失败后，重新打开新的 stream 加载完整 RAW
3. 添加失败缓存，避免重复尝试
4. **确保并发安全，支持多张 RAW 同时加载**

**潜在并发问题分析**：

**问题 A：失败缓存的并发访问**
```csharp
// 危险代码 - 多线程同时访问 HashSet
if (EmbeddedPreviewFailedCache.Contains(photoItem.RawPath))  // 线程 1 检查
{
    return await LoadRawFullAsync(photoItem.RawPath);
}
// 线程 2 同时检查，可能都返回 false，然后都尝试加载内嵌预览
```

**问题 B：多张 RAW 同时加载时的资源竞争**
```csharp
// 用户快速切换缩略图时
ThumbnailRepeater_ElementPrepared(...)  // 同时触发多次
{
    await LoadThumbnailToImageAsync(...);  // 多个任务并发
}
```

**问题 C：内存爆炸风险**
- 快速滚动时，可能同时打开 10+ 个 RAW 文件
- 每个 RAW 文件 2 个 stream（内嵌 + 完整）
- 可能导致文件句柄耗尽或内存溢出

**解决方案**：

1. **使用 ConcurrentDictionary 保证缓存线程安全**
2. **添加加载任务去重** - 同一文件只加载一次
3. **限制并发加载数量** - 使用信号量控制
4. **添加取消机制** - 切换时取消之前的加载

**实现步骤**：

```csharp
public class PreviewImageService
{
    // 并发安全的失败缓存
    private readonly ConcurrentDictionary<string, bool> _embeddedPreviewFailedCache = new();
    
    // 加载任务缓存 - 避免同一文件重复加载
    private readonly ConcurrentDictionary<string, Task<BitmapImage?>> _loadingTasks = new();
    
    // 限制并发加载数量（最多 3 个同时加载）
    private readonly SemaphoreSlim _loadingSemaphore = new(3, 3);

    public async Task<BitmapImage?> LoadPreviewAsync(PhotoItem photoItem, bool useEmbeddedPreview = true)
    {
        // 优先加载 JPG
        if (photoItem.HasJpg)
        {
            return await LoadJpgPreviewAsync(photoItem.JpgPath);
        }

        // 如果是 RAW
        if (photoItem.HasRaw && photoItem.RawPath != null)
        {
            var rawPath = photoItem.RawPath;
            
            // 检查是否有正在进行的加载任务
            if (_loadingTasks.TryGetValue(rawPath, out var existingTask))
            {
                return await existingTask;
            }

            // 创建新的加载任务
            var loadTask = LoadRawPreviewInternalAsync(rawPath, useEmbeddedPreview);
            _loadingTasks[rawPath] = loadTask;

            try
            {
                var result = await loadTask;
                return result;
            }
            finally
            {
                // 清理任务缓存
                _loadingTasks.TryRemove(rawPath, out _);
            }
        }

        return null;
    }

    private async Task<BitmapImage?> LoadRawPreviewInternalAsync(string rawPath, bool useEmbeddedPreview)
    {
        // 等待信号量（限制并发数量）
        await _loadingSemaphore.WaitAsync();
        
        try
        {
            // 检查是否在失败缓存中
            if (_embeddedPreviewFailedCache.ContainsKey(rawPath))
            {
                // 直接加载完整 RAW
                return await LoadRawFullAsync(rawPath);
            }

            if (useEmbeddedPreview)
            {
                try
                {
                    var embeddedPreview = await LoadRawEmbeddedPreviewAsync(rawPath);
                    if (embeddedPreview != null)
                    {
                        // 成功，从失败缓存中移除
                        _embeddedPreviewFailedCache.TryRemove(rawPath, out _);
                        return embeddedPreview;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"加载内嵌预览失败：{ex.Message}");
                    // 失败，加入缓存
                    _embeddedPreviewFailedCache[rawPath] = true;
                }
            }

            // 加载完整 RAW（使用新的 stream）
            return await LoadRawFullAsync(rawPath);
        }
        finally
        {
            _loadingSemaphore.Release();
        }
    }

    private async Task<BitmapImage?> LoadRawEmbeddedPreviewAsync(string rawPath)
    {
        // 使用独立的 stream
        var storageFile = await StorageFile.GetFileFromPathAsync(rawPath);
        
        using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        
        // 检查是否有有效的预览
        if (decoder.PixelWidth == 0 || decoder.PixelHeight == 0)
        {
            return null;
        }

        var bitmap = new BitmapImage();
        if (decoder.PixelWidth > MaxPreviewWidth || decoder.PixelHeight > MaxPreviewHeight)
        {
            bitmap.DecodePixelWidth = MaxPreviewWidth;
        }

        // 重新打开 stream 供 bitmap 使用（避免 stream 状态污染）
        using var bitmapStream = await storageFile.OpenAsync(FileAccessMode.Read);
        await bitmap.SetSourceAsync(bitmapStream);
        
        return bitmap;
    }

    private async Task<BitmapImage?> LoadRawFullAsync(string rawPath)
    {
        // 使用独立的 stream
        var storageFile = await StorageFile.GetFileFromPathAsync(rawPath);
        
        using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
        
        var bitmap = new BitmapImage
        {
            DecodePixelWidth = MaxPreviewWidth
        };

        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }
}
```

### 方案 2：预先检测 RAW 格式支持（备选）

**核心思路**：
1. 在加载前检测 RAW 文件是否有内嵌预览
2. 检查 WIC 是否支持该 RAW 格式
3. 不支持的格式直接跳过内嵌预览提取

### 方案 3：后台预加载（优化体验）

**核心思路**：
1. 用户浏览当前图片时，后台预加载下一张图片的内嵌预览
2. 使用双缓冲，避免切换时的等待

## 实施计划

### 第一阶段：修复核心 Bug（必须）
1. 添加 `using System.Collections.Concurrent;` 和 `using System.Threading;`
2. 在 PreviewImageService 中添加并发安全字段
3. 重构 LoadPreviewAsync 方法，添加任务去重
4. 重构 LoadRawEmbeddedPreviewAsync，使用独立 stream
5. 添加 SemaphoreSlim 限制并发数量

### 第二阶段：优化体验（可选）
1. 添加预加载功能
2. 添加加载进度提示
3. 优化内存管理

### 第三阶段：测试验证
1. 测试快速切换 RAW 文件
2. 测试多张 RAW 同时加载
3. 测试内存占用
4. 测试失败场景

## 验收标准

- [ ] 每个 RAW 文件独立处理，互不影响
- [ ] 有内嵌预览的 RAW 文件始终优先显示内嵌预览
- [ ] 没有内嵌预览的 RAW 文件直接加载完整 RAW（不重复尝试）
- [ ] 浏览体验流畅，无明显卡顿
- [ ] 内存占用合理，无内存泄漏
