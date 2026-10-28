using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Io;

public static partial class PolicyLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ScanPolicy LoadPolicy(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var text = File.ReadAllText(path);
        return ext switch
        {
            ".json" => JsonSerializer.Deserialize<ScanPolicy>(text, JsonOptions)
                       ?? throw new InvalidOperationException("Empty policy."),
            ".edn" => LoadEdnPolicy(text),
            _ => throw new NotSupportedException($"Policy format '{ext}' is not supported. Use .json or .edn."),
        };
    }

    private static ScanPolicy LoadEdnPolicy(string text)
    {
        var policy = new ScanPolicy();
        if (TryReadString(text, ":title", out var title))
            policy.Title = title;
        if (TryReadString(text, ":prefix", out var prefix))
            policy.Prefix = prefix;
        if (text.Contains(":hierarchical true", StringComparison.Ordinal))
            policy.Hierarchical = true;

        foreach (Match m in SourceBlockPattern().Matches(text))
        {
            policy.Sources.Add(new ScanSource
            {
                Lang = m.Groups[1].Value,
                Root = m.Groups[2].Value.Replace('\\', '/'),
            });
        }

        if (policy.Sources.Count == 0 && TryReadString(text, ":lang", out var lang))
        {
            var root = TryReadString(text, ":src", out var src) ? src :
                TryReadString(text, ":root", out var r) ? r : ".";
            policy.Sources.Add(new ScanSource { Lang = lang, Root = root });
        }

        return policy;
    }

    private static bool TryReadString(string text, string key, out string value)
    {
        var pattern = key + @"\s+""([^""]+)""";
        var m = Regex.Match(text, pattern);
        if (!m.Success)
        {
            value = "";
            return false;
        }

        value = m.Groups[1].Value;
        return true;
    }

    [GeneratedRegex(@":lang\s+:(\w+)[^}]*?:root\s+""([^""]+)""", RegexOptions.Singleline)]
    private static partial Regex SourceBlockPattern();
}

public static class IrJson
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Write(string path, IrDocument doc) =>
        File.WriteAllText(path, JsonSerializer.Serialize(doc, WriteOptions));

    public static IrDocument Read(string path)
    {
        var doc = JsonSerializer.Deserialize<IrDocument>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        return doc ?? throw new InvalidOperationException("Empty IR file.");
    }
}
