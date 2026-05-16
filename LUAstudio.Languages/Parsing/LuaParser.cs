using LUAstudio.Languages.Syntax;
using LUAstudio.Languages.Syntax.Nodes;
using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Parsing;

internal sealed class LuaParser
{
    private readonly List<LuaToken> _tokens;
    private readonly List<ParseDiagnostic> _diagnostics = [];
    private int _index;

    private LuaParser(List<LuaToken> tokens) => _tokens = tokens;

    public static (SyntaxNode Root, IReadOnlyList<ParseDiagnostic> Diagnostics) Parse(string source)
    {
        var meaningful = new LuaLexer(source).Tokenize()
            .Where(t => t.Kind is not (LuaTokenKind.Whitespace or LuaTokenKind.Comment))
            .ToList();

        var parser = new LuaParser(meaningful);
        var start = 0;
        var end = source.Length;
        var statements = parser.ParseBlockStatements();
        var root = new CompilationUnitSyntax(TextSpan.FromBounds(start, end), statements);
        return (root, parser._diagnostics);
    }

    private IReadOnlyList<SyntaxNode> ParseBlockStatements()
    {
        var statements = new List<SyntaxNode>();
        while (!IsAtEnd() && !IsBlockTerminator())
        {
            var indexBefore = _index;
            var stmt = ParseStatement();
            if (stmt is not null)
            {
                statements.Add(stmt);
            }
            else if (_index == indexBefore)
            {
                // Error recovery: never spin on the same token.
                Advance();
            }
        }

        return statements;
    }

    private SyntaxNode? ParseStatement()
    {
        if (MatchKeyword("local"))
        {
            return ParseLocal();
        }

        if (MatchKeyword("function"))
        {
            return ParseFunctionDeclaration(isLocal: false);
        }

        if (MatchKeyword("return"))
        {
            var start = Previous().Span.Start;
            _ = ParseExpression();
            var end = Previous().Span.End;
            return new LiteralExpressionSyntax(TextSpan.FromBounds(start, end), null,
                new SyntaxToken("return", TextSpan.FromBounds(start, start), null));
        }

        if (MatchKeyword("if"))
        {
            return ParseIfStatement();
        }

        if (MatchKeyword("while"))
        {
            return ParseWhileStatement();
        }

        if (MatchKeyword("for"))
        {
            return ParseForStatement();
        }

        if (MatchKeyword("do"))
        {
            return ParseDoStatement();
        }

        var exprStart = Current().Span.Start;
        var expr = ParsePrefixExpression();
        if (expr is null)
        {
            SkipToNextStatement();
            return null;
        }

        if (MatchText("="))
        {
            var value = ParseExpression();
            var end = Previous().Span.End;
            return new AssignmentStatementSyntax(TextSpan.FromBounds(exprStart, end), null, expr, value!);
        }

        if (expr is CallExpressionSyntax or MemberAccessExpressionSyntax or IdentifierNameSyntax)
        {
            var args = MatchText("(") ? ParseArgumentList() : [];
            var end = Previous().Span.End;
            return new CallExpressionSyntax(TextSpan.FromBounds(exprStart, end), null, expr, args, isStatement: true);
        }

        return expr;
    }

    private SyntaxNode ParseLocal()
    {
        var start = Previous().Span.Start;
        var name = ExpectIdentifier();
        TypeAnnotationSyntax? typeAnn = null;
        if (MatchText(":"))
        {
            typeAnn = ParseTypeAnnotation();
        }

        SyntaxNode? init = null;
        if (MatchText("="))
        {
            init = ParseExpression();
        }

        if (MatchKeyword("function"))
        {
            return ParseFunctionDeclaration(isLocal: true, start, name);
        }

        var end = Previous().Span.End;
        return new LocalStatementSyntax(TextSpan.FromBounds(start, end), null, name, init, typeAnn);
    }

    private SyntaxNode ParseFunctionDeclaration(bool isLocal, int? startOverride = null, SyntaxToken? nameOverride = null)
    {
        var start = startOverride ?? Previous().Span.Start;
        var name = nameOverride ?? ExpectIdentifier();
        TypeAnnotationSyntax? returnType = null;
        if (MatchText(":"))
        {
            returnType = ParseTypeAnnotation();
        }

        ExpectText("(");
        var parameters = ParseParameterList();
        ExpectText(")");
        var body = ParseFunctionBody();
        var end = Previous().Span.End;
        return new FunctionDeclarationSyntax(TextSpan.FromBounds(start, end), null, isLocal, name, parameters, body, returnType);
    }

