using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.PreCaseImages;

/// <summary>
/// Crop (Apply / Clear) and tag on a pre-Case image, posted from the evidence
/// viewer on the image record, Triage and Unidentified pages. Core owns the
/// rule; this page returns to the record the post came from.
/// </summary>
[Authorize(Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class IndexModel(
    ISavePreCaseImageCrop saveCrop,
    ITagPreCaseImage tagImage) : StaffPageModel
{
    /// <summary>Shown above the gallery the post returns to.</summary>
    public const string ErrorKey = "PreCaseImageError";

    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostCropAsync(
        Guid intakeAssetId,
        long expectedVersion,
        int rotation,
        decimal? cropLeft,
        decimal? cropTop,
        decimal? cropWidth,
        decimal? cropHeight,
        bool clear,
        string operationKey,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var crop = clear || cropLeft is null || cropTop is null || cropWidth is null || cropHeight is null
                ? CaseAssetCrop.Full
                : new CaseAssetCrop(
                    decimal.Round(cropLeft.Value, 7),
                    decimal.Round(cropTop.Value, 7),
                    decimal.Round(cropWidth.Value, 7),
                    decimal.Round(cropHeight.Value, 7));
            await saveCrop.ExecuteAsync(
                new SavePreCaseImageCropRequest(
                    intakeAssetId,
                    expectedVersion,
                    clear ? CaseAssetRotation.None : (CaseAssetRotation)rotation,
                    crop,
                    actor,
                    operationKey),
                cancellationToken);
            TempData["Confirmation"] = clear ? OperatorLabels.PreCaseImages.CropCleared : OperatorLabels.PreCaseImages.CropSaved;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData[ErrorKey] = exception is InvalidOperationException { Message.Length: > 0 } invalid
                && exception.GetType() == typeof(InvalidOperationException)
                    ? invalid.Message
                    : "The crop was not saved. Reload the record and try again.";
        }

        return Return(returnUrl);
    }

    public async Task<IActionResult> OnPostTagAsync(
        Guid intakeAssetId,
        Guid tagId,
        bool applied,
        string operationKey,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await tagImage.ExecuteAsync(
                new TagPreCaseImageRequest(intakeAssetId, tagId, applied, actor, operationKey),
                cancellationToken);
            TempData["Confirmation"] = applied ? OperatorLabels.PreCaseImages.TagAdded : OperatorLabels.PreCaseImages.TagRemoved;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData[ErrorKey] = exception is InvalidOperationException { Message.Length: > 0 } invalid
                && exception.GetType() == typeof(InvalidOperationException)
                    ? invalid.Message
                    : "The tag was not changed. Reload the record and try again.";
        }

        return Return(returnUrl);
    }

    private IActionResult Return(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : Redirect("/");
}
