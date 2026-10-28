using UmlViewer.Core.Models;
using UmlViewer.Engine;

namespace UmlViewer.Tests;

public class ProposalTests
{
    [Fact]
    public void ProposalView_builds_packages_and_remaps_edges()
    {
        var doc = SampleDoc();
        var view = ProposalHierarchy.ProposalView(doc, "p1");
        Assert.True(view.Packages.Count >= 2);
        Assert.Contains(view.Packages, p => p.Id == "proposal.outer");
        Assert.Contains(view.Packages, p => p.Id == "proposal.inner");
        Assert.All(view.Edges, e =>
        {
            Assert.Contains(view.Packages.SelectMany(p => p.Classes), c => c.Id == e.From);
            Assert.Contains(view.Packages.SelectMany(p => p.Classes), c => c.Id == e.To);
        });
    }

    [Fact]
    public void LayerView_shows_single_proposal_package()
    {
        var doc = SampleDoc();
        var layer = ProposalHierarchy.LayerView(doc, "p1", "proposal.outer");
        Assert.Single(layer.Packages);
        Assert.Equal("proposal.outer", layer.Packages[0].Id);
    }

    [Fact]
    public void LayerView_expands_nested_group()
    {
        var doc = SampleDocWithGroup();
        var root = ProposalHierarchy.ProposalView(doc, "p1");
        var group = root.Packages.SelectMany(p => p.Classes).First(c => c.ProposalGroup);
        var layer = ProposalHierarchy.LayerView(doc, "p1", group.Id);
        Assert.Single(layer.Packages);
        Assert.True(layer.Packages.SelectMany(p => p.Classes).Count() >= 2);
        Assert.NotEmpty(layer.Edges);
    }

    private static IrDocument SampleDoc()
    {
        return new IrDocument
        {
            Title = "Sample",
            Hierarchical = true,
            Classes =
            [
                new IrClass { Id = "App", Name = "App" },
                new IrClass { Id = "App.Services", Name = "Services" },
                new IrClass { Id = "App.Data", Name = "Data" },
                new IrClass { Id = "App.Data.Repo", Name = "Repo" },
            ],
            Edges =
            [
                new IrEdge { From = "App.Services", To = "App.Data.Repo", Kind = "dependency" },
                new IrEdge { From = "App.Data.Repo", To = "App.Services", Kind = "dependency" },
            ],
            Proposals =
            [
                new IrProposal
                {
                    Id = "p1",
                    Name = "Test",
                    Layers =
                    [
                        new IrProposalLayer
                        {
                            Id = "outer",
                            Label = "Outer",
                            Nses = [IrNseNode.Leaf("App")],
                        },
                        new IrProposalLayer
                        {
                            Id = "inner",
                            Label = "Inner",
                            Nses = [IrNseNode.Leaf("App.Services"), IrNseNode.Leaf("App.Data")],
                        },
                    ],
                },
            ],
        };
    }

    private static IrDocument SampleDocWithGroup()
    {
        return new IrDocument
        {
            Title = "Grouped",
            Classes =
            [
                new IrClass { Id = "A", Name = "A" },
                new IrClass { Id = "A.One", Name = "One" },
                new IrClass { Id = "A.Two", Name = "Two" },
                new IrClass { Id = "B", Name = "B" },
            ],
            Edges =
            [
                new IrEdge { From = "A.One", To = "A.Two", Kind = "dependency" },
            ],
            Proposals =
            [
                new IrProposal
                {
                    Id = "p1",
                    Name = "G",
                    Layers =
                    [
                        new IrProposalLayer
                        {
                            Id = "L1",
                            Label = "Layer",
                            Nses =
                            [
                                IrNseNode.Group("grp", "Group", [
                                    IrNseNode.Leaf("A.One"),
                                    IrNseNode.Leaf("A.Two"),
                                ]),
                                IrNseNode.Leaf("B"),
                            ],
                        },
                    ],
                },
            ],
        };
    }
}
