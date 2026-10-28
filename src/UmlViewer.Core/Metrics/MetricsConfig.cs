namespace UmlViewer.Core.Metrics;

public static class MetricsConfig
{
    private const double GradeBest = 10.0;
    private const double GradeMid = 5.5;
    private const double GradeWorst = 1.0;

    private const double CrapGreen = 8;
    private const double CrapYellow = 12;
    private const double CrapRed = 20;

    private const double MutRed = 0.80;
    private const double MutYellow = 0.90;
    private const double MutGreen = 1.00;

    public static double? CrapRisk(Models.IrCrapSummary? crap) =>
        crap?.Mu is { } mu ? mu + crap.Sigma : null;

    public static double CrapGrade(double? score)
    {
        if (score == null)
            return GradeWorst;
        return Along(score.Value, CrapGreen, CrapYellow, CrapRed, GradeBest, GradeMid, GradeWorst);
    }

    public static double? MutationRatio(int? killed, int? survived, int mutGap = 0)
    {
        var k = killed ?? 0;
        var s = survived ?? 0;
        var gap = mutGap;
        if (k == 0 && s == 0 && gap == 0)
            return null;
        var n = k + s + gap;
        return n > 0 ? (double)k / n : null;
    }

    public static double MutationGrade(double? ratio)
    {
        if (ratio == null)
            return GradeWorst;
        return Along(ratio.Value, MutRed, MutYellow, MutGreen, GradeWorst, GradeMid, GradeBest);
    }

    public static double? CombinedGrade(double? crapGrade, double? mutGrade)
    {
        var xs = new List<double>();
        if (crapGrade != null)
            xs.Add(crapGrade.Value);
        if (mutGrade != null)
            xs.Add(mutGrade.Value);
        return xs.Count == 0 ? null : xs.Average();
    }

    public static Models.IrCrapSummary? SummarizeScores(IEnumerable<double> scores)
    {
        var xs = scores.ToList();
        if (xs.Count == 0)
            return null;
        var mu = xs.Average();
        var max = xs.Max();
        var var = xs.Sum(x =>
        {
            var d = x - mu;
            return d * d;
        }) / xs.Count;
        return new Models.IrCrapSummary
        {
            Mu = Round1(mu),
            Max = Round1(max),
            Sigma = Round1(Math.Sqrt(var)),
            N = xs.Count,
        };
    }

    private static double Along(double x, double x0, double x1, double x2, double y0, double y1, double y2)
    {
        if (x <= x0)
            return y0;
        if (x <= x1)
            return Lerp(x, x0, x1, y0, y1);
        if (x <= x2)
            return Lerp(x, x1, x2, y1, y2);
        return y2;
    }

    private static double Lerp(double x, double x0, double x1, double y0, double y1)
    {
        if (Math.Abs(x0 - x1) < 1e-9)
            return y0;
        var t = (x - x0) / (x1 - x0);
        return y0 + t * (y1 - y0);
    }

    private static double Round1(double x) =>
        Math.Round(x * 10, MidpointRounding.AwayFromZero) / 10.0;
}
