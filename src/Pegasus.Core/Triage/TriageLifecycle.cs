using System.Diagnostics;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Triage;

public sealed class CreateTriageFromIntake(ITriageStore store, ITriageCasePairing pairing) : ICreateTriageFromIntake
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        CreateTriageFromIntakeRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateCreate(request);
        var created = await _store.CreateAsync(request, cancellationToken);
        var result = await pairing.PairTriageAsync(created.CaseId, cancellationToken);
        Activity.Current?.SetTag("triage.pairing_failures", result.Failures);
        Activity.Current?.SetTag("triage.failure_type", result.FirstFailure);
        // Creation replay reports its original result, not a later link or state.
        return created;
    }
}

public sealed class TriageCasePairing(ITriageStore store) : ITriageCasePairing
{
    public const string ActorId = "triage-case-pairing";
    private static readonly ActivitySource Telemetry = new("Pegasus.Core.Triage");

    public Task<TriageCasePairingResult> PairTriageAsync(Guid triageCaseId, CancellationToken cancellationToken) =>
        PairAsync(triageCaseId, null, 1, cancellationToken);

    public Task<TriageCasePairingResult> PairAcceptedCaseAsync(Guid caseId, CancellationToken cancellationToken) =>
        PairAsync(null, caseId, 50, cancellationToken);

    public Task<TriageCasePairingResult> ReconcileAsync(int maximumItems, CancellationToken cancellationToken) =>
        PairAsync(null, null, maximumItems, cancellationToken);

    private async Task<TriageCasePairingResult> PairAsync(
        Guid? triageCaseId, Guid? instructionCaseId, int maximumItems, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        IReadOnlyList<TriageCaseLinkCandidate> candidates;
        try
        {
            candidates = await store.ListAutomaticLinkCandidatesAsync(
                triageCaseId, instructionCaseId, maximumItems, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return new(0, 0, 1, exception.GetType().Name);
        }
        if (candidates.Count == 0)
        {
            // The Worker asks every ten seconds and usually nothing is waiting.
            // A span is opened only for a pass that has something to link.
            return new(0, 0, 0);
        }

        using var activity = Telemetry.StartActivity("triage_case_pairing");
        var linked = 0;
        var failures = 0;
        string? firstFailure = null;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (await store.LinkAutomaticallyAsync(
                        candidate, ActionActor.SystemWorker(ActorId), cancellationToken))
                {
                    linked++;
                }
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                failures++;
                firstFailure ??= exception.GetType().Name;
            }
        }

        activity?.SetTag("triage.pairing_failures", failures);
        activity?.SetTag("triage.failure_type", firstFailure);
        if (failures > 0)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "triage_pairing_failed");
        }
        return new(candidates.Count, linked, failures, firstFailure);
    }

    public static async Task<Guid?> MatchAsync(
        IPrincipalCaseMatchPolicy policy,
        ICaseMatchCandidateQueries candidates,
        string acceptedRegistration,
        CaseMatchSourceData source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(source);
        var suppliedRegistration = policy.DeriveIndexKeys(source).NormalizedVrm;
        if (!string.IsNullOrWhiteSpace(source.VehicleRegistration)
            && suppliedRegistration != acceptedRegistration)
        {
            return null;
        }

        var result = await new EvaluateIntakeCaseMatch([policy], candidates).ExecuteDeclaredAsync(
            policy.PrincipalCode, source with { VehicleRegistration = acceptedRegistration }, cancellationToken);
        return result is { Outcome: CaseMatchOutcome.UniqueMatch } ? result.MatchedCaseId : null;
    }

    public static bool CanLinkTarget(
        CaseLifecycleState state, bool archived, ActorKind? leaseHolderKind,
        DateTimeOffset? leaseExpiresAtUtc, DateTimeOffset nowUtc) =>
        !archived && !CaseLifecycleRules.IsTerminal(state)
        && !(leaseHolderKind == ActorKind.Staff && leaseExpiresAtUtc > nowUtc);
}

public sealed class AssignTriage(ITriageStore store) : IAssignTriage
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        AssignTriageRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateAssign(request);
        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        TriageLifecycleRules.RequireMutable(current.Record, "assign");
        return await _store.AssignAsync(request, cancellationToken);
    }
}

