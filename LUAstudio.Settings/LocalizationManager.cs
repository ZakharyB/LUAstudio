using System.Globalization;
using System.Windows;
using LUAstudio.Abstractions;
using LUAstudio.Core;

namespace LUAstudio.Settings;
public static class LocalizationManager
{
    // TODO : Add spanish
    public const string English = "en";
    public const string French = "fr";

    private static readonly HashSet<string> SupportedLanguages =
    [
        English,
        French
    ];

    private static ResourceDictionary? _currentDictionary;

    public static string CurrentLanguage { get; private set; } = English;

    public static event Action<string>? LanguageChanged;

    public static void Initialize()
    {
        var savedSetting =
            Engine.Globals.Get<string>(SettingKeys.ApplicationLanguage);

        string language;

        if (savedSetting is not null &&
            !string.IsNullOrWhiteSpace(savedSetting.Value))
        {
            // User explicitly selected a language before.
            language = savedSetting.Value;
        }
        else
        {
            // First launch: follow Windows.
            language = CultureInfo
                .CurrentUICulture
                .TwoLetterISOLanguageName;
        }

        SetLanguage(language, save: false);
    }

    public static void SetLanguage(string language, bool save = true)
    {
        language = language.ToLowerInvariant();

        if (!SupportedLanguages.Contains(language))
            language = English;

        var dictionary = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/LUAstudio;component/Resources/Strings.{language}.xaml",
                UriKind.Absolute)
        };

        var dictionaries =
            Application.Current.Resources.MergedDictionaries;

        if (_currentDictionary is not null)
            dictionaries.Remove(_currentDictionary);

        dictionaries.Add(dictionary);

        _currentDictionary = dictionary;
        CurrentLanguage = language;

        if (save)
        {
            var setting =
                Engine.Globals.Get<string>(
                    SettingKeys.ApplicationLanguage);

            if (setting is not null)
                setting.Value = language;
        }

        LanguageChanged?.Invoke(language);
    }

    public static string GetTranslation(string key)
    {
        return Application.Current.TryFindResource(key) as string
               ?? key;
    }

    public static bool IsSupported(string language)
    {
        return SupportedLanguages.Contains(
            language.ToLowerInvariant());
    }
}