using UmlViewer.Core.Mailbox;

namespace UmlViewer.Tests;

public class MailboxTests
{
    [Fact]
    public void Write_and_take_command_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "uml-mail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "to-agent.edn");
        try
        {
            var written = MailboxService.WriteCommand(file, "regen");
            Assert.Equal("regen", written.Op);
            var taken = MailboxService.TakeCommand(file, 0);
            Assert.NotNull(taken);
            Assert.Equal(written.Id, taken!.Id);
            Assert.Null(MailboxService.TakeCommand(file, taken.Id));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
