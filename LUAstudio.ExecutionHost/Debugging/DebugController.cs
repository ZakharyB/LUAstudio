using System.Collections.Concurrent;
using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Runtime;

namespace LUAstudio.ExecutionHost.Debugging;

public sealed class DebugController
{
    private readonly ConcurrentDictionary<int, VariableStore> _variableStores = new();
    private readonly List<BreakpointSpec> _breakpoints = [];
    private readonly List<StackFrameInfo> _frames = [];
    private int _nextVariableRef = 1;
    private int _nextFrameId = 1;
    private string? _breakpointSourcePath;
    private StepMode _stepMode = StepMode.None;
    private int _stepDepth;
    private int _stepFrameId;
    private bool _pauseRequested;
    private TaskCompletionSource<bool>? _pauseGate;

    public event Action<int, string?>? BreakpointHit;

    public void ResetExecutionControl()
    {
        _stepMode = StepMode.None;
        _pauseRequested = false;
        _frames.Clear();
        _variableStores.Clear();
        _nextVariableRef = 1;
        _nextFrameId = 1;
        ReleasePauseGate();
    }

    public void SetBreakpoints(string? sourcePath, IReadOnlyList<BreakpointSpec> breakpoints)
    {
        _breakpointSourcePath = sourcePath;
        _breakpoints.Clear();
        _breakpoints.AddRange(breakpoints);
    }

    public void Continue()
    {
        _stepMode = StepMode.None;
        ReleasePauseGate();
    }

    public void RequestPause()
    {
        _pauseRequested = true;
    }

    public void StepOver()
    {
        _stepMode = StepMode.Over;
        _stepDepth = _frames.Count;
        ReleasePauseGate();
    }

    public void StepInto()
    {
        _stepMode = StepMode.Into;
        ReleasePauseGate();
    }

    public void StepOut()
    {
        _stepMode = StepMode.Out;
        _stepFrameId = Math.Max(0, _frames.Count - 1);
        ReleasePauseGate();
    }

    public async Task OnLineAsync(int line, string? sourcePath, RuntimeFrame frame, CancellationToken cancellationToken)
    {
        UpdateFrame(frame, line, sourcePath);

        if (ShouldBreak(line, sourcePath))
        {
            BreakpointHit?.Invoke(line, sourcePath);
            await WaitForResumeAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (_pauseRequested)
        {
            _pauseRequested = false;
            await WaitForResumeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public IReadOnlyList<StackFrameInfo> GetStackFrames(string? fallbackSourcePath) =>
        _frames.Count == 0
            ? [new StackFrameInfo(0, "<main>", fallbackSourcePath, 1, 1)]
            : _frames.ToArray();

    public IReadOnlyList<ScopeInfo> GetScopes(int frameId)
    {
        var localsRef = _nextVariableRef++;
        var globalsRef = _nextVariableRef++;
        _variableStores[localsRef] = new VariableStore(GetFrameLocals(frameId));
        _variableStores[globalsRef] = new VariableStore(GetGlobalSnapshot());
        return
        [
            new ScopeInfo(localsRef, "Locals", VariableScopeKind.Local, localsRef),
            new ScopeInfo(globalsRef, "Globals", VariableScopeKind.Global, globalsRef)
        ];
    }

    public IReadOnlyList<VariableInfo> GetVariables(int variablesReference)
    {
        if (!_variableStores.TryGetValue(variablesReference, out var store))
        {
            return Array.Empty<VariableInfo>();
        }

        return store.ToVariableInfos(ref _nextVariableRef, _variableStores);
    }

    public string Evaluate(int frameId, string expression)
    {
        var locals = GetFrameLocals(frameId);
        if (locals.TryGetValue(expression.Trim(), out var value))
        {
            return SandboxValueFormatter.Format(value);
        }

        var globals = GetGlobalSnapshot();
        return globals.TryGetValue(expression.Trim(), out var global)
            ? SandboxValueFormatter.Format(global)
            : "nil";
    }

    private void UpdateFrame(RuntimeFrame frame, int line, string? sourcePath)
    {
        var info = new StackFrameInfo(
            _nextFrameId++,
            frame.Name,
            sourcePath,
            line,
            1);

        if (_frames.Count == 0)
        {
            _frames.Add(info);
        }
        else
        {
            _frames[^1] = info;
        }
    }

    private bool ShouldBreak(int line, string? sourcePath)
    {
        foreach (var bp in _breakpoints)
        {
            if (bp.Line != line)
            {
                continue;
            }

            if (_breakpointSourcePath is not null &&
                sourcePath is not null &&
                !string.Equals(_breakpointSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private async Task WaitForResumeAsync(CancellationToken cancellationToken)
    {
        _pauseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() => _pauseGate.TrySetCanceled(cancellationToken));
        await _pauseGate.Task.ConfigureAwait(false);

        if (_stepMode == StepMode.Over)
        {
            _stepMode = StepMode.None;
        }
    }

    private void ReleasePauseGate()
    {
        _pauseGate?.TrySetResult(true);
        _pauseGate = null;
    }

    private Dictionary<string, object?> GetFrameLocals(int frameId) =>
        _frames.Count == 0
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);

    private Dictionary<string, object?> GetGlobalSnapshot() =>
        new Dictionary<string, object?>(StringComparer.Ordinal);
}

internal sealed class VariableStore
{
    private readonly IReadOnlyDictionary<string, object?> _values;

    public VariableStore(IReadOnlyDictionary<string, object?> values) => _values = values;

    public IReadOnlyList<VariableInfo> ToVariableInfos(
        ref int nextRef,
        ConcurrentDictionary<int, VariableStore> stores)
    {
        var list = new List<VariableInfo>();
        foreach (var (name, value) in _values)
        {
            int? childRef = null;
            var hasChildren = value is IDictionary<string, object?>;
            if (hasChildren)
            {
                childRef = nextRef++;
                stores[childRef.Value] = new VariableStore(new Dictionary<string, object?>((IDictionary<string, object?>)value!));
            }

            list.Add(new VariableInfo(
                name,
                SandboxValueFormatter.Format(value),
                SandboxValueFormatter.TypeName(value),
                childRef,
                hasChildren));
        }

        return list;
    }
}

internal static class SandboxValueFormatter
{
    public static string Format(object? value) => value switch
    {
        null => "nil",
        string s => $"\"{s}\"",
        bool b => b ? "true" : "false",
        _ => value.ToString() ?? "nil"
    };

    public static string TypeName(object? value) => value switch
    {
        null => "nil",
        string => "string",
        bool => "boolean",
        double => "number",
        int => "number",
        IDictionary<string, object?> => "table",
        _ => value.GetType().Name.ToLowerInvariant()
    };
}

public sealed class RuntimeFrame
{
    public RuntimeFrame(string name, IDictionary<string, object?> locals, IDictionary<string, object?>? upvalues = null)
    {
        Name = name;
        Locals = locals;
        Upvalues = upvalues ?? new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    public string Name { get; }

    public IDictionary<string, object?> Locals { get; }

    public IDictionary<string, object?> Upvalues { get; }
}
