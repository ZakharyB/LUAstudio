using System.Windows;
using System.Windows.Controls;
using LUAstudio.IDE.Documents;

namespace LUAstudio;

public partial class DocumentEditorView : UserControl
{
    private TextDocument? _boundDocument;
    private bool _suppressVmPush;

    public DocumentEditorView()
    {
        InitializeComponent();
        Editor.TextChanged += (_, _) => PushTextToDocument();
    }

    public static readonly DependencyProperty DocumentProperty =
        DependencyProperty.Register(nameof(Document), typeof(TextDocument), typeof(DocumentEditorView),
            new PropertyMetadata(null, OnDocumentChanged));

    public TextDocument? Document
    {
        get => (TextDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    private static void OnDocumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DocumentEditorView)d;
        view.RebindDocument(e.OldValue as TextDocument, e.NewValue as TextDocument);
    }

    private void RebindDocument(TextDocument? oldDoc, TextDocument? newDoc)
    {
        if (oldDoc is not null)
        {
            oldDoc.PropertyChanged -= OnDocumentPropertyChanged;
        }

        _boundDocument = newDoc;

        if (newDoc is null)
        {
            Editor.Text = string.Empty;
            return;
        }

        _suppressVmPush = true;
        try
        {
            Editor.Text = newDoc.Content;
        }
        finally
        {
            _suppressVmPush = false;
        }

        newDoc.PropertyChanged += OnDocumentPropertyChanged;
    }

    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TextDocument.Content) || _boundDocument is null)
        {
            return;
        }

        _suppressVmPush = true;
        try
        {
            if (!string.Equals(Editor.Text, _boundDocument.Content, StringComparison.Ordinal))
            {
                Editor.Text = _boundDocument.Content;
            }
        }
        finally
        {
            _suppressVmPush = false;
        }
    }

    private void PushTextToDocument()
    {
        if (_suppressVmPush || _boundDocument is null)
        {
            return;
        }

        _boundDocument.Content = Editor.Text;
    }
}
