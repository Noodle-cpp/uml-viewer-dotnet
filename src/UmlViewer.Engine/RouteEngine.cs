namespace UmlViewer.Engine;

public static class RouteEngine
{
    private const double MinStub = 20.0;

    private sealed record Annotated(DiagramEdge Edge, SceneClass From, SceneClass To);

    private sealed record PortAttach(int Index, string Which, string Id, Point2 P, string Face);

    private sealed record Channel(double Lo, double Hi, double Span);

    private static string Head(string kind) => kind switch
    {
        "inheritance" or "implements" => "triangle",
        "composition" => "diamond-fill",
        "aggregation" => "diamond",
        _ => "open",
    };

    private static bool Lr(LayoutScene scene) => scene.Diagram.Direction is "lr" or "rl";

    private static bool LaidOut(SceneClass? n) => n != null && n.Rect.W > 0;

    private static Point2 PortOnT(Rect r, string face, double t)
    {
        t = Math.Clamp(t, 0.12, 0.88);
        return face switch
        {
            "top" => new Point2(r.X + t * r.W, r.Y),
            "bottom" => new Point2(r.X + t * r.W, r.Bottom),
            "left" => new Point2(r.X, r.Y + t * r.H),
            _ => new Point2(r.Right, r.Y + t * r.H),
        };
    }

    private static Point2 Port(SceneClass node, string face, double t) => PortOnT(node.Rect, face, t);

    private static bool Near(Point2 a, Point2 b) => Geom.Hypot(a.X - b.X, a.Y - b.Y) < 0.51;

    private static bool Collinear(Point2 a, Point2 b, Point2 c) =>
        (Math.Abs(a.X - b.X) < 0.51 && Math.Abs(b.X - c.X) < 0.51) ||
        (Math.Abs(a.Y - b.Y) < 0.51 && Math.Abs(b.Y - c.Y) < 0.51);

    private static bool Between(Point2 a, Point2 b, Point2 c) =>
        Collinear(a, b, c) &&
        Math.Min(a.X, c.X) <= b.X && b.X <= Math.Max(a.X, c.X) &&
        Math.Min(a.Y, c.Y) <= b.Y && b.Y <= Math.Max(a.Y, c.Y);

    private static List<Point2> Collapse(IEnumerable<Point2> pts)
    {
        var acc = new List<Point2>();
        foreach (var p in pts)
        {
            if (acc.Count == 0) { acc.Add(p); continue; }
            if (Near(acc[^1], p)) continue;
            if (acc.Count >= 2 && Between(acc[^2], acc[^1], p))
            {
                if (Near(acc[^1], p)) acc.RemoveAt(acc.Count - 1);
                else acc[^1] = p;
            }
            else acc.Add(p);
        }
        return acc;
    }

    private static bool PathHits(List<Point2> pts, string fromId, string toId, List<SceneClass> classes)
    {
        var others = classes.Where(c => c.Id != fromId && c.Id != toId && !c.Dummy);
        foreach (var (a, b) in pts.Zip(pts.Skip(1)))
        {
            if (others.Any(c => Geom.SegmentHitsRect(a, b, c.Rect)))
                return true;
        }
        return false;
    }

    private static double MinTurnDot(List<Point2> pts)
    {
        if (pts.Count < 3) return 1.0;
        var dots = new List<double>();
        for (var i = 1; i < pts.Count - 1; i++)
        {
            var ax = pts[i].X - pts[i - 1].X;
            var ay = pts[i].Y - pts[i - 1].Y;
            var bx = pts[i + 1].X - pts[i].X;
            var by = pts[i + 1].Y - pts[i].Y;
            var la = Geom.Hypot(ax, ay);
            var lb = Geom.Hypot(bx, by);
            dots.Add(la < 1e-9 || lb < 1e-9 ? 1.0 : (ax * bx + ay * by) / (la * lb));
        }
        return dots.Min();
    }

    private static bool StubOk(Rect? r, Point2? p, Point2? other)
    {
        if (r == null || p == null || other == null) return true;
        var face = CurveEngine.FaceOf(r.Value, p.Value);
        var dx = Math.Abs(p.Value.X - other.Value.X);
        var dy = Math.Abs(p.Value.Y - other.Value.Y);
        var (along, across) = face is "left" or "right" ? (dy, dx) : (dx, dy);
        return across + 1.0 >= along;
    }

