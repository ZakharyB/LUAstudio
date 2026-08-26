using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Runtime;
using Luau;
using Luau.Native;
using static Luau.Native.NativeMethods;

namespace LUAstudio.ExecutionHost.Debugging;

public sealed class LuauDebugController
{
    private const int MaxTableDepth = 3;
    private const int MaxTableKeys = 100;

    private readonly ConcurrentDictionary<int, VariableStore> _variableStores = new();
    private readonly Dictionary<(string? Path, int Line), BreakpointSpec> _breakpoints = new();
    private readonly Dictionary<(string? Path, int Line), int> _hitCounts = new();
    private readonly List<StackFrameInfo> _frames = [];
    private readonly object _sync = new();
    private LuauState? _state;
    private int _nextVariableRef = 1;
    private int _nextFrameId = 1;
    private StepMode _stepMode = StepMode.None;
    private int _stepStartDepth;
    private bool _pauseRequested;
    private bool _stopRequested;
    private TaskCompletionSource<bool>? _pauseGate;
    private string? _mainSourcePath;
    private string? _lastPauseReason;
    private int _maxStackDepth = 256;

    public event Action<int, string?, string>? Paused;

    public void Attach(LuauState state, string? mainSourcePath)
    {
        _state = state;
        _mainSourcePath = mainSourcePath;
    }

    public void ConfigureLimits(int maxStackDepth) => _maxStackDepth = Math.Max(1, maxStackDepth);

    public void ResetExecutionControl()
    {
        lock (_sync)
        {
            _stepMode = StepMode.None;
            _pauseRequested = false;
            _stopRequested = false;
            _frames.Clear();
            _variableStores.Clear();
            _hitCounts.Clear();
            _nextVariableRef = 1;
            _nextFrameId = 1;
            ReleasePauseGate();
        }
    }

    public void SetBreakpoints(string? sourcePath, IReadOnlyList<BreakpointSpec> breakpoints)
    {
        lock (_sync)
        {
            // Replace only this document's breakpoints. The IDE sends one group per
            // source file; clearing the entire map here made only the last file work.
            foreach (var key in _breakpoints.Keys.Where(key =>
                         string.Equals(key.Path, sourcePath, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _breakpoints.Remove(key);
                _hitCounts.Remove(key);
            }

            foreach (var bp in breakpoints)
            {
                _breakpoints[(sourcePath, bp.Line)] = bp;
            }
        }
    }

    public void Continue()
    {
        lock (_sync)
        {
            _stepMode = StepMode.None;
            DisableSingleStep();
            ReleasePauseGate();
        }
    }

    public void RequestPause()
    {
        _pauseRequested = true;
        // A flag alone is never observed by a running VM. Force the interpreter to
        // yield at its next safe instruction so Pause works without a breakpoint.
        Interrupt();
    }

    public void RequestStop() => _stopRequested = true;

    public void StepOver()
    {
        lock (_sync)
        {
            _stepMode = StepMode.Over;
            _stepStartDepth = GetStackDepth();
            EnableSingleStep();
            ReleasePauseGate();
        }
    }

    public void StepInto()
    {
        lock (_sync)
        {
            _stepMode = StepMode.Into;
            _stepStartDepth = GetStackDepth();
            EnableSingleStep();
            ReleasePauseGate();
        }
    }

    public void StepOut()
    {
        lock (_sync)
        {
            _stepMode = StepMode.Out;
            _stepStartDepth = GetStackDepth();
            EnableSingleStep();
            ReleasePauseGate();
        }
    }

    public unsafe void ExecuteFunction(LuauState state, CancellationToken cancellationToken)
    {
        Attach(state, _mainSourcePath);
        var L = state.AsPointer();
        ApplyNativeBreakpoints(L, -1);

        var status = lua_pcall(L, 0, 0, 0);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_stopRequested)
            {
                lua_break(L);
                break;
            }

            if (status == (int)lua_Status.LUA_OK)
            {
                break;
            }

            if (status == (int)lua_Status.LUA_BREAK)
            {
                if (GetStackDepth() > _maxStackDepth)
                {
                    throw new SandboxRuntimeException(
                        $"Stack depth exceeded limit of {_maxStackDepth}.",
                        _mainSourcePath,
                        1,
                        1,
                        "stack_overflow");
                }

                RefreshFrames(L);
                var (line, source) = GetCurrentLocation(L);
                var reason = DeterminePauseReason(line, source);
                if (reason is null)
                {
                    status = lua_resume(L, null, 0);
                    continue;
                }

                _lastPauseReason = reason;
                DisableSingleStep();
                Paused?.Invoke(line, source, reason);

                WaitForResumeAsync(cancellationToken).GetAwaiter().GetResult();
                if (_stopRequested)
                {
                    break;
                }

                ApplyStepModeAfterResume();
                status = lua_resume(L, null, 0);
                continue;
            }

            ThrowForStatus(L, status);
            break;
        }
    }

