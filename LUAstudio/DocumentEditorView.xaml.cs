using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;
using LUAstudio.Editor.Debugging;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

// Type aliases to resolve ambiguity
using AvalonDocument = ICSharpCode.AvalonEdit.Document.TextDocument;
using CustomDocument = LUAstudio.IDE.Documents.TextDocument;

namespace LUAstudio;

public partial class DocumentEditorView : UserControl, IDisposable
{
    #region Dependency Properties (Exposed Settings)

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(nameof(Document), typeof(CustomDocument), typeof(DocumentEditorView),
            new PropertyMetadata(null, OnDocumentChanged));

    public CustomDocument? Document
    {
        get => (CustomDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public static readonly DependencyProperty WordWrapProperty =
        DependencyProperty.Register(nameof(WordWrap), typeof(bool), typeof(DocumentEditorView),
            new PropertyMetadata(false, (d, e) => ((DocumentEditorView)d).Editor.WordWrap = (bool)e.NewValue));

    public bool WordWrap
    {
        get => (bool)GetValue(WordWrapProperty);
        set => SetValue(WordWrapProperty, value);
    }

    public static readonly DependencyProperty FontFamilyProperty =
        DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(DocumentEditorView),
            new PropertyMetadata(new FontFamily("Consolas"), (d, e) => ((DocumentEditorView)d).Editor.FontFamily = (FontFamily)e.NewValue));

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(DocumentEditorView),
            new PropertyMetadata(12.0, (d, e) => ((DocumentEditorView)d).Editor.FontSize = (double)e.NewValue));

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly DependencyProperty TabSizeProperty =
        DependencyProperty.Register(nameof(TabSize), typeof(int), typeof(DocumentEditorView),
            new PropertyMetadata(4, (d, e) => ((DocumentEditorView)d).Editor.Options.IndentationSize = (int)e.NewValue));

    public int TabSize
    {
        get => (int)GetValue(TabSizeProperty);
        set => SetValue(TabSizeProperty, value);
    }

    // Caret reporting (bound to ViewModel)
    public static readonly DependencyProperty CaretLineProperty =
        DependencyProperty.Register(nameof(CaretLine), typeof(int), typeof(DocumentEditorView),
            new PropertyMetadata(0));

    public int CaretLine
    {
        get => (int)GetValue(CaretLineProperty);
        set => SetValue(CaretLineProperty, value);
    }

    public static readonly DependencyProperty CaretColumnProperty =
        DependencyProperty.Register(nameof(CaretColumn), typeof(int), typeof(DocumentEditorView),
            new PropertyMetadata(0));

    public int CaretColumn
    {
        get => (int)GetValue(CaretColumnProperty);
        set => SetValue(CaretColumnProperty, value);
    }

    #endregion

    #region Fields

    private readonly IServiceProvider _services;
    private AvalonDocument? _currentAvalonDocument;
    private CustomDocument? _boundCustomDocument;
    private bool _isUpdatingFromViewModel;
    private bool _isUpdatingFromEditor;
    private bool _caretHooked;
    private bool _isLoaded;

    private WpfDocumentEditorHost? _languageHost;
    private EditorSettingsCoordinator? _settingsCoordinator;
    private IBreakpointService? _breakpointService;
    private EditorNavigationService? _navigationService;
    private BreakpointMargin? _breakpointMargin;
    private LineHighlighter? _lineHighlighter;
    private SearchPanel? _searchPanel;
    private bool _disposed;

    #endregion

    public DocumentEditorView()
    {
        InitializeComponent();

        _services = App.Services;

        Loaded += OnEditorLoaded;
        Unloaded += OnEditorUnloaded;
    }

    #region Load / Unload

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoaded = true;
            _languageHost = _services.GetRequiredService<WpfDocumentEditorHost>();
            _settingsCoordinator = _services.GetRequiredService<EditorSettingsCoordinator>();
            _breakpointService = _services.GetRequiredService<IBreakpointService>();
            _navigationService = _services.GetRequiredService<EditorNavigationService>();

            // Setup margins
            EnsureBreakpointMargin();
            EnsureLineHighlighter();

            // Settings
            _settingsCoordinator.Register(Editor);
            ApplySettings();

            // Navigation
            if (_navigationService != null)
                _navigationService.NavigationRequested += OnNavigationRequested;

            // Search panel – explicitly cast to disambiguate overloads
            _searchPanel = SearchPanel.Install((TextEditor)Editor);
            _searchPanel.UseRegex = false;
            _searchPanel.MatchCase = false;