    private static bool PathAttachOk(List<Point2> pts, string fromId, string toId, Dictionary<string, SceneClass> idx)
    {
        if (pts.Count <= 1) return false;
        idx.TryGetValue(fromId, out var from);
        idx.TryGetValue(toId, out var to);
        return StubOk(from?.Rect, pts[0], pts[1]) &&
               StubOk(to?.Rect, pts[^1], pts[^2]);
    }

    private static bool ReversesPastTarget(List<Point2> pts)
    {
        if (pts.Count < 3) return false;
        var s = pts[0];
        var e = pts[^1];
        var lrAxis = Math.Abs(e.X - s.X) >= Math.Abs(e.Y - s.Y);
        double Sv(bool lr) => lr ? s.X : s.Y;
        double Ev(bool lr) => lr ? e.X : e.Y;
        double Av(bool lr, Point2 p) => lr ? p.X : p.Y;
        var sv = Sv(lrAxis);
        var ev = Ev(lrAxis);
        var dir = ev.CompareTo(sv);
        if (dir == 0) return false;
        const double pad = 12.0;
        for (var i = 0; i < pts.Count - 1; i++)
        {
            var a = Av(lrAxis, pts[i]);
            var b = Av(lrAxis, pts[i + 1]);
            if (dir > 0)
            {
                if (a > ev + pad && b < a - 1.0) return true;
            }
            else if (a < ev - pad && b > a + 1.0) return true;
        }
        return false;
    }

    private static List<Point2>? TryPaths(List<List<Point2>> candidates, string fromId, string toId,
        List<SceneClass> classes, Dictionary<string, SceneClass> idx)
    {
        var clear = candidates.Where(c => c.Count > 0 && !PathHits(c, fromId, toId, classes)).ToList();
        var ok = clear.Where(c => PathAttachOk(c, fromId, toId, idx) && !ReversesPastTarget(c)).ToList();
        if (ok.Count > 0) return ok.MaxBy(MinTurnDot)!;
        var fallback = clear.Where(c => !ReversesPastTarget(c)).ToList();
        return fallback.FirstOrDefault() ?? clear.FirstOrDefault() ?? candidates.FirstOrDefault();
    }

    private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

    private static (string fromFace, string toFace) StackFaces(SceneClass from, SceneClass to, bool lr) =>
        lr
            ? (from.Rect.Cy < to.Rect.Cy ? ("bottom", "top") : ("top", "bottom"))
            : (from.Rect.Cx < to.Rect.Cx ? ("right", "left") : ("left", "right"));

    private static (string fromFace, string toFace) FlowFaces(SceneClass from, SceneClass to, bool lr) =>
        lr
            ? (from.Rect.Cx <= to.Rect.Cx ? ("right", "left") : ("left", "right"))
            : (from.Rect.Cy <= to.Rect.Cy ? ("bottom", "top") : ("top", "bottom"));

    private static (string fromFace, string toFace) SideFaces(string side) => side switch
    {
        "east" => ("right", "right"),
        "west" => ("left", "left"),
        "south" => ("bottom", "bottom"),
        _ => ("top", "top"),
    };

    private static (double lo, double hi) FaceSpan(Rect r, string face) => face switch
    {
        "top" or "bottom" => (r.X, r.Right),
        _ => (r.Y, r.Bottom),
    };

    private static (double lo, double hi)? Overlap((double lo, double hi) a, (double lo, double hi) b)
    {
        var lo = Math.Max(a.lo, b.lo);
        var hi = Math.Min(a.Item2, b.Item2);
        return lo + 1.0 < hi ? (lo, hi) : null;
    }

    private static List<Point2> AlongStack(SceneClass from, SceneClass to, double fromT, double toT, bool lr)
    {
        var (fromFace, toFace) = StackFaces(from, to, lr);
        var start = Port(from, fromFace, fromT);
        var end = Port(to, toFace, toT);
        var ov = Overlap(FaceSpan(from.Rect, fromFace), FaceSpan(to.Rect, toFace));
        if (ov != null)
        {
            var (lo, hi) = ov.Value;
            var span = hi - lo;
            var a = lo + fromT * span;
            var b = lo + toT * span;
            return lr
                ? Collapse([new Point2(a, start.Y), new Point2(b, end.Y)])
                : Collapse([new Point2(start.X, a), new Point2(end.X, b)]);
        }
        if (lr)
        {
            var gapY = (start.Y + end.Y) / 2;
            return Collapse([start, new Point2(start.X, gapY), new Point2(end.X, gapY), end]);
        }
        var gapX = (start.X + end.X) / 2;
        return Collapse([start, new Point2(gapX, start.Y), new Point2(gapX, end.Y), end]);
    }

