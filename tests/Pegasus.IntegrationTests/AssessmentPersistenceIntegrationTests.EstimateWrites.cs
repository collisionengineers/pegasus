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
/// What the estimate writes send to SQL Server: the stale-marking is one
/// statement however many generations it touches, and Discard reads neither
/// the lines nor the numbered versions it deletes.
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
    /// Editing the spec in use stales the report that costed it by one set
    /// UPDATE, not a row update from SaveChanges. The line replace is not
    /// pinned: R407 holds its set DELETE until eff-09 has measured.
    /// </summary>
    [Fact]
    public async Task MarkingStaleSendsOneSetUpdate()
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
            counter.CountMatching(text => text.Contains("UPDATE [c]", StringComparison.Ordinal)
                && text.Contains("[CaseReportGenerations] AS [c]", StringComparison.Ordinal)) == 1
                && counter.CountMatching(text => text.Contains("UPDATE [CaseReportGenerations]", StringComparison.Ordinal)) == 0,
            "The generation was not staled by one set UPDATE." + Environment.NewLine + counter.Describe());
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
    /// Two stale rules in one save (an estimate edit, then a Glass's VIN fill)
    /// each record their own stale history row against the generation, as
    /// before the set UPDATE, and the second reports the generation it met.
    /// </summary>
    [Fact]
    public async Task TwoStaleRulesInOneSaveEachRecordHistory()
    {
        var counter = new CommandCountingInterceptor();
        await using var harness = await Harness.CreateAsync(counter);
        var caseId = (await harness.AcceptAsync("write-count-two-rules-case")).Identity.CaseId;
        var (currentGenerationId, _) = await SeedGenerationsAsync(harness, caseId);
        var now = new DateTimeOffset(2031, 5, 6, 11, 0, 0, TimeSpan.Zero);

        counter.Reset();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            Assert.Equal(1, await EfCaseReportGenerationStore.MarkWorkStaleAsync(
                context, caseId, caseId, "current_estimate_saved", now, CancellationToken.None));
            Assert.Equal(1, await EfCaseReportGenerationStore.MarkWorkStaleAsync(
                context, caseId, caseId, "assessment_facts_changed", now, CancellationToken.None));
            var tracked = Assert.Single(context.ChangeTracker.Entries<CaseReportGenerationEntity>(),
                entry => entry.Entity.Id == currentGenerationId);
            Assert.Equal("Stale", tracked.Entity.State);
            Assert.False(tracked.Property(item => item.State).IsModified);
            await context.SaveChangesAsync();
            Assert.Equal("Stale", tracked.Entity.State);
            await transaction.CommitAsync();
        }

        Assert.Equal(1, counter.CountMatching(text => text.Contains("UPDATE [c]", StringComparison.Ordinal)
            && text.Contains("[CaseReportGenerations] AS [c]", StringComparison.Ordinal)));
        Assert.Equal("Stale", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentGenerationId:D}'"));
        Assert.Equal(2, await StaleRowCountAsync(harness, caseId));
        await using var after = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(0, await EfCaseReportGenerationStore.MarkWorkStaleAsync(
            after, caseId, caseId, "current_estimate_saved", now, CancellationToken.None));
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
