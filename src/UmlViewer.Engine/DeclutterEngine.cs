namespace UmlViewer.Engine;

public static class DeclutterEngine
{
    public static readonly string[] Modes = ["full", "arrows", "triangles", "elements", "classes"];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["full"] = "Declutter none",
        ["arrows"] = "Declutter arrows",
        ["triangles"] = "Remove arrows",
        ["elements"] = "Declutter elements",
        ["classes"] = "Declutter classes",
    };

    public static string NextMode(string? mode)
    {
        var current = string.IsNullOrEmpty(mode) ? "full" : mode;
        var i = Array.IndexOf(Modes, current);
        if (i < 0)
            i = 0;
        return Modes[(i + 1) % Modes.Length];
    }

    public static string NormalizeForCompile(string mode, bool openLayer)
    {
        if (openLayer && mode == "classes")
            return "elements";
        return string.IsNullOrEmpty(mode) ? "full" : mode;
    }

    public static ViewDiagram Apply(ViewDiagram view, string? mode)
    {
        var m = NormalizeMode(mode);
        if (m is "arrows" or "triangles" or "elements" or "classes")
            view = CollapseArrows(view);
        if (m == "triangles")
            view.HideEdges = true;
        if (m is "elements" or "classes")
            view = HideElements(view);
        if (m == "classes")
            view = HideClasses(view);
        return view;
    }

    private static string NormalizeMode(string? mode) =>
        mode switch
        {
            "methods" => "elements",
            null or "" => "full",
            _ => mode,
        };

    private static LeafDep LeafDep(DiagramEdge e) => new()
    {
        From = e.OrigFrom ?? e.From,
        To = e.OrigTo ?? e.To,
        Violating = e.Violating,
    };

    private static List<DiagramEdge> MergeDirectionEdges(List<DiagramEdge> edges)
    {
        return edges
            .GroupBy(e => (e.From, e.To))
            .Select(g =>
            {
                var es = g.ToList();
                var viol = es.Any(x => x.Violating);
                DiagramEdge best;
                if (viol)
                {
                    best = es.FirstOrDefault(x => x.Violating) ?? es[0];
                    best = CloneEdge(best);
                    best.Kind = "dependency";
                    best.Violating = true;
                }
                else
                {
                    best = CloneEdge(es.MaxBy(x => PolicyEdges.KindRankOf(x.Kind))!);
                }

                best.Deps = es.Select(LeafDep).ToList();
                return best;
            })
            .ToList();
    }

    private static DiagramEdge CloneEdge(DiagramEdge e) => new()
    {
        From = e.From,
        To = e.To,
        Kind = e.Kind,
        Violating = e.Violating,
        OrigFrom = e.OrigFrom,
        OrigTo = e.OrigTo,
        Deps = e.Deps.ToList(),
    };

    private static string? OwnerPkg(ViewDiagram view, string id) =>
        view.Packages.FirstOrDefault(p => p.Classes.Any(c => c.Id == id))?.Id;

    private static DiagramClass PkgDummy(DiagramPackage p) => new()
    {
        Id = p.Id,
        Name = string.IsNullOrEmpty(p.Label) ? p.Id : p.Label,
        Dummy = true,
        Drill = true,
        HideMembers = true,
    };

    private static DiagramClass WithMetrics(DiagramClass dummy, IReadOnlyList<DiagramClass> classes)
    {
        var crapScores = classes
            .SelectMany(c => c.Ops.Select(o => o.Crap ?? c.CrapSummary?.Mu ?? 20.0))
            .Where(x => x > 0)
            .ToList();
        if (crapScores.Count > 0)
            dummy.CrapSummary = Core.Metrics.MetricsConfig.SummarizeScores(crapScores);
        var k = classes.Sum(c => c.Killed ?? 0);
        var s = classes.Sum(c => c.Survived ?? 0);
        if (k > 0)
            dummy.Killed = k;
        if (s > 0)
            dummy.Survived = s;
        var levels = classes.Where(c => c.Level != null).Select(c => c.Level!.Value).ToList();
        if (levels.Count > 0)
            dummy.Level = levels.Max();
        return dummy;
    }

    private static ViewDiagram EnsurePkgDummies(ViewDiagram view)
    {
        view.Packages = view.Packages.Select(p =>
        {
            if (p.Classes.Any(c => c.Id == p.Id))
                return p;
            var dummy = WithMetrics(PkgDummy(p), p.Classes);
            p.Classes = [dummy, ..p.Classes];
            return p;
        }).ToList();
        return view;
    }

    public static ViewDiagram CollapseArrows(ViewDiagram view)
    {
        var pkgs = view.Packages;
        var multi = pkgs.Count > 1;
        if (multi)
            view = EnsurePkgDummies(view);
        string Remap(string id) => multi ? OwnerPkg(view, id) ?? id : id;
        var known = pkgs
            .SelectMany(p => p.Classes.Select(c => c.Id))
            .Concat(view.Foreign.Select(f => f.Id))
            .ToHashSet(StringComparer.Ordinal);
        var edges = view.Edges
            .Select(e =>
            {
                var copy = CloneEdge(e);
                copy.OrigFrom ??= e.From;
                copy.OrigTo ??= e.To;
                copy.From = Remap(e.From);
                copy.To = Remap(e.To);
                return copy;
            })
            .Where(e => known.Contains(e.From) && known.Contains(e.To) && e.From != e.To)
            .ToList();
        view.Edges = MergeDirectionEdges(edges);
        return view;
    }

    public static ViewDiagram HideElements(ViewDiagram view)
    {
        view.Packages = view.Packages.Select(p =>
        {
            p.Classes = p.Classes.Select(c => new DiagramClass
            {
                Id = c.Id,
                Name = c.Name,
                Ns = c.Ns,
                File = c.File,
                Lang = c.Lang,
                Stereotype = c.Stereotype,
                Drill = c.Drill,
                HideMembers = true,
                Foreign = c.Foreign,
                External = c.External,
                Shape = c.Shape,
                Level = c.Level,
                CrapSummary = c.CrapSummary,
                Coverage = c.Coverage,
                Killed = c.Killed,
                Survived = c.Survived,
                Dummy = c.Dummy,
                Ops = [],
                Contents = [],
                InDeps = [],
                OutDeps = [],
            }).ToList();
            return p;
        }).ToList();
        return view;
    }

    public static ViewDiagram HideClasses(ViewDiagram view)
    {
        if (view.Packages.Count > 1)
        {
            view.Packages = view.Packages.Select(p =>
            {
                var real = p.Classes.Where(c => !c.Dummy).ToList();
                var dummy = p.Classes.FirstOrDefault(c => c.Dummy) ?? WithMetrics(PkgDummy(p), real);
                dummy = WithMetrics(dummy, real);
                p.Classes = [dummy];
                return p;
            }).ToList();
        }
        else
        {
            view.Packages = view.Packages.Select(p =>
            {
                p.Classes = p.Classes
                    .Where(c => !c.Dummy)
                    .Select(c =>
                    {
                        c.Contents = [];
                        c.HideMembers = true;
                        return c;
                    })
                    .ToList();
                return p;
            }).ToList();
        }

        return view;
    }
}
