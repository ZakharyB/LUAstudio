using System.Windows;
using LUAstudio.Core;
using LUAstudio.Core.DependencyInjection;
using LUAstudio.Storage;
using LUAstudio.Core.Threading;
using LUAstudio.Editor.DependencyInjection;
using LUAstudio.IDE.DependencyInjection;
using LUAstudio.IDE.Handlers;
using LUAstudio.IDE.Services;
using LUAstudio.IDE.ViewModels;
using LUAstudio.Workspace.DependencyInjection;
using LUAstudio.Execution.DependencyInjection;
using LUAstudio.IntelliSense.Workspace;
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

        // that registers the system that will later
        // allow to set any value to be changeable on the fly
        // for plugins or setting implementations
        Engine.Initialize();
        RuntimeGlobals.RegisterDefaults();
        
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
        services.AddLuaStudioExecution();
        services.AddSingleton<IMainThread, WpfMainThread>();
        services.AddSingleton<IFileDialogService, WpfFileDialogService>();
        services.AddSingleton<IUserPromptService, WpfUserPromptService>();
        services.AddSingleton<IExplorerShellService, WpfExplorerShellService>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<WpfDocumentEditorHost>();
        services.AddSingleton<DiagnosticsPanelViewModel>();
        services.AddSingleton<SettingsBootstrap>();
        services.AddSingleton<EditorSettingsCoordinator>();

        _services = services.BuildServiceProvider();

        var bootstrap = _services.GetRequiredService<SettingsBootstrap>();
        bootstrap.LoadAsync().GetAwaiter().GetResult();
        bootstrap.AttachPersistence();
        _services.GetRequiredService<EditorSettingsCoordinator>().Start();

        _ = _services.GetRequiredService<DocumentSyncHandler>();
        _ = _services.GetRequiredService<RecentFilesRecordingHandler>();
        _ = _services.GetRequiredService<DocumentAnalysisHandler>();
        _ = _services.GetRequiredService<DiagnosticsPanelViewModel>();
        _ = _services.GetRequiredService<RequireGraphCoordinator>();

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
