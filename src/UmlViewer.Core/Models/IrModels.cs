using System.Text.Json.Serialization;

namespace UmlViewer.Core.Models;

public sealed class ScanPolicy
{
    public string Title { get; set; } = "Scan";
    public string Prefix { get; set; } = "";
    public bool Hierarchical { get; set; } = true;
    public List<ScanSource> Sources { get; set; } = [];
}

public sealed class ScanSource
{
    public string Lang { get; set; } = "csharp";
    public string Root { get; set; } = ".";
}

public sealed class IrDocument
{
    public string Title { get; set; } = "";
    public string Prefix { get; set; } = "";
    public bool Hierarchical { get; set; } = true;
    public List<IrClass> Classes { get; set; } = [];
    public List<IrEdge> Edges { get; set; } = [];
    public List<IrProposal> Proposals { get; set; } = [];
    public List<string> Omit { get; set; } = [];
    public List<IrEdgeEnd> OmitEdges { get; set; } = [];
    public Dictionary<string, string> EdgeKinds { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Stacked package diagrams when <see cref="Hierarchical"/> is false.</summary>
    public List<IrStackedDiagram> Diagrams { get; set; } = [];
}

public sealed class IrStackedDiagram
{
    public string Title { get; set; } = "UML";
    public List<IrDiagramPackage> Packages { get; set; } = [];
    public List<IrClass> Foreign { get; set; } = [];
    public List<IrEdge> Edges { get; set; } = [];
}

public sealed class IrDiagramPackage
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public List<IrClass> Classes { get; set; } = [];
}

public sealed class IrEdgeEnd
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed class IrCrapSummary
{
    public double Mu { get; set; }
    public double Max { get; set; }
    public double Sigma { get; set; }
    public int N { get; set; }
}

public sealed class IrClass
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ns { get; set; } = "";
    public string File { get; set; } = "";
    public string Lang { get; set; } = "csharp";
    public bool Foreign { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Stereotype { get; set; }

    public List<IrOp> Ops { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IrCrapSummary? CrapSummary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Crap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Cc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Coverage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Killed { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Survived { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Uncovered { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Sites { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Level { get; set; }
}

public sealed class IrOp
{
    public string Name { get; set; } = "";
    public string Text { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Crap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Cc { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Private { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Killed { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Survived { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Uncovered { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Sites { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Coverage { get; set; }
}

public sealed class IrEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Kind { get; set; } = "dependency";
}

public sealed class CrapEntry
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public int Complexity { get; set; }
    public double Coverage { get; set; }
    public double Crap { get; set; }
}

public sealed class MutateForm
{
    public string Id { get; set; } = "";
    public int Killed { get; set; }
    public int Survived { get; set; }
    public int Uncovered { get; set; }
}

public sealed class MutateSnapshot
{
    public string Namespace { get; set; } = "";
    public List<MutateForm> Forms { get; set; } = [];
}
