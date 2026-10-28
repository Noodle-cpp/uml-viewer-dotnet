using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using UmlViewer.Core.Metrics;
using UmlViewer.Engine;
using UmlViewer.App.Rendering;

namespace UmlViewer.App;

public sealed class DiagramCanvas : Control
{
    public static readonly StyledProperty<CompiledScene?> SceneProperty =
        AvaloniaProperty.Register<DiagramCanvas, CompiledScene?>(nameof(Scene));

    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<DiagramCanvas, string?>(nameof(SelectedId));

    public static readonly StyledProperty<string?> NavLabelProperty =
        AvaloniaProperty.Register<DiagramCanvas, string?>(nameof(NavLabel));

    private Point _pan;
    private double _zoom = 1;
    private Point? _dragStart;
    private Point _panStart;
    private Avalonia.Rect _navLabelBounds;
    private HitResult? _hoverHit;
    private Point _hoverScreen;

    private const double ScrollStep = 24;

    public CompiledScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public string? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>When set, shows a clickable «← …» back hint on the canvas (Clojure draw-nav-label).</summary>
    public string? NavLabel
    {
        get => GetValue(NavLabelProperty);
        set => SetValue(NavLabelProperty, value);
    }

    public event EventHandler<string?>? SelectionChanged;
    public event EventHandler<string>? DrillRequested;
    public event EventHandler<string>? DetailRequested;
    public event EventHandler? BackRequested;
    public event EventHandler<CanvasContextEventArgs>? ContextMenuRequested;

    public void SetViewPanZoom(double panX, double panY, double zoom)
    {
        _pan = new Point(panX, panY);
        _zoom = Math.Clamp(zoom, 0.25, 3);
        InvalidateVisual();
    }

    public (double PanX, double PanY, double Zoom) GetViewPanZoom() => (_pan.X, _pan.Y, _zoom);

    public DiagramCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var screen = e.GetPosition(this);

