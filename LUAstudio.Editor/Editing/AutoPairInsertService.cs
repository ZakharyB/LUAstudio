using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using LUAstudio.Abstractions;
using LUAstudio.Core;

namespace LUAstudio.Editor.Editing;

public sealed class AutoPairInsertService
{
    private TextEditor? _editor;

    public void Attach(TextEditor editor)
    {
        Detach();
        _editor = editor;
        editor.TextArea.TextEntering += OnTextEntering;
    }

    public void Detach()
    {
        if (_editor is not null)
        {
            _editor.TextArea.TextEntering -= OnTextEntering;
        }

        _editor = null;
    }

    private void OnTextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (_editor is null || string.IsNullOrEmpty(e.Text) ||
            Engine.Globals.Get<bool>(SettingKeys.EditorAutoPairBrackets)?.Value == false)
        {
            return;
        }

        var c = e.Text[0];
        var offset = _editor.CaretOffset;
        var doc = _editor.Document;

        switch (c)
        {
            case '(':
                e.Handled = true;
                doc.Insert(offset, "()");
                _editor.CaretOffset = offset + 1;
                break;

            case '{':
                e.Handled = true;
                doc.Insert(offset, "{}");
                _editor.CaretOffset = offset + 1;
                break;

            case '[':
                e.Handled = true;
                doc.Insert(offset, "[]");
                _editor.CaretOffset = offset + 1;
                break;

            case '"':
            case '\'':
                if (offset < doc.TextLength && doc.GetCharAt(offset) == c)
                {
                    e.Handled = true;
                    _editor.CaretOffset = offset + 1;
                }
                else
                {
                    e.Handled = true;
                    doc.Insert(offset, $"{c}{c}");
                    _editor.CaretOffset = offset + 1;
                }

                break;
        }
    }
}
