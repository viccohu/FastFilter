using FastPick.Models;
using FastPick.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FastPick.ViewModels;

/// <summary>
/// 主视图模型 - 管理应用状态和数据绑定
/// </summary>
public class MainViewModel : INotifyPropertyChanged
{
    private readonly ImageScanService _imageScanService;
    private readonly ThumbnailService _thumbnailService;
    private readonly JpgMetadataService _jpgMetadataService;
    private readonly RawMetadataService _rawMetadataService;
    private readonly PreviewImageService _previewImageService;

    // 图片列表
    public ObservableCollection<PhotoItem> PhotoItems { get; } = new();

    // 选中图片列表
    public ObservableCollection<PhotoItem> SelectedItems { get; } = new();

    // 预删除列表
    public ObservableCollection<PhotoItem> MarkedForDeletionItems { get; } = new();

    // 当前预览的图片
    private PhotoItem? _currentPreviewItem;
    public PhotoItem? CurrentPreviewItem
    {
        get => _currentPreviewItem;
        set
        {
            if (_currentPreviewItem != value)
            {
                _currentPreviewItem = value;
                OnPropertyChanged();
            }
        }
    }

    // 路径1
    private string _path1 = string.Empty;
    public string Path1
    {
        get => _path1;
        set
        {
            if (_path1 != value)
            {
                _path1 = value;
                OnPropertyChanged();
            }
        }
    }

    // 路径2
    private string _path2 = string.Empty;
    public string Path2
    {
        get => _path2;
        set
        {
            if (_path2 != value)
            {
                _path2 = value;
                OnPropertyChanged();
            }
        }
    }

    // 是否正在加载
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    // 加载进度消息
    private string _loadingMessage = string.Empty;
    public string LoadingMessage
    {
        get => _loadingMessage;
        set
        {
            if (_loadingMessage != value)
            {
                _loadingMessage = value;
                OnPropertyChanged();
            }
        }
    }

    // 预删除数量
    public int MarkedForDeletionCount => MarkedForDeletionItems.Count;

    // 总图片数量
    public int TotalCount => PhotoItems.Count;

    // 选中数量
    public int SelectedCount => SelectedItems.Count;

    public MainViewModel()
    {
        _imageScanService = new ImageScanService();
        _thumbnailService = new ThumbnailService();
        _jpgMetadataService = new JpgMetadataService();
        _rawMetadataService = new RawMetadataService();
        _previewImageService = new PreviewImageService();
    }

    /// <summary>
    /// 加载图片
    /// </summary>
    public async Task LoadPhotosAsync(IProgress<(int current, int total, string message)>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_imageScanService.IsPathValid(Path1))
            return;

        IsLoading = true;
        LoadingMessage = "正在扫描图片...";

