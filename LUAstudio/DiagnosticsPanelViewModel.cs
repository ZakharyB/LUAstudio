using System.Collections.ObjectModel;
using System.Windows;
using LUAstudio.Core.Events;
using LUAstudio.IntelliSense.Events;
using LUAstudio.IntelliSense.Workspace;

namespace LUAstudio;

public sealed class DiagnosticsPanelViewModel
{
    private readonly RequireGraphService _requireGraph;

    public DiagnosticsPanelViewModel(IEventBus eventBus, RequireGraphService requireGraph)
    {
        _requireGraph = requireGraph;
        eventBus.Subscribe<DocumentAnalyzedEvent>(OnAnalyzed);
        eventBus.Subscribe<RequireGraphUpdatedEvent>(_ => RefreshOnUiThread());
    }

    public ObservableCollection<DiagnosticListItem> Problems { get; } = new();

    public ObservableCollection<string> RequireGraphLines { get; } = new();

    private void OnAnalyzed(DocumentAnalyzedEvent e)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            Problems.Clear();
            foreach (var d in e.Result.SemanticModel.Diagnostics)
            {
                Problems.Add(new DiagnosticListItem(d.Code, d.Message, d.Severity.ToString(), d.FixSuggestion));
            }

            foreach (var p in e.Result.ParseResult.Tree.Diagnostics)
            {
                Problems.Add(new DiagnosticListItem(p.Code, p.Message, p.Severity.ToString(), null));
            }

            RefreshRequireGraph();
        });
    }

    public void RefreshRequireGraph()
    {
        RequireGraphLines.Clear();

        var edges = _requireGraph.GetEdges();
        if (edges.Count == 0)
        {
            RequireGraphLines.Add("(no require dependencies found in workspace)");
            return;
        }

        foreach (var edge in edges)
        {
            var marker = edge.IsCircular ? " [circular]" : string.Empty;
            RequireGraphLines.Add($"{edge.FromModule} → {edge.ToModule}{marker}");
        }

        foreach (var node in _requireGraph.GetNodes().Where(n => n.IsDead))
        {
            RequireGraphLines.Add($"dead module: {node.ModulePath}");
        }
    }

    private void RefreshOnUiThread()
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            RefreshRequireGraph();
            return;
        }

        Application.Current?.Dispatcher.Invoke(RefreshRequireGraph);
    }
}

public sealed record DiagnosticListItem(string Code, string Message, string Severity, string? FixSuggestion);