    private static List<Point2> PairU(SceneClass from, SceneClass to, double fromT, double toT, string side, int i)
    {
        var (fromFace, toFace) = SideFaces(side);
        var start = Port(from, fromFace, fromT);
        var end = Port(to, toFace, toT);
        var bb = Geom.Union([from.Rect, to.Rect]) ?? from.Rect;
        var pad = LayoutEngine.LaneGap + i * LayoutEngine.LaneGap;
        return side switch
        {
            "east" => Collapse([start, new Point2(bb.Right + pad, start.Y), new Point2(bb.Right + pad, end.Y), end]),
            "west" => Collapse([start, new Point2(bb.X - pad, start.Y), new Point2(bb.X - pad, end.Y), end]),
            "south" => Collapse([start, new Point2(start.X, bb.Bottom + pad), new Point2(end.X, bb.Bottom + pad), end]),
            _ => Collapse([start, new Point2(start.X, bb.Y - pad), new Point2(end.X, bb.Y - pad), end]),
        };
    }

    private static List<Point2> AroundBbox(Rect bb, Point2 start, Point2 end, int i, bool lr)
    {
        var pad = 2 * LayoutEngine.LaneGap + i * LayoutEngine.LaneGap;
        if (lr)
        {
            var track = bb.Right + pad;
            return Collapse([start, new Point2(track, start.Y), new Point2(track, end.Y), end]);
        }
        var trackY = bb.Bottom + pad;
        return Collapse([start, new Point2(start.X, trackY), new Point2(end.X, trackY), end]);
    }

    private static Channel GeometricChannel(SceneClass from, SceneClass to, bool lr)
    {
        var a = from.Rect;
        var b = to.Rect;
        if (lr)
        {
            var lo = Math.Min(a.Right, b.Right);
            var hi = Math.Max(a.X, b.X);
            return new Channel(lo, hi, hi - lo);
        }
        var loY = Math.Min(a.Bottom, b.Bottom);
        var hiY = Math.Max(a.Y, b.Y);
        return new Channel(loY, hiY, hiY - loY);
    }

    private static double LaneCoord(Channel ch, int i, int n)
    {
        var span = Math.Max(LayoutEngine.LaneGap, ch.Span);
        var step = span / (double)(Math.Max(n, 1) + 1);
        return ch.Lo + (i + 1) * step;
    }

    private static List<Point2>? ThroughChannel(Point2 start, Point2 end, Channel ch, int i, int n, bool lr)
    {
        var lane = LaneCoord(ch, i, n);
        return lr
            ? Collapse([start, new Point2(lane, start.Y), new Point2(lane, end.Y), end])
            : Collapse([start, new Point2(start.X, lane), new Point2(end.X, lane), end]);
    }

    private static Rect? RankBbox(List<SceneClass> classes, int rank) =>
        Geom.Union(classes.Where(c => c.Rank == rank).Select(c => c.Rect));

    private static List<Point2>? ViaRank(List<SceneClass> classes, int rank, Point2 start, Point2 end, int i, bool lr)
    {
        var bb = RankBbox(classes, rank);
        if (bb == null) return null;
        var r = bb.Value;
        if (lr)
        {
            var lo = Math.Min(start.Y, end.Y);
            var hi = Math.Max(start.Y, end.Y);
            var y = Clamp((start.Y + end.Y) / 2, lo, hi);
            var left = r.X - (LayoutEngine.LaneGap + i * LayoutEngine.LaneGap);
            var right = r.Right + (LayoutEngine.LaneGap + i * LayoutEngine.LaneGap);
            return [new Point2(left, y), new Point2(right, y)];
        }
        var loX = Math.Min(start.X, end.X);
        var hiX = Math.Max(start.X, end.X);
        var x = Clamp((start.X + end.X) / 2, loX, hiX);
        var top = Clamp(r.Y - (LayoutEngine.LaneGap + i * LayoutEngine.LaneGap), loX, hiX);
        var bot = Clamp(r.Bottom + (LayoutEngine.LaneGap + i * LayoutEngine.LaneGap), loX, hiX);
        return Math.Abs(top - bot) < 1.0 ? [new Point2(x, top)] : [new Point2(x, top), new Point2(x, bot)];
    }

