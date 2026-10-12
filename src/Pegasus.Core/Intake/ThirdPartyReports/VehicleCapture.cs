using System.Text.RegularExpressions;
using Pegasus.Core.Documents;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// The photographs a capture PDF carries (FRD-02 EXT-17): every JPEG printed
/// on its "Submitted Vehicle Images" page, whatever its size, and nothing
/// else in the document — not the letterhead logo and not the small icon
/// every page repeats.
/// </summary>
internal static class VehicleCaptureImages
{
    /// <summary>
    /// Shortest side, in pixels, below which an image on the photograph page
    /// is page furniture. The page icon measures 70x65; the submitted
    /// photographs measure 800x600 and above.
    /// </summary>
    internal const int MinimumPhotographSidePixels = 200;

    private static readonly Regex ImagesHeading =
        ThirdPartyRegex.Create(ThirdPartyReportProfiles.CaptureImagesHeading);

    /// <summary>
    /// The capture documents in a reading, by document label, and the
    /// photographs designated in them. An embedded image in a capture
    /// document is retained only when it is designated.
    /// </summary>
    internal static VehicleCaptureDesignation Designate(IntakeSourceReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        var documents = new List<string>();
        var photographs = new List<IntakeAssetCandidate>();
        foreach (var label in readResult.Content
                     .Select(fragment => InstructionExtractionPolicySelector.DocumentIdentity(fragment.SourceLabel))
                     .Distinct(StringComparer.Ordinal))
        {
            var document = ThirdPartyReportDocuments.ForDocument(readResult, label);
            var pages = ThirdPartySourcePage.Read(document);
            if (!ThirdPartyReportProfiles.IsVehicleCapture(pages))
            {
                continue;
            }

            documents.Add(label);
            var photographPages = pages
                .Where(page => page.Page is not null && ImagesHeading.IsMatch(page.Flat))
                .Select(page => page.Page!.Value)
                .ToHashSet();
            photographs.AddRange(document.AssetCandidates.Where(asset =>
                asset.Kind == IntakeAssetKind.EmbeddedImage
                && asset.PageNumber is { } page
                && photographPages.Contains(page)
                && string.Equals(asset.MediaType, "image/jpeg", StringComparison.OrdinalIgnoreCase)
                && !IsPageFurniture(asset)));
        }

        return new(documents, photographs);
    }

    // Unknown dimensions keep the image, as the photograph shape test does.
    private static bool IsPageFurniture(IntakeAssetCandidate asset) =>
        asset.WidthPixels is { } width
        && asset.HeightPixels is { } height
        && Math.Min(width, height) < MinimumPhotographSidePixels;
}

internal sealed record VehicleCaptureDesignation(
    IReadOnlyList<string> Documents,
    IReadOnlyList<IntakeAssetCandidate> Photographs)
{
    /// <summary>Whether a reader candidate belongs to one of the capture documents.</summary>
    public bool Covers(IntakeAssetCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return Documents.Any(document =>
            candidate.SourceLabel.StartsWith(document + ",", StringComparison.Ordinal));
    }

    public bool IsPhotograph(IntakeAssetCandidate candidate) =>
        Photographs.Contains<IntakeAssetCandidate>(candidate, ReferenceEqualityComparer.Instance);
}

/// <summary>
/// The vehicle identity intake recorded from one capture PDF: what
/// <see cref="ThirdPartyReportExtraction"/> read, as normalized values.
/// </summary>
public sealed record VehicleCaptureReading(
    Guid ReceiptId,
    Guid IntakeAssetId,
    string SourceLabel,
    string Registration,
    string? Make,
    string? Model,
    string? Vin,
    long? Mileage,
    DateOnly? AccidentDate);

/// <summary>
/// Finds the capture reading intake recorded for a receipt
/// (<see cref="RecordThirdPartyReportReading"/>). The same document is not
/// read twice: image automation pairs on its registration and the Case fill
/// writes its values from this one recording.
/// </summary>
public sealed class RecordedVehicleCaptureReading(IRetainedInstructionAnalysisStore analyses)
{
    /// <summary>
    /// The reading, or null unless exactly one of the receipt's documents is
    /// a capture PDF with a usable printed registration.
    /// </summary>
    public async Task<VehicleCaptureReading?> FindAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var readings = new List<VehicleCaptureReading>();
        foreach (var asset in receipt.AssetRecords.Where(OriginalReportPolicy.IsRecognitionCandidate))
        {
            var analysis = await analyses.FindByOperationKeyAsync(
                RecordThirdPartyReportReading.OperationKey(asset), cancellationToken);
            if (analysis is null
                || analysis.IntakeAssetId != asset.Id
                || !string.Equals(analysis.SourceSha256, asset.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rows = analysis.Candidates
                .Where(row => row.DocumentRole == ThirdPartyReportProfiles.VehicleCaptureDocumentRole)
                .ToArray();
            if (Usable(rows, ThirdPartyReportFields.Registration) is not { } registration)
            {
                continue;
            }

            readings.Add(new(
                receipt.Id,
                asset.Id,
                asset.FileName,
                registration,
                Usable(rows, ThirdPartyReportFields.Make),
                Usable(rows, ThirdPartyReportFields.Model),
                Usable(rows, ThirdPartyReportFields.Vin),
                InstructionFieldEngine.ParseMileage(Usable(rows, ThirdPartyReportFields.Mileage)),
                InstructionFieldEngine.ParseDate(Usable(rows, ThirdPartyReportFields.AccidentDate))));
        }

        return readings.Count == 1 ? readings[0] : null;
    }

    private static string? Usable(IReadOnlyList<RetainedInstructionCandidate> rows, string field) =>
        rows.FirstOrDefault(row => row.Field == field && row.Disposition == SourceCandidateDisposition.Usable)
            ?.NormalizedValue;
}
