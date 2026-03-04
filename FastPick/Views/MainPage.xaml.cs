using FastPick.Controls;
using FastPick.Models;
using FastPick.Services;
using FastPick.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FastPick.Views
{
    /// <summary>
    /// FastPick 主页面 - 五段式布局界面
    /// </summary>
    public partial class MainPage : Page
    {
        private Border _currentDrawer = null;
        private Window _window;
        private AppWindow _appWindow;
        private MainViewModel _viewModel;
        private KeyboardService _keyboardService;
        private CancellationTokenSource? _previewLoadCts;

        public MainPage()
        {
            this.InitializeComponent();
            _window = App.Window;
            
            // 初始化 ViewModel
            _viewModel = new MainViewModel();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            // 初始化键盘服务
            _keyboardService = new KeyboardService(_viewModel);
            _keyboardService.Register(_window);
            
            // 初始化窗口设置
            InitializeWindow();
            
            // 设置标题栏拖拽
            SetupTitleBarDrag();
            
            // 绑定缩略图列表
            SetupThumbnailList();
            
            // 绑定数据
            this.DataContext = _viewModel;
        }

        #region 数据绑定

        /// <summary>
        /// ViewModel 属性变化事件
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.CurrentPreviewItem):
                    UpdatePreview();
                    break;
                case nameof(MainViewModel.MarkedForDeletionCount):
                    UpdateDeleteCount();
                    break;
                case nameof(MainViewModel.IsLoading):
                    UpdateLoadingState();
                    break;
                case nameof(MainViewModel.LoadingMessage):
                    UpdateLoadingMessage();
                    break;
            }
        }

        /// <summary>
        /// 设置缩略图列表
        /// </summary>
        private void SetupThumbnailList()
        {
            // 绑定 ItemsRepeater 数据源
            ThumbnailRepeater.ItemsSource = _viewModel.PhotoItems;
            
            // 监听元素准备事件
            ThumbnailRepeater.ElementPrepared += ThumbnailRepeater_ElementPrepared;
            ThumbnailRepeater.ElementClearing += ThumbnailRepeater_ElementClearing;
        }

        /// <summary>
        /// 缩略图区滚轮事件 - 将垂直滚动转换为水平滚动
        /// </summary>
        private void ThumbnailScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer == null) return;

            var pointer = e.GetCurrentPoint(scrollViewer);
            var delta = pointer.Properties.MouseWheelDelta;

            // 将滚轮增量应用到水平滚动
            scrollViewer.ChangeView(
                scrollViewer.HorizontalOffset - delta,
                null,
                null,
                true
            );

            e.Handled = true;
        }

        /// <summary>
        /// ItemsRepeater 元素准备事件 - 虚拟滚动核心
        /// </summary>
        private async void ThumbnailRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
        {
            if (args.Element is Grid thumbnailGrid)
            {
                var photoItem = _viewModel.PhotoItems[args.Index];
                
                // 设置数据上下文
                thumbnailGrid.DataContext = photoItem;
                
                // 绑定点击事件
                thumbnailGrid.PointerPressed += (s, e) =>
                {
                    var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    var shiftPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    
                    _keyboardService.HandleThumbnailClick(photoItem, ctrlPressed, shiftPressed);
                    e.Handled = true;
                };
                
                // 直接加载缩略图并设置到 Image 控件
                await LoadThumbnailToImageAsync(thumbnailGrid, photoItem);
            }
        }

        /// <summary>
        /// 加载缩略图并直接设置到 Image 控件
        /// </summary>
        private async Task LoadThumbnailToImageAsync(Grid thumbnailGrid, PhotoItem photoItem)
        {
            try
            {
                var image = thumbnailGrid.FindName("ThumbnailImage") as Image;
                if (image == null) return;
                
                // 先检查是否已有缓存
                if (photoItem.Thumbnail is Microsoft.UI.Xaml.Media.Imaging.BitmapImage cachedBitmap)
                {
                    image.Source = cachedBitmap;
                    return;
                }
                
                // 异步加载缩略图
                var thumbnail = await _viewModel.GetThumbnailAsync(photoItem);
                if (thumbnail is Microsoft.UI.Xaml.Media.Imaging.BitmapImage bitmap)
                {
                    // 缓存到 PhotoItem
                    photoItem.Thumbnail = bitmap;
                    
                    // 直接设置到 Image 控件
                    image.Source = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载缩略图失败: {ex.Message}");
            }
        }

        /// <summary>
        /// ItemsRepeater 元素清理事件 - 释放资源
        /// </summary>
        private void ThumbnailRepeater_ElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
        {
            if (args.Element is Grid thumbnailGrid)
            {
                // 清理图片资源
                var image = thumbnailGrid.FindName("ThumbnailImage") as Image;
                if (image != null)
                {
                    image.Source = null;
                }
            }
        }

        /// <summary>
        /// 更新预览区
        /// </summary>
        private void UpdatePreview()
        {
            var item = _viewModel.CurrentPreviewItem;
            if (item == null)
            {
                PreviewImage.Source = null;
                FileNameTextBlock.Text = "文件名: -";
                FilePathTextBlock.Text = "路径: -";
                FileSizeTextBlock.Text = "大小: -";
                FileDimensionsTextBlock.Text = "尺寸: -";
                FileRatingTextBlock.Text = "评级: -";
                FileTypeTextBlock.Text = "类型: -";
                FileDateTextBlock.Text = "修改日期: -";
                return;
            }

            // 显示图片
            LoadPreviewImage(item);
            
            // 更新文件信息
            FileNameTextBlock.Text = $"文件名: {item.FileName}";
            FilePathTextBlock.Text = $"路径: {item.DisplayPath}";
            FileSizeTextBlock.Text = $"大小: {FormatFileSize(item.FileSize)}";
            FileDimensionsTextBlock.Text = $"尺寸: {item.Dimensions ?? "-"}";
            FileRatingTextBlock.Text = $"评级: {item.Rating} 星";
            FileTypeTextBlock.Text = $"类型: {GetFileTypeText(item)}";
            FileDateTextBlock.Text = $"修改日期: {item.ModifiedDate:yyyy-MM-dd HH:mm}";
        }

        /// <summary>
        /// 加载预览图
        /// </summary>
        private async void LoadPreviewImage(PhotoItem item)
        {
            // 取消之前的加载任务
            if (_previewLoadCts != null)
            {
                _previewLoadCts.Cancel();
                _previewLoadCts.Dispose();
            }
            _previewLoadCts = new CancellationTokenSource();
            var token = _previewLoadCts.Token;

            try
            {
                // 释放之前的预览图资源
                if (PreviewImage.Source is BitmapImage oldBitmap)
                {
                    oldBitmap.UriSource = null;
                    PreviewImage.Source = null;
                }

                // 使用 PreviewImageService 加载预览图
                var bitmap = await _viewModel.LoadPreviewAsync(item);
                
                // 检查是否被取消或当前项已改变
                if (token.IsCancellationRequested || _viewModel.CurrentPreviewItem != item)
                {
                    return;
                }

                if (bitmap != null)
                {
                    PreviewImage.Source = bitmap;
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载预览图失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载多选预览图（前5张）
        /// </summary>
        private async void LoadMultiPreviewImages()
        {
            try
            {
                // 清空当前预览
                PreviewImage.Source = null;
                
                // TODO: 实现多选预览平铺显示
                // 暂时只显示第一张
                if (_viewModel.SelectedItems.Count > 0)
                {
                    var firstItem = _viewModel.SelectedItems[0];
                    var bitmap = await _viewModel.LoadPreviewAsync(firstItem);
                    if (bitmap != null)
                    {
                        PreviewImage.Source = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载多选预览失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新预删除数量
        /// </summary>
        private void UpdateDeleteCount()
        {
            DeleteCountRun.Text = _viewModel.MarkedForDeletionCount.ToString();
        }

        /// <summary>
        /// 更新加载状态
        /// </summary>
        private void UpdateLoadingState()
        {
            LoadingRing.IsActive = _viewModel.IsLoading;
        }

        /// <summary>
        /// 更新加载消息
        /// </summary>
        private void UpdateLoadingMessage()
        {
            // 可以在这里显示加载进度消息
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        /// <summary>
        /// 获取文件类型文本
        /// </summary>
        private string GetFileTypeText(PhotoItem item)
        {
            if (item.HasJpg && item.HasRaw) return "JPG + RAW";
            if (item.HasRaw) return "RAW";
            return "JPG";
        }

        #endregion

        #region 窗口初始化

        /// <summary>
        /// 初始化窗口设置（无边框）
        /// </summary>
        private void InitializeWindow()
        {
            if (_window == null) return;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                _appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
                
                var titleBar = _appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
                
                titleBar.BackgroundColor = Microsoft.UI.Colors.Transparent;
                titleBar.ForegroundColor = Microsoft.UI.Colors.White;
                titleBar.InactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                titleBar.InactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0xB0, 0xB0, 0xB0);
                
                titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonHoverBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x38, 0x38, 0x38);
                titleBar.ButtonHoverForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonPressedBackgroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x44, 0x44, 0x44);
                titleBar.ButtonPressedForegroundColor = Microsoft.UI.Colors.White;
                titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                titleBar.ButtonInactiveForegroundColor = Microsoft.UI.ColorHelper.FromArgb(0xFF, 0x66, 0x66, 0x66);
            }
        }

        /// <summary>
        /// 设置标题栏拖拽功能
        /// </summary>
        private void SetupTitleBarDrag()
        {
            if (_appWindow == null) return;
            
            _window.SizeChanged += (s, e) => UpdateTitleBarDragRectangles();
            TitleBarGrid.SizeChanged += (s, e) => UpdateTitleBarDragRectangles();
            TitleBarGrid.Loaded += (s, e) => UpdateTitleBarDragRectangles();
        }

        /// <summary>
        /// 更新标题栏拖拽区域
        /// </summary>
        private void UpdateTitleBarDragRectangles()
        {
            if (_appWindow == null || TitleBarGrid == null) return;

            var scale = _window.Content.XamlRoot.RasterizationScale;
            var titleBarHeight = TitleBarGrid.ActualHeight;

            var leftButtonsWidth = GetLeftButtonsWidth();
            var rightAreaWidth = GetRightNonDragWidth();

            var dragRects = new System.Collections.Generic.List<Windows.Graphics.RectInt32>();

            var centerStart = (int)(leftButtonsWidth * scale);
            var centerEnd = (int)((TitleBarGrid.ActualWidth - rightAreaWidth) * scale);
            if (centerEnd > centerStart)
            {
                var centerRect = new Windows.Graphics.RectInt32(centerStart, 0, centerEnd - centerStart, (int)(titleBarHeight * scale));
                dragRects.Add(centerRect);
            }

            _appWindow.TitleBar.SetDragRectangles(dragRects.ToArray());
        }

        private double GetLeftButtonsWidth()
        {
            double width = 0;
            if (TitleBarButtonsPanel != null)
            {
                var transform = TitleBarButtonsPanel.TransformToVisual(TitleBarGrid);
                var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, TitleBarButtonsPanel.ActualWidth, TitleBarButtonsPanel.ActualHeight));
                width = bounds.Right + 8;
            }
            return width;
        }

        private double GetRightNonDragWidth()
        {
            return 180;
        }

        #endregion

        #region 抽屉管理

        /// <summary>
        /// 切换抽屉显示状态
        /// </summary>
        private void ToggleDrawer(Border drawerToShow)
        {
            // 如果点击的是当前已打开的抽屉，则关闭它
            if (_currentDrawer == drawerToShow && drawerToShow.Visibility == Visibility.Visible)
            {
                drawerToShow.Visibility = Visibility.Collapsed;
                _currentDrawer = null;
                return;
            }

            // 隐藏所有抽屉
            CloseAllDrawers();

            // 显示指定的抽屉
            if (drawerToShow != null)
            {
                drawerToShow.Visibility = Visibility.Visible;
                _currentDrawer = drawerToShow;
            }
        }

        /// <summary>
        /// 关闭所有抽屉
        /// </summary>
        private void CloseAllDrawers()
        {
            PathDrawer.Visibility = Visibility.Collapsed;
            ExportDrawer.Visibility = Visibility.Collapsed;
            DeleteDrawer.Visibility = Visibility.Collapsed;
            FilterDrawer.Visibility = Visibility.Collapsed;
            SettingsDrawer.Visibility = Visibility.Collapsed;
            _currentDrawer = null;
        }

        /// <summary>
        /// 切换右侧文件信息抽屉
        /// </summary>
        private void ToggleFileInfoDrawer()
        {
            if (FileInfoDrawer.Visibility == Visibility.Visible)
            {
                FileInfoDrawer.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 关闭其他抽屉
                CloseAllDrawers();
                FileInfoDrawer.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 页面点击事件 - 点击空白区域关闭抽屉
        /// </summary>
        private void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // 获取点击位置
            var point = e.GetCurrentPoint(this).Position;
            
            // 检查点击是否在抽屉区域外
            bool clickedOutsideDrawers = true;
            
            // 检查顶部抽屉
            if (_currentDrawer != null && _currentDrawer.Visibility == Visibility.Visible)
            {
                var drawerBounds = GetElementBounds(_currentDrawer);
                if (drawerBounds.Contains(point))
                {
                    clickedOutsideDrawers = false;
                }
            }
            
            // 检查文件信息抽屉
            if (FileInfoDrawer.Visibility == Visibility.Visible)
            {
                var fileInfoBounds = GetElementBounds(FileInfoDrawer);
                if (fileInfoBounds.Contains(point))
                {
                    clickedOutsideDrawers = false;
                }
            }
            
            // 检查标题栏按钮区域（点击按钮时不关闭抽屉）
            var titleBarBounds = GetElementBounds(TitleBarButtonsPanel);
            if (titleBarBounds.Contains(point))
            {
                clickedOutsideDrawers = false;
            }
            
            // 如果点击在抽屉外，关闭所有抽屉
            if (clickedOutsideDrawers)
            {
                CloseAllDrawers();
                FileInfoDrawer.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 获取元素在页面中的边界
        /// </summary>
        private Windows.Foundation.Rect GetElementBounds(FrameworkElement element)
        {
            if (element == null) return new Windows.Foundation.Rect(0, 0, 0, 0);
            
            var transform = element.TransformToVisual(this);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
            return new Windows.Foundation.Rect(position.X, position.Y, element.ActualWidth, element.ActualHeight);
        }

        #endregion

        #region 标题栏按钮事件

        private void PathButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleDrawer(PathDrawer);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleDrawer(ExportDrawer);
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleDrawer(DeleteDrawer);
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleDrawer(FilterDrawer);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleDrawer(SettingsDrawer);
        }

        #endregion

        #region 窗口控制按钮事件

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appWindow != null)
            {
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                presenter?.Minimize();
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_appWindow != null)
            {
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    if (presenter.State == OverlappedPresenterState.Maximized)
                    {
                        presenter.Restore();
                    }
                    else
                    {
                        presenter.Maximize();
                    }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _window?.Close();
        }

        #endregion

        #region 路径抽屉事件

        private async void SelectPath1Button_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            folderPicker.FileTypeFilter.Add("*");

            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, 
                WinRT.Interop.WindowNative.GetWindowHandle(_window));

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                Path1TextBox.Text = folder.Path;
                _viewModel.Path1 = folder.Path;
            }
        }

        private async void SelectPath2Button_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            folderPicker.FileTypeFilter.Add("*");

            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker,
                WinRT.Interop.WindowNative.GetWindowHandle(_window));

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                Path2TextBox.Text = folder.Path;
                _viewModel.Path2 = folder.Path;
            }
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            // 更新 ViewModel 路径
            _viewModel.Path1 = Path1TextBox.Text;
            _viewModel.Path2 = Path2TextBox.Text;
            
            // 加载图片
            var progress = new Progress<(int current, int total, string message)>(p =>
            {
                ShowToast($"{p.message} ({p.current}/{p.total})");
            });
            
            await _viewModel.LoadPhotosAsync(progress);
            ShowToast($"已加载 {_viewModel.TotalCount} 张照片");
        }

        #endregion

        #region 导出抽屉事件

        private async void BrowseExportPathButton_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker,
                WinRT.Interop.WindowNative.GetWindowHandle(_window));

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                ExportPathTextBox.Text = folder.Path;
            }
        }

        private void ExecuteExportButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现导出逻辑
            var rating = ExportRatingComboBox.SelectedItem as ComboBoxItem;
            ShowToast($"导出设置:\n评级: {rating?.Content}\nJPG文件夹: {JpgFolderTextBox.Text}\nRAW文件夹: {RawFolderTextBox.Text}\n导出路径: {ExportPathTextBox.Text}");
        }

        #endregion

        #region 删除抽屉事件

        private async void ExecuteDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var deleteOption = DeleteBothRadio.IsChecked == true ? DeleteOptionEnum.Both :
                              DeleteJpgOnlyRadio.IsChecked == true ? DeleteOptionEnum.JpgOnly : 
                              DeleteOptionEnum.RawOnly;
            
            await _viewModel.ExecuteDeletionAsync(deleteOption);
            ShowToast($"已删除 {_viewModel.MarkedForDeletionCount} 张照片");
        }

        #endregion

        #region 筛选抽屉事件

        private void ApplyFilterButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现筛选逻辑
            var fileType = FilterAllRadio.IsChecked == true ? "全部" :
                          FilterBothRadio.IsChecked == true ? "双文件" :
                          FilterJpgOnlyRadio.IsChecked == true ? "仅JPG" : "仅RAW";
            
            var ratingFilter = FilterRatingComboBox.SelectedItem as ComboBoxItem;
            var starCount = FilterStarCountComboBox.SelectedItem as ComboBoxItem;
            
            ShowToast($"应用筛选:\n文件类型: {fileType}\n星级: {ratingFilter?.Content} {starCount?.Content}");
        }

        #endregion

        #region 设置抽屉事件

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("设置已保存");
            ToggleDrawer(null);
        }

        private void CancelSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleDrawer(null);
        }

        #endregion

        #region 底部工具栏事件

        private void FileInfoButton_Click(object sender, RoutedEventArgs e)
        {
            // 切换右侧文件信息抽屉
            ToggleFileInfoDrawer();
        }

        private void FitToWindowButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现适应窗口逻辑
            ShowToast("适应窗口大小");
        }

        private void OpenInExplorerButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 在资源管理器中打开文件
            ShowToast("在资源管理器中打开");
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 显示 Toast 提示
        /// </summary>
        private void ShowToast(string message)
        {
            // TODO: 实现 Toast 提示
            System.Diagnostics.Debug.WriteLine($"Toast: {message}");
        }

        #endregion
    }
}
