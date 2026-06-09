namespace LUAstudio.Languages.Parsing;

public static class LuaTokenizer
{
    public static IReadOnlyList<LuaToken> Tokenize(string text) =>
        new LuaLexer(text).Tokenize();
}
