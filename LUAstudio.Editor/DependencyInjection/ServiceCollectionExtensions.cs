using LUAstudio.Editor.Diagnostics;
using LUAstudio.Editor.IntelliSense;
using LUAstudio.IntelliSense.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.Editor.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioEditor(this IServiceCollection services)
    {
        services.AddLuaStudioIntelliSense();
        services.AddSingleton<EditorDiagnosticService>();
        services.AddSingleton<EditorIntelliSenseController>();
        return services;
    }
}
