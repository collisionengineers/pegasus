using System.Security.Cryptography;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.ThirdPartyReports;

namespace Pegasus.Core.Intake;

/// <summary>
/// The one owner of which images are the instruction's evidence photographs.
/// Retention applies the photograph rule to every image a reading yields,
/// attached, inline or embedded in a PDF or Word document alike, so only
/// photographs are stored as images; a directly uploaded image source is
/// always kept. One photograph carried twice (attached and embedded, or
/// repeated across pages) appears once, preferring the attached copy.
/// Custody promotion and the case evidence gallery both resolve through
/// this selection.
/// </summary>
public static class InstructionEvidenceImages
{
    /// <summary>
    /// The shortest side, in pixels, below which an image is a logo, icon or
    /// signature graphic rather than a photograph (operator, 26 October
    /// 2026). It replaces a 40 KB byte floor, which measured compression
    /// rather than content and dropped real photographs: a.QDOS26120's five
    /// 576x768 damage photographs were 22–33 KB each. Across the corpus the
    /// side floor keeps 249 images the byte floor dropped, most of them
    /// photographs. Of the images the byte floor kept it drops 17: icons,
    /// small logos, two adverts and one 250x146 number-plate crop. A capture
    /// PDF's page icon measures 70x65.
    /// </summary>
    public const int MinimumPhotographSidePixels = 200;

    /// <summary>
    /// Corpus-measured shape test, and the one that separates a banner from
    /// a photograph. QDOS26008's two false positives were a 110,783-byte PNG
    /// at 1990x437 and a 77,972-byte JPEG at 2214x248, both large files. The
    /// same 1990x437 letterhead appears in five unrelated reports across the
    /// corpus.
    ///
    /// Every genuine photograph on that receipt measured between 1.09 and
    /// 1.15, and the widest thing in the corpus sample that might be a
    /// photograph measured 2.22, so 3.0 sits in open space: wide enough to
    /// leave a panoramic photograph alone, narrow enough to catch every
    /// banner measured (3.19, 3.30, 4.55, 8.93, 9.08).
    /// </summary>
    public const double MaximumPhotographSideRatio = 3.0;

