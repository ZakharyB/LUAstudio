using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;                  
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using LUAstudio.Editor.Debugging;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.ViewModels;
using ICSharpCode.AvalonEdit.Editing;
using Microsoft.Extensions.DependencyInjection;
using AvalonDocument = ICSharpCode.AvalonEdit.Document.TextDocument;
using CustomDocument = LUAstudio.IDE.Documents.TextDocument;

namespace LUAstudio;

public partial class DocumentEditorView : UserControl, IDisposable
{
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

    private DispatcherTimer? _throttleTimer;
    private string? _pendingContent;

    private FileSystemWatcher? _fileWatcher;
    private bool _disposed;
    private ErrorSquiggleRenderer? _errorSquiggleRenderer;

    public DocumentEditorView()
    {
        InitializeComponent();
        _services = App.Services;
        Loaded += OnEditorLoaded;
        Unloaded += OnEditorUnloaded;
    }

    private void OnDiagnosticsUpdated(object? sender, DiagnosticsUpdatedEventArgs e)
    {
        var errors = new List<(int Offset, int Length)>();
        foreach (var diag in e.Diagnostics)
        {
            errors.Add((diag.Offset, diag.Length));
        }
        _errorSquiggleRenderer?.SetErrors(errors);
    }
    
    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoaded = true;
            _languageHost = _services.GetRequiredService<WpfDocumentEditorHost>();
            _errorSquiggleRenderer = new ErrorSquiggleRenderer(Editor.TextArea.TextView);
            Editor.TextArea.TextView.BackgroundRenderers.Add(_errorSquiggleRenderer);
            _settingsCoordinator = _services.GetRequiredService<EditorSettingsCoordinator>();
            _breakpointService = _services.GetRequiredService<IBreakpointService>();
            _navigationService = _services.GetRequiredService<EditorNavigationService>();
            _languageHost.DiagnosticsUpdated += OnDiagnosticsUpdated;

            EnsureBreakpointMargin();
            EnsureLineHighlighter();
            ApplyRelativeLineNumbers(ShowRelativeLineNumbers);

            _settingsCoordinator.Register(Editor);
            ApplySettings();

            if (_navigationService != null)
                _navigationService.NavigationRequested += OnNavigationRequested;

            _searchPanelControl = SearchPanelControl;
            _searchPanelControl.Attach(Editor);
            Editor.PreviewKeyDown += Editor_PreviewKeyDown;

            EnsureCaretHook();

            Editor.PreviewMouseWheel += Editor_PreviewMouseWheel;

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
            newCustomDoc.PropertyChanged += OnCustomDocumentPropertyChanged;

        if (_isLoaded && IsEditorReady)
            ApplyDocument(newCustomDoc, false);
    }

    private void ApplyDocument(CustomDocument? customDoc, bool forceNew)
    {
        if (!IsEditorReady) return;

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
                    _boundCustomDocument!.Content = content;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reloading file: {ex.Message}");
                }
            }

            if (_fileWatcher != null)
                _fileWatcher.EnableRaisingEvents = true;
        }));
    }

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
        Editor.TextArea.Caret.BringCaretToView();

        _lineHighlighter?.HighlightLine(line);
        Editor.Focus();
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _searchPanelControl?.Open();
            e.Handled = true;
        }
    }

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

    private void EnsureLineHighlighter()
    {
        if (_lineHighlighter == null && IsEditorReady)
        {
            _lineHighlighter = new LineHighlighter(Editor.TextArea.TextView);
            _lineHighlighter.SetHighlightDuration(TimeSpan.FromSeconds(HighlightDurationSeconds));
        }
    }

    private void OnShowRelativeLineNumbersChanged(bool show)
    {
        if (!IsEditorReady) return;
        ApplyRelativeLineNumbers(show);
    }

    private void ApplyRelativeLineNumbers(bool show)
    {
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
            Editor.ShowLineNumbers = true;
        }
    }

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

    public void OpenSearch()
    {
        _searchPanelControl?.Open();
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        _searchPanelControl?.Open();
    }

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

    private void ApplySettings()
    {
        Editor.Options.IndentationSize = TabSize;
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.HighlightCurrentLine = true;
        Editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0));
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
    }

    private void Editor_Loaded(object sender, RoutedEventArgs e)
    {
        Editor.Focus();
    }

    private bool IsEditorReady => Editor != null && Editor.TextArea != null;

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
}