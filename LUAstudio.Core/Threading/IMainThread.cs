namespace LUAstudio.Core.Threading;

public interface IMainThread
{
    void Send(Action action);

    T Invoke<T>(Func<T> func);
}