    private static List<Point2>? ThroughIntermediates(SceneClass from, SceneClass to, Point2 start, Point2 end,
        List<SceneClass> classes, int i, bool lr)
    {
        var rf = from.Rank;
        var rt = to.Rank;
        var mids = rf < rt
            ? Enumerable.Range(rf + 1, rt - rf - 1)
            : Enumerable.Range(rt + 1, rf - rt - 1).Reverse();
        var hops = mids.SelectMany(r => ViaRank(classes, r, start, end, i, lr) ?? []).ToList();
        if (hops.Count == 0) return null;
        return Collapse(hops.Prepend(start).Append(end));
    }

    private static IEnumerable<List<Point2>> SideThenFlow(SceneClass from, SceneClass to, double fromT, double toT, int i, bool lr)
    {
        var pad = 2 * LayoutEngine.LaneGap + i * LayoutEngine.LaneGap;
        var a = from.Rect;
        var b = to.Rect;
        var (_, toFace) = FlowFaces(from, to, lr);
        var end = Port(to, toFace, toT);
        if (lr)
        {
            var top = Math.Min(a.Y, b.Y) - pad;
            var bot = Math.Max(a.Bottom, b.Bottom) + pad;
            var st = Port(from, "top", fromT);
            var sb = Port(from, "bottom", fromT);
            yield return Collapse([st, new Point2(st.X, top), new Point2(end.X, top), end]);
            yield return Collapse([sb, new Point2(sb.X, bot), new Point2(end.X, bot), end]);
        }
        else
        {
            var left = Math.Min(a.X, b.X) - pad;
            var right = Math.Max(a.Right, b.Right) + pad;
            var sl = Port(from, "left", fromT);
            var sr = Port(from, "right", fromT);
            yield return Collapse([sl, new Point2(left, sl.Y), new Point2(left, end.Y), end]);
            yield return Collapse([sr, new Point2(right, sr.Y), new Point2(right, end.Y), end]);
        }
    }

    private static List<List<Point2>> SameRankCandidates(SceneClass from, SceneClass to, double fromT, double toT,
        int i, List<SceneClass> classes, bool lr)
    {
        var outer = lr ? "east" : "south";
        var inner = lr ? "west" : "north";
        var (of, tf) = SideFaces(outer);
        var fb = Port(from, of, fromT);
        var fe = Port(to, tf, toT);
        var rankBb = RankBbox(classes, from.Rank) ?? from.Rect;
        return
        [
            AlongStack(from, to, fromT, toT, lr),
            PairU(from, to, fromT, toT, outer, i),
            PairU(from, to, fromT, toT, inner, i),
            AroundBbox(rankBb, fb, fe, i, lr),
        ];
    }

    private static bool Stacked(SceneClass from, SceneClass to, bool lr) =>
        lr
            ? to.Rect.X - from.Rect.Right > 8 || from.Rect.X - to.Rect.Right > 8
            : to.Rect.Y - from.Rect.Bottom > 8 || from.Rect.Y - to.Rect.Bottom > 8;

    private static List<List<Point2>> HopCandidates(SceneClass from, SceneClass to, double fromT, double toT,
        int i, int n, List<SceneClass> classes, bool lr)
    {
        var (ff, tf) = FlowFaces(from, to, lr);
        var start = Port(from, ff, fromT);
        var end = Port(to, tf, toT);
        var ch = GeometricChannel(from, to, lr);
        var list = new List<List<Point2>>();
        var primary = ThroughChannel(start, end, ch, i, n, lr);
        if (primary != null) list.Add(primary);
        list.AddRange(SideThenFlow(from, to, fromT, toT, i, lr));
        var via = ThroughIntermediates(from, to, start, end, classes, i, lr);
        if (via != null) list.Add(via);
        var alt = ThroughChannel(start, end, ch, n - i - 1, n, lr);
        if (alt != null) list.Add(alt);
        list.Add(Collapse([start, end]));
        return list;
    }

