namespace UmlViewer.Core.Mailbox;

public static class MailboxPaths
{
    public const string DirName = ".uml-viewer";
    public const string ToViewerName = "to-viewer.edn";
    public const string ToAgentName = "to-agent.edn";
    public const string SessionName = "session.edn";
    public const string CompanionName = "companion.edn";

    public static string MailboxDir(string metricsRoot) =>
        Path.Combine(metricsRoot, DirName);

    public static string ToViewer(string metricsRoot) =>
        Path.Combine(MailboxDir(metricsRoot), ToViewerName);

    public static string ToAgent(string metricsRoot) =>
        Path.Combine(MailboxDir(metricsRoot), ToAgentName);

    public static string SessionFile(string metricsRoot) =>
        Path.Combine(MailboxDir(metricsRoot), SessionName);

    public static string CompanionFile(string metricsRoot) =>
        Path.Combine(MailboxDir(metricsRoot), CompanionName);
}
