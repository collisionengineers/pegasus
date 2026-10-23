using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Background;

/// <summary>
/// Runs a Glass's VIN fill after Get valuation has answered its figures: the
/// fill is a Case write under the workflow lock that the figures do not wait
/// on. It runs on this host's provider work queue, in the order it was pressed
/// for the Case. A full queue fills the VIN in the press itself rather than
/// dropping it, and does not wait on the Case's queue gate, because the fill
/// takes the workflow lock itself. The fill logs and absorbs its own failures,
/// and a fill the queue cancels while it runs is logged the same way, so the
/// figures stand as they were answered. Queued work is not started when the
/// host stops, and the queue logs work cancelled before it started, at its time
/// cap (which counts the wait behind earlier work for the Case) or by shutdown.
/// </summary>
public sealed partial class GlassVinFillScheduler(
    ProviderWorkQueue queue,
    IServiceProvider services,
    ILogger<GlassVinFillScheduler> logger) : IScheduleGlassVinFill
{
    public const string Kind = "glass-vin-fill";

    public async Task ScheduleAsync(
        ActionActor actor,
        Guid caseId,
        CaseWorkSelector work,
        string vin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        using var reservation = queue.Reserve(caseId);
        var fill = new ProviderWork(caseId, Kind, async (scope, token) =>
        {
            try
            {
                await scope.GetRequiredService<IFillGlassVin>().FillAsync(actor, caseId, work, vin, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                LogNotFilled(logger, caseId, nameof(OperationCanceledException));
            }
        });
        if (reservation.Admit(fill) == ProviderWorkAdmission.Full)
        {
            await using var scope = services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IFillGlassVin>()
                .FillAsync(actor, caseId, work, vin, cancellationToken);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's VIN for case {CaseId} was not recorded: {Reason}")]
    private static partial void LogNotFilled(ILogger logger, Guid caseId, string reason);
}
