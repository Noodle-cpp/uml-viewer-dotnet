namespace UmlViewer.Engine;

public enum PathOpKind { Line, Cubic }

public sealed class PathOp
{
    public PathOpKind Kind { get; init; }
    public Point2 P { get; set; }
    public Point2 C1 { get; set; }
    public Point2 C2 { get; set; }
}

public sealed class BasisPath
{
    public Point2 Start { get; set; }
    public List<PathOp> Ops { get; set; } = [];
}

public static class CurveEngine
{
    public const double CornerRadius = 24.0;

    private static Point2 VSub(Point2 a, Point2 b) => new(a.X - b.X, a.Y - b.Y);
    private static Point2 VAdd(Point2 a, Point2 b) => new(a.X + b.X, a.Y + b.Y);
    private static Point2 VScale(Point2 v, double s) => new(v.X * s, v.Y * s);
    private static double VLen(Point2 v) => Geom.Hypot(v.X, v.Y);

    private static Point2 VNorm(Point2 v)
    {
        var l = VLen(v);
        return l < 1e-9 ? new Point2(0, 0) : new Point2(v.X / l, v.Y / l);
    }

    public static List<Point2> RoundCorners(IReadOnlyList<Point2> pts, double radius = CornerRadius)
    {
        var list = pts.ToList();
        var n = list.Count;
        if (n < 3)
            return list;
        var outPts = new List<Point2> { list[0] };
        for (var i = 1; i < n - 1; i++)
        {
            var prev = list[i - 1];
            var cur = list[i];
            var nxt = list[i + 1];
            var a = VSub(cur, prev);
            var b = VSub(nxt, cur);
            var la = VLen(a);
            var lb = VLen(b);
            var r = Math.Min(radius, Math.Min(0.45 * la, 0.45 * lb));
            var na = VNorm(a);
            var nb = VNorm(b);
            var turn = na.X * nb.X + na.Y * nb.Y;
            if (r < 4.0 || turn > 0.92)
                outPts.Add(cur);
            else
            {
                outPts.Add(VAdd(cur, VScale(na, -r)));
                outPts.Add(VAdd(cur, VScale(nb, r)));
            }
        }

        outPts.Add(list[^1]);
        return outPts;
    }

    private static PathOp Cubic(double x0, double y0, double x1, double y1, double x, double y) => new()
    {
        Kind = PathOpKind.Cubic,
        C1 = new Point2((2 * x0 + x1) / 3, (2 * y0 + y1) / 3),
        C2 = new Point2((x0 + 2 * x1) / 3, (y0 + 2 * y1) / 3),
        P = new Point2((x0 + 4 * x1 + x) / 6, (y0 + 4 * y1 + y) / 6),
    };

    private sealed class StepState
    {
        public int Point { get; set; }
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public Point2 Start { get; set; }
        public List<PathOp> Ops { get; } = [];
    }

    private static StepState Step(StepState state, Point2 pt)
    {
        var x = pt.X;
        var y = pt.Y;
        switch (state.Point)
        {
            case 0:
                state.Point = 1;
                state.X0 = x;
                state.Y0 = y;
                state.Start = pt;
                break;
            case 1:
                state.Point = 2;
                state.X1 = x;
                state.Y1 = y;
                break;
            case 2:
            {
                var mx = (5 * state.X0 + state.X1) / 6;
                var my = (5 * state.Y0 + state.Y1) / 6;
                state.Ops.Add(new PathOp { Kind = PathOpKind.Line, P = new Point2(mx, my) });
                state.Ops.Add(Cubic(state.X0, state.Y0, state.X1, state.Y1, x, y));
                state.Point = 3;
                state.X0 = state.X1;
                state.Y0 = state.Y1;
                state.X1 = x;
                state.Y1 = y;
                break;
            }
            default:
                state.Ops.Add(Cubic(state.X0, state.Y0, state.X1, state.Y1, x, y));
                state.X0 = state.X1;
                state.Y0 = state.Y1;
                state.X1 = x;
                state.Y1 = y;
                break;
        }

        return state;
    }

    private static BasisPath Finish(StepState state)
    {
        var ops = state.Ops;
        if (state.Point >= 3 && ops.Count > 0)
        {
            var last = ops[^1];
            ops[^1] = new PathOp
            {
                Kind = last.Kind,
                C1 = last.C1,
                C2 = last.C2,
                P = new Point2(state.X1, state.Y1),
            };
        }
        else if (state.Point == 2)
            ops.Add(new PathOp { Kind = PathOpKind.Line, P = new Point2(state.X1, state.Y1) });

        return new BasisPath { Start = state.Start, Ops = ops };
    }

    public static BasisPath BasisPath(IReadOnlyList<Point2> pts)
    {
        var rounded = RoundCorners(pts);
        var n = rounded.Count;
        if (n == 0)
            return new BasisPath { Start = new Point2(0, 0) };
        if (n == 1)
            return new BasisPath { Start = rounded[0] };
        var state = new StepState();
        foreach (var p in rounded)
            Step(state, p);
        return Finish(state);
    }

    public static (Point2 behind, Point2 tip) EndTangent(BasisPath path)
    {
        var cur = path.Start;
        var from = path.Start;
        foreach (var op in path.Ops)
        {
            var nxt = op.P;
            from = op.Kind == PathOpKind.Cubic ? op.C2 : cur;
            cur = nxt;
        }

        return (from, cur);
    }

