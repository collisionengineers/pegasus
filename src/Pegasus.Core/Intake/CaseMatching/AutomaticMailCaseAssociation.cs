using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Notifications;

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
/// Case and PO references are deliberately absent from the contract. The
/// receipt's recorded case-match decision outranks both: a Case it eliminated
/// is never the target, and an Ambiguous decision links nothing (FRD-09
/// shared eliminator; FRD-22; R35, operator, 15 October 2026).
/// </summary>
public sealed class AssociateRetainedMailWithCase(
    IAutomaticMailCaseAssociationEvidenceQueries evidenceQueries,
    IAutomaticCaseAssociationStore associationStore,
    TimeProvider timeProvider,
    ICaseStaffNotifier? caseNotifier = null)
{
    public const string PolicyKey = "mail_case_association";
    public const int PolicyVersion = 1;
    private const string SystemActor = "system-worker:intake-processing";

    public async Task<AutomaticCaseAssociationOutcome?> ExecuteAsync(
        Guid intakeReceiptId,
        CaseMatchEvaluationResult? caseMatchDecision = null,
        CancellationToken cancellationToken = default)
    {
        if (intakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt identifier is required.", nameof(intakeReceiptId));
        }

        // Decided before the evidence read, so an Ambiguous message pays no
        // registration or thread query.
        if (caseMatchDecision?.Outcome == CaseMatchOutcome.Ambiguous)
        {
            return null;
        }

        var evidence = await evidenceQueries.GetAsync(intakeReceiptId, cancellationToken);
        if (evidence is null || SelectTarget(evidence, caseMatchDecision) is not { } caseId)
        {
            return null;
        }

        var outcome = await associationStore.AssociateFromMatchAsync(
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
        if (outcome == AutomaticCaseAssociationOutcome.Associated && caseNotifier is not null)
        {
            // Work Centre D10 cause 3: the Case's engineer learns an e-mail arrived.
            await caseNotifier.NotifyMailArrivalAsync(caseId, intakeReceiptId, null, cancellationToken);
        }

        return outcome;
    }

    internal static Guid? SelectTarget(
        AutomaticMailCaseAssociationEvidence evidence,
        CaseMatchEvaluationResult? caseMatchDecision)
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

        // Refused after the uniqueness rule, never before it: removing an
        // eliminated Case first would promote another Case holding the same
        // registration. A Created in error candidate the decision redirected
        // is listed only as its replacement's RedirectedFromCaseId.
        var target = registrationCaseId ?? threadCaseId;
        if (target is { } caseId
            && caseMatchDecision is not null
            && caseMatchDecision.Candidates.Any(candidate =>
                candidate.Eliminations.Count > 0
                && (candidate.CaseId == caseId || candidate.RedirectedFromCaseId == caseId)))
        {
            return null;
        }

        return target;
    }
}
