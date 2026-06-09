namespace LUAstudio.IntelliSense.Completion;

public interface ICompletionProvider
{
    string Name { get; }

    Task<IReadOnlyList<CompletionItem>> GetCompletionsAsync(
        CompletionContext context,
        CancellationToken cancellationToken = default);
}