/// <summary>
/// Appends one operator note to the Triage's permanent history.
/// </summary>
/// <remarks>
/// The note goes into the same replay-probed history every other Triage
/// mutation writes, so a retried append returns the committed entry rather
/// than recording the note twice. A note is never editable and never replaces
/// an earlier one; correcting a note means writing another.
/// </remarks>
public sealed class AddTriageNote(ITriageStore store) : IAddTriageNote
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        AddTriageNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        TriageLifecycleRules.ValidateNote(request);
        if (await _store.ProbeAddNoteReplayAsync(request, cancellationToken) is { } replay)
        {
            return replay.Result;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(
            _store,
            request.CaseId,
            cancellationToken);
        TriageLifecycleRules.RequireMutable(current.Record, "note");
        return await _store.AddNoteAsync(request, cancellationToken);
    }
}

public sealed class UnassignTriage(ITriageStore store) : IUnassignTriage
{
    /// <summary>The fixed history text: removing the assignee needs no reason.</summary>
    public const string Reason = "Unassigned.";

    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        TriageTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var mutation = TriageLifecycleRules.ToMutation(request, Reason);
        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        TriageLifecycleRules.RequireMutable(current.Record, "unassign");
        return await _store.UnassignAsync(mutation, cancellationToken);
    }
}

public sealed class AwaitTriageInformation(ITriageStore store) : IAwaitTriageInformation
{
    /// <summary>The fixed history text: the transition asks for no reason.</summary>
    public const string Reason = "Awaiting information.";

    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        TriageTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var mutation = TriageLifecycleRules.ToMutation(request, Reason);
        var replay = await _store.ProbeStateChangeReplayAsync(
            mutation,
            TriageState.AwaitingInformation,
            cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.Record.State is not (TriageState.Open or TriageState.FindingRecorded))
        {
            throw new InvalidOperationException(
                "Triage can await information only while open or after a finding is recorded.");
        }

        return await _store.ChangeStateAsync(mutation, TriageState.AwaitingInformation, cancellationToken);
    }
}

public sealed class RecordTriageFinding(ITriageStore store) : IRecordTriageFinding
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateFinding(request, requiresSupersededFinding: false);
        var replay = await _store.ProbeRecordFindingReplayAsync(request, cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        TriageLifecycleRules.RequireMutable(current.Record, "record a finding");
        if (current.Record.State is not (TriageState.Open or TriageState.AwaitingInformation)
            || TriageLifecycleRules.HasActiveFinding(current))
        {
            throw new InvalidOperationException(
                "An existing active finding must be explicitly superseded.");
        }

        return await _store.RecordFindingAsync(request, cancellationToken);
    }
}

public sealed class SupersedeTriageFinding(ITriageStore store) : ISupersedeTriageFinding
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        RecordTriageFindingRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateFinding(request, requiresSupersededFinding: true);
        var replay = await _store.ProbeSupersedeFindingReplayAsync(request, cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.Record.State == TriageState.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled triage must be reopened before a finding can be corrected.");
        }

        if (!TriageLifecycleRules.IsActiveFinding(
                current,
                request.SupersedesFindingId!.Value))
        {
            throw new InvalidOperationException(
                "Only the current active finding can be superseded.");
        }

        return await _store.SupersedeFindingAsync(request, cancellationToken);
    }
}

public sealed class LinkTriageResponseEvidence(ITriageStore store) : ILinkTriageResponseEvidence
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task ExecuteAsync(
        TriageResponseEvidenceLinkRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateResponseEvidence(request);
        if (await _store.ProbeLinkResponseEvidenceReplayAsync(request, cancellationToken) is not null)
        {
            return;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        TriageLifecycleRules.RequireMutable(current.Record, "link response evidence");
        await _store.LinkResponseEvidenceAsync(request, cancellationToken);
    }
}

public sealed class UnlinkTriageResponseEvidence(ITriageStore store) : IUnlinkTriageResponseEvidence
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task ExecuteAsync(
        TriageResponseEvidenceUnlinkRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateResponseEvidence(request);
        if (await _store.ProbeUnlinkResponseEvidenceReplayAsync(request, cancellationToken) is not null)
        {
            return;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        TriageLifecycleRules.RequireMutable(current.Record, "unlink response evidence");
        await _store.UnlinkResponseEvidenceAsync(request, cancellationToken);
    }
}

