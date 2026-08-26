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
        services.AddSingleton<IBreakpointService, BreakpointService>();
        // These types hold editor-specific event handlers and renderer state.  They
        // must never be shared: attaching a singleton to a second tab detaches the
        // first tab and removes its highlighting/completion handlers.
        services.AddTransient<InlineCompletionService>();
        services.AddTransient<SmartEnterHandler>();
        services.AddTransient<AutoPairInsertService>();
        services.AddTransient<EditorIntelliSenseController>();
        return services;
    }
}
