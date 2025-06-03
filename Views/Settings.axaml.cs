using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ScanEditor.ViewModels;

namespace ScanEditor.Views;

public partial class Settings : Window
{
    private bool isLightTheme;
    public Settings()
    {
        InitializeComponent();
        DataContext = SettingsViewModel.Instance;
    }

    private void ApplyButtonClick(object? sender, RoutedEventArgs e)
    {
        SettingsViewModel.Instance.SaveToFile();
        Close();
    }
    private void CancelButtonClick(object? sender, RoutedEventArgs e)
    {
        SettingsViewModel.Instance.CancelChanges();
        Close();
    }
    
    private void ResetButtonClick(object? sender, RoutedEventArgs e)
    {
        SettingsViewModel.Instance.ResetToDefault();
    }

    public void ChangeTheme_ButtonOnClick(object? sender, RoutedEventArgs e)
    {
        var res = App.Current.Resources.MergedDictionaries;
        isLightTheme = SettingsViewModel.Instance.IsLight;
        if (isLightTheme)
        {
            res.RemoveAt(0);
            var rd1 = (ResourceDictionary)AvaloniaXamlLoader.Load(
                new Uri("avares://ScanEditor/Styles/Dark.axaml"));
            res.Add(rd1);
            SettingsViewModel.Instance.IsLight = false;
        }
        else
        {
            res.RemoveAt(0);
            var rd1 = (ResourceDictionary)AvaloniaXamlLoader.Load(
                new Uri("avares://ScanEditor/Styles/Light.axaml"));
            res.Add(rd1);
            SettingsViewModel.Instance.IsLight = true;
        }
    }
    private void OnNumericUpDownValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (sender is NumericUpDown numericUpDown)
        {
            if (!numericUpDown.Value.HasValue || string.IsNullOrEmpty(numericUpDown.Text))
            {
                numericUpDown.Value = 0;
            }
        }
    }
}