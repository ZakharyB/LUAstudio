using LUAstudio.IDE.Documents;

namespace LUAstudio.IDE.Events;

public sealed record DocumentOpenedEvent(IDocument Document);

public sealed record DocumentClosedEvent(IDocument Document);

public sealed record ActiveDocumentChangedEvent(IDocument? Document);
