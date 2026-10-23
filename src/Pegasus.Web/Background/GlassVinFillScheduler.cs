using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Background;

/// <summary>
/// Runs a Glass's VIN fill after Get valuation has answered its figures: the
/// fill is a Case write under the workflow lock that the figures do not wait
/// on. It runs on this host's provider work queue, in the order it was pressed
/// for the Case, and a full queue fills the VIN in the press itself rather than
/// dropping it. The fill logs and absorbs its own failures, so the figures
/// stand as they were answered. Queued work is not started when the host
/// stops, and is cancelled and logged by the queue at its time cap, which
/// counts the wait behind earlier work for the Case.
/// </summary>
public sealed class GlassVinFillScheduler(
    ProviderWorkQueue queue,
    IServiceProvider services) : IScheduleGlassVinFill
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
        var fill = new ProviderWork(caseId, Kind, (scope, token) =>
            scope.GetRequiredService<IFillGlassVin>().FillAsync(actor, caseId, work, vin, token));
        if (reservation.Admit(fill) == ProviderWorkAdmission.Full)
        {
            await reservation.RunHereAsync(fill, services, cancellationToken);
        }
    }
}
