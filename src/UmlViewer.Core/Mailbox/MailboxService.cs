namespace UmlViewer.Core.Mailbox;

public static class MailboxService
{
    private const int KeepCommands = 32;

    public static long LastId(string filePath) =>
        MailboxEdn.ReadEnvelope(filePath).Queue.LastOrDefault()?.Id ?? 0;

    public static MailboxCommand WriteCommand(string filePath, string op, IReadOnlyDictionary<string, object?>? extra = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var box = MailboxEdn.ReadEnvelope(filePath);
        var id = box.NextId <= 0 ? 1 : box.NextId;
        var cmd = new MailboxCommand
        {
            Id = id,
            Op = op,
            Extra = extra ?? new Dictionary<string, object?>(StringComparer.Ordinal),
        };
        var kept = box.Queue.Where(c => c.Id > id - KeepCommands).ToList();
        kept.Add(cmd);
        var next = new MailboxEnvelope { NextId = id + 1, Queue = kept };
        AtomicWrite(filePath, MailboxEdn.WriteEnvelope(next));
        return cmd;
    }

    public static MailboxCommand? TakeCommand(string filePath, long seenId)
    {
        var box = MailboxEdn.ReadEnvelope(filePath);
        var pending = box.Queue.Where(c => c.Id > seenId).OrderBy(c => c.Id).ToList();
        if (pending.Count == 0)
            return null;
        var cmd = pending[0];
        var rest = box.Queue.Where(c => c.Id != cmd.Id).ToList();
        AtomicWrite(filePath, MailboxEdn.WriteEnvelope(new MailboxEnvelope
        {
            NextId = box.NextId,
            Queue = rest,
        }));
        return cmd;
    }

    public static void AtomicWrite(string filePath, string content)
    {
        var tmp = filePath + ".tmp";
        File.WriteAllText(tmp, content);
        try
        {
            File.Move(tmp, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.Move(tmp, filePath);
        }
    }
}
