using LUAstudio.Editor.Completion;
using LUAstudio.Editor.Debugging;
using LUAstudio.Editor.Diagnostics;
using LUAstudio.Editor.Editing;
using LUAstudio.Editor.Highlighting;
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
        services.AddSingleton<EditorDiagnosticHoverController>();
        services.AddSingleton<IBreakpointService, BreakpointService>();
        services.AddSingleton<InlineCompletionService>();
        services.AddSingleton<SmartEnterHandler>();
        services.AddSingleton<AutoPairInsertService>();
        services.AddSingleton<EditorIntelliSenseController>();
        return services;
    }
}
