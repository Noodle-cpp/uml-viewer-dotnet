using EdnParser;

namespace UmlViewer.Core.Edn;

public static class EdnValueExtensions
{
    public static EdnMap RequireMap(this EdnValue value) =>
        value as EdnMap ?? throw new InvalidOperationException($"Expected map, got {value.GetType().Name}.");

    public static EdnVector RequireVector(this EdnValue value) =>
        value as EdnVector ?? throw new InvalidOperationException($"Expected vector, got {value.GetType().Name}.");

    public static string KeywordName(this EdnValue key)
    {
        var s = key.ToString() ?? "";
        return s.StartsWith(':') ? s[1..] : s;
    }

    public static string? GetString(EdnMap map, string keyword)
    {
        foreach (var kv in map.Entries())
        {
            if (kv.Key.KeywordName() != keyword)
                continue;
            return kv.Value switch
            {
                EdnString str => Unquote(str.ToString() ?? ""),
                EdnKeyword kw => kw.KeywordName(),
                EdnSymbol sym => sym.ToString(),
                _ => kv.Value.ToString(),
            };
        }

        return null;
    }

    public static bool GetBool(EdnMap map, string keyword, bool defaultValue = false)
    {
        foreach (var kv in map.Entries())
        {
            if (kv.Key.KeywordName() != keyword)
                continue;
            return kv.Value switch
            {
                EdnBoolean b => b.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true,
                _ => defaultValue,
            };
        }

        return defaultValue;
    }

    public static EdnVector? GetVector(EdnMap map, string keyword)
    {
        foreach (var kv in map.Entries())
        {
            if (kv.Key.KeywordName() == keyword && kv.Value is EdnVector vec)
                return vec;
        }

        return null;
    }

    public static EdnMap? GetMap(EdnMap map, string keyword)
    {
        foreach (var kv in map.Entries())
        {
            if (kv.Key.KeywordName() == keyword && kv.Value is EdnMap m)
                return m;
        }

        return null;
    }

    public static IEnumerable<EdnMap> MapEntries(EdnVector? vector)
    {
        if (vector == null)
            yield break;
        foreach (var item in vector.Values())
        {
            if (item is EdnMap m)
                yield return m;
        }
    }

    private static string Unquote(string s)
    {
        if (s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"'))
            return s[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
        return s;
    }
}
