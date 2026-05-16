using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.Storage.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioStorage(this IServiceCollection services)
    {
        services.AddSingleton<IAppDatabase, SqliteAppDatabase>();
        services.AddSingleton<ISettingsRepository, SettingsRepository>();
        services.AddSingleton<IWorkspaceRootsRepository, WorkspaceRootsRepository>();
        services.AddSingleton<IRecentFilesRepository, RecentFilesRepository>();
        return services;
    }
}
