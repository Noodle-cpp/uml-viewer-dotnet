using UmlViewer.Core.Metrics;
using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class ProposalHierarchy
{
    public const string UnassignedPackageId = "proposal.unassigned";

    public static bool ProposalPackageId(string? id) =>
        id != null && id.StartsWith("proposal.", StringComparison.Ordinal);

    public static ViewDiagram ProposalView(IrDocument doc, string? proposalId)
    {
        var root = HierarchyEngine.ViewAt(doc, []);
        var named = ResolveWhich(doc, proposalId);
        var normalized = ChosenProposal(doc, named);
        if (normalized == null || normalized.Layers.Count == 0)
        {
            var notice = named?.Notice ?? PolicyProposals.ProposalNotice;
            root.Title = notice;
            return root;
        }

        var ranks = PolicyRanks.RanksFromLayers(normalized.Layers);
        var omit = MergedOmit(doc, normalized);
        var leaves = VisibleLeaves(doc, omit);
        var (stamped, _) = PolicyRanks.RestampRanks(leaves, [], ranks);
        var byId = stamped.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var claimed = normalized.Layers
            .SelectMany(l => PolicyNse.NseIds(l.Nses))
            .ToHashSet(StringComparer.Ordinal);
        var extras = stamped.Where(c => SpareLeaf(claimed, omit, c)).ToList();
        var pkgs = ProposalPackages(byId, stamped, normalized.Layers, extras, doc.Classes);
        var notice2 = named?.Notice ?? normalized.Notice ?? PolicyProposals.ProposalNotice;
        var edges = ProposalEdges(doc, ranks, byId, pkgs, root, normalized.Layers);
        return AttachProposal(root, pkgs, edges, notice2);
    }

    public static ViewDiagram LayerView(IrDocument doc, string? proposalId, string pkgId)
    {
        var root = ProposalView(doc, proposalId);
        var pkg = root.Packages.FirstOrDefault(p => p.Id == pkgId);
        if (pkg != null)
        {
            root.Title = string.IsNullOrEmpty(pkg.Label) ? pkgId : pkg.Label;
            root.Packages = [pkg];
            return root;
        }

        var box = ClassInView(root, pkgId);
        if (box is { ProposalGroup: true, Members.Count: > 0 })
        {
            root.Title = box.Name;
            root.Packages =
            [
                new DiagramPackage
                {
                    Id = pkgId,
                    Label = box.Name,
                    Classes = box.Members.ToList(),
                },
            ];
            root.Edges = RestampVisibleEdges(doc, proposalId, root, box.Members);
            return root;
        }

        return root;
    }

    public static IrNseNode? FindNestedGroup(IrDocument doc, string? proposalId, string? groupId)
    {
        if (string.IsNullOrEmpty(groupId))
            return null;
        var normalized = ChosenProposal(doc, ResolveWhich(doc, proposalId));
        if (normalized == null)
            return null;
        return GroupsIn(normalized.Layers.SelectMany(l => l.Nses))
            .FirstOrDefault(g => string.Equals(g.Id, groupId, StringComparison.Ordinal));
    }

    private static ViewDiagram AttachProposal(
        ViewDiagram root,
        List<DiagramPackage> pkgs,
        List<DiagramEdge> edges,
        string notice)
    {
        if (pkgs.Count == 0)
            return root;
        root.Title = notice;
        root.Packages = pkgs;
        root.Edges = edges;
        return root;
    }

    private static List<DiagramEdge> ProposalEdges(
        IrDocument doc,
        IReadOnlyDictionary<string, int> ranks,
        IReadOnlyDictionary<string, IrClass> byId,
        IReadOnlyList<DiagramPackage> pkgs,
        ViewDiagram root,
        IReadOnlyList<IrProposalLayer> layers)
    {
        var visible = pkgs.SelectMany(p => p.Classes.Select(c => c.Id))
            .Concat(root.Foreign.Select(f => f.Id))
            .ToHashSet(StringComparer.Ordinal);
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var layer in layers)
        {
            foreach (var kv in PolicyNse.GroupOwners(layer.Nses))
                owners[kv.Key] = kv.Value;
        }
        var docEdges = doc.Edges.Select(IrEdgeToDiagram).ToList();
        var remapped = RemapProposalEdges(docEdges, visible, owners);
        remapped = PolicyEdges.MergeEdges(remapped);
        remapped = PolicyEdges.ApplyEdgeKinds(remapped, doc.EdgeKinds, doc.OmitEdges);
        var classes = byId.Values.ToList();
        var (_, edges) = PolicyRanks.RestampRanks(classes, remapped, ranks);
        return edges;
    }

    private static List<DiagramEdge> RestampVisibleEdges(
        IrDocument doc,
        string? proposalId,
        ViewDiagram view,
        IReadOnlyList<DiagramClass> classes)
    {
        var normalized = ChosenProposal(doc, ResolveWhich(doc, proposalId));
        var layers = normalized?.Layers ?? [];
        var ranks = PolicyRanks.RanksFromLayers(layers);
        var visible = classes.Select(c => c.Id)
            .Concat(view.Foreign.Select(f => f.Id))
            .ToHashSet(StringComparer.Ordinal);
        var docEdges = doc.Edges.Select(IrEdgeToDiagram).ToList();
        var remapped = RemapProposalEdges(docEdges, visible, new Dictionary<string, string>(StringComparer.Ordinal));
        remapped = PolicyEdges.MergeEdges(remapped);
        remapped = PolicyEdges.ApplyEdgeKinds(remapped, doc.EdgeKinds, doc.OmitEdges);
        var irClasses = classes.Select(DiagramToIrStub).ToList();
        var (_, edges) = PolicyRanks.RestampRanks(irClasses, remapped, ranks);
        return edges;
    }

    private static IrClass DiagramToIrStub(DiagramClass c) =>
        new() { Id = c.Id, Name = c.Name, Foreign = c.Foreign };

    private static List<DiagramPackage> ProposalPackages(
        IReadOnlyDictionary<string, IrClass> byId,
        IReadOnlyList<IrClass> stamped,
        IReadOnlyList<IrProposalLayer> layers,
        IReadOnlyList<IrClass> extras,
        IReadOnlyList<IrClass> allClasses)
    {
        var pkgs = layers
            .Select(layer => LayerPackage(byId, stamped, layer, allClasses))
            .Where(p => p != null)
            .Cast<DiagramPackage>()
            .ToList();
        if (extras.Count > 0)
        {
            pkgs.Add(new DiagramPackage
            {
                Id = UnassignedPackageId,
                Label = "Unassigned",
                Classes = extras.Select(c => AsModule(c, allClasses)).ToList(),
            });
        }

        pkgs.Reverse();
        return pkgs;
    }

    private static DiagramPackage? LayerPackage(
        IReadOnlyDictionary<string, IrClass> byId,
        IReadOnlyList<IrClass> stamped,
        IrProposalLayer layer,
        IReadOnlyList<IrClass> allClasses)
    {
        var classes = layer.Nses.SelectMany(n => PickEntries(byId, stamped, n, allClasses)).ToList();
        if (classes.Count == 0)
            return null;
        var layerId = string.IsNullOrEmpty(layer.Id) ? "layer" : layer.Id;
        return new DiagramPackage
        {
            Id = "proposal." + layerId,
            Label = layer.Label,
            Classes = classes,
        };
    }

    private static IEnumerable<DiagramClass> PickEntries(
        IReadOnlyDictionary<string, IrClass> byId,
        IReadOnlyList<IrClass> stamped,
        IrNseNode nse,
        IReadOnlyList<IrClass> allClasses)
    {
        if (nse.IsLeaf)
        {
            if (byId.TryGetValue(nse.LeafId!, out var c))
                yield return AsModule(c, allClasses);
            else
            {
                foreach (var c2 in stamped.Where(x => IdUnder(x.Id, nse.LeafId!)))
                    yield return AsModule(c2, allClasses);
            }

            yield break;
        }

        var kids = nse.Children.SelectMany(child => PickEntries(byId, stamped, child, allClasses)).ToList();
        if (kids.Count == 0)
            yield break;
        yield return NestedGroupBox(nse, kids);
    }

    private static DiagramClass NestedGroupBox(IrNseNode group, List<DiagramClass> kids)
    {
        var id = group.Id ?? "group";
        var crap = PoolCrap(kids);
        var (killed, survived) = PoolMutants(kids);
        var levels = kids.Where(c => c.Level != null).Select(c => c.Level!.Value).ToList();
        return new DiagramClass
        {
            Id = id,
            Name = string.IsNullOrEmpty(group.Label) ? NodeLabel(id) : group.Label,
            Drill = true,
            ProposalGroup = true,
            HideMembers = true,
            Members = kids,
            CrapSummary = crap,
            Killed = killed,
            Survived = survived,
            Level = levels.Count > 0 ? levels.Max() : null,
            Contents = kids.Select(c => new DiagramChild
            {
                Id = c.Id,
                Name = c.Name,
                Drill = c.Drill,
            }).ToList(),
        };
    }

    private static DiagramClass AsModule(IrClass c, IReadOnlyList<IrClass> allClasses)
    {
        var drill = HasDescendants(c.Id, allClasses);
        return new DiagramClass
        {
            Id = c.Id,
            Name = ModuleName(c),
            Ns = c.Ns,
            File = c.File,
            Lang = c.Lang,
            Stereotype = c.Stereotype,
            Drill = drill,
            HideMembers = true,
            Foreign = c.Foreign,
            CrapSummary = c.CrapSummary,
            Coverage = c.Coverage,
            Killed = c.Killed,
            Survived = c.Survived,
            Level = c.Level,
            Ops = drill ? [] : c.Ops.Where(o => o.Private != true).ToList(),
        };
    }

    private static List<DiagramEdge> RemapProposalEdges(
        IReadOnlyList<DiagramEdge> edges,
        IReadOnlySet<string> visible,
        IReadOnlyDictionary<string, string> owners)
    {
        var list = new List<DiagramEdge>();
        foreach (var e in edges)
        {
            var from = CoveringVisible(e.From, visible, owners);
            var to = CoveringVisible(e.To, visible, owners);
            if (from == null || to == null || from == to)
                continue;
            list.Add(new DiagramEdge
            {
                From = from,
                To = to,
                Kind = e.Kind,
                Violating = e.Violating,
            });
        }

        return list;
    }

    private static string? CoveringVisible(string id, IReadOnlySet<string> visible, IReadOnlyDictionary<string, string> owners)
    {
        if (visible.Contains(id))
            return id;
        if (owners.TryGetValue(id, out var g) && visible.Contains(g))
            return g;
        var parts = id.Split('.');
        for (var n = parts.Length - 1; n > 0; n--)
        {
            var anc = string.Join('.', parts.Take(n));
            if (visible.Contains(anc))
                return anc;
        }

        return null;
    }

    private static IEnumerable<IrNseNode> GroupsIn(IEnumerable<IrNseNode> nses)
    {
        foreach (var n in nses)
        {
            if (!n.IsLeaf)
            {
                yield return n;
                foreach (var g in GroupsIn(n.Children))
                    yield return g;
            }
        }
    }

    private static DiagramClass? ClassInView(ViewDiagram view, string id) =>
        view.Packages.SelectMany(p => p.Classes).FirstOrDefault(c => c.Id == id);

    private static IrProposal? ResolveWhich(IrDocument doc, string? which) =>
        PolicyProposals.ProposalById(doc, which)
        ?? doc.Proposals.FirstOrDefault();

    private static IrNormalizedProposal? ChosenProposal(IrDocument doc, IrProposal? named)
    {
        if (named != null)
            return PolicyProposals.NormalizeProposal(named);
        return null;
    }

    private static HashSet<string> MergedOmit(IrDocument doc, IrNormalizedProposal proposal)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in doc.Omit)
            set.Add(o);
        foreach (var o in proposal.Omit)
            set.Add(o);
        return set;
    }

    private static List<IrClass> VisibleLeaves(IrDocument doc, IReadOnlySet<string> omit) =>
        doc.Classes
            .Where(c => !c.Foreign && !PolicyOmit.OmittedId(c.Id, omit))
            .ToList();

    private static bool SpareLeaf(IReadOnlySet<string> claimed, IReadOnlySet<string> omit, IrClass c) =>
        !ClaimedCovers(c.Id, claimed) &&
        !omit.Contains(c.Id) &&
        !PolicyOmit.OmittedId(c.Id, omit);

    private static bool ClaimedCovers(string id, IReadOnlySet<string> claimed) =>
        claimed.Any(c => IdUnder(id, c) || IdUnder(c, id));

    private static bool IdUnder(string id, string nse) =>
        id == nse || id.StartsWith(nse + ".", StringComparison.Ordinal);

    private static bool HasDescendants(string id, IReadOnlyList<IrClass> classes) =>
        classes.Any(c => c.Id.StartsWith(id + ".", StringComparison.Ordinal));

    private static string ModuleName(IrClass c) => NodeLabel(c.Id);

    private static string NodeLabel(string id)
    {
        var part = id.Split('.')[^1];
        if (part.Length == 0)
            return part;
        if (part.Any(char.IsUpper))
            return char.ToUpperInvariant(part[0]) + part[1..];
        return char.ToUpperInvariant(part[0]) + (part.Length > 1 ? part[1..] : "");
    }

    private static DiagramEdge IrEdgeToDiagram(IrEdge e) => new()
    {
        From = e.From,
        To = e.To,
        Kind = e.Kind,
    };

    private static IrCrapSummary? PoolCrap(IReadOnlyList<DiagramClass> classes)
    {
        var scores = classes
            .SelectMany(c => c.Ops.Select(o => o.Crap ?? c.CrapSummary?.Mu ?? MetricsConfig.CrapGrade(null)))
            .Where(x => x > 0)
            .ToList();
        return scores.Count == 0 ? null : MetricsConfig.SummarizeScores(scores);
    }

    private static (int? killed, int? survived) PoolMutants(IReadOnlyList<DiagramClass> classes)
    {
        var k = classes.Sum(c => c.Killed ?? 0);
        var s = classes.Sum(c => c.Survived ?? 0);
        return (k > 0 ? k : null, s > 0 ? s : null);
    }
}

public static class PolicyOmit
{
    public static bool OmittedId(string id, IReadOnlySet<string> omit) =>
        omit.Any(o => MatchesPrefix(id, o));

    private static bool MatchesPrefix(string id, string prefix) =>
        id == prefix || id.StartsWith(prefix + ".", StringComparison.Ordinal);
}
