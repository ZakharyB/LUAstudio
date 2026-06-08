using System.Windows;
using System.Windows.Controls;
using LUAstudio.Settings.ViewModels;

namespace LUAstudio.Settings.Views;

public partial class SettingsView : UserControl
{
    private readonly StackPanel[] _pages;

    public SettingsView()
    {
        InitializeComponent();
        DataContext = new SettingsViewModel();
        _pages = [EditorPage, ColorsPage, IntelliSensePage, WorkspacePage];
        CategoryList.SelectionChanged += OnCategoryChanged;
    }

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        var index = CategoryList.SelectedIndex;
        for (var i = 0; i < _pages.Length; i++)
        {
            _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
