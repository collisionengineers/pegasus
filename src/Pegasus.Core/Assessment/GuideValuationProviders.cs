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

    /// <summary>
    /// The VIN the source names for the valued vehicle; none when it names
    /// none. It fills the work's VIN only where the work holds none
    /// (<see cref="GlassVinFillPolicy"/>).
    /// </summary>
    public string? Vin { get; init; }
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

/// <summary>Why a source's provider answered that it cannot value the vehicle.</summary>
public enum GuideValuationNotValuedReason
{
    /// <summary>Glass's does not value a vehicle of this age (operator, 2 October 2026).</summary>
    VehicleAge,

    /// <summary>
    /// The provider holds no data for the registration: Cazana's 404
    /// (operator, 9 October 2026) and Glass's plate search without a type
    /// number (operator, 10 October 2026).
    /// </summary>
    NoVehicleData,
}

/// <summary>
/// The source's provider answered that it cannot value this vehicle. Nothing
/// is broken, so the card says why rather than "unavailable".
/// </summary>
public sealed class GuideValuationNotValuedException(
    ValuationSource source,
    GuideValuationNotValuedReason reason,
    Exception? innerException = null)
    : InvalidOperationException($"{source} cannot value this vehicle ({reason}).", innerException)
{
    public ValuationSource ValuationSource { get; } = source;

    public GuideValuationNotValuedReason Reason { get; } = reason;
}

/// <summary>
/// The source has already answered for this Case, month, registration and
/// mileage, or is answering now, so it is not asked again (R900, 10 November
/// 2026). Nothing is fetched and nothing is charged.
/// </summary>
public sealed class GuideValuationAlreadyFetchedException(ValuationSource source)
    : InvalidOperationException($"{source} has already been asked for this Case.")
{
    public ValuationSource ValuationSource { get; } = source;
}

/// <summary>
/// One paid ask of a guide provider: the Case, the source, the guide month it
/// values and the registration and mileage it values at. A source is asked
/// once per key; a corrected registration or mileage is a new key (R900).
/// </summary>
public sealed record GuideValuationFetchKey(
    Guid CaseId,
    ValuationSource Source,
    DateOnly GuideMonth,
    string Registration,
    long Mileage,
    string MileageUnit);

/// <summary>
/// The record of each guide provider ask, so a source is never asked twice
/// for one key (R900). A claim is taken before the provider is asked; it is
/// marked answered when figures come back and released when none do, so only
/// an answer with figures uses the ask. A claim that is neither (its host
/// stopped mid-ask) lapses at its expiry.
/// </summary>
public interface IGuideValuationFetchLedger
{
    /// <summary>
    /// The claim's token, or null when the key has been answered or holds a
    /// live claim.
    /// </summary>
    Task<string?> TryClaimAsync(
        GuideValuationFetchKey key,
        ActionActor actor,
        DateTimeOffset nowUtc,
        DateTimeOffset claimExpiresAtUtc,
        CancellationToken cancellationToken);

    Task MarkAnsweredAsync(
        GuideValuationFetchKey key,
        string claimToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        GuideValuationFetchKey key,
        string claimToken,
        CancellationToken cancellationToken);
}

public static class GuideValuationFetchPolicy
{
    /// <summary>
    /// Longer than any provider's sign-in, lookup and valuation together, so a
    /// claim never lapses while its ask is still running.
    /// </summary>
    public static TimeSpan ClaimDuration { get; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The key a request asks for. A later month is valued today and filled
    /// as the current month, so it is the current month's ask; the
    /// registration is compared without spacing or case.
    /// </summary>
    public static GuideValuationFetchKey Key(
        Guid caseId,
        ValuationSource source,
        DateOnly guideMonth,
        string registration,
        long mileage,
        string? mileageUnit,
        DateOnly today)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registration);
        var month = new DateOnly(guideMonth.Year, guideMonth.Month, 1);
        var current = new DateOnly(today.Year, today.Month, 1);
        return new(
            caseId,
            source,
            month > current ? current : month,
            string.Concat(registration.Where(character => !char.IsWhiteSpace(character)))
                .ToUpperInvariant(),
            mileage,
            mileageUnit?.Trim() ?? string.Empty);
    }
}

public sealed record FetchGuideValuationRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string EditLeaseToken,
    ValuationSource Source,
    DateOnly GuideMonth)
{
    /// <summary>The work whose registration and mileage are valued (operator, 2 October 2026).</summary>
    public CaseWorkSelector Work { get; init; } = CaseWorkSelector.Current;
}

