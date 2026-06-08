using System.Windows;

namespace LUAstudio.Settings.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
