using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Notifications;
using Pegasus.Core.ReleaseNotes;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// <see cref="IntakeWebApplicationFactory.WithoutDatabase"/>: a Case page over
/// a recording store renders without opening a connection, a read that
/// reaches the database is recorded and fails the factory's disposal, every
/// default answers as the migrated database does, and the factory has no
/// database to hand out.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DatabaseLessWebHostTests
{
    [Fact]
    public async Task ACasePageOverTheRecordingStoreOpensNoConnection()
    {
        var store = new RecordingCaseDetailsStore();
        using var baseFactory = IntakeWebApplicationFactory.WithoutDatabase();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCaseEditBasis>(services, store);
                SubstituteDetailsPageReaders(services, store);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");

        Assert.Contains("QDOS3100042", html, StringComparison.Ordinal);
        Assert.Empty(baseFactory.DatabaseOpenAttempts);
    }

    /// <summary>
    /// The action log and its health metrics have no default, so the Logs
    /// page reaches the database. The refusal fails the request, and the
    /// record fails the disposal even where a page would have swallowed it.
    /// </summary>
    [Fact]
    public async Task AReadThatReachesTheDatabaseIsRecordedAndFailsTheDisposal()
    {
        var factory = IntakeWebApplicationFactory.WithoutDatabase();
        using (var client = IntakeWebDriver.CreateClient(factory))
        {
            HttpStatusCode? status = null;
            try
            {
                using var response = await client.GetAsync("/Administration/Logs");
                status = response.StatusCode;
            }
            catch (InvalidOperationException)
            {
                // The refusal propagated from the request instead of becoming a 500.
            }

            Assert.True(
                status is null or HttpStatusCode.InternalServerError,
                $"The Logs page answered {status} without its database.");
        }

        var attempts = factory.DatabaseOpenAttempts;
        Assert.NotEmpty(attempts);
        var disposal = Assert.Throws<InvalidOperationException>(() => factory.Dispose());
        Assert.All(attempts, attempt => Assert.Contains(attempt, disposal.Message, StringComparison.Ordinal));
        // The record is reported once: a second disposal is quiet.
        factory.Dispose();
    }

    /// <summary>
    /// Each default answers what the migrated, initialised database answers
    /// for a Case it does not hold, so a test moved off the database reads
    /// what it read before.
    /// </summary>
    [Fact]
    public async Task TheDefaultsAnswerAsTheMigratedDatabaseDoes()
    {
        var caseId = Guid.NewGuid();
        var administrator = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        const CaseWorkSelector work = CaseWorkSelector.Current;
        var none = CancellationToken.None;
        (string Read, Func<IServiceProvider, Task<object?>> Answer)[] reads =
        [
            ("notifications", async services =>
                await services.GetRequiredService<IMyStaffNotifications>().ListAsync(administrator, none)),
            ("release note", async services =>
                await services.GetRequiredService<IMyReleaseNotes>().GetUnacknowledgedAsync(administrator, none)),
            ("staff directory", async services => await StaffDirectoryAsync(
                services.GetRequiredService<IStaffAccountQueries>())),
            ("case kind", async services =>
                await services.GetRequiredService<ICaseKindQueries>().GetAsync(caseId, none)),
            ("asset preparations", async services =>
                await services.GetRequiredService<ICaseAssetPreparationQueries>().ListForCaseAsync(caseId, none)),
            ("asset preparation", async services => await services.GetRequiredService<ICaseAssetPreparationQueries>()
                .GetForOccurrenceAsync(caseId, Guid.NewGuid(), none)),
            ("previous inspection addresses", async services => await services
                .GetRequiredService<IInspectionAddressChoicesQueries>().GetPreviousAddressesAsync(caseId, none)),
            ("reviewed inspection locations", async services => await services
                .GetRequiredService<IInspectionAddressChoicesQueries>().GetDirectoryLocationsAsync(caseId, none)),
            ("AI jobs", async services =>
                await services.GetRequiredService<IAiJobQueries>().ListForSubjectAsync(caseId, none)),
            ("pending market research", async services =>
                await services.GetRequiredService<IAiJobQueries>().GetPendingMarketResearchAsync(caseId, none)),
            ("estimates", async services =>
                await services.GetRequiredService<IListCaseEstimates>().ExecuteAsync(caseId, work, none)),
            ("labour-rate cards", async services =>
                await services.GetRequiredService<ILabourRateCardStore>().ListAsync(none)),
            ("saved unroadworthy reasons", async services =>
                await services.GetRequiredService<IUnroadworthyReasonBankStore>().ListAsync("QDOS", none)),
            ("salvage matrix", async services =>
                await services.GetRequiredService<IPrincipalSalvageMatrixQueries>().GetForCaseAsync(caseId, none)),
            ("report inputs", async services => await services.GetRequiredService<ICaseReportSnapshotSource>()
                .GetAsync(caseId, administrator, work, null, none)),
            ("current report", async services => await services.GetRequiredService<ICaseReportGenerationStore>()
                .GetCurrentAsync(administrator, caseId, work, none)),
            ("Send to AI", async services =>
                await services.GetRequiredService<ISendToAiControl>().IsEnabledAsync(none)),
            ("valuations", async services =>
                await services.GetRequiredService<IListCaseValuations>().ExecuteAsync(caseId, work, none)),
            ("applied valuations", async services =>
                await services.GetRequiredService<IListAppliedValuations>().ExecuteAsync(caseId, work, none)),
            ("valuation presets", async services =>
                await services.GetRequiredService<IValuationPresetStore>().ListAsync(none)),
            ("claim sources", async services => await services.GetRequiredService<IContactDirectoryQueries>()
                .ListByRoleAsync(administrator, ContactRole.ClaimSource, none)),
            ("repairers", async services => await services.GetRequiredService<IContactDirectoryQueries>()
                .ListByRoleAsync(administrator, ContactRole.Repairer, none)),
            ("lease to resume", async services => await services.GetRequiredService<IResumeCaseEditLease>()
                .ExecuteAsync(new(caseId, administrator), none)),
            ("image tags", async services =>
                await services.GetRequiredService<IReadImageTagVocabulary>().ListAsync(none)),
            ("image intakes", async services =>
                await services.GetRequiredService<IImageIntakeQueries>().ListForCaseAsync(caseId, none)),
            ("image intake images", async services =>
                await services.GetRequiredService<IImageIntakeQueries>().ListImagesAsync(Array.Empty<Guid>(), none)),
        ];
        using var migrated = new IntakeWebApplicationFactory();
        using var databaseLess = IntakeWebApplicationFactory.WithoutDatabase();
        await using var migratedScope = migrated.Services.CreateAsyncScope();
        await using var databaseLessScope = databaseLess.Services.CreateAsyncScope();

        var expected = new List<string>();
        var actual = new List<string>();
        foreach (var (read, answer) in reads)
        {
            expected.Add($"{read}: {JsonSerializer.Serialize(await answer(migratedScope.ServiceProvider))}");
            actual.Add($"{read}: {JsonSerializer.Serialize(await answer(databaseLessScope.ServiceProvider))}");
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TheFactoryHasNoDatabaseToHandOut()
    {
        using var factory = IntakeWebApplicationFactory.WithoutDatabase();

        Assert.Throws<InvalidOperationException>(() => factory.Database);
    }

    /// <summary>The directory as the Case page reads it: who is listed, by name, role and sign-off.</summary>
    private static async Task<object> StaffDirectoryAsync(IStaffAccountQueries staff)
    {
        var none = CancellationToken.None;
        var roster = await staff.ListAsync(0, 100, none);
        var administrator = await staff.GetAsync(DevelopmentOfflineIdentity.AdministratorId, none);
        var listed = await staff.GetManyAsync([DevelopmentOfflineIdentity.AdministratorId], none);
        return new
        {
            Roster = roster.Accounts.Select(Named).ToArray(),
            roster.HasMoreAccounts,
            Administrator = administrator is null ? null : Named(administrator),
            Listed = listed.Select(Named).ToArray(),
            SignOffEngineers = await staff.ListSignOffEngineersAsync(none),
        };

        static object Named(StaffAccountSummary account) => new
        {
            account.Id,
            account.UserName,
            account.IsEnabled,
            account.MustChangePassword,
            account.Role,
            account.SignOff,
        };
    }
}
