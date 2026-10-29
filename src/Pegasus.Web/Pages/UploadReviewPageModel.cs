using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// What the two upload status surfaces add to the shared confirmation
/// decision: Register images for images the upload left in an Unidentified
/// item (operator, 28 October 2026, R463), the same act the Unidentified
/// record runs, offered by the same rule.
/// </summary>
public abstract partial class UploadReviewPageModel(
    IUploadCaseDecision caseDecision,
    GetUnidentifiedItemContext unidentifiedContexts) : UploadConfirmationPageModel(caseDecision)
{
    /// <summary>
    /// The one Unidentified item this surface's upload left its files in, read
    /// from the surface itself; null when there is none or more than one. A
    /// posted item id is never authority to register another item's images.
    /// </summary>
    protected abstract Task<Guid?> FindSurfaceUnidentifiedItemAsync(
        Guid surfaceId,
        ActionActor actor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Register images, offered when the upload's one Unidentified item can
    /// register, by the rule its record uses; its one agreeing reading
    /// prefills the registration.
    /// </summary>
    protected async Task<UploadReviewRegistration?> RegistrationOfferAsync(
        Guid? unidentifiedItemId,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (unidentifiedItemId is not { } itemId)
        {
            return null;
        }

        var context = await unidentifiedContexts.ExecuteAsync(actor, itemId, cancellationToken);
        return context is { CanRegisterImages: true }
            ? new UploadReviewRegistration(context.SuggestedRegistration)
            : null;
    }

    /// <summary>
    /// Register images: the upload's Unidentified images become an
    /// Image-initiated Case under the registration given. The surface then
    /// reports the registration as it reports an automatic one.
    /// </summary>
    public async Task<IActionResult> OnPostRegisterImagesAsync(
        Guid id,
        string? vehicleRegistration,
        string? reason,
        string? operationKey,
        [FromServices] StaffIntakeActions intakeActions,
        [FromServices] ILogger<UploadReviewPageModel> logger,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var itemId = await FindSurfaceUnidentifiedItemAsync(id, actor, cancellationToken);
            var context = itemId is { } value
                ? await unidentifiedContexts.ExecuteAsync(actor, value, cancellationToken)
                : null;
            if (context is not { CanRegisterImages: true })
            {
                TempData["UploadConfirmationError"] = OperatorLabels.ActionNotApplied;
                return RedirectToSurface(id);
            }

            var record = await intakeActions.RegisterItemImagesAsync(
                actor,
                context,
                vehicleRegistration,
                reason ?? string.Empty,
                operationKey ?? string.Empty,
                cancellationToken);
            TempData["Confirmation"] = OperatorLabels.RegisterImages.Registered(record.ImageIntakeReference);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRegisterImagesFailed(logger, id, exception);
            TempData["UploadConfirmationError"] = exception is ArgumentException argument
                ? argument.Message
                : OperatorLabels.ActionNotApplied;
        }

        return RedirectToSurface(id);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Register images failed on the upload review {SurfaceId}.")]
    private static partial void LogRegisterImagesFailed(ILogger logger, Guid surfaceId, Exception exception);
}