    private ParameterListSyntax ParseParameterList()
    {
        var start = Current().Span.Start;
        var parameters = new List<ParameterSyntax>();
        if (!MatchText(")"))
        {
            do
            {
                var name = ExpectIdentifier();
                TypeAnnotationSyntax? typeAnn = null;
                if (MatchText(":"))
                {
                    typeAnn = ParseTypeAnnotation();
                }

                parameters.Add(new ParameterSyntax(name.Span, null, name, typeAnn));
            } while (MatchText(","));

            ExpectText(")");
        }

        var end = Previous().Span.End;
        return new ParameterListSyntax(TextSpan.FromBounds(start, end), null, parameters);
    }

    private SyntaxNode ParseIfStatement()
    {
        var start = Previous().Span.Start;
        var cond = ParseExpression()!;
        ExpectKeyword("then");
        var thenBlock = ParseBlockNode();
        BlockSyntax? elseBlock = null;
        if (MatchKeyword("elseif"))
        {
            // Simplified: treat as else for structure
        }
        else if (MatchKeyword("else"))
        {
            elseBlock = ParseBlockNode();
        }

        ExpectKeyword("end");
        var end = Previous().Span.End;
        return new IfStatementSyntax(TextSpan.FromBounds(start, end), null, cond, thenBlock, elseBlock);
    }

    private SyntaxNode ParseWhileStatement()
    {
        var start = Previous().Span.Start;
        var cond = ParseExpression()!;
        ExpectKeyword("do");
        var body = ParseBlockNode();
        ExpectKeyword("end");
        var end = Previous().Span.End;
        return new WhileStatementSyntax(TextSpan.FromBounds(start, end), null, cond, body);
    }

    private SyntaxNode ParseForStatement()
    {
        var start = Previous().Span.Start;
        while (!IsAtEnd() && Current().Keyword != "do")
        {
            Advance();
        }

        ExpectKeyword("do");
        var body = ParseBlockNode();
        ExpectKeyword("end");
        var end = Previous().Span.End;
        return new ForStatementSyntax(TextSpan.FromBounds(start, end), null, body);
    }

    private SyntaxNode ParseDoStatement()
    {
        var start = Previous().Span.Start;
        var body = ParseBlockNode();
        ExpectKeyword("end");
        var end = Previous().Span.End;
        return new ForStatementSyntax(TextSpan.FromBounds(start, end), null, body);
    }

    private BlockSyntax ParseBlockNode()
    {
        var start = Current().Span.Start;
        var statements = ParseBlockStatements();
        var end = Previous().Span.End;
        return new BlockSyntax(TextSpan.FromBounds(start, end), null, statements);
    }

    private FunctionBodySyntax ParseFunctionBody()
    {
        var start = Current().Span.Start;
        var blockStart = start;
        var statements = ParseBlockStatements();
        ExpectKeyword("end");
        var end = Previous().Span.End;
        var block = new BlockSyntax(TextSpan.FromBounds(blockStart, end), null, statements);
        return new FunctionBodySyntax(TextSpan.FromBounds(start, end), null, block);
    }

    private TypeAnnotationSyntax ParseTypeAnnotation()
    {
        var start = Previous().Span.Start;
        var typeName = ExpectIdentifier();
        return new TypeAnnotationSyntax(TextSpan.FromBounds(start, typeName.Span.End), null, typeName);
    }

    private SyntaxNode? ParseExpression()
    {
        return ParsePrefixExpression();
    }

