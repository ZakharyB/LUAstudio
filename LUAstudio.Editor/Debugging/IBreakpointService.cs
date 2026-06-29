namespace LUAstudio.Editor.Debugging;

public sealed record BreakpointKey(string? SourcePath, int Line);

public interface IBreakpointService
{
    event Action? BreakpointsChanged;

    IReadOnlyCollection<BreakpointKey> Breakpoints { get; }

    bool IsBreakpoint(string? sourcePath, int line);

    bool ToggleBreakpoint(string? sourcePath, int line);

    void ClearBreakpoints(string? sourcePath);

    IReadOnlyList<(string? SourcePath, IReadOnlyList<Execution.Abstractions.BreakpointSpec> Breakpoints)> GetBreakpointGroups();

    IEnumerable<BreakpointKey> GetBreakpointsForFile(string filePath);
}