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
        Text = item.InsertText;
        Content = item.DisplayText;
        Description = string.IsNullOrWhiteSpace(item.Documentation) ? item.Detail : item.Documentation;
    }

    public CompletionItem Item { get; }

    public ImageSource? Image => null;

    public string Text { get; }

    public object Content { get; }

    public object Description { get; }

    public double Priority => Item.Priority;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}
