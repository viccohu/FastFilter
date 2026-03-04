using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FastPick.Models;

/// <summary>
/// 照片项模型 - 支持数据绑定
/// </summary>
public class PhotoItem : INotifyPropertyChanged
{
    private string _fileName = string.Empty;
    private string _jpgPath = string.Empty;
    private string? _rawPath;
    private int _rating;
    private bool _isMarkedForDeletion;
    private bool _isSelected;
    private FileTypeEnum _fileType;

    /// <summary>
    /// 文件名（不含扩展名）
    /// </summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            if (_fileName != value)
            {
                _fileName = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// JPG 文件路径
    /// </summary>
    public string JpgPath
    {
        get => _jpgPath;
        set
        {
            if (_jpgPath != value)
            {
                _jpgPath = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// RAW 文件路径
    /// </summary>
    public string? RawPath
    {
        get => _rawPath;
        set
        {
            if (_rawPath != value)
            {
                _rawPath = value;
                OnPropertyChanged();
                UpdateFileType();
            }
        }
    }

    /// <summary>
    /// 评级 (0-5)
    /// </summary>
    public int Rating
    {
        get => _rating;
        set
        {
            if (_rating != value)
            {
                _rating = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 是否标记为预删除
    /// </summary>
    public bool IsMarkedForDeletion
    {
        get => _isMarkedForDeletion;
        set
        {
            if (_isMarkedForDeletion != value)
            {
                _isMarkedForDeletion = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 是否被选中
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 文件类型
    /// </summary>
    public FileTypeEnum FileType
    {
        get => _fileType;
        private set
        {
            if (_fileType != value)
            {
                _fileType = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// 缩略图（运行时缓存）
    /// </summary>
    public object? Thumbnail { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 图片尺寸（如 "6000x4000"）
    /// </summary>
    public string? Dimensions { get; set; }

    /// <summary>
    /// 修改日期
    /// </summary>
    public DateTime ModifiedDate { get; set; }

    /// <summary>
    /// 获取显示用的文件路径（优先 JPG）
    /// </summary>
    public string DisplayPath => !string.IsNullOrEmpty(JpgPath) ? JpgPath : RawPath ?? string.Empty;

    /// <summary>
    /// 是否存在 JPG
    /// </summary>
    public bool HasJpg => !string.IsNullOrEmpty(JpgPath) && System.IO.File.Exists(JpgPath);

    /// <summary>
    /// 是否存在 RAW
    /// </summary>
    public bool HasRaw => !string.IsNullOrEmpty(RawPath) && System.IO.File.Exists(RawPath);

    /// <summary>
    /// 更新文件类型
    /// </summary>
    private void UpdateFileType()
    {
        if (HasJpg && HasRaw)
            FileType = FileTypeEnum.Both;
        else if (HasJpg)
            FileType = FileTypeEnum.JpgOnly;
        else if (HasRaw)
            FileType = FileTypeEnum.RawOnly;
        else
            FileType = FileTypeEnum.JpgOnly;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 文件类型枚举
/// </summary>
public enum FileTypeEnum
{
    JpgOnly,
    RawOnly,
    Both
}

/// <summary>
/// 删除选项枚举
/// </summary>
public enum DeleteOptionEnum
{
    Both,
    JpgOnly,
    RawOnly
}

/// <summary>
/// 导出选项枚举
/// </summary>
public enum ExportOptionEnum
{
    All,
    RatedOnly,
    JpgOnly,
    RawOnly,
    Both
}
