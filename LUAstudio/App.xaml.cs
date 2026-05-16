using System.Windows;
using LUAstudio.Core.DependencyInjection;
using LUAstudio.Core.Threading;
using LUAstudio.IDE.DependencyInjection;
using LUAstudio.IDE.Handlers;
using LUAstudio.IDE.Services;
using LUAstudio.IDE.ViewModels;
using LUAstudio.Workspace.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"An unexpected error occurred:{Environment.NewLine}{Environment.NewLine}{args.Exception.Message}",
                "LuaStudio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var services = new ServiceCollection();
        services.AddLuaStudioCore();
        services.AddLuaStudioWorkspace();
        services.AddLuaStudioIde();
        services.AddSingleton<IMainThread, WpfMainThread>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IUserPromptService, WpfUserPromptService>();
        services.AddSingleton<IExplorerShellService, WpfExplorerShellService>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        _ = _services.GetRequiredService<DocumentSyncHandler>();
        _ = _services.GetRequiredService<RecentFilesRecordingHandler>();

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
