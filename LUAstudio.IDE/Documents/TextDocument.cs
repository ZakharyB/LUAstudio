using System.ComponentModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LUAstudio.IDE.Documents;

public partial class TextDocument : ObservableObject, IDocument
{
    private string _baselineContent;

    public Guid Id { get; } = Guid.NewGuid();

    public string UntitledTitle { get; }

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private Encoding _encoding = Encoding.UTF8;

    public TextDocument(string untitledTitle, string? filePath = null, string initialContent = "", Encoding? encoding = null)
    {
        UntitledTitle = untitledTitle;
        FilePath = filePath;
        Encoding = encoding ?? Encoding.UTF8;
        _baselineContent = initialContent;
        _content = initialContent;
        IsDirty = false;
        PropertyChanged += OnPropertyChanged;
    }

    public string DisplayName => FilePath is null ? UntitledTitle : Path.GetFileName(FilePath);

    public void LoadFromDisk(string path, string diskContent, Encoding encoding)
    {
        FilePath = path;
        Encoding = encoding;
        SetContentAndBaseline(diskContent);
    }

    public void SetContentAndBaseline(string text)
    {
        _baselineContent = text;
        Content = text;
        IsDirty = false;
    }

    public void MarkCleanSnapshot()
    {
        _baselineContent = Content;
        IsDirty = false;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Content))
        {
            IsDirty = Content != _baselineContent;
        }

        if (e.PropertyName is nameof(FilePath) or nameof(Content))
        {
            OnPropertyChanged(nameof(DisplayName));
        }
    }
}
