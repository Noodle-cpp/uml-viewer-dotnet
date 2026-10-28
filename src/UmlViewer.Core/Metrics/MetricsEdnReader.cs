using System.Globalization;
using System.Text.RegularExpressions;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Metrics;

/// <summary>Minimal EDN reader for crapper/mutator snapshot files (stable keyword maps).</summary>
public static partial class MetricsEdnReader
{
    [GeneratedRegex(
        @"\{[^\{\}]*?:name\s+""([^""]+)""[^\{\}]*?:namespace\s+""([^""]+)""[^\{\}]*?:complexity\s+(\d+)[^\{\}]*?:coverage\s+([\d.]+)[^\{\}]*?:crap\s+([\d.]+)",
        RegexOptions.Singleline)]
    private static partial Regex CrapEntryPattern();

    [GeneratedRegex(@":namespace\s+""([^""]+)""")]
    private static partial Regex NamespacePattern();

    [GeneratedRegex(
        @"\{[^\{\}]*?:id\s+""([^""]+)""[^\{\}]*?:killed\s+(\d+)[^\{\}]*?:survived\s+(\d+)")]
    private static partial Regex MutateFormPattern();

    public static IReadOnlyList<CrapEntry> ReadCrap(string path)
    {
        var text = File.ReadAllText(path);
        return CrapEntryPattern().Matches(text).Select(m => new CrapEntry
        {
            Name = m.Groups[1].Value,
            Namespace = m.Groups[2].Value,
            Complexity = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
            Coverage = double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
            Crap = double.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
        }).ToList();
    }

    public static IReadOnlyDictionary<string, MutateSnapshot> ReadMutateDir(string mutateDir)
    {
        if (!Directory.Exists(mutateDir))
            return new Dictionary<string, MutateSnapshot>();

        var result = new Dictionary<string, MutateSnapshot>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(mutateDir, "*.edn", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var nsMatch = NamespacePattern().Match(text);
            if (!nsMatch.Success)
                continue;

            var ns = nsMatch.Groups[1].Value;
            var forms = MutateFormPattern().Matches(text).Select(m => new MutateForm
            {
                Id = m.Groups[1].Value,
                Killed = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                Survived = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
            }).ToList();

            if (forms.Count == 0)
                continue;

            if (result.TryGetValue(ns, out var existing))
            {
                existing.Forms.AddRange(forms);
            }
            else
            {
                result[ns] = new MutateSnapshot { Namespace = ns, Forms = forms };
            }
        }

        return result;
    }
}
