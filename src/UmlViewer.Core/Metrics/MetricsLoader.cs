using UmlViewer.Core.Models;

namespace UmlViewer.Core.Metrics;

public sealed class ProjectMetrics
{
    public IReadOnlyList<CrapEntry> Crap { get; init; } = [];
    public IReadOnlyDictionary<string, MutateSnapshot> Mutate { get; init; } =
        new Dictionary<string, MutateSnapshot>();
}

public static class MetricsLoader
{
    public static string FindMetricsRoot(string startPath)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startPath));
        if (dir.Exists == false && File.Exists(startPath))
            dir = dir.Parent ?? dir;

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".metrics")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    public static ProjectMetrics Load(string metricsRoot)
    {
        var crapPath = Path.Combine(metricsRoot, ".metrics", "crap.edn");
        var mutateDir = Path.Combine(metricsRoot, ".metrics", "mutate");
        return new ProjectMetrics
        {
            Crap = File.Exists(crapPath) ? MetricsEdnReader.ReadCrap(crapPath) : [],
            Mutate = MetricsEdnReader.ReadMutateDir(mutateDir),
        };
    }

    /// <summary>Fingerprint of CRAP + mutate snapshot files (same idea as Clojure metrics-stamp).</summary>
    public static string Stamp(string metricsRoot)
    {
        var files = new List<FileInfo>();
        var crap = new FileInfo(Path.Combine(metricsRoot, ".metrics", "crap.edn"));
        if (crap.Exists)
            files.Add(crap);
        var mutateDir = Path.Combine(metricsRoot, ".metrics", "mutate");
        if (Directory.Exists(mutateDir))
        {
            foreach (var f in Directory.EnumerateFiles(mutateDir, "*.edn", SearchOption.AllDirectories))
                files.Add(new FileInfo(f));
        }

        var parts = files
            .OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(f => $"{f.FullName}|{f.LastWriteTimeUtc.Ticks}|{f.Length}");
        return string.Join(";", parts);
    }
}
