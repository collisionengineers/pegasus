using Pegasus.Core.Identity;

namespace Pegasus.Core.AiWork;

public sealed record AdministrationAiJobPage(
    IReadOnlyList<AiJobRecord> Jobs,
    AiJobCounts Counts,
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
        CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);

        var rowsRead = queries.ListAsync((page - 1) * PageSize, PageSize + 1, cancellationToken);
        var countsRead = jobs.GetCountsAsync(cancellationToken);
        var enabledRead = sendToAi.IsEnabledAsync(cancellationToken);
        await Task.WhenAll(rowsRead, countsRead, enabledRead);
        var rows = await rowsRead;
        return new(rows.Take(PageSize).ToArray(), await countsRead, await enabledRead, rows.Count > PageSize);
    }
}
