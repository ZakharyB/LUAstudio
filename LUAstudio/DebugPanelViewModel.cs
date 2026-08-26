using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LUAstudio.Editor.Debugging;
using LUAstudio.Execution.Abstractions;
using LUAstudio.Execution.Abstractions.Protocol;

namespace LUAstudio;

public sealed partial class DebugPanelViewModel : ObservableObject
{
    private IExecutionHostClient? _client;
    private DebugSessionCoordinator? _coordinator;
    private IBreakpointService? _breakpoints;
    private IDebugEditorNavigation? _navigation;
    private Guid _sessionId;
    private CancellationTokenSource? _eventLoopCts;

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

    [ObservableProperty]
    private string _watchExpression = string.Empty;

    [ObservableProperty]
    private string _terminalCommand = string.Empty;

    [ObservableProperty]
    private string _terminalWorkingDirectory = Environment.CurrentDirectory;

    [ObservableProperty]
    private StackFrameInfo? _selectedStackFrame;

    public ObservableCollection<StackFrameInfo> StackFrames { get; } = new();

    public ObservableCollection<VariableInfo> Variables { get; } = new();

    public ObservableCollection<string> OutputLog { get; } = new();

    public ObservableCollection<BreakpointListItem> Breakpoints { get; } = new();

    partial void OnSessionStateChanged(ExecutionSessionState value) => UpdateCommandStates();

    partial void OnIsConnectedChanged(bool value) => UpdateCommandStates();

    partial void OnSelectedStackFrameChanged(StackFrameInfo? value)
    {
        if (value is null)
        {
            return;
        }

        _navigation?.NavigateTo(value.SourcePath, value.Line);
        _ = RefreshVariablesAsync(value.Id);
    }

    public void Configure(
        DebugSessionCoordinator coordinator,
        IBreakpointService breakpoints,
        IDebugEditorNavigation navigation)
    {
        _coordinator = coordinator;
        _breakpoints = breakpoints;
        _navigation = navigation;
        _breakpoints.BreakpointsChanged += SyncBreakpointList;
        SyncBreakpointList();
    }

