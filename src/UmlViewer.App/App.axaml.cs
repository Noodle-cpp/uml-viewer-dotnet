using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using UmlViewer.Core.Io;
using UmlViewer.Core.Metrics;
using UmlViewer.Core.Overlay;

namespace UmlViewer.App;

public partial class App : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        try
        {
            ParseStartupArgs();
            desktop.MainWindow = new MainWindow();
        }
        catch (Exception ex)
        {
            desktop.MainWindow = new Window
            {
                Title = "UML Viewer — startup error",
                Width = 640,
                Height = 320,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text =
                            ex.Message +
                            "\n\nExample:\n  UmlViewer.App --root <solution-folder> --ir <diagram.edn|json>\n\n" +
                            ex.StackTrace,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Thickness(16),
                    },
                },
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ParseStartupArgs()
    {
        var args = Environment.GetCommandLineArgs();
        string? root = null;
        string? ir = null;
        var restart = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--restart")
                restart = true;
            if (i < args.Length - 1 && args[i] == "--root")
                root = args[i + 1];
            if (i < args.Length - 1 && args[i] == "--ir")
                ir = args[i + 1];
        }

        ir = ResolveExistingFile(ir) ?? FindDefaultIr();
        if (string.IsNullOrWhiteSpace(ir))
        {
            throw new InvalidOperationException(
                "IR file not found. Pass --ir <diagram.edn|json> (file must exist).\n" +
                "If you run from the IDE, fix Properties/launchSettings.json commandLineArgs.");
        }

        var metricsRoot = string.IsNullOrWhiteSpace(root) || !LooksLikePath(root)
            ? MetricsLoader.FindMetricsRoot(ir)
            : Path.GetFullPath(root);

        if (string.IsNullOrWhiteSpace(metricsRoot) || !Directory.Exists(metricsRoot))
            metricsRoot = Path.GetDirectoryName(ir) ?? Directory.GetCurrentDirectory();

        var doc = IrLoader.Load(ir);
        var metrics = MetricsLoader.Load(metricsRoot);
        OverlayService.ApplyMetrics(doc, metrics);

        StartupOptions.MetricsRoot = metricsRoot;
        StartupOptions.Document = doc;
        StartupOptions.IrPath = Path.GetFullPath(ir);
        StartupOptions.RestartSession = restart;
        StartupOptions.DocumentService = new Services.ViewerDocumentService(
            StartupOptions.IrPath,
            metricsRoot,
            doc);
    }

    private static bool LooksLikePath(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains('…', StringComparison.Ordinal) &&
        !value.Contains("...", StringComparison.Ordinal);

    private static string? ResolveExistingFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !LooksLikePath(path))
            return null;
        path = Environment.ExpandEnvironmentVariables(path.Trim('"'));
        if (File.Exists(path))
            return Path.GetFullPath(path);
        var combined = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        return File.Exists(combined) ? combined : null;
    }

    private static string? FindDefaultIr()
    {
        var roots = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
        };
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                continue;
            var candidate = Path.GetFullPath(Path.Combine(root, "..", "uml-viewer", "target", "glpi-scan.edn"));
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.GetFullPath(Path.Combine(root, "uml-viewer", "target", "glpi-scan.edn"));
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
