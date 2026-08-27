using System.Globalization;
using LUAstudio.Abstractions;
using LUAstudio.Storage;

namespace LUAstudio.Core;

public sealed class SettingsBootstrap
{
    private readonly ISettingsRepository _repository;
    private bool _isLoading;

    public SettingsBootstrap(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _isLoading = true;
        try
        {
            await LoadValueAsync(SettingKeys.FpsLimit, int.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.ApplicationLanguage, v => v, cancellationToken).ConfigureAwait(false);

            await LoadValueAsync(SettingKeys.EditorFontFamily, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorFontSize, v => double.Parse(v, CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorFontBold, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorFontItalic, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorTabWidth, int.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorShowLineNumbers, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorWordWrap, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorConvertTabsToSpaces, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorForeground, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorBackground, v => v, cancellationToken).ConfigureAwait(false);

            await LoadValueAsync(SettingKeys.EditorColorKeyword, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorString, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorNumber, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorComment, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorOperator, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorFunction, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorType, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorBuiltin, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorGlobal, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorGhostText, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorText, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorBracket, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorTodo, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorLocalMethod, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorColorLocalProperty, v => v, cancellationToken).ConfigureAwait(false);

            await LoadValueAsync(SettingKeys.EditorAutoComplete, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorInlineCompletions, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorAutoPairBrackets, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorSmartEnter, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorSemanticHighlighting, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.EditorAutoSwitchOnOpen, bool.Parse, cancellationToken).ConfigureAwait(false);

            await LoadValueAsync(SettingKeys.DiagnosticsEnabled, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.DiagnosticsEnvironmentProfile, v => v, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.DiagnosticsStrictMode, bool.Parse, cancellationToken).ConfigureAwait(false);
            await LoadValueAsync(SettingKeys.DiagnosticsShowRequireGraph, bool.Parse, cancellationToken).ConfigureAwait(false);

            await LoadValueAsync(SettingKeys.RestoreWorkspaceRoots, bool.Parse, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void AttachPersistence()
    {
        Persist<string>(SettingKeys.ApplicationLanguage, v => v);
        Persist<int>(SettingKeys.FpsLimit, v => v.ToString(CultureInfo.InvariantCulture));

        Persist<string>(SettingKeys.EditorFontFamily, v => v);
        Persist<double>(SettingKeys.EditorFontSize, v => v.ToString(CultureInfo.InvariantCulture));
        Persist<bool>(SettingKeys.EditorFontBold, v => v.ToString());
        Persist<bool>(SettingKeys.EditorFontItalic, v => v.ToString());
        Persist<int>(SettingKeys.EditorTabWidth, v => v.ToString(CultureInfo.InvariantCulture));
        Persist<bool>(SettingKeys.EditorShowLineNumbers, v => v.ToString());
        Persist<bool>(SettingKeys.EditorWordWrap, v => v.ToString());
        Persist<bool>(SettingKeys.EditorConvertTabsToSpaces, v => v.ToString());
        Persist<string>(SettingKeys.EditorForeground, v => v);
        Persist<string>(SettingKeys.EditorBackground, v => v);

        Persist<string>(SettingKeys.EditorColorKeyword, v => v);
        Persist<string>(SettingKeys.EditorColorString, v => v);
        Persist<string>(SettingKeys.EditorColorNumber, v => v);
        Persist<string>(SettingKeys.EditorColorComment, v => v);
        Persist<string>(SettingKeys.EditorColorOperator, v => v);
        Persist<string>(SettingKeys.EditorColorFunction, v => v);
        Persist<string>(SettingKeys.EditorColorType, v => v);
        Persist<string>(SettingKeys.EditorColorBuiltin, v => v);
        Persist<string>(SettingKeys.EditorColorGlobal, v => v);
        Persist<string>(SettingKeys.EditorColorGhostText, v => v);
        Persist<string>(SettingKeys.EditorColorText, v => v);
        Persist<string>(SettingKeys.EditorColorBracket, v => v);
        Persist<string>(SettingKeys.EditorColorTodo, v => v);
        Persist<string>(SettingKeys.EditorColorLocalMethod, v => v);
        Persist<string>(SettingKeys.EditorColorLocalProperty, v => v);

        Persist<bool>(SettingKeys.EditorAutoComplete, v => v.ToString());
        Persist<bool>(SettingKeys.EditorInlineCompletions, v => v.ToString());
        Persist<bool>(SettingKeys.EditorAutoPairBrackets, v => v.ToString());
        Persist<bool>(SettingKeys.EditorSmartEnter, v => v.ToString());
        Persist<bool>(SettingKeys.EditorSemanticHighlighting, v => v.ToString());
        Persist<bool>(SettingKeys.EditorAutoSwitchOnOpen, v => v.ToString());

        Persist<bool>(SettingKeys.DiagnosticsEnabled, v => v.ToString());
        Persist<string>(SettingKeys.DiagnosticsEnvironmentProfile, v => v);
        Persist<bool>(SettingKeys.DiagnosticsStrictMode, v => v.ToString());
        Persist<bool>(SettingKeys.DiagnosticsShowRequireGraph, v => v.ToString());

        Persist<bool>(SettingKeys.RestoreWorkspaceRoots, v => v.ToString());
    }

    private async Task LoadValueAsync<T>(string key, Func<string, T> parse, CancellationToken cancellationToken)
    {
        var stored = await _repository.GetAsync(key, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return;
        }

        try
        {
            var global = Engine.Globals.Get<T>(key);
            if (global is not null)
            {
                global.Value = parse(stored);
            }
        }
        catch
        {
            // Ignore invalid stored values and keep defaults.
        }
    }

    private void Persist<T>(string key, Func<T, string> format)
    {
        var global = Engine.Globals.Get<T>(key);
        if (global is null)
        {
            return;
        }

        global.Changed += value =>
        {
            if (_isLoading)
            {
                return;
            }

            _ = _repository.SetAsync(key, format(value));
        };
    }
}