    public async Task InitializeAsync(IExecutionHostClient client)
    {
        if (ReferenceEquals(_client, client) && IsConnected)
        {
            return;
        }

        _eventLoopCts?.Cancel();
        _eventLoopCts?.Dispose();
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

        try
        {
            await foreach (var evt in _client.WatchEventsAsync(cancellationToken))
            {
                await Application.Current.Dispatcher.InvokeAsync(() => HandleEvent(evt));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal when reconnecting or closing the window.
        }
    }

    private void HandleEvent(SandboxEnvelope evt)
    {
        switch (evt.Kind)
        {
            case SandboxMessageKind.SessionStarted:
                if (SandboxPayload.As<SessionStartedPayload>(evt.Payload) is { } started)
                {
                    SessionState = started.State;
                }
                break;

            case SandboxMessageKind.ExecutionStateChanged:
                if (SandboxPayload.As<ExecutionStateChangedPayload>(evt.Payload) is { } stateChanged)
                {
                    SessionState = stateChanged.State;
                }
                break;

            case SandboxMessageKind.BreakpointHit:
                if (SandboxPayload.As<BreakpointHitPayload>(evt.Payload) is { } bpHit)
                {
                    SessionState = ExecutionSessionState.Paused;
                    OutputLog.Add($"Paused at line {bpHit.Line} in {bpHit.SourcePath ?? "<unknown>"} ({bpHit.Reason})");
                    _navigation?.NavigateTo(bpHit.SourcePath, bpHit.Line);
                    _ = RefreshDebugInspectionAsync();
                }
                break;

            case SandboxMessageKind.StepCompleted:
                if (SandboxPayload.As<StepCompletedPayload>(evt.Payload) is { } step)
                {
                    SessionState = ExecutionSessionState.Paused;
                    OutputLog.Add($"Step completed at line {step.Line} ({step.Reason})");
                    _navigation?.NavigateTo(step.SourcePath, step.Line);
                    _ = RefreshDebugInspectionAsync();
                }
                break;

            case SandboxMessageKind.OutputLog:
                if (SandboxPayload.As<OutputLogPayload>(evt.Payload) is { } output)
                {
                    // Script output should look like terminal output. In particular,
                    // print(17) is shown as "17", not as "[stdout] 17".
                    AppendTerminalText(output.Text);
                }
                break;

            case SandboxMessageKind.ExecutionFinished:
                if (SandboxPayload.As<ExecutionFinishedPayload>(evt.Payload) is { } finished)
                {
                    SessionState = ExecutionSessionState.Stopped;
                    OutputLog.Add($"Execution finished: {finished.Reason} ({finished.ElapsedMs:F2}ms)");
                }
                break;

            case SandboxMessageKind.ErrorThrown:
                if (SandboxPayload.As<ErrorThrownPayload>(evt.Payload) is { } error)
                {
                    SessionState = ExecutionSessionState.Crashed;
                    OutputLog.Add($"Error ({error.Error.ErrorKind ?? "runtime"}): {error.Error.Message}");
                    foreach (var frame in error.Error.StackTrace)
                    {
                        OutputLog.Add($"  at {frame}");
                    }

                    _navigation?.NavigateTo(error.Error.SourcePath, error.Error.Line);
                }
                break;

            case SandboxMessageKind.SessionStopped:
                SessionState = ExecutionSessionState.Stopped;
                OutputLog.Add("Session stopped.");
                break;

            case SandboxMessageKind.Error:
                OutputLog.Add($"Protocol error: {evt.Payload}");
                break;
        }
    }

    public async Task RunDocumentAsync(string source, string? sourcePath)
    {
        if (_coordinator is null)
        {
            return;
        }

        try
        {
            StackFrames.Clear();
            Variables.Clear();
            OutputLog.Add($"> run {sourcePath ?? "<untitled>"}");
            var client = await _coordinator.EnsureHostRunningAsync();
            if (_client != client)
            {
                await InitializeAsync(client);
            }

            _sessionId = await _coordinator.RunAsync(source, sourcePath);
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Error starting session: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RunOrContinueAsync()
    {
        if (SessionState == ExecutionSessionState.Paused && _client is not null && IsConnected)
        {
            await ContinueAsync();
            return;
        }

        if (RunActiveDocumentAsync is not null)
        {
            await RunActiveDocumentAsync();
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

            SelectedStackFrame = StackFrames.FirstOrDefault();
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

    [RelayCommand]
    private async Task EvaluateWatchAsync()
    {
        if (_client is null || !IsConnected || string.IsNullOrWhiteSpace(WatchExpression))
        {
            return;
        }

        try
        {
            var frameId = SelectedStackFrame?.Id ?? 0;
            var result = await _client.EvaluateAsync(_sessionId, frameId, WatchExpression);
            OutputLog.Add($"Watch: {WatchExpression} = {result}");
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Watch error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ClearOutput() => OutputLog.Clear();

    [RelayCommand]
    private async Task ExecuteTerminalCommandAsync()
    {
        var command = TerminalCommand.Trim();
        if (command.Length == 0)
        {
            return;
        }

        TerminalCommand = string.Empty;
        OutputLog.Add($"{TerminalWorkingDirectory}> {command}");

        if (string.Equals(command, "cls", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(command, "clear", StringComparison.OrdinalIgnoreCase))
        {
            OutputLog.Clear();
            return;
        }

        if (TryChangeDirectory(command))
        {
            return;
        }

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = isWindows ? "cmd.exe" : "/bin/bash",
                    Arguments = isWindows ? $"/d /s /c \"{command}\"" : $"-lc {QuoteBash(command)}",
                    WorkingDirectory = TerminalWorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            AppendTerminalText(await stdoutTask);
            AppendTerminalText(await stderrTask);
            if (process.ExitCode != 0)
            {
                OutputLog.Add($"[process exited with code {process.ExitCode}]");
            }
        }
        catch (Exception ex)
        {
            OutputLog.Add($"Terminal error: {ex.Message}");
        }
    }

    private bool TryChangeDirectory(string command)
    {
        if (!command.StartsWith("cd", StringComparison.OrdinalIgnoreCase) ||
            (command.Length > 2 && !char.IsWhiteSpace(command[2])))
        {
            return false;
        }

        var requested = command.Length == 2
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : command[2..].Trim().Trim('"');
        string path;
        try
        {
            path = Path.GetFullPath(requested, TerminalWorkingDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            OutputLog.Add($"The directory name is invalid: {requested}");
            return true;
        }
        if (!Directory.Exists(path))
        {
            OutputLog.Add($"The system cannot find the path specified: {path}");
            return true;
        }

        TerminalWorkingDirectory = path;
        return true;
    }

    private void AppendTerminalText(string text)
    {
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.Length > 0)
            {
                OutputLog.Add(line);
            }
        }
    }

    private static string QuoteBash(string command) => $"'{command.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    public Func<(string? SourcePath, int Line)?>? GetActiveEditorLocation { get; set; }

    public Func<Task>? RunActiveDocumentAsync { get; set; }

    [RelayCommand]
    private void ToggleActiveBreakpoint()
    {
        var location = GetActiveEditorLocation?.Invoke();
        if (location is null)
        {
            return;
        }

        ToggleBreakpoint(location.Value.SourcePath, location.Value.Line);
    }

    public void ToggleBreakpoint(string? sourcePath, int line) =>
        _breakpoints?.ToggleBreakpoint(sourcePath, line);

    private void SyncBreakpointList()
    {
        Breakpoints.Clear();
        if (_breakpoints is null)
        {
            return;
        }

        foreach (var bp in _breakpoints.Breakpoints.OrderBy(b => b.SourcePath).ThenBy(b => b.Line))
        {
            Breakpoints.Add(new BreakpointListItem(bp.SourcePath, bp.Line));
        }
    }

    private async Task RefreshDebugInspectionAsync()
    {
        await RefreshStackAsync();
        await RefreshVariablesAsync();
    }

    private void UpdateCommandStates()
    {
        CanRun = IsConnected && SessionState is ExecutionSessionState.Created or ExecutionSessionState.Loaded or ExecutionSessionState.Stopped or ExecutionSessionState.Paused;
        CanStop = IsConnected && SessionState is ExecutionSessionState.Running or ExecutionSessionState.Paused or ExecutionSessionState.Stepping;
        CanPause = IsConnected && SessionState == ExecutionSessionState.Running;
        CanStepOver = IsConnected && SessionState == ExecutionSessionState.Paused;
        CanStepInto = IsConnected && SessionState == ExecutionSessionState.Paused;
        CanStepOut = IsConnected && SessionState == ExecutionSessionState.Paused;
        CanContinue = IsConnected && SessionState == ExecutionSessionState.Paused;
    }

    public async ValueTask DisposeAsync()
    {
        if (_breakpoints is not null)
        {
            _breakpoints.BreakpointsChanged -= SyncBreakpointList;
        }

        _eventLoopCts?.Cancel();
        _eventLoopCts?.Dispose();
        if (_coordinator is not null)
        {
            await _coordinator.DisposeAsync();
        }
    }
}

public sealed record BreakpointListItem(string? SourcePath, int Line);
