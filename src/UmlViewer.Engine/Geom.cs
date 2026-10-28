namespace UmlViewer.Engine;

public readonly record struct Point2(double X, double Y);

public readonly record struct Rect(double X, double Y, double W, double H)
{
    public double Right => X + W;
    public double Bottom => Y + H;
    public double Cx => X + W / 2;
    public double Cy => Y + H / 2;

    public static Rect From(double x, double y, double w, double h) => new(x, y, w, h);

    public Rect Shift(double dx, double dy) => new(X + dx, Y + dy, W, H);

    public static Rect? Union(IEnumerable<Rect> rects)
    {
        var list = rects.ToList();
        if (list.Count == 0)
            return null;
        var x = list.Min(r => r.X);
        var y = list.Min(r => r.Y);
        var r = list.Max(r => r.Right);
        var b = list.Max(r => r.Bottom);
        return new Rect(x, y, r - x, b - y);
    }
}

public static class Geom
{
    public static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);

    public static Rect? Union(IEnumerable<Rect> rects) => Rect.Union(rects);

    public static bool Inside(Rect r, double x, double y) =>
        r.X <= x && x <= r.Right && r.Y <= y && y <= r.Bottom;

    public static bool Inside(Rect r, Point2 p) => Inside(r, p.X, p.Y);

    public static Point2 IntersectRect(Rect r, Point2 outside)
    {
        var x = r.Cx;
        var y = r.Cy;
        var px = outside.X;
        var py = outside.Y;
        var dx = px - x;
        var dy = py - y;
        var w = r.W / 2.0;
        var h = r.H / 2.0;
        if (Math.Abs(dy) * w > Math.Abs(dx) * h)
        {
            var hh = dy < 0 ? -h : h;
            var sx = Math.Abs(dy) < 1e-12 ? 0.0 : hh * dx / dy;
            return new Point2(x + sx, y + hh);
        }

        var ww = dx < 0 ? -w : w;
        var sy = Math.Abs(dx) < 1e-12 ? 0.0 : ww * dy / dx;
        return new Point2(x + ww, y + sy);
    }

    public static bool SegmentHitsRect(Point2 a, Point2 b, Rect r)
    {
        var xmin = Math.Min(a.X, b.X);
        var xmax = Math.Max(a.X, b.X);
        var ymin = Math.Min(a.Y, b.Y);
        var ymax = Math.Max(a.Y, b.Y);
        return xmin < r.Right && xmax > r.X && ymin < r.Bottom && ymax > r.Y;
    }

    public static bool Overlaps(Rect a, Rect b) =>
        a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;

    public static Rect Inflate(Rect r, double pad) =>
        new(r.X - pad, r.Y - pad, r.W + 2 * pad, r.H + 2 * pad);

    private static Point2 Lerp(Point2 a, Point2 b, double t) =>
        new(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));

    private static (double u1, double u2)? ClipParam((double u1, double u2) uv, double p, double q)
    {
        if (Math.Abs(p) < 1e-12)
            return q >= 0 ? uv : null;
        var t = q / p;
        if (p < 0)
            return t > uv.u2 ? null : (Math.Max(uv.u1, t), uv.u2);
        return t < uv.u1 ? null : (uv.u1, Math.Min(uv.u2, t));
    }

    private static (double u1, double u2)? OverlapParam(Point2 a, Point2 b, Rect r)
    {
        var x1 = a.X;
        var y1 = a.Y;
        var x2 = b.X;
        var y2 = b.Y;
        var dx = x2 - x1;
        var dy = y2 - y1;
        var edges = new[]
        {
            (-dx, x1 - r.X),
            (dx, r.Right - x1),
            (-dy, y1 - r.Y),
            (dy, r.Bottom - y1),
        };
        (double u1, double u2) uv = (0, 1);
        foreach (var (p, q) in edges)
        {
            var next = ClipParam(uv, p, q);
            if (next == null)
                return null;
            uv = next.Value;
        }

        return uv;
    }

    private static List<(double a, double b)> MergeIntervals(List<(double a, double b)> ivs)
    {
        var sorted = ivs.Where(iv => iv.a < iv.b).OrderBy(iv => iv.a).ToList();
        var acc = new List<(double a, double b)>();
        foreach (var iv in sorted)
        {
            if (acc.Count == 0)
            {
                acc.Add(iv);
                continue;
            }

            var last = acc[^1];
            if (iv.a <= last.b + 1e-9)
                acc[^1] = (last.a, Math.Max(last.b, iv.b));
            else
                acc.Add(iv);
        }

        return acc;
    }

    private static List<(double u0, double u1)> KeptParams(List<(double a, double b)> removed)
    {
        var ivs = MergeIntervals(removed);
        var outList = new List<(double, double)>();
        var u = 0.0;
        foreach (var (a, b) in ivs)
        {
            if (u < a - 1e-9)
                outList.Add((u, a));
            u = Math.Max(u, b);
        }

        if (u < 0.999999)
            outList.Add((u, 1.0));
        return outList;
    }

    private static bool Near(Point2 a, Point2 b) =>
        Geom.Hypot(a.X - b.X, a.Y - b.Y) < 0.51;

    public static List<(Point2 a, Point2 b)> GapSegment(Point2 a, Point2 b, IEnumerable<Rect> rects, double pad)
    {
        var rectList = rects.ToList();
        if (rectList.Count == 0 || Near(a, b))
            return Near(a, b) ? [] : new List<(Point2, Point2)> { (a, b) };

        var removed = rectList
            .Select(r => OverlapParam(a, b, Inflate(r, pad)))
            .Where(x => x != null)
            .Select(x => x!.Value)
            .ToList();

        if (removed.Count == 0)
            return new List<(Point2, Point2)> { (a, b) };

        return KeptParams(removed)
            .Select(uv => (Lerp(a, b, uv.u0), Lerp(a, b, uv.u1)))
            .Where(pq => !Near(pq.Item1, pq.Item2))
            .Select(pq => (pq.Item1, pq.Item2))
            .ToList();
    }

    public static List<List<Point2>> GapPolyline(IReadOnlyList<Point2> pts, IEnumerable<Rect> rects, double pad)
    {
        if (pts.Count < 2)
            return [];
        var rectList = rects.ToList();
        if (rectList.Count == 0)
            return [pts.ToList()];

        var paths = new List<List<Point2>>();
        foreach (var seg in pts.Zip(pts.Skip(1), (a, b) => (a, b)))
        {
            foreach (var piece in GapSegment(seg.a, seg.b, rectList, pad))
            {
                if (paths.Count > 0 && Near(paths[^1][^1], piece.a))
                    paths[^1].Add(piece.b);
                else
                    paths.Add([piece.a, piece.b]);
            }
        }

        return paths;
    }

    public static double PointSegDist(Point2 p, Point2 a, Point2 b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-12)
            return Geom.Hypot(p.X - a.X, p.Y - a.Y);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        var qx = a.X + t * dx;
        var qy = a.Y + t * dy;
        return Geom.Hypot(p.X - qx, p.Y - qy);
    }

    public static bool NearPolyline(Point2 p, IReadOnlyList<Point2> pts, double pad) =>
        pts.Zip(pts.Skip(1), (a, b) => (a, b)).Any(seg => PointSegDist(p, seg.a, seg.b) <= pad);

    private static double TriSign(Point2 p, Point2 a, Point2 b) =>
        (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);

    public static bool PointInTriangle(Point2 p, IReadOnlyList<Point2> tri)
    {
        var d1 = TriSign(p, tri[0], tri[1]);
        var d2 = TriSign(p, tri[1], tri[2]);
        var d3 = TriSign(p, tri[2], tri[0]);
        var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    public static bool NearTriangle(Point2 p, IReadOnlyList<Point2> pts, double pad)
    {
        if (PointInTriangle(p, pts))
            return true;
        var closed = pts.Concat([pts[0]]).ToList();
        return NearPolyline(p, closed, pad);
    }
}
