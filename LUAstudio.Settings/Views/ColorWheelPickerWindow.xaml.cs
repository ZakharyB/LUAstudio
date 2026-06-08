using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LUAstudio.Settings.Views;

public partial class ColorWheelPickerWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isDragging;
    private WriteableBitmap _wheelBitmap;
    private DateTime _lastUpdate = DateTime.MinValue;

    private const int WheelSize = 240;

    private readonly SolidColorBrush _previewBrush = new(Colors.White);
    public Brush PreviewBrush => _previewBrush;

    private string _hex = "#FFFFFF";
    public string Hex
    {
        get => _hex;
        set
        {
            if (_hex == value) return;
            _hex = value;
            OnPropertyChanged();
        }
    }

    private Color _currentColor = Colors.White;

    public ColorWheelPickerWindow()
    {
        InitializeComponent();
        DataContext = this;

        Loaded += (_, _) => DrawWheel();
    }

    private void DrawWheel()
    {
        int size = WheelSize;
        int radius = size / 2;

        _wheelBitmap = new WriteableBitmap(
            size, size, 96, 96, PixelFormats.Pbgra32, null);

        int stride = size * 4;
        byte[] pixels = new byte[size * size * 4];

        double cx = radius;
        double cy = radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double dx = x - cx;
                double dy = y - cy;

                double distance = Math.Sqrt(dx * dx + dy * dy);

                if (distance > radius)
                    continue;

                double saturation = distance / radius;

                double hue = Math.Atan2(dy, dx) * (180 / Math.PI);
                if (hue < 0) hue += 360;

                var color = FromHSV(hue, saturation, 1.0);

                int index = (y * size + x) * 4;

                pixels[index + 0] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 255;
            }
        }

        _wheelBitmap.WritePixels(
            new Int32Rect(0, 0, size, size),
            pixels,
            stride,
            0);

        _wheelBitmap.Freeze();
        Wheel.Fill = new ImageBrush(_wheelBitmap);
    }

    private void Wheel_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        CaptureMouse();
        UpdateColor(e.GetPosition(Wheel));
    }

    private void EndDrag()
    {
        _isDragging = false;

        if (IsMouseCaptured)
            ReleaseMouseCapture();
    }
    
    private void Wheel_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        if ((DateTime.Now - _lastUpdate).TotalMilliseconds < 16)
            return;

        _lastUpdate = DateTime.Now;

        UpdateColor(e.GetPosition(Wheel));
    }
    
    private void Wheel_MouseLeave(object sender, MouseEventArgs e)
    {
        EndDrag();
    }
    
    private void Wheel_MouseUp(object sender, MouseButtonEventArgs e)
    {
        EndDrag();
    }
    
    private void UpdateColor(Point p)
    {
        double cx = WheelSize / 2;
        double cy = WheelSize / 2;

        double dx = p.X - cx;
        double dy = p.Y - cy;

        double radius = Math.Sqrt(dx * dx + dy * dy);
        double maxRadius = WheelSize / 2;

        if (radius > maxRadius)
            return;

        double hue = Math.Atan2(dy, dx) * (180 / Math.PI);
        if (hue < 0) hue += 360;

        double saturation = radius / maxRadius;

        var color = FromHSV(hue, saturation, 1.0);

        _currentColor = color;

        _previewBrush.Color = color;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        EndDrag();
        Hex = $"#{_currentColor.R:X2}{_currentColor.G:X2}{_currentColor.B:X2}";
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        EndDrag();
        DialogResult = false;
        Close();
    }

    private Color FromHSV(double h, double s, double v)
    {
        int hi = (int)(h / 60) % 6;
        double f = h / 60 - Math.Floor(h / 60);

        v *= 255;
        byte vByte = (byte)v;
        byte p = (byte)(v * (1 - s));
        byte q = (byte)(v * (1 - f * s));
        byte t = (byte)(v * (1 - (1 - f) * s));

        return hi switch
        {
            0 => Color.FromRgb(vByte, t, p),
            1 => Color.FromRgb(q, vByte, p),
            2 => Color.FromRgb(p, vByte, t),
            3 => Color.FromRgb(p, q, vByte),
            4 => Color.FromRgb(t, p, vByte),
            _ => Color.FromRgb(vByte, p, q),
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}