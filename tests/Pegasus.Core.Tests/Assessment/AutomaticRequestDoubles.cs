using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tests.Workflow;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The doubles the automatic request tests share: the valuation tests and the
/// Experian check tests run the one <see cref="RunAutomaticRequest"/>.
/// </summary>
internal static class AutomaticRequestDoubles
{
    /// <summary>
    /// The runner over the given doubles. With no history check given, Experian
    /// is not connected; with no leases given, no lease is ever claimed.
    /// </summary>
    public static RunAutomaticRequest Runner(
        IFetchGuideValuation fetch,
        ICaseDataQueries caseData,
        RecordingAutomaticStore store,
        DateTimeOffset now,
        bool enabled = true,
        RunVehicleHistoryCheck? historyCheck = null,
        ScriptedCaseLeases? leases = null) =>
        new(
            fetch,
            historyCheck ?? new RunVehicleHistoryCheck([], caseData, new SaveAssessment(new UnusedAssessmentStore())),
            new OneCommandEditAuthority(
                new CaseEditLeases(leases ?? new ScriptedCaseLeases(), new RecordingCaseEditSignal()),
                new UnusedEditScopes()),
            caseData,
            store,
            new FixedAutomaticSwitch(enabled),
            new FixedAutomaticClock(now));

    public static AutomaticRequestTarget Open() =>
        new(IsOpen: true, HoldsAnswer: false, EditLeaseExpiresAtUtc: null, AcceptsAssessment: true);
}

internal sealed class RecordingAutomaticStore(AutomaticRequestTarget target, bool begins = true)
    : IAutomaticRequestStore
{
    public List<string> Steps { get; } = [];

    public List<(ActionActor Actor, ValuationDetails Card)> Recorded { get; } = [];

    public List<AutomaticRequestKind> TargetsRead { get; } = [];

    public Task<IReadOnlyList<AutomaticRequest>> ListDueAutomaticAsync(
        DateTimeOffset nowUtc, int maximumItems, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<AutomaticRequestTarget?> ReadAutomaticTargetAsync(
        Guid caseId, AutomaticRequestKind kind, CancellationToken cancellationToken)
    {
        TargetsRead.Add(kind);
        return Task.FromResult<AutomaticRequestTarget?>(target);
    }

    public Task DeferAutomaticAsync(Guid workItemId, DateTimeOffset dueAtUtc, CancellationToken cancellationToken)
    {
        Steps.Add($"defer:{dueAtUtc:O}");
        return Task.CompletedTask;
    }

    public Task<bool> BeginAutomaticAsync(Guid workItemId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        Steps.Add("begin");
        return Task.FromResult(begins);
    }

    public Task CompleteAutomaticAsync(
        Guid workItemId,
        AutomaticRequestOutcome outcome,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Steps.Add($"complete:{outcome}");
        return Task.CompletedTask;
    }

    public Task<bool> RecordAutomaticAsync(
        Guid caseId,
        ActionActor actor,
        string operationKey,
        ValuationDetails card,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Steps.Add("record");
        Recorded.Add((actor, card));
        return Task.FromResult(true);
    }
}

internal sealed class FixedAutomaticSwitch(bool on) : IAutomaticRequestControl
{
    public Task<bool> IsEnabledAsync(AutomaticRequestKind kind, CancellationToken cancellationToken) =>
        Task.FromResult(on);

    public Task<IReadOnlyDictionary<AutomaticRequestKind, bool>> ReadAsync(CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> SetEnabledAsync(
        AutomaticRequestKind kind,
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>
/// The Case lease port as the automatic check meets it: each claim is
/// recorded and answered with a lease, or refused with <see cref="ClaimRefusal"/>;
/// each release is recorded.
/// </summary>
internal sealed class ScriptedCaseLeases : ILeaseCaseForEdit
{
    public const string Token = "automatic-lease-token";

    public Exception? ClaimRefusal { get; init; }

    public List<ClaimCaseEditLeaseRequest> Claims { get; } = [];

    public List<ReleaseCaseEditLeaseRequest> Releases { get; } = [];

    public Task<CaseEditLease> ClaimAsync(ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken)
    {
        Claims.Add(request);
        return ClaimRefusal is { } refusal
            ? Task.FromException<CaseEditLease>(refusal)
            : Task.FromResult(new CaseEditLease(
                request.CaseId,
                Token,
                request.Actor.SubjectId,
                request.ExpectedVersion,
                DateTimeOffset.UnixEpoch.AddYears(100)));
    }

    public Task ReleaseAsync(ReleaseCaseEditLeaseRequest request, CancellationToken cancellationToken)
    {
        Releases.Add(request);
        return Task.CompletedTask;
    }

    public Task<CaseEditLease> HeartbeatAsync(HeartbeatCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CaseEditLease?> ResumeAsync(ResumeCaseEditLeaseRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CaseEditRequestRaised> RaiseRequestAsync(RaiseCaseEditRequestRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task AnswerRequestAsync(AnswerCaseEditRequestRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CaseEditRequestView> ReadRequestAsync(Guid caseId, ActionActor actor, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class UnusedEditScopes : IEditScopeLeases
{
    public Task<EditScopeSnapshot?> GetActiveAsync(
        EditScopeKind scopeKind, Guid recordId, ActionActor actor, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<EditScopeLease> ClaimAsync(ClaimEditScopeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<EditScopeLease> HeartbeatAsync(HeartbeatEditScopeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task ReleaseAsync(ReleaseEditScopeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class UnusedAssessmentStore : ICaseAssessmentStore
{
    public Task<CaseAssessmentProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<CaseAssessmentProjection> SaveAsync(SaveAssessmentRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>No valuation source is connected; nothing is fetched.</summary>
internal sealed class NoGuideValuations : IFetchGuideValuation
{
    public Task<GuideValuationQuote> ExecuteAsync(FetchGuideValuationRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public bool IsConnected(ValuationSource source) => false;
}

internal sealed class FixedAutomaticClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
