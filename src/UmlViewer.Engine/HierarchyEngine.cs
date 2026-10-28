using UmlViewer.Core.Metrics;
using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class HierarchyEngine
{
    public static ViewDiagram ViewAt(IrDocument doc, IReadOnlyList<string> path)
    {
        var classes = doc.Classes.Where(c => !c.Foreign).ToList();
        var idx = classes.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var nodeIds = SortIds(NodesAt(classes, path), []);
        var boxes = nodeIds.Select(id => ViewClass(idx, classes, path, id)).ToList();
        var ids = boxes.Select(b => b.Id).ToHashSet(StringComparer.Ordinal);
        var parts = PartitionEdges(doc.Edges, ids, path, idx);
        var foreign = parts.Foreign;
        var visible = ids.Union(foreign.Select(f => f.Id)).ToHashSet(StringComparer.Ordinal);
        var edges = PolicyEdges.ApplyEdgeKinds(
            PolicyEdges.MergeEdges(parts.Internal.Concat(parts.ForeignEdges)));
        edges = edges.Where(e => visible.Contains(e.From) && visible.Contains(e.To)).ToList();
        foreach (var box in boxes)
        {
            if (parts.In.TryGetValue(box.Id, out var ins))
                box.InDeps = ins;
            if (parts.Out.TryGetValue(box.Id, out var outs))
                box.OutDeps = outs;
        }

        return new ViewDiagram
        {
            Title = LabelAt(doc, path),
            Packages =
            [
                new DiagramPackage
                {
                    Id = "view",
                    Label = LabelAt(doc, path),
                    Classes = boxes,
                },
            ],
            Edges = edges,
            Foreign = foreign,
        };
    }

    private static string LabelAt(IrDocument doc, IReadOnlyList<string> path) =>
        path.Count > 0 ? string.Join('.', path) : (doc.Title.Length > 0 ? doc.Title : "UML");

    private static List<string> SortIds(List<string> ids, IReadOnlyList<string> order)
    {
        var rank = order.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i, StringComparer.Ordinal);
        return ids.OrderBy(id => rank.GetValueOrDefault(id, order.Count)).ThenBy(id => id, StringComparer.Ordinal).ToList();
    }

    private static List<string> NodesAt(List<IrClass> classes, IReadOnlyList<string> path) =>
        classes
            .Select(c => ChildId(c.Id, path))
            .Where(id => id != null)
            .Distinct(StringComparer.Ordinal)
            .Select(id => id!)
            .ToList();

    private static string? ChildId(string id, IReadOnlyList<string> path)
    {
        if (!Under(id, path))
            return null;
        var segs = Segs(id);
        var n = path.Count;
        if (segs.Length == n)
            return id;
        return JoinSegs(segs.Take(n + 1));
    }

    private static bool Under(string id, IReadOnlyList<string> path)
    {
        var s = Segs(id);
        var p = path.ToArray();
        if (s.Length == p.Length && s.SequenceEqual(p, StringComparer.Ordinal))
            return true;
        if (p.Length > s.Length)
            return false;
        return p.SequenceEqual(s.Take(p.Length), StringComparer.Ordinal);
    }

    private static string[] Segs(string id) => id.Split('.');

    private static string JoinSegs(IEnumerable<string> parts) => string.Join('.', parts);

    private static DiagramClass ViewClass(
        Dictionary<string, IrClass> idx,
        List<IrClass> classes,
        IReadOnlyList<string> path,
        string id)
    {
        idx.TryGetValue(id, out var leaf);
        leaf ??= new IrClass { Id = id, Name = NodeLabel(id) };
        var kids = ContentsOf(classes, path, id);
        var drill = kids.Count > 0;
        var hide = drill;
        var crap = drill ? RolledCrap(classes, id) : OwnCrap(leaf);
        var mut = drill ? RolledMutants(classes, id) : OwnMutants(leaf);
        return new DiagramClass
        {
            Id = id,
            Name = BoxName(leaf, id, drill),
            Ns = leaf.Ns,
            File = leaf.File,
            Lang = leaf.Lang,
            Stereotype = leaf.Stereotype,
            Drill = drill,
            HideMembers = hide,
            CrapSummary = crap,
            Coverage = leaf.Coverage,
            Killed = mut.killed,
            Survived = mut.survived,
            Ops = hide ? [] : leaf.Ops.Where(o => o.Private != true).ToList(),
            Contents = hide ? kids : [],
        };
    }

    private static List<DiagramChild> ContentsOf(List<IrClass> classes, IReadOnlyList<string> path, string nodeId)
    {
        var nextPath = path.Concat([LastSeg(nodeId)]).ToList();
        var kids = NodesAt(classes, nextPath).Where(k => k != nodeId).ToList();
        if (kids.Count == 0)
            return [];
        var idx = classes.ToDictionary(c => c.Id, StringComparer.Ordinal);
        return kids.Select(cid =>
        {
            idx.TryGetValue(cid, out var leaf);
            var directory = cid != nodeId && HasDescendants(cid, classes);
            return new DiagramChild
            {
                Id = cid,
                Name = BoxName(leaf ?? new IrClass { Id = cid }, cid, directory),
                Drill = directory,
            };
        }).ToList();
    }

    private static bool HasDescendants(string id, List<IrClass> classes)
    {
        var pfx = id + ".";
        return classes.Any(c => c.Id.StartsWith(pfx, StringComparison.Ordinal));
    }

    private static string LastSeg(string id) => Segs(id)[^1];

    private static string NodeLabel(string id)
    {
        var part = LastSeg(id);
        if (part.Length == 0)
            return part;
        if (part.Any(char.IsUpper))
            return char.ToUpperInvariant(part[0]) + part[1..];
        return char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part[1..] : "");
    }

    private static string BoxName(IrClass leaf, string id, bool directory)
    {
        if (directory)
            return NodeLabel(id);
        if (!string.IsNullOrEmpty(leaf.Name))
            return leaf.Name;
        return NodeLabel(id);
    }

    private static IrCrapSummary? OwnCrap(IrClass leaf) => leaf.CrapSummary;

    private static (int? killed, int? survived) OwnMutants(IrClass leaf) =>
        (leaf.Killed, leaf.Survived);

    private static IrCrapSummary? RolledCrap(List<IrClass> classes, string id)
    {
        var scores = classes.Where(c => UnderId(c, id))
            .SelectMany(c => c.Ops.Select(o => o.Crap ?? c.CrapSummary?.Mu ?? MetricsConfig.CrapGrade(null)))
            .Where(x => x > 0)
            .ToList();
        if (scores.Count == 0)
        {
            var n = classes.Count(c => UnderId(c, id));
            if (n == 0)
                return null;
            return MetricsConfig.SummarizeScores(Enumerable.Repeat(20.0, n));
        }

        return MetricsConfig.SummarizeScores(scores);
    }

    private static (int? killed, int? survived) RolledMutants(List<IrClass> classes, string id)
    {
        var subset = classes.Where(c => UnderId(c, id)).ToList();
        var k = subset.Sum(c => c.Killed ?? 0);
        var s = subset.Sum(c => c.Survived ?? 0);
        return (k > 0 ? k : null, s > 0 ? s : null);
    }

    private static bool UnderId(IrClass c, string id)
    {
        if (c.Id == id)
            return true;
        return c.Id.StartsWith(id + ".", StringComparison.Ordinal);
    }

    private sealed class EdgeParts
    {
        public List<DiagramEdge> Internal { get; } = [];
        public Dictionary<string, List<PortDep>> In { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<PortDep>> Out { get; } = new(StringComparer.Ordinal);
        public List<DiagramClass> Foreign { get; } = [];
        public List<DiagramEdge> ForeignEdges { get; } = [];
    }

    private static EdgeParts PartitionEdges(
        List<IrEdge> edges,
        HashSet<string> ids,
        IReadOnlyList<string> path,
        Dictionary<string, IrClass> idx)
    {
        var acc = new EdgeParts();
        foreach (var e in edges)
        {
            var de = new DiagramEdge { From = e.From, To = e.To, Kind = e.Kind };
            var ends = ComputeEdgeEnds(de, ids, path, idx);
            RouteEdge(acc, de, ends);
        }

        return acc;
    }

    private sealed record EdgeEndInfo(
        string? From,
        string? ToIn,
        PortDep? ToExt,
        PortDep? FromExt,
        bool FromHere,
        bool ToHere);

    private static EdgeEndInfo ComputeEdgeEnds(DiagramEdge e, HashSet<string> ids, IReadOnlyList<string> path, Dictionary<string, IrClass> idx)
    {
        var from = CollapseEnd(e.From, path, idx);
        var toIn = CollapseEnd(e.To, path, idx);
        return new EdgeEndInfo(
            from,
            toIn,
            InView(toIn, ids) ? null : ExternalEnd(e.To, path, idx),
            InView(from, ids) ? null : ExternalEnd(e.From, path, idx),
            InView(from, ids),
            InView(toIn, ids));
    }

    private static bool InView(string? id, HashSet<string> ids) =>
        id != null && ids.Contains(id);

    private static string? CollapseEnd(string id, IReadOnlyList<string> path, Dictionary<string, IrClass> idx)
    {
        if (idx.TryGetValue(id, out var c) && c.Foreign)
            return id;
        return ChildId(id, path);
    }

    private static PortDep? ExternalEnd(string id, IReadOnlyList<string> path, Dictionary<string, IrClass> idx)
    {
        if (idx.TryGetValue(id, out var c) && c.Foreign)
            return new PortDep { Id = id, Name = c.Name };
        if (Under(id, path))
            return null;
        var ext = JoinSegs(Segs(id).Take(Math.Max(1, path.Count)));
        return new PortDep { Id = ext, Name = NodeLabel(ext) };
    }

    private static void RouteEdge(EdgeParts acc, DiagramEdge e, EdgeEndInfo ends)
    {
        if (ends.FromHere && ends.ToHere && ends.From != ends.ToIn)
        {
            acc.Internal.Add(new DiagramEdge
            {
                From = ends.From!,
                To = ends.ToIn!,
                Kind = e.Kind,
            });
            return;
        }

        if (ends.FromHere && ends.ToExt != null && idxForeign(acc, ends.ToExt.Id))
        {
            AddForeign(acc, ends.From!, ends.ToExt, e);
            return;
        }

        if (ends.FromHere && ends.ToExt != null)
        {
            AddPort(acc.Out, ends.From!, ends.ToExt);
            return;
        }

        if (ends.ToHere && ends.FromExt != null && idxForeign(acc, ends.FromExt.Id))
        {
            var edge = new DiagramEdge { From = ends.FromExt.Id, To = ends.ToIn!, Kind = e.Kind };
            AddForeign(acc, ends.ToIn!, ends.FromExt, edge);
            return;
        }

        if (ends.ToHere && ends.FromExt != null)
            AddPort(acc.In, ends.ToIn!, ends.FromExt);
    }

    private static bool idxForeign(EdgeParts acc, string id) =>
        acc.Foreign.Any(f => f.Id == id);

    private static void AddPort(Dictionary<string, List<PortDep>> map, string boxId, PortDep dep)
    {
        if (!map.TryGetValue(boxId, out var list))
        {
            list = [];
            map[boxId] = list;
        }

        if (list.All(d => d.Id != dep.Id))
            list.Add(dep);
    }

    private static void AddForeign(EdgeParts acc, string boxId, PortDep dep, DiagramEdge e)
    {
        if (acc.Foreign.All(f => f.Id != dep.Id))
        {
            acc.Foreign.Add(new DiagramClass
            {
                Id = dep.Id,
                Name = dep.Name,
                Foreign = true,
                Shape = "oval",
            });
        }

        acc.ForeignEdges.Add(new DiagramEdge { From = boxId, To = dep.Id, Kind = e.Kind });
    }
}
