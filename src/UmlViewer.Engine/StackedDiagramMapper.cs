using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class StackedDiagramMapper
{
    public static ViewDiagram ToView(IrStackedDiagram diagram)
    {
        var packages = diagram.Packages.Select(p => new DiagramPackage
        {
            Id = p.Id,
            Label = string.IsNullOrEmpty(p.Label) ? p.Id : p.Label,
            Classes = p.Classes.Select(IrToDiagramClass).ToList(),
        }).ToList();

        var foreign = diagram.Foreign.Select(c =>
        {
            var dc = IrToDiagramClass(c);
            dc.Foreign = true;
            dc.External = true;
            dc.Shape = "oval";
            return dc;
        }).ToList();

        var edges = diagram.Edges.Select(e => new DiagramEdge
        {
            From = e.From,
            To = e.To,
            Kind = string.IsNullOrEmpty(e.Kind) ? "dependency" : e.Kind,
        }).ToList();

        return new ViewDiagram
        {
            Title = diagram.Title,
            Packages = packages,
            Foreign = foreign,
            Edges = edges,
        };
    }

    private static DiagramClass IrToDiagramClass(IrClass c) => new()
    {
        Id = c.Id,
        Name = string.IsNullOrEmpty(c.Name) ? c.Id : c.Name,
        Ns = c.Ns,
        File = c.File,
        Lang = c.Lang,
        Stereotype = c.Stereotype,
        Foreign = c.Foreign,
        CrapSummary = c.CrapSummary,
        Coverage = c.Coverage,
        Killed = c.Killed,
        Survived = c.Survived,
        Ops = c.Ops.Where(o => o.Private != true).ToList(),
    };
}
