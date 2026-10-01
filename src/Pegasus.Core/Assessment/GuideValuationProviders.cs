using System.Globalization;
using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
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
    long Mileage)
{
    /// <summary>
    /// The provider's own report of this valuation, filed on the Case after
    /// the figures have been answered; none when the source offers no report.
    /// </summary>
    public IGuideValuationReport? Report { get; init; }
}

/// <summary>
/// A provider's handle on its own report of one valuation. <see cref="Identity"/>
/// is the provider's record of that valuation, so one report is filed once
/// however often its filing is retried.
/// </summary>
public interface IGuideValuationReport
{
    string Identity { get; }

    /// <summary>The report as the provider publishes it: a PDF.</summary>
    Task<byte[]> FetchPdfAsync(CancellationToken cancellationToken);
}

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

/// <summary>
/// The source has no working provider on this host: none is connected, or the
/// connected one could not answer. The card says the same either way, and the
/// provider logs why it could not.
/// </summary>
public sealed class GuideValuationProviderUnavailableException(
    ValuationSource source,
    Exception? innerException = null)
    : InvalidOperationException($"No valuation provider could answer for {source}.", innerException);

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

    /// <summary>
    /// Whether the source has a connected provider, so the Case can say so
    /// before Get valuation is pressed rather than after (operator, 28
    /// September 2026).
    /// </summary>
    bool IsConnected(ValuationSource source);
}

public sealed class FetchGuideValuation(
    IEnumerable<IGuideValuationProvider> providers,
    ICaseDataQueries caseData,
    IScheduleGuideValuationReport reports) : IFetchGuideValuation
{
    private readonly IReadOnlyList<IGuideValuationProvider> _providers =
        [.. providers ?? throw new ArgumentNullException(nameof(providers))];
    private readonly ICaseDataQueries _caseData =
        caseData ?? throw new ArgumentNullException(nameof(caseData));
    private readonly IScheduleGuideValuationReport _reports =
        reports ?? throw new ArgumentNullException(nameof(reports));

    public bool IsConnected(ValuationSource source) =>
        ValuationSources.IsGuide(source) && _providers.Any(candidate => candidate.Source == source);

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

        // Every valuation's own report is filed on the Case as its evidence
        // (operator, 1 October 2026). The figures are answered first; the
        // report follows on its own.
        if (quote.Report is { } report)
        {
            await _reports.ScheduleAsync(
                new(request.Actor, request.CaseId, request.Source, registration, quote.GuideMonth, report),
                cancellationToken);
        }

        return quote;
    }

    /// <summary>The accepted value of a Case field: confirmed, else the intake fact; never a suggestion.</summary>
    private static CaseDataValue<T>? Accepted<T>(CaseField<T>? field) where T : notnull =>
        field?.Confirmed ?? field?.Fact;
}

/// <summary>One valuation's report, to be filed on the Case it was fetched for.</summary>
public sealed record FileGuideValuationReportRequest(
    ActionActor Actor,
    Guid CaseId,
    ValuationSource Source,
    string Registration,
    DateOnly GuideMonth,
    IGuideValuationReport Report);

/// <summary>
/// Files a valuation's report after Get valuation has answered with its
/// figures, so the report never holds the figures back. A host runs the filing
/// in its own background work.
/// </summary>
public interface IScheduleGuideValuationReport
{
    Task ScheduleAsync(FileGuideValuationReportRequest request, CancellationToken cancellationToken);
}

public interface IFileGuideValuationReport
{
    Task<CaseArtifactCustodyResult> ExecuteAsync(
        FileGuideValuationReportRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Files a guide valuation's report on its Case as evidence of the figures:
/// the provider's PDF, named for the source, registration and month. It is
/// retained as a Case artifact — not added as a staff document — so filing it
/// neither moves the Case's version nor touches the edit session the Engineer
/// may still have open with the figures unsaved.
/// </summary>
public sealed class FileGuideValuationReport(ICaseArtifactCustody custody) : IFileGuideValuationReport
{
    public const string MediaType = "application/pdf";

    private readonly ICaseArtifactCustody _custody =
        custody ?? throw new ArgumentNullException(nameof(custody));

    public async Task<CaseArtifactCustodyResult> ExecuteAsync(
        FileGuideValuationReportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        ArgumentNullException.ThrowIfNull(request.Report);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Registration) || string.IsNullOrWhiteSpace(request.Report.Identity))
        {
            throw new ArgumentException("A registration and the provider's report identity are required.", nameof(request));
        }

        var content = await request.Report.FetchPdfAsync(cancellationToken);
        if (!IsPdf(content))
        {
            throw new InvalidOperationException("The provider's valuation report is not a PDF.");
        }

        // The key is the provider's record of the valuation, never the Case
        // form's own operation key: that key belongs to the Save still to come.
        var key = string.Create(
            CultureInfo.InvariantCulture,
            $"guide-valuation-report:{request.Source}:{request.Report.Identity}");
        await using var stream = new MemoryStream(content, writable: false);
        return await _custody.RetainAsync(
            new CaseArtifactCustodyRequest(
                request.Actor,
                request.CaseId,
                IntakeReceiptId: null,
                OccurrenceIdentity: key,
                OperationKey: key,
                FileName: FileName(request.Source, request.Registration, request.GuideMonth),
                MediaType,
                content.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(content)),
                stream,
                SemanticRole: DocumentSemanticRole.Other,
                Source: DocumentSource.Generated),
            cancellationToken);
    }

    /// <summary>"Glass's valuation AB12CDE 2026-10.pdf".</summary>
    public static string FileName(ValuationSource source, string registration, DateOnly guideMonth) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ValuationPolicy.SourceName(source)} valuation {registration.Trim()} {guideMonth:yyyy-MM}.pdf");

    private static bool IsPdf(byte[]? content) =>
        content is { Length: > 5 } && "%PDF-"u8.SequenceEqual(content.AsSpan(0, 5));
}
