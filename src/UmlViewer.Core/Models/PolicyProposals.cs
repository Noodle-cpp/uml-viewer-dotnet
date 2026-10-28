namespace UmlViewer.Core.Models;

public sealed class IrProposalLayer
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public List<IrNseNode> Nses { get; set; } = [];
}

/// <summary>Namespace leaf id or nested group in a proposal layer :nses entry.</summary>
public sealed class IrNseNode
{
    public string? LeafId { get; init; }
    public string? Id { get; init; }
    public string? Label { get; init; }
    public List<IrNseNode> Children { get; init; } = [];

    public bool IsLeaf => LeafId != null;

    public static IrNseNode Leaf(string id) => new() { LeafId = id };

    public static IrNseNode Group(string id, string label, IReadOnlyList<IrNseNode> children) =>
        new() { Id = id, Label = label, Children = children.ToList() };
}

public sealed class IrProposal
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Notice { get; set; }
    public List<IrProposalLayer> Layers { get; set; } = [];
    public List<string> Omit { get; set; } = [];
}

public sealed class IrNormalizedProposal
{
    public string? Notice { get; init; }
    public List<IrProposalLayer> Layers { get; init; } = [];
    public List<string> Omit { get; init; } = [];
}

public static class PolicyProposals
{
    public const string ProposalNotice = "PROPOSAL — not instantiated in code";

    public static string TimestampName() =>
        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public static List<IrProposal> NamedProposals(IrDocument doc) => doc.Proposals;

    public static IrNormalizedProposal? NormalizeProposal(IrProposal proposal)
    {
        if (proposal.Layers.Count == 0)
            return null;
        return new IrNormalizedProposal
        {
            Notice = proposal.Notice ?? ProposalNotice,
            Layers = proposal.Layers,
            Omit = proposal.Omit.ToList(),
        };
    }

    public static IrProposal? ProposalById(IrDocument doc, string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return doc.Proposals.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
    }
}