    public static string FaceOf(Rect r, Point2 p)
    {
        var dl = Math.Abs(p.X - r.X);
        var dr = Math.Abs(p.X - r.Right);
        var dt = Math.Abs(p.Y - r.Y);
        var db = Math.Abs(p.Y - r.Bottom);
        var m = Math.Min(Math.Min(dl, dr), Math.Min(dt, db));
        if (Math.Abs(m - dl) < 1e-9)
            return "left";
        if (Math.Abs(m - dr) < 1e-9)
            return "right";
        if (Math.Abs(m - dt) < 1e-9)
            return "top";
        return "bottom";
    }

    private static Point2 FaceOut(string face) => face switch
    {
        "left" => new(-1, 0),
        "right" => new(1, 0),
        "top" => new(0, -1),
        _ => new(0, 1),
    };

    private static Point2 TravelNormal(string face, bool arriving)
    {
        var o = FaceOut(face);
        return arriving ? new(-o.X, -o.Y) : o;
    }

    private static bool InCone(double along, double plen) => along > 0 && plen <= along;

    private static Point2 ConeUnit(double px, double py, double plen, double nx, double ny) =>
        plen < 1e-9 ? new(-ny, nx) : new(px / plen, py / plen);

    private static Point2 OnCone(double len, double nx, double ny, double sx, double sy)
    {
        var s = 1.0 / Math.Sqrt(2);
        return new(len * (s * nx + s * sx), len * (s * ny + s * sy));
    }

    private static Point2 ClampToFaceCone(double dx, double dy, string face, bool arriving)
    {
        var (nx, ny) = TravelNormal(face, arriving);
        var len = Geom.Hypot(dx, dy);
        if (len < 1e-6)
            return new(nx, ny);
        var ux = dx / len;
        var uy = dy / len;
        var along = ux * nx + uy * ny;
        var px = ux - along * nx;
        var py = uy - along * ny;
        var plen = Geom.Hypot(px, py);
        if (InCone(along, plen))
            return new(dx, dy);
        var (sx, sy) = ConeUnit(px, py, plen, nx, ny);
        return OnCone(len, nx, ny, sx, sy);
    }

    private static BasisPath ConstrainStart(BasisPath path, Rect? r)
    {
        if (r == null || path.Ops.Count == 0 || path.Ops.Count == 1)
            return path;
        var face = FaceOf(r.Value, path.Start);
        var ops = path.Ops.ToList();
        var op = ops[0];
        var s = path.Start;
        if (op.Kind == PathOpKind.Line)
        {
            var (dx, dy) = ClampToFaceCone(op.P.X - s.X, op.P.Y - s.Y, face, false);
            ops[0] = new PathOp { Kind = PathOpKind.Line, P = new Point2(s.X + dx, s.Y + dy) };
        }
        else if (op.Kind == PathOpKind.Cubic)
        {
            var (dx, dy) = ClampToFaceCone(op.C1.X - s.X, op.C1.Y - s.Y, face, false);
            ops[0] = new PathOp { Kind = PathOpKind.Cubic, C1 = new Point2(s.X + dx, s.Y + dy), C2 = op.C2, P = op.P };
        }

        return new BasisPath { Start = path.Start, Ops = ops };
    }

    private static BasisPath ConstrainEnd(BasisPath path, Rect? r)
    {
        if (r == null || path.Ops.Count == 0)
            return path;
        var ops = path.Ops.ToList();
        var op = ops[^1];
        if (op.Kind != PathOpKind.Cubic)
            return path;
        var face = FaceOf(r.Value, op.P);
        var p = op.P;
        var c2 = op.C2;
        var (dx, dy) = ClampToFaceCone(p.X - c2.X, p.Y - c2.Y, face, true);
        ops[^1] = new PathOp { Kind = PathOpKind.Cubic, C1 = op.C1, C2 = new Point2(p.X - dx, p.Y - dy), P = p };
        return new BasisPath { Start = path.Start, Ops = ops };
    }

    public static BasisPath ConstrainEnds(BasisPath path, Rect? startR, Rect? endR) =>
        ConstrainEnd(ConstrainStart(path, startR), endR);

    public static Rect? PathBounds(BasisPath path)
    {
        var pts = new List<Point2> { path.Start };
        foreach (var op in path.Ops)
        {
            if (op.Kind == PathOpKind.Cubic)
            {
                pts.Add(op.C1);
                pts.Add(op.C2);
                pts.Add(op.P);
            }
            else
                pts.Add(op.P);
        }

        if (pts.Count == 0)
            return null;
        var x0 = pts.Min(p => p.X);
        var y0 = pts.Min(p => p.Y);
        return Rect.From(x0, y0, pts.Max(p => p.X) - x0, pts.Max(p => p.Y) - y0);
    }

    private static Point2 CubicAt(Point2 p0, Point2 c1, Point2 c2, Point2 p3, double t)
    {
        var u = 1 - t;
        var a = u * u * u;
        var b = 3 * u * u * t;
        var c = 3 * u * t * t;
        var d = t * t * t;
        return new Point2(
            a * p0.X + b * c1.X + c * c2.X + d * p3.X,
            a * p0.Y + b * c1.Y + c * c2.Y + d * p3.Y);
    }

    public static List<Point2> FlattenPath(BasisPath path, double step = 0.0625)
    {
        var outPts = new List<Point2> { path.Start };
        var cur = path.Start;
        foreach (var op in path.Ops)
        {
            var nxt = op.P;
            if (op.Kind == PathOpKind.Cubic)
            {
                for (var t = step; t <= 1.0000001; t += step)
                    outPts.Add(CubicAt(cur, op.C1, op.C2, nxt, t));
            }
            else
                outPts.Add(nxt);
            cur = nxt;
        }

        return outPts;
    }
}
