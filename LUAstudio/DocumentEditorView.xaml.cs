using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LUAstudio.IDE.Documents;
using LUAstudio.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ICSharpCode.AvalonEdit;

namespace LUAstudio;

public partial class DocumentEditorView : UserControl
{
    private TextDocument? _boundDocument;
    private TextDocument? _pendingDocument;
    private bool _suppressVmPush;
    private bool _caretHooked;
    private WpfDocumentEditorHost? _languageHost;
    private EditorSettingsCoordinator? _settingsCoordinator;

    public DocumentEditorView()
    {
        InitializeComponent();

        Loaded += OnEditorLoaded;
        Unloaded += OnEditorUnloaded;
    }

    public WpfDocumentEditorHost? LanguageHost
    {
        get => _languageHost;
        set => _languageHost = value;
    }

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(
            nameof(Document),
            typeof(TextDocument),
            typeof(DocumentEditorView),
            new PropertyMetadata(null, OnDocumentChanged));

    public TextDocument? Document
    {
        get => (TextDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var view = (DocumentEditorView)d;
        view.RebindDocument(
            e.OldValue as TextDocument,
            e.NewValue as TextDocument);
    }

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        _languageHost ??= App.Services.GetRequiredService<WpfDocumentEditorHost>();
        _settingsCoordinator ??= App.Services.GetRequiredService<EditorSettingsCoordinator>();

        _settingsCoordinator.Register(Editor);

        EnsureCaretHook();

        if (_pendingDocument is not null)
        {
            ApplyDocumentToEditor(_pendingDocument);
            _pendingDocument = null;
        }
        else if (_boundDocument is not null)
        {
            ApplyDocumentToEditor(_boundDocument);
        }

        Editor.Focus();

        ReportCaretPositionSafe();
    }

    private void OnEditorUnloaded(object sender, RoutedEventArgs e)
    {
        if (_caretHooked && Editor?.TextArea?.Caret is not null)
        {
            Editor.TextChanged -= OnEditorTextChanged;
            Editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            _caretHooked = false;
        }

        if (_boundDocument is not null && IsEditorReady)
        {
            _languageHost?.Detach(Editor, _boundDocument);
        }

        if (IsEditorReady)
        {
            _settingsCoordinator?.Unregister(Editor);
        }
    }

    private void RebindDocument(TextDocument? oldDoc, TextDocument? newDoc)
    {
        if (oldDoc is not null)
        {
            oldDoc.PropertyChanged -= OnDocumentPropertyChanged;

            if (IsEditorReady)
            {
                _languageHost?.Detach(Editor, oldDoc);
            }
        }

        _boundDocument = newDoc;
        _pendingDocument = newDoc;

        if (!IsLoaded)
        {
            return;
        }

        ApplyDocumentToEditor(newDoc);
        _pendingDocument = null;
    }

    private void ApplyDocumentToEditor(TextDocument? doc)
    {
        if (!IsEditorReady)
        {
            return;
        }

        if (doc is null)
        {
            _suppressVmPush = true;

            try
            {
                Editor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument(string.Empty);
            }
            finally
            {
                _suppressVmPush = false;
            }

            return;
        }

        _suppressVmPush = true;

        try
        {
            Editor.Document = new ICSharpCode.AvalonEdit.Document.TextDocument(doc.Content ?? string.Empty);
        }
        finally
        {
            _suppressVmPush = false;
        }

        doc.PropertyChanged -= OnDocumentPropertyChanged;
        doc.PropertyChanged += OnDocumentPropertyChanged;

        _languageHost?.Attach(Editor, doc);

        try
        {
            Editor.TextArea.Caret.BringCaretToView();
        }
        catch
        {
            // Layout may not be ready yet on first open.
        }

        ReportCaretPositionSafe();
    }

    private void OnDocumentPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TextDocument.Content) ||
            _boundDocument is null ||
            !IsEditorReady)
        {
            return;
        }

        var text = _boundDocument.Content ?? string.Empty;

        if (string.Equals(
                Editor.Document.Text,
                text,
                StringComparison.Ordinal))
        {
            return;
        }

        _suppressVmPush = true;

        try
        {
            Editor.Document.Text = text;
        }
        finally
        {
            _suppressVmPush = false;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        PushTextToDocument();
    }

    private void PushTextToDocument()
    {
        if (_suppressVmPush ||
            _boundDocument is null ||
            !IsEditorReady)
        {
            return;
        }

        _boundDocument.Content = Editor.Document.Text;

        _languageHost?.NotifyContentChanged(_boundDocument);
    }

    private void EnsureCaretHook()
    {
        if (_caretHooked || !IsEditorReady)
        {
            return;
        }

        Editor.TextChanged += OnEditorTextChanged;
        Editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;

        _caretHooked = true;
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        ReportCaretPositionSafe();
    }

    private void ReportCaretPositionSafe()
    {
        if (!IsEditorReady)
        {
            return;
        }

        if (Window.GetWindow(this)?.DataContext is not MainViewModel main)
        {
            return;
        }

        var line = Editor.TextArea.Caret.Line + 1;
        var column = Editor.TextArea.Caret.Column + 1;

        main.UpdateCaretPosition(line, column);
    }

    private bool IsEditorReady =>
        Editor is not null &&
        Editor.TextArea is not null;
}