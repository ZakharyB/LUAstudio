using System.Collections.ObjectModel;
using System.ComponentModel;
using LUAstudio.IDE.Documents;

namespace LUAstudio.IDE.Documents;

public interface IDocumentService : INotifyPropertyChanged
{
    ObservableCollection<TextDocument> Documents { get; }

    TextDocument? ActiveDocument { get; set; }

    TextDocument CreateUntitled();

    Task<TextDocument> OpenFromPathAsync(string path, CancellationToken cancellationToken = default, bool switchToDocument = true);

    void RemoveDocument(TextDocument document);

    Task SaveAsync(TextDocument document, CancellationToken cancellationToken = default);

    Task SaveAsAsync(TextDocument document, string path, CancellationToken cancellationToken = default);

    Task ReloadFromDiskAsync(TextDocument document, CancellationToken cancellationToken = default);
}
