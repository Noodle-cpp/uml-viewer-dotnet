using UmlViewer.Core.Edn;
using UmlViewer.Core.Io;
using UmlViewer.Core.Mailbox;
using UmlViewer.Core.Metrics;
using UmlViewer.Core.Models;
using UmlViewer.Core.Overlay;

namespace UmlViewer.App.Services;

public sealed class ViewerDocumentService
{
    public string IrPath { get; private set; }
    public string MetricsRoot { get; }

    public IrDocument Document { get; private set; }
    public long IrMtime { get; private set; }
    public string MetricsStamp { get; private set; }
    public long MailSeenViewer { get; set; }

    public ViewerDocumentService(string irPath, string metricsRoot, IrDocument initial)
    {
        IrPath = irPath;
        MetricsRoot = metricsRoot;
        Document = initial;
        RefreshStamps();
        MailSeenViewer = MailboxService.LastId(MailboxPaths.ToViewer(metricsRoot));
    }

    public void RefreshStamps()
    {
        IrMtime = File.Exists(IrPath) ? File.GetLastWriteTimeUtc(IrPath).Ticks : 0;
        MetricsStamp = MetricsLoader.Stamp(MetricsRoot);
    }

    public bool ShouldReload(long knownIrMtime, string knownMetricsStamp) =>
        IrMtime != knownIrMtime || MetricsStamp != knownMetricsStamp;

    public IrDocument ReloadDocument()
    {
        var doc = IrLoader.Load(IrPath);
        var metrics = MetricsLoader.Load(MetricsRoot);
        OverlayService.ApplyMetrics(doc, metrics);
        Document = doc;
        RefreshStamps();
        return doc;
    }

    public IrDocument ReloadFrom(string irPath)
    {
        IrPath = Path.GetFullPath(irPath);
        return ReloadDocument();
    }

    public void ReplaceDocument(IrDocument doc) => Document = doc;

    public void PersistProposals()
    {
        IrProposalPersistence.WriteProposals(IrPath, Document);
        RefreshStamps();
    }

    public MailboxCommand? PollViewerMail(Func<MailboxCommand, bool> handle)
    {
        while (true)
        {
            var cmd = MailboxService.TakeCommand(MailboxPaths.ToViewer(MetricsRoot), MailSeenViewer);
            if (cmd == null)
                return null;
            MailSeenViewer = cmd.Id;
            if (!handle(cmd))
                return cmd;
        }
    }
}
