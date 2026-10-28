using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static class ProposalEngine
{
    public static ViewDiagram CompileSource(
        IrDocument doc,
        IReadOnlyList<string> focus,
        ViewerCompileOptions opts)
    {
        if (focus.Count == 0 && !string.IsNullOrEmpty(opts.ProposalId))
        {
            if (!string.IsNullOrEmpty(opts.OpenLayerId))
                return ProposalHierarchy.LayerView(doc, opts.ProposalId, opts.OpenLayerId);
            if (PolicyProposals.ProposalById(doc, opts.ProposalId) != null)
                return ProposalHierarchy.ProposalView(doc, opts.ProposalId);
        }

        return HierarchyEngine.ViewAt(doc, focus);
    }
}