    private static Point2 SlideToCone(Rect r, Point2 p, Point2 other)
    {
        var face = CurveEngine.FaceOf(r, p);
        var px = p.X;
        var py = p.Y;
        var ox = other.X;
        var oy = other.Y;
        var loX = r.X + 0.12 * r.W;
        var hiX = r.Right - 0.12 * r.W;
        var loY = r.Y + 0.12 * r.H;
        var hiY = r.Bottom - 0.12 * r.H;
        if (face is "left" or "right")
        {
            var lim = Math.Max(1.0, Math.Abs(px - ox));
            var py2 = Clamp(py, loY, hiY);
            py2 = Clamp(py2, oy - lim, oy + lim);
            return new Point2(px, py2);
        }
        var limX = Math.Max(1.0, Math.Abs(py - oy));
        var px2 = Clamp(px, loX, hiX);
        px2 = Clamp(px2, ox - limX, ox + limX);
        return new Point2(px2, py);
    }

    private static Point2 StretchStub(Point2 from, Point2 to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = Geom.Hypot(dx, dy);
        if (len < 1e-9) return to;
        var s = Math.Max(1.0, MinStub / len);
        return new Point2(from.X + dx * s, from.Y + dy * s);
    }

    private static List<Point2> SyncStub(List<Point2> pts, string which, Point2 p, string face)
    {
        pts = pts.ToList();
        var n = pts.Count;
        var alongY = face is "left" or "right";
        if (n < 3)
        {
            if (which == "start") pts[0] = p;
            else pts[^1] = p;
            return pts;
        }
        if (which == "start")
        {
            var inner = pts[1];
            inner = alongY ? new Point2(inner.X, p.Y) : new Point2(p.X, inner.Y);
            inner = StretchStub(p, inner);
            pts[0] = p;
            pts[1] = inner;
        }
        else
        {
            var inner = pts[^2];
            inner = alongY ? new Point2(inner.X, p.Y) : new Point2(p.X, inner.Y);
            inner = StretchStub(p, inner);
            pts[^1] = p;
            pts[^2] = inner;
        }
        return pts;
    }

    private static List<Point2> ConstrainAttach(Rect fromR, Rect toR, List<Point2> pts)
    {
        if (pts.Count < 2) return pts;
        var s = SlideToCone(fromR, pts[0], pts[1]);
        var e = SlideToCone(toR, pts[^1], pts[^2]);
        pts = SyncStub(pts, "start", s, CurveEngine.FaceOf(fromR, s));
        return SyncStub(pts, "end", e, CurveEngine.FaceOf(toR, e));
    }

    private static double FaceAlong(string face, Point2 p) => face is "top" or "bottom" ? p.X : p.Y;

    private static Point2 SetFaceAlong(string face, Point2 p, double v) =>
        face is "top" or "bottom" ? new Point2(v, p.Y) : new Point2(p.X, v);

    private static (double lo, double hi) FaceLimits(Rect r, string face)
    {
        const double pad = 0.12;
        return face is "top" or "bottom"
            ? (r.X + pad * r.W, r.Right - pad * r.W)
            : (r.Y + pad * r.H, r.Bottom - pad * r.H);
    }

    private static IEnumerable<PortAttach> SpreadGroup(Rect r, string face, List<PortAttach> items)
    {
        if (items.Count < 2) return items;
        var (lo, hi) = FaceLimits(r, face);
        var ordered = items.OrderBy(it => FaceAlong(face, it.P)).ToList();
        var step = (hi - lo) / (ordered.Count + 1);
        return ordered.Select((it, k) => it with { P = SetFaceAlong(face, it.P, lo + (k + 1) * step) });
    }

    private static List<SceneEdge> ApplyPorts(List<SceneEdge> edges, IEnumerable<PortAttach> placements)
    {
        var copy = edges.Select(e => new SceneEdge
        {
            From = e.From, To = e.To, Kind = e.Kind, Violating = e.Violating, Head = e.Head,
            Points = e.Points.ToList(), Deps = e.Deps.ToList(),
        }).ToList();
        foreach (var pl in placements)
        {
            var e = copy[pl.Index];
            var pts = e.Points.ToList();
            pts = SyncStub(pts, pl.Which, pl.P, pl.Face);
            e.Points = pts;
        }
        return copy;
    }

