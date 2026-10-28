using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public sealed class DiagramEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Kind { get; set; } = "dependency";
    public bool Violating { get; set; }
    public string? OrigFrom { get; set; }
    public string? OrigTo { get; set; }
    public List<LeafDep> Deps { get; set; } = [];
}

public sealed class DiagramClass
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Ns { get; set; }
    public string? File { get; set; }
    public string? Lang { get; set; }
    public string? Stereotype { get; set; }
    public bool Drill { get; set; }
    public bool HideMembers { get; set; }
    public bool Dummy { get; set; }
    public bool Foreign { get; set; }
    public bool External { get; set; }
    public string Shape { get; set; } = "box";
    public int? Level { get; set; }
    public IrCrapSummary? CrapSummary { get; set; }
    public double? Coverage { get; set; }
    public int? Killed { get; set; }
    public int? Survived { get; set; }
    public List<IrOp> Ops { get; set; } = [];
    public List<DiagramChild> Contents { get; set; } = [];
    public List<PortDep> InDeps { get; set; } = [];
    public List<PortDep> OutDeps { get; set; } = [];
    public bool ProposalGroup { get; set; }
    public List<DiagramClass> Members { get; set; } = [];
}

public sealed class DiagramChild
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Drill { get; set; }
}

public sealed class PortDep
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Rect Rect { get; set; }
}

public sealed class DiagramPackage
{
    public string Id { get; set; } = "view";
    public string Label { get; set; } = "";
    public List<DiagramClass> Classes { get; set; } = [];
}

public sealed class ViewDiagram
{
    public string Title { get; set; } = "";
    public string Direction { get; set; } = "tb";
    public bool HideEdges { get; set; }
    public List<DiagramPackage> Packages { get; set; } = [];
    public List<DiagramEdge> Edges { get; set; } = [];
    public List<DiagramClass> Foreign { get; set; } = [];
}

public sealed class ClassLine
{
    public string Kind { get; set; } = "name";
    public string Text { get; set; } = "";
    public string? Id { get; set; }
    public bool Drill { get; set; }
}

public sealed class ScenePackage
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Title { get; set; } = "";
    public Rect Rect { get; set; }
    public IrCrapSummary? CrapSummary { get; set; }
}

public sealed class SceneClass
{
    public DiagramClass Source { get; init; } = new();
    public string Id => Source.Id;
    public string? Package { get; set; }
    public int Rank { get; set; }
    public bool Dummy { get; set; }
    public string Shape { get; set; } = "box";
    public Rect Rect { get; set; }
    public List<ClassLine> Lines { get; set; } = [];
    public List<PortDep> InPorts { get; set; } = [];
    public List<PortDep> OutPorts { get; set; } = [];
}

public sealed class SceneEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Kind { get; set; } = "dependency";
    public bool Violating { get; set; }
    public List<Point2> Points { get; set; } = [];
    public string Head { get; set; } = "open";
    public List<List<Point2>> Strokes { get; set; } = [];
    public Point2? Tip { get; set; }
    public Point2? Behind { get; set; }
    public List<LeafDep> Deps { get; set; } = [];
}

public sealed class LeafDep
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public bool Violating { get; set; }
}

public sealed class DepIndicator
{
    public string Id { get; set; } = "";
    public string Dir { get; set; } = "";
    public List<Point2> Triangle { get; set; } = [];
    public bool Violating { get; set; }
    public List<LeafDep> Deps { get; set; } = [];
}

public sealed class SceneSize
{
    public double W { get; set; }
    public double H { get; set; }
    public double MinX { get; set; }
    public double MinY { get; set; }
}

public sealed class LayoutScene
{
    public ViewDiagram Diagram { get; set; } = new();
    public List<ScenePackage> Packages { get; set; } = [];
    public List<SceneClass> Classes { get; set; } = [];
    public List<SceneEdge> Edges { get; set; } = [];
    public SceneSize Size { get; set; } = new();
}

public sealed class CompiledScene
{
    public string Title { get; set; } = "";
    public ViewDiagram Diagram { get; set; } = new();
    public SceneSize Size { get; set; } = new();
    public List<SceneSection> Sections { get; set; } = [];
    public List<ScenePackage> Packages { get; set; } = [];
    public List<SceneClass> Classes { get; set; } = [];
    public List<SceneEdge> Edges { get; set; } = [];
    public List<DepIndicator> DepIndicators { get; set; } = [];
    public Dictionary<string, SceneClass> ClassById { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, ScenePackage> PackageById { get; set; } = new(StringComparer.Ordinal);

    public double Width => Size.W;
    public double Height => Size.H;
}

public sealed class SceneSection
{
    public string Title { get; set; } = "";
    public double TitleY { get; set; }
}

public sealed class HitResult
{
    public string Kind { get; set; } = "";
    public string? Id { get; set; }
    public string? Parent { get; set; }
    public string? Dir { get; set; }
    public bool Drill { get; set; }
    public bool Violating { get; set; }
    public List<LeafDep> Deps { get; set; } = [];
    public string? From { get; set; }
    public string? To { get; set; }
}
