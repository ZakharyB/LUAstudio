using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

    public static readonly DependencyProperty HighlightDurationSecondsProperty =
        DependencyProperty.Register(nameof(HighlightDurationSeconds), typeof(double), typeof(DocumentEditorView),
            new PropertyMetadata(2.0, (d, e) =>
            {
                var view = (DocumentEditorView)d;
                view._lineHighlighter?.SetHighlightDuration(TimeSpan.FromSeconds((double)e.NewValue));
            }));

    public double HighlightDurationSeconds
    {
        get => (double)GetValue(HighlightDurationSecondsProperty);
        set => SetValue(HighlightDurationSecondsProperty, value);
    }

    public static readonly DependencyProperty ShowRelativeLineNumbersProperty =
        DependencyProperty.Register(nameof(ShowRelativeLineNumbers), typeof(bool), typeof(DocumentEditorView),
            new PropertyMetadata(false, (d, e) => ((DocumentEditorView)d).OnShowRelativeLineNumbersChanged((bool)e.NewValue)));

    public bool ShowRelativeLineNumbers
    {
        get => (bool)GetValue(ShowRelativeLineNumbersProperty);
        set => SetValue(ShowRelativeLineNumbersProperty, value);
    }

    public static readonly DependencyProperty BreakpointMarginWidthProperty =
        DependencyProperty.Register(nameof(BreakpointMarginWidth), typeof(double), typeof(DocumentEditorView),
            new PropertyMetadata(20.0, (d, e) =>
            {
                var view = (DocumentEditorView)d;
                if (view._breakpointMargin != null)
                    view._breakpointMargin.MarginWidth = (double)e.NewValue;
            }));

    public double BreakpointMarginWidth
    {
        get => (double)GetValue(BreakpointMarginWidthProperty);
        set => SetValue(BreakpointMarginWidthProperty, value);
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
    private RelativeLineNumberMargin? _relativeLineNumberMargin;
    private CustomSearchPanel? _searchPanelControl;

    // Throttling for rapid content changes
    private DispatcherTimer? _throttleTimer;
    private string? _pendingContent;

    // External file watcher
    private FileSystemWatcher? _fileWatcher;
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
            ApplyRelativeLineNumbers(ShowRelativeLineNumbers);

            // Settings
            _settingsCoordinator.Register(Editor);
            ApplySettings();

            // Navigation
            if (_navigationService != null)
                _navigationService.NavigationRequested += OnNavigationRequested;

            // Search panel
            _searchPanelControl = SearchPanelControl;
            _searchPanelControl.Attach(Editor);
            Editor.PreviewKeyDown += Editor_PreviewKeyDown;

            // Caret
            EnsureCaretHook();

            // Zoom
            Editor.PreviewMouseWheel += Editor_PreviewMouseWheel;

            // Initial document
            if (Document != null)
                ApplyDocument(Document, true);
            else if (_boundCustomDocument != null)
                ApplyDocument(_boundCustomDocument, true);

            Editor.Focus();
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

            Editor.PreviewKeyDown -= Editor_PreviewKeyDown;
            Editor.PreviewMouseWheel -= Editor_PreviewMouseWheel;

            if (_breakpointMargin != null)
            {
                Editor.TextArea.LeftMargins.Remove(_breakpointMargin);
                _breakpointMargin.Dispose();
                _breakpointMargin = null;
            }

            if (_relativeLineNumberMargin != null)
            {
                Editor.TextArea.LeftMargins.Remove(_relativeLineNumberMargin);
                _relativeLineNumberMargin = null;
            }

            _lineHighlighter?.Dispose();
            _lineHighlighter = null;

            _searchPanelControl = null;

            _throttleTimer?.Stop();
            _throttleTimer = null;

            StopFileWatcher();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during editor unload: {ex}");
        }
    }

    #endregion

    #region Document Binding (Preserving Undo & Throttling)

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DocumentEditorView)d;
        view.DocumentChanged(e.OldValue as CustomDocument, e.NewValue as CustomDocument);
    }

    private void DocumentChanged(CustomDocument? oldCustomDoc, CustomDocument? newCustomDoc)
    {
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

        if (_isLoaded && IsEditorReady)
            ApplyDocument(newCustomDoc, false);
    }

    private void ApplyDocument(CustomDocument? customDoc, bool forceNew)
    {
        if (!IsEditorReady) return;

        // Same document, just ensure content matches
        if (!forceNew && customDoc == _boundCustomDocument && _currentAvalonDocument != null)
        {
            var newContent = customDoc?.Content ?? string.Empty;
            if (_currentAvalonDocument.Text != newContent)
            {
                _isUpdatingFromViewModel = true;
                try
                {
                    ReplaceContentPreservingUndo(newContent);
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
            StopFileWatcher();
        }
        else
        {
            var content = customDoc.Content ?? string.Empty;
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

                    if (_breakpointMargin != null && !string.IsNullOrEmpty(customDoc.FilePath))
                        _breakpointMargin.SourcePath = customDoc.FilePath;

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

                    StartFileWatcher(customDoc.FilePath);
                    Editor.Focus();
                }
                finally
                {
                    _isUpdatingFromViewModel = false;
                }
            }));
        }
    }

    private void ReplaceContentPreservingUndo(string newContent)
    {
        if (_currentAvalonDocument == null) return;
        var oldText = _currentAvalonDocument.Text;
        if (oldText == newContent) return;

        // Use a single Replace operation to preserve undo stack (one undo step)
        _currentAvalonDocument.Replace(0, oldText.Length, newContent);
    }

    private void OnCustomDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CustomDocument.Content) || _isUpdatingFromEditor)
            return;

        var customDoc = sender as CustomDocument;
        if (customDoc == null || _currentAvalonDocument == null || !IsEditorReady)
            return;

        var newContent = customDoc.Content ?? string.Empty;
        if (_currentAvalonDocument.Text == newContent)
            return;

        // Throttle updates (live‑update scenarios)
        _pendingContent = newContent;
        if (_throttleTimer == null)
        {
            _throttleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _throttleTimer.Tick += OnThrottleTimerTick;
        }
        _throttleTimer.Stop();
        _throttleTimer.Start();
    }

    private void OnThrottleTimerTick(object? sender, EventArgs e)
    {
        _throttleTimer?.Stop();
        if (_pendingContent == null || _currentAvalonDocument == null) return;
        if (_currentAvalonDocument.Text == _pendingContent) return;

        _isUpdatingFromViewModel = true;
        try
        {
            ReplaceContentPreservingUndo(_pendingContent);
        }
        finally
        {
            _isUpdatingFromViewModel = false;
            _pendingContent = null;
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

    #region External File Watcher

    private void StartFileWatcher(string? filePath)
    {
        StopFileWatcher();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);
        if (directory == null) return;

        _fileWatcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _fileWatcher.Changed += OnFileWatcherChanged;
    }

    private void StopFileWatcher()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.Changed -= OnFileWatcherChanged;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }

    private void OnFileWatcherChanged(object sender, FileSystemEventArgs e)
    {
        // Guard against multiple rapid events
        _fileWatcher!.EnableRaisingEvents = false;
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            var result = MessageBox.Show(
                $"The file '{e.FullPath}' has been modified externally.\nReload it?",
                "File Changed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var content = File.ReadAllText(e.FullPath);
                    _boundCustomDocument!.Content = content; // triggers property changed
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reloading file: {ex.Message}");
                }
            }
            // Re‑enable watcher
            if (_fileWatcher != null)
                _fileWatcher.EnableRaisingEvents = true;
        }));
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
        // Clear navigation highlights when caret moves
        _lineHighlighter?.ClearHighlights();
        _relativeLineNumberMargin?.InvalidateVisual();
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

        int line = Math.Max(1, Math.Min((int)_navigationService.Line, (int)Editor.Document.LineCount));
        var lineObj = Editor.Document.GetLineByNumber(line);
        int col = Math.Max(1, Math.Min((int)_navigationService.Column, (int)(lineObj.Length + 1)));

        Editor.TextArea.Caret.Line = line - 1;
        Editor.TextArea.Caret.Column = col - 1;
        Editor.TextArea.Caret.BringCaretToView();
     //   Editor.ScrollToLine(line);
        Editor.TextArea.Caret.BringCaretToView();


        _lineHighlighter?.HighlightLine(line);
        Editor.Focus();
    }

    #endregion

    #region Custom Search Panel Keyboard Shortcut

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _searchPanelControl?.Open();
            e.Handled = true;
        }
    }

    #endregion

    #region Zoom (Ctrl+MouseWheel)

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double delta = e.Delta > 0 ? 1 : -1;
            double newSize = Math.Max(6, Math.Min(72, FontSize + delta));
            FontSize = newSize;
            e.Handled = true;
        }
    }

    #endregion

    #region Breakpoint Margin (Enhanced)

    private void EnsureBreakpointMargin()
    {
        if (_breakpointMargin != null || _breakpointService == null || !IsEditorReady)
            return;

        _breakpointMargin = new BreakpointMargin(_breakpointService);
        _breakpointMargin.MarginWidth = BreakpointMarginWidth;
        if (_boundCustomDocument?.FilePath != null)
            _breakpointMargin.SourcePath = _boundCustomDocument.FilePath;

        Editor.TextArea.LeftMargins.Add(_breakpointMargin);
    }

    #endregion

    #region Line Highlighter (Enhanced)

    private void EnsureLineHighlighter()
    {
        if (_lineHighlighter == null && IsEditorReady)
        {
            _lineHighlighter = new LineHighlighter(Editor.TextArea.TextView);
            _lineHighlighter.SetHighlightDuration(TimeSpan.FromSeconds(HighlightDurationSeconds));
        }
    }

    #endregion

    #region Relative Line Numbers

    private void OnShowRelativeLineNumbersChanged(bool show)
    {
        if (!IsEditorReady) return;
        ApplyRelativeLineNumbers(show);
    }

    private void ApplyRelativeLineNumbers(bool show)
    {
        // Remove default line number margin (if present) and optionally add our custom one
        var existingNumberMargin = Editor.TextArea.LeftMargins
            .OfType<LineNumberMargin>().FirstOrDefault();
        if (show)
        {
            if (existingNumberMargin != null)
                Editor.TextArea.LeftMargins.Remove(existingNumberMargin);
            if (_relativeLineNumberMargin == null)
            {
                _relativeLineNumberMargin = new RelativeLineNumberMargin(Editor);
                Editor.TextArea.LeftMargins.Insert(0, _relativeLineNumberMargin);
            }
        }
        else
        {
            if (_relativeLineNumberMargin != null)
            {
                Editor.TextArea.LeftMargins.Remove(_relativeLineNumberMargin);
                _relativeLineNumberMargin = null;
            }
            // Restore default line numbers (ShowLineNumbers property controls that)
            // The built‑in margin is automatically managed; we just ensure ShowLineNumbers is true
            Editor.ShowLineNumbers = true;
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
            line = Math.Max(1, Math.Min(line, Editor.Document.LineCount));
            Editor.TextArea.Caret.Line = line - 1;
            Editor.TextArea.Caret.BringCaretToView();
            Editor.ScrollToLine(line);
            _lineHighlighter?.HighlightLine(line);
        }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        _searchPanelControl?.Open();
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

    #region Settings & Visual Indicators

    private void ApplySettings()
    {
        Editor.Options.IndentationSize = TabSize;
        Editor.Options.ConvertTabsToSpaces = true;

        // Visual indicators: current line highlight and matching brace
        Editor.Options.HighlightCurrentLine = true;
        Editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);

        // Bracket highlighting (property name may vary; adjust to your AvalonEdit version)
       // Editor.Options.ShowBracketHighlighting = true;
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
            _breakpointMargin?.Dispose();
            StopFileWatcher();
            if (_boundCustomDocument != null)
                _languageHost?.Detach(Editor, _boundCustomDocument);
        }
        _disposed = true;
    }

    #endregion
}

