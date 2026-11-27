using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The acts a new Case asks automatically, each behind its own Administrator
/// switch (R545, R546; operator, 26 November 2026, R1286): the Glass's,
/// Brego, Cazana and CAP valuations and the Experian vehicle history check.
/// </summary>
public enum AutomaticRequestKind
{
    GlassesValuation,
    BregoValuation,
    CazanaValuation,
    CapValuation,
    VehicleHistoryCheck,
}

/// <summary>
/// The Administrator's switches for the automatic requests (R546, 29 October
/// 2026; R1286 and R1287, 26 November 2026), held beside the Send to AI switch
/// and in the same shape (<see cref="Pegasus.Core.AiWork.ISendToAiControl"/>).
/// Case creation reads them: a Case created while a switch is off has no
/// request of that kind, then or later.
/// </summary>
public interface IAutomaticRequestControl
{
    Task<bool> IsEnabledAsync(AutomaticRequestKind kind, CancellationToken cancellationToken);

    /// <summary>Every kind's switch, read at once.</summary>
    Task<IReadOnlyDictionary<AutomaticRequestKind, bool>> ReadAsync(CancellationToken cancellationToken);

    Task<bool> SetEnabledAsync(
        AutomaticRequestKind kind,
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken);
}

/// <summary>One Case's automatic request of one kind, recorded when the Case was created.</summary>
public sealed record AutomaticRequest(Guid WorkItemId, Guid CaseId, AutomaticRequestKind Kind);

/// <summary>
/// What the Case is as its automatic request finds it: whether it is open,
/// whether its current work already holds the answer (a card of the
/// valuation's source, or Vehicle history text for the history check), its
/// edit lease, and whether its lifecycle state accepts an assessment save
/// (<see cref="AssessmentPolicy.IsWritableState"/>), so its Vehicle history
/// text is editable.
/// </summary>
public sealed record AutomaticRequestTarget(
    bool IsOpen,
    bool HoldsAnswer,
    DateTimeOffset? EditLeaseExpiresAtUtc,
    bool AcceptsAssessment);

/// <summary>
/// The automatic requests and the Case they ask for. A request is recorded
/// once, by Case creation, so a Case has one automatic request of each kind
/// ever (R545, R1286); this store reads, waits, starts and settles it, and
/// records a valuation's card as system work: the Case version advances and
/// the edit lease is left as it is (FRD-14).
/// </summary>
public interface IAutomaticRequestStore
{
    /// <summary>The waiting requests of every kind due by <paramref name="nowUtc"/>, oldest first.</summary>
    Task<IReadOnlyList<AutomaticRequest>> ListDueAutomaticAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>
    /// The Case and whether its current work already holds the kind's answer;
    /// null when no Case has a workflow.
    /// </summary>
    Task<AutomaticRequestTarget?> ReadAutomaticTargetAsync(
        Guid caseId,
        AutomaticRequestKind kind,
        CancellationToken cancellationToken);

