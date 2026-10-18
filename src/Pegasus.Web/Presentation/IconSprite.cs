using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The one owner of the icon sprite's address. The sprite is the static file
/// <c>wwwroot/images/lucide-sprite.svg</c>; a Razor call site draws a glyph as
/// <c>&lt;use href="@IconSprite.Href("icon-…")"&gt;</c>, and the three html
/// shells publish <see cref="Url"/> as <c>data-icon-sprite</c> for site.js.
/// </summary>
/// <remarks>
/// The address carries the file's content fingerprint, so a changed sprite is a
/// new URL and an unchanged one is never fetched again. The static-asset map
/// names the fingerprinted file where the endpoint carries it (the same address
/// the tag helpers give the PNG marks); otherwise the file's content hash rides
/// as <c>?v=</c>. Either way the request's PathBase leads the address.
/// </remarks>
public sealed class IconSprite(
    IHttpContextAccessor httpContextAccessor,
    IFileVersionProvider fileVersions)
{
    private const string AssetPath = "images/lucide-sprite.svg";

    private string? url;

    /// <summary>The fingerprinted address of the sprite file.</summary>
    public string Url => url ??= Resolve();

    /// <summary>The address of one glyph in the sprite, from its symbol id (<c>icon-…</c>).</summary>
    public string Href(string icon) => Url + "#" + icon;

    private string Resolve()
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("The icon sprite's address is read while a request renders.");
        var pathBase = context.Request.PathBase;
        var asset = context.GetEndpoint()?.Metadata.GetMetadata<ResourceAssetCollection>()?[AssetPath]
            ?? AssetPath;
        return fileVersions.AddFileVersionToPath(pathBase, pathBase + "/" + asset);
    }
}
