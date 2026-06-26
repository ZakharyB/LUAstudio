using System.IO;
using LUAstudio.Execution.Abstractions;
using LUAstudio.IDE.Documents;
using LUAstudio.IntelliSense.Workspace;

namespace LUAstudio;

public sealed class WorkspaceModuleBridge
{
    private readonly IDocumentService _documents;
    private readonly RequireGraphService _requireGraph;

    public WorkspaceModuleBridge(IDocumentService documents, RequireGraphService requireGraph)
    {
        _documents = documents;
        _requireGraph = requireGraph;
    }

    public IReadOnlyList<WorkspaceModuleEntry> CreateSnapshot(string? activeSourcePath, string? activeSource)
    {
        var modules = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in _documents.Documents)
        {
            if (string.IsNullOrWhiteSpace(document.FilePath))
            {
                continue;
            }

            modules[Normalize(document.FilePath)] = document.Content ?? string.Empty;
        }

        foreach (var node in _requireGraph.GetNodes())
        {
            if (string.IsNullOrWhiteSpace(node.FilePath) || modules.ContainsKey(Normalize(node.FilePath)))
            {
                continue;
            }

            var content = TryReadFile(node.FilePath);
            if (content is not null)
            {
                modules[Normalize(node.FilePath)] = content;
            }
        }

        if (!string.IsNullOrWhiteSpace(activeSourcePath) && activeSource is not null)
        {
            modules[Normalize(activeSourcePath)] = activeSource;
        }

        return modules
            .Select(pair => new WorkspaceModuleEntry(pair.Key, pair.Value))
            .ToList();
    }

    private static string? TryReadFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
