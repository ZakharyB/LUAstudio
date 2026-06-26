using LUAstudio.IntelliSense.Events;
using LUAstudio.IntelliSense.Semantic;
using LUAstudio.Core.Events;
using LUAstudio.Languages.Parsing;
using LUAstudio.Languages.Text;

namespace LUAstudio.IntelliSense.Workspace;

public sealed class RequireGraphWorkspaceScanner
{
    private readonly ILuaParser _parser;
    private readonly SemanticBinder _binder;
    private readonly IModuleResolver _moduleResolver;
    private readonly RequireGraphService _requireGraph;
    private readonly IEventBus _eventBus;

    public RequireGraphWorkspaceScanner(
        ILuaParser parser,
        SemanticBinder binder,
        IModuleResolver moduleResolver,
        RequireGraphService requireGraph,
        IEventBus eventBus)
    {
        _parser = parser;
        _binder = binder;
        _moduleResolver = moduleResolver;
        _requireGraph = requireGraph;
        _eventBus = eventBus;
    }

    public async Task ScanAsync(IEnumerable<string> workspaceRoots, CancellationToken cancellationToken = default)
    {
        var roots = workspaceRoots
            .Select(Path.GetFullPath)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _requireGraph.Clear();

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*.lua", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(root, "*.luau", SearchOption.AllDirectories));
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    var dialect = file.EndsWith(".luau", StringComparison.OrdinalIgnoreCase)
                        ? LuaDialect.Luau
                        : LuaDialect.Lua;
                    var snapshot = new SourceSnapshot(Guid.NewGuid(), 1, SourceText.From(text), file, dialect);
                    var parseResult = await _parser.ParseDocumentAsync(snapshot, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    var binding = _binder.Bind(parseResult);
                    RecordFileRequires(file, binding);
                }
                catch
                {
                    // Skip unreadable or unparseable files during graph scan.
                }
            }
        }

        _eventBus.Publish(new RequireGraphUpdatedEvent());
    }

    public void RecordFileRequires(string filePath, SemanticBindingResult binding)
    {
        var edges = binding.RequireEdges
            .Select(edge =>
            {
                var resolved = _moduleResolver.ResolveModule(edge.ModulePath, filePath);
                return (edge.ModulePath, resolved?.ContainingFilePath);
            })
            .ToArray();

        _requireGraph.SetFileRequires(filePath, edges);
    }

    public void PublishUpdated() => _eventBus.Publish(new RequireGraphUpdatedEvent());
}
