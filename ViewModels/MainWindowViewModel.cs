using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using DynamicData;
using ReactiveUI;
using ScanEditor.Views;

namespace ScanEditor.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private WriteableBitmap _bitmapImage;
    [ObservableProperty] private bool _addButtonVisible = true;
    [ObservableProperty] private bool _isAicEnabled;
    [ObservableProperty] private double _zoomLevel = 1.0;
    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;
    [ObservableProperty] private bool _isLoad;
    private Point? _dragStartPosition;
    private bool _isDragging;
    private readonly double _borderWidthClamp = 327;
    private readonly double _borderHeightClamp = 466;
    public ICommand OpenImageRedactorButtonClicked { get; }
    public ObservableCollection<ResImage> OutputImages { get; set; }
    private ResImage? _imageScan; // Input Image
    private string _directorySavePath;
    private bool _isChanges;
    
    public MainWindowViewModel()
    {
        OutputImages = [];
        OpenImageRedactorButtonClicked = ReactiveCommand.Create<ResImage>(OpenImageRedactor);
        _directorySavePath = SettingsViewModel.Instance.DefaultPath;
        IsLoad = false;
        
        _isAicEnabled = true;
    }
    
    public void HandleMouseWheel(PointerWheelEventArgs e, Control imageControl)
    {
        var delta = e.Delta.Y > 0 ? 0.2 : -0.2;
        var newZoom = Math.Clamp(ZoomLevel + delta, 1.0, 5.0);
        if (newZoom < ZoomLevel)
        {
            var mousePos = e.GetPosition(imageControl);
            var relativeX = (mousePos.X - OffsetX) / ZoomLevel;
            var relativeY = (mousePos.Y - OffsetY) / ZoomLevel;
    
            OffsetX = mousePos.X - relativeX * newZoom;
            OffsetY = mousePos.Y - relativeY * newZoom;
        }
        
        if (newZoom == 1.0)
        {
            OffsetX = 0;
            OffsetY = 0;
        }
        ZoomLevel = newZoom;
        ClampOffsets();
    }
    
    public void HandleMouseDown(PointerPressedEventArgs e, Control imageControl)
    {
        if (e.GetCurrentPoint(imageControl).Properties.IsLeftButtonPressed && ZoomLevel > 1.0)
        {
            _dragStartPosition = e.GetPosition(imageControl);
            _isDragging = true;
        }
    }
    
    public void HandleMouseMove(PointerEventArgs e, Control imageControl)
    {
        if (_isDragging && _dragStartPosition is { } startPos)
        {
            var currentPos = e.GetPosition(imageControl);
            var newOffsetX = OffsetX + (currentPos.X - startPos.X);
            var newOffsetY = OffsetY + (currentPos.Y - startPos.Y);
            
            OffsetX = newOffsetX;
            OffsetY = newOffsetY;
        
            _dragStartPosition = currentPos;
            ClampOffsets();
        }
    }
    
    public void HandleMouseUp(PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _dragStartPosition = null;
        
        if (ZoomLevel <= 1.0)
        {
            OffsetX = 0;
            OffsetY = 0;
        }
        else
        {
            ClampOffsets();
        }
    }
    
    private void ClampOffsets()
    {
        if (ZoomLevel <= 1.0)
        {
            OffsetX = 0;
            OffsetY = 0;
            return;
        }

        var scaledWidth = _borderWidthClamp * ZoomLevel;
        var scaledHeight = _borderHeightClamp * ZoomLevel;

        var maxOffsetX = (scaledWidth - _borderWidthClamp) / 2;
        var maxOffsetY = (scaledHeight - _borderHeightClamp) / 2;

        OffsetX = Math.Clamp(OffsetX, -maxOffsetX, maxOffsetX);
        OffsetY = Math.Clamp(OffsetY, -maxOffsetY, maxOffsetY);
    }
    
    public async void AddScanButtonClicked()
    {
        _isChanges = false;
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Image",
                AllowMultiple = false,
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "Images", Extensions = { SettingsViewModel.Instance.Extensions } }
                }
            };
        
            var result = await dialog.ShowAsync(new Window());
            if (result != null && result.Length > 0)
            {
                string selectedFile = result[0];
                if (File.Exists(selectedFile))
                {
                    Bitmap bitmap = new Bitmap(selectedFile);
                    _imageScan = new ResImage(new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi));
                    using (var buf = _imageScan.BaseImage.Lock())
                    {
                        bitmap.CopyPixels(new PixelRect(0,0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), buf.Address, buf.RowBytes * buf.Size.Height, buf.RowBytes);
                    }
                    AddButtonVisible = false;
                    BitmapImage = _imageScan.BaseImage;
                }
            }
        }
        catch (Exception e)
        {
            throw; //
        }
    }

    public async void SaveAsButtonClicked()
    {
        var filters = SettingsViewModel.Instance.Filters.OrderByDescending(f => f.Extensions.Contains(SettingsViewModel.Instance.DefaultFileExtension)).ToList();
        var saveFileDialog = new SaveFileDialog
        {
            InitialFileName = SettingsViewModel.Instance.FileNamePattern,
            DefaultExtension = SettingsViewModel.Instance.DefaultFileExtension,
            Filters = filters
        };
        if (saveFileDialog == null) throw new ArgumentNullException(nameof(saveFileDialog));
        try
        {
            if (OutputImages.Count != 0)
            {
                var filepath = await saveFileDialog.ShowAsync(new Window());
                if (!string.IsNullOrEmpty(filepath))
                {
                    foreach (var item in OutputImages)
                    {
                        item.SaveImage(filepath);
                    }
                    OutputImages.Clear();
                    _directorySavePath = Path.GetDirectoryName(filepath);
                }
            }
            else if (_imageScan != null && _isChanges)
            {
                var filepath = await saveFileDialog.ShowAsync(new Window());
                if (!string.IsNullOrEmpty(filepath))
                {
                    _imageScan.SaveImage(filepath);
                    _directorySavePath = Path.GetDirectoryName(filepath);
                }
                _imageScan = null;
                Update();
                AddButtonVisible = true;
            }
        }
        catch (Exception e)
        {
            throw; //
        }
    }
    
    public void SaveButtonClicked()
    {
        var filepath = Path.Combine(_directorySavePath, $"{SettingsViewModel.Instance.FileNamePattern}.{SettingsViewModel.Instance.DefaultFileExtension}");
        if (OutputImages.Count != 0)
        {
            foreach (var item in OutputImages)
            {
                item.SaveImage(filepath);
            }
            OutputImages.Clear();
        }
        else if (_imageScan != null && _isChanges)
        {
            _imageScan.SaveImage(filepath);
            _imageScan = null;
            Update();
            AddButtonVisible = true;
        }
        else
        {
          
        }
    }

    private async void OpenImageRedactor(ResImage image)
    {
        try
        {
            ImageRedactor imageRedactorWindow = new ImageRedactor();
            var viewModel = new ImageRedactorViewModel(image);
            imageRedactorWindow.DataContext = viewModel;
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
            await imageRedactorWindow.ShowDialog(mainWindow);
        
            OutputImages[OutputImages.IndexOf(image)] = image;
        }
        catch (Exception e)
        {
            throw;
        }
    }
    
    public async Task FindBorderButtonClicked()
    {
        if (_imageScan == null)
        {
            return;
        }
        IsLoad = true;
        await Task.Delay(1);
        try
        {
            await Task.Run(FindImages);
            Update();
        }
        finally
        {
            IsLoad = false;
        }
    }

    public void RemoveImageButtonClicked(ResImage image)
    {
        if (image != null && OutputImages.Contains(image))
        {
            OutputImages.Remove(image);
        }
    }
    
    private void FindImages()
    {
        var imageBounds = _imageScan.GetImagesBounds(SettingsViewModel.Instance.WhiteSensitivity);
        var foundImages = new List<ResImage>();
        foreach (var item in imageBounds)
        {
            if (item.Width > SettingsViewModel.Instance.MinWidthPhoto && item.Height > SettingsViewModel.Instance.MinHeightPhoto)
            {
                var foundImage = new ResImage(_imageScan.CopyPixelToWriteableBitmap(item));
                foundImages.Add(foundImage);
            }
        }

        foreach (var image in foundImages)
        {
            var rotateAngle = image.FindRotateAngle(SettingsViewModel.Instance.WhiteSensitivity);

            if (rotateAngle >= 0.08 && SettingsViewModel.Instance.AutoRotate)
            {
                image.RotateImageOnAngle(-rotateAngle);
                image.CutBackground(SettingsViewModel.Instance.WhiteSensitivity);
                image.DenoiseMedianFilter();
            }
            OutputImages.Add(new ResImage(image.BaseImage)); 
        } 
    }
    
    private void Update()
    {
        BitmapImage = _imageScan?.BaseImage;
    }
}