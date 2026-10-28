using EdnParser;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Edn;

public static class EdnIrLoader
{
    public static IrDocument Load(string path)
    {
        var text = File.ReadAllText(path);
        var tokens = new Parser().Parse(text);
        var root = tokens.First(t => t is not EdnComment) as EdnMap
                   ?? throw new InvalidOperationException("IR EDN must be a single map.");
        return MapDocument(root);
    }

    private static IrDocument MapDocument(EdnMap root)
    {
        var hierarchical = EdnValueExtensions.GetBool(root, "hierarchical", true);
        var doc = new IrDocument
        {
            Title = EdnValueExtensions.GetString(root, "title") ?? "",
            Prefix = EdnValueExtensions.GetString(root, "prefix") ?? "",
            Hierarchical = hierarchical,
            Proposals = MapProposals(EdnValueExtensions.GetVector(root, "proposals")),
            Omit = MapStringList(EdnValueExtensions.GetVector(root, "omit")),
        };

        if (hierarchical)
        {
            doc.Classes = MapClasses(EdnValueExtensions.GetVector(root, "classes"));
            doc.Edges = MapEdges(EdnValueExtensions.GetVector(root, "edges"));
        }
        else
        {
            doc.Hierarchical = false;
            var diagrams = EdnValueExtensions.GetVector(root, "diagrams");
            doc.Diagrams = diagrams != null
                ? MapStackedDiagrams(diagrams)
                : [MapStackedDiagram(root)];
        }

        return doc;
    }

    private static List<IrStackedDiagram> MapStackedDiagrams(EdnVector vector)
    {
        var list = new List<IrStackedDiagram>();
        foreach (var item in vector.Values())
        {
            if (item is EdnMap map)
                list.Add(MapStackedDiagram(map));
        }

        return list;
    }

    private static IrStackedDiagram MapStackedDiagram(EdnMap map) => new()
    {
        Title = EdnValueExtensions.GetString(map, "title") ?? "UML",
        Packages = MapPackages(EdnValueExtensions.GetVector(map, "packages")),
        Foreign = MapClasses(EdnValueExtensions.GetVector(map, "foreign")),
        Edges = MapEdges(EdnValueExtensions.GetVector(map, "edges")),
    };

    private static List<IrDiagramPackage> MapPackages(EdnVector? vector)
    {
        var list = new List<IrDiagramPackage>();
        foreach (var m in EdnValueExtensions.MapEntries(vector))
        {
            var id = EdnValueExtensions.GetString(m, "id")
                     ?? EdnValueExtensions.GetString(m, "label")
                     ?? "pkg";
            list.Add(new IrDiagramPackage
            {
                Id = id,
                Label = EdnValueExtensions.GetString(m, "label") ?? id,
                Classes = MapClasses(EdnValueExtensions.GetVector(m, "classes")),
            });
        }

        return list;
    }

    private static List<IrProposal> MapProposals(EdnVector? vector)
    {
        var list = new List<IrProposal>();
        foreach (var m in EdnValueExtensions.MapEntries(vector))
        {
            var id = EdnValueExtensions.GetString(m, "id") ?? "";
            list.Add(new IrProposal
            {
                Id = id,
                Name = EdnValueExtensions.GetString(m, "name") ?? id,
                Notice = EdnValueExtensions.GetString(m, "notice"),
                Layers = EdnProposalLoader.MapProposalLayers(EdnValueExtensions.GetVector(m, "layers")),
                Omit = MapStringList(EdnValueExtensions.GetVector(m, "omit")),
            });
        }

        return list;
    }

    private static List<string> MapStringList(EdnVector? vector)
    {
        var list = new List<string>();
        if (vector == null)
            return list;
        foreach (var item in vector.Values())
        {
            var s = EdnId.From(item);
            if (s.Length > 0)
                list.Add(s);
        }

        return list;
    }

    private static List<IrClass> MapClasses(EdnVector? vector)
    {
        var list = new List<IrClass>();
        foreach (var m in EdnValueExtensions.MapEntries(vector))
        {
            var id = EdnValueExtensions.GetString(m, "id") ?? "";
            list.Add(new IrClass
            {
                Id = id,
                Name = EdnValueExtensions.GetString(m, "name") ?? id,
                Ns = EdnValueExtensions.GetString(m, "ns") ?? "",
                File = EdnValueExtensions.GetString(m, "file") ?? "",
                Lang = EdnValueExtensions.GetString(m, "lang") ?? "csharp",
                Foreign = EdnValueExtensions.GetBool(m, "foreign"),
                Stereotype = EdnValueExtensions.GetString(m, "stereotype"),
                Ops = MapOps(EdnValueExtensions.GetVector(m, "ops")),
            });
        }

        return list;
    }

    private static List<IrOp> MapOps(EdnVector? vector)
    {
        var ops = new List<IrOp>();
        foreach (var m in EdnValueExtensions.MapEntries(vector))
        {
            ops.Add(new IrOp
            {
                Name = EdnValueExtensions.GetString(m, "name") ?? "",
                Text = EdnValueExtensions.GetString(m, "text") ?? EdnValueExtensions.GetString(m, "name") ?? "",
                Private = EdnValueExtensions.GetBool(m, "private") ? true : null,
            });
        }

        return ops;
    }

    private static List<IrEdge> MapEdges(EdnVector? vector)
    {
        var edges = new List<IrEdge>();
        foreach (var m in EdnValueExtensions.MapEntries(vector))
        {
            edges.Add(new IrEdge
            {
                From = EdnValueExtensions.GetString(m, "from") ?? "",
                To = EdnValueExtensions.GetString(m, "to") ?? "",
                Kind = EdnValueExtensions.GetString(m, "kind") ?? "dependency",
            });
        }

        return edges;
    }
}
