using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Intake;

/// <summary>
/// Adapts direct image attachments from an otherwise-Unidentified mailbox
/// receipt into the existing grouped intake lifecycle. It deliberately owns
/// no recognition, matching, case creation, or custody policy.
/// </summary>
public sealed class SubmitMailboxImageIntake(
    IIntakeArtifactStore artifactStore,
    IGroupedIntakeSubmission groupedSubmission,
    IIntakeSubmissionGroupStore groupStore,
    IRegisterUnidentified registerUnidentified)
{
    private const string SystemActor = "system-worker:mailbox-image-intake";

    public static IReadOnlyList<IntakeAssetRecord> SelectAttachments(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.SourceIdentity.Channel != IntakeSourceChannel.Mailbox
            || receipt.Decision != IntakeDecision.NeedsSorting
            || receipt.CurrentCaseId is not null
            || receipt.CaseMatchDecision?.Outcome == CaseMatchOutcome.UniqueMatch
            || receipt.InstructionDraft is not null
            || ProcessIntake.IsTriageRequest(receipt)
            || receipt.MailClassificationDecision is { } classification
                && MailOperationalDestinationPolicy.Map(classification).Destination
                    != MailOperationalDestination.Unidentified)
        {
            return [];
        }

        return receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.Attachment
                && asset.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public static bool IsCandidate(IntakeReceipt receipt) =>
        SelectAttachments(receipt).Count > 0;

    public async Task<bool> HasSubmissionAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken = default) =>
        IsCandidate(receipt)
        && await groupStore.FindAsync(
            IntakeSourceChannel.Mailbox,
            SubmissionToken(receipt.Id),
            cancellationToken) is not null;

    public async Task<bool> ExecuteAsync(
        IntakeReceipt receipt,
        bool isFinalAttempt,
        CancellationToken cancellationToken = default)
    {
        var attachments = SelectAttachments(receipt);
        if (attachments.Count == 0)
        {
            return false;
        }

        try
        {
            var files = new List<GroupedIntakeFile>(attachments.Count);
            var submissionToken = SubmissionToken(receipt.Id);
            for (var ordinal = 0; ordinal < attachments.Count; ordinal++)
            {
                var attachment = attachments[ordinal];
                var content = await artifactStore.ReadAsync(attachment.StorageKey, cancellationToken)
                    ?? throw new IntakeArtifactIntegrityException();
                var actualHash = Convert.ToHexString(SHA256.HashData(content.Span));
                if (content.Length != attachment.ContentLength
                    || !DownloadIntakeSource.FixedTimeHashEquals(actualHash, attachment.ContentHash))
                {
                    throw new IntakeArtifactIntegrityException();
                }

                files.Add(new(
                    ordinal,
                    new(
                        attachment.FileName,
                        attachment.MediaType,
                        content,
                        receipt.ReceivedAtUtc,
                        SystemActor,
                        new(
                            IntakeSourceChannel.Mailbox,
                            GroupedIntakeMemberToken.Create(submissionToken, ordinal)))));
            }

            await groupedSubmission.ExecuteAsync(
                new(
                    submissionToken,
                    SystemActor,
                    receipt.ReceivedAtUtc,
                    files,
                    IntakeSourceChannel.Mailbox,
                    receipt.Id),
                cancellationToken);
            return true;
        }
        catch (Exception exception) when (
            IntakeExceptionPolicy.IsRecoverable(exception)
            && (isFinalAttempt || !IntakeExceptionPolicy.IsTransientFailure(exception)))
        {
            var group = await groupStore.FindAsync(
                IntakeSourceChannel.Mailbox,
                SubmissionToken(receipt.Id),
                cancellationToken);
            await registerUnidentified.ExecuteAsync(
                BuildFailureRegistrationRequest(receipt, group),
                cancellationToken);
            return true;
        }
    }

    internal static RegisterUnidentifiedRequest BuildFailureRegistrationRequest(
        IntakeReceipt receipt,
        IntakeSubmissionGroup? group = null) =>
        new(
            group is null
                ? UnidentifiedOrigin.Receipt(receipt.Id)
                : UnidentifiedOrigin.SubmissionGroup(group.Id),
            UnidentifiedReasonCode.TechnicalProcessingFailure,
            "The image attachments could not be submitted for processing.",
            ActionActor.SystemWorker("mailbox-image-intake"),
            group is null
                ? $"mailbox-image-submission-failure:{receipt.Id:N}"
                : $"mailbox-image-submission-failure:group:{group.Id:N}",
            group?.ReceivedAtUtc ?? receipt.ReceivedAtUtc);

    internal static RegisterUnidentifiedRequest BuildFailureRegistrationRequest(
        IntakeSubmissionGroup group) =>
        new(
            UnidentifiedOrigin.SubmissionGroup(group.Id),
            UnidentifiedReasonCode.TechnicalProcessingFailure,
            "The image attachments could not be submitted for processing.",
            ActionActor.SystemWorker("mailbox-image-intake"),
            $"mailbox-image-submission-failure:group:{group.Id:N}",
            group.ReceivedAtUtc);

    private static string SubmissionToken(Guid receiptId) =>
        $"mailbox-images:{receiptId:N}";
}