    private SyntaxNode? ParsePrefixExpression()
    {
        SyntaxNode? expr;
        if (TryMatchLiteral(out var literalToken))
        {
            expr = new LiteralExpressionSyntax(literalToken.Span, null, literalToken);
        }
        else if (MatchText("{"))
        {
            expr = ParseTable();
        }
        else if (Current().Kind == LuaTokenKind.Identifier)
        {
            var name = AdvanceSyntaxToken();
            expr = new IdentifierNameSyntax(name.Span, null, name);
        }
        else
        {
            return null;
        }

        while (true)
        {
            if (MatchText(".") || MatchText(":"))
            {
                var member = ExpectIdentifier();
                expr = new MemberAccessExpressionSyntax(
                    TextSpan.FromBounds(expr.Span.Start, member.Span.End), null, expr, member);
            }
            else if (MatchText("("))
            {
                var args = ParseArgumentList();
                var end = Previous().Span.End;
                if (expr is IdentifierNameSyntax { Name.Text: "require" } && args.Count == 1 &&
                    args[0] is LiteralExpressionSyntax lit)
                {
                    expr = new RequireCallSyntax(TextSpan.FromBounds(expr.Span.Start, end), null, lit.Token);
                }
                else
                {
                    expr = new CallExpressionSyntax(TextSpan.FromBounds(expr.Span.Start, end), null, expr, args, false);
                }
            }
            else if (MatchText("["))
            {
                var index = ParseExpression();
                ExpectText("]");
                var end = Previous().Span.End;
                expr = new MemberAccessExpressionSyntax(TextSpan.FromBounds(expr.Span.Start, end), null, expr,
                    new SyntaxToken("[]", new TextSpan(expr.Span.End, 0), null));
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private TableExpressionSyntax ParseTable()
    {
        var start = Previous().Span.Start;
        var fields = new List<TableFieldSyntax>();
        while (!MatchText("}") && !IsAtEnd())
        {
            SyntaxNode? key = null;
            if (Current().Kind == LuaTokenKind.Identifier && PeekToken(1).Text is "[" or "=")
            {
                key = new IdentifierNameSyntax(Current().Span, null, AdvanceSyntaxToken());
                ExpectText("=");
            }
            else if (MatchText("["))
            {
                key = ParseExpression();
                ExpectText("]");
                ExpectText("=");
            }

            var value = ParseExpression()!;
            fields.Add(new TableFieldSyntax(TextSpan.FromBounds(key?.Span.Start ?? value.Span.Start, value.Span.End), null, key, value));
            MatchText(",");
        }

        var end = Previous().Span.End;
        return new TableExpressionSyntax(TextSpan.FromBounds(start, end), null, fields);
    }

    private IReadOnlyList<SyntaxNode> ParseArgumentList()
    {
        var args = new List<SyntaxNode>();
        if (!MatchText(")"))
        {
            do
            {
                var arg = ParseExpression();
                if (arg is not null)
                {
                    args.Add(arg);
                }
            } while (MatchText(","));

            ExpectText(")");
        }

        return args;
    }

    private SyntaxToken AdvanceSyntaxToken()
    {
        var t = Advance();
        return new SyntaxToken(t.Text, t.Span, null);
    }

    private SyntaxToken ExpectIdentifier()
    {
        if (Current().Kind != LuaTokenKind.Identifier)
        {
            ReportError("Expected identifier.");
            return new SyntaxToken("<missing>", Current().Span, null);
        }

        return AdvanceSyntaxToken();
    }

    private void ExpectText(string text)
    {
        if (!MatchText(text))
        {
            ReportError($"Expected '{text}'.");
        }
    }

    private void ExpectKeyword(string keyword)
    {
        if (!MatchKeyword(keyword))
        {
            ReportError($"Expected '{keyword}'.");
        }
    }

    private bool MatchKeyword(string keyword)
    {
        if (Current().Keyword == keyword)
        {
            Advance();
            return true;
        }

        return false;
    }

    private bool MatchText(string text)
    {
        if (Current().Text == text)
        {
            Advance();
            return true;
        }

        return false;
    }

    private LuaToken Advance()
    {
        if (_index >= _tokens.Count)
        {
            return _tokens[^1];
        }

        return _tokens[_index++];
    }

    private LuaToken Previous() => _tokens[Math.Max(0, _index - 1)];

    private LuaToken Current() =>
        _index >= _tokens.Count ? _tokens[^1] : _tokens[_index];

    private LuaToken PeekToken(int offset)
    {
        var idx = _index + offset;
        return idx < _tokens.Count ? _tokens[idx] : _tokens[^1];
    }

    private bool IsAtEnd() =>
        _index >= _tokens.Count || _tokens[_index].Kind == LuaTokenKind.EndOfFile;

    private bool IsBlockTerminator() =>
        Current().Keyword is "end" or "else" or "elseif" or "until";

    private void SkipToNextStatement()
    {
        while (!IsAtEnd() && !IsBlockTerminator())
        {
            Advance();
        }
    }

    private void ReportError(string message) =>
        _diagnostics.Add(new ParseDiagnostic("LUA0001", message, Current().Span, DiagnosticSeverity.Error));

    private bool TryMatchLiteral(out SyntaxToken token)
    {
        if (MatchKeyword("nil") || MatchKeyword("true") || MatchKeyword("false") ||
            Current().Kind is LuaTokenKind.Number or LuaTokenKind.String)
        {
            token = AdvanceSyntaxToken();
            return true;
        }

        token = new SyntaxToken(string.Empty, Current().Span, null);
        return false;
    }
}
