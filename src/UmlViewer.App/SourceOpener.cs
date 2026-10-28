using System.Diagnostics;
using System.IO;

namespace UmlViewer.App;

public static class SourceOpener
{
    public static void OpenFile(string metricsRoot, string relativeFile, string? methodName)
    {
        var full = Path.Combine(metricsRoot, relativeFile);
        if (!File.Exists(full))
        {
            foreach (var dir in Directory.EnumerateDirectories(metricsRoot, "*", SearchOption.AllDirectories))
            {
                var tryPath = Path.Combine(dir, relativeFile);
                if (File.Exists(tryPath))
                {
                    full = tryPath;
                    break;
                }
            }
        }

        if (!File.Exists(full))
            return;

        if (methodName == null)
        {
            Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
            return;
        }

        var line = FindMethodLine(full, methodName);
        TryOpenInEditor(full, line);
    }

    private static int FindMethodLine(string file, string methodName)
    {
        var lines = File.ReadAllLines(file);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(methodName + "(", StringComparison.Ordinal) ||
                lines[i].Contains(methodName + " ", StringComparison.Ordinal))
                return i + 1;
        }

        return 1;
    }

    private static void TryOpenInEditor(string file, int line)
    {
        var cursor = Process.Start(new ProcessStartInfo
        {
            FileName = "cursor",
            ArgumentList = { "--goto", $"{file}:{line}" },
            UseShellExecute = false,
        });
        if (cursor != null)
            return;

        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }
}
