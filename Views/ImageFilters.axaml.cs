using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ScanEditor.ViewModels;

namespace ScanEditor.Views;

public partial class ImageFilters : Window
{
    public ImageFilters()
    {
        InitializeComponent();
    }

    private void DenoiseFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageRedactorViewModel vm)
        {
            vm.DenoiseFilter();
        }
    }

    private void SharpenFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageRedactorViewModel vm)
        {
            vm.SharpenFilter();
        }
    }

    private void BlurFilterButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ImageRedactorViewModel vm)
        {
            vm.BlurFilter();
        }
    }
}