namespace UmlViewer.Core.Edn;

/// <summary>
/// Replace or insert a top-level map key whose value is a vector, preserving other file content.
/// </summary>
public static class EdnRootMapPatcher
{
    public static void SetVectorKey(string filePath, string key, string vectorEdn)
    {
        var text = File.ReadAllText(filePath);
        var keyToken = ":" + key;
        var searchFrom = 0;
        while (searchFrom < text.Length)
        {
            var idx = text.IndexOf(keyToken, searchFrom, StringComparison.Ordinal);
            if (idx < 0)
                break;
            if (!IsKeyAt(text, idx, keyToken))
            {
                searchFrom = idx + 1;
                continue;
            }

            var valueStart = SkipWhitespace(text, idx + keyToken.Length);
            if (valueStart >= text.Length || text[valueStart] != '[')
            {
                searchFrom = idx + 1;
                continue;
            }

            var end = FindMatchingBracket(text, valueStart, '[', ']');
            if (end < 0)
                break;
            var updated = text[..valueStart] + vectorEdn + text[(end + 1)..];
            File.WriteAllText(filePath, updated);
            return;
        }

        InsertBeforeRootClose(text, filePath, key, vectorEdn);
    }

    private static void InsertBeforeRootClose(string text, string filePath, string key, string vectorEdn)
    {
        var open = text.IndexOf('{');
        if (open < 0)
            throw new InvalidOperationException("EDN root map not found.");
        var close = FindMatchingBracket(text, open, '{', '}');
        if (close < 0)
            throw new InvalidOperationException("Unclosed EDN root map.");
        var insert = ", :" + key + " " + vectorEdn;
        var updated = text[..close] + insert + text[close..];
        File.WriteAllText(filePath, updated);
    }

    private static bool IsKeyAt(string text, int idx, string keyToken)
    {
        if (idx > 0)
        {
            var prev = text[idx - 1];
            if (char.IsLetterOrDigit(prev) || prev is ':' or '_' or '.')
                return false;
        }

        return text.AsSpan(idx, keyToken.Length).SequenceEqual(keyToken.AsSpan());
    }

    private static int SkipWhitespace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
            i++;
        return i;
    }

    private static int FindMatchingBracket(string text, int start, char open, char close)
    {
        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escape)
                    escape = false;
                else if (c == '\\')
                    escape = true;
                else if (c == '"')
                    inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == open)
                depth++;
            else if (c == close)
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }
}
