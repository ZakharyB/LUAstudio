using System.Text;
using LUAstudio.Execution.Abstractions;
using LUAstudio.ExecutionHost.Debugging;
using Luau;

namespace LUAstudio.ExecutionHost.Runtime;

public sealed class LuauRuntime : IDisposable
{
    private readonly LuauDebugController _debug;
    private readonly ModuleResolver _modules;
    private readonly LuauSandboxBootstrap _bootstrap;
    private readonly SessionConfiguration _configuration;
    private readonly ExecutionTraceRecorder _trace;
    private LuauState? _state;
    private InMemoryLuauRequirer? _requirer;
    private string? _sourcePath;
    private byte[]? _loadedBytecode;

    public LuauRuntime(
        LuauDebugController debug,
        ModuleResolver modules,
        SessionConfiguration configuration,
        ExecutionTraceRecorder? trace = null)
    {
        _debug = debug;
        _modules = modules;
        _configuration = configuration;
        _trace = trace ?? new ExecutionTraceRecorder();
        _bootstrap = new LuauSandboxBootstrap(_trace);
    }

    public event Action<string, string>? Output;

    public void Initialize(bool enableRobloxMocks)
    {
        DisposeState();
        _state = LuauState.Create();
        _bootstrap.Configure(_state, enableRobloxMocks, (channel, text) => Output?.Invoke(channel, text));
        _requirer = new InMemoryLuauRequirer(_modules);
        _state.OpenRequireLibrary(_requirer);
    }

    public void LoadScript(string source, string? sourcePath)
    {
        EnsureState();
        _sourcePath = sourcePath;
        _loadedBytecode = LuauCompiler.Compile(Encoding.UTF8.GetBytes(source));
    }

    public void Execute(CancellationToken cancellationToken)
    {
        EnsureState();
        if (_loadedBytecode is null)
        {
            throw new InvalidOperationException("LoadScript must be called before execution.");
        }

        _debug.Attach(_state!, _sourcePath);
        _state!.SetTop(0);
        _state.Load(_loadedBytecode);
        _debug.ExecuteFunction(_state, cancellationToken);
    }

    public LuauState State => _state ?? throw new InvalidOperationException("Runtime is not initialized.");

    public ExecutionTraceRecorder TraceRecorder => _trace;

    private void EnsureState()
    {
        if (_state is null)
        {
            throw new InvalidOperationException("Runtime is not initialized.");
        }
    }

    public void Dispose()
    {
        DisposeState();
    }

    private void DisposeState()
    {
        _state?.Dispose();
        _state = null;
        _requirer = null;
        _loadedBytecode = null;
    }
}
