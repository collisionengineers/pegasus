using CollisionSpike.Core.Intake.Qdos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CollisionSpike.Web.Pages.Intake;

public sealed class ReviewModel(
    IQdosIntakeQueries queries,
    IIntakeArtifactStore artifactStore) : PageModel
{
    public QdosIntakeRecord Receipt { get; private set; } = null!;

    public bool IsDuplicate { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        bool duplicate = false,
        CancellationToken cancellationToken = default)
    {
        var receipt = await queries.GetAsync(id, cancellationToken);
        if (receipt is null)
        {
            return NotFound();
        }

        Receipt = receipt;
        IsDuplicate = duplicate;
        return Page();
    }

    public async Task<IActionResult> OnGetAssetAsync(
        Guid id,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var asset = await queries.GetAssetAsync(id, assetId, cancellationToken);
        if (asset is null)
        {
            return NotFound();
        }

        ReadOnlyMemory<byte>? content;
        try
        {
            content = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken);
        }
        catch (IntakeArtifactIntegrityException)
        {
            return new ContentResult
            {
                StatusCode = StatusCodes.Status409Conflict,
                ContentType = "text/plain",
                Content = "The retained asset failed integrity validation and cannot be served."
            };
        }
        if (content is null)
        {
            return NotFound();
        }

        if (IsReviewableImage(asset.MediaType))
        {
            return File(content.Value.ToArray(), asset.MediaType);
        }

        return File(content.Value.ToArray(), "application/octet-stream", asset.FileName);
    }

    public static bool IsReviewableImage(string mediaType) =>
        mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase);

    public int DuplicateOccurrenceCount(IntakeAssetRecord asset) =>
        Receipt.AssetRecords.Count(candidate => candidate.ContentHash == asset.ContentHash);

    public static string DecisionLabel(QdosIntakeDecision decision) => decision switch
    {
        QdosIntakeDecision.ConfirmedQdos => "Confirmed QDOS",
        QdosIntakeDecision.NeedsSorting => "Needs sorting",
        QdosIntakeDecision.OcrRequired => "Document text required",
        QdosIntakeDecision.TechnicalFailure => "Technical failure",
        _ => "Unsupported"
    };

    public static string SourceChannelLabel(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "Manual upload",
        _ => channel.ToString()
    };
}
