using System.Globalization;
using System.Text;
using EdnParser;
using UmlViewer.Core.Edn;

namespace UmlViewer.Core.Mailbox;

internal static class MailboxEdn
{
    internal static MailboxEnvelope ReadEnvelope(string path)
    {
        if (!File.Exists(path))
            return new MailboxEnvelope();
        try
        {
            var text = File.ReadAllText(path);
            var tokens = new Parser().Parse(text);
            var root = tokens.FirstOrDefault(t => t is not EdnComment) as EdnMap;
            if (root == null)
                return ReadLegacySingleCommand(text);
            var nextId = ReadLong(root, "next-id", 1);
            var queue = ReadQueue(UmlViewer.Core.Edn.EdnValueExtensions.GetVector(root, "queue"));
            return new MailboxEnvelope { NextId = nextId, Queue = queue };
        }
        catch
        {
            return new MailboxEnvelope();
        }
    }

    private static MailboxEnvelope ReadLegacySingleCommand(string text)
    {
        try
        {
            var tokens = new Parser().Parse(text);
            var root = tokens.FirstOrDefault(t => t is not EdnComment) as EdnMap;
            if (root == null || !root.Entries().Any(e => KeyName(e.Key) == "op"))
                return new MailboxEnvelope();
            var cmd = MapCommand(root);
            return new MailboxEnvelope
            {
                NextId = cmd.Id + 1,
                Queue = [cmd],
            };
        }
        catch
        {
            return new MailboxEnvelope();
        }
    }

    private static IReadOnlyList<MailboxCommand> ReadQueue(EdnVector? vector)
    {
        var list = new List<MailboxCommand>();
        if (vector == null)
            return list;
        foreach (var item in vector.Values())
        {
            if (item is EdnMap map)
                list.Add(MapCommand(map));
        }

        return list;
    }

    private static MailboxCommand MapCommand(EdnMap map)
    {
        var id = ReadLong(map, "id", 0);
        var op = UmlViewer.Core.Edn.EdnValueExtensions.GetString(map, "op") ?? "";
        var extra = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var kv in map.Entries())
        {
            var key = KeyName(kv.Key);
            if (key is "id" or "op")
                continue;
            extra[key] = EdnToObject(kv.Value);
        }

        return new MailboxCommand { Id = id, Op = op, Extra = extra };
    }

    private static object? EdnToObject(EdnValue value) =>
        value switch
        {
            EdnKeyword kw => kw.ToString()?.TrimStart(':'),
            EdnSymbol sym => sym.ToString(),
            EdnString str => str.ToString(),
            EdnInteger n => Convert.ToInt64(n.Value),
            EdnMap map => map.Entries().ToDictionary(e => KeyName(e.Key), e => EdnToObject(e.Value)),
            EdnVector vec => vec.Values().Select(EdnToObject).ToList(),
            _ => value.ToString(),
        };

    internal static long ReadLong(EdnMap map, string key, long fallback)
    {
        foreach (var kv in map.Entries())
        {
            if (KeyName(kv.Key) != key)
                continue;
            return kv.Value switch
            {
                EdnInteger n => Convert.ToInt64(n.Value),
                _ when long.TryParse(kv.Value.ToString(), out var n) => n,
                _ => fallback,
            };
        }

        return fallback;
    }

    internal static string KeyName(EdnValue key) =>
        key switch
        {
            EdnKeyword kw => kw.ToString()?.TrimStart(':') ?? "",
            EdnSymbol sym => sym.ToString() ?? "",
            _ => key.ToString()?.TrimStart(':') ?? "",
        };

    internal static string WriteEnvelope(MailboxEnvelope box)
    {
        var sb = new StringBuilder();
        sb.Append("{:next-id ");
        sb.Append(box.NextId.ToString(CultureInfo.InvariantCulture));
        sb.Append(", :queue [");
        for (var i = 0; i < box.Queue.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            WriteCommand(sb, box.Queue[i]);
        }

        sb.Append("]}");
        return sb.ToString();
    }

    internal static void WriteCommand(StringBuilder sb, MailboxCommand cmd)
    {
        sb.Append("{:id ");
        sb.Append(cmd.Id.ToString(CultureInfo.InvariantCulture));
        sb.Append(", :op ");
        WriteKeyword(sb, cmd.Op);
        foreach (var kv in cmd.Extra)
        {
            sb.Append(", :");
            sb.Append(kv.Key);
            sb.Append(' ');
            WriteValue(sb, kv.Value);
        }

        sb.Append('}');
    }

    private static void WriteValue(StringBuilder sb, object? value)
    {
        switch (value)
        {
            case null:
                sb.Append("nil");
                break;
            case string s when LooksLikeKeyword(s):
                WriteKeyword(sb, s);
                break;
            case string s:
                sb.Append('"');
                sb.Append(EscapeString(s));
                sb.Append('"');
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case long l:
                sb.Append(l.ToString(CultureInfo.InvariantCulture));
                break;
            case int n:
                sb.Append(n.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                sb.Append(d.ToString(CultureInfo.InvariantCulture));
                break;
            case IDictionary<string, object?> map:
                sb.Append('{');
                var first = true;
                foreach (var kv in map)
                {
                    if (!first)
                        sb.Append(", ");
                    first = false;
                    sb.Append(':');
                    sb.Append(kv.Key);
                    sb.Append(' ');
                    WriteValue(sb, kv.Value);
                }

                sb.Append('}');
                break;
            case IEnumerable<object?> list:
                sb.Append('[');
                var firstItem = true;
                foreach (var item in list)
                {
                    if (!firstItem)
                        sb.Append(' ');
                    firstItem = false;
                    WriteValue(sb, item);
                }

                sb.Append(']');
                break;
            default:
                sb.Append('"');
                sb.Append(EscapeString(value.ToString() ?? ""));
                sb.Append('"');
                break;
        }
    }

    private static bool LooksLikeKeyword(string s) =>
        s.Length > 0 && !s.Contains(' ') && char.IsLetter(s[0]);

    private static void WriteKeyword(StringBuilder sb, string op)
    {
        var name = op.TrimStart(':');
        sb.Append(':');
        sb.Append(name);
    }

    private static string EscapeString(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
