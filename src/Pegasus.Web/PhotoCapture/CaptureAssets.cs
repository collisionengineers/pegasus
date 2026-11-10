using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;

namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// The capture app's image manifest: every file under
/// <c>wwwroot/capture/img</c>, keyed by its path below that folder
/// (<c>logo.webp</c>, <c>d/front-1-o.jpg</c>), mapped to its fingerprinted
/// address. The files are enumerated once; the JSON is kept per path base.
/// </summary>
public sealed class CaptureAssets
{
    private const string ImageFolder = "capture/img";

    private readonly IFileVersionProvider fileVersions;
    private readonly Lazy<IReadOnlyList<string>> files;
    private readonly ConcurrentDictionary<string, string> manifests = new(StringComparer.Ordinal);

    public CaptureAssets(IWebHostEnvironment environment, IFileVersionProvider fileVersions)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(fileVersions);
        this.fileVersions = fileVersions;
        files = new(() => Enumerate(environment.WebRootFileProvider));
    }

    /// <summary>The manifest as JSON, safe to embed in a script block.</summary>
    public string ManifestJson(PathString pathBase) =>
        manifests.GetOrAdd(pathBase.Value ?? string.Empty, _ => JsonSerializer.Serialize(
            files.Value.ToDictionary(
                name => name,
                name => fileVersions.AddFileVersionToPath(pathBase, "/" + ImageFolder + "/" + name),
                StringComparer.Ordinal),
            CaptureContextFactory.JsonOptions));

    private static List<string> Enumerate(IFileProvider provider)
    {
        var names = new List<string>();
        Walk(provider, ImageFolder, string.Empty, names);
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static void Walk(IFileProvider provider, string folder, string prefix, List<string> names)
    {
        foreach (var entry in provider.GetDirectoryContents(folder))
        {
            if (entry.IsDirectory)
            {
                Walk(provider, folder + "/" + entry.Name, prefix + entry.Name + "/", names);
            }
            else
            {
                names.Add(prefix + entry.Name);
            }
        }
    }
}
