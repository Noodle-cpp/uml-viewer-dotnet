using EdnParser;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Edn;

internal static class EdnId
{
    internal static string From(EdnValue value) =>
        value switch
        {
            EdnKeyword kw => kw.ToString()?.TrimStart(':') ?? "",
            EdnSymbol sym => sym.ToString() ?? "",
            EdnString str => str.ToString() ?? "",
            _ => value.ToString()?.TrimStart(':') ?? "",
        };
}

public static class EdnProposalLoader
{
    internal static List<IrNseNode> MapNseList(EdnVector? vector)
    {
        var list = new List<IrNseNode>();
        if (vector == null)
            return list;
        foreach (var item in vector.Values())
        {
            var node = MapNse(item);
            if (node != null)
                list.Add(node);
        }

        return list;
    }

    internal static IrNseNode? MapNse(EdnValue value) =>
        value switch
        {
            EdnKeyword or EdnSymbol or EdnString => IrNseNode.Leaf(EdnId.From(value)),
            EdnMap map => MapNseGroup(map),
            _ => null,
        };

    private static IrNseNode? MapNseGroup(EdnMap map)
    {
        var id = EdnValueExtensions.GetString(map, "id") ?? "";
        var label = EdnValueExtensions.GetString(map, "label") ?? id;
        var children = MapNseList(EdnValueExtensions.GetVector(map, "nses"));
        if (string.IsNullOrEmpty(id) && children.Count == 0)
            return null;
        if (string.IsNullOrEmpty(id))
            id = label;
        return IrNseNode.Group(id, label, children);
    }

    internal static IrProposalLayer? MapProposalLayer(EdnMap map, int index)
    {
        var id = EdnValueExtensions.GetString(map, "id")
                 ?? EdnValueExtensions.GetString(map, "label")
                 ?? $"layer-{index}";
        return new IrProposalLayer
        {
            Id = id,
            Label = EdnValueExtensions.GetString(map, "label") ?? id,
            Nses = MapNseList(EdnValueExtensions.GetVector(map, "nses")),
        };
    }

    internal static List<IrProposalLayer> MapProposalLayers(EdnVector? vector)
    {
        var layers = new List<IrProposalLayer>();
        var i = 0;
        foreach (var m in EdnValueExtensions.MapEntries(vector))
        {
            var layer = MapProposalLayer(m, i++);
            if (layer != null && layer.Nses.Count > 0)
                layers.Add(layer);
        }

        return layers;
    }
}
