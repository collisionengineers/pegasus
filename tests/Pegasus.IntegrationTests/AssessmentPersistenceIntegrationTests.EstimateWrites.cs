using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// What the estimate writes send to SQL Server: the line replace and the
/// stale-marking are one statement each however many rows they touch, and
/// Discard reads neither the lines nor the numbered versions it deletes.
/// </summary>
public sealed partial class AssessmentPersistenceIntegrationTests
{
    // EF names a set statement "DELETE FROM [c]" and a SaveChanges row delete
    // "DELETE FROM [Table]", so the two shapes are told apart by that text.
    private static bool IsSetDelete(string text, string table) =>
        text.Contains("DELETE FROM [c]", StringComparison.Ordinal)
        && text.Contains($"[{table}] AS [c]", StringComparison.Ordinal);

    private static bool IsRowDelete(string text, string table) =>
        text.Contains($"DELETE FROM [{table}]", StringComparison.Ordinal);

    /// <summary>
    /// Editing the spec in use replaces its eight lines by one DELETE and
    /// stales the report that costed it by one UPDATE; neither is sent a row
    /// at a time, and the stale read carries no joined work row. Before, a
    /// replace sent eight row deletes and a stale-marking one row update.
    /// </summary>
    [Fact]
    public async Task ReplacingLinesAndMarkingStaleSendOneStatementEach()
    {
        var counter = new CommandCountingInterceptor();
        await using var harness = await Harness.CreateAsync(counter);
        var caseId = (await harness.AcceptAsync("write-count-edit-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        var save = new SaveEstimate(
            harness.RepairSpecifications, new EfAiJobStore(harness.Factory, harness.Clock));
        var createLease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "write-count-lease-create");
        var inUse = await save.ExecuteAsync(
            new(caseId, createLease.Version, engineer, "write-count-create", "Recorded the spec.",
                createLease.Token, null, new("Repairer", 40m, 0m, 20m),
                [.. Enumerable.Range(1, 8).Select(number => new EstimateLineInput(
                    "repair", null, $"Repair {number}", 2m, null, false, null, null, "judgement", null))],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        Assert.Equal(8, inUse.Lines.Count);
        var (currentGenerationId, _) = await SeedGenerationsAsync(harness, caseId);
        var editLease = await harness.AcquireLeaseAsync(caseId, 1, engineer, "write-count-lease-edit");

        counter.Reset();
        var edited = await save.ExecuteAsync(
            new(caseId, editLease.Version, engineer, "write-count-edit", "Repriced the spec in use.",
                editLease.Token, inUse.SpecificationId, inUse.Details with { LabourRate = 60m },
                [.. inUse.Lines.Select(RepairSpecificationScaling.ToInput)], inUse.Source,
                ExistingLineIds: [.. inUse.Lines.Select(line => (Guid?)line.Id)]),
            CancellationToken.None);

        Assert.True(
            counter.CountMatching(text => IsSetDelete(text, "CaseEstimateLines")) == 1
                && counter.CountMatching(text => IsRowDelete(text, "CaseEstimateLines")) == 0,
            "The lines were not replaced by one set DELETE." + Environment.NewLine + counter.Describe());
        Assert.True(
            counter.CountMatching(text => text.Contains("UPDATE [c]", StringComparison.Ordinal)
                && text.Contains("[CaseReportGenerations] AS [c]", StringComparison.Ordinal)) == 1
                && counter.CountMatching(text => text.Contains("UPDATE [CaseReportGenerations]", StringComparison.Ordinal)) == 0,
            "The generation was not staled by one set UPDATE." + Environment.NewLine + counter.Describe());
        Assert.Equal(0, counter.CountMatching(text =>
            text.Contains("[SnapshotJson]", StringComparison.Ordinal)
            && text.Contains("[CaseWorks]", StringComparison.Ordinal)));
        Assert.Equal(inUse.SpecificationId, edited.SpecificationId);
        Assert.Equal(8, edited.Lines.Count);
        Assert.Equal(60m, edited.Details.LabourRate);
        Assert.Equal(8, await harness.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseEstimateLines WHERE RepairSpecificationId = '{inUse.SpecificationId:D}'"));
        Assert.Equal("Stale", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentGenerationId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(harness, caseId));
    }

    /// <summary>
    /// Discard deletes the lines and the numbered versions by one statement
    /// each, in the open transaction, and reads neither: no command selects
    /// the lines or a version's JSON. A Current spec is refused before any
    /// delete is sent.
    /// </summary>
    [Fact]
    public async Task DiscardDeletesLinesAndVersionsWithoutReadingThem()
    {
        var counter = new CommandCountingInterceptor();
        await using var harness = await Harness.CreateAsync(counter);
        var caseId = (await harness.AcceptAsync("write-count-discard-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        var xml = System.Text.Encoding.UTF8.GetBytes(GlassEstimateXmlParserTests.GlassExport.BuildXml());
        var parsed = new Pegasus.Infrastructure.Glass.GlassEstimateXmlParser().Parse(xml);
        var hash = Convert.ToHexStringLower(SHA256.HashData(xml));

        var firstLease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "write-count-discard-lease-1");
        var first = await harness.RepairSpecifications.SaveImportedEstimateAsync(
            new(caseId, 0, engineer, "write-count-discard-import", ImportRawEstimate.ImportReason,
                firstLease.Token, null, new("Glass's 1", 40m, null, 20m), parsed.Lines,
                new(RepairSpecificationSourceRoute.Glasses, "estimate-import:write-count", parsed.SourceVersion, hash)),
            CancellationToken.None);
        var typedLease = await harness.AcquireLeaseAsync(caseId, 1, engineer, "write-count-discard-lease-typed");
        var typed = await new SaveEstimate(harness.RepairSpecifications,
            new EfAiJobStore(harness.Factory, harness.Clock)).ExecuteAsync(
            new(caseId, typedLease.Version, engineer, "write-count-discard-typed", "Recorded a typed spec.",
                typedLease.Token, null, new("Typed", 40m, 0m, 20m),
                [new("repair", null, "Repair door", 2m, null, false, null, null, "judgement", null)],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        Assert.True(typed.IsCurrent);
        Assert.NotEmpty(first.Lines);
        var discard = new DiscardEstimate(harness.RepairSpecifications);

        var refusalLease = await harness.AcquireLeaseAsync(caseId, 2, engineer, "write-count-discard-lease-refused");
        counter.Reset();
        await Assert.ThrowsAsync<InvalidOperationException>(() => discard.ExecuteAsync(
            new DiscardEstimateRequest(
                caseId, refusalLease.Version, engineer, "write-count-discard-refused", "In use.",
                refusalLease.Token, typed.SpecificationId),
            CancellationToken.None));
        Assert.Equal(0, counter.CountMatching(text => text.Contains("DELETE", StringComparison.Ordinal)));

        counter.Reset();
        await discard.ExecuteAsync(
            new DiscardEstimateRequest(
                caseId, refusalLease.Version, engineer, "write-count-discard", "Wrong document.",
                refusalLease.Token, first.SpecificationId),
            CancellationToken.None);

        Assert.True(
            counter.CountMatching(text => IsSetDelete(text, "CaseEstimateLines")) == 1
                && counter.CountMatching(text => IsRowDelete(text, "CaseEstimateLines")) == 0,
            "The lines were not deleted by one set DELETE." + Environment.NewLine + counter.Describe());
        Assert.True(
            counter.CountMatching(text => IsSetDelete(text, "CaseRepairSpecificationSnapshots")) == 1
                && counter.CountMatching(text => IsRowDelete(text, "CaseRepairSpecificationSnapshots")) == 0,
            "The versions were not deleted by one set DELETE." + Environment.NewLine + counter.Describe());
        Assert.Equal(0, counter.CountMatching(text =>
            text.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)
            && (text.Contains("[CaseEstimateLines]", StringComparison.Ordinal)
                || text.Contains("[DetailsJson]", StringComparison.Ordinal)
                || text.Contains("[LinesJson]", StringComparison.Ordinal))));
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.False(await context.CaseRepairSpecifications.AsNoTracking()
            .AnyAsync(item => item.Id == first.SpecificationId));
        Assert.False(await context.CaseEstimateLines.AsNoTracking()
            .AnyAsync(item => item.RepairSpecificationId == first.SpecificationId));
        Assert.False(await context.CaseRepairSpecificationSnapshots.AsNoTracking()
            .AnyAsync(item => item.SpecificationId == first.SpecificationId));
        Assert.True(await context.CaseEstimateLines.AsNoTracking()
            .AnyAsync(item => item.RepairSpecificationId == typed.SpecificationId));
    }
}
