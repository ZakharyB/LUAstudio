using System.Text;

namespace LUAstudio.IDE.Documents;

public interface IDocument
{
    Guid Id { get; }

    string? FilePath { get; }

    string DisplayName { get; }

    string Content { get; set; }

    bool IsDirty { get; }

    Encoding Encoding { get; }
}
