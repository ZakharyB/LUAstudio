using CommunityToolkit.Mvvm.ComponentModel;
using LUAstudio;
using LUAstudio.Abstractions;
using LUAstudio.Core;

namespace LUAstudio.Settings.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly List<(string Key, Delegate Handler)> _subscriptions = new();

    public SettingsViewModel()
    {
        Track<string>(SettingKeys.EditorFontFamily, () => OnPropertyChanged(nameof(EditorFontFamily)));
        Track<double>(SettingKeys.EditorFontSize, () => OnPropertyChanged(nameof(EditorFontSize)));
        Track<bool>(SettingKeys.EditorFontBold, () => OnPropertyChanged(nameof(EditorFontBold)));
        Track<bool>(SettingKeys.EditorFontItalic, () => OnPropertyChanged(nameof(EditorFontItalic)));
        Track<int>(SettingKeys.EditorTabWidth, () => OnPropertyChanged(nameof(EditorTabWidth)));
        Track<bool>(SettingKeys.EditorShowLineNumbers, () => OnPropertyChanged(nameof(EditorShowLineNumbers)));
        Track<bool>(SettingKeys.EditorWordWrap, () => OnPropertyChanged(nameof(EditorWordWrap)));
        Track<bool>(SettingKeys.EditorConvertTabsToSpaces, () => OnPropertyChanged(nameof(EditorConvertTabsToSpaces)));
        Track<string>(SettingKeys.EditorForeground, () => OnPropertyChanged(nameof(EditorForeground)));
        Track<string>(SettingKeys.EditorBackground, () => OnPropertyChanged(nameof(EditorBackground)));
        TrackColor(SettingKeys.EditorColorKeyword, nameof(EditorColorKeyword));
        TrackColor(SettingKeys.EditorColorString, nameof(EditorColorString));
        TrackColor(SettingKeys.EditorColorNumber, nameof(EditorColorNumber));
        TrackColor(SettingKeys.EditorColorComment, nameof(EditorColorComment));
        TrackColor(SettingKeys.EditorColorOperator, nameof(EditorColorOperator));
        TrackColor(SettingKeys.EditorColorFunction, nameof(EditorColorFunction));
        TrackColor(SettingKeys.EditorColorType, nameof(EditorColorType));
        TrackColor(SettingKeys.EditorColorBuiltin, nameof(EditorColorBuiltin));
        TrackColor(SettingKeys.EditorColorGlobal, nameof(EditorColorGlobal));
        TrackColor(SettingKeys.EditorColorGhostText, nameof(EditorColorGhostText));
        TrackColor(SettingKeys.EditorColorText, nameof(EditorColorText));
        TrackColor(SettingKeys.EditorColorBracket, nameof(EditorColorBracket));
        TrackColor(SettingKeys.EditorColorTodo, nameof(EditorColorTodo));
        TrackColor(SettingKeys.EditorColorLocalMethod, nameof(EditorColorLocalMethod));
        TrackColor(SettingKeys.EditorColorLocalProperty, nameof(EditorColorLocalProperty));
        Track<bool>(SettingKeys.EditorAutoComplete, () => OnPropertyChanged(nameof(EditorAutoComplete)));
        Track<bool>(SettingKeys.EditorInlineCompletions, () => OnPropertyChanged(nameof(EditorInlineCompletions)));
        Track<bool>(SettingKeys.EditorAutoPairBrackets, () => OnPropertyChanged(nameof(EditorAutoPairBrackets)));
        Track<bool>(SettingKeys.EditorSmartEnter, () => OnPropertyChanged(nameof(EditorSmartEnter)));
        Track<bool>(SettingKeys.EditorSemanticHighlighting, () => OnPropertyChanged(nameof(EditorSemanticHighlighting)));
        Track<bool>(SettingKeys.EditorAutoSwitchOnOpen, () => OnPropertyChanged(nameof(EditorAutoSwitchOnOpen)));
        Track<bool>(SettingKeys.DiagnosticsEnabled, () => OnPropertyChanged(nameof(DiagnosticsEnabled)));
        Track<string>(SettingKeys.DiagnosticsEnvironmentProfile, () => OnPropertyChanged(nameof(DiagnosticsEnvironmentProfile)));
        Track<bool>(SettingKeys.DiagnosticsStrictMode, () => OnPropertyChanged(nameof(DiagnosticsStrictMode)));
        Track<bool>(SettingKeys.DiagnosticsShowRequireGraph, () => OnPropertyChanged(nameof(DiagnosticsShowRequireGraph)));
        Track<bool>(SettingKeys.RestoreWorkspaceRoots, () => OnPropertyChanged(nameof(RestoreWorkspaceOnStartup)));
        Track<int>(SettingKeys.FpsLimit, () => OnPropertyChanged(nameof(FpsLimit)));
        Track<double>(SettingKeys.EditorBreakpointMarginWidth, () => OnPropertyChanged(nameof(EditorBreakpointMarginWidth)));
        Track<double>(SettingKeys.EditorHighlightDurationSeconds, () => OnPropertyChanged(nameof(EditorHighlightDurationSeconds)));
        Track<bool>(SettingKeys.EditorShowRelativeLineNumbers, () => OnPropertyChanged(nameof(EditorShowRelativeLineNumbers)));
        Track<bool>(SettingKeys.EditorHighlightCurrentLine, () => OnPropertyChanged(nameof(EditorHighlightCurrentLine)));
        Track<bool>(SettingKeys.EditorShowBracketHighlighting, () => OnPropertyChanged(nameof(EditorShowBracketHighlighting)));
    }
    
    public sealed record LanguageOption(
        string Code,
        string DisplayName);

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("en", "English"),
        new("fr", "Français"),
        new("af", "Afrikaans"),
        new("es", "español")
    ];

    public LanguageOption SelectedLanguage
    {
        get
        {
            return LanguageOptions.First(
                x => x.Code == LocalizationManager.CurrentLanguage);
        }

        set
        {
            if (value is null ||
                value.Code == LocalizationManager.CurrentLanguage)
                return;

            LocalizationManager.SetLanguage(value.Code);
            OnPropertyChanged();
        }
    }


    public string EditorFontFamily
    {
        get => Get(SettingKeys.EditorFontFamily, string.Empty);
        set => Set(SettingKeys.EditorFontFamily, value);
    }

    public double EditorFontSize
    {
        get => Get(SettingKeys.EditorFontSize, 14d);
        set => Set(SettingKeys.EditorFontSize, value);
    }

    public bool EditorFontBold
    {
        get => Get(SettingKeys.EditorFontBold, false);
        set => Set(SettingKeys.EditorFontBold, value);
    }

    public bool EditorFontItalic
    {
        get => Get(SettingKeys.EditorFontItalic, false);
        set => Set(SettingKeys.EditorFontItalic, value);
    }

    public int EditorTabWidth
    {
        get => Get(SettingKeys.EditorTabWidth, 4);
        set => Set(SettingKeys.EditorTabWidth, Math.Clamp(value, 1, 16));
    }

    public bool EditorShowLineNumbers
    {
        get => Get(SettingKeys.EditorShowLineNumbers, true);
        set => Set(SettingKeys.EditorShowLineNumbers, value);
    }

    public bool EditorWordWrap
    {
        get => Get(SettingKeys.EditorWordWrap, false);
        set => Set(SettingKeys.EditorWordWrap, value);
    }

    public bool EditorConvertTabsToSpaces
    {
        get => Get(SettingKeys.EditorConvertTabsToSpaces, true);
        set => Set(SettingKeys.EditorConvertTabsToSpaces, value);
    }

    public string EditorForeground
    {
        get => Get(SettingKeys.EditorForeground, "#BCBEC8");
        set => Set(SettingKeys.EditorForeground, value);
    }

    public string EditorBackground
    {
        get => Get(SettingKeys.EditorBackground, "#0E0F11");
        set => Set(SettingKeys.EditorBackground, value);
    }

    public string EditorColorKeyword
    {
        get => Get(SettingKeys.EditorColorKeyword, "#C586C8");
        set => Set(SettingKeys.EditorColorKeyword, value);
    }

    public string EditorColorString
    {
        get => Get(SettingKeys.EditorColorString, "#CE9178");
        set => Set(SettingKeys.EditorColorString, value);
    }

    public string EditorColorNumber
    {
        get => Get(SettingKeys.EditorColorNumber, "#B5CEA8");
        set => Set(SettingKeys.EditorColorNumber, value);
    }

    public string EditorColorComment
    {
        get => Get(SettingKeys.EditorColorComment, "#6A9955");
        set => Set(SettingKeys.EditorColorComment, value);
    }

    public string EditorColorOperator
    {
        get => Get(SettingKeys.EditorColorOperator, "#D4D4D4");
        set => Set(SettingKeys.EditorColorOperator, value);
    }

    public string EditorColorFunction
    {
        get => Get(SettingKeys.EditorColorFunction, "#DCDCAA");
        set => Set(SettingKeys.EditorColorFunction, value);
    }

    public string EditorColorType
    {
        get => Get(SettingKeys.EditorColorType, "#4EC9B0");
        set => Set(SettingKeys.EditorColorType, value);
    }

    public string EditorColorBuiltin
    {
        get => Get(SettingKeys.EditorColorBuiltin, "#4EC9B0");
        set => Set(SettingKeys.EditorColorBuiltin, value);
    }

    public string EditorColorGlobal
    {
        get => Get(SettingKeys.EditorColorGlobal, "#569CD6");
        set => Set(SettingKeys.EditorColorGlobal, value);
    }

    public string EditorColorGhostText
    {
        get => Get(SettingKeys.EditorColorGhostText, "#5A5D66");
        set => Set(SettingKeys.EditorColorGhostText, value);
    }

    public string EditorColorText
    {
        get => Get(SettingKeys.EditorColorText, "#BCBEC8");
        set => Set(SettingKeys.EditorColorText, value);
    }

    public string EditorColorBracket
    {
        get => Get(SettingKeys.EditorColorBracket, "#BCBEC8");
        set => Set(SettingKeys.EditorColorBracket, value);
    }

    public string EditorColorTodo
    {
        get => Get(SettingKeys.EditorColorTodo, "#FFCC66");
        set => Set(SettingKeys.EditorColorTodo, value);
    }

    public string EditorColorLocalMethod
    {
        get => Get(SettingKeys.EditorColorLocalMethod, "#D4D4AA");
        set => Set(SettingKeys.EditorColorLocalMethod, value);
    }

    public string EditorColorLocalProperty
    {
        get => Get(SettingKeys.EditorColorLocalProperty, "#9CDCFE");
        set => Set(SettingKeys.EditorColorLocalProperty, value);
    }

    public bool EditorAutoComplete
    {
        get => Get(SettingKeys.EditorAutoComplete, true);
        set => Set(SettingKeys.EditorAutoComplete, value);
    }

    public bool EditorInlineCompletions
    {
        get => Get(SettingKeys.EditorInlineCompletions, true);
        set => Set(SettingKeys.EditorInlineCompletions, value);
    }

    public bool EditorAutoPairBrackets
    {
        get => Get(SettingKeys.EditorAutoPairBrackets, true);
        set => Set(SettingKeys.EditorAutoPairBrackets, value);
    }

    public bool EditorSmartEnter
    {
        get => Get(SettingKeys.EditorSmartEnter, true);
        set => Set(SettingKeys.EditorSmartEnter, value);
    }

    public bool EditorSemanticHighlighting
    {
        get => Get(SettingKeys.EditorSemanticHighlighting, true);
        set => Set(SettingKeys.EditorSemanticHighlighting, value);
    }

    public bool EditorAutoSwitchOnOpen
    {
        get => Get(SettingKeys.EditorAutoSwitchOnOpen, true);
        set => Set(SettingKeys.EditorAutoSwitchOnOpen, value);
    }

    public bool DiagnosticsEnabled
    {
        get => Get(SettingKeys.DiagnosticsEnabled, true);
        set => Set(SettingKeys.DiagnosticsEnabled, value);
    }

    public string DiagnosticsEnvironmentProfile
    {
        get => Get(SettingKeys.DiagnosticsEnvironmentProfile, LuaEnvironmentProfiles.RobloxLua);
        set => Set(SettingKeys.DiagnosticsEnvironmentProfile, value);
    }

    public bool DiagnosticsStrictMode
    {
        get => Get(SettingKeys.DiagnosticsStrictMode, false);
        set => Set(SettingKeys.DiagnosticsStrictMode, value);
    }

    public bool DiagnosticsShowRequireGraph
    {
        get => Get(SettingKeys.DiagnosticsShowRequireGraph, true);
        set => Set(SettingKeys.DiagnosticsShowRequireGraph, value);
    }

    public IReadOnlyList<string> EnvironmentProfileOptions { get; } =
        [LuaEnvironmentProfiles.StandardLua, LuaEnvironmentProfiles.RobloxLua, LuaEnvironmentProfiles.Custom];

    public bool RestoreWorkspaceOnStartup
    {
        get => Get(SettingKeys.RestoreWorkspaceRoots, true);
        set => Set(SettingKeys.RestoreWorkspaceRoots, value);
    }

    public int FpsLimit
    {
        get => Get(SettingKeys.FpsLimit, 60);
        set => Set(SettingKeys.FpsLimit, Math.Clamp(value, 15, 360));
    }

    public double EditorBreakpointMarginWidth
    {
        get => Get(SettingKeys.EditorBreakpointMarginWidth, 20d);
        set => Set(SettingKeys.EditorBreakpointMarginWidth, value);
    }

    public double EditorHighlightDurationSeconds
    {
        get => Get(SettingKeys.EditorHighlightDurationSeconds, 2d);
        set => Set(SettingKeys.EditorHighlightDurationSeconds, value);
    }

    public bool EditorShowRelativeLineNumbers
    {
        get => Get(SettingKeys.EditorShowRelativeLineNumbers, false);
        set => Set(SettingKeys.EditorShowRelativeLineNumbers, value);
    }

    public bool EditorHighlightCurrentLine
    {
        get => Get(SettingKeys.EditorHighlightCurrentLine, true);
        set => Set(SettingKeys.EditorHighlightCurrentLine, value);
    }

    public bool EditorShowBracketHighlighting
    {
        get => Get(SettingKeys.EditorShowBracketHighlighting, true);
        set => Set(SettingKeys.EditorShowBracketHighlighting, value);
    }
    
    public IReadOnlyList<string> FontSizeOptions { get; } =
        ["10", "11", "12", "13", "14", "15", "16", "18", "20", "22", "24"];

    public IReadOnlyList<string> TabWidthOptions { get; } = ["2", "4", "8"];

    private static T Get<T>(string key, T fallback)
    {
        var global = Engine.Globals.Get<T>(key);
        return global is null ? fallback : global.Value;
    }

    private static void Set<T>(string key, T value)
    {
        var global = Engine.Globals.Get<T>(key);
        if (global is not null)
        {
            global.Value = value;
        }
    }

    private void Track<T>(string key, Action onChanged)
    {
        var global = Engine.Globals.Get<T>(key);
        if (global is null)
        {
            return;
        }

        Action<T> handler = _ => onChanged();
        global.Changed += handler;
        _subscriptions.Add((key, handler));
    }

    private void TrackColor(string key, string propertyName) =>
        Track<string>(key, () => OnPropertyChanged(propertyName));
}
