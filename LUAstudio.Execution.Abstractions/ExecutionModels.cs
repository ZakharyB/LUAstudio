namespace LUAstudio.Execution.Abstractions;

public enum ExecutionSessionState
{
    Created,
    Loaded,
    Running,
    Paused,
    Stepping,
    Stopped,
    Crashed,
    TimedOut
}

public enum StepMode
{
    None,
    Over,
    Into,
    Out
}

public enum SandboxMessageKind
{
    CreateSession,
    LoadScript,
    SetBreakpoints,
    Execute,
    Continue,
    Pause,
    StepOver,
    StepInto,
    StepOut,
    Stop,
    EvaluateExpression,
    StackTrace,
    Scopes,
    Variables,
    SetWorkspaceModules,
    LoadModule,
    ConfigureEnvironment,

    SessionStarted,
    BreakpointHit,
    StepCompleted,
    ExecutionStateChanged,
    StackTraceResponse,
    ScopesResponse,
    VariablesResponse,
    VariableState,
    OutputLog,
    ErrorThrown,
    ExecutionFinished,
    SessionStopped,
    Ack,
    Error
}

public enum BreakpointKind
{
    Line,
    Conditional,
    HitCount
}

public enum VariableScopeKind
{
    Local,
    Upvalue,
    Global
}

public enum ExecutionFinishReason
{
    Completed,
    Error,
    Timeout,
    Cancelled,
    StackOverflow
}
