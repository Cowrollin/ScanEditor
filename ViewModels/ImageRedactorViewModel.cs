using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ScanEditor.Views;

namespace ScanEditor.ViewModels;

public partial class ImageRedactorViewModel: ObservableObject
{
    [ObservableProperty] private WriteableBitmap _wrtBitmap;
    [ObservableProperty] private int _hue;
    [ObservableProperty] private int _saturation;
    [ObservableProperty] private int _brightness;
    [ObservableProperty] private int _contrast;
    [ObservableProperty] private double _zoomLevel = 1.0;
    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;
    private Point? _dragStartPosition;
    private bool _isDragging;
    private readonly ResImage _image;
    private ImageFilters? _imageFiltersWindow;
    private readonly int OriginHue;
    private readonly int OriginSaturation;
    private readonly int OriginBrightness;
    private readonly int OriginContrast;
    private double BorderWidthClamp = 300;
    private double BorderHeightClamp = 200;
    
    public ImageRedactorViewModel(ResImage image)
    {
        _image = image;
        WrtBitmap = image.BaseImage;
        Hue = image.Hue; OriginHue = image.Hue;
        Saturation = image.Saturation; OriginSaturation = image.Saturation;
        Brightness = image.Brightness; OriginBrightness = image.Brightness;
        Contrast = image.Contrast; OriginContrast = image.Contrast;
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
            OffsetX += currentPos.X - startPos.X;
            OffsetY += currentPos.Y - startPos.Y;
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

        var scaledWidth = BorderWidthClamp * ZoomLevel;
        var scaledHeight = BorderHeightClamp * ZoomLevel;

        var maxOffsetX = (scaledWidth - BorderWidthClamp) / 2;
        var maxOffsetY = (scaledHeight - BorderHeightClamp) / 2;

        OffsetX = Math.Clamp(OffsetX, -maxOffsetX, maxOffsetX);
        OffsetY = Math.Clamp(OffsetY, -maxOffsetY, maxOffsetY);
    }
    
    public void Rotate90()
    {
        _image.RotateImageOn90(true);
        Update();
    }
    
    public void RotateMinus90()
    {
        _image.RotateImageOn90(false);
        Update();
    }

    public void OpenFilterWindow(ImageRedactor owner)
    {
        if (_imageFiltersWindow == null)
        {
            _imageFiltersWindow = new ImageFilters
            {
                DataContext = this
            };
            _imageFiltersWindow.Closed += (_, _) => _imageFiltersWindow = null;
            _imageFiltersWindow.Show(owner);
        }
        else
        {
            _imageFiltersWindow.Activate();
        }
    }

    public void DenoiseFilter()
    {
        _image.DenoiseMedianFilter();
        Update();
    }

    public void SharpenFilter()
    {
        _image.SharpenFilter();
        Update();
    }

    public void BlurFilter()
    {
        _image.BlurFilter();
        Update();
    }
    
    public void OnSliderValueChanged()
    {
        _image.HueSaturationBrightnessContrastEditor(Hue, Saturation, Brightness, Contrast);
        Update();
    }

    public async void AiColorizeButtonClicked(ImageRedactor owner)
    {
        owner.IsEnabled = false;
        bool isProcessComplete = await _image.AiColorizeImage(SettingsViewModel.Instance.UseGpu);
        if (isProcessComplete)
        {
            await _image.SelectAicOutput(owner);
            Update();
        }
        owner.IsEnabled = true;
        // ~ 77377 ms //
    }
    
    public void HueResetToDefaultButtonClicked()
    {
        Hue = 0;
        OnSliderValueChanged();
    }
    
    public void SaturationResetToDefaultButtonClicked()
    {
        Saturation = 0;
        OnSliderValueChanged();
    }
    
    public void BrightnessResetToDefaultButtonClicked()
    {
        Brightness = 0;
        OnSliderValueChanged();
    }
    
    public void ContrastResetToDefaultButtonClicked()
    {
        Contrast = 0;
        OnSliderValueChanged();
    }

    public void ResetButtonClicked()
    {
        _image.ResetImage();
        Hue = 0;
        Saturation = 0;
        Brightness = 0;
        Contrast = 0;
        OnSliderValueChanged();
        Update();
    }
    
    public void CloseButtonClicked()
    {
        _image.ResetImage();
        Hue = OriginHue;
        Saturation = OriginSaturation;
        Brightness = OriginBrightness;
        Contrast = OriginContrast;
        OnSliderValueChanged();
        Update();
    }

    public void SaveButtonClicked()
    {
        _image.Save();
    }
    
    private void Update()
    {
        WrtBitmap = _image.BaseImage;
    }
}