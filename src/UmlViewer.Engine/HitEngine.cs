namespace UmlViewer.Engine;

public static class HitEngine
{
    private const double TriangleSide = 12.0;
    private static readonly double TriangleH = TriangleSide * 0.8660254037844386;

    private static HitResult? PortAt(SceneClass c, double x, double y)
    {
        foreach (var p in c.InPorts)
        {
            if (Geom.Inside(p.Rect, x, y))
                return new HitResult { Kind = "port", Id = p.Id, Parent = c.Id, Dir = "in" };
        }

        foreach (var p in c.OutPorts)
        {
            if (Geom.Inside(p.Rect, x, y))
                return new HitResult { Kind = "port", Id = p.Id, Parent = c.Id, Dir = "out" };
        }

        return null;
    }

    public static List<LeafDep> DepsOf(SceneEdge e) =>
        e.Deps.Count > 0
            ? e.Deps
            : [new LeafDep { From = e.From, To = e.To, Violating = e.Violating }];

    private static IEnumerable<List<Point2>> EdgePaths(SceneEdge e) =>
        e.Strokes.Count > 0 ? e.Strokes : (e.Points.Count > 1 ? [e.Points] : []);

    private static List<Point2> ClassTriangle(Rect r, string dir)
    {
        var cx = r.Cx;
        var half = TriangleSide / 2;
        if (dir == "in")
            return [new Point2(cx, r.Y - TriangleH), new Point2(cx - half, r.Y), new Point2(cx + half, r.Y)];
        return [new Point2(cx - half, r.Bottom), new Point2(cx + half, r.Bottom), new Point2(cx, r.Bottom + TriangleH)];
    }

    public static List<DepIndicator> DepIndicators(CompiledScene scene)
    {
        var edges = scene.Edges;
        var result = new List<DepIndicator>();
        foreach (var c in scene.Classes)
        {
            var incoming = edges.Where(e => e.To == c.Id).SelectMany(DepsOf).ToList();
            var outgoing = edges.Where(e => e.From == c.Id).SelectMany(DepsOf).ToList();
            if (incoming.Count > 0)
            {
                result.Add(new DepIndicator
                {
                    Id = c.Id,
                    Dir = "in",
                    Triangle = ClassTriangle(c.Rect, "in"),
                    Violating = incoming.Any(d => d.Violating),
                    Deps = incoming,
                });
            }

            if (outgoing.Count > 0)
            {
                result.Add(new DepIndicator
                {
                    Id = c.Id,
                    Dir = "out",
                    Triangle = ClassTriangle(c.Rect, "out"),
                    Violating = outgoing.Any(d => d.Violating),
                    Deps = outgoing,
                });
            }
        }

        return result;
    }

    public static HitResult? EdgeAt(CompiledScene scene, double x, double y, double pad = 8)
    {
        var p = new Point2(x, y);
        foreach (var e in scene.Edges.AsEnumerable().Reverse())
        {
            if (EdgePaths(e).Any(path => Geom.NearPolyline(p, path, pad)))
            {
                return new HitResult
                {
                    Kind = "edge",
                    From = e.From,
                    To = e.To,
                    Deps = DepsOf(e),
                };
            }
        }

        return null;
    }

    public static HitResult? At(CompiledScene scene, double x, double y)
    {
        foreach (var ind in scene.DepIndicators.AsEnumerable().Reverse())
        {
            if (Geom.NearTriangle(new Point2(x, y), ind.Triangle, 5))
            {
                return new HitResult
                {
                    Kind = "dep",
                    Dir = ind.Dir,
                    Id = ind.Id,
                    Deps = ind.Deps,
                    Violating = ind.Violating,
                };
            }
        }

        var visible = scene.Classes.Where(c => !c.Dummy).ToList();
        var list = visible.Count > 0 ? visible : scene.Classes;
        foreach (var c in list.AsEnumerable().Reverse())
        {
            var port = PortAt(c, x, y);
            if (port != null)
                return port;
            if (Geom.Inside(c.Rect, x, y))
            {
                var line = LayoutEngine.LineAt(c, x, y);
                if (line != null && line.Kind == "child")
                {
                    return new HitResult
                    {
                        Kind = "child",
                        Id = line.Id,
                        Parent = c.Id,
                        Drill = line.Drill,
                    };
                }

                return new HitResult { Kind = "class", Id = c.Id, Drill = c.Source.Drill };
            }
        }

        var edge = EdgeAt(scene, x, y);
        if (edge != null)
            return edge;

        foreach (var pkg in scene.Packages.AsEnumerable().Reverse())
        {
            if (Geom.Inside(pkg.Rect, x, y))
                return new HitResult { Kind = "package", Id = pkg.Id };
        }

        return null;
    }
}