            // Hook caret
            EnsureCaretHook();

            // Bind initial document
            if (Document != null)
                ApplyDocument(Document, true);
            else if (_boundCustomDocument != null)
                ApplyDocument(_boundCustomDocument, true);

            // Focus
            Editor.Focus();

            // Drop
            AllowDrop = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during editor load: {ex}");
        }
    }

    private void OnEditorUnloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoaded = false;

            if (_caretHooked && Editor?.TextArea?.Caret != null)
            {
                Editor.TextChanged -= OnEditorTextChanged;
                Editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
                _caretHooked = false;
            }

            if (_boundCustomDocument != null)
            {
                _boundCustomDocument.PropertyChanged -= OnCustomDocumentPropertyChanged;
                _languageHost?.Detach(Editor, _boundCustomDocument);
            }

            _settingsCoordinator?.Unregister(Editor);

            if (_navigationService != null)
                _navigationService.NavigationRequested -= OnNavigationRequested;

            if (_breakpointMargin != null)
            {
                Editor.TextArea.LeftMargins.Remove(_breakpointMargin);
                _breakpointMargin = null;
            }

            _lineHighlighter?.Dispose();
            _lineHighlighter = null;

            _searchPanel?.Uninstall();
            _searchPanel = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during editor unload: {ex}");
        }
    }

    #endregion

    #region Document Binding (Preserving Undo)

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DocumentEditorView)d;
        view.DocumentChanged(e.OldValue as CustomDocument, e.NewValue as CustomDocument);
    }

    private void DocumentChanged(CustomDocument? oldCustomDoc, CustomDocument? newCustomDoc)
    {
        // Unsubscribe from old custom document
        if (oldCustomDoc != null)
        {
            oldCustomDoc.PropertyChanged -= OnCustomDocumentPropertyChanged;
            if (IsEditorReady)
                _languageHost?.Detach(Editor, oldCustomDoc);
        }

        _boundCustomDocument = newCustomDoc;

        if (newCustomDoc != null)
        {
            newCustomDoc.PropertyChanged += OnCustomDocumentPropertyChanged;
        }

        // Apply if loaded
        if (_isLoaded && IsEditorReady)
            ApplyDocument(newCustomDoc, false);
    }

    private void ApplyDocument(CustomDocument? customDoc, bool forceNew)
    {
        if (!IsEditorReady) return;

        // If the custom document is the same as the one already bound, just update text if needed.
        if (!forceNew && customDoc == _boundCustomDocument && _currentAvalonDocument != null)
        {
            var newContent = customDoc?.Content ?? string.Empty;
            if (_currentAvalonDocument.Text != newContent)
            {
                _isUpdatingFromViewModel = true;
                try
                {
                    _currentAvalonDocument.Text = newContent;
                }
                finally
                {
                    _isUpdatingFromViewModel = false;
                }
            }
            return;
        }

        // Different document: create new AvalonEdit document
        if (customDoc == null)
        {
            Editor.Document = new AvalonDocument(string.Empty);
            _currentAvalonDocument = Editor.Document;
        }
        else
        {
            var content = customDoc.Content ?? string.Empty;
            // Load asynchronously for large files
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    _isUpdatingFromViewModel = true;
                    var newDoc = new AvalonDocument(content);
                    Editor.Document = newDoc;
                    _currentAvalonDocument = newDoc;
                    _languageHost?.Attach(Editor, customDoc);
                    _breakpointMargin?.SetDocument(customDoc);

                    // Set source path for breakpoint margin
                    if (_breakpointMargin != null && !string.IsNullOrEmpty(customDoc.FilePath))
                        _breakpointMargin.SourcePath = customDoc.FilePath;

                    // Update syntax highlighting based on file extension
                    if (!string.IsNullOrEmpty(customDoc.FilePath))
                    {
                        var ext = Path.GetExtension(customDoc.FilePath);
                        if (!string.IsNullOrEmpty(ext))
                        {
                            ext = ext.TrimStart('.');
                            var highlighting = HighlightingManager.Instance.GetDefinitionByExtension("." + ext);
                            if (highlighting != null)
                                Editor.SyntaxHighlighting = highlighting;
                        }
                    }

                    Editor.Focus();
                }
                finally
                {
                    _isUpdatingFromViewModel = false;
                }
            }));
        }
    }

    private void OnCustomDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CustomDocument.Content) && !_isUpdatingFromEditor)
        {
            var customDoc = sender as CustomDocument;
            if (customDoc != null && _currentAvalonDocument != null && IsEditorReady)
            {
                var newContent = customDoc.Content ?? string.Empty;
                if (_currentAvalonDocument.Text != newContent)
                {
                    _isUpdatingFromViewModel = true;
                    try
                    {
                        _currentAvalonDocument.Text = newContent;
                    }
                    finally
                    {
                        _isUpdatingFromViewModel = false;
                    }
                }
            }
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingFromViewModel || _boundCustomDocument == null) return;
        _isUpdatingFromEditor = true;
        try
        {
            _boundCustomDocument.Content = Editor.Document.Text;
            _languageHost?.NotifyContentChanged(_boundCustomDocument);
        }
        finally
        {
            _isUpdatingFromEditor = false;
        }
    }

    #endregion

    #region Caret & Navigation

    private void EnsureCaretHook()
    {
        if (_caretHooked || !IsEditorReady) return;
        Editor.TextChanged += OnEditorTextChanged;
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _caretHooked = true;
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        ReportCaretPosition();
    }

    private void ReportCaretPosition()
    {
        if (!IsEditorReady) return;
        CaretLine = Editor.TextArea.Caret.Line + 1;
        CaretColumn = Editor.TextArea.Caret.Column + 1;
    }

    private void OnNavigationRequested()
    {
        if (_navigationService == null || _boundCustomDocument?.FilePath == null || !IsEditorReady)
            return;

        if (!string.Equals(_boundCustomDocument.FilePath, _navigationService.SourcePath, StringComparison.OrdinalIgnoreCase))
            return;

        // Explicit casts to int for ALL Math.Min/Max arguments to avoid overload ambiguity
        int line = Math.Max(1, Math.Min((int)_navigationService.Line, (int)Editor.Document.LineCount));
        var lineObj = Editor.Document.GetLineByNumber(line);
        int col = Math.Max(1, Math.Min((int)_navigationService.Column, (int)(lineObj.Length + 1)));

        Editor.TextArea.Caret.Line = line - 1;
        Editor.TextArea.Caret.Column = col - 1;
        Editor.TextArea.Caret.BringCaretToView();

        // Highlight the line
        if (_lineHighlighter != null)
        {
            _lineHighlighter.HighlightLine(line);
        }

        Editor.Focus();
    }

    #endregion

    #region Breakpoint Margin (using AbstractMargin)

    private void EnsureBreakpointMargin()
    {
        if (_breakpointMargin != null || _breakpointService == null || !IsEditorReady)
            return;

        _breakpointMargin = new BreakpointMargin(_breakpointService);
        if (_boundCustomDocument?.FilePath != null)
            _breakpointMargin.SourcePath = _boundCustomDocument.FilePath;

        Editor.TextArea.LeftMargins.Add(_breakpointMargin);
    }

    #endregion

    #region Line Highlighter (for navigation)

    private void EnsureLineHighlighter()
    {
        if (_lineHighlighter == null && IsEditorReady)
        {
            _lineHighlighter = new LineHighlighter(Editor.TextArea.TextView);
        }
    }

    #endregion

    #region Context Menu Commands

    private void ToggleBreakpoint_Click(object sender, RoutedEventArgs e)
    {
        if (_breakpointService == null || _boundCustomDocument?.FilePath == null)
            return;

        var line = Editor.TextArea.Caret.Line + 1;
        _breakpointService.ToggleBreakpoint(_boundCustomDocument.FilePath, line);
    }

    private void GoToLine_Click(object sender, RoutedEventArgs e)
    {
        var input = new InputDialog("Go to Line", "Enter line number:");
        if (input.ShowDialog() == true && int.TryParse(input.Result, out int line))
        {
            line = Math.Max(1, Math.Min((int)line, (int)Editor.Document.LineCount));
            Editor.TextArea.Caret.Line = line - 1;
            Editor.TextArea.Caret.BringCaretToView();
            if (_lineHighlighter != null)
                _lineHighlighter.HighlightLine(line);
        }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        if (_searchPanel != null)
        {
            _searchPanel.Open();
            _searchPanel.Focus();
        }
    }

    #endregion

    #region Drag & Drop

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0)
            {
                if (DataContext is MainViewModel mainVm)
                    mainVm.OpenDocumentCommand?.Execute(files[0]);
            }
        }
    }

    #endregion

    #region Settings & Other

    private void ApplySettings()
    {
        Editor.Options.IndentationSize = TabSize;
        Editor.Options.ConvertTabsToSpaces = true;
    }

    private void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
    }

    private bool IsEditorReady => Editor != null && Editor.TextArea != null;

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _lineHighlighter?.Dispose();
            _searchPanel?.Uninstall();
            if (_boundCustomDocument != null)
                _languageHost?.Detach(Editor, _boundCustomDocument);
        }
        _disposed = true;
    }

    #endregion
}

