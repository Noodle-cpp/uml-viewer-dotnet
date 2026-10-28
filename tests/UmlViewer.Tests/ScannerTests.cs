using UmlViewer.Core.Metrics;
using UmlViewer.Scan.CSharp;

namespace UmlViewer.Tests;

public class ScannerTests
{
    [Fact]
    public void IdOf_strips_policy_prefix()
    {
        var id = CSharpProjectScanner.IdOf(
            "TP.Cmdb.Glpi.Sync.Application.Services.Foo",
            "TP.Cmdb.Glpi.Sync");
        Assert.Equal("Application.Services.Foo", id);
    }
}

public class MetricsReaderTests
{
    [Fact]
    public void ReadCrap_parses_entry()
    {
        var text = """
            {:entries [
              {:name "Run" :namespace "Demo.App.Board" :complexity 3 :coverage 50.0 :crap 6.4}
            ]}
            """;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, text);
            var entries = MetricsEdnReader.ReadCrap(path);
            Assert.Single(entries);
            Assert.Equal("Run", entries[0].Name);
            Assert.Equal("Demo.App.Board", entries[0].Namespace);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