/// <summary>
/// Get valuation for one guide source: asks that source's provider for the
/// Case's registration and mileage in the chosen month and answers with the
/// figures. The figures fill the source's card, where the Engineer reads,
/// corrects and saves them; the save is the one route to a record, so a
/// fetched figure and a typed one are the same record. Each source is asked
/// once per Case, guide month, registration and mileage (R900).
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
    IScheduleGuideValuationReport reports,
    IFillGlassVin vins,
    IGuideValuationFetchLedger ledger,
    TimeProvider timeProvider) : IFetchGuideValuation
{
    private readonly IReadOnlyList<IGuideValuationProvider> _providers =
        [.. providers ?? throw new ArgumentNullException(nameof(providers))];
    private readonly ICaseDataQueries _caseData =
        caseData ?? throw new ArgumentNullException(nameof(caseData));
    private readonly IScheduleGuideValuationReport _reports =
        reports ?? throw new ArgumentNullException(nameof(reports));
    private readonly IFillGlassVin _vins =
        vins ?? throw new ArgumentNullException(nameof(vins));
    private readonly IGuideValuationFetchLedger _ledger =
        ledger ?? throw new ArgumentNullException(nameof(ledger));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

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

        var data = await _caseData.GetAsync(request.CaseId, request.Work, cancellationToken)
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

        var mileageUnit = Accepted(data.Vehicle.MileageUnit)?.Value;

        // A paid ask is made once per key (R900): the claim is taken before
        // the provider is asked, so two presses at once ask it once.
        var now = _timeProvider.GetUtcNow();
        var key = GuideValuationFetchPolicy.Key(
            request.CaseId, request.Source, guideMonth, registration, mileage, mileageUnit,
            LondonCalendar.DateAt(now));
        var claim = await _ledger.TryClaimAsync(
            key, request.Actor, now, now.Add(GuideValuationFetchPolicy.ClaimDuration), cancellationToken)
            ?? throw new GuideValuationAlreadyFetchedException(request.Source);

        GuideValuationQuote quote;
        try
        {
            quote = await provider.GetAsync(
                new(request.Actor, request.CaseId, registration, mileage, mileageUnit, guideMonth),
                cancellationToken);
            ArgumentNullException.ThrowIfNull(quote);
        }
        catch
        {
            // No figures came back, so the ask is not used: the next press asks again.
            await _ledger.ReleaseAsync(key, claim, CancellationToken.None);
            throw;
        }

        // The figures are the paid answer; whatever follows, the ask is used.
        await _ledger.MarkAnsweredAsync(key, claim, _timeProvider.GetUtcNow(), CancellationToken.None);

        // Every valuation's own report is filed on the Case as its evidence
        // (operator, 1 October 2026). The figures are answered first; the
        // report follows on its own.
        if (quote.Report is { } report)
        {
            await _reports.ScheduleAsync(
                new(request.Actor, request.CaseId, request.Source, registration, quote.GuideMonth, report),
                cancellationToken);
        }

        // The valued vehicle's VIN fills the work's empty VIN (operator,
        // 7 October 2026); the figures are answered either way.
        if (quote.Vin is { } vin)
        {
            await _vins.FillAsync(request.Actor, request.CaseId, request.Work, vin, cancellationToken);
        }

        return quote;
    }

    /// <summary>Whether the work holds the accepted registration and mileage a valuation asks for.</summary>
    public static bool HasVehicleFacts(CaseDataProjection data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return !string.IsNullOrWhiteSpace(Accepted(data.Vehicle.Registration)?.Value)
            && Accepted(data.Vehicle.Mileage) is not null;
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
/// may still have open with the figures unsaved. The report is kept only when
/// its own text names the Case registration (operator, 5 October 2026): the
/// provider's shared account makes a file for another vehicle possible, and
/// such a file is refused with a flags-only reason, never the plate.
/// </summary>
public sealed class FileGuideValuationReport(ICaseArtifactCustody custody, IExtractPdfPageText pdfText)
    : IFileGuideValuationReport
{
    public const string MediaType = "application/pdf";

    /// <summary>A valuation report is one page; this bounds what is read of any other answer.</summary>
    private const int MaximumTextCharacters = 20_000;

    private readonly ICaseArtifactCustody _custody =
        custody ?? throw new ArgumentNullException(nameof(custody));

    private readonly IExtractPdfPageText _pdfText =
        pdfText ?? throw new ArgumentNullException(nameof(pdfText));

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

        // The extractor tests each page it reads and stops at the first that
        // names the registration, so the last answer is the report's answer:
        // no page is tested twice.
        var plate = GlassRepairEstimateSessionPolicy.CompactRegistration(request.Registration);
        var named = false;
        var extraction = await _pdfText.ExtractAsync(
            content,
            MaximumTextCharacters,
            cancellationToken,
            text => named = GlassRepairEstimateSessionPolicy.NamesCompactedRegistration(text, plate));
        if (extraction?.Pages.Any(page => !string.IsNullOrWhiteSpace(page.Text)) != true)
        {
            throw new InvalidOperationException(
                "The provider's valuation report names no registration (registration=absent).");
        }

        if (!named)
        {
            throw new InvalidOperationException(
                "The provider's valuation report names another registration (registration=different).");
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
                SemanticRole: DocumentSemanticRole.ValuationEvidence,
                Source: DocumentSource.Generated,
                // One file per source and registration: a later report from the
                // same provider for the same vehicle is its next Box version
                // (operator, 4 November 2026).
                SeriesIdentity: string.Create(
                    CultureInfo.InvariantCulture,
                    $"guide-valuation-report:{request.Source}:{plate}")),
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
