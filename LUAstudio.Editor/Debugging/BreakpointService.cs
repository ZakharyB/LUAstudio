using System;
using System.Collections.Generic;
using System.Linq;
using LUAstudio.Execution.Abstractions;

namespace LUAstudio.Editor.Debugging;

public sealed class BreakpointService : IBreakpointService
{
    private readonly HashSet<BreakpointKey> _breakpoints = new();

    public event Action? BreakpointsChanged;

    public IReadOnlyCollection<BreakpointKey> Breakpoints => _breakpoints;

    public bool IsBreakpoint(string? sourcePath, int line) =>
        _breakpoints.Contains(Normalize(sourcePath, line));

    public bool ToggleBreakpoint(string? sourcePath, int line)
    {
        var key = Normalize(sourcePath, line);
        if (!_breakpoints.Add(key))
        {
            _breakpoints.Remove(key);
            BreakpointsChanged?.Invoke();
            return false;
        }

        BreakpointsChanged?.Invoke();
        return true;
    }

    public void ClearBreakpoints(string? sourcePath)
    {
        if (sourcePath is null)
        {
            _breakpoints.Clear();
            BreakpointsChanged?.Invoke();
            return;
        }

        var normalized = NormalizePath(sourcePath);
        var removed = _breakpoints.RemoveWhere(k => string.Equals(NormalizePath(k.SourcePath), normalized, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            BreakpointsChanged?.Invoke();
        }
    }

    public IReadOnlyList<(string? SourcePath, IReadOnlyList<BreakpointSpec> Breakpoints)> GetBreakpointGroups()
    {
        return _breakpoints
            .GroupBy(k => k.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(g => (
                g.Key,
                (IReadOnlyList<BreakpointSpec>)g
                    .OrderBy(k => k.Line)
                    .Select(k => new BreakpointSpec(k.Line))
                    .ToList()))
            .ToList();
    }

    public IEnumerable<BreakpointKey> GetBreakpointsForFile(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return _breakpoints.Where(k => string.Equals(NormalizePath(k.SourcePath), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static BreakpointKey Normalize(string? sourcePath, int line) =>
        new(NormalizePath(sourcePath), line);

    private static string? NormalizePath(string? sourcePath) =>
        string.IsNullOrWhiteSpace(sourcePath)
            ? null
            : sourcePath.Replace('\\', '/');
}