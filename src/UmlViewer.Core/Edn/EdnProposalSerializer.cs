using System.Globalization;
using System.Text;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Edn;

public static class EdnProposalSerializer
{
    public static string SerializeProposalsVector(IReadOnlyList<IrProposal> proposals)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (var i = 0; i < proposals.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            WriteProposal(sb, proposals[i]);
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static void WriteProposal(StringBuilder sb, IrProposal p)
    {
        sb.Append("{:id ");
        WriteId(sb, p.Id);
        sb.Append(", :name ");
        WriteString(sb, p.Name);
        sb.Append(", :layers ");
        WriteLayers(sb, p.Layers);
        sb.Append('}');
    }

    private static void WriteLayers(StringBuilder sb, IReadOnlyList<IrProposalLayer> layers)
    {
        sb.Append('[');
        for (var i = 0; i < layers.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            WriteLayer(sb, layers[i]);
        }

        sb.Append(']');
    }

    private static void WriteLayer(StringBuilder sb, IrProposalLayer layer)
    {
        sb.Append("{:id ");
        WriteId(sb, layer.Id);
        sb.Append(", :label ");
        WriteString(sb, layer.Label);
        sb.Append(", :nses ");
        WriteNses(sb, layer.Nses);
        sb.Append('}');
    }

    private static void WriteNses(StringBuilder sb, IReadOnlyList<IrNseNode> nses)
    {
        sb.Append('[');
        for (var i = 0; i < nses.Count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            WriteNse(sb, nses[i]);
        }

        sb.Append(']');
    }

    private static void WriteNse(StringBuilder sb, IrNseNode node)
    {
        if (node.IsLeaf)
        {
            WriteId(sb, node.LeafId!);
            return;
        }

        sb.Append("{:id ");
        WriteId(sb, node.Id ?? "");
        sb.Append(", :label ");
        WriteString(sb, node.Label ?? node.Id ?? "");
        sb.Append(", :nses ");
        WriteNses(sb, node.Children);
        sb.Append('}');
    }

    private static void WriteId(StringBuilder sb, string id)
    {
        sb.Append(':');
        sb.Append(id.TrimStart(':'));
    }

    private static void WriteString(StringBuilder sb, string value)
    {
        sb.Append('"');
        sb.Append(value.Replace("\\", "\\\\").Replace("\"", "\\\""));
        sb.Append('"');
    }
}
