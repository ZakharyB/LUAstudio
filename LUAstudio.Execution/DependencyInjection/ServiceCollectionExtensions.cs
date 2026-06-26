using LUAstudio.Execution.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.Execution.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioExecution(this IServiceCollection services)
    {
        services.AddSingleton<IExecutionHostProcessManager, Transport.ExecutionHostProcessManager>();
        return services;
    }
}
