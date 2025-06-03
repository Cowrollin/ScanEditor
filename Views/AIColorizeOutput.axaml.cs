using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

namespace ScanEditor.Views;

public partial class AIColorizeOutput : Window
{
    private WriteableBitmap ImageEccv;
    private WriteableBitmap ImageSiggraph;
    
    
    public AIColorizeOutput(WriteableBitmap _imageEccv, WriteableBitmap _imageSiggraph)
    {
        InitializeComponent();
        
        ImageEccv = _imageEccv;
        ImageSiggraph = _imageSiggraph;
        
        Eccv.Source = ImageEccv;
        Siggraph.Source = ImageSiggraph;
        
    }
    
    private void EccvButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(ImageEccv);
    }
    
    private void SiggraphButtonClick(object? sender, RoutedEventArgs e)
    {
        Close(ImageSiggraph);
    }
    
}