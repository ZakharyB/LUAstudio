using LUAstudio.ExecutionHost.Debugging;
using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;

namespace LUAstudio.ExecutionHost.Runtime;

public sealed class InstrumentedAstInterpreter
{
    private readonly SandboxEnvironment _environment;
    private readonly DebugController _debug;
    private string _source = string.Empty;

    public InstrumentedAstInterpreter(SandboxEnvironment environment, DebugController debug)
    {
        _environment = environment;
        _debug = debug;
        _environment.Output = (channel, text) => Output?.Invoke(channel, text);
    }

    public event Action<string, string>? Output;

    public async Task ExecuteAsync(
        CompilationUnitSyntax unit,
        string source,
        string? sourcePath,
        CancellationToken cancellationToken)
    {
        _source = source;

        var frame = new RuntimeFrame("<main>", new Dictionary<string, object?>(StringComparer.Ordinal));
        await ExecuteStatementsAsync(unit.Statements, frame, sourcePath, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteStatementsAsync(
        IReadOnlyList<SyntaxNode> statements,
        RuntimeFrame frame,
        string? sourcePath,
        CancellationToken cancellationToken)
    {
        foreach (var stmt in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = GetLineFromSource(stmt.Span);
            await _debug.OnLineAsync(line, sourcePath, frame, cancellationToken).ConfigureAwait(false);
            ExecuteStatement(stmt, frame, sourcePath);
        }
    }

    private void ExecuteStatement(SyntaxNode stmt, RuntimeFrame frame, string? sourcePath)
    {
        switch (stmt)
        {
            case LocalStatementSyntax local:
                frame.Locals[local.Name.Text] = Evaluate(local.Initializer, frame, sourcePath);
                break;

            case AssignmentStatementSyntax assign when assign.Target is IdentifierNameSyntax id:
                var value = Evaluate(assign.Value, frame, sourcePath);
                if (!frame.Locals.TryGetValue(id.Name.Text, out _))
                {
                    _environment.Globals[id.Name.Text] = value;
                }
                else
                {
                    frame.Locals[id.Name.Text] = value;
                }

                break;

            case CallExpressionSyntax call when call.Kind == SyntaxKind.CallStatement:
                _ = Evaluate(call, frame, sourcePath);
                break;

            case RequireCallSyntax req:
                _ = Evaluate(req, frame, sourcePath);
                break;

            case FunctionDeclarationSyntax fn:
                _environment.Globals[fn.Name.Text] = CreateFunction(fn, frame);
                break;

            case BlockSyntax block:
                ExecuteStatement(block, frame, sourcePath);
                break;

            default:
                if (stmt is CallExpressionSyntax or RequireCallSyntax or IdentifierNameSyntax)
                {
                    _ = Evaluate(stmt, frame, sourcePath);
                }

                break;
        }
    }

    private object? Evaluate(SyntaxNode? node, RuntimeFrame frame, string? sourcePath)
    {
        if (node is null)
        {
            return null;
        }

        switch (node)
        {
            case LiteralExpressionSyntax lit:
                return ParseLiteral(lit.Token.Text);

            case IdentifierNameSyntax id:
                if (frame.Locals.TryGetValue(id.Name.Text, out var local))
                {
                    return local;
                }

                if (_environment.Globals.TryGetValue(id.Name.Text, out var global))
                {
                    return global;
                }

                return null;

            case RequireCallSyntax req:
                return _environment.Require(TrimQuotes(req.ModulePath.Text), sourcePath);

            case CallExpressionSyntax call:
                return InvokeCall(call, frame, sourcePath);

            case TableExpressionSyntax table:
                return BuildTable(table, frame, sourcePath);

            default:
                return null;
        }
    }

    private object? InvokeCall(CallExpressionSyntax call, RuntimeFrame frame, string? sourcePath)
    {
        if (call.Target is IdentifierNameSyntax { Name.Text: "require" } &&
            call.Arguments.Count > 0 &&
            call.Arguments[0] is LiteralExpressionSyntax lit)
        {
            return _environment.Require(TrimQuotes(lit.Token.Text), sourcePath);
        }

        var target = Evaluate(call.Target, frame, sourcePath);
        var args = call.Arguments.Select(arg => Evaluate(arg, frame, sourcePath)).ToArray();

        if (target is Func<object?[], object?> fn)
        {
            return fn(args);
        }

        if (target is Action<object?> action1 && args.Length == 1)
        {
            action1(args[0]);
            return null;
        }

        if (target is Action action0 && args.Length == 0)
        {
            action0();
            return null;
        }

        return null;
    }

    private object CreateFunction(FunctionDeclarationSyntax fn, RuntimeFrame parent)
    {
        return new Func<object?[], object?>(args =>
        {
            var locals = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < fn.Parameters.Parameters.Count; i++)
            {
                locals[fn.Parameters.Parameters[i].Name.Text] = i < args.Length ? args[i] : null;
            }

            var frame = new RuntimeFrame(fn.Name.Text, locals, parent.Locals);
            ExecuteStatement(fn.Body.Block, frame, null);
            return null;
        });
    }

    private Dictionary<string, object?> BuildTable(TableExpressionSyntax table, RuntimeFrame frame, string? sourcePath)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var field in table.Fields)
        {
            var key = field.Key switch
            {
                IdentifierNameSyntax id => id.Name.Text,
                LiteralExpressionSyntax lit => TrimQuotes(lit.Token.Text),
                _ => field.Value.Span.Start.ToString()
            };

            result[key] = Evaluate(field.Value, frame, sourcePath);
        }

        return result;
    }

    private static object? ParseLiteral(string text) => text switch
    {
        "nil" => null,
        "true" => true,
        "false" => false,
        _ when text.StartsWith('"') || text.StartsWith('\'') => TrimQuotes(text),
        _ when double.TryParse(text, out var number) => number,
        _ => text
    };

    private static int GetLine(TextSpan span, string? sourcePath) =>
        Math.Max(1, span.Start / 40 + 1);

    private int GetLineFromSource(TextSpan span)
    {
        if (string.IsNullOrEmpty(_source) || span.Start < 0 || span.Start > _source.Length)
        {
            return GetLine(span, null);
        }

        var line = 1;
        for (var i = 0; i < span.Start && i < _source.Length; i++)
        {
            if (_source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string TrimQuotes(string text) => text.Trim().Trim('"', '\'');
}
