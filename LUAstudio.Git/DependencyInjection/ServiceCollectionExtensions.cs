using LUAstudio.IDE.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.Git.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioGit(this IServiceCollection services)
    {
        services.AddSingleton<IGitStatusService, GitStatusService>();
        services.AddSingleton<IGitDecorationProvider, GitDecorationProvider>();
        services.AddSingleton<SourceControlViewModel>();
        return services;
    }
}
