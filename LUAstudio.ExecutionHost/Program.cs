using LUAstudio.Execution.Abstractions.Protocol;

namespace LUAstudio.ExecutionHost;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var pipeName = SandboxPipeNames.DefaultHostPipe;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--pipe", StringComparison.OrdinalIgnoreCase))
            {
                pipeName = args[i + 1];
            }
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var server = new SandboxHostServer(pipeName);
        await server.RunAsync(cts.Token).ConfigureAwait(false);
    }
}
