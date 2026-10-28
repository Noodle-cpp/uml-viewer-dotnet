using System.Globalization;
using UmlViewer.Core.Metrics;
using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static partial class DetailEngine
{
    public sealed class DetailRow
    {
        public string Kind { get; set; } = "";
        public string Text { get; set; } = "";
        public string? Crap { get; set; }
        public string? Cc { get; set; }
        public string? Cov { get; set; }
        public string? Killed { get; set; }
        public string? Survived { get; set; }
        public string? Uncovered { get; set; }
        public string? MutNote { get; set; }
        public string? OpName { get; set; }
        public bool Private { get; set; }
        public bool IsHeader { get; set; }
    }

    public static IReadOnlyList<DetailRow> BuildRows(DetailModel model)
    {
        var rows = new List<DetailRow>
        {
            new() { Kind = "name", Text = model.ClassName },
            new() { Kind = "muted", Text = model.Ns },
        };
        if (model.CrapSummary != null)
        {
            var c = model.CrapSummary;
            rows.Add(new DetailRow
            {
                Kind = "crap",
                Text = $"Crap μ {c.Mu:F1}  max {c.Max:F1}  σ {c.Sigma:F1}",
            });
        }

        rows.Add(new DetailRow { Kind = "heading", Text = "Relationships", IsHeader = true });
        foreach (var r in model.Rels)
        {
            rows.Add(new DetailRow
            {
                Kind = "rel",
                Text = $"{r.Phrase}  {r.Name} ({r.Kind})",
            });
        }

        rows.Add(new DetailRow { Kind = "heading", Text = "Operations", IsHeader = true });
        rows.Add(new DetailRow { Kind = "col-header", Text = model.ClassName, IsHeader = true });
        var classCells = FormatCells(model.ClassMetrics);
        rows.Add(new DetailRow
        {
            Kind = "stats",
            Text = model.ClassName,
            Crap = classCells.Crap,
            Cc = classCells.Cc,
            Cov = classCells.Cov,
            Killed = classCells.Killed,
            Survived = classCells.Survived,
            Uncovered = classCells.Uncovered,
            MutNote = classCells.MutNote,
        });
        foreach (var op in model.Ops)
        {
            var cells = FormatCells(op);
            rows.Add(new DetailRow
            {
                Kind = "stats",
                Text = (op.Private ? "- " : "+ ") + op.Name,
                OpName = op.Name,
                Private = op.Private,
                Crap = cells.Crap,
                Cc = cells.Cc,
                Cov = cells.Cov,
                Killed = cells.Killed,
                Survived = cells.Survived,
                Uncovered = cells.Uncovered,
                MutNote = cells.MutNote,
            });
        }

        return rows;
    }

    private sealed class MetricCells
    {
        public string? Crap { get; init; }
        public string? Cc { get; init; }
        public string? Cov { get; init; }
        public string? Killed { get; init; }
        public string? Survived { get; init; }
        public string? Uncovered { get; init; }
        public string? MutNote { get; init; }
    }

    private static MetricCells FormatCells(OpRow op)
    {
        var noSites = (op.Killed ?? 0) + (op.Survived ?? 0) + (op.Uncovered ?? 0) == 0;
        return new MetricCells
        {
            Crap = op.Crap.HasValue ? op.Crap.Value.ToString("F1", CultureInfo.InvariantCulture) : null,
            Cc = op.Cc.HasValue ? ((long)op.Cc.Value).ToString(CultureInfo.InvariantCulture) : null,
            Cov = MetricsDisplay.FormatCoverage(op.Coverage),
            Killed = noSites ? null : op.Killed?.ToString(CultureInfo.InvariantCulture),
            Survived = noSites ? null : op.Survived?.ToString(CultureInfo.InvariantCulture),
            Uncovered = noSites ? null : op.Uncovered?.ToString(CultureInfo.InvariantCulture),
            MutNote = noSites ? "---no mutation sites---" : null,
        };
    }

    private static MetricCells FormatCells(ClassMetricsRow row)
    {
        var noSites = row.Killed + row.Survived + row.Uncovered == 0;
        return new MetricCells
        {
            Crap = row.CrapMu.HasValue ? row.CrapMu.Value.ToString("F1", CultureInfo.InvariantCulture) + "μ" : null,
            Cov = MetricsDisplay.FormatCoverage(row.Coverage),
            Killed = noSites ? null : row.Killed.ToString(CultureInfo.InvariantCulture),
            Survived = noSites ? null : row.Survived.ToString(CultureInfo.InvariantCulture),
            Uncovered = noSites ? null : row.Uncovered.ToString(CultureInfo.InvariantCulture),
            MutNote = noSites ? "---no mutation sites---" : null,
        };
    }
}

public static class MetricsDisplay
{
    public static string? FormatCoverage(double? coverage)
    {
        if (coverage == null)
            return null;
        return (coverage.Value * 100).ToString("F0", CultureInfo.InvariantCulture) + "%";
    }
}