public sealed class CompleteTriage(ITriageStore store) : ICompleteTriage
{
    /// <summary>The fixed history text: completion records the outcome, actor and time.</summary>
    public const string Reason = "Outcome recorded.";

    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        TriageTransitionRequest request,
        CancellationToken cancellationToken)
    {
        var mutation = TriageLifecycleRules.ToMutation(request, Reason);
        var replay = await _store.ProbeStateChangeReplayAsync(
            mutation,
            TriageState.Completed,
            cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.Record.State != TriageState.FindingRecorded)
        {
            throw new InvalidOperationException("Triage can be completed only after a finding is recorded.");
        }

        return await _store.ChangeStateAsync(mutation, TriageState.Completed, cancellationToken);
    }
}

public sealed class CancelTriage(ITriageStore store) : ICancelTriage
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        TriageMutationRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateMutation(request);
        var replay = await _store.ProbeStateChangeReplayAsync(
            request,
            TriageState.Cancelled,
            cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        TriageLifecycleRules.RequireMutable(current.Record, "cancel");
        return await _store.ChangeStateAsync(request, TriageState.Cancelled, cancellationToken);
    }
}

public sealed class ReopenTriage(ITriageStore store) : IReopenTriage
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TriageRecord> ExecuteAsync(
        TriageMutationRequest request,
        CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateMutation(request);
        var replay = await _store.ProbeStateChangeReplayAsync(
            request,
            TriageState.Open,
            cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        var current = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        if (current.Record.State is not (TriageState.Completed or TriageState.Cancelled))
        {
            throw new InvalidOperationException("Only completed or cancelled triage can be reopened.");
        }

        return await _store.ChangeStateAsync(request, TriageState.Open, cancellationToken);
    }
}

public sealed class LinkTriageCase(ITriageStore store) : ILinkTriageCase
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task ExecuteAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateCaseLink(request);
        _ = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        await _store.LinkCaseAsync(request, cancellationToken);
    }
}

public sealed class UnlinkTriageCase(ITriageStore store) : IUnlinkTriageCase
{
    private readonly ITriageStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task ExecuteAsync(TriageCaseLinkRequest request, CancellationToken cancellationToken)
    {
        TriageLifecycleRules.ValidateCaseLink(request);
        _ = await TriageLifecycleRules.GetRequiredAsync(_store, request.CaseId, cancellationToken);
        await _store.UnlinkCaseAsync(request, cancellationToken);
    }
}

public static class TriageLifecycleRules
{
    public static async Task<TriageDetail> GetRequiredAsync(
        ITriageQueries queries,
        Guid triageId,
        CancellationToken cancellationToken)
    {
        if (triageId == Guid.Empty)
        {
            throw new ArgumentException("A triage identifier is required.", nameof(triageId));
        }

        return await queries.GetAsync(triageId, cancellationToken)
            ?? throw new KeyNotFoundException($"Triage '{triageId}' was not found.");
    }

