using LUAstudio.Execution.Abstractions.Protocol;

namespace LUAstudio.ExecutionHost;

public static class Program
{
    public static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine(
                $"ExecutionHost starting PID={Environment.ProcessId}");

            Console.WriteLine(
                $"Arguments: {string.Join(" ", args)}");

            var pipeName = SandboxPipeNames.DefaultHostPipe;

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(
                        args[i],
                        "--pipe",
                        StringComparison.OrdinalIgnoreCase))
                {
                    pipeName = args[i + 1];
                    break;
                }
            }

            Console.WriteLine(
                $"Using named pipe: {pipeName}");

            using var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var server = new SandboxHostServer(pipeName);

            Console.WriteLine(
                $"Starting SandboxHostServer on {pipeName}");

            await server
                .RunAsync(cts.Token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
        }
    }
}