namespace LUAstudio.Core.Logging;

public sealed class DebugAppLogger : IAppLogger
{
    public void Trace(string message)
    {
        Write("TRACE", message, null);
    }

    public void Info(string message)
    {
        Write("INFO", message, null);
    }

    public void Warn(string message)
    {
        Write("WARN", message, null);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    private static void Write(string level, string message, Exception? exception)
    {
        var line = $"[{level}] {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        System.Diagnostics.Debug.WriteLine(line);
    }
}
