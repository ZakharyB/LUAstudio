using System.Windows;
using LUAstudio.IDE.Threading;

namespace LUAstudio;

public sealed class WpfMainThread : IMainThread
{
    public void Send(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
