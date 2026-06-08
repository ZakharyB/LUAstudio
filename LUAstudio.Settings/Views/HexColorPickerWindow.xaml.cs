using System.Windows;
using System.Windows.Media;

namespace LUAstudio.Settings.Views;

public partial class HexColorPickerWindow : Window
{
    public string SelectedHex { get; set; } = "#FFFFFF";

    public Brush PreviewBrush =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(SelectedHex));

    public HexColorPickerWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}