// ============================================
// Enhanced Helper Classes
// ============================================

// LineHighlighter with multiple lines, configurable duration
public class LineHighlighter : IDisposable
{
    private readonly TextView _textView;
    private DispatcherTimer? _timer;
    private TimeSpan? _highlightDuration = TimeSpan.FromSeconds(2);
    private readonly HashSet<int> _highlightedLines = new HashSet<int>();
    private readonly LineHighlightTransformer _transformer;

    public LineHighlighter(TextView textView)
    {
        _textView = textView;
        _transformer = new LineHighlightTransformer(this);
        _textView.LineTransformers.Add(_transformer);
    }

    public void SetHighlightDuration(TimeSpan? duration)
    {
        _highlightDuration = duration;
        if (_timer != null)
        {
            _timer.Interval = duration ?? TimeSpan.Zero;
        }
    }

    public void HighlightLine(int line)
    {
        if (line <= 0) return;
        _highlightedLines.Add(line);
        _textView.InvalidateVisual();

        if (_highlightDuration.HasValue && _highlightDuration.Value > TimeSpan.Zero)
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = _highlightDuration.Value };
                _timer.Tick += OnTimerTick;
            }
            _timer.Stop();
            _timer.Start();
        }
    }

    public void ClearHighlights()
    {
        if (_highlightedLines.Count == 0) return;
        _highlightedLines.Clear();
        _textView.InvalidateVisual();
        _timer?.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer?.Stop();
        ClearHighlights();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _textView.LineTransformers.Remove(_transformer);
    }

    private class LineHighlightTransformer : IVisualLineTransformer
    {
        private readonly LineHighlighter _owner;
        public LineHighlightTransformer(LineHighlighter owner) => _owner = owner;

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements)
        {
            if (_owner._highlightedLines.Count == 0) return;
            int line = context.VisualLine.FirstDocumentLine.LineNumber;
            if (!_owner._highlightedLines.Contains(line)) return;

            foreach (var element in elements)
            {
                element.TextRunProperties.SetBackgroundBrush(Brushes.LightYellow);
            }
        }
    }
}

