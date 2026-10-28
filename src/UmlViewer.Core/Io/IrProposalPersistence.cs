using UmlViewer.Core.Edn;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Io;

public static class IrProposalPersistence
{
    public static void WriteProposals(string irPath, IrDocument doc)
    {
        if (string.IsNullOrWhiteSpace(irPath) || !File.Exists(irPath))
            return;

        var proposalsEdn = EdnProposalSerializer.SerializeProposalsVector(doc.Proposals);
        EdnRootMapPatcher.SetVectorKey(irPath, "proposals", proposalsEdn);

        var policyPath = ResolvePolicyPath(irPath, doc);
        if (policyPath != null && File.Exists(policyPath))
            EdnRootMapPatcher.SetVectorKey(policyPath, "proposals", proposalsEdn);
    }

    private static string? ResolvePolicyPath(string irPath, IrDocument doc)
    {
        if (irPath.EndsWith(".edn", StringComparison.OrdinalIgnoreCase))
        {
            var sibling = irPath[..^4] + ".policy.edn";
            if (File.Exists(sibling))
                return sibling;
        }

        return null;
    }
}
