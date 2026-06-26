using LUAstudio.Abstractions;
using LUAstudio.Core;

namespace LUAstudio;

public static class RuntimeGlobals
{
    public static void RegisterDefaults()
    {
        Register(SettingKeys.FpsLimit, new GlobalValue<int>(SettingKeys.FpsLimit, 60));

        Register(SettingKeys.EditorFontFamily, new GlobalValue<string>(SettingKeys.EditorFontFamily, "Cascadia Code, Consolas, Courier New"));
        Register(SettingKeys.EditorFontSize, new GlobalValue<double>(SettingKeys.EditorFontSize, 14));
        Register(SettingKeys.EditorFontBold, new GlobalValue<bool>(SettingKeys.EditorFontBold, false));
        Register(SettingKeys.EditorFontItalic, new GlobalValue<bool>(SettingKeys.EditorFontItalic, false));
        Register(SettingKeys.EditorTabWidth, new GlobalValue<int>(SettingKeys.EditorTabWidth, 4));
        Register(SettingKeys.EditorShowLineNumbers, new GlobalValue<bool>(SettingKeys.EditorShowLineNumbers, true));
        Register(SettingKeys.EditorWordWrap, new GlobalValue<bool>(SettingKeys.EditorWordWrap, false));
        Register(SettingKeys.EditorConvertTabsToSpaces, new GlobalValue<bool>(SettingKeys.EditorConvertTabsToSpaces, true));
        Register(SettingKeys.EditorForeground, new GlobalValue<string>(SettingKeys.EditorForeground, "#BCBEC8"));
        Register(SettingKeys.EditorBackground, new GlobalValue<string>(SettingKeys.EditorBackground, "#0E0F11"));

        Register(SettingKeys.EditorColorKeyword, new GlobalValue<string>(SettingKeys.EditorColorKeyword, "#C586C8"));
        Register(SettingKeys.EditorColorString, new GlobalValue<string>(SettingKeys.EditorColorString, "#CE9178"));
        Register(SettingKeys.EditorColorNumber, new GlobalValue<string>(SettingKeys.EditorColorNumber, "#B5CEA8"));
        Register(SettingKeys.EditorColorComment, new GlobalValue<string>(SettingKeys.EditorColorComment, "#6A9955"));
        Register(SettingKeys.EditorColorOperator, new GlobalValue<string>(SettingKeys.EditorColorOperator, "#D4D4D4"));
        Register(SettingKeys.EditorColorFunction, new GlobalValue<string>(SettingKeys.EditorColorFunction, "#DCDCAA"));
        Register(SettingKeys.EditorColorType, new GlobalValue<string>(SettingKeys.EditorColorType, "#4EC9B0"));
        Register(SettingKeys.EditorColorBuiltin, new GlobalValue<string>(SettingKeys.EditorColorBuiltin, "#4EC9B0"));
        Register(SettingKeys.EditorColorGlobal, new GlobalValue<string>(SettingKeys.EditorColorGlobal, "#569CD6"));
        Register(SettingKeys.EditorColorGhostText, new GlobalValue<string>(SettingKeys.EditorColorGhostText, "#5A5D66"));
        Register(SettingKeys.EditorColorText, new GlobalValue<string>(SettingKeys.EditorColorText, "#BCBEC8"));
        Register(SettingKeys.EditorColorBracket, new GlobalValue<string>(SettingKeys.EditorColorBracket, "#BCBEC8"));
        Register(SettingKeys.EditorColorTodo, new GlobalValue<string>(SettingKeys.EditorColorTodo, "#FFCC66"));
        Register(SettingKeys.EditorColorLocalMethod, new GlobalValue<string>(SettingKeys.EditorColorLocalMethod, "#D4D4AA"));
        Register(SettingKeys.EditorColorLocalProperty, new GlobalValue<string>(SettingKeys.EditorColorLocalProperty, "#9CDCFE"));

        Register(SettingKeys.EditorAutoComplete, new GlobalValue<bool>(SettingKeys.EditorAutoComplete, true));
        Register(SettingKeys.EditorInlineCompletions, new GlobalValue<bool>(SettingKeys.EditorInlineCompletions, true));
        Register(SettingKeys.EditorAutoPairBrackets, new GlobalValue<bool>(SettingKeys.EditorAutoPairBrackets, true));
        Register(SettingKeys.EditorSmartEnter, new GlobalValue<bool>(SettingKeys.EditorSmartEnter, true));
        Register(SettingKeys.EditorSemanticHighlighting, new GlobalValue<bool>(SettingKeys.EditorSemanticHighlighting, true));
        Register(SettingKeys.EditorAutoSwitchOnOpen, new GlobalValue<bool>(SettingKeys.EditorAutoSwitchOnOpen, true));

        Register(SettingKeys.DiagnosticsEnabled, new GlobalValue<bool>(SettingKeys.DiagnosticsEnabled, true));
        Register(SettingKeys.DiagnosticsEnvironmentProfile, new GlobalValue<string>(SettingKeys.DiagnosticsEnvironmentProfile, LuaEnvironmentProfiles.RobloxLua));
        Register(SettingKeys.DiagnosticsStrictMode, new GlobalValue<bool>(SettingKeys.DiagnosticsStrictMode, false));
        Register(SettingKeys.DiagnosticsShowRequireGraph, new GlobalValue<bool>(SettingKeys.DiagnosticsShowRequireGraph, true));

        Register(SettingKeys.RestoreWorkspaceRoots, new GlobalValue<bool>(SettingKeys.RestoreWorkspaceRoots, true));
    }

    private static void Register<T>(string key, GlobalValue<T> value) =>
        Engine.Globals.Register(key, value);
}
