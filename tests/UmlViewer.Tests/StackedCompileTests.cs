using UmlViewer.Core.Edn;
using UmlViewer.Engine;

namespace UmlViewer.Tests;

public class StackedCompileTests
{
    [Fact]
    public void CompileDocument_stacks_section_titles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "uml-stack-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "doc.edn");
        try
        {
            File.WriteAllText(path,
                """
                {:title "Doc"
                 :hierarchical false
                 :diagrams [{:title "One"
                              :packages [{:id :p :label "P" :classes [{:id :a :name "A"}]}]
                              :edges []}
                             {:title "Two"
                              :packages [{:id :p :label "P" :classes [{:id :b :name "B"}]}]
                              :edges []}]}
                """);
            var doc = EdnIrLoader.Load(path);
            var scene = DiagramCompiler.Compile(doc, []);
            Assert.Equal(["One", "Two"], scene.Sections.Select(s => s.Title).ToList());
            Assert.True(scene.Sections[1].TitleY > scene.Sections[0].TitleY);
            Assert.NotEmpty(scene.Classes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
