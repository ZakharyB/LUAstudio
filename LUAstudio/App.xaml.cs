using System.Windows;
using LUAstudio.Core.DependencyInjection;
using LUAstudio.IDE.DependencyInjection;
using LUAstudio.IDE.Services;
using LUAstudio.IDE.Threading;
using LUAstudio.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddLuaStudioCore();
        services.AddLuaStudioIde();
        services.AddSingleton<IMainThread, WpfMainThread>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IUserPromptService, WpfUserPromptService>();
        services.AddSingleton<DockLayoutStore>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
