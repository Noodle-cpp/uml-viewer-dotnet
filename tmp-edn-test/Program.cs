using EdnParser;

var text = File.ReadAllText(@"D:\Users\Noodle\Desktop\ub\uml-viewer\target\glpi-scan.edn");
var tokens = new Parser().Parse(text);
var root = tokens.First(t => t is not EdnComment);
Console.WriteLine("root " + root.GetType().Name);
if (root is EdnMap map)
{
    foreach (var kv in map.Entries().Take(8))
        Console.WriteLine(EdnKeys.KeyName(kv.Key) + " => " + kv.Value.GetType().Name);
    var classesKey = map.Entries().First(kv => EdnKeys.KeyName(kv.Key) == "classes");
    if (classesKey.Value is EdnVector vec)
    {
        var first = vec.Values().First();
        if (first is EdnMap cm)
        {
            foreach (var kv in cm.Entries())
                Console.WriteLine("  " + EdnKeys.KeyName(kv.Key) + " = " + EdnKeys.AsString(kv.Value));
        }
    }
}

static class EdnKeys
{
    public static string KeyName(EdnValue k) => k switch
    {
        EdnKeyword kw => kw.ToString()!.Trim(':'),
        EdnSymbol sym => sym.ToString()!,
        _ => k.ToString() ?? ""
    };

    public static string AsString(EdnValue v) => v switch
    {
        EdnString s => s.ToString()!,
        EdnKeyword kw => kw.ToString()!,
        EdnSymbol sym => sym.ToString()!,
        _ => v.ToString() ?? ""
    };
}
