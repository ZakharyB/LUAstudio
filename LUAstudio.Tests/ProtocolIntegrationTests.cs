using System.IO.Pipes;
using LUAstudio.Execution;
using LUAstudio.Execution.Abstractions;
using LUAstudio.Execution.Abstractions.Protocol;
using LUAstudio.ExecutionHost;
using Xunit;

namespace LUAstudio.Tests;

[Collection("ExecutionHost")]
public sealed class ProtocolIntegrationTests
{
    [Fact]
    public void Ide_processes_receive_distinct_private_pipe_names()
    {
        Assert.Equal("LUAstudio-15420", SandboxPipeNames.ForIdeProcess(15420));
        Assert.Equal("LUAstudio-28704", SandboxPipeNames.ForIdeProcess(28704));
        Assert.NotEqual(SandboxPipeNames.ForIdeProcess(15420), SandboxPipeNames.ForIdeProcess(28704));
    }

    [Fact]
    public async Task Host_process_supports_create_session_and_load_script()
    {
        await using var host = await InProcessExecutionHost.StartAsync();
        await using var client = new ExecutionHostClient(host.PipeName);
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(connectCts.Token);

        var sessionId = await client.CreateSessionAsync(new SessionConfiguration());
        await client.LoadScriptAsync(sessionId, "print(\"protocol\")", "protocol.lua");
    }

    [Fact]
    public async Task Host_commands_surface_invalid_session_transitions()
    {
        await using var host = await InProcessExecutionHost.StartAsync();
        await using var client = new ExecutionHostClient(host.PipeName);
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync(connectCts.Token);

        var sessionId = await client.CreateSessionAsync(new SessionConfiguration());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ExecuteAsync(sessionId));
        Assert.Contains("LoadScript", error.Message);
    }

    [Fact]
    public async Task Configured_host_executes_script_and_publishes_print_output()
    {
        await using var host = await InProcessExecutionHost.StartAsync();
        await using var client = new ExecutionHostClient(host.PipeName);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await client.ConnectAsync(timeout.Token);

        var configuration = new SessionConfiguration(EnableRobloxMocks: false, EnvironmentProfile: "lua");
        var sessionId = await client.CreateSessionAsync(configuration, timeout.Token);
        await client.LoadScriptAsync(sessionId, "print(5 + 12)", "sum.lua", timeout.Token);
        await client.ExecuteAsync(sessionId, timeout.Token);

        var output = new List<string>();
        var states = new List<ExecutionSessionState>();
        await foreach (var message in client.WatchEventsAsync(timeout.Token))
        {
            if (message.Kind == SandboxMessageKind.OutputLog &&
                SandboxPayload.As<OutputLogPayload>(message.Payload) is { } line)
            {
                output.Add(line.Text);
            }

            if (message.Kind == SandboxMessageKind.ExecutionStateChanged &&
                SandboxPayload.As<ExecutionStateChangedPayload>(message.Payload) is { } state)
            {
                states.Add(state.State);
            }

            if (message.Kind == SandboxMessageKind.ExecutionFinished)
            {
                break;
            }
        }

        Assert.Contains("17", output);
        Assert.Contains(ExecutionSessionState.Running, states);
        Assert.Equal(ExecutionSessionState.Stopped, states.Last());
    }
}

[Collection("ExecutionHost")]
public sealed class SandboxIsolationTests
{
    [Fact]
    public async Task Two_clients_can_connect_to_host_without_sharing_state()
    {
        await using var host = await InProcessExecutionHost.StartAsync();
        await using var clientA = new ExecutionHostClient(host.PipeName);
        await using var clientB = new ExecutionHostClient(host.PipeName);
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await clientA.ConnectAsync(connectCts.Token);
        await clientB.ConnectAsync(connectCts.Token);

        var sessionA = await clientA.CreateSessionAsync(new SessionConfiguration());
        var sessionB = await clientB.CreateSessionAsync(new SessionConfiguration());

        Assert.NotEqual(sessionA, sessionB);
    }
}

internal sealed class InProcessExecutionHost : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;

    private InProcessExecutionHost(string pipeName)
    {
        PipeName = pipeName;
        _serverTask = Task.Run(() => new SandboxHostServer(pipeName).RunAsync(_cts.Token));
    }

    public string PipeName { get; }

    public static async Task<InProcessExecutionHost> StartAsync(CancellationToken cancellationToken = default)
    {
        var host = new InProcessExecutionHost($"LUAstudio.Test.{Guid.NewGuid():N}");
        await host.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);
        return host;
    }

    private async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        while (!timeout.Token.IsCancellationRequested)
        {
            try
            {
                await using var probe = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await probe.ConnectAsync(200, timeout.Token).ConfigureAwait(false);
                return;
            }
            catch (TimeoutException)
            {
                await Task.Delay(25, timeout.Token).ConfigureAwait(false);
            }
        }

        throw new TimeoutException($"Execution host failed to listen on pipe '{PipeName}'.");
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort shutdown.
        }

        _cts.Dispose();
    }
}