    /// <summary>Leaves the request waiting until <paramref name="dueAtUtc"/>.</summary>
    Task DeferAutomaticAsync(Guid workItemId, DateTimeOffset dueAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the request as asked, before the provider is asked, so it is
    /// never asked twice; false when it no longer waits.
    /// </summary>
    Task<bool> BeginAutomaticAsync(Guid workItemId, DateTimeOffset nowUtc, CancellationToken cancellationToken);

    /// <summary>Settles the request with its outcome; it is never asked again.</summary>
    Task CompleteAutomaticAsync(
        Guid workItemId,
        AutomaticRequestOutcome outcome,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a valuation's card on the current work in one transaction while
    /// the Case is open and the work holds no card of the source; false, with
    /// nothing written, otherwise.
    /// </summary>
    Task<bool> RecordAutomaticAsync(
        Guid caseId,
        ActionActor actor,
        string operationKey,
        ValuationDetails card,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>What became of one automatic request on one pass.</summary>
public enum AutomaticRequestOutcome
{
    /// <summary>The card, or the Vehicle history text, was recorded.</summary>
    Recorded,

    /// <summary>
    /// The request waits: the Case is being edited, or its registration (and,
    /// for a valuation, its mileage) is not yet accepted.
    /// </summary>
    Waiting,

    /// <summary>The Administrator's switch for the kind is off, so the request is settled unasked.</summary>
    SwitchedOff,

    /// <summary>
    /// The Case is closed or archived, its current work already holds the
    /// answer (a card of the source, or Vehicle history text), or the source
    /// has already been asked this month (R900).
    /// </summary>
    NotNeeded,

    /// <summary>
    /// The provider answered that it cannot value the vehicle, or holds no
    /// vehicle for the registration.
    /// </summary>
    NotValued,

    /// <summary>The provider is not connected on this host or could not answer.</summary>
    Unavailable,

    /// <summary>
    /// The provider answered, but the Case closed, took the answer meanwhile
    /// or refused the write.
    /// </summary>
    NotRecorded,
}

/// <summary>
/// The automatic requests (R465, 28 October 2026; R545 and R546, 29 October
/// 2026; R1286 and R1287, 26 November 2026): one request per kind per Case,
/// recorded at its creation while the kind's switch is on, beside the
/// automatic vehicle lookup.
/// </summary>
public static class AutomaticRequestPolicy
{
    /// <summary>The Glass's recorder; existing rows and history carry it.</summary>
    public const string GlassesActorId = "glass-valuation";

    public const string BregoActorId = "brego-valuation";

    public const string CazanaActorId = "cazana-valuation";

    public const string CapActorId = "cap-valuation";

    /// <summary>The recorder of the automatic Experian check's Vehicle history text.</summary>
    public const string VehicleHistoryCheckActorId = "vehicle-history-check";

    private static readonly ActionActor GlassesActor = ActionActor.Automation(GlassesActorId);
    private static readonly ActionActor BregoActor = ActionActor.Automation(BregoActorId);
    private static readonly ActionActor CazanaActor = ActionActor.Automation(CazanaActorId);
    private static readonly ActionActor CapActor = ActionActor.Automation(CapActorId);
    private static readonly ActionActor VehicleHistoryCheckActor = ActionActor.Automation(VehicleHistoryCheckActorId);

    /// <summary>How long a waiting request waits before it is read again.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMinutes(1);

    /// <summary>The valuation source the kind asks; null for the history check.</summary>
    public static ValuationSource? Source(AutomaticRequestKind kind) => kind switch
    {
        AutomaticRequestKind.GlassesValuation => ValuationSource.Glasses,
        AutomaticRequestKind.BregoValuation => ValuationSource.Brego,
        AutomaticRequestKind.CazanaValuation => ValuationSource.Cazana,
        AutomaticRequestKind.CapValuation => ValuationSource.Cap,
        AutomaticRequestKind.VehicleHistoryCheck => null,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The automatic request kind is unknown."),
    };

    /// <summary>The Automation recorder the kind's request is made and recorded as.</summary>
    public static ActionActor Actor(AutomaticRequestKind kind) => kind switch
    {
        AutomaticRequestKind.GlassesValuation => GlassesActor,
        AutomaticRequestKind.BregoValuation => BregoActor,
        AutomaticRequestKind.CazanaValuation => CazanaActor,
        AutomaticRequestKind.CapValuation => CapActor,
        AutomaticRequestKind.VehicleHistoryCheck => VehicleHistoryCheckActor,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The automatic request kind is unknown."),
    };

    /// <summary>A closed Case, or a work that already holds the answer, needs no automatic request.</summary>
    public static bool IsNeeded(AutomaticRequestTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.IsOpen && !target.HoldsAnswer;
    }

    /// <summary>
    /// Whether the work holds the facts the kind asks about: a valuation values
    /// an accepted registration at an accepted mileage
    /// (<see cref="FetchGuideValuation.HasVehicleFacts"/>); the history check
    /// checks the accepted registration Run Experian check checks
    /// (<see cref="RunVehicleHistoryCheck.AcceptedRegistration"/>).
    /// </summary>
    public static bool HasFacts(AutomaticRequestKind kind, CaseDataProjection data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Source(kind) is null
            ? !string.IsNullOrWhiteSpace(RunVehicleHistoryCheck.AcceptedRegistration(data))
            : FetchGuideValuation.HasVehicleFacts(data);
    }

    /// <summary>
    /// System work yields to a held edit lease (FRD-14), and the request waits
    /// for the facts it asks about (<see cref="HasFacts"/>): as the vehicle
    /// lookup waits for a registration, and so a mileage the vehicle lookup
    /// fills is the one valued. The history check is Run Experian check run by
    /// the system, which is offered only while the Vehicle history text is
    /// editable (R746), so it also waits while the Case's state accepts no
    /// assessment save, such as a Held Case; a valuation does not.
    /// </summary>
    public static bool Waits(
        AutomaticRequestKind kind,
        AutomaticRequestTarget target,
        bool hasVehicleFacts,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(target);
        return !hasVehicleFacts
            || CaseEditAuthority.IsHeld(target.EditLeaseExpiresAtUtc, nowUtc)
            || (Source(kind) is null && !target.AcceptsAssessment);
    }
}

/// <summary>
/// Runs a Case's automatic requests. A valuation is Get valuation run by the
/// system: the same <see cref="IFetchGuideValuation"/> for the current work and
/// the current month, so the report is filed and an empty VIN filled as they
/// are for staff, and the figures are recorded as the card a Save records. The
/// history check is Run Experian check run by the system under an edit lease
/// it claims for that one command and gives back (R1286, R1291). Each provider
/// is asked at most once per request; whatever it answers settles the request,
/// and any later ask is staff-started.
/// </summary>
public sealed class RunAutomaticRequest(
    IFetchGuideValuation fetch,
    RunVehicleHistoryCheck historyCheck,
    OneCommandEditAuthority editAuthority,
    ICaseDataQueries caseData,
    IAutomaticRequestStore store,
    IAutomaticRequestControl control,
    TimeProvider timeProvider)
{
    /// <summary>How many requests one pass reads.</summary>
    public const int BatchSize = 10;

    private readonly IFetchGuideValuation _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    private readonly RunVehicleHistoryCheck _historyCheck =
        historyCheck ?? throw new ArgumentNullException(nameof(historyCheck));
    private readonly OneCommandEditAuthority _editAuthority =
        editAuthority ?? throw new ArgumentNullException(nameof(editAuthority));
    private readonly ICaseDataQueries _caseData = caseData ?? throw new ArgumentNullException(nameof(caseData));
    private readonly IAutomaticRequestStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IAutomaticRequestControl _control =
        control ?? throw new ArgumentNullException(nameof(control));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<IReadOnlyList<AutomaticRequest>> ListDueAsync(CancellationToken cancellationToken) =>
        _store.ListDueAutomaticAsync(_timeProvider.GetUtcNow(), BatchSize, cancellationToken);

    public async Task<AutomaticRequestOutcome> ExecuteAsync(
        AutomaticRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var source = AutomaticRequestPolicy.Source(request.Kind);

        // Turning a switch off settles what is still waiting of its kind: a
        // request is made only while its switch is on (R546).
        if (!await _control.IsEnabledAsync(request.Kind, cancellationToken))
        {
            return await SettleAsync(request, AutomaticRequestOutcome.SwitchedOff, cancellationToken);
        }

        if (source is { } connectedSource ? !_fetch.IsConnected(connectedSource) : !_historyCheck.IsConnected)
        {
            return await SettleAsync(request, AutomaticRequestOutcome.Unavailable, cancellationToken);
        }

        var target = await _store.ReadAutomaticTargetAsync(request.CaseId, request.Kind, cancellationToken);
        if (target is null || !AutomaticRequestPolicy.IsNeeded(target))
        {
            return await SettleAsync(request, AutomaticRequestOutcome.NotNeeded, cancellationToken);
        }

        var data = await _caseData.GetAsync(request.CaseId, CaseWorkSelector.Current, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        if (data is null
            || AutomaticRequestPolicy.Waits(
                request.Kind, target, AutomaticRequestPolicy.HasFacts(request.Kind, data), now))
        {
            return await DeferAsync(request, now, cancellationToken);
        }

        if (source is not { } valuationSource)
        {
            return await CheckHistoryAsync(request, data, now, cancellationToken);
        }

        if (!await _store.BeginAutomaticAsync(request.WorkItemId, now, cancellationToken))
        {
            return AutomaticRequestOutcome.NotRecorded;
        }

        var outcome = await AskAsync(request, valuationSource, data.Version, cancellationToken);
        await _store.CompleteAutomaticAsync(request.WorkItemId, outcome, _timeProvider.GetUtcNow(), cancellationToken);
        return outcome;
    }

    private async Task<AutomaticRequestOutcome> AskAsync(
        AutomaticRequest request,
        ValuationSource source,
        long caseVersion,
        CancellationToken cancellationToken)
    {
        var actor = AutomaticRequestPolicy.Actor(request.Kind);
        var operationKey = $"automatic-guide-valuation:{Guid.NewGuid():N}";
        var today = LondonCalendar.DateAt(_timeProvider.GetUtcNow());
        GuideValuationQuote quote;
        try
        {
            quote = await _fetch.ExecuteAsync(
                new(
                    request.CaseId,
                    caseVersion,
                    actor,
                    operationKey,
                    // System work claims no edit lease.
                    string.Empty,
                    source,
                    new DateOnly(today.Year, today.Month, 1)),
                cancellationToken);
        }
        catch (GuideValuationNotValuedException)
        {
            return AutomaticRequestOutcome.NotValued;
        }
        catch (GuideValuationProviderUnavailableException)
        {
            return AutomaticRequestOutcome.Unavailable;
        }
        catch (GuideValuationAlreadyFetchedException)
        {
            // The source has already answered this month for these vehicle facts (R900).
            return AutomaticRequestOutcome.NotNeeded;
        }

        var now = _timeProvider.GetUtcNow();
        var recordedAt = LondonCalendar.TimeAt(now);
        var card = ValuationPolicy.ValidateGuideEntry(
            actor,
            new ValuationDetails(
                source,
                DateOnly.FromDateTime(recordedAt),
                TimeOnly.FromDateTime(recordedAt),
                // A guide card carries no mileage: the Case's own is used.
                null,
                quote.RetailValue,
                quote.TradeValue,
                quote.GuideMonth));
        return await _store.RecordAutomaticAsync(request.CaseId, actor, operationKey, card, now, cancellationToken)
            ? AutomaticRequestOutcome.Recorded
            : AutomaticRequestOutcome.NotRecorded;
    }

    /// <summary>
    /// Run Experian check, asked by the system (R1286, R1291). The edit lease
    /// is claimed before the request is marked asked, so a lease claimed by
    /// someone else first leaves the request waiting and unasked; once marked,
    /// Experian is asked once and the lease is given back whatever it answered.
    /// </summary>
    private async Task<AutomaticRequestOutcome> CheckHistoryAsync(
        AutomaticRequest request,
        CaseDataProjection data,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var actor = AutomaticRequestPolicy.Actor(request.Kind);
        var operationKey = $"automatic-vehicle-history-check:{Guid.NewGuid():N}";
        OneCommandEditClaim claim;
        try
        {
            claim = await _editAuthority.ClaimAsync(
                request.CaseId,
                isTriageCase: false,
                data.Version,
                actor,
                OneCommandEditAuthority.DeriveOperationKey(operationKey, ":lease"),
                cancellationToken);
        }
        catch (Exception exception) when (exception is CaseEditLeaseConflictException or CaseVersionConflictException)
        {
            // Someone took the lease, or the Case moved, since it was read.
            return await DeferAsync(request, nowUtc, cancellationToken);
        }

        try
        {
            if (!await _store.BeginAutomaticAsync(request.WorkItemId, nowUtc, cancellationToken))
            {
                return AutomaticRequestOutcome.NotRecorded;
            }

            var outcome = await RunHistoryCheckAsync(request.CaseId, data, claim, actor, operationKey, cancellationToken);
            await _store.CompleteAutomaticAsync(request.WorkItemId, outcome, _timeProvider.GetUtcNow(), cancellationToken);
            return outcome;
        }
        finally
        {
            await ReleaseAsync(claim, actor);
        }
    }

    private async Task<AutomaticRequestOutcome> RunHistoryCheckAsync(
        Guid caseId,
        CaseDataProjection data,
        OneCommandEditClaim claim,
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _historyCheck.ExecuteAsync(
                    new(
                        caseId,
                        claim.Version,
                        actor,
                        operationKey,
                        claim.Token,
                        RunVehicleHistoryCheck.AcceptedRegistration(data)!),
                    cancellationToken) == VehicleHistoryCheckOutcome.NotFound
                ? AutomaticRequestOutcome.NotValued
                : AutomaticRequestOutcome.Recorded;
        }
        catch (VehicleHistoryCheckUnavailableException)
        {
            return AutomaticRequestOutcome.Unavailable;
        }
        catch (Exception exception) when (exception is CaseEditLeaseExpiredException
            or CaseEditLeaseConflictException
            or CaseVersionConflictException
            or CaseTerminalMutationException
            or CaseArchivedException
            or AcceptedVehicleRegistrationConflictException)
        {
            // The save refused: staff took the lease over, or the Case closed
            // or changed its registration, while Experian answered.
            return AutomaticRequestOutcome.NotRecorded;
        }
    }

    /// <summary>
    /// Gives the lease back. The Automation Actor's write keeps its lease, so
    /// there is one to give; a lease staff took over meanwhile is theirs, and
    /// one that cannot be given back lapses by itself (FRD-14).
    /// </summary>
    private async Task ReleaseAsync(OneCommandEditClaim claim, ActionActor actor)
    {
        try
        {
            await _editAuthority.ReleaseAsync(
                claim,
                actor,
                OneCommandEditAuthority.DeriveOperationKey(claim.OperationKey, ":release"),
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException)
        {
            // The lease is no longer this request's to give back.
        }
    }

    private async Task<AutomaticRequestOutcome> DeferAsync(
        AutomaticRequest request,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        await _store.DeferAutomaticAsync(request.WorkItemId, nowUtc + AutomaticRequestPolicy.Wait, cancellationToken);
        return AutomaticRequestOutcome.Waiting;
    }

    private async Task<AutomaticRequestOutcome> SettleAsync(
        AutomaticRequest request,
        AutomaticRequestOutcome outcome,
        CancellationToken cancellationToken)
    {
        await _store.CompleteAutomaticAsync(request.WorkItemId, outcome, _timeProvider.GetUtcNow(), cancellationToken);
        return outcome;
    }
}
