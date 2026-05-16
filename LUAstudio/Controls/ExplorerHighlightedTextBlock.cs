using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LUAstudio.Controls;

public sealed class ExplorerHighlightedTextBlock : TextBlock
{
    public static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.Register(nameof(SourceText), typeof(string), typeof(ExplorerHighlightedTextBlock),
            new PropertyMetadata(string.Empty, OnHighlightChanged));

    public static readonly DependencyProperty MatchIndexCsvProperty =
        DependencyProperty.Register(nameof(MatchIndexCsv), typeof(string), typeof(ExplorerHighlightedTextBlock),
            new PropertyMetadata(string.Empty, OnHighlightChanged));

    public static readonly DependencyProperty IsFilterMatchProperty =
        DependencyProperty.Register(nameof(IsFilterMatch), typeof(bool), typeof(ExplorerHighlightedTextBlock),
            new PropertyMetadata(false, OnHighlightChanged));

    public string SourceText
    {
        get => (string)GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    public string MatchIndexCsv
    {
        get => (string)GetValue(MatchIndexCsvProperty);
        set => SetValue(MatchIndexCsvProperty, value);
    }

    public bool IsFilterMatch
    {
        get => (bool)GetValue(IsFilterMatchProperty);
        set => SetValue(IsFilterMatchProperty, value);
    }

    private static void OnHighlightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ExplorerHighlightedTextBlock)d).RebuildInlines();
    }

    private void RebuildInlines()
    {
        var text = SourceText ?? string.Empty;
        var indices = ParseIndices(MatchIndexCsv);
        if (!IsFilterMatch || indices.Count == 0)
        {
            Inlines.Clear();
            Text = text;
            return;
        }

        Text = string.Empty;
        Inlines.Clear();
        var accent = TryFindResource("BrushAccent") as Brush ?? Brushes.DodgerBlue;
        var indexSet = new HashSet<int>(indices);
        for (var i = 0; i < text.Length; i++)
        {
            var run = new Run(text[i].ToString());
            if (indexSet.Contains(i))
            {
                run.Background = accent;
                run.Foreground = Brushes.White;
                run.FontWeight = FontWeights.SemiBold;
            }

            Inlines.Add(run);
        }
    }

    private static List<int> ParseIndices(string? csv)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return result;
        }

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var index))
            {
                result.Add(index);
            }
        }

        return result;
    }
}