    public void Interrupt()
    {
        if (_state is null)
        {
            return;
        }

        unsafe
        {
            lua_break(_state.AsPointer());
        }
    }

    public IReadOnlyList<StackFrameInfo> GetStackFrames(string? fallbackSourcePath)
    {
        lock (_sync)
        {
            if (_frames.Count == 0)
            {
                return [new StackFrameInfo(0, "<main>", fallbackSourcePath, 1, 1)];
            }

            return _frames.ToArray();
        }
    }

    public IReadOnlyList<ScopeInfo> GetScopes(int frameId)
    {
        var localsRef = Interlocked.Increment(ref _nextVariableRef);
        var upvaluesRef = Interlocked.Increment(ref _nextVariableRef);
        var globalsRef = Interlocked.Increment(ref _nextVariableRef);

        _variableStores[localsRef] = new VariableStore(CaptureLocals(frameId));
        _variableStores[upvaluesRef] = new VariableStore(CaptureUpvalues(frameId));
        _variableStores[globalsRef] = new VariableStore(CaptureGlobals());

        return
        [
            new ScopeInfo(localsRef, "Locals", VariableScopeKind.Local, localsRef),
            new ScopeInfo(upvaluesRef, "Upvalues", VariableScopeKind.Upvalue, upvaluesRef),
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
        if (_state is null)
        {
            return "nil";
        }

        try
        {
            var chunk = $"return ({expression})";
            var results = LuauScriptRunner.DoString(_state, chunk);
            return results.Length == 0 ? "nil" : LuauValueFormatter.Format(results[0]);
        }
        catch (Exception ex)
        {
            return $"error: {ex.Message}";
        }
    }

    private string? DeterminePauseReason(int line, string? sourcePath)
    {
        if (_pauseRequested)
        {
            _pauseRequested = false;
            return "pause";
        }

        if (ShouldBreakAt(line, sourcePath, out var reason))
        {
            return reason;
        }

        if (_stepMode != StepMode.None && ShouldStopForStep())
        {
            return _stepMode switch
            {
                StepMode.Over => "stepOver",
                StepMode.Into => "stepInto",
                StepMode.Out => "stepOut",
                _ => "step"
            };
        }

        return null;
    }

    private bool ShouldBreakAt(int line, string? sourcePath, out string reason)
    {
        reason = "breakpoint";
        if (!_breakpoints.TryGetValue((sourcePath, line), out var bp) &&
            !_breakpoints.TryGetValue((null, line), out bp))
        {
            return false;
        }

        var key = (sourcePath, line);
        _hitCounts.TryGetValue(key, out var hits);
        hits++;
        _hitCounts[key] = hits;

        if (bp.HitCount is > 0 && hits < bp.HitCount)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(bp.Condition) && _state is not null)
        {
            try
            {
                var result = Evaluate(0, bp.Condition);
                if (result.StartsWith("error:", StringComparison.Ordinal))
                {
                    return false;
                }

                if (result is "false" or "nil")
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldStopForStep()
    {
        var depth = GetStackDepth();
        return _stepMode switch
        {
            StepMode.Into => true,
            StepMode.Over => depth <= _stepStartDepth,
            StepMode.Out => depth < _stepStartDepth,
            _ => false
        };
    }

    private void ApplyStepModeAfterResume()
    {
        unsafe
        {
            if (_stepMode != StepMode.None)
            {
                EnableSingleStep();
            }
            else
            {
                DisableSingleStep();
            }
        }
    }

    private unsafe void ApplyNativeBreakpoints(lua_State* L, int funcIndex)
    {
        lock (_sync)
        {
            foreach (var ((path, line), _) in _breakpoints)
            {
                if (path is not null && _mainSourcePath is not null &&
                    !string.Equals(path, _mainSourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                lua_breakpoint(L, funcIndex, line, 1);
            }
        }
    }

    private unsafe void EnableSingleStep()
    {
        if (_state is null)
        {
            return;
        }

        lua_singlestep(_state.AsPointer(), 1);
    }

    private unsafe void DisableSingleStep()
    {
        if (_state is null)
        {
            return;
        }

        lua_singlestep(_state.AsPointer(), 0);
    }

    private int GetStackDepth()
    {
        if (_state is null)
        {
            return 0;
        }

        unsafe
        {
            var L = _state.AsPointer();
            var depth = 0;
            lua_Debug ar = default;
            var what = Encoding.UTF8.GetBytes("S");
            fixed (byte* whatPtr = what)
            {
                for (var level = 0; ; level++)
                {
                    if (lua_getinfo(L, level, whatPtr, &ar) == 0)
                    {
                        break;
                    }

                    depth++;
                }
            }

            return depth;
        }
    }

    private unsafe void RefreshFrames(lua_State* L)
    {
        lock (_sync)
        {
            _frames.Clear();
            _nextFrameId = 1;
            lua_Debug ar = default;
            var what = Encoding.UTF8.GetBytes("Snl");
            fixed (byte* whatPtr = what)
            {
                for (var level = 0; ; level++)
                {
                    if (lua_getinfo(L, level, whatPtr, &ar) == 0)
                    {
                        break;
                    }

                    var name = ReadNativeString(ar.name) ?? "<anonymous>";
                    var source = NormalizeSource(ReadNativeString(ar.source));
                    _frames.Add(new StackFrameInfo(_nextFrameId++, name, source, ar.currentline, 1));
                }
            }
        }
    }

    private unsafe (int Line, string? Source) GetCurrentLocation(lua_State* L)
    {
        lua_Debug ar = default;
        var what = Encoding.UTF8.GetBytes("Sl");
        fixed (byte* whatPtr = what)
        {
            if (lua_getinfo(L, 0, whatPtr, &ar) == 0)
            {
                return (1, _mainSourcePath);
            }

            return (ar.currentline, NormalizeSource(ReadNativeString(ar.source)) ?? _mainSourcePath);
        }
    }

    private Dictionary<string, object?> CaptureLocals(int frameId)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_state is null)
        {
            return result;
        }

        unsafe
        {
            var L = _state.AsPointer();
            // Protocol frame ids are one-based stable identifiers, whereas the
            // native debug API addresses frames by a zero-based stack level.
            var level = frameId <= 0 ? 0 : frameId - 1;
            for (var index = 1; ; index++)
            {
                var namePtr = lua_getlocal(L, level, index);
                if (namePtr == null)
                {
                    break;
                }

                var name = ReadNativeString(namePtr) ?? $"local{index}";
                result[name] = ReadStackValue(_state, -1);
                lua_pop(L, 1);
            }
        }

        return result;
    }

    private Dictionary<string, object?> CaptureUpvalues(int frameId)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_state is null)
        {
            return result;
        }

        unsafe
        {
            var L = _state.AsPointer();
            var funcIndex = -lua_gettop(L);
            for (var index = 1; ; index++)
            {
                var namePtr = lua_getupvalue(L, funcIndex, index);
                if (namePtr == null)
                {
                    break;
                }

                var name = ReadNativeString(namePtr) ?? $"upvalue{index}";
                result[name] = ReadStackValue(_state, -1);
                lua_pop(L, 1);
            }
        }

        return result;
    }

    private Dictionary<string, object?> CaptureGlobals()
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_state is null)
        {
            return result;
        }

