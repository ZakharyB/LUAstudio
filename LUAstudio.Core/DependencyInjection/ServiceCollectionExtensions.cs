using LUAstudio.Core.Events;
using LUAstudio.Core.Logging;
using LUAstudio.Core.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioCore(this IServiceCollection services)
    {
        services.AddSingleton<IEventBus, EventBus>();
        services.AddSingleton<IAppLogger, DebugAppLogger>();
        services.AddSingleton<IBackgroundWorkScheduler, BackgroundWorkScheduler>();
        services.AddSingleton<IAnalysisWorkQueue, AnalysisWorkQueue>();
        return services;
    }
}
