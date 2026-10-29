using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Documents;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Custody;
using QuestPDF.Fluent;
using SkiaSharp;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// The one <see cref="IAssessmentReportRenderer"/> adapter (ADR-0050): lays
/// the accepted snapshot out with QuestPDF on embedded fonts, returns the
/// bytes with their SHA-256, page count, template and engine versions, and
/// fails closed on anything the snapshot does not supply in a printable form.
/// </summary>
internal sealed class QuestPdfAssessmentReportRenderer(ReportRenderGate gate, IMemoryCache cache) : IAssessmentReportRenderer
{
    /// <summary>How many prepared signatures are kept: the most recently prepared.</summary>
    internal const int SignatureCapacity = 16;

    private readonly ReportRenderGate gate = gate ?? throw new ArgumentNullException(nameof(gate));

    private readonly IMemoryCache cache = cache ?? throw new ArgumentNullException(nameof(cache));

    // The process has one IMemoryCache and it has no size limit, so the
    // renderer bounds its own entries: the keys it kept, oldest first.
    private readonly Lock signatureGate = new();

    private readonly Queue<SignatureKey> keptSignatures = new();

    // A private key type, so no other IMemoryCache user can collide with an entry.
    private readonly record struct SignatureKey(string Sha256);

    public string EngineVersion { get; } =
        $"QuestPDF/{typeof(QuestPDF.Settings).Assembly.GetName().Version}";

    public async Task<RenderedReportArtifact> RenderAsync(
        AssessmentReportSnapshot snapshot,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ReportResources.RegisterFonts();
        var suffix = kind switch
        {
            CaseReportArtifactKind.FeeNote => "fee_note",
            CaseReportArtifactKind.ImagePack => "images",
            CaseReportArtifactKind.AssessmentReport => "report",
            _ => throw new ReportRenderRejectedException($"Unsupported report artifact kind '{kind}'."),
        };
        var fileName = $"{ReportChrome.Slug(snapshot.OurReference)}_{suffix}.pdf";
        var pageCount = new PageCountProbe();
        var pdf = await gate.RunAsync(token => RenderPdfAsync(snapshot, kind, pageCount, token), cancellationToken)
            .ConfigureAwait(false);
        return Artifact(fileName, pdf, pageCount.Count);
    }

