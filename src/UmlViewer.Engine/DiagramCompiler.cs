using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class DiagramCompiler
{
    public static CompiledScene Compile(IrDocument doc, IReadOnlyList<string> focusPath) =>
        Compile(doc, focusPath, null);

    public static CompiledScene Compile(
        IrDocument doc,
        IReadOnlyList<string> focusPath,
        ViewerCompileOptions? options)
    {
        options ??= new ViewerCompileOptions();
        if (!doc.Hierarchical && doc.Diagrams.Count > 0 && string.IsNullOrEmpty(options.ProposalId))
        {
            var mode = DeclutterEngine.NormalizeForCompile(options.DeclutterMode, false);
            var template = new ViewDiagram { HideEdges = mode == "triangles" };
            return ComposeEngine.CompileDocument(doc, template, mode);
        }

        var view = ProposalEngine.CompileSource(doc, focusPath, options);
        var mode2 = DeclutterEngine.NormalizeForCompile(options.DeclutterMode, options.OpenLayer);
        view = DeclutterEngine.Apply(view, mode2);
        return ComposeEngine.CompileDiagram(view);
    }
}
