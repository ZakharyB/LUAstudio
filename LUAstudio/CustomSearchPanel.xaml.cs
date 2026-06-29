using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Search;

namespace LUAstudio;

public partial class CustomSearchPanel : UserControl
{
    private TextEditor? _editor;
    private ISearchStrategy? _searchStrategy;
    private int _lastFoundOffset = -1;

    public CustomSearchPanel()
    {
        InitializeComponent();
    }

    public void Attach(TextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        Visibility = Visibility.Collapsed;
    }

    public void Open()
    {
        if (_editor == null) return;
        Visibility = Visibility.Visible;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public void Close()
    {
        Visibility = Visibility.Collapsed;
        ClearHighlights();
        _editor?.Focus();
    }

    private void UpdateSearchStrategy()
    {
        if (_editor == null) return;
        string pattern = SearchBox.Text;
        if (string.IsNullOrEmpty(pattern))
        {
            _searchStrategy = null;
            return;
        }

        bool matchCase = MatchCaseCheck.IsChecked == true;
        bool wholeWords = WholeWordCheck.IsChecked == true;
        bool useRegex = RegexCheck.IsChecked == true;
        SearchMode mode = useRegex ? SearchMode.RegEx : SearchMode.Normal;

        _searchStrategy = SearchStrategyFactory.Create(pattern, matchCase, wholeWords, mode);
        _lastFoundOffset = -1;
    }

    private void FindNext()
    {
        if (_editor == null || _searchStrategy == null) return;
        var doc = _editor.Document;
        int startOffset = _lastFoundOffset >= 0 ? _lastFoundOffset + 1 : _editor.CaretOffset;
        // FindNext overload: (ITextSource, int, int)
        var result = _searchStrategy.FindNext(doc, startOffset, doc.TextLength);
        if (result != null)
        {
            SelectResult(result);
            _lastFoundOffset = result.Offset;
        }
        else if (_lastFoundOffset >= 0) // wrap around
        {
            result = _searchStrategy.FindNext(doc, 0, doc.TextLength);
            if (result != null)
            {
                SelectResult(result);
                _lastFoundOffset = result.Offset;
            }
        }
    }

    private void FindPrevious()
    {
        if (_editor == null || _searchStrategy == null) return;
        var doc = _editor.Document;
        int startOffset = _lastFoundOffset >= 0 ? _lastFoundOffset : _editor.CaretOffset;

        // Reverse search using a loop (fine for moderate documents)
        int lastMatch = -1;
        int lastLength = 0;
        int searchFrom = 0;
        ISearchResult? res;
        while ((res = _searchStrategy.FindNext(doc, searchFrom, startOffset)) != null)
        {
            lastMatch = res.Offset;
            lastLength = res.Length;
            searchFrom = res.Offset + 1;
        }
        if (lastMatch >= 0)
        {
            _editor.Select(lastMatch, lastLength);
            var loc = doc.GetLocation(lastMatch);
            _editor.ScrollTo(loc.Line, loc.Column);
            _lastFoundOffset = lastMatch;
        }
        else // wrap to end
        {
            searchFrom = 0;
            lastMatch = -1;
            while ((res = _searchStrategy.FindNext(doc, searchFrom, doc.TextLength)) != null)
            {
                lastMatch = res.Offset;
                lastLength = res.Length;
                searchFrom = res.Offset + 1;
            }
            if (lastMatch >= 0)
            {
                _editor.Select(lastMatch, lastLength);
                var loc = doc.GetLocation(lastMatch);
                _editor.ScrollTo(loc.Line, loc.Column);
                _lastFoundOffset = lastMatch;
            }
        }
    }

    private void SelectResult(ISearchResult result)
    {
        if (_editor == null) return;
        _editor.Select(result.Offset, result.Length);
        var loc = _editor.Document.GetLocation(result.Offset);
        _editor.ScrollTo(loc.Line, loc.Column);
    }

    private void ClearHighlights()
    {
        if (_editor != null)
            _editor.SelectionLength = 0;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                FindPrevious();
            else
                FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSearchStrategy();
        if (!string.IsNullOrEmpty(SearchBox.Text))
        {
            _lastFoundOffset = -1;
            FindNext();
        }
        else
        {
            ClearHighlights();
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();
    private void FindPrevious_Click(object sender, RoutedEventArgs e) => FindPrevious();
    private void SearchOptionsChanged(object sender, RoutedEventArgs e) => UpdateSearchStrategy();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}