        foreach (var key in EnumerateGlobalKeys(_state))
        {
            if (result.Count >= MaxTableKeys)
            {
                break;
            }

            result[key] = ConvertLuauValue(_state[key]);
        }

        return result;
    }

    private static IEnumerable<string> EnumerateGlobalKeys(LuauState state)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "print", "game", "workspace", "script", "require", "Instance"
        };

        foreach (var key in keys)
        {
            yield return key;
        }
    }

    private static object? ReadStackValue(LuauState state, int index) =>
        ConvertLuauValue(state.ToValue(index));

    private static object? ConvertLuauValue(LuauValue value)
    {
        return value.Type switch
        {
            LuauType.Nil => null,
            LuauType.Boolean => value.Read<bool>(),
            LuauType.Number => value.Read<double>(),
            LuauType.String => value.Read<string>(),
            LuauType.Table => ConvertTable(value.Read<LuauTable>()),
            _ => LuauValueFormatter.Format(value)
        };
    }

    private static Dictionary<string, object?> ConvertTable(LuauTable table, int depth = 0)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (depth >= MaxTableDepth)
        {
            return result;
        }

        var count = 0;
        foreach (var pair in table)
        {
            if (count++ >= MaxTableKeys)
            {
                break;
            }

            if (pair.Key.Type != LuauType.String)
            {
                continue;
            }

            var key = pair.Key.Read<string>();
            result[key] = pair.Value.Type switch
            {
                LuauType.Table => ConvertTable(pair.Value.Read<LuauTable>(), depth + 1),
                _ => ConvertLuauValue(pair.Value)
            };
        }

        return result;
    }

    private unsafe static void ThrowForStatus(lua_State* L, int status)
    {
        var message = lua_tostring(L, -1);
        var text = ReadNativeString(message) ?? $"Luau execution failed with status {status}";
        throw new SandboxRuntimeException(text, null, 1, 1, "runtime");
    }

    private static string? NormalizeSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        return source.StartsWith('@') ? source[1..] : source;
    }

    private static unsafe string? ReadNativeString(byte* value) =>
        value == null ? null : Marshal.PtrToStringUTF8((IntPtr)value);

    private async Task WaitForResumeAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> gate;
        lock (_sync)
        {
            gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pauseGate = gate;
        }

        using var reg = cancellationToken.Register(() => gate.TrySetCanceled(cancellationToken));
        await gate.Task.ConfigureAwait(false);

        if (_stepMode == StepMode.Over && _lastPauseReason?.StartsWith("step", StringComparison.Ordinal) == true)
        {
            _stepMode = StepMode.None;
        }
    }

    private void ReleasePauseGate()
    {
        lock (_sync)
        {
            _pauseGate?.TrySetResult(true);
            _pauseGate = null;
        }
    }
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
        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        IDictionary<string, object?> => "{...}",
        _ => value.ToString() ?? "nil"
    };

    public static string TypeName(object? value) => value switch
    {
        null => "nil",
        string => "string",
        bool => "boolean",
        double or float or int => "number",
        IDictionary<string, object?> => "table",
        _ => value.GetType().Name.ToLowerInvariant()
    };
}

internal static class LuauValueFormatter
{
    public static string Format(LuauValue value) => value.Type switch
    {
        LuauType.Nil => "nil",
        LuauType.Boolean => value.Read<bool>() ? "true" : "false",
        LuauType.Number => value.Read<double>().ToString(System.Globalization.CultureInfo.InvariantCulture),
        LuauType.String => value.Read<string>(),
        LuauType.Table => "{...}",
        LuauType.Funciton => "function",
        _ => value.ToString() ?? "nil"
    };
}
