using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace LUAstudio.Editor.Highlighting;

public sealed class SyntaxHighlightingService
{
    private IHighlightingDefinition? _definition;

    public void Apply(TextEditor editor)
    {
        editor.SyntaxHighlighting = GetDefinition();
    }

    public IHighlightingDefinition GetDefinition()
    {
        if (_definition is not null)
        {
            return _definition;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Editor", "Lua.xshd");
        if (File.Exists(path))
        {
            using var reader = XmlReader.Create(path);
            _definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            return _definition;
        }

        _definition = HighlightingManager.Instance.GetDefinition("Lua")
            ?? HighlightingManager.Instance.GetDefinition("JavaScript")
            ?? HighlightingManager.Instance.HighlightingDefinitions.First();
        return _definition;
    }
}
