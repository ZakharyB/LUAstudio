using System.Windows;
using LUAstudio.Plugins.ViewModels;

namespace LUAstudio.Plugins.Views;

public partial class PluginsWindow : Window
{
    public PluginsWindow()
    {
        InitializeComponent();
        DataContext = new PluginsViewModelMock();
    }
}
