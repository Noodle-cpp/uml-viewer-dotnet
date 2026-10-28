using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class PolicyRanks
{
    public static Dictionary<string, int> RanksFromLayers(IReadOnlyList<IrProposalLayer> layers)
    {
        var groups = layers
            .Select(layer => PolicyNse.NseIds(layer.Nses))
            .ToList();
        return LevelRanks(groups);
    }

    public static Dictionary<string, int> LevelRanks(IReadOnlyList<IReadOnlyList<string>> groups)
    {
        var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var rank = 0; rank < groups.Count; rank++)
        {
            foreach (var seg in groups[rank])
            {
                var id = NormalizeId(seg);
                if (id.Length > 0)
                    ranks[id] = rank;
            }
        }

        return ranks;
    }

    public static int? RankOf(string id, IReadOnlyDictionary<string, int> ranks)
    {
        id = NormalizeId(id);
        var parts = id.Split('.');
        for (var n = parts.Length; n > 0; n--)
        {
            var prefix = string.Join('.', parts.Take(n));
            if (ranks.TryGetValue(prefix, out var r))
                return r;
        }

        return null;
    }

    public static List<IrClass> WithLevels(IEnumerable<IrClass> classes, IReadOnlyDictionary<string, int> ranks) =>
        classes.Select(c =>
        {
            if (!c.Foreign)
                c.Level = RankOf(c.Id, ranks);
            return c;
        }).ToList();

    public static (List<IrClass> Classes, List<DiagramEdge> Edges) RestampRanks(
        IReadOnlyList<IrClass> classes,
        IReadOnlyList<DiagramEdge> edges,
        IReadOnlyDictionary<string, int> ranks)
    {
        var leveled = WithLevels(classes, ranks);
        var stamped = MarkViolations(edges, ranks);
        return (leveled, stamped);
    }

    public static List<DiagramEdge> MarkViolations(IReadOnlyList<DiagramEdge> edges, IReadOnlyDictionary<string, int> ranks)
    {
        return edges.Select(e =>
        {
            if (e.Kind != "dependency")
            {
                e.Violating = false;
                return e;
            }

            var rf = RankOf(e.From, ranks);
            var rt = RankOf(e.To, ranks);
            e.Violating = rf != null && rt != null && rf < rt;
            return e;
        }).ToList();
    }

    private static string NormalizeId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return id;
        return id.StartsWith(':') ? id[1..] : id;
    }
}
