using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LUAstudio.Settings.Views;
using LUAstudio.Core;

namespace LUAstudio.Settings.Controls;

public partial class ColorSettingRow : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ColorSettingRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty HexValueProperty =
        DependencyProperty.Register(
            nameof(HexValue),
            typeof(string),
            typeof(ColorSettingRow),
            new FrameworkPropertyMetadata("#FFFFFF", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHexChanged));

    public static readonly DependencyProperty PreviewBrushProperty =
        DependencyProperty.Register(nameof(PreviewBrush), typeof(Brush), typeof(ColorSettingRow), new PropertyMetadata(Brushes.Transparent));

    public ColorSettingRow()
    {
        InitializeComponent();
        Loaded += (_, __) => UpdatePreviewBrush(HexValue);
        UpdatePreviewBrush(HexValue);
    }

    private void OnColorClicked(object sender, RoutedEventArgs e)
    {
        var picker = new ColorWheelPickerWindow
        {
            Owner = Window.GetWindow(this),
            Hex = HexValue
        };

        if (picker.ShowDialog() == true)
        {
            HexValue = picker.Hex;
        }
    }
    
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string HexValue
    {
        get => (string)GetValue(HexValueProperty);
        set => SetValue(HexValueProperty, value);
    }

    public Brush PreviewBrush
    {
        get => (Brush)GetValue(PreviewBrushProperty);
        private set => SetValue(PreviewBrushProperty, value);
    }
    
    
    private static void OnHexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ColorSettingRow row)
        {
            row.UpdatePreviewBrush(e.NewValue as string);
        }
    }

    
    private void UpdatePreviewBrush(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            hex = "#FFFFFF";

        var rgb = SettingColorParser.ParseRgb(hex, 0xFFFFFF);

        var brush = new SolidColorBrush(Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF)));

        brush.Freeze();
        PreviewBrush = brush;
    }
}
