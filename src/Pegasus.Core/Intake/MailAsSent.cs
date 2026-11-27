using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

/// <summary>The sanitised, self-contained HTML document of one e-mail as it was sent.</summary>
public sealed record MailAsSentRendering(string Html);

public interface IRenderMailAsSent
{
    /// <summary>Changes whenever the rendering rules change, so a browser's kept copy is not reused.</summary>
    string Version { get; }

    /// <summary>
    /// The formatted view of the e-mail in <paramref name="original"/> (its raw MIME), or null when the
    /// message it shows has no HTML part or the bytes cannot be read as a message. Never throws for bad input.
    /// </summary>
    MailAsSentRendering? Render(ReadOnlyMemory<byte> original, bool isStaffForward);
}

public enum MailAsSentStatus { Available, NoFormatting, Unavailable }

/// <param name="Html">The document, when <see cref="MailAsSentStatus.Available"/>.</param>
/// <param name="Version">The content address of that document: the original's hash and the renderer's version.</param>
public sealed record MailAsSent(MailAsSentStatus Status, string? Html, string? Version);

/// <summary>What the store knows of a kept message's original.</summary>
public sealed record RetainedMailOriginal(Guid? IntakeReceiptId, string? SourceSha256, MailFolderScope Folder, bool IsStaffForward, bool? HasHtmlBody);

/// <summary>
/// One kept e-mail as it was sent (R1277, R1279, R1280): its original, read
/// from the receipt's source where it has one and from the kept original by
/// its hash where it does not, rendered safe.
/// </summary>
public sealed class GetMailAsSent(IRetainedMailQueries queries, IDownloadIntakeSource sources, IIntakeArtifactStore artifacts, IRenderMailAsSent renderer)
{
    private static readonly MailAsSent NoFormatting = new(MailAsSentStatus.NoFormatting, null, null);
    private static readonly MailAsSent Unavailable = new(MailAsSentStatus.Unavailable, null, null);

    /// <summary>Null when there is no such kept message.</summary>
    public async Task<MailAsSent?> ExecuteAsync(ActionActor actor, Guid messageId, CancellationToken cancellationToken = default)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (messageId == Guid.Empty)
        {
            return null;
        }

        var original = await queries.GetOriginalAsync(messageId, cancellationToken);
        if (original is null)
        {
            return null;
        }

        if (original.HasHtmlBody == false)
        {
            return NoFormatting;
        }

        var read = await ReadAsync(actor, original, cancellationToken);
        if (read is not { } kept)
        {
            return Unavailable;
        }

        var rendering = renderer.Render(kept.Content, original.IsStaffForward);
        return rendering is null
            ? NoFormatting
            : new MailAsSent(MailAsSentStatus.Available, rendering.Html, VersionOf(kept.Sha256, renderer.Version));
    }

    /// <summary>The address a rendering of an original with this hash is kept under by the browser.</summary>
    public static string VersionOf(string sourceSha256, string rendererVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(rendererVersion);
        var hash = sourceSha256.Trim();
        return $"{hash[..Math.Min(hash.Length, 16)].ToLowerInvariant()}-{rendererVersion}";
    }

    /// <summary>
    /// The original's bytes and their hash: the receipt's verified source where
    /// the message has a receipt, else the kept original by the row's hash.
    /// Null when neither is there, or the source's custody or integrity cannot
    /// be relied on.
    /// </summary>
    private async Task<(ReadOnlyMemory<byte> Content, string Sha256)?> ReadAsync(
        ActionActor actor,
        RetainedMailOriginal original,
        CancellationToken cancellationToken)
    {
        try
        {
            if (original.IntakeReceiptId is { } receiptId)
            {
                var download = await sources.ExecuteAsync(new DownloadIntakeSourceQuery(receiptId, actor), cancellationToken);
                return download is null ? null : (download.Content, download.Sha256);
            }

            if (!string.IsNullOrWhiteSpace(original.SourceSha256))
            {
                var content = await artifacts.ReadByHashAsync(original.SourceSha256, cancellationToken);
                return content is { } bytes ? (bytes, original.SourceSha256) : null;
            }

            return null;
        }
        catch (IntakeCustodyUnavailableException)
        {
            return null;
        }
        catch (IntakeArtifactIntegrityException)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }
}
