using System.Diagnostics;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace FastPick.Services;

/// <summary>
/// JPG 元数据服务 - 使用 WIC 读写 Exif Rating 标签
/// </summary>
public class JpgMetadataService
{
    // Exif Rating 标签的查询字符串
    private const string RatingQuery = "/app1/ifd/exif/{ushort=18249}";
    
    /// <summary>
    /// 读取 JPG 评级
    /// </summary>
    /// <param name="filePath">JPG 文件路径</param>
    /// <returns>评级 (0-5)，读取失败返回 0</returns>
    public async Task<int> ReadRatingAsync(string filePath)
    {
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
            
            using var stream = await storageFile.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            
            // 读取元数据
            var properties = decoder.BitmapProperties;
            
            if (properties != null)
            {
                try
                {
                    var ratingProperty = await properties.GetPropertiesAsync(new[] { RatingQuery });
                    
                    if (ratingProperty.TryGetValue(RatingQuery, out var ratingValue))
                    {
                        // Rating 值通常是 ushort
                        if (ratingValue?.Value is ushort rating)
                        {
                            return Math.Clamp((int)rating, 0, 5);
                        }
                        if (ratingValue?.Value is uint ratingUint)
                        {
                            return Math.Clamp((int)ratingUint, 0, 5);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"读取 Rating 失败: {filePath}, {ex.Message}");
                }
            }
            
            return 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"读取 JPG 元数据失败: {filePath}, {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// 写入 JPG 评级
    /// </summary>
    /// <param name="filePath">JPG 文件路径</param>
    /// <param name="rating">评级 (0-5)</param>
    /// <returns>是否成功</returns>
    public async Task<bool> WriteRatingAsync(string filePath, int rating)
    {
        rating = Math.Clamp(rating, 0, 5);
        
        try
        {
            var storageFile = await StorageFile.GetFileFromPathAsync(filePath);
            
            // 创建编码器来写入元数据
            using var stream = await storageFile.OpenAsync(FileAccessMode.ReadWrite);
            
            // 创建内存流来保存修改后的图像
            var memoryStream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            
            // 打开原图
            var decoder = await BitmapDecoder.CreateAsync(stream);
            
            // 创建编码器
            var encoder = await BitmapEncoder.CreateForTranscodingAsync(memoryStream, decoder);
            
            // 设置元数据
            var bitmapProperties = new BitmapPropertySet();
            
            // Rating 值使用 ushort 类型
            var ratingValue = new BitmapTypedValue(
                (ushort)rating,
                Windows.Foundation.PropertyType.UInt16
            );
            
            bitmapProperties.Add(RatingQuery, ratingValue);
            
            // 写入元数据
            await encoder.BitmapProperties.SetPropertiesAsync(bitmapProperties);
            
            // 刷新编码器
            await encoder.FlushAsync();
            
            // 复制回原始文件
            stream.Seek(0);
            memoryStream.Seek(0);
            
            // 清空原文件
            stream.Size = 0;
            
            // 复制新数据
            await Windows.Storage.Streams.RandomAccessStream.CopyAsync(memoryStream, stream);
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"写入 JPG 元数据失败: {filePath}, {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 批量写入 JPG 评级
    /// </summary>
    /// <param name="filePaths">JPG 文件路径列表</param>
    /// <param name="rating">评级 (0-5)</param>
    /// <returns>成功写入的文件数</returns>
    public async Task<int> WriteRatingBatchAsync(List<string> filePaths, int rating)
    {
        int successCount = 0;
        
        foreach (var filePath in filePaths)
        {
            if (await WriteRatingAsync(filePath, rating))
            {
                successCount++;
            }
            
            // 小延迟避免阻塞
            await Task.Delay(10);
        }
        
        return successCount;
    }
}
