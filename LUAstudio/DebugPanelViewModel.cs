using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUAstudio.Execution.Abstractions;
using LUAstudio.Execution.Abstractions.Protocol;

namespace LUAstudio;

public sealed partial class DebugPanelViewModel : ObservableObject
{
    private IExecutionHostClient? _client;
    private Guid _sessionId;
    private CancellationTokenSource? _eventLoopCts;
    private readonly Dictionary<int, BreakpointSpec> _breakpoints = new();

    [ObservableProperty]
    private ExecutionSessionState _sessionState = ExecutionSessionState.Stopped;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _canRun;

    [ObservableProperty]
    private bool _canStop;

    [ObservableProperty]
    private bool _canPause;

    [ObservableProperty]
    private bool _canStepOver;

    [ObservableProperty]
    private bool _canStepInto;

    [ObservableProperty]
    private bool _canStepOut;

    [ObservableProperty]
    private bool _canContinue;

    public ObservableCollection<StackFrameInfo> StackFrames { get; } = new();

    public ObservableCollection<VariableInfo> Variables { get; } = new();

    public ObservableCollection<string> OutputLog { get; } = new();

    public ObservableCollection<BreakpointListItem> Breakpoints { get; } = new();

    partial void OnSessionStateChanged(ExecutionSessionState value)
    {
        UpdateCommandStates();
    }

    partial void OnIsConnectedChanged(bool value)
    {
        UpdateCommandStates();
    }

    private void UpdateCommandStates()
    {
        CanRun = IsConnected && SessionState is ExecutionSessionState.Created or ExecutionSessionState.Loaded or ExecutionSessionState.Stopped;
        CanStop = IsConnected && SessionState is ExecutionSessionState.Running or ExecutionSessionState.Paused or ExecutionSessionState.Stepping;
        CanPause = IsConnected && SessionState == ExecutionSessionState.Running;
        CanStepOver = IsConnected && SessionState == ExecutionSessionState.Paused;
        CanStepInto = IsConnected && SessionState == ExecutionSessionState.Paused;
        CanStepOut = IsConnected && SessionState == ExecutionSessionState.Paused;
        CanContinue = IsConnected && SessionState == ExecutionSessionState.Paused;
    }

    public async Task InitializeAsync(IExecutionHostClient client)
    {
        _client = client;
        await _client.ConnectAsync();
        IsConnected = true;
        _eventLoopCts = new CancellationTokenSource();
        _ = EventLoopAsync(_eventLoopCts.Token);
    }

    private async Task EventLoopAsync(CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            return;
        }

        await foreach (var evt in _client.WatchEventsAsync(cancellationToken))
        {
            await Application.Current.Dispatcher.InvokeAsync(() => HandleEvent(evt));
        }
    }

    private void HandleEvent(SandboxEnvelope evt)
    {
        switch (evt.Kind)
        {
            case SandboxMessageKind.SessionStarted:
                var started = SandboxPayload.As<SessionStartedPayload>(evt.Payload);
                if (started is not null)
                {
                    SessionState = started.State;
                }
                break;

            case SandboxMessageKind.BreakpointHit:
                var bpHit = SandboxPayload.As<BreakpointHitPayload>(evt.Payload);
                if (bpHit is not null)
                {
                    SessionState = ExecutionSessionState.Paused;
                    OutputLog.Add($"Breakpoint hit at line {bpHit.Line} in {bpHit.SourcePath ?? "<unknown>"}");
                }
                break;

            case SandboxMessageKind.OutputLog:
                var output = SandboxPayload.As<OutputLogPayload>(evt.Payload);
                if (output is not null)
                {
                    OutputLog.Add($"[{output.Channel}] {output.Text}");
                }
                break;

            case SandboxMessageKind.ExecutionFinished:
                var finished = SandboxPayload.As<ExecutionFinishedPayload>(evt.Payload);
                if (finished is not null)
                {
                    SessionState = ExecutionSessionState.Stopped;
                    OutputLog.Add($"Execution finished: {finished.Reason} ({finished.ElapsedMs:F2}ms)");
                }
                break;

            case SandboxMessageKind.ErrorThrown:
                var error = SandboxPayload.As<ErrorThrownPayload>(evt.Payload);
                if (error is not null)
                {
                    SessionState = ExecutionSessionState.Crashed;
                    OutputLog.Add($"Error: {error.Error.Message}");
                }
                break;
        }
    }

    public async Task RunDocumentAsync(string source, string? sourcePath)
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            _sessionId = await _client.CreateSessionAsync(new SessionConfiguration());
            SessionState = ExecutionSessionState.Created;

            if (source is not null)
            {
                await _client.LoadScriptAsync(_sessionId, source, sourcePath);
                SessionState = ExecutionSessionState.Loaded;
            }

            await _client.ExecuteAsync(_sessionId);
            SessionState = ExecutionSessionState.Running;
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error starting session: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            await _client.StopAsync(_sessionId);
            SessionState = ExecutionSessionState.Stopped;
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error stopping session: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PauseAsync()
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            await _client.PauseAsync(_sessionId);
            SessionState = ExecutionSessionState.Paused;
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error pausing session: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StepOverAsync()
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            await _client.StepOverAsync(_sessionId);
            SessionState = ExecutionSessionState.Stepping;
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error stepping over: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StepIntoAsync()
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            await _client.StepIntoAsync(_sessionId);
            SessionState = ExecutionSessionState.Stepping;
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error stepping into: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task StepOutAsync()
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            await _client.StepOutAsync(_sessionId);
            SessionState = ExecutionSessionState.Stepping;
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error stepping out: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ContinueAsync()
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            await _client.ContinueAsync(_sessionId);
            SessionState = ExecutionSessionState.Running;
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error continuing: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshStackAsync()
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            var frames = await _client.GetStackTraceAsync(_sessionId);
            StackFrames.Clear();
            foreach (var frame in frames)
            {
                StackFrames.Add(frame);
            }
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error getting stack trace: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshVariablesAsync(int frameId = 0)
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            var scopes = await _client.GetScopesAsync(_sessionId, frameId);
            Variables.Clear();
            
            foreach (var scope in scopes)
            {
                var vars = await _client.GetVariablesAsync(_sessionId, scope.VariablesReference);
                foreach (var v in vars)
                {
                    Variables.Add(v);
                }
            }
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error getting variables: {ex.Message}");
        }
    }

    public void SetBreakpoint(string? sourcePath, int line)
    {
        var key = line;
        if (_breakpoints.ContainsKey(key))
        {
            _breakpoints.Remove(key);
            var existing = Breakpoints.FirstOrDefault(b => b.Line == line && b.SourcePath == sourcePath);
            if (existing is not null)
            {
                Breakpoints.Remove(existing);
            }
        }
        else
        {
            var bp = new BreakpointSpec(line);
            _breakpoints[key] = bp;
            Breakpoints.Add(new BreakpointListItem(sourcePath, line));
        }

        _ = SendBreakpointsAsync(sourcePath);
    }

    private async Task SendBreakpointsAsync(string? sourcePath)
    {
        if (_client is null || !IsConnected)
        {
            return;
        }

        try
        {
            var bps = _breakpoints.Values.ToList();
            await _client.SetBreakpointsAsync(_sessionId, sourcePath, bps);
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error setting breakpoints: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _eventLoopCts?.Cancel();
        _eventLoopCts?.Dispose();
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }
    }
}

public sealed record BreakpointListItem(string? SourcePath, int Line);
