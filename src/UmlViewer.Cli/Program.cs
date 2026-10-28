using System.CommandLine;
using UmlViewer.Core.Io;
using UmlViewer.Core.Metrics;
using UmlViewer.Core.Overlay;
using UmlViewer.Scan.CSharp;

namespace UmlViewer.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var scanCmd = new Command("scan", "Scan C# sources and write IR JSON.");
        var policyArg = new Argument<FileInfo>("policy", "Policy (.json or .edn).");
        var outArg = new Argument<FileInfo>("output", "Output IR (.json).");
        scanCmd.AddArgument(policyArg);
        scanCmd.AddArgument(outArg);
        scanCmd.SetHandler((policy, output) =>
        {
            var p = PolicyLoader.LoadPolicy(policy.FullName);
            var doc = CSharpProjectScanner.Scan(p);
            IrJson.Write(output.FullName, doc);
            Console.WriteLine($"Wrote {output.FullName} ({doc.Classes.Count} classes).");
        }, policyArg, outArg);

        var checkCmd = new Command("overlay-check", "Compare crap.edn namespaces to IR.");
        var irArg = new Argument<FileInfo>("ir", "IR JSON from scan.");
        var rootOpt = new Option<DirectoryInfo?>("--root", "Solution root with .metrics (default: infer from IR path).");
        var prefixOpt = new Option<string?>("--prefix", "Limit crap namespaces to this prefix.");
        checkCmd.AddArgument(irArg);
        checkCmd.AddOption(rootOpt);
        checkCmd.AddOption(prefixOpt);
        checkCmd.SetHandler((ir, root, prefix) =>
        {
            var doc = IrLoader.Load(ir.FullName);
            var metricsRoot = root?.FullName ?? MetricsLoader.FindMetricsRoot(ir.FullName);
            var metrics = MetricsLoader.Load(metricsRoot);
            var result = OverlayService.CheckNamespaces(doc, metrics, prefix ?? doc.Prefix);
            if (result.MissingCount == 0)
            {
                Console.WriteLine("OK: all crap namespaces present in IR.");
                return;
            }

            Console.WriteLine($"Missing {result.MissingCount} namespace(s) in IR:");
            foreach (var ns in result.MissingNamespaces)
                Console.WriteLine($"  {ns}");
            Environment.ExitCode = 1;
        }, irArg, rootOpt, prefixOpt);

        var viewCmd = new Command("view", "Open interactive Avalonia viewer (same as UmlViewer.App).");
        var viewRoot = new Option<DirectoryInfo>("--root", "Solution root (.metrics + source files).") { IsRequired = true };
        var viewIr = new Option<FileInfo?>("--ir", "IR JSON (scan first if omitted).");
        var viewPolicy = new Option<FileInfo?>("--policy", "Policy when IR must be generated.");
        viewCmd.AddOption(viewRoot);
        viewCmd.AddOption(viewIr);
        viewCmd.AddOption(viewPolicy);
        viewCmd.SetHandler((root, ir, policy) =>
        {
            var appProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                "..", "..", "..", "..", "UmlViewer.App", "UmlViewer.App.csproj"));
            if (!File.Exists(appProject))
                appProject = Path.GetFullPath("src/UmlViewer.App/UmlViewer.App.csproj");

            var irPath = ir?.FullName;
            if (irPath == null)
            {
                if (policy == null)
                    throw new InvalidOperationException("Provide --ir or --policy.");
                irPath = Path.Combine(Path.GetTempPath(), $"uml-viewer-{Guid.NewGuid():N}.json");
                var p = PolicyLoader.LoadPolicy(policy.FullName);
                var doc = CSharpProjectScanner.Scan(p);
                IrJson.Write(irPath, doc);
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "run",
                    "--project",
                    appProject,
                    "--",
                    "--root",
                    root.FullName,
                    "--ir",
                    irPath,
                },
                UseShellExecute = true,
            };
            System.Diagnostics.Process.Start(psi);
        }, viewRoot, viewIr, viewPolicy);

        var rootCmd = new RootCommand("UML viewer for .NET 8 (no JVM).");
        rootCmd.AddCommand(scanCmd);
        rootCmd.AddCommand(checkCmd);
        rootCmd.AddCommand(viewCmd);

        return await rootCmd.InvokeAsync(args);
    }
}
