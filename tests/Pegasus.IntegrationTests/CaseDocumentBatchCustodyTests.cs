using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The batch download's Core port, <see cref="IOpenCaseDocumentBatch"/>, over
/// the EF custody store (#1504): the set in request order from one read, the
/// bytes read with no SQL, a <c>document_downloaded</c> row for each item the
/// caller says it handed back and none for the others, all in one save, and
/// SQL that does not grow with the number of items (R25).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseDocumentBatchCustodyTests
{
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public async Task TheBatchKeepsRequestOrderAndRecordsOnlyTheIndexesItIsGiven()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        var added = await AddDocumentsAsync(factory, caseId, 3);
        var order = new[] { 2, 0, 1 };
        const string operationKey = "batch-custody-order";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var batch = await scope.ServiceProvider.GetRequiredService<IOpenCaseDocumentBatch>().ExecuteAsync(
                new(caseId, [.. order.Select(at => added[at].Item)], Engineer, operationKey),
                CancellationToken.None);

            Assert.NotNull(batch);
            Assert.Equal(
                order.Select(at => added[at].Item.VersionId),
                batch.Metadata.Select(metadata => metadata.VersionId));
            Assert.Equal(
                order.Select(at => added[at].Item.OccurrenceId),
                batch.Metadata.Select(metadata => metadata.OccurrenceId));
            for (var index = 0; index < order.Length; index++)
            {
                Assert.Equal(
                    added[order[index]].Content,
                    (await batch.ReadContentAsync(index, CancellationToken.None)).ToArray());
            }

            await batch.RecordDownloadedAsync([0, 2], CancellationToken.None);
        }

        await using var context = await factory.Services
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var rows = await context.ActionHistory.AsNoTracking()
            .Where(row => row.EventKind == "document_downloaded")
            .OrderBy(row => row.CorrelationId)
            .Select(row => new { row.AggregateType, row.AggregateId, row.CorrelationId, row.ActorSubjectId })
            .ToArrayAsync();
        Assert.Equal(
            [
                $"case_document {added[order[0]].Item.VersionId:D} {operationKey}:0 {Engineer.SubjectId}",
                $"case_document {added[order[2]].Item.VersionId:D} {operationKey}:2 {Engineer.SubjectId}"
            ],
            rows.Select(row => $"{row.AggregateType} {row.AggregateId} {row.CorrelationId} {row.ActorSubjectId}").ToArray());
    }

    /// <summary>
    /// One command reads the whole set with its Case identity, the reads of
    /// the bytes cost none, and one command writes every row: three items and
    /// six send the same commands, and that is two.
    /// </summary>
    [Fact]
    public async Task BatchSqlCommandsDoNotGrowWithTheItemCount()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        var added = await AddDocumentsAsync(factory, caseId, 6);

        var withThree = await OpenReadAndRecordAsync(factory, counter, caseId, added[..3]);
        var withThreeDescription = counter.Describe();
        var withSix = await OpenReadAndRecordAsync(factory, counter, caseId, added);

        Assert.True(withThree > 0, "The interceptor observed no commands at all.");
        Assert.True(
            withThree == withSix,
            $"The batch grew with its items: {withThree} commands for three and {withSix} for six."
            + Environment.NewLine + withThreeDescription);
        Assert.True(
            withThree <= 2,
            $"The batch sent {withThree} commands, more than one read and one save."
            + Environment.NewLine + withThreeDescription);
    }

    private static async Task<int> OpenReadAndRecordAsync(
        IntakeWebApplicationFactory factory,
        SqlStatementCounter counter,
        Guid caseId,
        (CaseDocumentBatchItem Item, byte[] Content)[] documents)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var open = scope.ServiceProvider.GetRequiredService<IOpenCaseDocumentBatch>();
        counter.Reset();
        var batch = await open.ExecuteAsync(
            new(
                caseId,
                [.. documents.Select(document => document.Item)],
                Engineer,
                $"batch-custody-count:{Guid.NewGuid():N}"),
            CancellationToken.None);
        Assert.NotNull(batch);
        for (var index = 0; index < documents.Length; index++)
        {
            Assert.Equal(
                documents[index].Content,
                (await batch.ReadContentAsync(index, CancellationToken.None)).ToArray());
        }

        await batch.RecordDownloadedAsync([.. Enumerable.Range(0, documents.Length)], CancellationToken.None);
        return counter.Count;
    }

    private static async Task<(CaseDocumentBatchItem Item, byte[] Content)[]> AddDocumentsAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId,
        int count)
    {
        var added = new (CaseDocumentBatchItem Item, byte[] Content)[count];
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        for (var index = 0; index < count; index++)
        {
            var workflow = await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(caseId, CancellationToken.None)
                ?? throw new InvalidOperationException("The seeded Case has no workflow.");
            var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
                new(caseId, workflow.Version, Engineer, $"batch-custody-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
            var content = Encoding.UTF8.GetBytes($"batch custody document {index}");
            var result = await services.GetRequiredService<IAddCaseDocument>().ExecuteAsync(
                new(
                    caseId,
                    $"document-{index}.txt",
                    "text/plain",
                    content,
                    DocumentSemanticRole.Other,
                    DocumentSource.StaffUpload,
                    $"staff:{Guid.NewGuid():N}",
                    Engineer,
                    $"batch-custody-add:{Guid.NewGuid():N}",
                    lease.Version,
                    lease.Token),
                CancellationToken.None);
            added[index] = (new(result.Occurrence.Id, result.Version.Id), content);
        }

        return added;
    }
}
