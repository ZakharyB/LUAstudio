using System.Diagnostics;
using LUAstudio.Execution;
using LUAstudio.Execution.Abstractions;
using Xunit;

namespace LUAstudio.Tests;

public sealed class ProtocolIntegrationTests
{
    [Fact]
    public async Task Host_process_supports_create_session_and_load_script()
    {
        var hostPath = Path.Combine(AppContext.BaseDirectory, "LUAstudio.ExecutionHost.exe");
        if (!File.Exists(hostPath))
        {
            hostPath = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "LUAstudio.ExecutionHost",
                "bin",
                "Debug",
                "net8.0",
                "LUAstudio.ExecutionHost.exe");
        }

        if (!File.Exists(hostPath))
        {
            return;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = "--pipe LUAstudio.ExecutionHost.Test",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(process);

        await using var client = new ExecutionHostClient("LUAstudio.ExecutionHost.Test");
        await client.ConnectAsync();

        var sessionId = await client.CreateSessionAsync(new SessionConfiguration());
        await client.LoadScriptAsync(sessionId, "print(\"protocol\")", "protocol.lua");

        process.Kill(entireProcessTree: true);
    }
}

public sealed class SandboxIsolationTests
{
    [Fact]
    public async Task Two_clients_can_connect_to_host_without_sharing_state()
    {
        var hostPath = ResolveHostPath();
        if (hostPath is null)
        {
            return;
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = hostPath,
            Arguments = "--pipe LUAstudio.ExecutionHost.Isolation",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        Assert.NotNull(process);

        await using var clientA = new ExecutionHostClient("LUAstudio.ExecutionHost.Isolation");
        await using var clientB = new ExecutionHostClient("LUAstudio.ExecutionHost.Isolation");
        await clientA.ConnectAsync();
        await clientB.ConnectAsync();

        var sessionA = await clientA.CreateSessionAsync(new SessionConfiguration());
        var sessionB = await clientB.CreateSessionAsync(new SessionConfiguration());

        Assert.NotEqual(sessionA, sessionB);

        process.Kill(entireProcessTree: true);
    }

    private static string? ResolveHostPath()
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "LUAstudio.ExecutionHost.exe");
        return File.Exists(candidate) ? candidate : null;
    }
}
