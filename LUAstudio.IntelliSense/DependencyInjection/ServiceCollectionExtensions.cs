using LUAstudio.IntelliSense.Diagnostics;
using LUAstudio.IntelliSense.Diagnostics.Analyzers;
using LUAstudio.IntelliSense.Analysis;
using LUAstudio.IntelliSense.Completion;
using LUAstudio.IntelliSense.Completion.Providers;
using LUAstudio.IntelliSense.Documents;
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.IntelliSense.Workspace;
using LUAstudio.Languages.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace LUAstudio.IntelliSense.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLuaStudioIntelliSense(this IServiceCollection services)
    {
        services.AddLuaStudioLanguages();

        services.AddSingleton<IDocumentSnapshotStore, DocumentSnapshotStore>();
        services.AddSingleton<ISymbolIndex, SymbolIndex>();
        services.AddSingleton<IModuleResolver, ModuleResolver>();
        services.AddSingleton<RequireGraphService>();
        services.AddSingleton<RequireGraphWorkspaceScanner>();
        services.AddSingleton<RequireGraphCoordinator>();
        services.AddSingleton<IRobloxApiDatabase, RobloxApiDatabase>();
        services.AddSingleton<ExpressionTypeResolver>();
        services.AddSingleton<SemanticBinder>();

        services.AddSingleton<IDiagnosticAnalyzer, ScopeSymbolAnalyzer>();
        services.AddSingleton<IDiagnosticAnalyzer, GlobalEnvironmentAnalyzer>();
        services.AddSingleton<IDiagnosticAnalyzer, ControlFlowAnalyzer>();
        services.AddSingleton<IDiagnosticAnalyzer, DataFlowAnalyzer>();
        services.AddSingleton<IDiagnosticAnalyzer, TypeCheckAnalyzer>();
        services.AddSingleton<IDiagnosticAnalyzer, ModuleDependencyAnalyzer>();
        services.AddSingleton<IDiagnosticAnalyzer, LintRulesAnalyzer>();
        services.AddSingleton<DiagnosticEngine>();
        services.AddSingleton<SemanticAnalyzer>();
        services.AddSingleton<IAnalysisOrchestrator, AnalysisOrchestrator>();

        services.AddSingleton<ICompletionProvider, ScopeCompletionProvider>();
        services.AddSingleton<ICompletionProvider, RobloxCompletionProvider>();
        services.AddSingleton<ICompletionProvider, GetServiceCompletionProvider>();
        services.AddSingleton<ICompletionProvider, KeywordSnippetCompletionProvider>();
        services.AddSingleton<ICompletionService, CompletionService>();

        services.AddSingleton<SignatureHelpService>();
        services.AddSingleton<HoverInfoService>();

        return services;
    }
}