    private static List<SceneEdge> SeparatePorts(List<SceneEdge> edges, Dictionary<string, SceneClass> idx)
    {
        var atts = new List<PortAttach>();
        for (var i = 0; i < edges.Count; i++)
        {
            var pts = edges[i].Points;
            if (pts.Count < 2) continue;
            atts.Add(new PortAttach(i, "start", edges[i].From, pts[0], ""));
            atts.Add(new PortAttach(i, "end", edges[i].To, pts[^1], ""));
        }
        var withFaces = atts.Select(a =>
        {
            if (idx.TryGetValue(a.Id, out var c))
                return a with { Face = CurveEngine.FaceOf(c.Rect, a.P) };
            return a;
        }).ToList();
        var placed = withFaces
            .GroupBy(a => (a.Id, a.Face))
            .SelectMany(g =>
            {
                if (!idx.TryGetValue(g.Key.Id, out var c)) return g;
                return SpreadGroup(c.Rect, g.Key.Face, g.ToList());
            });
        return ApplyPorts(edges, placed);
    }

    private static Dictionary<int, double> PortT(List<Annotated> annotated, bool lr, bool fromSide)
    {
        var groups = fromSide
            ? annotated.GroupBy(a => a.Edge.From)
            : annotated.GroupBy(a => a.Edge.To);
        var result = new Dictionary<int, double>();
        var indexMap = annotated.Select((a, i) => (a, i)).ToDictionary(x => x.a, x => x.i);
        foreach (var g in groups)
        {
            var sorted = g.OrderBy(a => lr
                ? (fromSide ? a.To.Rect.Cy : a.From.Rect.Cy)
                : (fromSide ? a.To.Rect.Cx : a.From.Rect.Cx)).ToList();
            for (var i = 0; i < sorted.Count; i++)
                result[indexMap[sorted[i]]] = (i + 1.0) / (sorted.Count + 1);
        }
        return result;
    }

    public static LayoutScene Route(LayoutScene scene)
    {
        var idx = scene.Classes.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var classes = scene.Classes;
        var lr = Lr(scene);
        var annotated = scene.Diagram.Edges
            .Select(e => new Annotated(e, idx.GetValueOrDefault(e.From)!, idx.GetValueOrDefault(e.To)!))
            .Where(a => LaidOut(a.From) && LaidOut(a.To))
            .ToList();
        var fromT = PortT(annotated, lr, true);
        var toT = PortT(annotated, lr, false);
        var hopGroups = annotated.GroupBy(a => (Math.Min(a.From.Rank, a.To.Rank), Math.Max(a.From.Rank, a.To.Rank)));
        var hopIndex = new Dictionary<Annotated, (int i, int n)>();
        foreach (var g in hopGroups)
        {
            var sorted = g.OrderBy(a => a.Edge.From).ThenBy(a => a.Edge.To).ToList();
            for (var i = 0; i < sorted.Count; i++)
                hopIndex[sorted[i]] = (i, sorted.Count);
        }

        var edges = new List<SceneEdge>();
        for (var ai = 0; ai < annotated.Count; ai++)
        {
            var a = annotated[ai];
            var ft = fromT.GetValueOrDefault(ai, 0.5);
            var tt = toT.GetValueOrDefault(ai, 0.5);
            var (hi, hn) = hopIndex.GetValueOrDefault(a, (0, 1));
            var same = !Stacked(a.From, a.To, lr);
            List<Point2>? raw;
            if (same)
            {
                raw = TryPaths(SameRankCandidates(a.From, a.To, ft, tt, hi, classes, lr),
                    a.From.Id, a.To.Id, classes, idx);
            }
            else
            {
                raw = TryPaths(HopCandidates(a.From, a.To, ft, tt, hi, hn, classes, lr),
                    a.From.Id, a.To.Id, classes, idx);
            }
            raw ??= Collapse([Port(a.From, FlowFaces(a.From, a.To, lr).fromFace, ft),
                Port(a.To, FlowFaces(a.From, a.To, lr).toFace, tt)]);
            var pts = ConstrainAttach(a.From.Rect, a.To.Rect, raw);
            edges.Add(new SceneEdge
            {
                From = a.Edge.From,
                To = a.Edge.To,
                Kind = a.Edge.Kind,
                Violating = a.Edge.Violating,
                Points = pts,
                Head = Head(a.Edge.Kind),
                Deps = a.Edge.Deps.Count > 0
                    ? a.Edge.Deps.ToList()
                    : [new LeafDep { From = a.Edge.From, To = a.Edge.To, Violating = a.Edge.Violating }],
            });
        }

        scene.Edges = SeparatePorts(edges, idx);
        return scene;
    }
}
