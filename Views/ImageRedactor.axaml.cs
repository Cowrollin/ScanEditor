using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using ScanEditor.ViewModels;

namespace ScanEditor.Views;

public partial class ImageRedactor : Window
{
    private bool IsSave = false;
    public ImageRedactor()
    {
        InitializeComponent();
        this.Closing += ClosingButton_OnClick;
    }

    private void Image_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is ImageRedactorViewModel vm && sender is Control imageControl)
        {
            vm.HandleMouseWheel(e, imageControl);
        }
    }
    
    private void Image_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ImageRedactorViewModel vm && sender is Control imageControl)
        {
            vm.HandleMouseDown(e, imageControl);
        }
    }

    private void Image_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is ImageRedactorViewModel vm && sender is Control imageControl)
        {
            vm.HandleMouseMove(e, imageControl);
        }
    }

    private void Image_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is ImageRedactorViewModel vm)
        {
            vm.HandleMouseUp(e);
        }
    }
    
    private void ClosingButton_OnClick(object? sender, WindowClosingEventArgs e)
    {
        ImageRedactorViewModel viewModel = (ImageRedactorViewModel)DataContext;
        if (IsSave)
        {
            viewModel.SaveButtonClicked();
        }
        else
        {
            viewModel.CloseButtonClicked();
        }
    }
    
    private void CloseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IsSave = false;
        Close();
    }

    private void SaveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        IsSave = true;
        Close();
    }

    private void OnSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        ImageRedactorViewModel viewModel = (ImageRedactorViewModel)DataContext;
        viewModel.OnSliderValueChanged();
    }

    private void AICButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ImageRedactorViewModel viewModel = (ImageRedactorViewModel)DataContext;
        viewModel.AiColorizeButtonClicked(this);
    }

    private void FilterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        ImageRedactorViewModel viewModel = (ImageRedactorViewModel)DataContext;
        viewModel.OpenFilterWindow(this);
    }
    
    private void OnNumericUpDownValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (sender is NumericUpDown numericUpDown)
        {
            if (!numericUpDown.Value.HasValue || string.IsNullOrEmpty(numericUpDown.Text))
            {
                numericUpDown.Value = 100;
            }
        }
    }
}