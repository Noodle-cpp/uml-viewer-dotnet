using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class PolicyEdges
{
    private static readonly Dictionary<string, int> KindRank = new(StringComparer.Ordinal)
    {
        ["implements"] = 4,
        ["inheritance"] = 4,
        ["composition"] = 3,
        ["aggregation"] = 2,
        ["association"] = 1,
        ["dependency"] = 0,
    };

    public static int KindRankOf(string kind) =>
        KindRank.GetValueOrDefault(kind, 0);

    public static List<DiagramEdge> MergeEdges(IEnumerable<DiagramEdge> edges)
    {
        return edges
            .GroupBy(e => (e.From, e.To))
            .Select(g =>
            {
                var es = g.ToList();
                var best = es.MaxBy(e => KindRankOf(e.Kind))!;
                best = Clone(best);
                if (best.Kind == "dependency" && es.Any(x => x.Violating))
                    best.Violating = true;
                else if (best.Kind != "dependency")
                    best.Violating = false;
                return best;
            })
            .ToList();
    }

    public static List<DiagramEdge> ApplyEdgeKinds(IEnumerable<DiagramEdge> edges) =>
        ApplyEdgeKinds(edges, null, null);

    public static List<DiagramEdge> ApplyEdgeKinds(
        IEnumerable<DiagramEdge> edges,
        IReadOnlyDictionary<string, string>? kinds,
        IReadOnlyList<IrEdgeEnd>? omitEdges)
    {
        var omit = omitEdges == null
            ? new HashSet<(string, string)>()
            : omitEdges.Select(o => (o.From, o.To)).ToHashSet();
        return edges
            .Where(e => !omit.Contains((e.From, e.To)))
            .Select(e =>
            {
                var copy = Clone(e);
                var key = EdgeKindKey(copy.From, copy.To);
                if (kinds != null && kinds.TryGetValue(key, out var k))
                    copy.Kind = k;
                if (copy.Kind != "dependency")
                    copy.Violating = false;
                return copy;
            })
            .ToList();
    }

    public static string EdgeKindKey(string from, string to) => from + "\0" + to;

    private static DiagramEdge Clone(DiagramEdge e) => new()
    {
        From = e.From,
        To = e.To,
        Kind = e.Kind,
        Violating = e.Violating,
        OrigFrom = e.OrigFrom,
        OrigTo = e.OrigTo,
        Deps = e.Deps.ToList(),
    };
}
