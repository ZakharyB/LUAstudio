using System;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace LUAstudio;

public partial class DiagnosticsPanelView : UserControl
{
    private GridViewColumnHeader _activeHeader;
    private ListSortDirection _direction = ListSortDirection.Ascending;
    private ListCollectionView _view;

    public DiagnosticsPanelView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(OnHeaderClick));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ProblemsList.ItemsSource == null) return;

        _view = (ListCollectionView)CollectionViewSource.GetDefaultView(ProblemsList.ItemsSource);
    }

    private void OnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header || header.Column == null)
            return;

        if (ProblemsList.ItemsSource == null)
            return;

        _view ??= (ListCollectionView)CollectionViewSource.GetDefaultView(ProblemsList.ItemsSource);

        string sortBy = header.Column.DisplayMemberBinding is Binding b
            ? b.Path.Path
            : header.Column.Header?.ToString();

        if (string.IsNullOrWhiteSpace(sortBy))
            return;

        ListSortDirection newDirection = ListSortDirection.Ascending;

        if (_activeHeader == header && _direction == ListSortDirection.Ascending)
            newDirection = ListSortDirection.Descending;

        _view.SortDescriptions.Clear();
        _view.CustomSort = null;

        if (sortBy == "Severity")
            _view.CustomSort = new SeverityComparer(newDirection);
        else
            _view.SortDescriptions.Add(new SortDescription(sortBy, newDirection));

        _activeHeader = header;
        _direction = newDirection;

        UpdateHeaders();
    }

    private void UpdateHeaders()
    {
        foreach (var col in ((GridView)ProblemsList.View).Columns)
        {
            string baseText = GetBaseHeaderText(col);

            if (col == _activeHeader?.Column)
            {
                col.Header = baseText + (_direction == ListSortDirection.Ascending ? " ▲" : " ▼");
            }
            else
            {
                col.Header = baseText;
            }
        }
    }

    private string GetBaseHeaderText(GridViewColumn col)
    {
        if (col.DisplayMemberBinding is Binding b)
            return b.Path.Path;

        if (col.Header is string s)
        {
            return s.Replace(" ▲", "").Replace(" ▼", "");
        }

        return col.Header?.ToString() ?? "";
    }
}

public class SeverityComparer : IComparer
{
    private readonly ListSortDirection _direction;

    public SeverityComparer(ListSortDirection direction)
    {
        _direction = direction;
    }

    private int Rank(object s)
    {
        return s?.ToString()?.ToLower() switch
        {
            "error" => 0,
            "warning" => 1,
            "info" => 2,
            "hint" => 3,
            _ => 4
        };
    }

    public int Compare(object x, object y)
    {
        int r = Rank(x) - Rank(y);
        return _direction == ListSortDirection.Ascending ? r : -r;
    }
}