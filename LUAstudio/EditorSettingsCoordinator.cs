using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using LUAstudio.Abstractions;
using LUAstudio.Core;
using LUAstudio.Editor.Editing;
using LUAstudio.Editor.Highlighting;

namespace LUAstudio;

public sealed class EditorSettingsCoordinator
{
    private readonly HashSet<TextEditor> _editors = new();

    public void Start()
    {
        Subscribe<string>(SettingKeys.EditorFontFamily, ApplyToAllEditors);
        Subscribe<double>(SettingKeys.EditorFontSize, ApplyToAllEditors);
        Subscribe<bool>(SettingKeys.EditorFontBold, ApplyToAllEditors);
        Subscribe<bool>(SettingKeys.EditorFontItalic, ApplyToAllEditors);
        Subscribe<int>(SettingKeys.EditorTabWidth, ApplyToAllEditors);
        Subscribe<bool>(SettingKeys.EditorShowLineNumbers, ApplyToAllEditors);
        Subscribe<bool>(SettingKeys.EditorWordWrap, ApplyToAllEditors);
        Subscribe<bool>(SettingKeys.EditorConvertTabsToSpaces, ApplyToAllEditors);
        Subscribe<string>(SettingKeys.EditorForeground, ApplyToAllEditors);
        Subscribe<string>(SettingKeys.EditorBackground, ApplyToAllEditors);

        SubscribeColor(SettingKeys.EditorColorKeyword);
        SubscribeColor(SettingKeys.EditorColorString);
        SubscribeColor(SettingKeys.EditorColorNumber);
        SubscribeColor(SettingKeys.EditorColorComment);
        SubscribeColor(SettingKeys.EditorColorOperator);
        SubscribeColor(SettingKeys.EditorColorFunction);
        SubscribeColor(SettingKeys.EditorColorType);
        SubscribeColor(SettingKeys.EditorColorBuiltin);
        SubscribeColor(SettingKeys.EditorColorGlobal);
        SubscribeColor(SettingKeys.EditorColorGhostText);
        SubscribeColor(SettingKeys.EditorColorText);
        SubscribeColor(SettingKeys.EditorColorBracket);
        SubscribeColor(SettingKeys.EditorColorTodo);
        SubscribeColor(SettingKeys.EditorColorLocalMethod);
        SubscribeColor(SettingKeys.EditorColorLocalProperty);

        Subscribe<bool>(SettingKeys.EditorSemanticHighlighting, RedrawAll);

        Engine.Globals.Get<int>(SettingKeys.EditorTabWidth)!.Changed += width =>
            BlockStructureService.TabWidth = width;
        BlockStructureService.TabWidth = Engine.Globals.Get<int>(SettingKeys.EditorTabWidth)!.Value;
    }

    public void Register(TextEditor editor)
    {
        _editors.Add(editor);
        Apply(editor);
    }

    public void Unregister(TextEditor editor) => _editors.Remove(editor);

    private void Subscribe<T>(string key, Action handler)
    {
        var global = Engine.Globals.Get<T>(key);
        if (global is not null)
        {
            global.Changed += _ => handler();
        }
    }

    private void SubscribeColor(string key)
    {
        var global = Engine.Globals.Get<string>(key);
        if (global is not null)
        {
            global.Changed += _ =>
            {
                HighlightBrushes.Invalidate();
                RedrawAll();
            };
        }
    }

    private void ApplyToAllEditors()
    {
        foreach (var editor in _editors.ToArray())
        {
            Apply(editor);
        }
    }

    private void RedrawAll()
    {
        foreach (var editor in _editors.ToArray())
        {
            editor.TextArea.TextView.Redraw();
        }
    }

    public static void Apply(TextEditor editor)
    {
        var fontFamily = Engine.Globals.Get<string>(SettingKeys.EditorFontFamily)!.Value;
        var fontSize = Engine.Globals.Get<double>(SettingKeys.EditorFontSize)!.Value;
        var bold = Engine.Globals.Get<bool>(SettingKeys.EditorFontBold)!.Value;
        var italic = Engine.Globals.Get<bool>(SettingKeys.EditorFontItalic)!.Value;
        var tabWidth = Engine.Globals.Get<int>(SettingKeys.EditorTabWidth)!.Value;
        var showLineNumbers = Engine.Globals.Get<bool>(SettingKeys.EditorShowLineNumbers)!.Value;
        var wordWrap = Engine.Globals.Get<bool>(SettingKeys.EditorWordWrap)!.Value;
        var convertTabs = Engine.Globals.Get<bool>(SettingKeys.EditorConvertTabsToSpaces)!.Value;
        var foreground = ColorFromRgb(SettingColorParser.ParseRgb(Engine.Globals.Get<string>(SettingKeys.EditorForeground)!.Value, 0xBCBEC8));
        var background = ColorFromRgb(SettingColorParser.ParseRgb(Engine.Globals.Get<string>(SettingKeys.EditorBackground)!.Value, 0x0E0F11));

        editor.FontFamily = new FontFamily(fontFamily);
        editor.FontSize = fontSize;
        editor.FontWeight = bold ? FontWeights.Bold : FontWeights.Normal;
        editor.FontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
        editor.ShowLineNumbers = showLineNumbers;
        editor.WordWrap = wordWrap;

        editor.Options.IndentationSize = tabWidth;
        editor.Options.ConvertTabsToSpaces = convertTabs;

        editor.Background = new SolidColorBrush(background);
        editor.Foreground = new SolidColorBrush(foreground);

        foreach (var margin in editor.TextArea.LeftMargins)
        {
            if (margin is LineNumberMargin lineNumbers)
            {
                lineNumbers.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x9A, 0x9D, 0xA5)));
            }
        }

        editor.TextArea.TextView.LinkTextForegroundBrush = new SolidColorBrush(Color.FromRgb(0x35, 0x74, 0xF0));
        HideEditorScrollBars(editor);
    }

    private static void HideEditorScrollBars(DependencyObject root)
    {
        var hiddenStyle = Application.Current?.TryFindResource("HiddenScrollBar") as Style;
        if (hiddenStyle is null)
        {
            return;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollBar scrollBar)
            {
                scrollBar.Style = hiddenStyle;
            }

            HideEditorScrollBars(child);
        }
    }

    private static Color ColorFromRgb(uint rgb) =>
        Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));
}
