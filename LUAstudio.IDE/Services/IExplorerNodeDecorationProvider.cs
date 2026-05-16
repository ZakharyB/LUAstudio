using LUAstudio.Workspace;

namespace LUAstudio.IDE.Services;

public interface IExplorerNodeDecorationProvider
{
    ExplorerNodeDecoration GetDecoration(FileSystemEntryNode node);

    void RefreshAll(IEnumerable<FileSystemEntryNode> roots);

    void RefreshPath(string? fullPath, IEnumerable<FileSystemEntryNode> roots);
}