        try
        {
            // 清空现有数据
            PhotoItems.Clear();
            SelectedItems.Clear();
            MarkedForDeletionItems.Clear();
            await _thumbnailService.ClearCacheAsync();

            // 扫描图片
            var items = await _imageScanService.ScanPathsAsync(Path1, Path2, progress, cancellationToken);

            // 添加到集合
            foreach (var item in items)
            {
                PhotoItems.Add(item);
            }

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(MarkedForDeletionCount));
        }
        finally
        {
            IsLoading = false;
            LoadingMessage = string.Empty;
        }
    }

    /// <summary>
    /// 设置图片评级
    /// </summary>
    public async Task SetRatingAsync(PhotoItem item, int rating)
    {
        if (item == null || rating < 0 || rating > 5)
            return;

        item.Rating = rating;

        // 写入元数据
        await WriteRatingToMetadataAsync(item, rating);
    }

    /// <summary>
    /// 写入评级到元数据
    /// </summary>
    private async Task WriteRatingToMetadataAsync(PhotoItem item, int rating)
    {
        // JPG: 写入 Exif
        if (item.HasJpg)
        {
            await _jpgMetadataService.WriteRatingAsync(item.JpgPath, rating);
        }

        // RAW: 写入 ADS
        if (item.HasRaw && item.RawPath != null)
        {
            _rawMetadataService.WriteRating(item.RawPath, rating);
        }
    }

    /// <summary>
    /// 从元数据读取评级
    /// </summary>
    public async Task<int> ReadRatingFromMetadataAsync(PhotoItem item)
    {
        int rating = 0;

        // 优先从 JPG 读取
        if (item.HasJpg)
        {
            rating = await _jpgMetadataService.ReadRatingAsync(item.JpgPath);
        }

        // 如果没有 JPG 或有 RAW，从 RAW 读取
        if (rating == 0 && item.HasRaw && item.RawPath != null)
        {
            rating = _rawMetadataService.ReadRating(item.RawPath);
        }

        return rating;
    }

    /// <summary>
    /// 批量设置评级
    /// </summary>
    public async Task SetRatingForSelectedAsync(int rating)
    {
        foreach (var item in SelectedItems.ToList())
        {
            await SetRatingAsync(item, rating);
        }
    }

    /// <summary>
    /// 切换预删除标记
    /// </summary>
    public void ToggleMarkForDeletion(PhotoItem item)
    {
        if (item == null)
            return;

        item.IsMarkedForDeletion = !item.IsMarkedForDeletion;

        if (item.IsMarkedForDeletion)
        {
            if (!MarkedForDeletionItems.Contains(item))
                MarkedForDeletionItems.Add(item);
        }
        else
        {
            MarkedForDeletionItems.Remove(item);
        }

        OnPropertyChanged(nameof(MarkedForDeletionCount));
    }

    /// <summary>
    /// 批量切换预删除标记
    /// </summary>
    public void ToggleMarkForDeletionForSelected()
    {
        foreach (var item in SelectedItems.ToList())
        {
            ToggleMarkForDeletion(item);
        }
    }

    /// <summary>
    /// 清空预删除列表
    /// </summary>
    public void ClearMarkedForDeletion()
    {
        foreach (var item in MarkedForDeletionItems)
        {
            item.IsMarkedForDeletion = false;
        }
        MarkedForDeletionItems.Clear();
        OnPropertyChanged(nameof(MarkedForDeletionCount));
    }

    /// <summary>
    /// 执行删除
    /// </summary>
    public async Task ExecuteDeletionAsync(DeleteOptionEnum option)
    {
        var itemsToDelete = MarkedForDeletionItems.ToList();

        foreach (var item in itemsToDelete)
        {
            try
            {
                // 删除文件
                if (option == DeleteOptionEnum.Both || option == DeleteOptionEnum.JpgOnly)
                {
                    if (item.HasJpg)
                        await DeleteFileToRecycleBinAsync(item.JpgPath);
                }

                if (option == DeleteOptionEnum.Both || option == DeleteOptionEnum.RawOnly)
                {
                    if (item.HasRaw && item.RawPath != null)
                        await DeleteFileToRecycleBinAsync(item.RawPath);
                }

                // 从列表移除
                PhotoItems.Remove(item);
                MarkedForDeletionItems.Remove(item);
                SelectedItems.Remove(item);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除文件失败: {ex.Message}");
            }
        }

        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(MarkedForDeletionCount));
    }

    /// <summary>
    /// 删除文件到回收站
    /// </summary>
    private async Task DeleteFileToRecycleBinAsync(string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                if (File.Exists(filePath))
                {
                    // 使用 FileOperationService 移动到回收站
                    FileOperationService.MoveToRecycleBin(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除文件失败: {filePath}, {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// 选择/取消选择图片
    /// </summary>
    public void SelectItem(PhotoItem item, bool isSelected)
    {
        if (item == null)
            return;

        item.IsSelected = isSelected;

        if (isSelected)
        {
            if (!SelectedItems.Contains(item))
                SelectedItems.Add(item);
        }
        else
        {
            SelectedItems.Remove(item);
        }

        OnPropertyChanged(nameof(SelectedCount));

        // 更新当前预览
        if (isSelected && SelectedItems.Count == 1)
        {
            CurrentPreviewItem = item;
        }
    }

    /// <summary>
    /// 全选
    /// </summary>
    public void SelectAll()
    {
        SelectedItems.Clear();
        foreach (var item in PhotoItems)
        {
            item.IsSelected = true;
            SelectedItems.Add(item);
        }
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// 取消全选
    /// </summary>
    public void DeselectAll()
    {
        foreach (var item in SelectedItems)
        {
            item.IsSelected = false;
        }
        SelectedItems.Clear();
        OnPropertyChanged(nameof(SelectedCount));
    }

    /// <summary>
    /// 获取缩略图
    /// </summary>
    public async Task<object?> GetThumbnailAsync(PhotoItem item)
    {
        return await _thumbnailService.GetThumbnailAsync(item);
    }

    /// <summary>
    /// 加载预览图
    /// </summary>
    public async Task<Microsoft.UI.Xaml.Media.Imaging.BitmapImage?> LoadPreviewAsync(PhotoItem item)
    {
        return await _previewImageService.LoadPreviewAsync(item);
    }

    /// <summary>
    /// 加载多选预览图（前5张）
    /// </summary>
    public async Task<List<Microsoft.UI.Xaml.Media.Imaging.BitmapImage>> LoadMultiPreviewAsync(int maxCount = 5)
    {
        return await _previewImageService.LoadMultiPreviewAsync(SelectedItems.ToList(), maxCount);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
