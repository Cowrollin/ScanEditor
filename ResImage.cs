using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ScanEditor.Enum;
using ScanEditor.ViewModels;
using ScanEditor.Views;
using Point = Avalonia.Point;

namespace ScanEditor;

public class ResImage
{
    public WriteableBitmap BaseImage { get; set; }
    private WriteableBitmap HslParameterImage { get; set; }
    private WriteableBitmap OriginalImage { get; set; }
    private readonly Vector _imageDpi;
    private readonly string _applicationDirectory = AppContext.BaseDirectory;
    private readonly string _aiColorizeDirectory;
    private readonly string _eccvImage;
    private readonly string _siggraphImage;
    private Color _colorRgb;
    private HslColor _colorHsLl;
    
    public bool IsVertical => BaseImage.PixelSize.Height > BaseImage.PixelSize.Width;

    public int Hue { get; private set; } // -180 180
    public int Saturation { get; private set; } // 0 200
    public int Brightness { get; private set; } // -100 100
    public int Contrast { get; private set; } // -100 100

    public ResImage(WriteableBitmap baseImage)
    {
        BaseImage = baseImage;
        HslParameterImage = BaseImage;
        OriginalImage = baseImage;
        _imageDpi = baseImage.Dpi;

        Hue = 0;
        Saturation = 0;
        Brightness = 0;
        Contrast = 0;
        
        while (_applicationDirectory.Contains(@"\ScanEditor\"))
        {
            _applicationDirectory = Directory.GetParent(_applicationDirectory)?.FullName ?? _applicationDirectory;
        }
        _aiColorizeDirectory = Path.Combine(_applicationDirectory, "AIColorization");
        _eccvImage = Path.Combine(_aiColorizeDirectory, "AIC_eccv16.png");
        _siggraphImage = Path.Combine(_aiColorizeDirectory, "AIC_siggraph17.png");
    }


    public unsafe List<PixelRect> GetImagesBounds(byte whiteSensitivity)
    {
        List<PixelRect> rectangles = [];
        if (BaseImage.PixelSize.Width == 0 || BaseImage.PixelSize.Height == 0)
        {
            return rectangles;
        }
        
        var sourceWidth = BaseImage.PixelSize.Width;
        var sourceHeight = BaseImage.PixelSize.Height;
        var visited = new bool[sourceWidth, sourceHeight];

        using var buf = BaseImage.Lock();
        var ptr = (uint*)buf.Address;
        for (int y = 0; y < sourceHeight; y++)
        {
            for (int x = 0; x < sourceWidth; x++)
            {
                var pixelData = ptr[y * sourceWidth + x];
                if (visited[x, y] || IsWhite(whiteSensitivity, pixelData)) continue;
                
                (PixelRect pixelRect, visited) = GetBoundsOfConnectedPixels(whiteSensitivity, visited, x, y);
                rectangles.Add(pixelRect);
            }
        }
        return rectangles;
    }

    private unsafe (PixelRect, bool[,] visited) GetBoundsOfConnectedPixels(byte whiteSensitivity, bool[,] visited, int startX, int startY)
    {
        var sourceWidth = BaseImage.PixelSize.Width;
        var sourceHeight = BaseImage.PixelSize.Height;
        int minX = startX, minY = startY, maxX = startX, maxY = startY;

        Queue<Point> queue = new Queue<Point>();
        queue.Enqueue(new Point(startX, startY));
        
        int[] dx = [-2, -1, 0, 1, 2, -2, -1, 0, 1, 2, -2, -1, 0, 1, 2, -2, -1, 0, 1, 2, -2, -1, 0, 1, 2];
        int[] dy = [-2, -2, -2, -2, -2, -1, -1, -1, -1, -1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2];

        using var buf = BaseImage.Lock();
        var ptr = (uint*)buf.Address;
        while (queue.Count > 0)
        {
            var (d, d1) = queue.Dequeue();
            var x = (int)d;
            var y = (int)d1;

            if (x < 0 || x >= sourceWidth || y < 0 || y >= sourceHeight || visited[x, y])
                continue;

            var pixelData = ptr[y * sourceWidth + x];
            if (IsWhite(whiteSensitivity, pixelData))
            {
                continue;
            }
                
            visited[x, y] = true;

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;

            for (var i = 0; i < dx.Length; i++)
            {
                var nx = x + dx[i];
                var ny = y + dy[i];
                if (nx >= 0 && nx < sourceWidth && ny >= 0 && ny < sourceHeight && !visited[nx, ny])
                {
                    queue.Enqueue(new Point(nx, ny));
                }
            }
        }
        return (new PixelRect(minX, minY, maxX - minX, maxY - minY), visited);
    }

    /// <summary>
    /// Copy pixels in rectangle coord
    /// </summary>
    /// <param name="coord"></param>
    /// <returns></returns>
    public unsafe WriteableBitmap CopyPixelToWriteableBitmap(PixelRect coord)
    {
        var resultBitmap = new WriteableBitmap(coord.Size, _imageDpi);
        var sourceWidth = resultBitmap.PixelSize.Width;
        var sourceHeight = resultBitmap.PixelSize.Height;

        using var buf = resultBitmap.Lock();
        using var buf2 = BaseImage.Lock();
        var ptr = (uint*)buf.Address;
        var ptr2 = (uint*)buf2.Address;

        for (var y = 0; y < sourceHeight; y++)
        {
            for (var x = 0; x < sourceWidth; x++)
            {
                ptr[y * sourceWidth + x] = ptr2[(y + coord.Y) * BaseImage.PixelSize.Width + x + coord.X];
            }
        }
        return resultBitmap;
    }
    
    public void CutBackground(byte whiteSensitivity)
    {
        var imageBounds = GetImagesBounds(whiteSensitivity);

        foreach (var item in imageBounds)
        {
            if (item.Width > SettingsViewModel.Instance.MinWidthPhoto && item.Height > SettingsViewModel.Instance.MinHeightPhoto)
            {
                BaseImage = CopyPixelToWriteableBitmap(item);
            }
        }
    }

    /// <summary>
    /// rotates the image 90 degrees in both directions
    /// </summary>
    /// <param name="angle">
    ///     &lt;c&gt;true&lt;/c&gt;: rotate 90
    ///     &lt;c&gt;false&lt;/c&gt;: rotate minus 90
    /// </param>
    /// <returns>Rotated <c>WriteableBitmap</c></returns>
    public unsafe void RotateImageOn90(bool angle)
    {
        int sourceWidth = BaseImage.PixelSize.Height;
        int sourceHeight = BaseImage.PixelSize.Width;
        WriteableBitmap resultBitmap = new WriteableBitmap(new PixelSize(sourceWidth, sourceHeight), _imageDpi);
        WriteableBitmap resultBitmapHsl = new WriteableBitmap(new PixelSize(sourceWidth, sourceHeight), _imageDpi);
        using var buf = resultBitmap.Lock();
        using var bufhsl = resultBitmapHsl.Lock() ;
        using var buf2 = BaseImage.Lock();
        using var bufhsl2 = HslParameterImage.Lock();
        var ptr = (uint*)buf.Address;
        var ptrhsl = (uint*)bufhsl.Address;
        var ptr2 = (uint*)buf2.Address;
        var ptrhsl2 = (uint*)bufhsl2.Address;
        if (angle)
        {
            for (int y = 0; y < sourceHeight; y++)
            {
                for (int x = 0; x < sourceWidth; x++)
                {
                    ptr[(y + 1) * sourceWidth - x] = ptr2[x * sourceHeight + y];
                    ptrhsl[(y + 1) * sourceWidth - x] = ptrhsl2[x * sourceHeight + y];
                }
            }
        }
        else
        {
            for (var y = 0; y < sourceHeight; y++)
            {
                for (var x = 0; x < sourceWidth; x++)
                {
                    ptr[y * sourceWidth + x] = ptr2[(x + 1) * sourceHeight - y];
                    ptrhsl[y * sourceWidth + x] = ptrhsl2[(x + 1) * sourceHeight - y];
                }
            }
        }

        BaseImage = resultBitmap; HslParameterImage = resultBitmapHsl;
    }

    /// <summary>
    /// Creates the original file name (adds a number to the end of the name) and saves the image using the WriteableBitmap.Save() method
    /// </summary>
    /// <param name="filePath"> Full path of a file with a custom name </param>
    public void SaveImage(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var directoryName = Path.GetDirectoryName(filePath);

        string fullPath;
        var c = 0;

        while (true)
        {
            fullPath = Path.Combine(directoryName!, $"{fileName}{c}{extension}");

            if (File.Exists(fullPath))
            {
                c++;
            }
            else
            {
                break;
            }
        }
        BaseImage.Save(fullPath);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="angleRadians"></param>
    public unsafe void RotateImageOnAngle(double angleRadians)
    {
        // If you need use Celsius in func parameter
        //double angleRadians = angle * Math.PI / 180.0;

        var sourceWidth = BaseImage.PixelSize.Width;
        var sourceHeight = BaseImage.PixelSize.Height;

        var cosAngle = Math.Abs(Math.Cos(angleRadians));
        var sinAngle = Math.Abs(Math.Sin(angleRadians));
        
        var targetWidth = (int)Math.Ceiling(sourceWidth * cosAngle + sourceHeight * sinAngle);
        var targetHeight = (int)Math.Ceiling(sourceHeight * cosAngle + sourceWidth * sinAngle);

        WriteableBitmap resultImage = new WriteableBitmap(new PixelSize(targetWidth, targetHeight), _imageDpi);

        var centerXSource = sourceWidth  / 2;
        var centerYSource = sourceHeight / 2;
        var centerXTarget = targetWidth  / 2;
        var centerYTarget = targetHeight / 2;

        using (var targetBuffer = resultImage.Lock())
        using (var sourceBuffer = BaseImage.Lock())
        {
            var sourcePixels = (uint*)sourceBuffer.Address;
            var targetPixels = (uint*)targetBuffer.Address;

            for (var i = 0; i < targetWidth * targetHeight; i++)
            {
                targetPixels[i] = 0xffffffff;
            }

            for (var y = 0; y < sourceHeight; y++)
            {
                for (var x = 0; x < sourceWidth; x++)
                {
                    var newX = (int)(Math.Cos(angleRadians) * (x - centerXSource) -
                                     Math.Sin(angleRadians) * (y - centerYSource) + centerXTarget);
                    var newY = (int)(Math.Sin(angleRadians) * (x - centerXSource) +
                                     Math.Cos(angleRadians) * (y - centerYSource) + centerYTarget);

                    if (newX >= 0 && newX < targetWidth && newY >= 0 && newY < targetHeight)
                    {
                        targetPixels[newY * targetWidth + newX] = sourcePixels[y * sourceWidth + x];
                    }
                }
            }
        }
        BaseImage = resultImage;
    }

    /// <summary>
    /// Finds angle in image using formula: angle = arc tan(a/b), where
    /// a,b - the cathetes of a right triangle formed by the boundaries of the canvas and one side of the image.
    /// </summary>
    /// <param name="whiteSensitivity"> sensitivity level for background color (0 ... 255) </param>
    /// <returns></returns>
    public unsafe double FindRotateAngle(byte whiteSensitivity)
    {
        var sourceWidth = BaseImage.PixelSize.Width;
        var sourceHeight = BaseImage.PixelSize.Height;

        var akatet = 0;
        var bkatet = -1;

        using (var buf = BaseImage.Lock())
        {
            var ptr = (uint*)buf.Address;

            for (var y = 0; y < sourceHeight; y++)
            {
                for (var x = 0; x < sourceWidth; x++)
                {
                    if (!IsWhite(whiteSensitivity, ptr[y * sourceWidth + x]))
                    {
                        akatet = x;
                        goto LoopEnd;
                    }
                }
            }

            LoopEnd:

            for (var y = 0; y < sourceHeight; y++)
            {
                for (var x = 0; x < sourceWidth; x++)
                {
                    if (!IsWhite(whiteSensitivity, ptr[x * sourceWidth + y]))
                    {
                        bkatet = x;
                        goto LoopEnd2;
                    }
                }
            }

            LoopEnd2: ;
        }
        if (akatet <= 5 || bkatet <= 5)
        {
            return 0.0;
        }
        
        return Math.Round(Math.Atan2(akatet, bkatet), 2);
    }

    /// <summary>
    /// Removing noise in an image with a smoothing algorithm using a median filter
    /// </summary>
    /// <param name="neighbours"> overlay matrix size, number, more smoothing </param>
    public void DenoiseMedianFilter(int neighbours = 1)
    {
        var sourceWidth = BaseImage.PixelSize.Width;
        var sourceHeight = BaseImage.PixelSize.Height;
        var resultBitmap = new WriteableBitmap(new PixelSize(sourceWidth, sourceHeight), _imageDpi);
        var neighbors = new List<uint>();

        using (var buf = resultBitmap.Lock())
        {
            using (var buf2 = BaseImage.Lock())
            {
                unsafe
                {
                    var ptr = (uint*)buf.Address;
                    var ptr2 = (uint*)buf2.Address;

                    for (var y = neighbours; y < sourceHeight - neighbours; y++)
                    {
                        for (var x = neighbours; x < sourceWidth - neighbours; x++)
                        {
                            for (var ky = -neighbours; ky <= neighbours; ky++)
                            {
                                for (var kx = -neighbours; kx <= neighbours; kx++)
                                {
                                    neighbors.Add(ptr2[(y + ky) * sourceWidth + (x + kx)]);
                                }
                            }
                            neighbors.Sort();
                            ptr[y * sourceWidth + x] = neighbors[neighbors.Count / 2];
                            neighbors.Clear();
                        }
                    }
                }
            }
        }
        BaseImage = resultBitmap; HslParameterImage = resultBitmap;
    }

    /// <summary>
    /// Increases image sharpness by applying a color filter matrix
    /// </summary>
    public unsafe void SharpenFilter()
    {
        var sourceWidth = BaseImage.PixelSize.Width;
        var sourceHeight = BaseImage.PixelSize.Height;
        var resultBitmap = new WriteableBitmap(new PixelSize(sourceWidth, sourceHeight), _imageDpi);
        
        int[,] kernel = 
        {
            { -1, -1, -1 },
            { -1, 9, -1 },
            { -1, -1, -1 }
        };
        var kernelSize = 3;
        var kHalf = kernelSize / 2;
        
        using var buf = resultBitmap.Lock();
        using var buf2 = BaseImage.Lock();

        var src = (uint*)buf2.Address;
        var dst = (uint*)buf.Address;

        for (var y = kHalf; y < sourceHeight - kHalf; y++)
        {
            for (var x = kHalf; x < sourceWidth - kHalf; x++)
            {
                int r = 0, g = 0, b = 0;

                for (var ky = -kHalf; ky <= kHalf; ky++)
                {
                    for (var kx = -kHalf; kx <= kHalf; kx++)
                    {
                        int px = x + kx;
                        int py = y + ky;
                        int weight = kernel[ky + kHalf, kx + kHalf];

                        var pixel = src[py * sourceWidth + px];
                        var pr = (byte)((pixel >> 16) & 0xFF);
                        var pg = (byte)((pixel >> 8) & 0xFF);
                        var pb = (byte)(pixel & 0xFF);

                        r += pr * weight;
                        g += pg * weight;
                        b += pb * weight;
                    }
                }
                    
                r = Math.Clamp(r, 0, 255);
                g = Math.Clamp(g, 0, 255);
                b = Math.Clamp(b, 0, 255);

                dst[y * sourceWidth + x] = (uint)(0xFF << 24 | r << 16 | g << 8 | b);
            }
        }
        BaseImage = resultBitmap; HslParameterImage = resultBitmap;
    }

    /// <summary>
    /// Blur the image by applying the color filter matrix
    /// </summary>
    public unsafe void BlurFilter()
    {
        var width = BaseImage.PixelSize.Width;
        var height = BaseImage.PixelSize.Height;
        var resultBitmap = new WriteableBitmap(new PixelSize(width, height), _imageDpi);

        var kernel = new[,]
        {
            { 1, 1, 1 },
            { 1, 1, 1 },
            { 1, 1, 1 }
        };
        int kernelSize = 3;
        int kHalf = kernelSize / 2;
        int kernelSum = 9;

        using var buf = resultBitmap.Lock();
        using var buf2 = BaseImage.Lock();

        var src = (uint*)buf2.Address;
        var dst = (uint*)buf.Address;

        for (var y = kHalf; y < height - kHalf; y++)
        {
            for (var x = kHalf; x < width - kHalf; x++)
            {
                int r = 0, g = 0, b = 0;

                for (var ky = -kHalf; ky <= kHalf; ky++)
                {
                    for (var kx = -kHalf; kx <= kHalf; kx++)
                    {
                        int px = x + kx;
                        int py = y + ky;
                        int weight = kernel[ky + kHalf, kx + kHalf];

                        var pixel = src[py * width + px];
                        var pr = (byte)((pixel >> 16) & 0xFF);
                        var pg = (byte)((pixel >> 8) & 0xFF);
                        var pb = (byte)(pixel & 0xFF);

                        r += pr * weight;
                        g += pg * weight;
                        b += pb * weight;
                    }
                }

                r /= kernelSum;
                g /= kernelSum;
                b /= kernelSum;

                dst[y * width + x] = (uint)(0xFF << 24 | r << 16 | g << 8 | b);
            }
        }
        BaseImage = resultBitmap; HslParameterImage = resultBitmap;
    }
    
    /// <summary>
    /// Changes HSL and image contrast
    /// </summary>
    /// <param name="hueShift"></param>
    /// <param name="saturationMultiplier"></param>
    /// <param name="brightnessMultiplier"></param>
    /// <param name="contrastMultiplier"></param>
    public unsafe void HueSaturationBrightnessContrastEditor(int hueShift, int saturationMultiplier, int brightnessMultiplier, int contrastMultiplier)
    {
        Hue = hueShift;
        Saturation = saturationMultiplier;
        Brightness = brightnessMultiplier;
        Contrast = contrastMultiplier;

        var resultBitmap = new WriteableBitmap(BaseImage.PixelSize, BaseImage.Dpi);
        var pixelCount = BaseImage.PixelSize.Width * BaseImage.PixelSize.Height;

        using var buf = resultBitmap.Lock();
        using var buf2 = HslParameterImage.Lock();
        var scr = (uint*)buf.Address;
        var ptr = (uint*)buf2.Address;

        for (var i = 0; i < pixelCount; i++)
        {
            var pixel = ptr[i];
            _colorRgb = UIntToArgb(pixel);
            _colorHsLl = _colorRgb.ToHsl();

            // Hue
            var hue = (_colorHsLl.H + hueShift);
            if (hue < 0)
            {
                hue += 360;
            }
            if (hue > 360)
            {
                hue -= 360;
            }

            // Saturation
            var saturation = _colorHsLl.S + saturationMultiplier / 100.0;
            // Lightness
            var brightness = _colorHsLl.L + brightnessMultiplier / 100.0;
            // Contrast
            brightness = 0.5 + (brightness - 0.5) * (1 + contrastMultiplier / 100.0);
                
            scr[i] = ArgbToUInt(new HslColor(_colorRgb.A, hue, saturation, brightness).ToRgb());
        }

        BaseImage = resultBitmap;
    }
    
    /// <summary>
    /// Colorizes black and white images using a neural network (AIColorize.exe). Arguments for the application: black and white image, directory for colored images, flag for GPU usage 
    /// </summary>
    /// <param name="useGpu"> <c>true</c> - use Gpu; <c>false</c> - use Cpu </param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"> Incorrect input arguments </exception>
    /// <exception cref="Exception"> neural network error </exception>
    public async Task<bool> AiColorizeImage(bool useGpu)
    {
        try
        {
            var scriptExePath = Path.Combine(_aiColorizeDirectory, "AIColorize.exe");
            var inputImage = Path.Combine(_aiColorizeDirectory, "input.jpg");

            BaseImage.Save(inputImage);
            
            if (!File.Exists(scriptExePath))
            {
                throw new FileNotFoundException($"File {scriptExePath} not found.");
            }
            
            if (!File.Exists(inputImage))
            {
                throw new FileNotFoundException($"File {inputImage} not found.");
            }
                
            var useGpuFlag = "";
            if (useGpu)
            {
                useGpuFlag = "--use_gpu";
            }
            
            var arguments = $"-i \"{inputImage}\" -o \"{_aiColorizeDirectory}\" {useGpuFlag}";       
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = scriptExePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new Exception("Process not run");
            }
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception(await process.StandardError.ReadToEndAsync());
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
    
    public async Task SelectAicOutput(ImageRedactor owner)
    {
        var eccvBitmap = GetWriteableBitmapFromBitmap(new Bitmap(_eccvImage));
        var siggrapgBitmap = GetWriteableBitmapFromBitmap(new Bitmap(_siggraphImage));
        
        try
        {
            var aiCwindow = new AIColorizeOutput(eccvBitmap, siggrapgBitmap);
            BaseImage = await aiCwindow.ShowDialog<WriteableBitmap>(owner);
        }
        catch (Exception)
        {
            Console.WriteLine("Error select aic output");
        }
    }

    private static WriteableBitmap GetWriteableBitmapFromBitmap(Bitmap bitmap)
    {
        var resultWriteableBitmap = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi);
        using var buf = resultWriteableBitmap.Lock();
        bitmap.CopyPixels(new PixelRect(0,0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), buf.Address, buf.RowBytes * buf.Size.Height, buf.RowBytes);
        return resultWriteableBitmap;
    }

    public void ResetImage()
    {
        BaseImage = OriginalImage;
        HslParameterImage = OriginalImage;
    }

    public void Save()
    {
        OriginalImage = BaseImage;
        Hue = 0; Saturation = 0; Brightness = 0; Contrast = 0;
        HslParameterImage = BaseImage;
    } 
    
    private bool IsWhite(byte whiteSensitivity, uint pixelValue)
    {
        return GetPixelArgb(ColorChannelArgb.R, pixelValue) >= whiteSensitivity &&
               GetPixelArgb(ColorChannelArgb.G, pixelValue) >= whiteSensitivity &&
               GetPixelArgb(ColorChannelArgb.B, pixelValue) >= whiteSensitivity;
    }
    
    private byte GetPixelArgb(ColorChannelArgb colorChannelRgb, uint pixelValue)
    {
        switch (colorChannelRgb)
        {
            case ColorChannelArgb.A:
                return (byte)((pixelValue >> 24) & 0xFF);
            case ColorChannelArgb.R:
                return (byte)((pixelValue >> 16) & 0xFF);
            case ColorChannelArgb.G:
                return (byte)((pixelValue >> 8) & 0xFF);
            case ColorChannelArgb.B:
                return (byte)(pixelValue & 0xFF);
            default:
                throw new ArgumentException("Invalid color channel");
        }
    }
    
    private Color UIntToArgb(uint color)
    {
        var a = (byte)(color >> 24);
        var r = (byte)(color >> 16);
        var g = (byte)(color >> 8);
        var b = (byte)(color >> 0);
        return Color.FromArgb(a, r, g, b);
    }
    
    private uint ArgbToUInt(Color color)
    {
        return (uint)((color.A << 24) | (color.R << 16) |
                      (color.G << 8)  | (color.B << 0));
    }
}