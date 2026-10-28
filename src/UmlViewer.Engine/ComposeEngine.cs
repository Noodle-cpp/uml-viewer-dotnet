using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class ComposeEngine
{
    private static LayoutScene Translate(LayoutScene scene, double dx, double dy)
    {
        foreach (var p in scene.Packages)
            p.Rect = p.Rect.Shift(dx, dy);
        foreach (var c in scene.Classes)
            c.Rect = c.Rect.Shift(dx, dy);
        foreach (var e in scene.Edges)
            e.Points = e.Points.Select(p => new Point2(p.X + dx, p.Y + dy)).ToList();
        return scene;
    }

    private static Rect BoundsOf(LayoutScene scene)
    {
        var rects = scene.Packages.Select(p => p.Rect)
            .Concat(scene.Classes.Select(c => c.Rect))
            .ToList();
        foreach (var e in scene.Edges)
        {
            var bb = EdgeBounds(scene, e);
            if (bb != null)
                rects.Add(bb.Value);
        }

        var u = Geom.Union(rects) ?? Rect.From(0, 0, 400, 300);
        return Geom.Inflate(u, LayoutEngine.HeadSize);
    }

    private static BasisPath? DrawnPath(LayoutScene scene, SceneEdge e)
    {
        if (e.Points.Count < 2)
            return null;
        var from = scene.Classes.FirstOrDefault(c => c.Id == e.From);
        var to = scene.Classes.FirstOrDefault(c => c.Id == e.To);
        if (from == null || to == null)
            return null;
        return CurveEngine.ConstrainEnds(CurveEngine.BasisPath(e.Points), from.Rect, to.Rect);
    }

    private static Rect? EdgeBounds(LayoutScene scene, SceneEdge e)
    {
        var path = DrawnPath(scene, e);
        return path == null ? null : CurveEngine.PathBounds(path);
    }

    private static LayoutScene WithSize(LayoutScene scene)
    {
        var b = BoundsOf(scene);
        scene.Size = new SceneSize
        {
            W = b.Right + LayoutEngine.Margin,
            H = b.Bottom + LayoutEngine.Margin,
            MinX = Math.Min(0, b.X - LayoutEngine.Margin),
            MinY = Math.Min(0, b.Y - LayoutEngine.Margin),
        };
        return scene;
    }

    public static LayoutScene FitScene(LayoutScene scene)
    {
        var b = BoundsOf(scene);
        var dx = Math.Max(0, LayoutEngine.Margin - b.X);
        var dy = Math.Max(0, LayoutEngine.Margin - b.Y);
        if (dx > 0 || dy > 0)
            scene = Translate(scene, dx, dy);
        return WithSize(scene);
    }

    private static IEnumerable<Rect> NearbyRects(List<SceneClass> classes, SceneEdge e, Rect? bb)
    {
        var ends = new HashSet<string>(StringComparer.Ordinal) { e.From, e.To };
        var probe = bb == null ? (Rect?)null : Geom.Inflate(bb.Value, LayoutEngine.UnderGap + 8);
        foreach (var c in classes)
        {
            if (c.Dummy || ends.Contains(c.Id))
                continue;
            if (probe == null || Geom.Overlaps(probe.Value, c.Rect))
                yield return c.Rect;
        }
    }

    private static SceneEdge PrepareEdge(List<SceneClass> classes, SceneEdge e)
    {
        if (e.Points.Count < 2)
            return e;
        var from = classes.First(c => c.Id == e.From);
        var to = classes.First(c => c.Id == e.To);
        var path = CurveEngine.ConstrainEnds(CurveEngine.BasisPath(e.Points), from.Rect, to.Rect);
        var (behind, tip) = CurveEngine.EndTangent(path);
        var samples = CurveEngine.FlattenPath(path);
        var bb = CurveEngine.PathBounds(path);
        var obstacles = NearbyRects(classes, e, bb).ToList();
        e.Strokes = Geom.GapPolyline(samples, obstacles, LayoutEngine.UnderGap);
        e.Tip = tip;
        e.Behind = behind;
        return e;
    }

    public static CompiledScene CompileDiagram(ViewDiagram diagram)
    {
        var laid = LayoutEngine.Layout(diagram);
        var routed = RouteEngine.Route(laid);
        var fitted = FitScene(routed);
        var classes = fitted.Classes;
        var edges = fitted.Edges.Select(e => PrepareEdge(classes, CloneEdge(e))).ToList();
        var scene = new CompiledScene
        {
            Title = diagram.Title,
            Diagram = diagram,
            Packages = fitted.Packages,
            Classes = classes,
            Edges = edges,
            Size = fitted.Size,
            ClassById = classes.ToDictionary(c => c.Id, StringComparer.Ordinal),
            PackageById = fitted.Packages.ToDictionary(p => p.Id, StringComparer.Ordinal),
        };
        if (diagram.HideEdges)
            scene.DepIndicators = HitEngine.DepIndicators(scene);
        return scene;
    }

    public static CompiledScene CompileDocument(IrDocument doc, ViewDiagram? declutterTemplate = null, string? declutterMode = null)
    {
        const double gap = 64;
        const double titleH = 40;
        var sections = new List<SceneSection>();
        var allPackages = new List<ScenePackage>();
        var allClasses = new List<SceneClass>();
        var allEdges = new List<SceneEdge>();
        double y = 24;
        double totalH = 24;
        var mode = string.IsNullOrEmpty(declutterMode) ? "full" : declutterMode;

        for (var i = 0; i < doc.Diagrams.Count; i++)
        {
            var irD = doc.Diagrams[i];
            var view = StackedDiagramMapper.ToView(irD);
            view = DeclutterEngine.Apply(view, mode);
            if (declutterTemplate != null)
                view.HideEdges = declutterTemplate.HideEdges;

            var laid = RouteEngine.Route(LayoutEngine.Layout(view));
            laid = QualifyScene(i, laid);
            laid = FitScene(laid);
            var dy = y + titleH;
            laid = Translate(laid, LayoutEngine.Margin, dy);
            sections.Add(new SceneSection { Title = irD.Title, TitleY = y });
            allPackages.AddRange(laid.Packages);
            allClasses.AddRange(laid.Classes);
            allEdges.AddRange(laid.Edges);
            totalH = dy + laid.Size.H + gap;
            y = totalH - gap;
        }

        var stacked = new LayoutScene
        {
            Packages = allPackages,
            Classes = allClasses,
            Edges = allEdges,
            Size = new SceneSize { W = 800, H = totalH },
        };
        var b = BoundsOf(stacked);
        var dx = Math.Max(0, LayoutEngine.Margin - b.X);
        if (dx > 0)
        {
            stacked = Translate(stacked, dx, 0);
            foreach (var sec in sections)
                sec.TitleY += 0;
        }

        stacked = WithSize(stacked);
        var prepared = PrepareScene(stacked);
        return new CompiledScene
        {
            Title = doc.Title,
            Diagram = new ViewDiagram { Title = doc.Title, HideEdges = declutterTemplate?.HideEdges ?? false },
            Sections = sections,
            Packages = prepared.Packages,
            Classes = prepared.Classes,
            Edges = prepared.Edges,
            Size = prepared.Size,
            ClassById = prepared.Classes.ToDictionary(c => c.Id, StringComparer.Ordinal),
            PackageById = prepared.Packages.ToDictionary(p => p.Id, StringComparer.Ordinal),
            DepIndicators = declutterTemplate?.HideEdges == true ? HitEngine.DepIndicators(new CompiledScene
            {
                Classes = prepared.Classes,
                Edges = prepared.Edges,
            }) : [],
        };
    }

    private static LayoutScene QualifyScene(int index, LayoutScene scene)
    {
        string Q(string id) => $"d{index}-{id}";
        foreach (var p in scene.Packages)
            p.Id = Q(p.Id);

        foreach (var c in scene.Classes)
        {
            c.Source.Id = Q(c.Source.Id);
            if (!string.IsNullOrEmpty(c.Package))
                c.Package = Q(c.Package);
        }

        foreach (var e in scene.Edges)
        {
            e.From = Q(e.From);
            e.To = Q(e.To);
        }

        return scene;
    }

    private static LayoutScene PrepareScene(LayoutScene scene)
    {
        var classes = scene.Classes;
        scene.Edges = scene.Edges.Select(e => PrepareEdge(classes, CloneEdge(e))).ToList();
        return scene;
    }

    private static SceneEdge CloneEdge(SceneEdge e) => new()
    {
        From = e.From,
        To = e.To,
        Kind = e.Kind,
        Violating = e.Violating,
        Points = e.Points.ToList(),
        Head = e.Head,
        Deps = e.Deps.ToList(),
    };
}