// BreakpointMargin with caching, event subscription, state‑aware rendering, configurable width
public class BreakpointMargin : AbstractMargin, IDisposable
{
    private readonly IBreakpointService _breakpointService;
    private string? _sourcePath;
    private double _marginWidth = 20;
    private List<CachedBreakpointVisual> _cachedVisuals = new List<CachedBreakpointVisual>();

    public string? SourcePath
    {
        get => _sourcePath;
        set
        {
            if (_sourcePath != value)
            {
                UnsubscribeBreakpointEvents();
                _sourcePath = value;
                SubscribeBreakpointEvents();
                InvalidateCache();
                InvalidateVisual();
            }
        }
    }

    public double MarginWidth
    {
        get => _marginWidth;
        set
        {
            if (Math.Abs(_marginWidth - value) > 0.01)
            {
                _marginWidth = value;
                Width = value;
                InvalidateVisual();
            }
        }
    }

    public BreakpointMargin(IBreakpointService service)
    {
        _breakpointService = service;
        Width = _marginWidth;
        SubscribeBreakpointEvents();
    }

    public void SetDocument(CustomDocument? doc) { /* no‑op */ }

    private void SubscribeBreakpointEvents()
    {
        if (_breakpointService != null)
            _breakpointService.BreakpointsChanged += OnBreakpointsChanged;
    }

