using System.Windows;
using LUAstudio.Core.DependencyInjection;
using LUAstudio.Core.Threading;
using LUAstudio.Editor.DependencyInjection;
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
    private bool _isShowingErrorDialog;

    public static IServiceProvider Services =>
        ((App)Current)._services ?? throw new InvalidOperationException("Application services are not initialized.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            if (_isShowingErrorDialog)
            {
                args.Handled = true;
                return;
            }

            _isShowingErrorDialog = true;
            try
            {
                MessageBox.Show(
                    $"An unexpected error occurred:{Environment.NewLine}{Environment.NewLine}{args.Exception.Message}",
                    "LuaStudio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isShowingErrorDialog = false;
            }

            args.Handled = true;
        };

        var services = new ServiceCollection();
        services.AddLuaStudioCore();
        services.AddLuaStudioWorkspace();
        services.AddLuaStudioEditor();
        services.AddLuaStudioIde();
        services.AddSingleton<IMainThread, WpfMainThread>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IUserPromptService, WpfUserPromptService>();
        services.AddSingleton<IExplorerShellService, WpfExplorerShellService>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<WpfDocumentEditorHost>();

        _services = services.BuildServiceProvider();

        _ = _services.GetRequiredService<DocumentSyncHandler>();
        _ = _services.GetRequiredService<RecentFilesRecordingHandler>();
        _ = _services.GetRequiredService<DocumentAnalysisHandler>();

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
