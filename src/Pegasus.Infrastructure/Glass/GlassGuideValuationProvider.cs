using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// Glass's as a connected guide source (ADR-0059): Get valuation signs in with
/// the Glass's valuation account, values the Case's registration and mileage
/// in the chosen month, and answers Retail Transacted as the card's retail and
/// Glass's Trade as its trade.
///
/// <para>
/// <b>As the portal does it.</b> Every valuation then saves the vehicle to the
/// account's stock list, as the portal does after each valuation in insurance
/// mode, and the stocked vehicle's "Values Only" report is offered to the Case
/// as the valuation's evidence (operator, 1 October 2026). A valuation whose
/// stock save failed still answers its figures; it has no report to offer.
/// </para>
///
/// <para>
/// <b>Every failure is "unavailable".</b> The card says the same approved
/// sentence whatever stopped the valuation — the account, the plate, the
/// provider or the network — and the reason goes to the host log by its
/// failure code. Configuration is read on each valuation, never when the
/// provider is built: every Case page builds it, including on a host whose
/// Glass's settings are not yet resolved.
/// </para>
/// </summary>
public sealed partial class GlassGuideValuationProvider(
    IHttpClientFactory httpClientFactory,
    Func<GlassRepairEstimateOptions> options,
    Func<GlassValuationAccount> account,
    TimeProvider timeProvider,
    ILogger<GlassGuideValuationProvider> logger) : IGuideValuationProvider
{
    public ValuationSource Source => ValuationSource.Glasses;

    public async Task<GuideValuationQuote> GetAsync(
        GuideValuationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        GlassMvaClient client;
        GlassGuideFigures figures;
        long miles;
        try
        {
            if (!CaseOdometer.TryWholeMiles(request.Mileage, request.MileageUnit, out miles))
            {
                throw new GlassMvaStageException(GlassFailure.IdentityMileage, detail: "unit=unreadable");
            }

            var signIn = account();
            client = new GlassMvaClient(
                httpClientFactory.CreateClient(GlassRepairEstimateOptions.HttpClientName),
                options(),
                new Dictionary<string, string>(StringComparer.Ordinal),
                timeProvider);
            await client.SignInAsync(signIn.Username, signIn.Password, cancellationToken);
            figures = await client.ValueAsync(request.Registration, miles, request.GuideMonth, cancellationToken);
        }
        catch (Exception exception) when (Unavailable(exception, cancellationToken))
        {
            LogUnavailable(logger, request.CaseId, request.GuideMonth, Code(exception), Detail(exception));
            throw new GuideValuationProviderUnavailableException(Source, exception);
        }

        var quote = new GuideValuationQuote(figures.Retail, figures.Trade, request.GuideMonth, request.Mileage);
        try
        {
            var vehicleId = await client.CreateVehicleAsync(request.Registration, miles, request.GuideMonth, cancellationToken);
            return quote with { Report = new GlassValuationReport(client, vehicleId) };
        }
        catch (Exception exception) when (Unavailable(exception, cancellationToken))
        {
            // The figures stand without the stock save, as the portal shows
            // them before it stocks; there is just no report to file.
            LogStockSaveFailed(logger, request.CaseId, request.GuideMonth, Code(exception), Detail(exception));
            return quote;
        }
    }

    /// <summary>
    /// Whatever the provider, the account or the network did — never the
    /// caller giving up, which is the caller's own outcome.
    /// </summary>
    private static bool Unavailable(Exception exception, CancellationToken cancellationToken) =>
        exception switch
        {
            GlassMvaStageException or HttpRequestException or TimeoutException => true,
            // An unresolved or missing setting: the reason is logged by key,
            // never shown on the card.
            InvalidOperationException => true,
            OperationCanceledException => !cancellationToken.IsCancellationRequested,
            _ => false,
        };

    private static string Code(Exception exception) => exception switch
    {
        GlassMvaStageException stage => stage.FailureCode,
        HttpRequestException or OperationCanceledException or TimeoutException => GlassFailure.TransportFailed,
        _ => "glass.valuation.configuration",
    };

    private static string? Detail(Exception exception) => exception switch
    {
        GlassMvaStageException stage => stage.Detail,
        // A configuration refusal names its key and nothing secret.
        InvalidOperationException configuration => configuration.Message,
        _ => exception.GetType().Name,
    };

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's valuation for case {CaseId} in {GuideMonth} was unavailable at {FailureCode} {Detail}")]
    private static partial void LogUnavailable(
        ILogger logger, Guid caseId, DateOnly guideMonth, string failureCode, string? detail);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's valuation for case {CaseId} in {GuideMonth} answered its figures but was not saved to the stock list at {FailureCode} {Detail}")]
    private static partial void LogStockSaveFailed(
        ILogger logger, Guid caseId, DateOnly guideMonth, string failureCode, string? detail);

    /// <summary>
    /// The stocked vehicle's report, read later over the same signed-in
    /// session. Its identity is the account's stock id: each valuation stocks
    /// a vehicle of its own, so each has one report.
    /// </summary>
    private sealed class GlassValuationReport(GlassMvaClient client, string vehicleId) : IGuideValuationReport
    {
        public string Identity => "glass-stock:" + vehicleId;

        public async Task<byte[]> FetchPdfAsync(CancellationToken cancellationToken) =>
            await client.DownloadExportAsync(
                await client.ValuationReportAsync(vehicleId, cancellationToken),
                cancellationToken);
    }
}
