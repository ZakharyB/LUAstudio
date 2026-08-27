namespace LUAstudio.Execution.Abstractions;

public sealed record BreakpointSpec(
    int Line,
    string? Condition = null,
    int? HitCount = null,
    BreakpointKind Kind = BreakpointKind.Line);

public sealed record StackFrameInfo(
    int Id,
    string Name,
    string? SourcePath,
    int Line,
    int Column);

public sealed record ScopeInfo(
    int Id,
    string Name,
    VariableScopeKind Kind,
    int VariablesReference);

public sealed record VariableInfo(
    string Name,
    string Value,
    string TypeName,
    int? VariablesReference,
    bool HasNamedChildren);

public sealed record ExecutionErrorInfo(
    string Message,
    string? SourcePath,
    int Line,
    int Column,
    IReadOnlyList<string> StackTrace,
    string? ErrorKind = null);

public sealed record SessionConfiguration(
    int ExecutionTimeoutMs = 30_000,
    int MaxStackDepth = 256,
    bool AllowNetwork = false,
    bool EnableRobloxMocks = true,
    string EnvironmentProfile = "roblox");

public sealed record CreateSessionRequest(
    Guid SessionId,
    SessionConfiguration Configuration);

public sealed record LoadScriptRequest(
    Guid SessionId,
    string Source,
    string? SourcePath);

public sealed record SetBreakpointsRequest(
    Guid SessionId,
    string? SourcePath,
    IReadOnlyList<BreakpointSpec> Breakpoints);

public sealed record SessionCommandRequest(Guid SessionId);

public sealed record EvaluateExpressionRequest(
    Guid SessionId,
    int FrameId,
    string Expression);

public sealed record StackTraceRequest(Guid SessionId, int FrameId);

public sealed record ScopesRequest(Guid SessionId, int FrameId);

public sealed record VariablesRequest(Guid SessionId, int VariablesReference);

public sealed record WorkspaceModuleEntry(string Path, string Source);

public sealed record SetWorkspaceModulesRequest(
    Guid SessionId,
    IReadOnlyList<WorkspaceModuleEntry> Modules);

public sealed record LoadModuleRequest(
    Guid SessionId,
    string Path,
    string Source);

public sealed record ConfigureEnvironmentRequest(
    Guid SessionId,
    string EnvironmentProfile,
    bool EnableRobloxMocks,
    bool AllowNetwork);

public sealed record StepCompletedPayload(
    Guid SessionId,
    int Line,
    string? SourcePath,
    string Reason);

public sealed record ExecutionStateChangedPayload(
    Guid SessionId,
    ExecutionSessionState State);

public sealed record SandboxEnvelope(
    SandboxMessageKind Kind,
    Guid? SessionId,
    string? RequestId,
    object? Payload);

public sealed record SessionStartedPayload(Guid SessionId, ExecutionSessionState State);

public sealed record BreakpointHitPayload(
    Guid SessionId,
    int Line,
    string? SourcePath,
    string Reason);

public sealed record OutputLogPayload(Guid SessionId, string Channel, string Text);

public sealed record ExecutionFinishedPayload(
    Guid SessionId,
    ExecutionFinishReason Reason,
    double ElapsedMs);

public sealed record ErrorThrownPayload(Guid SessionId, ExecutionErrorInfo Error);

public sealed record StackTraceResponsePayload(
    Guid SessionId,
    IReadOnlyList<StackFrameInfo> Frames);

public sealed record ScopesResponsePayload(
    Guid SessionId,
    IReadOnlyList<ScopeInfo> Scopes);

public sealed record VariablesResponsePayload(
    Guid SessionId,
    IReadOnlyList<VariableInfo> Variables);

public sealed record EvaluateResultPayload(
    Guid SessionId,
    string Result,
    string? Error);

public interface IExecutionHostClient : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateSessionAsync(SessionConfiguration configuration, CancellationToken cancellationToken = default);

    Task LoadScriptAsync(Guid sessionId, string source, string? sourcePath, CancellationToken cancellationToken = default);

    Task SetBreakpointsAsync(
        Guid sessionId,
        string? sourcePath,
        IReadOnlyList<BreakpointSpec> breakpoints,
        CancellationToken cancellationToken = default);

    Task SetWorkspaceModulesAsync(
        Guid sessionId,
        IReadOnlyList<WorkspaceModuleEntry> modules,
        CancellationToken cancellationToken = default);

    Task LoadModuleAsync(
        Guid sessionId,
        string path,
        string source,
        CancellationToken cancellationToken = default);

    Task ConfigureEnvironmentAsync(
        Guid sessionId,
        ConfigureEnvironmentRequest configuration,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task ContinueAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task PauseAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task StepOverAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task StepIntoAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task StepOutAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task StopAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StackFrameInfo>> GetStackTraceAsync(Guid sessionId, int frameId = 0, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScopeInfo>> GetScopesAsync(Guid sessionId, int frameId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VariableInfo>> GetVariablesAsync(Guid sessionId, int variablesReference, CancellationToken cancellationToken = default);

    Task<string> EvaluateAsync(Guid sessionId, int frameId, string expression, CancellationToken cancellationToken = default);

    IAsyncEnumerable<SandboxEnvelope> WatchEventsAsync(CancellationToken cancellationToken = default);
}

public interface IExecutionHostProcessManager
{
    event EventHandler<ExecutionHostLogEventArgs>? Log;

    Task<IExecutionHostClient> StartHostAsync(CancellationToken cancellationToken = default);

    Task StopHostAsync(CancellationToken cancellationToken = default);
}

public sealed class ExecutionHostLogEventArgs : EventArgs
{
    public ExecutionHostLogEventArgs(string message) => Message = message;

    public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;

    public string Message { get; }
}
