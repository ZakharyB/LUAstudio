using LUAstudio.IntelliSense.Symbols;

namespace LUAstudio.IntelliSense.Workspace;

public interface IModuleResolver
{
    Symbol? ResolveModule(string modulePath, string? fromFilePath);

    void RebuildIndex(IEnumerable<string> workspaceRootPaths);
}
