using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace FastPick.Services;

/// <summary>
/// RAW 元数据服务 - 使用 NTFS Alternate Data Streams (ADS) 存储评级
/// ADS 格式: filename.raw:FastPickRating (存储 0-5 数字)
/// </summary>
public class RawMetadataService
{
    // ADS 流名称
    private const string AdsStreamName = "FastPickRating";

    /// <summary>
    /// 检查文件所在分区是否为 NTFS
    /// </summary>
    public bool IsNtfsDrive(string filePath)
    {
        try
        {
            var drive = Path.GetPathRoot(filePath);
            if (string.IsNullOrEmpty(drive))
                return false;

            var driveInfo = new DriveInfo(drive);
            return driveInfo.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 读取 RAW 评级
    /// </summary>
    /// <param name="filePath">RAW 文件路径</param>
    /// <returns>评级 (0-5)，读取失败返回 0</returns>
    public int ReadRating(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return 0;

            // 构建 ADS 路径: filename.raw:FastPickRating
            string adsPath = $"{filePath}:{AdsStreamName}";

            // 检查 ADS 是否存在
            if (!AdsExists(filePath, AdsStreamName))
                return 0;

            // 读取 ADS 内容
            using var stream = new FileStream(adsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string content = reader.ReadToEnd();

            if (int.TryParse(content.Trim(), out int rating))
            {
                return Math.Clamp(rating, 0, 5);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"读取 RAW 评级失败: {filePath}, {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 写入 RAW 评级
    /// </summary>
    /// <param name="filePath">RAW 文件路径</param>
    /// <param name="rating">评级 (0-5)</param>
    /// <returns>是否成功</returns>
    public bool WriteRating(string filePath, int rating)
    {
        rating = Math.Clamp(rating, 0, 5);

        try
        {
            if (!File.Exists(filePath))
                return false;

            // 检查是否为 NTFS
            if (!IsNtfsDrive(filePath))
            {
                Debug.WriteLine($"非 NTFS 分区，无法写入 ADS: {filePath}");
                return false;
            }

            // 构建 ADS 路径
            string adsPath = $"{filePath}:{AdsStreamName}";

            // 写入评级到 ADS
            using var stream = new FileStream(adsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(rating.ToString());

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"写入 RAW 评级失败: {filePath}, {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 删除 RAW 评级
    /// </summary>
    /// <param name="filePath">RAW 文件路径</param>
    /// <returns>是否成功</returns>
    public bool DeleteRating(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            // 构建 ADS 路径
            string adsPath = $"{filePath}:{AdsStreamName}";

            // 检查 ADS 是否存在
            if (!AdsExists(filePath, AdsStreamName))
                return true; // 不存在视为成功

            // 使用 Windows API 删除 ADS
            return DeleteAdsStream(filePath, AdsStreamName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"删除 RAW 评级失败: {filePath}, {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查 ADS 是否存在
    /// </summary>
    private bool AdsExists(string filePath, string streamName)
    {
        try
        {
            string adsPath = $"{filePath}:{streamName}";
            return File.Exists(adsPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 删除 ADS 流
    /// </summary>
    private bool DeleteAdsStream(string filePath, string streamName)
    {
        try
        {
            // 使用 Windows API 删除 ADS
            string adsPath = $"{filePath}:{streamName}";

            // 打开文件并删除 ADS
            using (var stream = new FileStream(adsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
            {
                // 清空 ADS 内容（相当于删除）
                stream.SetLength(0);
            }

            // 使用 P/Invoke 真正删除流
            return NativeMethods.DeleteFile(adsPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"删除 ADS 流失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 批量写入 RAW 评级
    /// </summary>
    /// <param name="filePaths">RAW 文件路径列表</param>
    /// <param name="rating">评级 (0-5)</param>
    /// <returns>成功写入的文件数</returns>
    public int WriteRatingBatch(List<string> filePaths, int rating)
    {
        int successCount = 0;

        foreach (var filePath in filePaths)
        {
            if (WriteRating(filePath, rating))
            {
                successCount++;
            }

            // 小延迟避免阻塞
            Thread.Sleep(10);
        }

        return successCount;
    }
}

/// <summary>
/// Windows API P/Invoke
/// </summary>
internal static class NativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteFile(string lpFileName);
}
