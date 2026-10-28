using UmlViewer.Core.Edn;
using UmlViewer.Core.Models;

namespace UmlViewer.Core.Io;

public static class IrLoader
{
    public static IrDocument Load(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".edn" => EdnIrLoader.Load(path),
            ".json" => IrJson.Read(path),
            _ => throw new NotSupportedException($"IR format '{ext}' is not supported. Use .edn or .json."),
        };
    }
}
