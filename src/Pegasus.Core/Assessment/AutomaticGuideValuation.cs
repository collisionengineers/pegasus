using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The Administrator's switch for the automatic Glass's valuation (R546, 29
/// October 2026), held beside the Send to AI switch and in the same shape
/// (<see cref="Pegasus.Core.AiWork.ISendToAiControl"/>). Case creation reads it: a Case
/// created while it is off has no automatic request, then or later.
/// </summary>
public interface IAutomaticGuideValuationControl
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken);

    Task<bool> SetEnabledAsync(
        bool enabled,
        ActionActor actor,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken);
}

/// <summary>One Case's automatic valuation request, recorded when the Case was created.</summary>
public sealed record AutomaticGuideValuationRequest(Guid WorkItemId, Guid CaseId);

/// <summary>What the Case is as its automatic valuation request finds it.</summary>
public sealed record AutomaticGuideValuationTarget(
    bool IsOpen,
    bool HoldsSourceCard,
    DateTimeOffset? EditLeaseExpiresAtUtc);

/// <summary>
/// The automatic valuation requests and the Case they value. A request is
/// recorded once, by Case creation, so a Case has one automatic request ever
/// (R545); this store reads, waits, starts and settles it, and records the card
/// as system work: the Case version advances and the edit lease is left as it
/// is (FRD-14).
/// </summary>
public interface IAutomaticGuideValuationStore
{
    /// <summary>The waiting requests due by <paramref name="nowUtc"/>, oldest first.</summary>
    Task<IReadOnlyList<AutomaticGuideValuationRequest>> ListDueAutomaticAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken);

    /// <summary>The Case and whether its current work holds a card of the source; null when no Case has a workflow.</summary>
    Task<AutomaticGuideValuationTarget?> ReadAutomaticTargetAsync(
        Guid caseId,
        ValuationSource source,
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
        AutomaticGuideValuationOutcome outcome,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the card on the current work in one transaction while the Case
    /// is open and the work holds no card of the source; false, with nothing
    /// written, otherwise.
    /// </summary>
    Task<bool> RecordAutomaticAsync(
        Guid caseId,
        ActionActor actor,
        string operationKey,
        ValuationDetails card,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>What became of one automatic valuation request on one pass.</summary>
public enum AutomaticGuideValuationOutcome
{
    /// <summary>The card was recorded.</summary>
    Recorded,

    /// <summary>The request waits: the Case is being edited, or its registration or mileage is not yet accepted.</summary>
    Waiting,

    /// <summary>The Administrator's switch is off, so the request is settled unasked.</summary>
    SwitchedOff,

    /// <summary>The Case is closed or archived, or its current work already holds a Glass's card.</summary>
    NotNeeded,

    /// <summary>The provider answered that it cannot value the vehicle.</summary>
    NotValued,

    /// <summary>The provider is not connected on this host or could not answer.</summary>
    Unavailable,

    /// <summary>The provider answered, but the Case closed or took a Glass's card meanwhile.</summary>
    NotRecorded,
}

/// <summary>
/// The automatic Glass's valuation (R465, 28 October 2026; R545 and R546, 29
/// October 2026): one request per Case, recorded at its creation while the
/// Administrator's switch is on, beside the automatic vehicle lookup.
/// </summary>
public static class AutomaticGuideValuationPolicy
{
    /// <summary>The source the request asks (R465: the Case's Glass's valuation).</summary>
    public const ValuationSource Source = ValuationSource.Glasses;

    /// <summary>The Automation recorder the request is made and recorded as.</summary>
    public const string ActorId = "glass-valuation";

    public static readonly ActionActor Actor = ActionActor.Automation(ActorId);

    /// <summary>How long a waiting request waits before it is read again.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMinutes(1);

    /// <summary>A closed Case, or a work that already holds a Glass's card, needs no automatic request.</summary>
    public static bool IsNeeded(AutomaticGuideValuationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.IsOpen && !target.HoldsSourceCard;
    }

    /// <summary>
    /// System work yields to a held edit lease (FRD-14), and Glass's values an
    /// accepted registration at an accepted mileage, so the request waits for
    /// both: as the vehicle lookup waits for a registration, and so a mileage
    /// the vehicle lookup fills is the one valued.
    /// </summary>
    public static bool Waits(AutomaticGuideValuationTarget target, bool hasVehicleFacts, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(target);
        return !hasVehicleFacts || CaseEditAuthority.IsHeld(target.EditLeaseExpiresAtUtc, nowUtc);
    }
}

/// <summary>
/// Runs a Case's one automatic Glass's valuation request. It is Get valuation
/// run by the system: the same <see cref="IFetchGuideValuation"/> for the
/// current work and the current month, so the report is filed and an empty VIN
/// filled as they are for staff, and the figures are recorded as the card a
/// Save records. Glass's is asked at most once per request; whatever it
/// answers settles the request, and any later valuation is staff-started.
/// </summary>
public sealed class RunAutomaticGuideValuation(
    IFetchGuideValuation fetch,
    ICaseDataQueries caseData,
    IAutomaticGuideValuationStore store,
    IAutomaticGuideValuationControl control,
    TimeProvider timeProvider)
{
    /// <summary>How many requests one pass reads.</summary>
    public const int BatchSize = 10;

    private readonly IFetchGuideValuation _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    private readonly ICaseDataQueries _caseData = caseData ?? throw new ArgumentNullException(nameof(caseData));
    private readonly IAutomaticGuideValuationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IAutomaticGuideValuationControl _control =
        control ?? throw new ArgumentNullException(nameof(control));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<IReadOnlyList<AutomaticGuideValuationRequest>> ListDueAsync(CancellationToken cancellationToken) =>
        _store.ListDueAutomaticAsync(_timeProvider.GetUtcNow(), BatchSize, cancellationToken);

    public async Task<AutomaticGuideValuationOutcome> ExecuteAsync(
        AutomaticGuideValuationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        const ValuationSource source = AutomaticGuideValuationPolicy.Source;

        // Turning the switch off settles what is still waiting: a request is
        // made only while the switch is on (R546).
        if (!await _control.IsEnabledAsync(cancellationToken))
        {
            return await SettleAsync(request, AutomaticGuideValuationOutcome.SwitchedOff, cancellationToken);
        }

        if (!_fetch.IsConnected(source))
        {
            return await SettleAsync(request, AutomaticGuideValuationOutcome.Unavailable, cancellationToken);
        }

        var target = await _store.ReadAutomaticTargetAsync(request.CaseId, source, cancellationToken);
        if (target is null || !AutomaticGuideValuationPolicy.IsNeeded(target))
        {
            return await SettleAsync(request, AutomaticGuideValuationOutcome.NotNeeded, cancellationToken);
        }

        var data = await _caseData.GetAsync(request.CaseId, CaseWorkSelector.Current, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        if (data is null
            || AutomaticGuideValuationPolicy.Waits(target, FetchGuideValuation.HasVehicleFacts(data), now))
        {
            await _store.DeferAutomaticAsync(request.WorkItemId, now + AutomaticGuideValuationPolicy.Wait, cancellationToken);
            return AutomaticGuideValuationOutcome.Waiting;
        }

        if (!await _store.BeginAutomaticAsync(request.WorkItemId, now, cancellationToken))
        {
            return AutomaticGuideValuationOutcome.NotRecorded;
        }

        var outcome = await AskAsync(request.CaseId, data.Version, cancellationToken);
        await _store.CompleteAutomaticAsync(request.WorkItemId, outcome, _timeProvider.GetUtcNow(), cancellationToken);
        return outcome;
    }

    private async Task<AutomaticGuideValuationOutcome> AskAsync(
        Guid caseId,
        long caseVersion,
        CancellationToken cancellationToken)
    {
        var actor = AutomaticGuideValuationPolicy.Actor;
        var operationKey = $"automatic-guide-valuation:{Guid.NewGuid():N}";
        var today = LondonCalendar.DateAt(_timeProvider.GetUtcNow());
        GuideValuationQuote quote;
        try
        {
            quote = await _fetch.ExecuteAsync(
                new(
                    caseId,
                    caseVersion,
                    actor,
                    operationKey,
                    // System work claims no edit lease.
                    string.Empty,
                    AutomaticGuideValuationPolicy.Source,
                    new DateOnly(today.Year, today.Month, 1)),
                cancellationToken);
        }
        catch (GuideValuationNotValuedException)
        {
            return AutomaticGuideValuationOutcome.NotValued;
        }
        catch (GuideValuationProviderUnavailableException)
        {
            return AutomaticGuideValuationOutcome.Unavailable;
        }

        var now = _timeProvider.GetUtcNow();
        var recordedAt = LondonCalendar.TimeAt(now);
        var card = ValuationPolicy.ValidateGuideEntry(
            actor,
            new ValuationDetails(
                AutomaticGuideValuationPolicy.Source,
                DateOnly.FromDateTime(recordedAt),
                TimeOnly.FromDateTime(recordedAt),
                // A guide card carries no mileage: the Case's own is used.
                null,
                quote.RetailValue,
                quote.TradeValue,
                quote.GuideMonth));
        return await _store.RecordAutomaticAsync(caseId, actor, operationKey, card, now, cancellationToken)
            ? AutomaticGuideValuationOutcome.Recorded
            : AutomaticGuideValuationOutcome.NotRecorded;
    }

    private async Task<AutomaticGuideValuationOutcome> SettleAsync(
        AutomaticGuideValuationRequest request,
        AutomaticGuideValuationOutcome outcome,
        CancellationToken cancellationToken)
    {
        await _store.CompleteAutomaticAsync(request.WorkItemId, outcome, _timeProvider.GetUtcNow(), cancellationToken);
        return outcome;
    }
}
