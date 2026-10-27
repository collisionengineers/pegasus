using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Notifications;

namespace Pegasus.Core.Intake;

/// <summary>
/// One Case that carries the message's vehicle registration, with the Case's
/// own incident date where the case-match index has one. The date narrows a
/// registration that several Cases share (D15); it never selects a Case on its
/// own.
/// </summary>
public sealed record MailAssociationRegistrationCandidate(Guid CaseId, DateOnly? IncidentDate);

public sealed record AutomaticMailCaseAssociationEvidence(
    Guid IntakeReceiptId,
    long IntakeVersion,
    string MailboxId,
    string? ConversationIdentity,
    string? NormalizedVehicleRegistration,
    DateOnly? IncidentDate,
    IReadOnlyList<Guid> ThreadCaseIds,
    IReadOnlyList<Guid> ReplyCaseIds,
    IReadOnlyList<Guid> ClaimReferenceCaseIds,
    IReadOnlyList<MailAssociationRegistrationCandidate> RegistrationCandidates)
{
    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new
        {
            IntakeReceiptId,
            IntakeVersion,
            MailboxId,
            ConversationIdentity,
            NormalizedVehicleRegistration,
            IncidentDate,
            ThreadCaseIds = ThreadCaseIds.Distinct().Order().ToArray(),
            ReplyCaseIds = ReplyCaseIds.Distinct().Order().ToArray(),
            ClaimReferenceCaseIds = ClaimReferenceCaseIds.Distinct().Order().ToArray(),
            RegistrationCandidates = RegistrationCandidates
                .Distinct()
                .OrderBy(candidate => candidate.CaseId)
                .ThenBy(candidate => candidate.IncidentDate)
                .ToArray()
        }))));
}

public interface IAutomaticMailCaseAssociationEvidenceQueries
{
    Task<AutomaticMailCaseAssociationEvidence?> GetAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Associates one retained inbound message with the one Case every linking
/// signal agrees on: the exact mailbox conversation, the In-Reply-To and
/// References headers, a claim reference the subject or body names, and the
/// vehicle registration narrowed by incident date (D5, D13–D16; items F,
/// T–W, operator, 27 October 2026). Every signal that has any candidate
/// contributes its whole candidate set and the sets are intersected, so a
/// broader signal no longer vetoes a narrower one that agrees with it. The
/// receipt's recorded case-match decision still outranks them: a Case it
/// eliminated is never the target, and an Ambiguous decision links nothing
/// (FRD-09 shared eliminator; FRD-22; R35, operator, 15 October 2026).
/// </summary>
public sealed class AssociateRetainedMailWithCase(
    IAutomaticMailCaseAssociationEvidenceQueries evidenceQueries,
    IAutomaticCaseAssociationStore associationStore,
    TimeProvider timeProvider,
    ICaseStaffNotifier? caseNotifier = null)
{
    public const string PolicyKey = "mail_case_association";
    public const int PolicyVersion = 2;
    private const string SystemActor = "system-worker:intake-processing";

    /// <summary>The linking signals, in the order the reason names them.</summary>
    internal enum MailAssociationSignal
    {
        ConversationThread,
        ReplyHeaders,
        ClaimReference,
        VehicleRegistration
    }

    internal sealed record MailAssociationTarget(
        Guid CaseId,
        IReadOnlyList<MailAssociationSignal> Signals);

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
        // registration, thread, reply or claim query.
        if (caseMatchDecision?.Outcome == CaseMatchOutcome.Ambiguous)
        {
            return null;
        }

        var evidence = await evidenceQueries.GetAsync(intakeReceiptId, cancellationToken);
        if (evidence is null || SelectTarget(evidence, caseMatchDecision) is not { } target)
        {
            return null;
        }

        var outcome = await associationStore.AssociateFromMatchAsync(
            new(
                intakeReceiptId,
                target.CaseId,
                PolicyKey,
                PolicyVersion,
                SystemActor,
                $"mail-case-association:{intakeReceiptId:N}",
                $"Automatic association from {DescribeSignals(target.Signals)}.",
                evidence.Fingerprint),
            timeProvider.GetUtcNow(),
            cancellationToken);
        if (outcome == AutomaticCaseAssociationOutcome.Associated && caseNotifier is not null)
        {
            // Work Centre D10 cause 3: the Case's engineer learns an e-mail arrived.
            await caseNotifier.NotifyMailArrivalAsync(target.CaseId, intakeReceiptId, null, cancellationToken);
        }

        return outcome;
    }

