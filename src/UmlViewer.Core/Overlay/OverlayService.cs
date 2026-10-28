using UmlViewer.Core.Metrics;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Overlay;

public static class OverlayService
{
    public static IrDocument ApplyMetrics(IrDocument doc, Metrics.ProjectMetrics metrics)
    {
        var crapByNs = metrics.Crap
            .GroupBy(c => c.Namespace, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var snapshotNs = metrics.Crap.Select(c => c.Namespace)
            .Concat(metrics.Mutate.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var owners = OwnersByNs(doc.Classes, doc.Prefix, snapshotNs);

        foreach (var cls in doc.Classes.Where(c => !c.Foreign))
            PaintClass(cls, crapByNs, metrics.Mutate, owners);

        foreach (var diagram in doc.Diagrams)
        {
            foreach (var pkg in diagram.Packages)
            {
                foreach (var cls in pkg.Classes.Where(c => !c.Foreign))
                    PaintClass(cls, crapByNs, metrics.Mutate, owners);
            }

            foreach (var cls in diagram.Foreign.Where(c => !c.Foreign))
                PaintClass(cls, crapByNs, metrics.Mutate, owners);
        }

        return doc;
    }

    private static void PaintClass(
        IrClass c,
        Dictionary<string, List<CrapEntry>> crapByNs,
        IReadOnlyDictionary<string, MutateSnapshot> mutateByNs,
        Dictionary<string, List<string>> owners)
    {
        var nss = owners.TryGetValue(c.Ns, out var owned) && owned.Count > 0
            ? owned
            : [c.Ns];

        var crapFns = nss.SelectMany(ns => crapByNs.GetValueOrDefault(ns) ?? []).ToList();
        var mutForms = nss
            .Select(ns => mutateByNs.GetValueOrDefault(ns))
            .Where(s => s != null)
            .SelectMany(s => s!.Forms)
            .ToList();
        var mutByName = mutForms
            .Select(ParseFormName)
            .Where(x => x.name != null)
            .GroupBy(x => x.name!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().form, StringComparer.Ordinal);

        var scores = crapFns.Select(x => x.Crap).ToList();
        if (scores.Count > 0)
        {
            c.CrapSummary = MetricsConfig.SummarizeScores(scores);
            c.Crap = c.CrapSummary?.Mu;
            c.Cc = crapFns.Sum(x => x.Complexity);
            c.Coverage = crapFns.Average(x => x.Coverage) / 100.0;
        }

        if (mutByName.Count > 0)
        {
            c.Killed = mutByName.Values.Sum(f => f.Killed);
            c.Survived = mutByName.Values.Sum(f => f.Survived);
        }

        var opNames = c.Ops.Select(o => o.Name)
            .Concat(crapFns.Select(x => x.Name))
            .Concat(mutByName.Keys)
            .Distinct(StringComparer.Ordinal);

        var ops = new List<IrOp>();
        foreach (var name in opNames)
        {
            var existing = c.Ops.FirstOrDefault(o => o.Name == name);
            var op = existing ?? new IrOp { Name = name, Text = name };
            var crap = crapFns.FirstOrDefault(x => x.Name == name);
            if (crap != null)
            {
                op.Cc = crap.Complexity;
                op.Crap = crap.Crap;
            }

            if (mutByName.TryGetValue(name, out var form))
            {
                op.Killed = form.Killed;
                op.Survived = form.Survived;
                op.Uncovered = form.Uncovered;
                if (form.Id.StartsWith("defn-/", StringComparison.Ordinal))
                    op.Private = true;
            }

            ops.Add(op);
        }

        c.Ops = ops;
    }

    private static Dictionary<string, List<string>> OwnersByNs(
        List<IrClass> classes,
        string prefix,
        List<string> snapshotNs)
    {
        var acc = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var snap in snapshotNs)
        {
            var owner = SnapshotOwner(classes, prefix, snap);
            if (owner == null || string.IsNullOrEmpty(owner.Ns))
                continue;
            if (!acc.TryGetValue(owner.Ns, out var list))
            {
                list = [];
                acc[owner.Ns] = list;
            }

            if (!list.Contains(snap, StringComparer.Ordinal))
                list.Add(snap);
        }

        return acc;
    }

    private static IrClass? SnapshotOwner(List<IrClass> classes, string prefix, string nsName)
    {
        var snap = nsName.Replace("::", ".", StringComparison.Ordinal);
        IrClass? best = null;
        (int major, int minor)? bestRank = null;
        foreach (var c in classes.Where(x => !x.Foreign))
        {
            var rank = ClaimRank(c, prefix, snap);
            if (rank == null)
                continue;
            if (bestRank == null ||
                rank.Value.major < bestRank.Value.major ||
                (rank.Value.major == bestRank.Value.major && rank.Value.minor < bestRank.Value.minor))
            {
                bestRank = rank;
                best = c;
            }
        }

        return best;
    }

    private static (int major, int minor)? ClaimRank(IrClass c, string prefix, string snap)
    {
        var nsName = c.Ns;
        var idName = c.Id;
        if (string.Equals(nsName, snap, StringComparison.Ordinal))
            return (0, 0);
        if (string.Equals(idName, snap, StringComparison.Ordinal))
            return (1, 0);
        if (!string.IsNullOrEmpty(idName) && snap.StartsWith(idName + ".", StringComparison.Ordinal))
            return (2, -idName.Length);
        return null;
    }

    public static OverlayCheckResult CheckNamespaces(IrDocument doc, Metrics.ProjectMetrics metrics, string? nsPrefix)
    {
        var irNs = doc.Classes.Where(c => !c.Foreign).Select(c => c.Ns).ToHashSet(StringComparer.Ordinal);
        var crapNs = metrics.Crap.Select(c => c.Namespace).Distinct(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(nsPrefix))
            crapNs = crapNs.Where(n => n.StartsWith(nsPrefix, StringComparison.Ordinal));

        var missing = crapNs.Where(n => !irNs.Contains(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
        return new OverlayCheckResult(missing.Count, missing);
    }

    private static (string? name, MutateForm form) ParseFormName(MutateForm form)
    {
        var id = form.Id;
        string? name = null;
        if (id.StartsWith("defn-/", StringComparison.Ordinal))
            name = id["defn-/".Length..];
        else if (id.StartsWith("defn/", StringComparison.Ordinal))
            name = id["defn/".Length..];
        return (name, form);
    }
}

public sealed record OverlayCheckResult(int MissingCount, IReadOnlyList<string> MissingNamespaces);
