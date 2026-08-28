using System.Windows;
using System.Windows.Controls;

namespace LUAstudio;

internal static class ShellInfoWindow
{
    public static void ShowAbout(Window owner, string version) => Show(owner, "About LuaStudio",
        $"LuaStudio {version}\n\nA focused Lua and Luau development environment.\n\nBuilt with .NET, WPF, AvalonEdit, AvalonDock, and Luau.");

    public static void ShowShortcuts(Window owner) => Show(owner, "Keyboard Shortcuts", """
        File
          Ctrl+N          New file
          Ctrl+O          Open file
          Ctrl+S          Save
          Ctrl+Shift+S    Save as
          Ctrl+W          Close file

        Editor
          Ctrl+Z / Ctrl+Y Undo / redo
          Ctrl+X/C/V      Cut / copy / paste
          Ctrl+A          Select all
          Ctrl+F          Find
          Ctrl+Space      Show completions

        Debug
          F5              Run / continue
          Shift+F5        Stop
          F6              Pause
          F9              Toggle breakpoint
          F10             Step over
          F11             Step into
          Shift+F11       Step out
        """);

    private static void Show(Window owner, string title, string text)
    {
        var content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(22) };
        new Window
        {
            Owner = owner, Title = title, Content = content, Width = 480, Height = 430,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize
        }.ShowDialog();
    }
}