// ========================================
// Helper classes (fully corrected)
// ========================================

public class LineHighlighter : IDisposable
{
    private readonly TextView _textView;
    private readonly DispatcherTimer _timer;
    private int _highlightedLine;
    private readonly LineHighlightTransformer _transformer;

    public LineHighlighter(TextView textView)
    {
        _textView = textView;
        _transformer = new LineHighlightTransformer(this);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (s, e) =>
        {
            _timer.Stop();
            _highlightedLine = 0;
            _textView.InvalidateVisual();
        };
        _textView.LineTransformers.Add(_transformer);
    }

    public void HighlightLine(int line)
    {
        _highlightedLine = line;
        _textView.InvalidateVisual();
        _timer.Stop();
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _textView.LineTransformers.Remove(_transformer);
    }

    private class LineHighlightTransformer : IVisualLineTransformer
    {
        private readonly LineHighlighter _owner;
        public LineHighlightTransformer(LineHighlighter owner) => _owner = owner;

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
        {
            if (_owner._highlightedLine == 0 || context.VisualLine.FirstDocumentLine.LineNumber != _owner._highlightedLine)
                return;

            foreach (var element in elements)
            {
                element.TextRunProperties.SetBackgroundBrush(Brushes.LightYellow);
            }
        }
    }
}

