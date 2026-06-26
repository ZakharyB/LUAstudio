using LUAstudio.Execution.Abstractions;

namespace LUAstudio.ExecutionHost.Runtime;

public sealed class SandboxRuntimeException : Exception
{
    public SandboxRuntimeException(string message, string? sourcePath, int line, int column)
        : base(message)
    {
        SourcePath = sourcePath;
        Line = line;
        Column = column;
    }

    public string? SourcePath { get; }

    public int Line { get; }

    public int Column { get; }

    public ExecutionErrorInfo ToErrorInfo() =>
        new(Message, SourcePath, Line, Column, Array.Empty<string>());
}

public sealed class SandboxEnvironment
{
    private readonly Dictionary<string, object?> _globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, object?>> _modules = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _robloxMocks;

    public SandboxEnvironment(bool robloxMocks)
    {
        _robloxMocks = robloxMocks;
        SeedSafeGlobals();
        if (_robloxMocks)
        {
            SeedRobloxMocks();
        }
    }

    public IDictionary<string, object?> Globals => _globals;

    public object? Require(string modulePath, string? sourcePath)
    {
        if (_modules.TryGetValue(modulePath, out var cached))
        {
            return cached;
        }

        var exports = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["default"] = modulePath
        };
        _modules[modulePath] = exports;
        return exports;
    }

    private void SeedSafeGlobals()
    {
        _globals["print"] = new Action<object?>(value =>
            Output?.Invoke("stdout", SandboxPrint(value)));

        _globals["require"] = new Func<string, object?>(path => Require(path, null));
        _globals["type"] = new Func<object?, string>(value => value switch
        {
            null => "nil",
            string => "string",
            bool => "boolean",
            double => "number",
            int => "number",
            IDictionary<string, object?> => "table",
            _ => "userdata"
        });
        _globals["tostring"] = new Func<object?, string>(SandboxPrint);
        _globals["tonumber"] = new Func<object?, double?>(value =>
            value switch
            {
                double d => d,
                int i => i,
                string s when double.TryParse(s, out var n) => n,
                _ => null
            });
    }

    private void SeedRobloxMocks()
    {
        var game = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Name"] = "Game"
        };
        _globals["game"] = game;
        _globals["workspace"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["Name"] = "Workspace" };
        _globals["script"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["Name"] = "Script" };
    }

    private static string SandboxPrint(object? value) => value switch
    {
        null => "nil",
        string s => s,
        bool b => b ? "true" : "false",
        _ => value.ToString() ?? "nil"
    };

    public Action<string, string>? Output { get; set; }
}
