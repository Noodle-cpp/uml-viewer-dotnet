using UmlViewer.Core.Models;

namespace UmlViewer.App;

public static class StartupOptions
{
    public static string MetricsRoot { get; set; } = "";
    public static string IrPath { get; set; } = "";
    public static IrDocument Document { get; set; } = new();
    public static bool RestartSession { get; set; }
    public static Services.ViewerDocumentService? DocumentService { get; set; }
}
