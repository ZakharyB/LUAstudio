namespace LUAstudio.IDE.Threading;

public interface IMainThread
{
    void Send(Action action);
}