    private void UnsubscribeBreakpointEvents()
    {
        if (_breakpointService != null)
            _breakpointService.BreakpointsChanged -= OnBreakpointsChanged;
    }

    // Signature matches Action
    private void OnBreakpointsChanged()
    {
        InvalidateCache();
        Dispatcher.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_sourcePath == null || TextView?.Document == null) return;

        foreach (var bpVisual in _cachedVisuals)
        {
            var yPos = bpVisual.YPosition;
            var radius = bpVisual.Radius;
            var center = new Point(Width / 2, yPos - radius / 2);
            drawingContext.DrawEllipse(bpVisual.Fill, bpVisual.Stroke, center, radius, radius);
        }
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView != null)
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        if (newTextView != null)
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
        InvalidateCache();
        InvalidateVisual();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e)
    {
        InvalidateCache();
        InvalidateVisual();
    }

    private void InvalidateCache()
    {
        _cachedVisuals.Clear();
        if (_sourcePath == null || TextView?.Document == null) return;

        var breakpoints = _breakpointService.GetBreakpointsForFile(_sourcePath);
        if (breakpoints == null) return;

        // NOTE: BreakpointKey does not currently expose a State property.
        // To show different colors for disabled/error breakpoints, extend the
        // BreakpointKey record with a State enum and adjust the logic below.
        foreach (var bp in breakpoints)
        {
            var line = bp.Line;
            if (line > TextView.Document.LineCount) continue;

            var yPos = TextView.GetVisualPosition(
                new TextViewPosition(line, 1, 0),
                VisualYPosition.LineBottom).Y;

            Brush fill = Brushes.Red;
            Pen stroke = new Pen(Brushes.DarkRed, 1);

            _cachedVisuals.Add(new CachedBreakpointVisual
            {
                YPosition = yPos,
                Radius = 6.0,
                Fill = fill,
                Stroke = stroke
            });
        }
    }

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
        return new Size(_marginWidth, availableSize.Height);
    }

    public void Dispose()
    {
        UnsubscribeBreakpointEvents();
    }

    private class CachedBreakpointVisual
    {
        public double YPosition;
        public double Radius;
        public Brush Fill;
        public Pen Stroke;
    }
}

// Custom margin for relative line numbers
public class RelativeLineNumberMargin : AbstractMargin
{
    private readonly TextEditor _editor;

    public RelativeLineNumberMargin(TextEditor editor)
    {
        _editor = editor;
        Width = 40;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_editor.Document == null) return;

        var caretLine = _editor.TextArea.Caret.Line;
        var typeface = new Typeface(_editor.FontFamily, _editor.FontStyle, _editor.FontWeight, _editor.FontStretch);
        var foreground = _editor.LineNumbersForeground ?? Brushes.Gray;
        var fontSize = _editor.FontSize;
        var format = new FormattedText("0", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, typeface, fontSize, foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        foreach (var visualLine in TextView.VisualLines)
        {
            int lineNum = visualLine.FirstDocumentLine.LineNumber;
            int relative = lineNum - caretLine;
            string text = relative == 0 ? ">" : relative.ToString();
            double y = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.TextTop);
            drawingContext.DrawText(new FormattedText(text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip),
                new Point(Width - 5 - 10, y));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(40, availableSize.Height);
    }
}

// Simple input dialog (unchanged)
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