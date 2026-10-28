using System.Net;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Brego's Valuation Report PDF for one valuation lookup (ADR-0084): loads the
/// derivative's factory option configuration, requests the report from Brego's
/// <c>POST /platform/api/pdf/{organizationId}</c> endpoint with
/// <c>featuredMonthNav: "high"</c> and <c>showPdfReportImages: true</c>, and
/// repairs any 14×16 broken listing image placeholders on Pages 1 and 2 with
/// Brego's studio stock images before the PDF is verified and filed in Box.
/// </summary>
internal sealed class BregoValuationReport(
    BregoGuideValuationProvider provider,
    BregoClient client,
    BregoValuationAccount account,
    BregoSession initialSession,
    string reportId,
    BregoVehicle vehicle,
    long miles) : IGuideValuationReport
{
    public string Identity { get; } = "brego-valuation:" + reportId;

    public async Task<byte[]> FetchPdfAsync(CancellationToken cancellationToken)
    {
        var session = initialSession;
        BregoOptionConfiguration options;
        byte[] pdfBytes;
        try
        {
            options = await client.GetOptionConfigurationAsync(session, vehicle, cancellationToken);
            pdfBytes = await client.CreateValuationPdfAsync(session, vehicle, miles, options, cancellationToken);
        }
        catch (BregoFailure failure) when (failure.Status == HttpStatusCode.Unauthorized)
        {
            (session, _) = await provider.GetSessionAsync(
                client,
                account,
                forceRefresh: true,
                cancellationToken,
                staleSession: session);
            options = await client.GetOptionConfigurationAsync(session, vehicle, cancellationToken);
            pdfBytes = await client.CreateValuationPdfAsync(session, vehicle, miles, options, cancellationToken);
        }

        return await BregoPdfImageRepair.RepairIfBrokenAsync(pdfBytes, vehicle, client, cancellationToken);
    }
}
