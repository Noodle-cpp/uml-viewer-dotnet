namespace UmlViewer.Core.Mailbox;

public sealed class MailboxCommand
{
    public long Id { get; init; }
    public string Op { get; init; } = "";
    public IReadOnlyDictionary<string, object?> Extra { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}

public sealed class MailboxEnvelope
{
    public long NextId { get; init; } = 1;
    public IReadOnlyList<MailboxCommand> Queue { get; init; } = [];
}
