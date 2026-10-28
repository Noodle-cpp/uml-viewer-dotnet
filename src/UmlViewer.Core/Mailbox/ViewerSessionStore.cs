using EdnParser;
using UmlViewer.Core.Edn;

namespace UmlViewer.Core.Mailbox;

public sealed class ViewerSession
{
    public IReadOnlyList<string> Focus { get; init; } = [];
    public string? ProposalId { get; init; }
    public bool Proposal { get; init; }
    public string? OpenLayer { get; init; }
    public string? Declutter { get; init; }
    public double CamX { get; init; }
    public double CamY { get; init; }
    public double Zoom { get; init; } = 1;
    public string? Selected { get; init; }
    public string? DetailId { get; init; }
}

public static class ViewerSessionStore
{
    public static ViewerSession? Read(string metricsRoot)
    {
        var path = MailboxPaths.SessionFile(metricsRoot);
        if (!File.Exists(path))
            return null;
        try
        {
            var tokens = new Parser().Parse(File.ReadAllText(path));
            var root = tokens.FirstOrDefault(t => t is not EdnComment) as EdnMap;
            if (root == null)
                return null;
            return new ViewerSession
            {
                Focus = ReadStringList(EdnValueExtensions.GetVector(root, "focus")),
                ProposalId = EdnValueExtensions.GetString(root, "proposal-id"),
                Proposal = EdnValueExtensions.GetBool(root, "proposal"),
                OpenLayer = EdnValueExtensions.GetString(root, "open-layer"),
                Declutter = EdnValueExtensions.GetString(root, "declutter"),
                CamX = ReadDouble(root, "cam-x"),
                CamY = ReadDouble(root, "cam-y"),
                Zoom = ReadDouble(root, "zoom", 1),
                Selected = EdnValueExtensions.GetString(root, "selected"),
                DetailId = EdnValueExtensions.GetString(root, "detail-id"),
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string metricsRoot, ViewerSession session)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{:focus [");
        for (var i = 0; i < session.Focus.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(':');
            sb.Append(session.Focus[i]);
        }

        sb.Append("]");
        if (!string.IsNullOrEmpty(session.ProposalId))
        {
            sb.Append(", :proposal-id :");
            sb.Append(session.ProposalId.TrimStart(':'));
            sb.Append(", :proposal true");
        }

        if (!string.IsNullOrEmpty(session.OpenLayer))
        {
            sb.Append(", :open-layer :");
            sb.Append(session.OpenLayer.TrimStart(':'));
        }

        if (!string.IsNullOrEmpty(session.Declutter))
        {
            sb.Append(", :declutter :");
            sb.Append(session.Declutter.TrimStart(':'));
        }

        sb.Append(", :cam-x ");
        sb.Append(session.CamX.ToString(System.Globalization.CultureInfo.InvariantCulture));
        sb.Append(", :cam-y ");
        sb.Append(session.CamY.ToString(System.Globalization.CultureInfo.InvariantCulture));
        sb.Append(", :zoom ");
        sb.Append(session.Zoom.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(session.Selected))
        {
            sb.Append(", :selected :");
            sb.Append(session.Selected.TrimStart(':'));
        }

        if (!string.IsNullOrEmpty(session.DetailId))
        {
            sb.Append(", :detail-id :");
            sb.Append(session.DetailId.TrimStart(':'));
        }

        sb.Append('}');
        Directory.CreateDirectory(MailboxPaths.MailboxDir(metricsRoot));
        MailboxService.AtomicWrite(MailboxPaths.SessionFile(metricsRoot), sb.ToString());
    }

    private static List<string> ReadStringList(EdnVector? vector)
    {
        var list = new List<string>();
        if (vector == null)
            return list;
        foreach (var item in vector.Values())
        {
            var s = item switch
            {
                EdnKeyword kw => kw.KeywordName(),
                EdnSymbol sym => sym.ToString() ?? "",
                EdnString str => str.ToString() ?? "",
                _ => item.ToString()?.TrimStart(':') ?? "",
            };
            if (s.Length > 0)
                list.Add(s);
        }

        return list;
    }

    private static double ReadDouble(EdnMap map, string key, double fallback = 0)
    {
        foreach (var kv in map.Entries())
        {
            if (kv.Key.KeywordName() != key)
                continue;
            return kv.Value switch
            {
                EdnInteger n => Convert.ToDouble(n.Value),
                _ when double.TryParse(kv.Value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
                _ => fallback,
            };
        }

        return fallback;
    }
}
