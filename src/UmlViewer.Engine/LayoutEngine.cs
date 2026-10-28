using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class LayoutEngine
{
    public const double CharW = 8;
    public const double LineH = 18;
    public const double Pad = 12;
    public const double BannerH = 32;
    public const double ClassGap = 40;
    public const double PackGap = 40;
    public const double RankGap = 80;
    public const double ClassRankGap = 140;
    public const double Margin = 40;
    public const double HeadSize = 16;
    public const double LaneGap = 10;
    public const double SidebarW = 280;
    public const double PortH = 18;
    public const double PortGap = 6;
    public const double PortLink = 22;
    public const double PortLinkOut = 40;
    public const double PortStairX = 16;
    public const double PortStairY = 28;
    public const double UnderGap = 5;
    public const double InspectorPad = 16;
    public const double InspectorRowH = 22;

    public static double TextW(string? s) => CharW * (s?.Length ?? 0);

    public static List<ClassLine> ClassLines(DiagramClass c)
    {
        var lines = new List<ClassLine>();
        if (!string.IsNullOrEmpty(c.Stereotype))
            lines.Add(new ClassLine { Kind = "stereo", Text = "«" + c.Stereotype + "»" });
        lines.Add(new ClassLine { Kind = "name", Text = c.Name });
        if (c.Contents.Count > 0 && !c.HideMembers)
        {
            lines.Add(new ClassLine { Kind = "rule", Text = "" });
            foreach (var ch in c.Contents)
                lines.Add(new ClassLine { Kind = "child", Text = ch.Name, Id = ch.Id, Drill = ch.Drill });
        }

        if (!c.HideMembers && c.Ops.Count > 0)
        {
            lines.Add(new ClassLine { Kind = "rule", Text = "" });
            foreach (var op in c.Ops.Where(o => o.Private != true))
                lines.Add(new ClassLine { Kind = "op", Text = string.IsNullOrEmpty(op.Text) ? op.Name : op.Text });
        }

        return lines;
    }

    public static ClassLine? LineAt(SceneClass c, double x, double y)
    {
        if (c.Lines.Count == 0)
            return null;
        var ly = c.Rect.Y + Pad + 4;
        foreach (var line in c.Lines)
        {
            if (line.Kind == "rule")
            {
                ly += Pad;
                continue;
            }

            if (ly <= y && y <= ly + LineH - 1)
                return line;
            ly += LineH;
        }

        return null;
    }

    private static (double cw, double ch, List<ClassLine> lines) ClassBoxSize(DiagramClass c)
    {
        var lines = ClassLines(c);
        var texts = lines.Where(l => l.Kind != "rule").Select(l => l.Text).ToList();
        var contentW = Math.Max(120, Pad * 2 + (texts.Count == 0 ? 0 : texts.Max(TextW)));
        var textLines = lines.Count(l => l.Kind != "rule");
        var rules = lines.Count(l => l.Kind == "rule");
        var h = Pad * 2 + textLines * LineH + rules * Pad;
        return (contentW, h, lines);
    }

    private static double PortW(PortDep dep) =>
        Math.Max(44, 10 + TextW(dep.Name.Length > 0 ? dep.Name : dep.Id));

    private static double PortRowW(IReadOnlyList<PortDep> deps) =>
        deps.Count == 0 ? 0 : deps.Sum(PortW) + PortGap * Math.Max(0, deps.Count - 1);

    private static double OutStackW(IReadOnlyList<PortDep> deps)
    {
        if (deps.Count == 0)
            return 0;
        var widest = deps.Max(PortW);
        return deps.Count >= 2 ? widest + PortStairX : widest;
    }

    private static double OutStackH(int n) =>
        n > 0 ? PortLinkOut + PortH + (n - 1) * PortStairY : 0;

    private sealed class Packed
    {
        public DiagramClass Source { get; init; } = new();
        public List<ClassLine> Lines { get; set; } = [];
        public double ContentW { get; set; }
        public double ContentH { get; set; }
        public double InSpace { get; set; }
        public double OutSpace { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public int Rank { get; set; }
        public Rect Rect { get; set; }
        public List<PortDep> InPorts { get; set; } = [];
        public List<PortDep> OutPorts { get; set; } = [];
    }

    private static Packed PackClass(DiagramClass c)
    {
        var (cw, ch, lines) = ClassBoxSize(c);
        var inSpace = c.InDeps.Count > 0 ? PortH + PortLink : 0;
        var outSpace = OutStackH(c.OutDeps.Count);
        var pw = Math.Max(cw, Math.Max(PortRowW(c.InDeps), OutStackW(c.OutDeps)));
        return new Packed
        {
            Source = c,
            Lines = lines,
            ContentW = cw,
            ContentH = ch,
            InSpace = inSpace,
            OutSpace = outSpace,
            W = pw,
            H = inSpace + ch + outSpace,
        };
    }

    private static Rect ContentRect(Packed c, double x, double y) =>
        Rect.From(x + (c.W - c.ContentW) / 2, y + c.InSpace, c.ContentW, c.ContentH);

    private static bool InheritEdge(DiagramEdge e) =>
        e.Kind is "inheritance" or "implements";

    private static Dictionary<string, int> BfsRanks(List<string> ids, List<DiagramEdge> edges)
    {
        var idset = ids.ToHashSet(StringComparer.Ordinal);
        var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (!idset.Contains(e.From) || !idset.Contains(e.To))
                continue;
            if (InheritEdge(e))
                AddOut(outgoing, e.To, e.From);
            else
                AddOut(outgoing, e.From, e.To);
        }

        var high = outgoing.Values.SelectMany(x => x).ToHashSet(StringComparer.Ordinal);
        var roots = ids.Where(id => !high.Contains(id)).ToList();
        if (roots.Count == 0)
            roots = ids.ToList();

        var rank = roots.ToDictionary(r => r, _ => 0, StringComparer.Ordinal);
        var q = new Queue<(string id, int r)>();
        foreach (var r in roots)
            q.Enqueue((r, 0));

        while (q.Count > 0)
        {
            var (n, r) = q.Dequeue();
            if (!outgoing.TryGetValue(n, out var kids))
                continue;
            foreach (var k in kids.Where(k => !rank.ContainsKey(k)))
            {
                rank[k] = r + 1;
                q.Enqueue((k, r + 1));
            }
        }

        foreach (var id in ids)
            rank.TryAdd(id, 0);
        return rank;
    }

    private static void AddOut(Dictionary<string, List<string>> m, string from, string to)
    {
        if (!m.TryGetValue(from, out var list))
        {
            list = [];
            m[from] = list;
        }

        list.Add(to);
    }

    private static Dictionary<string, HashSet<string>> Neighbors(List<string> ids, List<DiagramEdge> edges)
    {
        var idset = ids.ToHashSet(StringComparer.Ordinal);
        var nbr = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            if (!idset.Contains(e.From) || !idset.Contains(e.To))
                continue;
            AddNbr(nbr, e.From, e.To);
            AddNbr(nbr, e.To, e.From);
        }

        return nbr;
    }

    private static void AddNbr(Dictionary<string, HashSet<string>> m, string a, string b)
    {
        if (!m.TryGetValue(a, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            m[a] = set;
        }

        set.Add(b);
    }

    private static List<string> BarycenterOrder(List<string> rankIds, Dictionary<string, double> pos,
        Dictionary<string, HashSet<string>> nbr)
    {
        return rankIds.OrderBy(id =>
        {
            if (!nbr.TryGetValue(id, out var ns))
                return pos.GetValueOrDefault(id, 0);
            var xs = ns.Where(pos.ContainsKey).Select(n => pos[n]).ToList();
            return xs.Count > 0 ? xs.Average() : pos.GetValueOrDefault(id, 0);
        }).ThenBy(id => id, StringComparer.Ordinal).ToList();
    }

    private static List<Packed> PlacePortRow(Packed c, List<PortDep> deps, double y)
    {
        if (deps.Count == 0)
            return [];
        var widths = deps.Select(PortW).ToList();
        var total = widths.Sum() + PortGap * Math.Max(0, deps.Count - 1);
        var x = c.Rect.Cx - total / 2;
        var result = new List<Packed>();
        for (var i = 0; i < deps.Count; i++)
        {
            deps[i].Rect = Rect.From(x, y, widths[i], PortH);
            x += widths[i] + PortGap;
        }

        return [];
    }

    private static void LayoutPorts(Packed c)
    {
        var r = c.Rect;
        c.InPorts = [];
        if (c.Source.InDeps.Count > 0)
        {
            var deps = c.Source.InDeps.ToList();
            var y = r.Y - PortLink - PortH;
            var widths = deps.Select(PortW).ToList();
            var total = widths.Sum() + PortGap * Math.Max(0, deps.Count - 1);
            var x = r.Cx - total / 2;
            for (var i = 0; i < deps.Count; i++)
            {
                c.InPorts.Add(new PortDep { Id = deps[i].Id, Name = deps[i].Name, Rect = Rect.From(x, y, widths[i], PortH) });
                x += widths[i] + PortGap;
            }
        }

        c.OutPorts = [];
        if (c.Source.OutDeps.Count > 0)
        {
            var deps = c.Source.OutDeps.ToList();
            var widths = deps.Select(PortW).ToList();
            var widest = widths.Max();
            var stackW = deps.Count >= 2 ? widest + PortStairX : widest;
            var x0 = r.Cx - stackW / 2;
            var y0 = r.Bottom + PortLinkOut;
            for (var i = 0; i < deps.Count; i++)
            {
                var dx = i % 2 == 1 ? PortStairX : 0;
                var y = y0 + i * PortStairY;
                c.OutPorts.Add(new PortDep
                {
                    Id = deps[i].Id,
                    Name = deps[i].Name,
                    Rect = Rect.From(x0 + dx, y, widths[i], PortH),
                });
            }
        }
    }

    private static List<Packed> PlaceClasses(List<DiagramClass> classes, List<DiagramEdge> edges, string direction)
    {
        var sized = classes.Select(PackClass).ToList();
        var byId = sized.ToDictionary(p => p.Source.Id, StringComparer.Ordinal);
        var ids = sized.Select(p => p.Source.Id).ToList();
        var ranks = BfsRanks(ids, edges);
        var nbr = Neighbors(ids, edges);
        var lr = direction is "lr" or "rl";
        var useLevel = !lr && sized.Any(p => p.Source.Level != null);
        var grouped = useLevel
            ? ids.GroupBy(id => byId[id].Source.Level ?? 0).ToDictionary(g => g.Key, g => g.ToList())
            : ids.GroupBy(id => ranks[id]).ToDictionary(g => g.Key, g => g.ToList());
        var rankKeys = useLevel ? grouped.Keys.OrderByDescending(k => k).ToList() : grouped.Keys.OrderBy(k => k).ToList();
        var pos = ids.Select((id, i) => (id, (double)(i * 80))).ToDictionary(x => x.id, x => x.Item2, StringComparer.Ordinal);
        for (var k = 0; k < 5; k++)
        {
            foreach (var r in rankKeys)
            {
                var ordered = BarycenterOrder(grouped[r], pos, nbr);
                for (var i = 0; i < ordered.Count; i++)
                    pos[ordered[i]] = i * 80;
            }
        }

        var groups = rankKeys.Select(r =>
        {
            var ordered = BarycenterOrder(grouped[r], pos, nbr);
            var items = ordered.Select(id => byId[id]).ToList();
            return (rank: r, items, w: items.Count == 0 ? 0 : items.Max(p => p.W), h: items.Count == 0 ? 0 : items.Max(p => p.H));
        }).ToList();

        var placed = new List<Packed>();
        if (lr)
        {
            var cols = groups.Select(g =>
            {
                var h = g.items.Sum(p => p.H) + ClassGap * Math.Max(0, g.items.Count - 1);
                return (g.rank, g.items, g.w, colH: h);
            }).ToList();
            var totalH = cols.Count == 0 ? 0 : cols.Max(c => c.colH);
            var x = 0.0;
            foreach (var col in cols)
            {
                var y0 = Math.Max(0, totalH - col.colH) / 2;
                var y = y0;
                foreach (var p in col.items)
                {
                    p.Rank = col.rank;
                    p.Rect = ContentRect(p, x, y);
                    LayoutPorts(p);
                    placed.Add(p);
                    y += p.H + ClassGap;
                }

                x += col.w + ClassRankGap;
            }
        }
        else
        {
            var y = 0.0;
            foreach (var row in groups)
            {
                var x = 0.0;
                foreach (var p in row.items)
                {
                    p.Rank = row.rank;
                    p.Rect = ContentRect(p, x, y);
                    LayoutPorts(p);
                    placed.Add(p);
                    x += p.W + ClassGap;
                }

                y += row.h + ClassRankGap;
            }
        }

        return placed;
    }

    private static SceneClass ToScene(Packed p, string? packageId) => new()
    {
        Source = p.Source,
        Package = packageId,
        Rank = p.Rank,
        Lines = p.Lines,
        Rect = p.Rect,
        InPorts = p.InPorts,
        OutPorts = p.OutPorts,
        Shape = p.Source.Shape,
    };

    private static ScenePackage LayoutPackage(DiagramPackage pkg, double ox, double oy, List<DiagramEdge> edges,
        string direction, int rankBase)
    {
        var inner = PlaceClasses(pkg.Classes, edges, direction);
        var shifted = inner.Select(p =>
        {
            p.Rect = Rect.From(ox + Pad + p.Rect.X, oy + BannerH + Pad + p.Rect.Y, p.Rect.W, p.Rect.H);
            LayoutPorts(p);
            p.Rank += rankBase;
            return p;
        }).ToList();

        var body = Geom.Union(shifted.SelectMany(p =>
            new[] { p.Rect }.Concat(p.InPorts.Select(x => x.Rect)).Concat(p.OutPorts.Select(x => x.Rect)))) ??
                   Rect.From(ox + Pad, oy + BannerH + Pad, 160, 40);
        var title = pkg.Label;
        var packW = Math.Max(Math.Max(body.Right + Pad - ox, Pad * 2 + TextW(title)), 180);
        var packH = body.Bottom + Pad - oy;
        var packRect = Rect.From(ox, oy, packW, packH);
        return new ScenePackage
        {
            Id = pkg.Id,
            Label = pkg.Label,
            Title = title,
            Rect = packRect,
        };
    }

    private static List<SceneClass> LayoutForeigns(List<DiagramClass> foreign, double x, double y)
    {
        var result = new List<SceneClass>();
        foreach (var c in foreign)
        {
            var w = Math.Max(80, Pad * 2 + TextW(c.Name));
            var h = Math.Max(44, w * 0.55);
            result.Add(new SceneClass
            {
                Source = c,
                Shape = "oval",
                Lines = [new ClassLine { Kind = "name", Text = c.Name }],
                Rect = Rect.From(x, y, w, h),
            });
            y += h + ClassGap;
        }

        return result;
    }

    public static LayoutScene Layout(ViewDiagram diagram)
    {
        var edges = diagram.Edges;
        var direction = string.IsNullOrEmpty(diagram.Direction) ? "tb" : diagram.Direction;
        var pkgs = diagram.Packages;
        var stride = 1 + (pkgs.Count == 0 ? 1 : pkgs.Max(p => p.Classes.Count));
        var y = Margin;
        var laidPkgs = new List<(ScenePackage pkg, List<SceneClass> classes)>();
        for (var i = 0; i < pkgs.Count; i++)
        {
            var pkg = pkgs[i];
            var inner = PlaceClasses(pkg.Classes, edges, direction);
            var shifted = inner.Select(p =>
            {
                p.Rect = Rect.From(Margin + Pad + p.Rect.X, y + BannerH + Pad + p.Rect.Y, p.Rect.W, p.Rect.H);
                LayoutPorts(p);
                p.Rank += i * stride;
                return p;
            }).ToList();
            var body = Geom.Union(shifted.SelectMany(p =>
                new[] { p.Rect }.Concat(p.InPorts.Select(x => x.Rect)).Concat(p.OutPorts.Select(x => x.Rect)))) ??
                       Rect.From(Margin + Pad, y + BannerH + Pad, 160, 40);
            var title = pkg.Label;
            var packW = Math.Max(Math.Max(body.Right + Pad - Margin, Pad * 2 + TextW(title)), 180);
            var packH = body.Bottom + Pad - y;
            var sp = new ScenePackage
            {
                Id = pkg.Id,
                Label = pkg.Label,
                Title = title,
                Rect = Rect.From(Margin, y, packW, packH),
            };
            laidPkgs.Add((sp, shifted.Select(p => ToScene(p, pkg.Id)).ToList()));
            y += packH + RankGap;
        }

        var packBounds = Geom.Union(laidPkgs.Select(t => t.pkg.Rect)) ?? Rect.From(Margin, Margin, 400, 300);
        var fx = packBounds.Right + PackGap;
        var fy = packBounds.Y;
        var ovals = LayoutForeigns(diagram.Foreign, fx, fy);
        var ovalH = ovals.Count == 0 ? 0 : ovals[^1].Rect.Bottom - fy;
        var dy = ovals.Count > 0 && packBounds.H > ovalH ? (packBounds.H - ovalH) / 2 : 0;
        if (dy > 0)
        {
            ovals = ovals.Select(o =>
            {
                o.Rect = o.Rect.Shift(0, dy);
                return o;
            }).ToList();
        }

        var classes = laidPkgs.SelectMany(t => t.classes).Concat(ovals).ToList();
        var bounds = Geom.Union(laidPkgs.Select(t => t.pkg.Rect).Concat(ovals.Select(o => o.Rect))) ?? packBounds;
        return new LayoutScene
        {
            Diagram = diagram,
            Packages = laidPkgs.Select(t => t.pkg).ToList(),
            Classes = classes,
            Size = new SceneSize { W = bounds.Right + Margin, H = bounds.Bottom + Margin },
        };
    }
}
