using LUAstudio.Storage.DependencyInjection;
using LUAstudio.Workspace.FileWatching;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.Workspace.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioWorkspace(this IServiceCollection services)
    {
        services.AddLuaStudioStorage();
        services.AddSingleton<FileSystemWatchCoordinator>();
        services.AddSingleton<IWorkspaceService, WorkspaceService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        return services;
    }
}
