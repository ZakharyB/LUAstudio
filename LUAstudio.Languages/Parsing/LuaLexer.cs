using LUAstudio.Languages.Text;

namespace LUAstudio.Languages.Parsing;

public  enum LuaTokenKind
{
    EndOfFile,
    Identifier,
    Number,
    String,
    Keyword,
    Operator,
    Punctuation,
    Comment,
    Whitespace
}

public readonly record struct LuaToken(LuaTokenKind Kind, string Text, TextSpan Span, string? Keyword = null);

internal sealed class LuaLexer
{
    private readonly string _text;
    private int _pos;

    public LuaLexer(string text) => _text = text;

    public List<LuaToken> Tokenize()
    {
        var tokens = new List<LuaToken>();
        while (_pos < _text.Length)
        {
            SkipTrivia(tokens);
            if (_pos >= _text.Length)
            {
                break;
            }

            var start = _pos;
            var c = _text[_pos];

            if (char.IsLetter(c) || c == '_')
            {
                ReadIdentifier(start, tokens);
            }
            else if (char.IsDigit(c))
            {
                ReadNumber(start, tokens);
            }
            else if (c is '"' or '\'')
            {
                ReadString(start, c, tokens);
            }
            else if (c == '-' && Peek(1) == '-')
            {
                ReadComment(start, tokens);
            }
            else
            {
                ReadOperatorOrPunctuation(start, tokens);
            }
        }

        tokens.Add(new LuaToken(LuaTokenKind.EndOfFile, string.Empty, new TextSpan(_pos, 0)));
        return tokens;
    }

    private void SkipTrivia(List<LuaToken> tokens)
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
        {
            var start = _pos++;
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }

            tokens.Add(new LuaToken(LuaTokenKind.Whitespace, _text[start.._pos], TextSpan.FromBounds(start, _pos)));
        }
    }

    private void ReadIdentifier(int start, List<LuaToken> tokens)
    {
        _pos++;
        while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_'))
        {
            _pos++;
        }

        var text = _text[start.._pos];
        var keyword = IsKeyword(text) ? text : null;
        var kind = keyword is not null ? LuaTokenKind.Keyword : LuaTokenKind.Identifier;
        tokens.Add(new LuaToken(kind, text, TextSpan.FromBounds(start, _pos), keyword));
    }

    private void ReadNumber(int start, List<LuaToken> tokens)
    {
        _pos++;
        while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] is '.' or 'x' or 'X'))
        {
            _pos++;
        }

        tokens.Add(new LuaToken(LuaTokenKind.Number, _text[start.._pos], TextSpan.FromBounds(start, _pos)));
    }

    private void ReadString(int start, char quote, List<LuaToken> tokens)
    {
        _pos++;
        while (_pos < _text.Length && _text[_pos] != quote)
        {
            if (_text[_pos] == '\\' && _pos + 1 < _text.Length)
            {
                _pos += 2;
            }
            else
            {
                _pos++;
            }
        }

        if (_pos < _text.Length)
        {
            _pos++;
        }

        tokens.Add(new LuaToken(LuaTokenKind.String, _text[start.._pos], TextSpan.FromBounds(start, _pos)));
    }
    
    private void ReadComment(int start, List<LuaToken> tokens)
    {
        _pos += 2;

        if (_pos < _text.Length && _text[_pos] == '[' && Peek(1) == '[')
        {
            _pos += 2;

            while (_pos < _text.Length)
            {
                if (_text[_pos] == ']' && Peek(1) == ']')
                {
                    _pos += 2;
                    break;
                }

             
                if (_text[_pos] == '-' &&
                    Peek(1) == '-' &&
                    Peek(2) == ']' &&
                    Peek(3) == ']')
                {
                    _pos += 4;
                    break;
                }

                _pos++;
            }
        }
        else
        {
            while (_pos < _text.Length && _text[_pos] != '\n')
                _pos++;
        }

        tokens.Add(new LuaToken(
            LuaTokenKind.Comment,
            _text[start.._pos],
            TextSpan.FromBounds(start, _pos)
        ));
    }

    private void ReadOperatorOrPunctuation(int start, List<LuaToken> tokens)
    {
        _pos = start;

        char c1 = _text[_pos];
        char c2 = _pos + 1 < _text.Length ? _text[_pos + 1] : '\0';

        bool isTwoCharOp =
            (c1 == '.' && c2 == '.') ||   // ..
            (c1 == ':' && c2 == ':') ||   // ::
            (c1 == '-' && c2 == '>') ||   // ->
            (c1 == '>' && c2 == '=') ||   // >=
            (c1 == '<' && c2 == '=') ||   // <=
            (c1 == '~' && c2 == '=') ||   // ~=
            (c1 == '=' && c2 == '=');     // ==

        _pos += isTwoCharOp ? 2 : 1;

        int length = _pos - start;
        var span = _text.AsSpan(start, length);

        bool isPunctuation =
            length == 1 &&
            (c1 == '(' || c1 == ')' || c1 == '{' || c1 == '}' ||
             c1 == '[' || c1 == ']' || c1 == ',' || c1 == ';' ||
             c1 == '.');

        tokens.Add(new LuaToken(
            isPunctuation ? LuaTokenKind.Punctuation : LuaTokenKind.Operator,
            span.ToString(),
            TextSpan.FromBounds(start, _pos)
        ));
    }

    private char Peek(int offset) =>
        _pos + offset < _text.Length ? _text[_pos + offset] : '\0';

    private static bool IsKeyword(string text) => text switch
    {
        "and" or "break" or "do" or "else" or "elseif" or "end" or "false" or "for" or "function"
            or "goto" or "if" or "in" or "local" or "nil" or "not" or "or" or "repeat" or "return"
            or "then" or "true" or "until" or "while" or "type" or "export" => true,
        _ => false
    };
}
