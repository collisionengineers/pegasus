using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Intake;

/// <summary>
/// The two staff acts a receipt offers besides Create case, run the same way
/// from the Unidentified record and the Inbox message page: the offer is
/// re-checked, the Core command runs, a reading the registration used is
/// confirmed, and the Unidentified item is settled against the destination the
/// material now has. The last two are advisory bookkeeping over a committed
/// act: a recoverable failure there is logged and the act stands, with the
/// periodic sweep as the backstop.
/// </summary>
public sealed partial class StaffIntakeActions(
    IGetIntakeOfferedActions offers,
    IImageIntakeOriginResolver originResolver,
    IRegisterImageIntake registerImageIntake,
    ICreateTriageFromIntake createTriage,
    IVrmSuggestionStore vrmSuggestions,
    IIntakeReceiptQueries receipts,
    ReconcileUnidentifiedDestinations reconcile,
    ILogger<StaffIntakeActions> logger)
{
    /// <summary>Register images: the receipt's material becomes an Image-initiated Case under the registration given.</summary>
    public async Task<ImageIntakeRecord> RegisterImagesAsync(
        ActionActor actor,
        IntakeReceipt receipt,
        string? vehicleRegistration,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var offered = await offers.ExecuteAsync(actor, receipt, cancellationToken);
        if (!offered.CanRegisterImages)
        {
            throw new InvalidOperationException("Images cannot be registered from this item.");
        }

        var normalized = ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration);
        var origin = await originResolver.ResolveOriginAsync(receipt.Id, cancellationToken)
            ?? throw new InvalidOperationException("The material has no completed evaluation to register from.");
        var record = await registerImageIntake.ExecuteAsync(
            new(origin, normalized, actor, operationKey, reason),
            cancellationToken);
        await ConfirmMatchingReadingsAsync(offered.RegistrationReadings, record, actor, cancellationToken);
        await SynchronizeAsync(receipt.Id, cancellationToken);
        return record;
    }

    /// <summary>
    /// Register images for an Unidentified upload group: the whole group
    /// becomes one Image-initiated Case under the registration given, originating
    /// from the member the item's context chose. The store moves every image-only
    /// member to the registered decision in the same transaction.
    /// </summary>
    public async Task<ImageIntakeRecord> RegisterGroupImagesAsync(
        ActionActor actor,
        UnidentifiedItemContext context,
        string? vehicleRegistration,
        string reason,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!context.CanRegisterImages
            || context.SubmissionGroup is not { } group
            || context.GroupRegistrationReceipt is not { } primary)
        {
            throw new InvalidOperationException("Images cannot be registered from this item.");
        }

        var normalized = ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration);
        var origin = await originResolver.ResolveOriginAsync(primary.Id, cancellationToken)
            ?? throw new InvalidOperationException("The material has no completed evaluation to register from.");
        var record = await registerImageIntake.ExecuteAsync(
            new(origin, normalized, actor, operationKey, reason, SubmissionGroupId: group.Id),
            cancellationToken);
        await ConfirmMatchingReadingsAsync(context.RegistrationReadings, record, actor, cancellationToken);
        try
        {
            await reconcile.SynchronizeForSubmissionGroupAsync(group.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not UnidentifiedOperationConflictException && IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogCommandFailed(logger, "synchronize", group.Id, exception);
        }

        return record;
    }

    /// <summary>
    /// Open the Triage: a Triage request held for want of a registration is
    /// promoted by someone supplying one; the receipt's accepted Triage-match
    /// record is passed back as recorded.
    /// </summary>
    public async Task OpenTriageAsync(
        ActionActor actor,
        IntakeReceipt receipt,
        string? vehicleRegistration,
        string operationKey,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        var offered = await offers.ExecuteAsync(actor, receipt, cancellationToken);
        if (!offered.CanOpenTriage)
        {
            throw new InvalidOperationException("A Triage cannot be opened from this item.");
        }

        var acceptedMatch = receipt.Evidence.Single(
            evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch);
        var origin = await originResolver.ResolveOriginAsync(receipt.Id, cancellationToken)
            ?? throw new InvalidOperationException("The material has no completed evaluation to open a Triage from.");
        await createTriage.ExecuteAsync(
            new(
                new TriageOrigin(origin.ReceiptId, origin.SourceIdentity, origin.SourceHash, origin.EvaluationRevisionId),
                ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration),
                acceptedMatch,
                actor,
                $"triage-from-staff:{operationKey}"),
            cancellationToken);
        await SynchronizeAsync(receipt.Id, cancellationToken);
    }

    // A reading the staff registration used is confirmed; bookkeeping over a committed registration.
    private async Task ConfirmMatchingReadingsAsync(
        IReadOnlyList<ImageVrmSuggestion> readings,
        ImageIntakeRecord record,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        foreach (var reading in readings.Where(reading =>
                     reading.Disposition == ImageVrmSuggestionDisposition.Pending
                     && reading.Outcome == VrmRecognitionOutcomeKind.Suggested
                     && string.Equals(reading.SuggestedRegistration, record.NormalizedVehicleRegistration, StringComparison.Ordinal)))
        {
            try
            {
                await vrmSuggestions.SetDispositionAsync(
                    new(reading.Id, ImageVrmSuggestionDisposition.Confirmed, actor,
                        "The staff registration used this suggested registration.", $"vrm-confirm:{reading.Id:N}"),
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                LogCommandFailed(logger, "confirm_reading", reading.Id, exception);
            }
        }
    }

    // Settles the Unidentified item against the destination the material now
    // has, through the one owner of that rule, reading the receipt afresh so the
    // new registration or Triage is in hand. Advisory: the periodic sweep is the
    // backstop, except for a permanently taken operation key, which surfaces.
    private async Task SynchronizeAsync(Guid receiptId, CancellationToken cancellationToken)
    {
        try
        {
            if (await receipts.GetAsync(receiptId, cancellationToken) is { } receipt)
            {
                await reconcile.SynchronizeForReceiptAsync(receipt, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not UnidentifiedOperationConflictException && IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogCommandFailed(logger, "synchronize", receiptId, exception);
        }
    }

    [LoggerMessage(EventId = 7301, Level = LogLevel.Warning, Message = "Staff intake action {Command} failed for {SubjectId}.")]
    private static partial void LogCommandFailed(ILogger logger, string command, Guid subjectId, Exception exception);
}
