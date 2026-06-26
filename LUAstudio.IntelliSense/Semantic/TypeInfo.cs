namespace LUAstudio.IntelliSense.Semantic;

public sealed class TypeInfo
{
    public string DisplayName { get; init; } = "unknown";

    public bool IsNullable { get; init; }

    public bool IsUnion { get; init; }

    public IReadOnlyList<TypeInfo>? UnionMembers { get; init; }

    public IReadOnlyDictionary<string, TypeInfo>? TableShape { get; init; }

    public bool IsNil { get; init; }

    public static TypeInfo Unknown { get; } = new() { DisplayName = "unknown" };

    public static TypeInfo Nil { get; } = new() { DisplayName = "nil", IsNil = true };

    public static TypeInfo String { get; } = new() { DisplayName = "string" };

    public static TypeInfo Number { get; } = new() { DisplayName = "number" };

    public static TypeInfo Boolean { get; } = new() { DisplayName = "boolean" };

    public static TypeInfo Table { get; } = new() { DisplayName = "table" };

    public static TypeInfo Function { get; } = new() { DisplayName = "function" };

    public static TypeInfo FromAnnotation(string name, bool nullable = false) =>
        new() { DisplayName = name, IsNullable = nullable };

    public static TypeInfo FromLiteralToken(string token) => token switch
    {
        "nil" => Nil,
        "true" or "false" => Boolean,
        _ when token.StartsWith('"') || token.StartsWith('\'') => String,
        _ when double.TryParse(token, out _) => Number,
        _ => Unknown
    };

    public static TypeInfo Union(params TypeInfo[] members)
    {
        var distinct = members.Where(m => !m.IsUnknown).DistinctBy(m => m.DisplayName).ToArray();
        if (distinct.Length == 0)
        {
            return Unknown;
        }

        if (distinct.Length == 1)
        {
            return distinct[0];
        }

        return new TypeInfo
        {
            DisplayName = string.Join(" | ", distinct.Select(m => m.DisplayName)),
            IsUnion = true,
            UnionMembers = distinct
        };
    }

    public bool IsUnknown => DisplayName == "unknown";

    public bool MightBeNil => IsNil || IsNullable;

    public bool IsCompatibleWith(TypeInfo other)
    {
        if (IsUnknown || other.IsUnknown)
        {
            return true;
        }

        if (IsUnion && UnionMembers is not null)
        {
            return UnionMembers.Any(m => m.IsCompatibleWith(other));
        }

        if (other.IsUnion && other.UnionMembers is not null)
        {
            return other.UnionMembers.Any(m => IsCompatibleWith(m));
        }

        if (DisplayName == other.DisplayName)
        {
            return true;
        }

        if (DisplayName == "any" || other.DisplayName == "any")
        {
            return true;
        }

        return false;
    }

    public override string ToString() => IsNullable ? $"{DisplayName}?" : DisplayName;
}
