using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// What a guide provider is asked for: the Case's accepted registration and
/// mileage, as recorded, and the guide month the operator chose. A provider
/// converts the mileage unit itself; the Case does not restate it.
/// </summary>
public sealed record GuideValuationRequest(
    ActionActor Actor,
    Guid CaseId,
    string Registration,
    long Mileage,
    string? MileageUnit,
    DateOnly GuideMonth);

/// <summary>A provider's answer: the guide figures and the month and mileage they stand for.</summary>
public sealed record GuideValuationQuote(
    decimal RetailValue,
    decimal TradeValue,
    DateOnly GuideMonth,
    long Mileage);

/// <summary>
/// One guide source's live provider. There is at most one per
/// <see cref="ValuationSource"/>; a source with none registered is not
/// connected, and the Get valuation button says so rather than writing a
/// card.
/// </summary>
public interface IGuideValuationProvider
{
    ValuationSource Source { get; }

    Task<GuideValuationQuote> GetAsync(
        GuideValuationRequest request,
        CancellationToken cancellationToken);
}

/// <summary>The source has no connected provider on this host.</summary>
public sealed class GuideValuationProviderUnavailableException(ValuationSource source)
    : InvalidOperationException($"No valuation provider is connected for {source}.");

public sealed record FetchGuideValuationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    ValuationSource Source,
    DateOnly GuideMonth);

/// <summary>
/// Get valuation for one guide source: asks that source's provider for the
/// Case's registration and mileage in the chosen month and answers with the
/// figures. The figures fill the source's card, where the Engineer reads,
/// corrects and saves them; the save is the one route to a record, so a
/// fetched figure and a typed one are the same record.
/// </summary>
public interface IFetchGuideValuation
{
    Task<GuideValuationQuote> ExecuteAsync(
        FetchGuideValuationRequest request,
        CancellationToken cancellationToken);
}

public sealed class FetchGuideValuation(
    IEnumerable<IGuideValuationProvider> providers,
    ICaseDataQueries caseData) : IFetchGuideValuation
{
    private readonly IReadOnlyList<IGuideValuationProvider> _providers =
        [.. providers ?? throw new ArgumentNullException(nameof(providers))];
    private readonly ICaseDataQueries _caseData =
        caseData ?? throw new ArgumentNullException(nameof(caseData));

    public async Task<GuideValuationQuote> ExecuteAsync(
        FetchGuideValuationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.OperationKey))
        {
            throw new ArgumentException("An operation key is required.", nameof(request));
        }

        // A guide is fetched; the Engineer's Value is confirmed by hand and
        // AI market research runs its own job.
        if (!ValuationPolicy.IsManuallyRecordable(request.Source)
            || request.Source == ValuationSource.EngineersValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The valuation source has no guide to fetch.");
        }

        var guideMonth = new DateOnly(request.GuideMonth.Year, request.GuideMonth.Month, 1);
        var provider = _providers.FirstOrDefault(candidate => candidate.Source == request.Source)
            ?? throw new GuideValuationProviderUnavailableException(request.Source);

        var data = await _caseData.GetAsync(request.CaseId, CaseWorkSelector.Current, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        var registration = Accepted(data.Vehicle.Registration)?.Value;
        if (string.IsNullOrWhiteSpace(registration))
        {
            throw new InvalidOperationException(
                "A confirmed vehicle registration is required before a valuation can be fetched.");
        }

        var mileage = Accepted(data.Vehicle.Mileage)?.Value
            ?? throw new InvalidOperationException(
                "A confirmed mileage is required before a valuation can be fetched.");

        var quote = await provider.GetAsync(
            new(request.Actor, request.CaseId, registration, mileage, Accepted(data.Vehicle.MileageUnit)?.Value, guideMonth),
            cancellationToken);
        ArgumentNullException.ThrowIfNull(quote);
        return quote;
    }

    /// <summary>The accepted value of a Case field: confirmed, else the intake fact; never a suggestion.</summary>
    private static CaseDataValue<T>? Accepted<T>(CaseField<T>? field) where T : notnull =>
        field?.Confirmed ?? field?.Fact;
}
