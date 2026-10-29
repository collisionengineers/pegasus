using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Asks the host to request a Case's Glass's valuation automatically after the
/// command that brought the Case to With Engineer has answered. The host runs
/// <see cref="RunAutomaticGuideValuation"/> in its own background work, so the
/// command never waits on Glass's.
/// </summary>
public interface IScheduleAutomaticGuideValuation
{
    Task ScheduleAsync(Guid caseId, CancellationToken cancellationToken);
}

/// <summary>What the Case is as an automatic valuation finds it.</summary>
public sealed record AutomaticGuideValuationTarget(
    CaseLifecycleState State,
    bool IsArchived,
    DateTimeOffset? EditLeaseExpiresAtUtc,
    bool HoldsSourceCard);

/// <summary>
/// The Case's current work as the automatic valuation reads it and records it.
/// The card is system work: it advances the Case version and leaves the edit
/// lease as it is (FRD-14).
/// </summary>
public interface IAutomaticGuideValuationStore
{
    /// <summary>The Case and whether its current work holds a card of the source; null when no Case has a workflow.</summary>
    Task<AutomaticGuideValuationTarget?> ReadAutomaticTargetAsync(
        Guid caseId,
        ValuationSource source,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records the card on the current work in one transaction when
    /// <see cref="AutomaticGuideValuationPolicy.Accepts"/> still holds there;
    /// false, with nothing written, when it no longer does.
    /// </summary>
    Task<bool> RecordAutomaticAsync(
        Guid caseId,
        ActionActor actor,
        string operationKey,
        ValuationDetails card,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>What became of one automatic valuation request.</summary>
public enum AutomaticGuideValuationOutcome
{
    Recorded,

    /// <summary>The source has no connected provider on this host.</summary>
    NotConnected,

    /// <summary>The Case is not With Engineer and open, its current work already holds the card, or an edit lease is held.</summary>
    NotAccepted,

    /// <summary>The current work has no accepted registration or mileage.</summary>
    MissingVehicleFacts,

    /// <summary>The provider answered that it cannot value the vehicle.</summary>
    NotValued,

    /// <summary>The provider could not answer.</summary>
    Unavailable,
}

/// <summary>
/// When a Case takes an automatic valuation (R465, 28 October 2026): it is
/// With Engineer and open, its current work holds no card of the source, and
/// nobody holds its edit lease, because system work yields to a held lease.
/// </summary>
public static class AutomaticGuideValuationPolicy
{
    /// <summary>The source an automatic valuation asks (R465: the Case's Glass's valuation).</summary>
    public const ValuationSource Source = ValuationSource.Glasses;

    /// <summary>The Automation recorder an automatic valuation is made and recorded as.</summary>
    public const string ActorId = "glass-valuation";

    public static readonly ActionActor Actor = ActionActor.Automation(ActorId);

    public static bool Accepts(AutomaticGuideValuationTarget target, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.State == CaseLifecycleState.ReportPreparation
            && !target.IsArchived
            && !target.HoldsSourceCard
            && !CaseEditAuthority.IsHeld(target.EditLeaseExpiresAtUtc, nowUtc);
    }
}

/// <summary>
/// The Case's Glass's valuation, requested automatically when the Case reaches
/// With Engineer (R465, 28 October 2026). It is Get valuation run by the
/// system: the same <see cref="IFetchGuideValuation"/> for the current work and
/// the current month, so the report is filed and an empty VIN filled as they
/// are for staff, and the figures are recorded as the card a Save records. It
/// asks only while <see cref="AutomaticGuideValuationPolicy.Accepts"/> holds,
/// and asks again before recording. A vehicle Glass's cannot value, a
/// provider that cannot answer, or missing vehicle facts record nothing, and
/// Get valuation stays as it is.
/// </summary>
public sealed class RunAutomaticGuideValuation(
    IFetchGuideValuation fetch,
    ICaseDataQueries caseData,
    IAutomaticGuideValuationStore store,
    TimeProvider timeProvider)
{
    private readonly IFetchGuideValuation _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
    private readonly ICaseDataQueries _caseData = caseData ?? throw new ArgumentNullException(nameof(caseData));
    private readonly IAutomaticGuideValuationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<AutomaticGuideValuationOutcome> ExecuteAsync(Guid caseId, CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        const ValuationSource source = AutomaticGuideValuationPolicy.Source;
        if (!_fetch.IsConnected(source))
        {
            return AutomaticGuideValuationOutcome.NotConnected;
        }

        // Checked before Glass's is asked, so no lookup is spent on a Case
        // that would refuse the card.
        var target = await _store.ReadAutomaticTargetAsync(caseId, source, cancellationToken);
        if (target is null || !AutomaticGuideValuationPolicy.Accepts(target, _timeProvider.GetUtcNow()))
        {
            return AutomaticGuideValuationOutcome.NotAccepted;
        }

        var data = await _caseData.GetAsync(caseId, CaseWorkSelector.Current, cancellationToken);
        if (data is null || !FetchGuideValuation.HasVehicleFacts(data))
        {
            return AutomaticGuideValuationOutcome.MissingVehicleFacts;
        }

        var actor = AutomaticGuideValuationPolicy.Actor;
        var operationKey = $"automatic-guide-valuation:{Guid.NewGuid():N}";
        var today = LondonCalendar.DateAt(_timeProvider.GetUtcNow());
        GuideValuationQuote quote;
        try
        {
            quote = await _fetch.ExecuteAsync(
                new(
                    caseId,
                    data.Version,
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
                source,
                DateOnly.FromDateTime(recordedAt),
                TimeOnly.FromDateTime(recordedAt),
                // A guide card carries no mileage: the Case's own is used.
                null,
                quote.RetailValue,
                quote.TradeValue,
                quote.GuideMonth));
        return await _store.RecordAutomaticAsync(caseId, actor, operationKey, card, now, cancellationToken)
            ? AutomaticGuideValuationOutcome.Recorded
            : AutomaticGuideValuationOutcome.NotAccepted;
    }
}
