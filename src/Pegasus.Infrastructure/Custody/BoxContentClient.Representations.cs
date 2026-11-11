using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Box's PDF representation of a stored file: the rendering Box makes of a
/// document the browser cannot draw (a Word document), asked for by exact
/// version and under the same custody checks as the file's own content.
/// </summary>
/// <remarks>
/// Observed against the test estate on 11 November 2026: the representation
/// is listed on <c>files/{id}</c> when <c>x-rep-hints: [pdf]</c> is sent; its
/// state is <c>success</c> when Box already holds it and <c>pending</c> for
/// two to three seconds when it does not; the whole document is the content
/// address with an empty asset path; the content host is
/// <c>dl.boxcloud.com</c> and refuses a request without the bearer token; and
/// the addresses name the file's current version.
/// </remarks>
internal sealed partial class BoxContentClient
{
    private const string AssetPathVariable = "{+asset_path}";

    private readonly record struct PdfRepresentation(string State, Uri Info, Uri Content);

    /// <summary>
    /// Where Box serves the PDF representation of one exact stored version,
    /// once Box has made it; <c>null</c> when Box offers none for this version.
    /// </summary>
    /// <remarks>
    /// The file passes the read path's checks first: it is a file, it is not
    /// in the trash, its parent is the expected folder and that folder sits
    /// under the approved root. A representation describes the file's current
    /// version, so a stored version that is no longer current has none. A
    /// representation still being made after <paramref name="wait"/> is a
    /// <see cref="TimeoutException"/>: Box goes on making it, and a later call
    /// finds it.
    /// </remarks>
    public async Task<Uri?> FindOwnedVersionPdfAsync(
        string fileId,
        string versionId,
        string expectedParentId,
        TimeSpan wait,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedParentId);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var representation = await ReadPdfRepresentationAsync(
                fileId, versionId, expectedParentId, cancellationToken);
            if (representation is not { } found)
            {
                return null;
            }

            var state = found.State;
            for (var poll = 0; !string.Equals(state, "success", StringComparison.Ordinal); poll++)
            {
                if (state is not ("none" or "pending"))
                {
                    // Box could not make one of this file ("error"), or said
                    // something this reader does not know.
                    return null;
                }
                var delay = TimeSpan.FromMilliseconds(250 * (1 << Math.Min(poll, 2)));
                if (Stopwatch.GetElapsedTime(started) + delay >= wait)
                {
                    throw new TimeoutException("Box is still making the PDF representation.");
                }
                await Task.Delay(delay, cancellationToken);
                // Asking for the representation's own record is also what
                // starts a representation Box has not begun.
                using var response = await SendAsync(HttpMethod.Get, found.Info, null, cancellationToken);
                using var info = await ReadSuccessJsonAsync(response, cancellationToken);
                state = ReadNestedString(info.RootElement, "status", "state") ?? string.Empty;
            }
            return found.Content;
        }
        catch (Exception exception) when (IsFenceFailure(exception))
        {
            Forget(expectedParentId);
            throw;
        }
    }

    /// <summary>
    /// The PDF at an address <see cref="FindOwnedVersionPdfAsync"/> returned,
    /// in a temporary file that is deleted when the stream is disposed.
    /// </summary>
    public async Task<Stream> DownloadPdfRepresentationAsync(
        Uri content,
        long maximumLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        if (!IsBoxAddress(content, contentHost: true))
        {
            throw new ArgumentException("The address is not a Box content address.", nameof(content));
        }
        using var response = await SendAsync(HttpMethod.Get, content, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            throw new TimeoutException("Box has not finished the PDF representation.");
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Box representation download returned {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
        return await RetainContentAsync(
            response,
            maximumLength,
            contentHash: null,
            "The Box PDF representation exceeds the content limit.",
            cancellationToken);
    }

    private async Task<PdfRepresentation?> ReadPdfRepresentationAsync(
        string fileId,
        string versionId,
        string expectedParentId,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            new Uri(options.BaseUri,
                $"files/{Uri.EscapeDataString(fileId)}?fields=id,name,type,etag,file_version,size,content_type,parent,trashed_at,representations"),
            null,
            cancellationToken,
            request => request.Headers.Add("x-rep-hints", "[pdf]"));
        using var metadata = await ReadSuccessJsonAsync(response, cancellationToken);
        var file = RequireOwnedFile(metadata.RootElement, expectedParentId);
        await EnsureLiveFolderForReadAsync(expectedParentId, cancellationToken);
        if (!string.Equals(file.VersionId, versionId, StringComparison.Ordinal)
            || !metadata.RootElement.TryGetProperty("representations", out var representations)
            || representations.ValueKind != JsonValueKind.Object
            || !representations.TryGetProperty("entries", out var entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !string.Equals(ReadString(entry, "representation"), "pdf", StringComparison.Ordinal))
            {
                continue;
            }
            // The addresses are Box's to choose. The bearer token goes only to
            // Box's own hosts, so an address anywhere else is no representation.
            var template = ReadNestedString(entry, "content", "url_template");
            return ReadNestedString(entry, "status", "state") is { } state
                && Uri.TryCreate(ReadNestedString(entry, "info", "url"), UriKind.Absolute, out var info)
                && IsBoxAddress(info, contentHost: false)
                && template is not null
                && Uri.TryCreate(
                    template.Replace(AssetPathVariable, string.Empty, StringComparison.Ordinal),
                    UriKind.Absolute,
                    out var content)
                && IsBoxAddress(content, contentHost: true)
                    ? new PdfRepresentation(state, info, content)
                    : null;
        }
        return null;
    }

    private bool IsBoxAddress(Uri address, bool contentHost) =>
        address.IsAbsoluteUri
        && string.Equals(address.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && (string.Equals(address.Host, options.BaseUri.Host, StringComparison.OrdinalIgnoreCase)
            || (contentHost && address.Host.EndsWith(".boxcloud.com", StringComparison.OrdinalIgnoreCase)));

    private static string? ReadNestedString(JsonElement value, string parent, string property) =>
        value.ValueKind == JsonValueKind.Object
        && value.TryGetProperty(parent, out var nested)
        && nested.ValueKind == JsonValueKind.Object
            ? ReadString(nested, property)
            : null;
}