    internal static MailAssociationTarget? SelectTarget(
        AutomaticMailCaseAssociationEvidence evidence,
        CaseMatchEvaluationResult? caseMatchDecision)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var signals = new List<(MailAssociationSignal Signal, IReadOnlySet<Guid> CaseIds)>();
        if (evidence.ThreadCaseIds.Count > 0)
        {
            // The thread is read by mailbox and conversation identity; candidates
            // without one cannot have come from a thread.
            if (string.IsNullOrWhiteSpace(evidence.ConversationIdentity))
            {
                return null;
            }

            signals.Add((MailAssociationSignal.ConversationThread, evidence.ThreadCaseIds.ToHashSet()));
        }

        if (evidence.ReplyCaseIds.Count > 0)
        {
            signals.Add((MailAssociationSignal.ReplyHeaders, evidence.ReplyCaseIds.ToHashSet()));
        }

        if (evidence.ClaimReferenceCaseIds.Count > 0)
        {
            signals.Add((MailAssociationSignal.ClaimReference, evidence.ClaimReferenceCaseIds.ToHashSet()));
        }

        var registration = RegistrationCandidates(evidence);
        if (registration.Count > 0)
        {
            signals.Add((MailAssociationSignal.VehicleRegistration, registration));
        }

        if (signals.Count == 0)
        {
            return null;
        }

        // Every non-empty signal set must contain the target (D16), so a set
        // that still holds several Cases after the intersection links nothing.
        var agreed = signals[0].CaseIds.ToHashSet();
        foreach (var signal in signals.Skip(1))
        {
            agreed.IntersectWith(signal.CaseIds);
        }

        if (agreed.Count != 1)
        {
            return null;
        }

        var target = agreed.Single();
        // Refused after the intersection, never before it: removing an
        // eliminated Case first would promote another Case the signals agree on.
        // A Created in error candidate the decision redirected is listed only as
        // its replacement's RedirectedFromCaseId.
        if (caseMatchDecision is not null
            && caseMatchDecision.Candidates.Any(candidate =>
                candidate.Eliminations.Count > 0
                && (candidate.CaseId == target || candidate.RedirectedFromCaseId == target)))
        {
            return null;
        }

        return new(target, signals.Select(signal => signal.Signal).ToArray());
    }

    /// <summary>
    /// The Cases carrying the message's registration. A registration several
    /// Cases share is narrowed by the message's incident date: candidates with
    /// a different date are dropped, and an equal date wins when one is left
    /// (D15). With no date to compare, or with dates still tied, the set stays
    /// as it is and the intersection decides.
    /// </summary>
    private static HashSet<Guid> RegistrationCandidates(
        AutomaticMailCaseAssociationEvidence evidence)
    {
        if (evidence.NormalizedVehicleRegistration is null)
        {
            return [];
        }

        var candidates = evidence.RegistrationCandidates
            .GroupBy(candidate => candidate.CaseId)
            .Select(group => group.First())
            .ToArray();
        if (candidates.Length > 1 && evidence.IncidentDate is { } incidentDate)
        {
            candidates = candidates
                .Where(candidate => candidate.IncidentDate == incidentDate)
                .ToArray();
        }

        return candidates.Select(candidate => candidate.CaseId).ToHashSet();
    }

    private static string DescribeSignals(IReadOnlyList<MailAssociationSignal> signals)
    {
        var names = signals.Select(signal => signal switch
        {
            MailAssociationSignal.ConversationThread => "conversation thread",
            MailAssociationSignal.ReplyHeaders => "reply headers",
            MailAssociationSignal.ClaimReference => "claim reference",
            _ => "vehicle registration"
        }).ToArray();
        return names.Length switch
        {
            1 => names[0],
            2 => $"{names[0]} and {names[1]}",
            _ => $"{string.Join(", ", names[..^1])} and {names[^1]}"
        };
    }
}