        if (!string.IsNullOrEmpty(NavLabel) && _navLabelBounds.Contains(screen))
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        var pt = ToDiagram(screen);
        if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed ||
            e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed &&
            e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _dragStart = screen;
            _panStart = _pan;
            return;
        }

        if (Scene == null)
            return;
        var hit = HitEngine.At(Scene, pt.X, pt.Y);
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            ContextMenuRequested?.Invoke(this, new CanvasContextEventArgs(e, hit, screen));
            e.Handled = true;
            return;
        }
        var id = hit?.Kind switch
        {
            "class" or "child" => hit.Id,
            "port" => hit.Parent,
            _ => hit?.Id,
        };
        SelectedId = id;
        SelectionChanged?.Invoke(this, id);
        InvalidateVisual();
        if (e.ClickCount >= 2 && hit != null && hit.Id != null)
        {
            if (hit.Kind == "package" && ProposalHierarchy.ProposalPackageId(hit.Id))
            {
                DrillRequested?.Invoke(this, hit.Id);
                return;
            }

            if ((hit.Kind == "child" && hit.Drill) || (hit.Kind == "class" && hit.Drill))
                DrillRequested?.Invoke(this, hit.Id);
            else if (hit.Kind == "class")
                DetailRequested?.Invoke(this, hit.Id);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var screen = e.GetPosition(this);
        if (_dragStart != null)
        {
            var p = screen;
            _pan = _panStart + (p - _dragStart.Value);
            InvalidateVisual();
            return;
        }

        if (Scene != null)
        {
            var pt = ToDiagram(screen);
            _hoverHit = HitEngine.At(Scene, pt.X, pt.Y);
            _hoverScreen = screen;
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragStart = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var delta = e.Delta.Y > 0 ? 1.1 : 0.9;
        _zoom = Math.Clamp(_zoom * delta, 0.25, 3);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                _pan = new Point(_pan.X + ScrollStep, _pan.Y);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Right:
                _pan = new Point(_pan.X - ScrollStep, _pan.Y);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Up:
                _pan = new Point(_pan.X, _pan.Y + ScrollStep);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Down:
                _pan = new Point(_pan.X, _pan.Y - ScrollStep);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Escape when !string.IsNullOrEmpty(NavLabel):
                BackRequested?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
                break;
        }
    }

    private Point ToDiagram(Point screen) =>
        new((screen.X - _pan.X) / _zoom, (screen.Y - _pan.Y) / _zoom);

    private static void DrawEdge(DrawingContext ctx, SceneEdge e, bool selected)
    {
        var paths = e.Strokes.Count > 0
            ? e.Strokes
            : e.Points.Count > 1 ? new List<List<Point2>> { e.Points } : [];
        var color = e.Violating ? ViewerTheme.Violation : selected ? ViewerTheme.Gold : ViewerTheme.Muted;
        var pen = new Pen(new SolidColorBrush(color), selected ? 2.2 : 1.4);
        foreach (var sub in paths)
        {
            for (var i = 0; i < sub.Count - 1; i++)
            {
                var a = sub[i];
                var b = sub[i + 1];
                ctx.DrawLine(pen, new Point(a.X, a.Y), new Point(b.X, b.Y));
            }
        }

        if (e.Tip != null && e.Behind != null && e.Head != "open")
            DrawArrowhead(ctx, e.Head, e.Tip.Value, e.Behind.Value, color);
    }

    private static void DrawArrowhead(DrawingContext ctx, string kind, Point2 tip, Point2 behind, Color color)
    {
        var ang = Math.Atan2(tip.Y - behind.Y, tip.X - behind.X);
        var size = LayoutEngine.HeadSize;
        var half = Math.PI / 9;
        var x1 = tip.X - size * Math.Cos(ang + half);
        var y1 = tip.Y - size * Math.Sin(ang + half);
        var x2 = tip.X - size * Math.Cos(ang - half);
        var y2 = tip.Y - size * Math.Sin(ang - half);
        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            if (kind is "triangle" or "diamond" or "diamond-fill")
            {
                gc.BeginFigure(new Point(tip.X, tip.Y), true);
                gc.LineTo(new Point(x1, y1));
                gc.LineTo(new Point(x2, y2));
                gc.EndFigure(true);
            }
        }

        if (kind is "triangle" or "diamond" or "diamond-fill")
        {
            var fill = kind == "diamond-fill" ? ViewerTheme.Ink : ViewerTheme.Bg;
            ctx.DrawGeometry(new SolidColorBrush(fill), new Pen(new SolidColorBrush(color), 1.5), geo);
        }
    }

    private static void DrawOpenArrowhead(DrawingContext ctx, Point2 tip, Point2 behind)
    {
        var ang = Math.Atan2(tip.Y - behind.Y, tip.X - behind.X);
        var size = LayoutEngine.HeadSize;
        var half = Math.PI / 9;
        var x1 = tip.X - size * Math.Cos(ang + half);
        var y1 = tip.Y - size * Math.Sin(ang + half);
        var x2 = tip.X - size * Math.Cos(ang - half);
        var y2 = tip.Y - size * Math.Sin(ang - half);
        var pen = new Pen(new SolidColorBrush(ViewerTheme.Muted), 1.3);
        ctx.DrawLine(pen, new Point(tip.X, tip.Y), new Point(x1, y1));
        ctx.DrawLine(pen, new Point(tip.X, tip.Y), new Point(x2, y2));
    }

    private static double ClampX(Engine.Rect r, double x) =>
        Math.Max(r.X + 6, Math.Min(r.Right - 6, x));

    private static void DrawPortLink(DrawingContext ctx, Point2 from, Point2 to)
    {
        var pen = new Pen(new SolidColorBrush(ViewerTheme.Muted), 1.3);
        ctx.DrawLine(pen, new Point(from.X, from.Y), new Point(to.X, to.Y));
        DrawOpenArrowhead(ctx, to, from);
    }

    private static void DrawPort(DrawingContext ctx, PortDep p, bool hot)
    {
        var r = p.Rect;
        ctx.FillRectangle(new SolidColorBrush(hot ? Color.FromRgb(74, 96, 78) : Color.FromRgb(42, 56, 52)),
            new Avalonia.Rect(r.X, r.Y, r.W, r.H), 3);
        ctx.DrawRectangle(new Pen(new SolidColorBrush(hot ? ViewerTheme.Gold : ViewerTheme.Muted), hot ? 2.2 : 1),
            new Avalonia.Rect(r.X, r.Y, r.W, r.H), 3);
        if (string.IsNullOrEmpty(p.Name))
            return;
        var text = new FormattedText(p.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), 10, new SolidColorBrush(ViewerTheme.Ink));
        ctx.DrawText(text, new Point(r.Cx - text.Width / 2, r.Cy - text.Height / 2));
    }

    private static void DrawClassPorts(DrawingContext ctx, SceneClass c, string? selectedId)
    {
        var r = c.Rect;
        foreach (var p in c.InPorts)
        {
            var hot = selectedId == p.Id || selectedId == c.Id;
            DrawPort(ctx, p, hot);
            var pr = p.Rect;
            var x = pr.Cx;
            DrawPortLink(ctx, new Point2(x, pr.Bottom), new Point2(ClampX(r, x), r.Y));
        }

        foreach (var p in c.OutPorts)
        {
            var hot = selectedId == p.Id || selectedId == c.Id;
            DrawPort(ctx, p, hot);
            var pr = p.Rect;
            var x = pr.Cx;
            DrawPortLink(ctx, new Point2(ClampX(r, x), r.Bottom), new Point2(x, pr.Y));
        }
    }

    private static void DrawDepTriangle(DrawingContext ctx, DepIndicator ind)
    {
        if (ind.Triangle.Count < 3)
            return;
        var fill = ind.Violating ? ViewerTheme.Violation : ViewerTheme.Muted;
        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(new Point(ind.Triangle[0].X, ind.Triangle[0].Y), true);
            for (var i = 1; i < ind.Triangle.Count; i++)
                gc.LineTo(new Point(ind.Triangle[i].X, ind.Triangle[i].Y));
            gc.EndFigure(true);
        }

        ctx.DrawGeometry(new SolidColorBrush(fill), null, geo);
    }

    private static void DrawPackage(DrawingContext ctx, ScenePackage p)
    {
        var r = p.Rect;
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(ViewerTheme.Line), 1),
            new Avalonia.Rect(r.X, r.Y, r.W, r.H));
        var text = new FormattedText(p.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 13,
            new SolidColorBrush(ViewerTheme.Muted));
        ctx.DrawText(text, new Point(r.X + LayoutEngine.Pad, r.Y + 8));
    }

    private void DrawNavLabelOverlay(DrawingContext ctx)
    {
        if (string.IsNullOrEmpty(NavLabel))
        {
            _navLabelBounds = default;
            return;
        }

        var text = new FormattedText(NavLabel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Normal), 14,
            new SolidColorBrush(ViewerTheme.Gold));
        var x = 12.0;
        var y = 28.0;
        ctx.DrawText(text, new Point(x, y));
        _navLabelBounds = new Avalonia.Rect(x, y, text.Width + 8, text.Height + 4);
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(ViewerTheme.Bg), Bounds);
        if (Scene == null)
        {
            DrawNavLabelOverlay(context);
            return;
        }

        var hideEdges = Scene.Diagram.HideEdges;

        using (context.PushTransform(Matrix.CreateTranslation(_pan.X, _pan.Y)))
        using (context.PushTransform(Matrix.CreateScale(_zoom, _zoom)))
        {
            foreach (var sec in Scene.Sections)
            {
                var st = new FormattedText(sec.Title, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), 20,
                    new SolidColorBrush(ViewerTheme.Gold));
                context.DrawText(st, new Point(LayoutEngine.Pad, sec.TitleY));
            }

            foreach (var p in Scene.Packages)
                DrawPackage(context, p);
            if (!hideEdges)
            {
                foreach (var e in Scene.Edges)
                    DrawEdge(context, e, false);
            }

            foreach (var c in Scene.Classes.Where(c => !c.Dummy))
                DrawClass(context, c);

            if (hideEdges)
            {
                foreach (var ind in Scene.DepIndicators)
                    DrawDepTriangle(context, ind);
            }
        }

        DrawNavLabelOverlay(context);
        DrawEdgePopup(context);
    }

    private void DrawEdgePopup(DrawingContext ctx)
    {
        if (_hoverHit == null || _hoverHit.Deps.Count == 0)
            return;
        if (_hoverHit.Kind is not ("edge" or "dep"))
            return;

        var deps = _hoverHit.Deps
            .OrderBy(d => d.Violating)
            .ThenBy(d => d.From, StringComparer.Ordinal)
            .ThenBy(d => d.To, StringComparer.Ordinal)
            .ToList();
        var labels = deps.Select(d => $"{d.From} -> {d.To}").ToList();
        const double lineH = 16;
        const double pad = 8;
        var maxW = labels.Count == 0 ? 40 : labels.Max(l => l.Length * 7);
        var boxW = maxW + pad * 2;
        var boxH = pad * 2 + lineH * labels.Count;
        var mx = _hoverScreen.X;
        var my = _hoverScreen.Y;
        var x = Math.Max(8, Math.Min(mx, Bounds.Width - boxW - 8));
        var y = my + 16 + boxH < Bounds.Height ? my + 16 : Math.Max(8, my - boxH - 8);
        ctx.FillRectangle(new SolidColorBrush(Color.FromArgb(240, 18, 22, 24)),
            new Avalonia.Rect(x, y, boxW, boxH), 4);
        ctx.DrawRectangle(new Pen(new SolidColorBrush(ViewerTheme.Line), 1),
            new Avalonia.Rect(x, y, boxW, boxH), 4);
        for (var i = 0; i < labels.Count; i++)
        {
            var dep = deps[i];
            var ft = new FormattedText(labels[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default), 12,
                new SolidColorBrush(dep.Violating ? ViewerTheme.Violation : ViewerTheme.Ink));
            ctx.DrawText(ft, new Point(x + pad, y + pad + i * lineH));
        }
    }

    private void DrawClass(DrawingContext ctx, SceneClass c)
    {
        if (c.Shape == "oval")
        {
            DrawOval(ctx, c);
            return;
        }

        var r = c.Rect;
        var grade = GradeOf(c.Source);
        var fill = ViewerTheme.FillForGrade(grade);
        var stroke = ViewerTheme.StrokeForGrade(grade);
        var selected = SelectedId == c.Id;
        ctx.FillRectangle(new SolidColorBrush(fill), new Avalonia.Rect(r.X, r.Y, r.W, r.H));
        ctx.DrawRectangle(new Pen(new SolidColorBrush(selected ? ViewerTheme.Gold : stroke), selected ? 2.5 : 1.2),
            new Avalonia.Rect(r.X, r.Y, r.W, r.H));

        if (c.Source.Level is int lv)
        {
            var lvText = new FormattedText(lv.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface(FontFamily.Default), 11,
                new SolidColorBrush(ViewerTheme.Muted));
            ctx.DrawText(lvText, new Point(r.X + 5, r.Y + 3));
        }

        DrawClassPorts(ctx, c, SelectedId);

        var y = r.Y + LayoutEngine.Pad;
        foreach (var line in c.Lines)
        {
            if (line.Kind == "rule")
            {
                y += LayoutEngine.Pad / 2;
                ctx.DrawLine(new Pen(new SolidColorBrush(ViewerTheme.Line)), new Point(r.X, y),
                    new Point(r.Right, y));
                y += LayoutEngine.Pad / 2;
                continue;
            }

            var typeface = line.Kind is "name" or "child" && (line.Drill || line.Kind == "name")
                ? FontStyle.Italic
                : FontStyle.Normal;
            var weight = line.Kind == "name" ? FontWeight.SemiBold : FontWeight.Normal;
            var text = new FormattedText(
                line.Text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, typeface, weight),
                13,
                new SolidColorBrush(ViewerTheme.Ink));
            ctx.DrawText(text, new Point(r.X + LayoutEngine.Pad, y));
            y += LayoutEngine.LineH;
        }

        DrawMetricDots(ctx, c, r);
    }

    private static void DrawOval(DrawingContext ctx, SceneClass c)
    {
        var r = c.Rect;
        ctx.DrawEllipse(new SolidColorBrush(ViewerTheme.FillForGrade(null)),
            new Pen(new SolidColorBrush(ViewerTheme.Muted), 1.3),
            new Point(r.Cx, r.Cy), r.W / 2, r.H / 2);
        var text = new FormattedText(c.Source.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), 14, new SolidColorBrush(ViewerTheme.Ink));
        ctx.DrawText(text, new Point(r.Cx - text.Width / 2, r.Cy - text.Height / 2));
    }

    private static double? GradeOf(DiagramClass c)
    {
        var crapG = MetricsConfig.CrapGrade(MetricsConfig.CrapRisk(c.CrapSummary));
        var mutG = MetricsConfig.MutationGrade(MetricsConfig.MutationRatio(c.Killed, c.Survived));
        return MetricsConfig.CombinedGrade(crapG, mutG);
    }

    private static void DrawMetricDots(DrawingContext ctx, SceneClass c, Engine.Rect r)
    {
        const double dotSize = 12;
        const double inset = 10;
        const double gap = 14;
        var mx = r.Right - inset;
        var y = r.Y + inset;
        DrawDot(ctx, mx - gap, y, dotSize, "C", MetricsConfig.CrapGrade(MetricsConfig.CrapRisk(c.Source.CrapSummary)));
        DrawDot(ctx, mx, y, dotSize, "M",
            MetricsConfig.MutationGrade(MetricsConfig.MutationRatio(c.Source.Killed, c.Source.Survived)));
    }

    private static void DrawDot(DrawingContext ctx, double x, double y, double size, string ch, double grade)
    {
        var fill = ViewerTheme.FillForGrade(grade);
        var stroke = ViewerTheme.StrokeForGrade(grade);
        ctx.DrawEllipse(new SolidColorBrush(fill), new Pen(new SolidColorBrush(stroke), 1), new Point(x, y), size / 2,
            size / 2);
        var text = new FormattedText(ch, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default), 9, Brushes.White);
        ctx.DrawText(text, new Point(x - text.Width / 2, y - text.Height / 2));
    }
}
