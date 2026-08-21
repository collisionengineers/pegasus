using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Pegasus.Core.Intake;

public sealed record AutomaticMailCaseAssociationEvidence(
    Guid IntakeReceiptId,
    long IntakeVersion,
    string? NormalizedVehicleRegistration,
    IReadOnlyList<Guid> RegistrationCaseIds,
    string MailboxId,
    string? ConversationIdentity,
    IReadOnlyList<Guid> ThreadCaseIds)
{
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            IntakeReceiptId,
            IntakeVersion,
            NormalizedVehicleRegistration,
            RegistrationCaseIds = RegistrationCaseIds.Distinct().Order().ToArray(),
            MailboxId,
            ConversationIdentity,
            ThreadCaseIds = ThreadCaseIds.Distinct().Order().ToArray()
        }))));
}

public interface IAutomaticMailCaseAssociationEvidenceQueries
{
    Task<AutomaticMailCaseAssociationEvidence?> GetAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Conservatively associates one retained inbound message using only a unique
/// system-wide registration or an already-associated exact mailbox thread.
/// Case and PO references are deliberately absent from the contract.
/// </summary>
public sealed class AssociateRetainedMailWithCase(
    IAutomaticMailCaseAssociationEvidenceQueries evidenceQueries,
    IAutomaticCaseAssociationStore associationStore,
    TimeProvider timeProvider)
{
    public const string PolicyKey = "mail_case_association";
    public const int PolicyVersion = 1;
    private const string SystemActor = "system-worker:intake-processing";

    public async Task<AutomaticCaseAssociationOutcome?> ExecuteAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken = default)
    {
        if (intakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(intakeReceiptId));
        }

        var evidence = await evidenceQueries.GetAsync(intakeReceiptId, cancellationToken);
        if (evidence is null || SelectTarget(evidence) is not { } caseId)
        {
            return null;
        }

        return await associationStore.AssociateFromMatchAsync(
            new(
                intakeReceiptId,
                caseId,
                PolicyKey,
                PolicyVersion,
                SystemActor,
                $"mail-case-association:{intakeReceiptId:N}",
                "Automatic association from unique current VRM or exact mailbox-thread evidence.",
                evidence.Fingerprint),
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    internal static Guid? SelectTarget(AutomaticMailCaseAssociationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Guid? registrationCaseId = null;
        if (evidence.NormalizedVehicleRegistration is not null)
        {
            var registrationCandidates = evidence.RegistrationCaseIds.Distinct().Take(2).ToArray();
            if (registrationCandidates.Length != 1)
            {
                return null;
            }

            registrationCaseId = registrationCandidates[0];
        }

        Guid? threadCaseId = null;
        if (evidence.ThreadCaseIds.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(evidence.ConversationIdentity))
            {
                return null;
            }

            var threadCandidates = evidence.ThreadCaseIds.Distinct().Take(2).ToArray();
            if (threadCandidates.Length != 1)
            {
                return null;
            }

            threadCaseId = threadCandidates[0];
        }

        if (registrationCaseId is not null
            && threadCaseId is not null
            && registrationCaseId != threadCaseId)
        {
            return null;
        }

        return registrationCaseId ?? threadCaseId;
    }
}
