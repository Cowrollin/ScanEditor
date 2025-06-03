using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ScanEditor.ViewModels;

namespace ScanEditor.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var isLightTheme = SettingsViewModel.Instance.IsLight;
        var res = App.Current.Resources.MergedDictionaries;
        if (!isLightTheme)
        {
            res.RemoveAt(0);
            var rd1 = (ResourceDictionary)AvaloniaXamlLoader.Load(
                new Uri("avares://ScanEditor/Styles/Dark.axaml"));
            res.Add(rd1);
            SettingsViewModel.Instance.IsLight = false;
        }

        this.Closing += MainWindowClosed;
    }
    
    private void SettingsButtonClicked(object? sender, RoutedEventArgs e)
    {
        var existingSettingsWindow = ((IClassicDesktopStyleApplicationLifetime)App.Current.ApplicationLifetime).Windows.OfType<Settings>().FirstOrDefault();
    
        if (existingSettingsWindow != null)
        {
            existingSettingsWindow.Activate();
            return;
        }
        
        Settings settingsWindow = new();
        settingsWindow.Show();
        var vm = SettingsViewModel.Instance;
        vm.OpenSettings();
    }

    private void MainWindowClosed(object? sender, WindowClosingEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            foreach (var window in desktopLifetime.Windows.ToList())
            {
                if (window != this)
                    window.Close();
            }
        }
    }
    
    private void Image_OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is Control imageControl)
        {
            vm.HandleMouseWheel(e, imageControl);
        }
    }
    
    private void Image_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is Control imageControl)
        {
            vm.HandleMouseDown(e, imageControl);
        }
    }

    private void Image_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && sender is Control imageControl)
        {
            vm.HandleMouseMove(e, imageControl);
        }
    }

    private void Image_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.HandleMouseUp(e);
        }
    }
}