public class BreakpointMargin : AbstractMargin
{
    private readonly IBreakpointService _breakpointService;
    private string? _sourcePath;

    public BreakpointMargin(IBreakpointService service)
    {
        _breakpointService = service;
        Width = 20;
    }

    public string? SourcePath
    {
        get => _sourcePath;
        set => _sourcePath = value;
    }

    public void SetDocument(CustomDocument? doc) { } // compatibility

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_sourcePath == null || TextView?.Document == null) return;

        // Get breakpoints - now using BreakpointKey
        var breakpoints = _breakpointService.GetBreakpointsForFile(_sourcePath);
        if (breakpoints == null) return;

        foreach (var bp in breakpoints)  // bp is BreakpointKey
        {
            var line = bp.Line;
            var document = TextView.Document;
            if (line > document.LineCount) continue;

            // Use the 3‑parameter constructor to avoid ambiguity
            var yPos = TextView.GetVisualPosition(
                new TextViewPosition(line, 1, 0),
                VisualYPosition.LineBottom).Y;

            var radius = 6.0;
            var center = new Point(Width / 2, yPos - radius / 2);
            drawingContext.DrawEllipse(Brushes.Red, new Pen(Brushes.DarkRed, 1), center, radius, radius);
        }
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        if (newTextView != null)
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_sourcePath == null || TextView == null) return;

        var pos = e.GetPosition(this);
        var line = GetLineFromY(pos.Y);
        if (line.HasValue)
        {
            _breakpointService.ToggleBreakpoint(_sourcePath, line.Value);
        }
    }

    private int? GetLineFromY(double y)
    {
        if (TextView == null) return null;
        foreach (var vl in TextView.VisualLines)
        {
            // Use line number directly; no need for GetLocation()
            var top = TextView.GetVisualPosition(
                new TextViewPosition(vl.FirstDocumentLine.LineNumber, 1, 0),
                VisualYPosition.LineTop).Y;
            var bottom = TextView.GetVisualPosition(
                new TextViewPosition(vl.FirstDocumentLine.LineNumber, 1, 0),
                VisualYPosition.LineBottom).Y;
            if (y >= top && y <= bottom)
                return vl.FirstDocumentLine.LineNumber;
        }
        return null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(20, availableSize.Height);
    }
}

// Simple input dialog
public class InputDialog : Window
{
    public string? Result { get; private set; }

    public InputDialog(string title, string prompt)
    {
        Title = title;
        Width = 300;
        Height = 150;
        var txtInput = new TextBox { Name = "txtInput", Margin = new Thickness(10) };
        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(5), IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(5), IsCancel = true };

        okButton.Click += (s, e) => { Result = txtInput.Text; DialogResult = true; };
        cancelButton.Click += (s, e) => DialogResult = false;

        Content = new StackPanel
        {
            Children =
            {
                new Label { Content = prompt },
                txtInput,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center,
                    Children = { okButton, cancelButton } }
            }
        };

        Loaded += (s, e) => txtInput.Focus();
    }
}