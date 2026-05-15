using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using LUAstudio.Core.Events;
using LUAstudio.Core.Logging;
using LUAstudio.IDE.Events;
using LUAstudio.IDE.Threading;

namespace LUAstudio.IDE.Documents;

public sealed partial class DocumentService : ObservableObject, IDocumentService
{
    private readonly IEventBus _eventBus;
    private readonly IAppLogger _logger;
    private readonly IMainThread _mainThread;
    private int _untitledCounter;

    public ObservableCollection<TextDocument> Documents { get; } = new();

    [ObservableProperty]
    private TextDocument? _activeDocument;

    public DocumentService(IEventBus eventBus, IAppLogger logger, IMainThread mainThread)
    {
        _eventBus = eventBus;
        _logger = logger;
        _mainThread = mainThread;
    }

    partial void OnActiveDocumentChanged(TextDocument? oldValue, TextDocument? newValue)
    {
        _eventBus.Publish(new ActiveDocumentChangedEvent(newValue));
    }

    public TextDocument CreateUntitled()
    {
        var title = $"Untitled-{Interlocked.Increment(ref _untitledCounter)}";
        var doc = new TextDocument(title);
        Documents.Add(doc);
        ActiveDocument = doc;
        _eventBus.Publish(new DocumentOpenedEvent(doc));
        return doc;
    }

    public Task<TextDocument> OpenFromPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);

        return OpenFromPathCoreAsync(fullPath, cancellationToken);
    }

    private async Task<TextDocument> OpenFromPathCoreAsync(string fullPath, CancellationToken cancellationToken)
    {
        TextDocument? existing = null;
        _mainThread.Send(() =>
        {
            existing = Documents.FirstOrDefault(d =>
                string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
        });

        if (existing is not null)
        {
            _mainThread.Send(() => ActiveDocument = existing);
            return existing;
        }

        await using var stream = File.OpenRead(fullPath);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var encoding = reader.CurrentEncoding;

        var doc = new TextDocument(
            untitledTitle: Path.GetFileName(fullPath),
            filePath: fullPath,
            initialContent: text,
            encoding: encoding);

        _mainThread.Send(() =>
        {
            Documents.Add(doc);
            ActiveDocument = doc;
            _eventBus.Publish(new DocumentOpenedEvent(doc));
        });

        _logger.Info($"Opened document: {fullPath}");
        return doc;
    }

    public void RemoveDocument(TextDocument document)
    {
        _mainThread.Send(() =>
        {
            var wasActive = ReferenceEquals(ActiveDocument, document);
            Documents.Remove(document);
            if (wasActive)
            {
                ActiveDocument = Documents.LastOrDefault();
            }

            _eventBus.Publish(new DocumentClosedEvent(document));
        });
    }

    public async Task SaveAsync(TextDocument document, CancellationToken cancellationToken = default)
    {
        if (document.FilePath is null)
        {
            throw new InvalidOperationException("Cannot save a document without a path. Use SaveAs instead.");
        }

        await File.WriteAllTextAsync(document.FilePath, document.Content, document.Encoding, cancellationToken)
            .ConfigureAwait(false);

        _mainThread.Send(() => document.MarkCleanSnapshot());
        _logger.Info($"Saved document: {document.FilePath}");
    }

    public async Task SaveAsAsync(TextDocument document, string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        await File.WriteAllTextAsync(fullPath, document.Content, document.Encoding, cancellationToken).ConfigureAwait(false);

        _mainThread.Send(() => document.LoadFromDisk(fullPath, document.Content, document.Encoding));
        _logger.Info($"Saved document as: {fullPath}");
    }
}
