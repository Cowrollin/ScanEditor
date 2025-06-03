using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Window = Avalonia.Controls.Window;

namespace ScanEditor.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly Lazy<SettingsViewModel> _instance = new(() => new SettingsViewModel());
    public static SettingsViewModel Instance => _instance.Value;
    private readonly string SettingsFilePath;
    private readonly string ApplicationDirectory = AppContext.BaseDirectory;
    
    [ObservableProperty]
    private int _minWidthPhoto;
    [ObservableProperty]
    private int _minHeightPhoto;
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(WhiteLineThresholdText))]
    private byte _whiteSensitivity;
    [ObservableProperty]
    private bool _useGpu;
    [ObservableProperty]
    private string _fileNamePattern;
    [ObservableProperty]
    private string _defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    [ObservableProperty]
    private bool _autoRotate;
    [ObservableProperty] 
    private bool _isLight;
    public List<string> Extensions { get; } = new() { "png", "jpg", "jpeg", "bmp" };
    private string _defaultFileExtension = "png";
    public string DefaultFileExtension
    {
        get => _defaultFileExtension;
        set => SetProperty(ref _defaultFileExtension, value);
    }
    
    public string WhiteLineThresholdText => WhiteSensitivity.ToString();
    public List<FileDialogFilter> Filters { get; } = new()
    {
        new FileDialogFilter { Name = "png files", Extensions = { "png" } },
        new FileDialogFilter { Name = "jpg files", Extensions = { "jpg" } },
        new FileDialogFilter { Name = "jpeg files", Extensions = { "jpeg" } },
        new FileDialogFilter { Name = "bmp files", Extensions = { "bmp" } }
    };
    
    private TempSettings _tempSettings;
    
    private SettingsViewModel()
    {
        while (ApplicationDirectory.Contains(@"\ScanEditor\"))
        {
            ApplicationDirectory = Directory.GetParent(ApplicationDirectory)?.FullName ?? ApplicationDirectory;
        }
        SettingsFilePath = Path.Combine(ApplicationDirectory, "Settings.txt");
        LoadFromFile();
        LoadTempSettings();
    }
    
    public void OpenSettings()
    {
        LoadFromFile();
    }
    
    public void SaveToFile()
    {
        try
        {
            var sb = new StringBuilder(200);
            sb.AppendLine($"MinWidthPhoto={MinWidthPhoto}")
                .AppendLine($"MinHeightPhoto={MinHeightPhoto}")
                .AppendLine($"WhiteSensivity={WhiteSensitivity}")
                .AppendLine($"UseGpu={UseGpu}")
                .AppendLine($"FileNamePattern={FileNamePattern}")
                .AppendLine($"DefaultPath={DefaultPath}")
                .AppendLine($"AutoRotate={AutoRotate}")
                .AppendLine($"DefaultFileExtension={DefaultFileExtension}")
                .AppendLine($"IsLight={IsLight}");
            
            using var fs = new FileStream(SettingsFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096);
            using var sw = new StreamWriter(fs);
            sw.Write(sb.ToString());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error save settings: {ex.Message}");
        }
    }

    private void LoadFromFile()
    {
        if (!File.Exists(SettingsFilePath))
        {
            ResetToDefault();
            return;
        }

        foreach (var line in File.ReadAllLines(SettingsFilePath))
        {
            var parts = line.Split('=');
            if (parts.Length != 2) continue;

            try
            {
                switch (parts[0])
                {
                    case "MinWidthPhoto": MinWidthPhoto = int.Parse(parts[1]); break;
                    case "MinHeightPhoto": MinHeightPhoto = int.Parse(parts[1]); break;
                    case "WhiteSensivity": WhiteSensitivity = byte.Parse(parts[1]); break;
                    case "UseGpu": UseGpu = bool.Parse(parts[1]); break;
                    case "FileNamePattern": FileNamePattern = parts[1]; break;
                    case "DefaultPath": DefaultPath = parts[1]; break;
                    case "AutoRotate": AutoRotate = bool.Parse(parts[1]); break;
                    case "DefaultFileExtension": DefaultFileExtension = parts[1]; break;
                    case "IsLight": IsLight = bool.Parse(parts[1]); break;
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }
    }

    private void LoadTempSettings()
    {
        _tempSettings = new TempSettings
        {
            MinWidthPhoto = MinWidthPhoto,
            MinHeightPhoto = MinHeightPhoto,
            WhiteSensivity = WhiteSensitivity,
            AutoRotate = AutoRotate,
            UseGpu = UseGpu,
            FileNamePattern = FileNamePattern,
            DefaultPath = DefaultPath,
            DefaultFileExtension = DefaultFileExtension
            
        };
    }
    public async void SelectDirectory()
    {
        var dialog = new OpenFolderDialog()
        {
            Title = "Select folder for save",
            Directory = string.IsNullOrEmpty(DefaultPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                : DefaultPath
        };
        
        var result = await dialog.ShowAsync(new Window());
        
        if (!string.IsNullOrEmpty(result))
        {
            DefaultPath = result;
        }
    }

    public void CancelChanges()
    {
        MinWidthPhoto = _tempSettings.MinWidthPhoto;
        MinHeightPhoto = _tempSettings.MinHeightPhoto;
        WhiteSensitivity = _tempSettings.WhiteSensivity;
        AutoRotate = _tempSettings.AutoRotate;
        UseGpu = _tempSettings.UseGpu;
        FileNamePattern = _tempSettings.FileNamePattern;
        DefaultPath = _tempSettings.DefaultPath;
        DefaultFileExtension = _tempSettings.DefaultFileExtension;
    }
    
    public void ResetToDefault()
    {
        MinWidthPhoto = 100;
        MinHeightPhoto = 100;
        WhiteSensitivity = 240;
        UseGpu = false;
        FileNamePattern = "image";
        DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        AutoRotate = true;
        DefaultFileExtension = "png";
        SaveToFile();
    }
}

public class TempSettings
{
    public int MinWidthPhoto { get; set; }
    public int MinHeightPhoto { get; set; }
    public byte WhiteSensivity { get; set; }
    public bool AutoRotate { get; set; }
    public bool UseGpu { get; set; }
    public string FileNamePattern { get; set; }
    public string DefaultPath { get; set; }
    public string DefaultFileExtension { get; set; }
}