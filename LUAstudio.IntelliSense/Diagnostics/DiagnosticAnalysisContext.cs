using LUAstudio.Abstractions;
using LUAstudio.Core;
using LUAstudio.IntelliSense.Roblox;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.IntelliSense.Symbols;
using LUAstudio.IntelliSense.Workspace;
using LUAstudio.Languages.Parsing;

namespace LUAstudio.IntelliSense.Diagnostics;

public sealed class DiagnosticAnalysisContext
{
    public DiagnosticAnalysisContext(
        ParseResult parseResult,
        SemanticBindingResult binding,
        IRobloxApiDatabase roblox,
        ISymbolIndex symbolIndex,
        IModuleResolver moduleResolver,
        RequireGraphService requireGraph)
    {
        ParseResult = parseResult;
        Binding = binding;
        Roblox = roblox;
        SymbolIndex = symbolIndex;
        ModuleResolver = moduleResolver;
        RequireGraph = requireGraph;
        EnvironmentProfile = LuaEnvironmentProfiles.FromStorageValue(
            Engine.Globals.Get<string>(SettingKeys.DiagnosticsEnvironmentProfile)?.Value);
        StrictMode = Engine.Globals.Get<bool>(SettingKeys.DiagnosticsStrictMode)?.Value ?? false;
        Enabled = Engine.Globals.Get<bool>(SettingKeys.DiagnosticsEnabled)?.Value ?? true;
    }

    public ParseResult ParseResult { get; }

    public SemanticBindingResult Binding { get; }

    public IRobloxApiDatabase Roblox { get; }

    public ISymbolIndex SymbolIndex { get; }

    public IModuleResolver ModuleResolver { get; }

    public RequireGraphService RequireGraph { get; }

    public LuaEnvironmentProfile EnvironmentProfile { get; }

    public bool StrictMode { get; }

    public bool Enabled { get; }

    public string? FilePath => ParseResult.Snapshot.FilePath;
}

public interface IDiagnosticAnalyzer
{
    int Order { get; }

    void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics);
}
