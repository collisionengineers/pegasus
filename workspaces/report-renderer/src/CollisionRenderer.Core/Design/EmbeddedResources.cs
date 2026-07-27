using System.Reflection;

namespace CollisionRenderer.Core.Design;

/// <summary>
/// Loads files embedded in the Core assembly (templates, stylesheet, brand images,
/// sample payloads). Resources are matched by their trailing path so callers can use
/// natural relative paths (e.g. "templates/report.css") regardless of how MSBuild
/// mangles the manifest resource name.
/// </summary>
internal static class EmbeddedResources
{
    private static readonly Assembly Asm = typeof(EmbeddedResources).Assembly;
    private static readonly string[] Names = Asm.GetManifestResourceNames();

    public static IReadOnlyList<string> All => Names;

    public static Stream Open(string relativePath)
    {
        var name = Resolve(relativePath);
        return Asm.GetManifestResourceStream(name)
               ?? throw new FileNotFoundException($"Embedded resource stream missing: {name}");
    }

    public static byte[] ReadBytes(string relativePath)
    {
        using var s = Open(relativePath);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public static string ReadText(string relativePath)
    {
        using var s = Open(relativePath);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public static bool Exists(string relativePath) => TryResolve(relativePath, out _);

    private static string Resolve(string relativePath)
    {
        if (TryResolve(relativePath, out var name))
        {
            return name!;
        }

        throw new FileNotFoundException(
            $"Embedded resource not found: '{relativePath}'. Known resources: {string.Join(", ", Names)}");
    }

    private static bool TryResolve(string relativePath, out string? name)
    {
        var tail = "." + relativePath.Replace('\\', '/').Replace('/', '.');
        name = Names.FirstOrDefault(n => n.EndsWith(tail, StringComparison.OrdinalIgnoreCase));
        return name is not null;
    }
}
