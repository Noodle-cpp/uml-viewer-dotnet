using Avalonia.Media;

namespace UmlViewer.App.Rendering;

public static class ViewerTheme
{
    public static readonly Color Bg = Color.FromRgb(22, 28, 32);
    public static readonly Color Panel = Color.FromRgb(26, 36, 40);
    public static readonly Color Ink = Color.FromRgb(236, 236, 228);
    public static readonly Color Muted = Color.FromRgb(157, 184, 168);
    public static readonly Color Gold = Color.FromRgb(232, 196, 72);
    public static readonly Color Line = Color.FromRgb(42, 61, 54);
    public static readonly Color Good = Color.FromRgb(95, 181, 138);
    public static readonly Color Violation = Color.FromRgb(196, 42, 36);

    public static Color FillForGrade(double? grade)
    {
        if (grade == null)
            return Color.FromRgb(36, 52, 48);
        var g = grade.Value;
        if (g <= 5)
            return Lerp(Color.FromRgb(74, 40, 24), Color.FromRgb(61, 58, 24), g / 5);
        return Lerp(Color.FromRgb(61, 58, 24), Color.FromRgb(30, 74, 56), Math.Min(1, (g - 5) / 5));
    }

    public static Color StrokeForGrade(double? grade)
    {
        if (grade == null)
            return Color.FromRgb(90, 110, 100);
        var g = grade.Value;
        if (g <= 5)
            return Lerp(Color.FromRgb(224, 122, 74), Color.FromRgb(212, 192, 90), g / 5);
        return Lerp(Color.FromRgb(212, 192, 90), Good, Math.Min(1, (g - 5) / 5));
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        byte Mix(byte x, byte y) => (byte)(x + t * (y - x));
        return Color.FromRgb(Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B));
    }
}
