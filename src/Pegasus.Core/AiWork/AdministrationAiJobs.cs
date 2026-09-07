using Pegasus.Core.Identity;

namespace Pegasus.Core.AiWork;

public sealed record AdministrationAiJobPage(
    IReadOnlyList<AiJobRecord> Jobs,
    AiJobCounts Counts,
    bool TransportComposed,
    bool SendToAiSwitchEnabled,
    bool HasMore);

public interface IAdministrationAiJobQueries
{
    Task<IReadOnlyList<AiJobRecord>> ListAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken);
}

public sealed class GetAdministrationAiJobs(
    IAdministrationAiJobQueries queries,
    IAiJobQueries jobs,
    ISendToAiControl sendToAi)
{
    public const int PageSize = 50;

    public async Task<AdministrationAiJobPage> ExecuteAsync(
        ActionActor actor,
        int page,
        bool transportComposed,
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

        var rows = await queries.ListAsync((page - 1) * PageSize, PageSize + 1, cancellationToken);
        var counts = await jobs.GetCountsAsync(cancellationToken);
        var enabled = await sendToAi.IsEnabledAsync(cancellationToken);
        return new(rows.Take(PageSize).ToArray(), counts, transportComposed, enabled, rows.Count > PageSize);
    }
}