    /// <summary>
    /// The retained images that are evidence photographs, in display order,
    /// one per content. Retention already applied the photograph rule, so
    /// every retained JPEG or PNG qualifies.
    /// </summary>
    public static IReadOnlyList<IntakeAssetRecord> Select(
        IEnumerable<IntakeAssetRecord> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        return assets
            .Where(asset => IsImage(asset.MediaType))
            .OrderBy(asset => asset.Kind switch
            {
                IntakeAssetKind.Attachment => 0,
                IntakeAssetKind.Source => 1,
                _ => 2
            })
            .ThenBy(asset => asset.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(asset => asset.Id)
            .DistinctBy(asset => asset.ContentHash, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// The reader candidates that become separately retained receipt assets:
    /// those <see cref="IsRetentionCandidate"/> keeps, less letterhead art,
    /// meaning an image placed on every page of a PDF of two or more pages
    /// (operator, 22 October 2026). Inside a capture PDF an image must also
    /// be one of its designated photographs (FRD-02 EXT-17). The capture
    /// designation is the caller's, computed once per reading and shared with
    /// the assessment.
    /// </summary>
    internal static IEnumerable<IntakeAssetCandidate> RetentionCandidates(
        IntakeSourceReadResult readResult,
        VehicleCaptureDesignation capture)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        ArgumentNullException.ThrowIfNull(capture);
        var letterhead = LetterheadArt(readResult);
        return readResult.AssetCandidates.Where(candidate =>
            IsRetentionCandidate(candidate)
            && !letterhead.Contains(candidate)
            && (candidate.Kind != IntakeAssetKind.EmbeddedImage
                || !capture.Covers(candidate)
                || capture.IsPhotograph(candidate)));
    }

    /// <summary>
    /// Two buffers with the same bytes. The placements of one PDF image
    /// object share one buffer, which compares equal at once.
    /// </summary>
    internal static IEqualityComparer<ReadOnlyMemory<byte>> SameBytes { get; } = new SameBytesComparer();

    /// <summary>
    /// The embedded images whose content is placed on every page of a PDF of
    /// two or more pages. A PDF whose image read stopped early has pages
    /// without placements, so its images are kept. One pass groups the
    /// embedded images by the PDF that carries them, and each group is checked
    /// against its own PDF's page count.
    /// </summary>
    private static HashSet<IntakeAssetCandidate> LetterheadArt(IntakeSourceReadResult readResult)
    {
        var art = new HashSet<IntakeAssetCandidate>(ReferenceEqualityComparer.Instance);
        var pageCounts = readResult.PdfReadings
            .Where(pdf => pdf.PageCount >= 2)
            .ToLookup(pdf => pdf.SourceLabel, pdf => pdf.PageCount, StringComparer.Ordinal);
        if (pageCounts.Count == 0)
        {
            return art;
        }

        foreach (var pdf in readResult.AssetCandidates
                     .Where(candidate => candidate.Kind == IntakeAssetKind.EmbeddedImage
                         && candidate.PageNumber is not null)
                     .GroupBy(candidate => PdfLabel(candidate.SourceLabel), StringComparer.Ordinal))
        {
            if (!pageCounts.Contains(pdf.Key))
            {
                continue;
            }

            var counts = pageCounts[pdf.Key];
            foreach (var placements in pdf.GroupBy(candidate => candidate.Content, SameBytes))
            {
                if (counts.Contains(placements.Select(candidate => candidate.PageNumber).Distinct().Count()))
                {
                    art.UnionWith(placements);
                }
            }
        }

        return art;
    }

    /// <summary>The label of the PDF an embedded image's "label, page n, image m" names.</summary>
    private static string PdfLabel(string imageLabel)
    {
        var page = imageLabel.LastIndexOf(", page ", StringComparison.Ordinal);
        return page < 0 ? imageLabel : imageLabel[..page];
    }

    private sealed class SameBytesComparer : IEqualityComparer<ReadOnlyMemory<byte>>
    {
        public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y) => x.Span.SequenceEqual(y.Span);

        public int GetHashCode(ReadOnlyMemory<byte> obj) => obj.Length;
    }

    /// <summary>
    /// Decides whether a reader candidate becomes a separately retained
    /// receipt asset, before storage, so excluded material never enters
    /// custody. A non-image attachment (a PDF, a Word document, a forwarded
    /// e-mail) is kept as a document. A JPEG or PNG, whether attached, inline
    /// or embedded, is kept only when it is a photograph: at least
    /// <see cref="MinimumPhotographSidePixels"/> on its shortest side, sides
    /// in a ratio under <see cref="MaximumPhotographSideRatio"/>, and not one
    /// of the <see cref="KnownGraphics"/>.
    /// </summary>
    public static bool IsRetentionCandidate(IntakeAssetCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return IsImage(candidate.MediaType)
            ? IsPhotographShaped(candidate.WidthPixels, candidate.HeightPixels)
                && !KnownGraphics.Contains(candidate)
            : candidate.Kind == IntakeAssetKind.Attachment;
    }

    /// <summary>
    /// Whether retained material contains at least one selected photograph.
    /// Image automation uses this rather than inferring eligibility from the
    /// receipt source MIME type, because an email or PDF can carry the
    /// photographs while remaining attached as source evidence itself.
    /// </summary>
    public static bool HasSelectedPhotographs(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Select(receipt.AssetRecords).Count != 0;
    }

    /// <summary>
    /// The evidence images whose durable bytes can be served now. Only a
    /// confirmed hand-over has an authorized content location; Pending,
    /// Failed and Unknown all remain visible as receipt metadata but never
    /// become a download/gallery link.
    /// </summary>
    public static IReadOnlyList<IntakeAssetRecord> Servable(
        IEnumerable<IntakeAssetRecord> assets) =>
        [.. Select(assets).Where(asset => asset.CustodyState == IncomingArtifactCustodyState.Confirmed)];

    /// <summary>
    /// Whether an image is sized and shaped like a photograph: its shortest
    /// side at least <see cref="MinimumPhotographSidePixels"/> and its sides
    /// in a ratio under <see cref="MaximumPhotographSideRatio"/>. Fails open:
    /// the reader measures every image that decodes, and one whose size is
    /// unknown is judged on the other rules alone, because losing a genuine
    /// photograph is the worse error of the two.
    /// </summary>
    private static bool IsPhotographShaped(int? widthPixels, int? heightPixels)
    {
        if (widthPixels is not { } width
            || heightPixels is not { } height
            || width <= 0
            || height <= 0)
        {
            return true;
        }

        var longest = Math.Max(width, height);
        var shortest = Math.Min(width, height);
        return shortest >= MinimumPhotographSidePixels
            && (double)longest / shortest < MaximumPhotographSideRatio;
    }

    /// <summary>
    /// The case-file image policy accepts only inert raster formats. In
    /// particular, an SVG is not evidence-gallery content: it may contain
    /// active markup and document graphics rather than a vehicle photograph.
    /// </summary>
    public static bool IsImage(string? mediaType) =>
        string.Equals(mediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "image/png", StringComparison.OrdinalIgnoreCase);
}

public sealed record DownloadIntakeAssetQuery(
    Guid ReceiptId,
    Guid AssetId,
    ActionActor Actor);

public interface IDownloadIntakeAsset
{
    Task<IntakeSourceDownload?> ExecuteAsync(
        DownloadIntakeAssetQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads one retained asset of a receipt, hash-verified against its
/// recorded content the same way the source download is. The receipt id
/// scopes the lookup so an asset can never be fetched under another
/// receipt's identity.
///
/// When the logical-document reader is composed, the bytes are served through
/// it by asset identity, so no storage key crosses this boundary and the reader
/// resolves the custody or cache address itself. Until that adapter exists the
/// hash-verified artifact path below is the whole of the behaviour, and the
/// integrity check is identical either way — the difference is where the bytes
/// come from, never whether they are verified.
/// </summary>
public sealed class DownloadIntakeAsset(
    IIntakeReceiptQueries receiptQueries,
    IIntakeArtifactStore artifactStore,
    IReadLogicalDocumentVersion? logicalDocumentReader = null) : IDownloadIntakeAsset
{
    public async Task<IntakeSourceDownload?> ExecuteAsync(
        DownloadIntakeAssetQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ReceiptId == Guid.Empty || query.AssetId == Guid.Empty)
        {
            return null;
        }

        // Staff casework, or the Automation Actor, which ADR-0011 grants
        // exactly the ordinary operational casework surface. A Principal or
        // system-worker actor fails closed here rather than at a surface that
        // might forget to ask.
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        // One asset of the receipt, not the receipt's whole aggregate.
        var asset = await receiptQueries.GetAssetAsync(query.ReceiptId, query.AssetId, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        if (logicalDocumentReader is not null)
        {
            await using var logical = await logicalDocumentReader.OpenAsync(
                new(
                    query.Actor,
                    DocumentId: null,
                    VersionId: null,
                    IntakeAssetId: asset.Id,
                    CaseId: null,
                    IntakeReceiptId: query.ReceiptId,
                    asset.ContentHash,
                    asset.ContentLength),
                cancellationToken);
            // Sized from the recorded length, so the copy fills one array that
            // is then the answer, not a second copy of it. A read that yields
            // more or fewer bytes than recorded fails the length check below.
            using var buffer = new MemoryStream(
                (int)Math.Clamp(asset.ContentLength, 0, Array.MaxLength));
            await logical.Content.CopyToAsync(buffer, cancellationToken);
            return Verified(
                new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length),
                asset);
        }

        var content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken)
            ?? throw new IntakeArtifactIntegrityException();
        return Verified(content, asset);
    }

    private static IntakeSourceDownload Verified(
        ReadOnlyMemory<byte> content,
        IntakeAssetRecord asset)
    {
        var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
        if (content.Length != asset.ContentLength
            || !DownloadIntakeSource.FixedTimeHashEquals(actualHash, asset.ContentHash))
        {
            throw new IntakeArtifactIntegrityException();
        }

        return new(
            content,
            asset.FileName,
            asset.MediaType,
            content.Length,
            actualHash);
    }
}
