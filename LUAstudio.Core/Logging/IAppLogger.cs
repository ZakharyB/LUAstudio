namespace LUAstudio.Core.Logging;

public interface IAppLogger
{
    void Trace(string message);

    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
