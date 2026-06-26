using LUAstudio.Abstractions;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Languages.Syntax.Nodes;

namespace LUAstudio.IntelliSense.Diagnostics.Analyzers;

public sealed class GlobalEnvironmentAnalyzer : IDiagnosticAnalyzer
{
    private static readonly HashSet<string> StandardLuaBuiltins = new(StringComparer.Ordinal)
    {
        "print", "pairs", "ipairs", "next", "type", "tostring", "tonumber", "pcall", "xpcall",
        "error", "assert", "select", "unpack", "table", "string", "math", "coroutine",
        "require", "setfenv", "getfenv", "setmetatable", "getmetatable", "rawget", "rawset",
        "load", "loadfile", "dofile", "collectgarbage", "_G", "_VERSION"
    };

    private static readonly HashSet<string> RobloxBuiltins = new(StandardLuaBuiltins, StringComparer.Ordinal)
    {
        "game", "workspace", "script", "plugin", "shared", "tick", "wait", "spawn", "delay",
        "warn", "typeof", "task", "Instance", "Vector3", "CFrame", "Color3", "UDim2",
        "Enum", "Ray", "Region3", "Random", "debug", "settings", "elapsedTime", "time"
    };

    public int Order => 15;

    public void Analyze(DiagnosticAnalysisContext context, ICollection<SemanticDiagnostic> diagnostics)
    {
        foreach (var node in context.ParseResult.Tree.Root.DescendantsAndSelf())
        {
            if (node is not IdentifierNameSyntax id || !IsGlobalReference(id))
            {
                continue;
            }

            var name = id.Name.Text;
            if (IsKnownGlobal(name, context))
            {
                continue;
            }

            diagnostics.Add(new SemanticDiagnostic(
                "LUA2001",
                $"Undefined global '{name}'.",
                id.Span,
                SemanticDiagnosticSeverity.Warning,
                context.EnvironmentProfile switch
                {
                    LuaEnvironmentProfile.RobloxLua => $"Declare 'local {name}' or verify Roblox API spelling.",
                    LuaEnvironmentProfile.StandardLua => $"Declare 'local {name}' or add to environment allowlist.",
                    _ => $"Declare 'local {name}' or define in custom environment profile."
                }));
        }

        foreach (var global in context.Binding.AssignedGlobals)
        {
            if (IsKnownGlobal(global, context))
            {
                continue;
            }

            diagnostics.Add(new SemanticDiagnostic(
                "LUA2301",
                $"Global pollution: assigning to undeclared global '{global}'.",
                context.ParseResult.Snapshot.Content?.IndexOf(global, StringComparison.Ordinal) is int idx && idx >= 0
                    ? new LUAstudio.Languages.Text.TextSpan(idx, global.Length)
                    : default,
                SemanticDiagnosticSeverity.Info,
                $"Use 'local {global}' instead of polluting the global environment."));
        }
    }

    private static bool IsGlobalReference(IdentifierNameSyntax id) =>
        id.Parent switch
        {
            LocalStatementSyntax local when local.Name == id.Name => false,
            FunctionDeclarationSyntax fn when fn.Name == id.Name => false,
            ParameterSyntax param when param.Name == id.Name => false,
            TypeAnnotationSyntax => false,
            AssignmentStatementSyntax assign when assign.Target == id => false,
            _ => true
        };

    private static bool IsKnownGlobal(string name, DiagnosticAnalysisContext context)
    {
        if (context.EnvironmentProfile switch
            {
                LuaEnvironmentProfile.StandardLua => StandardLuaBuiltins.Contains(name),
                LuaEnvironmentProfile.RobloxLua => RobloxBuiltins.Contains(name) ||
                    context.Roblox.GlobalTypeAliases.ContainsKey(name) ||
                    context.Roblox.TryGetGlobal(name, out _),
                LuaEnvironmentProfile.Custom => StandardLuaBuiltins.Contains(name) ||
                    context.Roblox.TryGetGlobal(name, out _),
                _ => false
            })
        {
            return true;
        }

        return context.Binding.RootScope.TryResolveLocal(name, out _);
    }
}
