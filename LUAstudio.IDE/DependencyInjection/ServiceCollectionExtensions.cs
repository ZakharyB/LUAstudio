using LUAstudio.IDE.Documents;
using LUAstudio.IDE.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.IDE.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioIde(this IServiceCollection services)
    {
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<MainViewModel>();
        return services;
    }
}