    public static void ValidateCreate(CreateTriageFromIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Origin, nameof(request));
        ValidateOrigin(request.Origin);
        ValidateNormalizedRegistration(request.NormalizedVehicleRegistration);
        ValidateAcceptedMatchEvidence(request.AcceptedMatchEvidence);
        ValidateActorAndOperation(request.Actor, request.OperationKey, allowSystemWorker: true);
    }

    public static void ValidateMutation(TriageMutationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
    }

    /// <summary>
    /// A reasonless change as the store records it: the same mutation every
    /// state change writes, with the command's fixed history text.
    /// </summary>
    public static TriageMutationRequest ToMutation(TriageTransitionRequest request, string reason)
    {
        ArgumentNullException.ThrowIfNull(request);
        var mutation = new TriageMutationRequest(
            request.CaseId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            reason)
        {
            EditLeaseToken = request.EditLeaseToken
        };
        ValidateMutation(mutation);
        return mutation;
    }

    /// <summary>
    /// The staff reply a Triage offers from its origin message: the chaser until
    /// the outcome is recorded, Reply with outcome once Completed, and none once
    /// Cancelled. Sending is never a gate (FRD-03).
    /// </summary>
    public static StaffMailPurpose? ReplyPurpose(TriageState state) => state switch
    {
        TriageState.Completed => StaffMailPurpose.TriageOutcomeReply,
        TriageState.Cancelled => null,
        _ => StaffMailPurpose.TriageChaser
    };

    /// <summary>
    /// The states a Triage is still being worked in: the Triage queue, the shell
    /// count and the Triages metric read these. Completed and Cancelled Triages
    /// are found through Search (FRD-15).
    /// </summary>
    public static IReadOnlyCollection<TriageState> ActiveStates { get; } =
        [TriageState.Open, TriageState.AwaitingInformation, TriageState.FindingRecorded];

    /// <summary>
    /// The outcome reply for the Triage's current completion has been sent, so
    /// Reply with finding is no longer offered; only Reopen and a fresh
    /// completion offer it again (FRD-03). A failed or cancelled send leaves it
    /// offered.
    /// </summary>
    public static bool OutcomeReplySent(TriageDetail detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        if (detail.Record.State != TriageState.Completed
            || detail.SentOutcomeReplyVersion is not { } sentAgainst)
        {
            return false;
        }

        // The version the Triage entered Completed at. Later entries that leave
        // it Completed (a linked Sent item) do not start a new completion.
        long? completedAtVersion = null;
        TriageState? previous = null;
        foreach (var entry in detail.History.OrderBy(item => item.AfterVersion))
        {
            if (entry.AfterState == TriageState.Completed && previous != TriageState.Completed)
            {
                completedAtVersion = entry.AfterVersion;
            }

            previous = entry.AfterState;
        }

        return completedAtVersion is { } version && sentAgainst >= version;
    }

    public static void ValidateNote(AddTriageNoteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(
            request.Note,
            "A note is required.",
            TriageNotes.MaximumLength,
            nameof(request));
    }

    public static void ValidateAssign(AssignTriageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdAndVersion(request.CaseId, request.ExpectedVersion);
        if (request.AssigneeId == Guid.Empty)
        {
            throw new ArgumentException("An assignee is required.", nameof(request));
        }

        ValidateActorAndOperation(request.Actor, request.OperationKey);
    }

    public static void ValidateFinding(
        RecordTriageFindingRequest request,
        bool requiresSupersededFinding)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdAndVersion(request.CaseId, request.ExpectedVersion);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));

        if (request.Roadworthiness is null && request.Assessment is null)
        {
            throw new ArgumentException("At least one triage finding is required.", nameof(request));
        }

        if (request.Roadworthiness is { } roadworthiness && !Enum.IsDefined(roadworthiness))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The roadworthiness finding is invalid.");
        }

        if (request.Assessment is { } assessment && !Enum.IsDefined(assessment))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The assessment finding is invalid.");
        }

        if (requiresSupersededFinding
            && (request.SupersedesFindingId is not { } findingId || findingId == Guid.Empty))
        {
            throw new ArgumentException("A finding to supersede is required.", nameof(request));
        }

        if (!requiresSupersededFinding && request.SupersedesFindingId is not null)
        {
            throw new ArgumentException("A new finding cannot supersede another finding.", nameof(request));
        }
    }

    public static void ValidateResponseEvidence(TriageResponseEvidenceLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdAndVersion(request.CaseId, request.ExpectedVersion);
        if (request.PollOutcomeId == Guid.Empty)
        {
            throw new ArgumentException("An approved Sent poll outcome is required.", nameof(request));
        }

        if (request.SentEvidenceId == Guid.Empty)
        {
            throw new ArgumentException("Sent response evidence is required.", nameof(request));
        }

        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
    }

    public static void ValidateResponseEvidence(TriageResponseEvidenceUnlinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdAndVersion(request.CaseId, request.ExpectedVersion);
        if (request.SentEvidenceId == Guid.Empty)
        {
            throw new ArgumentException("Sent response evidence is required.", nameof(request));
        }

        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
    }

    public static void ValidateCaseLink(TriageCaseLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIdAndVersion(request.CaseId, request.ExpectedTriageVersion);
        if (request.InstructionCaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        if (request.ExpectedCaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected case version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        ValidateActorAndOperation(request.Actor, request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
        RequireText(
            request.CaseEditLeaseToken,
            "An active case edit lease token is required.",
            128,
            nameof(request));
    }

    public static void RequireMutable(TriageRecord triage, string action)
    {
        if (triage.State is TriageState.Completed or TriageState.Cancelled)
        {
            throw new InvalidOperationException($"Completed or cancelled triage cannot {action}.");
        }
    }

    internal static bool HasActiveFinding(TriageDetail triage) =>
        triage.Findings.Any(finding => IsUnsupersededFinding(triage, finding.Id));

    internal static bool IsActiveFinding(TriageDetail triage, Guid findingId) =>
        triage.Findings.Any(finding => finding.Id == findingId)
        && IsUnsupersededFinding(triage, findingId)
        && triage.Findings.Count(
            finding => IsUnsupersededFinding(triage, finding.Id)) == 1;

    private static bool IsUnsupersededFinding(TriageDetail triage, Guid findingId) =>
        !triage.Findings.Any(finding => finding.SupersedesFindingId == findingId);

    private static void ValidateOrigin(TriageOrigin origin)
    {
        if (origin.ReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An originating intake receipt is required.", nameof(origin));
        }

        ArgumentNullException.ThrowIfNull(origin.SourceIdentity, nameof(origin));
        if (!Enum.IsDefined(origin.SourceIdentity.Channel))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "The intake source channel is invalid.");
        }

        RequireText(
            origin.SourceIdentity.ExternalReceiptToken,
            "The source receipt token is required.",
            200,
            nameof(origin));
        RequireText(
            origin.SourceHash,
            "The source hash is required.",
            64,
            nameof(origin));
        if (origin.EvaluationRevisionId == Guid.Empty)
        {
            throw new ArgumentException("A completed intake evaluation revision is required.", nameof(origin));
        }

        if (origin.SourceHash.Length != 64 || origin.SourceHash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The source hash must be a SHA-256 hexadecimal value.", nameof(origin));
        }
    }

    private static void ValidateAcceptedMatchEvidence(Pegasus.Core.Intake.IntakeEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enum.IsDefined(evidence.Source))
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidence),
                "The accepted Triage-match evidence source is invalid.");
        }
        if (evidence.Strength != Pegasus.Core.Intake.IntakeEvidenceStrength.Strong
            || evidence.Finding != Pegasus.Core.Intake.IntakeEvidenceFinding.AcceptedTriageMatch)
        {
            throw new ArgumentException(
                "Triage creation requires explicit strong accepted Triage-match evidence.",
                nameof(evidence));
        }

        RequireText(
            evidence.Signal,
            "The accepted Triage-match signal is required.",
            100,
            nameof(evidence));
        RequireText(
            evidence.Detail,
            "The accepted Triage-match detail is required.",
            500,
            nameof(evidence));
        RequireText(
            evidence.MatcherKey!,
            "The accepted Triage matcher key is required.",
            100,
            nameof(evidence));
        if (evidence.MatcherVersion is null or <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidence),
                "The accepted Triage matcher version must be positive.");
        }
    }

    private static void ValidateNormalizedRegistration(string registration)
    {
        RequireText(registration, "A normalized vehicle registration is required.", 20, nameof(registration));
        if (registration.Any(character => !(char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))))
        {
            throw new ArgumentException(
                "The vehicle registration must be uppercase ASCII letters and digits with no separators.",
                nameof(registration));
        }
    }

    private static void ValidateIdAndVersion(Guid triageId, long expectedVersion)
    {
        if (triageId == Guid.Empty)
        {
            throw new ArgumentException("A triage identifier is required.", nameof(triageId));
        }

        if (expectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion), "The expected version cannot be negative.");
        }
    }

    private static void ValidateActorAndOperation(
        ActionActor actor,
        string operationKey,
        bool allowSystemWorker = false)
    {
        RequireActor(actor, allowSystemWorker);
        RequireText(operationKey, "An operation key is required.", 100, nameof(operationKey));
    }

    /// <summary>
    /// Triage carries the acting identity, not a subject string, so history records
    /// the kind that made each mutation. Staff and Automation require casework
    /// authority; the system worker is admitted only by the intake-creation route.
    /// Nothing infers a kind from a prefix or defaults to Staff.
    /// </summary>
    private static void RequireActor(ActionActor actor, bool allowSystemWorker)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind == ActorKind.SystemWorker && allowSystemWorker)
        {
            StaffAuthorization.Require(actor, StaffAccessRight.ExecuteSystemWork);
        }
        else if (actor.Kind is ActorKind.Staff or ActorKind.Automation)
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        }
        else
        {
            throw new UnauthorizedAccessException("This actor cannot mutate Triage material.");
        }

        RequireText(actor.SubjectId, "An actor is required.", 200, nameof(actor));
    }

    private static void RequireText(string value, string message, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
