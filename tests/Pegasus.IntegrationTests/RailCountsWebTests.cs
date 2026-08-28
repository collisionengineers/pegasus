using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// PLAT-003 / PLAT-029: the operator rail's Cases count must show the real
/// Not ready + Review + Held total (the already-deployed stage aggregate),
/// and a route with no established figure (Inbox, Operations) must render no
/// count at all rather than a stale zero.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RailCountsWebTests
{
    [Fact]
    public async Task CasesCountShowsTheRealStageTotalAndOtherRoutesRenderNoCount()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var originReceiptId = await StoreMinimalReceiptAsync(services, "rail-fixture.pdf");
        await SeedNotReadyCaseAsync(services, originReceiptId, "QDOS" + DateTime.UtcNow.Ticks % 1_000_000);

        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var casesMatch = Regex.Match(
            html,
            "Cases</span>\\s*<span class=\"nav-count\"[^>]*>(\\d+)</span>");
        Assert.True(casesMatch.Success, "Cases rail count markup not found.");
        Assert.Equal(1, int.Parse(casesMatch.Groups[1].Value, CultureInfo.InvariantCulture));

        // Inbox and Operations have no established figure to reuse
        // (research.md): their rail links must carry no nav-count span at all.
        Assert.False(
            Regex.IsMatch(html, "Inbox</span>\\s*<span class=\"nav-count\""),
            "Inbox must render no count until a real figure exists for it.");
        Assert.False(
            Regex.IsMatch(html, "Operations</span>\\s*<span class=\"nav-count\""),
            "Operations must render no count until a real figure exists for it.");
    }

    /// <summary>
    /// Copied from <c>TriageQueuesWebTests.StoreMinimalReceiptAsync</c> (that
    /// helper is <c>private</c> to its class): the minimal receipt an
    /// origin-scoped Case fixture needs.
    /// </summary>
    private static async Task<Guid> StoreMinimalReceiptAsync(IServiceProvider services, string sourceFileName)
    {
        var receiptStore = services.GetRequiredService<IIntakeReceiptStore>();
        var receipt = await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                sourceFileName,
                "application/pdf",
                1024,
                Guid.NewGuid().ToString("N"),
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                "test-actor",
                IntakeDecision.NeedsSorting,
                "test decision reason",
                [],
                [],
                null,
                [],
                null,
                null,
                "test-reader",
                "1",
                null,
                null),
            CancellationToken.None);
        return receipt.Id;
    }

    /// <summary>
    /// Copied from <c>TriageQueuesWebTests.SeedNotReadyCaseAsync</c> (that
    /// helper is <c>private</c> to its class): a raw-SQL Not-ready Case
    /// fixture — exercising the full instruction pipeline just to get one
    /// NotReady case row is unrelated to what this test verifies, matching
    /// the precedent <c>ImageIntakePersistenceTests.SeedCaseAsync</c> already
    /// set for duplicating this shape rather than sharing a static helper
    /// across test classes.
    /// </summary>
    private static async Task SeedNotReadyCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Rail counts fixture {reference}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {nameof(CaseLifecycleState.NotReady)}, {"pending"}, {originReceiptId}, {true}, {true}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"rail-fixture-reader"}, {"1"}, {"rail-fixture"}, {1}, {reference}, {1}, {true}, {now})");
    }
}
