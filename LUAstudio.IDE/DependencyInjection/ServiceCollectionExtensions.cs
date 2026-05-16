using LUAstudio.IDE.Documents;
using LUAstudio.IDE.Handlers;
using LUAstudio.IDE.Services;
using LUAstudio.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.IDE.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioIde(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IExplorerNodeDecorationProvider, ExplorerNodeDecorationProvider>();
        services.AddSingleton<WorkspaceExplorerViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<IFileSystemActivitySink>(sp => sp.GetRequiredService<MainViewModel>());
        services.AddSingleton<DocumentSyncHandler>();
        services.AddSingleton<RecentFilesRecordingHandler>();
        services.AddSingleton<DocumentAnalysisHandler>();
        return services;
    }
}
