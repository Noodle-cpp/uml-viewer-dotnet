using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class PolicyNse
{
    public static List<string> NseIds(IReadOnlyList<IrNseNode> nses)
    {
        var ids = new List<string>();
        foreach (var n in nses)
            CollectLeafIds(n, ids);
        return ids;
    }

    private static void CollectLeafIds(IrNseNode node, List<string> ids)
    {
        if (node.IsLeaf)
        {
            ids.Add(node.LeafId!);
            return;
        }

        foreach (var child in node.Children)
            CollectLeafIds(child, ids);
    }

    public static Dictionary<string, string> GroupOwners(IReadOnlyList<IrNseNode> nses)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var n in nses)
            CollectGroupOwners(n, map);
        return map;
    }

    private static void CollectGroupOwners(IrNseNode node, Dictionary<string, string> map)
    {
        if (node.IsLeaf)
            return;
        var groupId = node.Id ?? "";
        foreach (var leaf in NseIds(node.Children))
            map[leaf] = groupId;
        foreach (var child in node.Children.Where(c => !c.IsLeaf))
            CollectGroupOwners(child, map);
    }
}
