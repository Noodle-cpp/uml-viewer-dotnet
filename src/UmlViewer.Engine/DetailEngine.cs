using UmlViewer.Core.Models;

namespace UmlViewer.Engine;

public static partial class DetailEngine
{
    public const double Width = 640;
    public const double Height = 700;
    public const double Pad = 16;

    public sealed class RelRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "";
        public bool Outgoing { get; set; }
        public string Phrase { get; set; } = "";
    }

    public sealed class OpRow
    {
        public string Name { get; set; } = "";
        public double? Crap { get; set; }
        public double? Cc { get; set; }
        public double? Coverage { get; set; }
        public int? Killed { get; set; }
        public int? Survived { get; set; }
        public int? Uncovered { get; set; }
        public bool Private { get; set; }
    }

    public sealed class ClassMetricsRow
    {
        public double? CrapMu { get; set; }
        public double? Coverage { get; set; }
        public int Killed { get; set; }
        public int Survived { get; set; }
        public int Uncovered { get; set; }
    }

    public sealed class DetailModel
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Ns { get; set; } = "";
        public string? File { get; set; }
        public string ClassName { get; set; } = "";
        public IrCrapSummary? CrapSummary { get; set; }
        public ClassMetricsRow ClassMetrics { get; set; } = new();
        public List<OpRow> Ops { get; set; } = [];
        public List<RelRow> Rels { get; set; } = [];
    }

    private static readonly Dictionary<string, string[]> RelPhrases = new(StringComparer.Ordinal)
    {
        ["inheritance"] = ["extends", "extended by"],
        ["implements"] = ["implements", "implemented by"],
        ["association"] = ["associates with", "associated from"],
        ["dependency"] = ["depends on", "used by"],
        ["aggregation"] = ["aggregates", "aggregated by"],
        ["composition"] = ["composes", "composed in"],
    };

    public static DetailModel? Build(CompiledScene scene, string id)
    {
        if (!scene.ClassById.TryGetValue(id, out var c))
            return null;
        var src = c.Source;
        var rels = scene.Edges
            .Where(e => e.From == id || e.To == id)
            .Select(e =>
            {
                var outGoing = e.From == id;
                var oid = outGoing ? e.To : e.From;
                scene.ClassById.TryGetValue(oid, out var other);
                var phrases = RelPhrases.GetValueOrDefault(e.Kind) ?? ["to", "from"];
                return new RelRow
                {
                    Id = oid,
                    Name = other?.Source.Name ?? oid,
                    Kind = e.Kind,
                    Outgoing = outGoing,
                    Phrase = outGoing ? phrases[0] : phrases[1],
                };
            }).ToList();

        var ops = src.Ops.Select(o => new OpRow
        {
            Name = o.Name,
            Crap = o.Crap ?? src.CrapSummary?.Mu,
            Cc = o.Cc,
            Coverage = o.Coverage ?? src.Coverage,
            Killed = o.Killed,
            Survived = o.Survived,
            Uncovered = o.Uncovered,
            Private = o.Private == true,
        }).ToList();

        return new DetailModel
        {
            Id = id,
            Title = scene.Diagram.Title,
            Ns = src.Ns ?? id,
            File = src.File,
            ClassName = src.Name,
            CrapSummary = src.CrapSummary,
            ClassMetrics = new ClassMetricsRow
            {
                CrapMu = src.CrapSummary?.Mu,
                Coverage = src.Coverage,
                Killed = src.Killed ?? ops.Sum(x => x.Killed ?? 0),
                Survived = src.Survived ?? ops.Sum(x => x.Survived ?? 0),
                Uncovered = ops.Sum(x => x.Uncovered ?? 0),
            },
            Ops = ops,
            Rels = rels,
        };
    }
}
