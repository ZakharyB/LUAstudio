namespace LUAstudio.Abstractions;

public enum LuaEnvironmentProfile
{
    StandardLua,
    RobloxLua,
    Custom
}

public static class LuaEnvironmentProfiles
{
    public const string StandardLua = "standard";
    public const string RobloxLua = "roblox";
    public const string Custom = "custom";

    public static string ToStorageValue(LuaEnvironmentProfile profile) => profile switch
    {
        LuaEnvironmentProfile.StandardLua => StandardLua,
        LuaEnvironmentProfile.RobloxLua => RobloxLua,
        LuaEnvironmentProfile.Custom => Custom,
        _ => RobloxLua
    };

    public static LuaEnvironmentProfile FromStorageValue(string? value) => value switch
    {
        StandardLua => LuaEnvironmentProfile.StandardLua,
        Custom => LuaEnvironmentProfile.Custom,
        _ => LuaEnvironmentProfile.RobloxLua
    };
}