    /// <summary>
    /// Prepares the images, one source image in memory at a time, then lays
    /// the document out. The token is the gate's: cancelled when the caller
    /// stops waiting, and checked between images so an abandoned render stops
    /// instead of running on.
    /// </summary>
    private async Task<byte[]> RenderPdfAsync(
        AssessmentReportSnapshot snapshot,
        CaseReportArtifactKind kind,
        PageCountProbe pageCount,
        CancellationToken cancellationToken)
    {
        PreparedReportImages images;
        using (DocumentReadTelemetry.Start("report.photos.prepare"))
        {
            images = await PrepareAsync(snapshot, kind, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var document = AssessmentReportLayout.Compose(snapshot, kind, images, pageCount);
        using var generating = DocumentReadTelemetry.Start("report.pdf.generate");
        return document.GeneratePdf();
    }

    /// <summary>
    /// The images one artifact prints. The report leads page 1 with the
    /// Overview and prints it nowhere else (operator, 7 October 2026); the
    /// image pack prints every image in the grid. Full page has no effect on
    /// the Overview. Each image is opened, prepared and let go of before the
    /// next is opened. A report generated without vehicle images opens none:
    /// it has no lead and no image pages, and its image pack still prints
    /// every image.
    /// </summary>
    private async Task<PreparedReportImages> PrepareAsync(
        AssessmentReportSnapshot snapshot, CaseReportArtifactKind kind, CancellationToken cancellationToken)
    {
        if (kind == CaseReportArtifactKind.FeeNote)
        {
            return new(null, [], [], ReportResources.Logo());
        }
        if (kind == CaseReportArtifactKind.AssessmentReport && !snapshot.IncludeVehicleImages)
        {
            return new(null, [], SignatureFor(snapshot.Signatory), ReportResources.Logo());
        }
        var ordered = snapshot.OrderedPhotos;
        var overview = ordered.FirstOrDefault(photo => photo.Role == CaseAssetReportRole.Overview);
        var lead = kind == CaseReportArtifactKind.AssessmentReport ? overview : null;
        var leadImage = lead is null
            ? null
            : await PreparePhotoAsync(lead, ReportChrome.LeadSlotHeight, cancellationToken).ConfigureAwait(false);
        var photos = new List<PreparedReportPhoto>(ordered.Count);
        foreach (var photo in ordered)
        {
            if (ReferenceEquals(photo, lead))
            {
                continue;
            }
            var fullPage = photo.FullPage && !ReferenceEquals(photo, overview);
            photos.Add(new PreparedReportPhoto(
                await PreparePhotoAsync(photo, null, cancellationToken).ConfigureAwait(false),
                fullPage));
        }
        return new(leadImage, photos, SignatureFor(snapshot.Signatory), ReportResources.Logo());
    }

    private static async Task<byte[]> PreparePhotoAsync(
        ReportImageEvidence photo, float? slotHeight, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var content = await photo.OpenAsync(cancellationToken).ConfigureAwait(false);
        return ReportPhotoPreparation.Prepare(
            content, photo.CustodyReference, photo.Rotation, photo.AppliedCrop, slotHeight, cancellationToken);
    }

    private RenderedReportArtifact Artifact(string fileName, byte[] pdf, int pageCount) =>
        new(
            fileName,
            pdf,
            pageCount,
            Convert.ToHexStringLower(SHA256.HashData(pdf)),
            AssessmentReportContract.TemplateVersion,
            EngineVersion);

    private byte[] SignatureFor(ReportSignatory signatory) =>
        GetOrPrepareSignature(signatory.SignatureContent, PrepareSignature);

    /// <summary>
    /// The prepared signature PNG, kept by the SHA-256 of the stored signature
    /// it was prepared from, so a signatory's signature is decoded and
    /// re-encoded once rather than for every artifact rendered. A changed
    /// signature has a new hash and so a new entry; the oldest entry goes when
    /// <see cref="SignatureCapacity"/> are kept. A preparation that fails is
    /// never kept: the next render prepares it again and fails the same way.
    /// The preparation runs outside the lock; two renders that miss together
    /// both prepare it and the first result stays.
    /// </summary>
    internal byte[] GetOrPrepareSignature(byte[] stored, Func<byte[], byte[]> prepare)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(prepare);
        var key = new SignatureKey(Convert.ToHexStringLower(SHA256.HashData(stored)));
        if (cache.TryGetValue(key, out byte[]? kept) && kept is not null)
        {
            return kept;
        }

        var fresh = prepare(stored);
        lock (signatureGate)
        {
            if (cache.TryGetValue(key, out byte[]? raced) && raced is not null)
            {
                return raced;
            }

            if (keptSignatures.Count >= SignatureCapacity)
            {
                cache.Remove(keptSignatures.Dequeue());
            }

            cache.Set(key, fresh);
            keptSignatures.Enqueue(key);
            return fresh;
        }
    }

    /// <summary>
    /// The signature as the page prints it, prepared once per stored signature
    /// (<see cref="GetOrPrepareSignature"/>): decoded once, EXIF orientation
    /// applied, re-encoded losslessly so any accepted source format prints
    /// the same way and undecodable bytes fail closed.
    /// </summary>
    private static byte[] PrepareSignature(byte[] stored)
    {
        using var data = SKData.CreateCopy(stored);
        using var codec = SKCodec.Create(data)
            ?? throw new ReportRenderRejectedException("The report signature image could not be decoded.");
        using var decoded = SKBitmap.Decode(codec)
            ?? throw new ReportRenderRejectedException("The report signature image could not be decoded.");
        var origin = codec.EncodedOrigin;
        var transposed = EncodedImageOrientation.IsTransposed(origin);
        var width = transposed ? decoded.Height : decoded.Width;
        var height = transposed ? decoded.Width : decoded.Height;
        using var shown = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(shown))
        {
            canvas.Clear(SKColors.Transparent);
            EncodedImageOrientation.Apply(canvas, origin, width, height);
            canvas.DrawBitmap(decoded, 0, 0);
        }
        using var image = SKImage.FromBitmap(shown);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new ReportRenderRejectedException("The report signature image could not be prepared for print.");
        return encoded.ToArray();
    }

}
