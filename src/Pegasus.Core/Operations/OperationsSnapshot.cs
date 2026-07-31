using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Operations;

public sealed record StagedArtifactOperationsSnapshot(
    IReadOnlyList<StagedArtifactInventoryItem> Items)
{
    public int Pending => Count(StagedArtifactDisposition.Pending);

    public int Completed => Count(StagedArtifactDisposition.Completed);

    public int Failed => Count(StagedArtifactDisposition.Failed);

    public int Unmatched => Count(StagedArtifactDisposition.Unmatched);

    public int Orphans => Count(StagedArtifactDisposition.Orphan);

    private int Count(StagedArtifactDisposition disposition) =>
        Items.Count(item => item.Disposition == disposition);
}

public sealed record OperationsSnapshot(
    DateTimeOffset AsOfUtc,
    IntakeQueueCounts Intake,
    int TriageCount,
    IReadOnlyList<CaseDueWork> DueWork,
    StagedArtifactOperationsSnapshot StagedArtifacts);

public interface IGetOperationsSnapshot
{
    Task<OperationsSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default);
}

public sealed class GetOperationsSnapshot(
    IIntakeReceiptQueries intakeQueries,
    IListTriage listTriage,
    ICaseDueWorkQueries dueWorkQueries,
    IIntakeArtifactStore artifactStore,
    TimeProvider timeProvider) : IGetOperationsSnapshot
{
    private const int MaximumDueWork = 20;
    private const int MaximumStagedArtifacts = 20;

    private readonly IIntakeReceiptQueries intakeQueries =
        intakeQueries ?? throw new ArgumentNullException(nameof(intakeQueries));
    private readonly IListTriage listTriage =
        listTriage ?? throw new ArgumentNullException(nameof(listTriage));
    private readonly ICaseDueWorkQueries dueWorkQueries =
        dueWorkQueries ?? throw new ArgumentNullException(nameof(dueWorkQueries));
    private readonly IIntakeArtifactStore artifactStore =
        artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public async Task<OperationsSnapshot> ExecuteAsync(
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        var asOfUtc = timeProvider.GetUtcNow();
        var intake = await intakeQueries.GetCountsAsync(cancellationToken);
        var triage = await listTriage.ExecuteAsync(
            new(actor, State: null, Page: 1, PageSize: 1),
            cancellationToken);
        var dueWork = await dueWorkQueries.GetDueAsync(
            asOfUtc,
            MaximumDueWork,
            cancellationToken);
        var stagedArtifacts = await artifactStore.ListStagedAsync(
            MaximumStagedArtifacts,
            cancellationToken);
        return new(
            asOfUtc,
            intake,
            triage.TotalCount,
            dueWork,
            new(stagedArtifacts));
    }
}
