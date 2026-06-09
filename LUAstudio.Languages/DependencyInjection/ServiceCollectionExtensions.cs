using LUAstudio.Languages.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.Languages.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioLanguages(this IServiceCollection services)
    {
        services.AddSingleton<ILuaParser, LuaParserService>();
        return services;
    }
}
