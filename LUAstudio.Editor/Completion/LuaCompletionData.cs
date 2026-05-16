using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using LUAstudio.IntelliSense.Completion;

namespace LUAstudio.Editor.Completion;

public sealed class LuaCompletionData : ICompletionData
{
    public LuaCompletionData(CompletionItem item)
    {
        Item = item;
        Text = SnippetEngine.ContainsPlaceholders(item.InsertText)
            ? SnippetEngine.Expand(item.InsertText).Text
            : item.InsertText;
        Content = item.DisplayText;
        Description = string.IsNullOrWhiteSpace(item.Documentation)
            ? item.Detail ?? string.Empty
            : item.Documentation;
    }

    public CompletionItem Item { get; }

    public ImageSource? Image => null;

    public string Text { get; }

    public object Content { get; }

    public object Description { get; }

    public double Priority => Item.Priority;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        if (SnippetEngine.ContainsPlaceholders(Item.InsertText))
        {
            var expansion = SnippetEngine.Expand(Item.InsertText);
            textArea.Document.Replace(completionSegment, expansion.Text);
            if (expansion.Placeholders.Count > 0)
            {
                _ = new SnippetSession(textArea, expansion);
            }
        }
        else
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
