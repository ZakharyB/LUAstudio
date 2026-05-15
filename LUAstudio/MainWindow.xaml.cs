using LUAstudio.IDE.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace LUAstudio;

public partial class MainWindow
{
    private readonly DockLayoutStore _layoutStore;

    public MainWindow(MainViewModel viewModel, DockLayoutStore layoutStore)
    {
        InitializeComponent();
        DataContext = viewModel;
        _layoutStore = layoutStore;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _layoutStore.TryLoad(DockManager);
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        _layoutStore.Save(DockManager);
    }
}
