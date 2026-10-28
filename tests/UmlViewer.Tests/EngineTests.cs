using UmlViewer.Core.Edn;
using UmlViewer.Engine;

namespace UmlViewer.Tests;

public class EngineTests
{
    [Fact]
    public void EdnIrLoader_reads_classes_and_edges()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "uml-viewer", "target", "glpi-scan.edn"));
        if (!File.Exists(path))
            return;

        var doc = EdnIrLoader.Load(path);
        Assert.True(doc.Classes.Count > 100);
        Assert.Contains(doc.Edges, e => e.Kind == "implements");
        var view = HierarchyEngine.ViewAt(doc, []);
        Assert.NotEmpty(view.Packages[0].Classes);
        var scene = DiagramCompiler.Compile(doc, []);
        Assert.True(scene.Width > 100);
        Assert.NotEmpty(scene.Classes);
    }

    [Fact]
    public void Declutter_triangles_hides_edge_strokes()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "uml-viewer", "target", "glpi-scan.edn"));
        if (!File.Exists(path))
            return;

        var doc = EdnIrLoader.Load(path);
        var full = DiagramCompiler.Compile(doc, [], new ViewerCompileOptions { DeclutterMode = "full" });
        var triangles = DiagramCompiler.Compile(doc, [], new ViewerCompileOptions { DeclutterMode = "triangles" });
        Assert.NotEmpty(full.Edges);
        Assert.Empty(triangles.Edges);
        Assert.NotEmpty(triangles.DepIndicators);
    }
}
