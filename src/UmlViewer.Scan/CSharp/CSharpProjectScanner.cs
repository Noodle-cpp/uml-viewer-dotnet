using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UmlViewer.Core.Models;

namespace UmlViewer.Scan.CSharp;

public static class CSharpProjectScanner
{
    private static readonly HashSet<string> SkipDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".vs", "target", "dist", "build", "out", "coverage",
        "node_modules", "test", "tests", "spec", "specs", "__tests__",
    };

    private static readonly string[] GeneratedSuffixes =
        [".Designer.cs", ".g.cs", ".generated.cs"];

    private static readonly string[] TestSuffixes = ["Tests.cs", "Test.cs"];

    public static IrDocument Scan(ScanPolicy policy)
    {
        var classes = new List<IrClass>();
        var edges = new List<IrEdge>();

        foreach (var source in policy.Sources.Where(s => s.Lang.Equals("csharp", StringComparison.OrdinalIgnoreCase)))
        {
            var root = Path.GetFullPath(source.Root);
            foreach (var file in EnumerateCsFiles(root))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                foreach (var cls in ParseFile(file, relative, policy.Prefix))
                    classes.Add(cls);
            }
        }

        classes = MergePartials(classes);
        LinkEdges(classes, edges);

        return new IrDocument
        {
            Title = policy.Title,
            Prefix = policy.Prefix,
            Hierarchical = policy.Hierarchical,
            Classes = classes,
            Edges = edges,
        };
    }

    private static IEnumerable<string> EnumerateCsFiles(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (PathContainsSkippedDir(file, root))
                continue;
            var name = Path.GetFileName(file);
            if (IsExcludedFile(name))
                continue;
            if (IsUnderTestTree(file))
                continue;
            yield return file;
        }
    }

    private static bool IsExcludedFile(string name) =>
        GeneratedSuffixes.Any(name.EndsWith) || TestSuffixes.Any(name.EndsWith);

    private static bool IsUnderTestTree(string filePath)
    {
        foreach (var part in filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (IsTestTreeSegment(part))
                return true;
        }

        return false;
    }

    private static bool IsTestTreeSegment(string segment)
    {
        var s = segment.ToLowerInvariant();
        return s.EndsWith(".tests", StringComparison.Ordinal)
               || s.EndsWith(".integrationtests", StringComparison.Ordinal)
               || s.EndsWith(".testutils", StringComparison.Ordinal);
    }

    private static bool ShouldSkipDir(string name) =>
        name.StartsWith('.') || SkipDirNames.Contains(name) || IsTestTreeSegment(name);

    private static IEnumerable<IrClass> ParseFile(string path, string relative, string prefix)
    {
        var text = File.ReadAllText(path);
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetCompilationUnitRoot();

        foreach (var nsDecl in root.Members.OfType<BaseNamespaceDeclarationSyntax>())
        {
            var ns = nsDecl.Name.ToString();
            foreach (var type in nsDecl.Members.OfType<TypeDeclarationSyntax>())
            {
                foreach (var cls in TypeToIr(type, ns, relative, prefix))
                    yield return cls;
            }
        }

        foreach (var type in root.Members.OfType<TypeDeclarationSyntax>())
        {
            foreach (var cls in TypeToIr(type, "", relative, prefix))
                yield return cls;
        }
    }

    private static IEnumerable<IrClass> TypeToIr(TypeDeclarationSyntax type, string ns, string relative, string prefix)
    {
        var typeName = type.Identifier.Text;
        var qualified = string.IsNullOrEmpty(ns) ? typeName : $"{ns}.{typeName}";
        var ops = CollectMethods(type).Select(n => new IrOp { Name = n, Text = n }).ToList();
        yield return new IrClass
        {
            Id = IdOf(qualified, prefix),
            Name = typeName,
            Ns = qualified,
            File = relative,
            Lang = "csharp",
            Ops = ops,
        };
    }

    private static List<string> CollectMethods(TypeDeclarationSyntax type)
    {
        var typeName = type.Identifier.Text;
        var names = new List<string>();
        foreach (var member in type.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;
            if (method.Modifiers.Any(SyntaxKind.PrivateKeyword))
                continue;
            var name = method.Identifier.Text;
            if (name != typeName && !IsAccessorName(name))
                names.Add(name);
        }

        foreach (var member in type.Members)
        {
            if (member is not MethodDeclarationSyntax method)
                continue;
            if (method.ExplicitInterfaceSpecifier == null)
                continue;
            var name = method.Identifier.Text;
            if (name != typeName)
                names.Add(name);
        }

        return names.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool IsAccessorName(string name) =>
        name is "get" or "set" or "add" or "remove";

    private static List<IrClass> MergePartials(List<IrClass> classes)
    {
        return classes
            .GroupBy(c => c.Id, StringComparer.Ordinal)
            .Select(g =>
            {
                var first = g.First();
                return new IrClass
                {
                    Id = first.Id,
                    Name = first.Name,
                    Ns = first.Ns,
                    File = first.File,
                    Lang = first.Lang,
                    Ops = g.SelectMany(c => c.Ops).GroupBy(o => o.Name).Select(x => x.First()).ToList(),
                };
            })
            .OrderBy(c => c.Ns, StringComparer.Ordinal)
            .ToList();
    }

    private static void LinkEdges(List<IrClass> classes, List<IrEdge> edges)
    {
        _ = classes;
        _ = edges;
    }

    private static bool PathContainsSkippedDir(string file, string root)
    {
        var rel = Path.GetRelativePath(root, file);
        foreach (var part in rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (ShouldSkipDir(part))
                return true;
        }

        return false;
    }

    public static string IdOf(string ns, string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return ns;
        var dotted = prefix + ".";
        return ns.StartsWith(dotted, StringComparison.Ordinal) ? ns[dotted.Length..] : ns;
    }
}
