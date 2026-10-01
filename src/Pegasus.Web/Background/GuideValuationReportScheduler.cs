using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;

namespace Pegasus.Web.Background;

/// <summary>
/// Files a guide valuation's report after Get valuation has answered its
/// figures (operator, 1 October 2026): the report is fetched over the
/// provider's own signed-in session, which lives in this host, so the work
/// runs on this host's provider work queue (ADR-0058, ADR-0060). Work for one
/// Case runs in the order it was pressed. A full queue files the report in the
/// press itself rather than dropping it.
/// </summary>
public sealed partial class GuideValuationReportScheduler(
    ProviderWorkQueue queue,
    IServiceProvider services) : IScheduleGuideValuationReport
{
    public const string Kind = "guide-valuation-report";

    public async Task ScheduleAsync(FileGuideValuationReportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var reservation = queue.Reserve(request.CaseId);
        var work = new ProviderWork(request.CaseId, Kind, (scope, token) => FileAsync(scope, request, token));
        if (reservation.Admit(work) == ProviderWorkAdmission.Full)
        {
            await reservation.RunHereAsync(work, services, cancellationToken);
        }
    }

    /// <summary>
    /// A report that could not be fetched or filed leaves the figures as they
    /// were answered; the reason is logged and never reaches the card.
    /// </summary>
    private static async Task FileAsync(
        IServiceProvider scope, FileGuideValuationReportRequest request, CancellationToken cancellationToken)
    {
        var logger = scope.GetRequiredService<ILogger<GuideValuationReportScheduler>>();
        try
        {
            var filed = await scope.GetRequiredService<IFileGuideValuationReport>()
                .ExecuteAsync(request, cancellationToken);
            if (filed.Disposition is CaseArtifactCustodyDisposition.Failed or CaseArtifactCustodyDisposition.Unknown)
            {
                LogNotRetained(logger, request.Source, request.CaseId, filed.Disposition, filed.FailureCode);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException
            || !cancellationToken.IsCancellationRequested)
        {
            LogNotFiled(logger, request.Source, request.CaseId, exception);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The {Source} valuation report for case {CaseId} was not filed")]
    private static partial void LogNotFiled(
        ILogger logger, ValuationSource source, Guid caseId, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The {Source} valuation report for case {CaseId} was not retained: {Disposition} {FailureCode}")]
    private static partial void LogNotRetained(
        ILogger logger, ValuationSource source, Guid caseId,
        CaseArtifactCustodyDisposition disposition, string? failureCode);
}
