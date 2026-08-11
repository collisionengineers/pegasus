using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Browser;

[Trait("Category", "SqlServer")]
[Trait("Category", "Browser")]
public sealed class OperatorJourneyTests
{
    [Fact]
    public async Task CustodyRecoveryAndEvaHandoffAreKeyboardUsableWithoutInternalIdentifiersOrExternalClaims()
    {
        var vehicleEvidence = new BrowserVehicleEvidenceQueries();
        var caseDataState = new BrowserCaseDataState();
        await using var support = await BrowserTestSupport.StartAsync(
            width: 1440,
            height: 900,
            javaScriptEnabled: false,
            configureWebHost: builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Eva:AcceptedMapping:Key"] = CaseEvaMapping.MappingKey,
                        ["Eva:AcceptedMapping:Version"] = CaseEvaMapping.MappingVersion
                            .ToString(CultureInfo.InvariantCulture),
                        ["Eva:AcceptedMapping:EvidenceReference"] = "browser-controlled-accepted-mapping"
                    }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IVehicleEvidenceQueries>();
                    services.AddSingleton<IVehicleEvidenceQueries>(vehicleEvidence);
                    services.RemoveAll<ICaseDataQueries>();
                    services.AddScoped<ICaseDataQueries>(provider => new BrowserAcceptedCaseDataQueries(
                        provider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                        caseDataState));
                    services.RemoveAll<EvaMappingAcceptance>();
                    services.AddSingleton(new EvaMappingAcceptance(
                        CaseEvaMapping.MappingKey,
                        CaseEvaMapping.MappingVersion,
                        "browser-controlled-accepted-mapping"));
                });
            });
        var accepted = await SeedCustodyRecoveryCaseAsync(support.Services);
        caseDataState.Set(accepted.CaseId, accepted.Reference);
        vehicleEvidence.Set(ConfirmedVehicle(accepted.CaseId));
        await MarkCustodyFailedAsync(support.Services, accepted.CaseId, accepted.CustodyWorkId);

        var response = await support.GoToAsync($"/Cases/{accepted.CaseId:D}");
        Assert.Equal(200, response.Status);
        var initialText = await support.Page.Locator("main").InnerTextAsync();
        Assert.Contains("Case evidence", initialText, StringComparison.Ordinal);
        Assert.Contains("failed", initialText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temporarily unavailable", initialText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("At least one custody-confirmed current image version is required", initialText,
            StringComparison.Ordinal);
        Assert.Contains("Case custody has not been confirmed", initialText, StringComparison.Ordinal);
        AssertOperatorSafe(initialText, accepted.CaseId);

        await SeedEligibleImageAsync(support.Services, accepted.CaseId, accepted.Reference);
        await support.GoToAsync($"/Cases/{accepted.CaseId:D}");
        Assert.DoesNotContain(
            "At least one custody-confirmed current image version is required",
            await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.Ordinal);

        await EnterEditModeByKeyboardAsync(support.Page);
        var retryButton = support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Retry custody", Exact = true });
        var retryForm = retryButton.Locator("xpath=ancestor::form");
        var retryReason = retryForm.GetByLabel("Reason", new() { Exact = true });
        Assert.NotNull(await retryReason.GetAttributeAsync("required"));
        await retryReason.FillAsync("Staff reviewed the visible custody failure and approved recovery.");
        await retryButton.FocusAsync();
        await retryButton.PressAsync("Enter");
        await support.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("pending", await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);

        await using (var scope = support.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>()
                .ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);
        }
        await support.GoToAsync($"/Cases/{accepted.CaseId:D}");
        var confirmedText = await support.Page.Locator("main").InnerTextAsync();
        Assert.Contains("Case evidence", confirmedText, StringComparison.Ordinal);
        Assert.Contains("confirmed", confirmedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Case custody has not been confirmed", confirmedText, StringComparison.Ordinal);

        await EnterEditModeByKeyboardAsync(support.Page);
        await SubmitGenerateByKeyboardAsync(support.Page, "Prepare the reviewed deterministic handoff.");
        var generatedText = await support.Page.Locator("main").InnerTextAsync();
        Assert.Contains("Business revision 1", generatedText, StringComparison.Ordinal);
        Assert.Contains("integrity verified", generatedText, StringComparison.Ordinal);
        Assert.Contains("Revisions\n1", generatedText.Replace("\r", string.Empty), StringComparison.Ordinal);
        AssertOperatorSafe(generatedText, accepted.CaseId);

        await EnterEditModeByKeyboardAsync(support.Page);
        await SubmitGenerateByKeyboardAsync(support.Page, "Repeat unchanged reviewed handoff preparation.");
        var replayText = await support.Page.Locator("main").InnerTextAsync();
        Assert.Contains("Revisions\n1", replayText.Replace("\r", string.Empty), StringComparison.Ordinal);

        await EnterEditModeByKeyboardAsync(support.Page);
        var downloadButton = support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Download handoff", Exact = true });
        var downloadForm = downloadButton.Locator("xpath=ancestor::form");
        await downloadForm.GetByLabel("Reason", new() { Exact = true })
            .FillAsync("Download the reviewed handoff for manual EVA drag-and-drop.");
        var responseTask = support.Page.WaitForResponseAsync(value =>
            value.Request.Method == "POST"
            && value.Url.Contains("handler=EvaDownload", StringComparison.OrdinalIgnoreCase));
        var downloadTask = support.Page.WaitForDownloadAsync();
        await downloadButton.FocusAsync();
        await downloadButton.PressAsync("Enter");
        var downloadResponse = await responseTask;
        var download = await downloadTask;
        Assert.Equal(200, downloadResponse.Status);
        var path = Assert.IsType<string>(await download.PathAsync());
        var bytes = await File.ReadAllBytesAsync(path);
        var digest = Convert.ToBase64String(SHA256.HashData(bytes));
        var headers = await downloadResponse.AllHeadersAsync();
        Assert.Equal($"sha-256=:{digest}:", headers["content-digest"]);
        Assert.Equal($"EVA-{accepted.Reference}-Revision-001.zip", download.SuggestedFilename);
        Assert.DoesNotContain(accepted.CaseId.ToString("D"), download.SuggestedFilename,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("[0-9a-f]{32,64}", download.SuggestedFilename);

        Assert.False(await support.Page.EvaluateAsync<bool>("() => navigator.javaEnabled()"));
    }

    [Fact]
    public async Task OperationsFirstJourneyUsesAuthenticatedRealHttpRoutes()
    {
        await using var support = await BrowserTestSupport.StartAsync();

        var operationsResponse = await support.GoToAsync("/");

        Assert.Equal(200, operationsResponse.Status);
        Assert.Equal(
            "Dashboard",
            await support.Page.GetByRole(
                AriaRole.Heading,
                new PageGetByRoleOptions { Name = "Dashboard", Exact = true }).InnerTextAsync());
        Assert.Contains(
            "development-offline-administrator",
            await support.Page.Locator("[aria-label='User']").InnerTextAsync(),
            StringComparison.Ordinal);

        var navigation = await support.Page.Locator("nav[aria-label='Primary']").InnerTextAsync();
        // The navigation speaks the business's language, not the pipeline's:
        // "Intake" was internal vocabulary for what the office calls the Inbox,
        // and "Triage" is a reserved business term that was being spent on a
        // screen which is not about Triage-type work at all.
        AssertOrdered(
            navigation,
            "Dashboard",
            "Inbox",
            "Upload",
            "Queues",
            "Cases",
            "Administration",
            "development-offline-administrator");

        // The three sections an operator actually opens this screen to read.
        // Lowercased because the section labels are uppercased by the
        // stylesheet, so the rendered text is the styling, not the copy.
        var dashboard = (await support.Page.Locator("main").InnerTextAsync()).ToLowerInvariant();
        AssertOrdered(dashboard, "active cases", "e-mail activity", "today and this week");

        // Every metric opens the exact filtered list behind it. Review is the
        // case stage, and the tile is backed by a count of cases in it — it
        // used to render an intake-receipt count and link into the intake
        // queue, which is a different entity on a different screen.
        await support.Page.Locator(".metric-strip a.metric", new PageLocatorOptions { HasText = "Review" }).ClickAsync();
        Assert.Equal("/Triage?queue=review", new Uri(support.Page.Url).PathAndQuery);

        await support.GoToAsync("/");
        await support.Page.Locator(".metric-strip a.metric", new PageLocatorOptions { HasText = "Needs sorting" }).ClickAsync();
        Assert.Equal("/Received?decision=needs_sorting", new Uri(support.Page.Url).PathAndQuery);
    }

    [Fact]
    public async Task UnimplementedAndExternalBoundariesAreObservableAndFailClosed()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        await support.GoToAsync("/");

        // The invariant is now the opposite of what it was. This screen used to
        // ship nine tiles and two cards hardcoded to the literal string
        // "Unavailable", so a first-run operator met a wall of failure chrome
        // on a healthy system. A tile whose query does not exist is not
        // shipped; every tile that is shipped renders a number, and 0 is a
        // number.
        Assert.Equal(0, await support.Page.Locator("[data-queue-state='unavailable']").CountAsync());
        var metricValues = await support.Page.Locator(".metric .metric__value").AllInnerTextsAsync();
        Assert.NotEmpty(metricValues);
        Assert.All(metricValues, value => Assert.Matches(@"^\d+$", value.Trim()));

        var unknownRequest = await support.GoToAsync("/Uploads/not-an-accepted-token");
        Assert.Equal(404, unknownRequest.Status);

        var unknownEvaHandoff = await support.GoToAsync($"/Received/EvaHandoff/{Guid.NewGuid():D}");
        Assert.Equal(404, unknownEvaHandoff.Status);
    }

    [Fact]
    public async Task KeyboardJourneyExposesSkipLinkAndVisibleFocus()
    {
        await using var support = await BrowserTestSupport.StartAsync();
        await support.GoToAsync("/");

        await support.Page.Keyboard.PressAsync("Tab");
        var skipLink = support.Page.Locator(".skip-link");
        await Assertions.Expect(skipLink).ToBeFocusedAsync();
        Assert.True(await skipLink.IsVisibleAsync());

        await support.Page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(support.Page.Locator("#main-content")).ToBeFocusedAsync();
    }

    private static async Task<BrowserAcceptedCase> SeedCustodyRecoveryCaseAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var scopedServices = scope.ServiceProvider;
        var now = scopedServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var email = IntakeTestEvidence.CreateEmail(
            $"custody-eva-browser-{Guid.NewGuid():N}.eml",
            """
            QDOS instruction
            Claimant Name: Controlled Browser Claimant
            Claim Number: BROWSER-2031-001
            Vehicle Registration: AB12 CDE
            Vehicle Make: Example Make
            Vehicle Model: Example Model
            Vehicle Mileage: 12,345 miles
            Accident Circumstances: Controlled browser protocol circumstances
            Date of Incident: 04/03/2031
            Instruction Date: 05/03/2031
            Inspection Date: 06/03/2031
            Inspection Address: Image Based Assessment
            VAT Status: VAT registered
            """);
        var source = new IntakeSource(
            email.FileName,
            email.MediaType,
            email.Content,
            now,
            "browser-controlled-fixture",
            new(IntakeSourceChannel.ManualUpload, $"browser-custody-eva:{Guid.NewGuid():N}"));
        var receipt = await scopedServices.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(source, CancellationToken.None);
        Assert.Equal(IntakeDecision.CaseCreated, receipt.Decision);
        await SeedPrincipalAsync(scopedServices, QdosPrincipal.Code, now);
        var accepted = await scopedServices.GetRequiredService<IAcceptIntake>().ExecuteAsync(
            new(
                receipt.Id,
                receipt.Version,
                ActionActor.SystemWorker("browser-custody-eva"),
                $"browser-case-accept:{Guid.NewGuid():N}",
                "Controlled browser evidence is complete for the custody and EVA journey.",
                CaseType.Inspection,
                QdosPrincipal.Code,
                new(true, true, true, true),
                null,
                new DateOnly(2031, 3, 6)),
            CancellationToken.None);
        return new(
            accepted.Identity.CaseId,
            accepted.Identity.Reference,
            accepted.CustodyWorkId);
    }

    private static async Task SeedPrincipalAsync(
        IServiceProvider services,
        string principalCode,
        DateTimeOffset now)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        if (await context.Principals.AnyAsync(item => item.Code == principalCode && item.IsActive))
        {
            return;
        }
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Browser controlled provider"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO OrganizationRoles (OrganizationId, Role) VALUES ({organizationId}, {"work_provider"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({Guid.NewGuid()}, {organizationId}, {principalCode}, {lineageId}, {true}, {0L})");
        await transaction.CommitAsync();
    }

    private static async Task MarkCustodyFailedAsync(
        IServiceProvider services,
        Guid caseId,
        Guid workItemId)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ExternalWorkItems SET State = {"failed"}, AttemptCount = {1}, FailureCode = {"provider_unavailable"}, FailureReason = {"The custody provider is temporarily unavailable."}, LeaseToken = NULL, LeaseExpiresAtUtc = NULL WHERE Id = {workItemId}");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cases SET CustodyState = {"failed"} WHERE Id = {caseId}");
    }

    private static async Task SeedEligibleImageAsync(
        IServiceProvider services,
        Guid caseId,
        string caseReference)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var contentStore = services.GetRequiredService<IDocumentContentStore>();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("controlled browser image evidence");
        var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        await using var context = await contextFactory.CreateDbContextAsync();
        var ordinal = await context.Set<CaseDocumentEntity>().CountAsync(item => item.CaseId == caseId) + 1;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDocuments (Id, CaseId, Ordinal, SourceOccurrenceIdentity) VALUES ({documentId}, {caseId}, {ordinal}, {"browser:damage-image"})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO DocumentVersions (Id, DocumentId, Version, FileName, MediaType, ContentLength, Sha256, CustodyStatus, CreatedAtUtc, CreatedBy, IsCurrent, IsLogicallyRemoved) VALUES ({versionId}, {documentId}, {1}, {"Controlled damage.jpg"}, {"image/jpeg"}, {(long)content.Length}, {sha256}, {"Confirmed"}, {now}, {"staff:browser-fixture"}, {true}, {false})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO DocumentOccurrences (Id, CaseId, DocumentId, VersionId, Ordinal, SemanticRole, Source, SourceOccurrenceIdentity, RecordedAtUtc, OperationKey) VALUES ({occurrenceId}, {caseId}, {documentId}, {versionId}, {ordinal}, {"Image"}, {"StaffUpload"}, {"browser:damage-image"}, {now}, {"browser:damage-image"})");
        await contentStore.StoreAsync(caseId, caseReference, versionId, content, sha256, CancellationToken.None);
    }

    private static async Task EnterEditModeByKeyboardAsync(IPage page)
    {
        var button = page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Enter edit mode", Exact = true });
        await button.FocusAsync();
        await button.PressAsync("Enter");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("You hold edit authority", await page.Locator("main").InnerTextAsync(),
            StringComparison.Ordinal);
    }

    private static async Task SubmitGenerateByKeyboardAsync(IPage page, string reason)
    {
        var button = page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Generate deterministic EVA handoff", Exact = true });
        Assert.True(await button.IsVisibleAsync(), await page.Locator("main").InnerTextAsync());
        var form = button.Locator("xpath=ancestor::form");
        await form.GetByLabel("Reason", new() { Exact = true }).FillAsync(reason);
        await button.FocusAsync();
        await button.PressAsync("Enter");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private static CaseVehicleEvidence ConfirmedVehicle(Guid caseId) => new(
        caseId,
        new(
            VehicleField("AB12CDE"),
            VehicleField("Example Make"),
            VehicleField("Example Model"),
            VehicleField(12345L),
            VehicleField(VehicleMileageUnit.Miles)),
        null,
        [],
        []);

    private static ConfirmedVehicleField<T> VehicleField<T>(T value)
        where T : notnull => new(
            value,
            "staff-confirmation",
            "browser-controlled-vehicle",
            "Controlled browser vehicle evidence",
            "browser-vehicle-v1",
            1,
            "staff:browser-fixture",
            new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
            null);

    private static void AssertOperatorSafe(string visibleText, Guid caseId)
    {
        Assert.DoesNotContain(caseId.ToString("D"), visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", visibleText);
        Assert.DoesNotMatch("(?i)\\b[0-9a-f]{64}\\b", visibleText);
        Assert.DoesNotContain(".pegasus-create-", visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Workflow version", visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EVA received", visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Engineer assigned", visibleText, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record BrowserAcceptedCase(Guid CaseId, string Reference, Guid CustodyWorkId);

    private sealed class BrowserCaseDataState
    {
        public Guid CaseId { get; private set; }

        public string? Reference { get; private set; }

        public void Set(Guid caseId, string reference) => (CaseId, Reference) = (caseId, reference);
    }

    private sealed class BrowserAcceptedCaseDataQueries(
        IDbContextFactory<PegasusDbContext> contextFactory,
        BrowserCaseDataState state) : ICaseDataQueries
    {
        public async Task<CaseDataProjection?> GetAsync(
            Guid caseId,
            CancellationToken cancellationToken)
        {
            if (state.CaseId != caseId || string.IsNullOrWhiteSpace(state.Reference))
            {
                return null;
            }
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var version = await context.CaseWorkflows.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => item.Version)
                .SingleAsync(cancellationToken);
            var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
            return new(
                new(caseId, QdosPrincipal.Code, 2031, 1, state.Reference),
                new(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    IntakeSourceChannel.ManualUpload,
                    "browser-controlled-source",
                    new string('a', 64),
                    now,
                    "browser-controlled-reader",
                    "1",
                    "browser-controlled-policy",
                    1),
                now,
                version,
                CaseLifecycleState.Review,
                new(new(true, true, true, true), new(true, "browser-completeness", 1)),
                new(CaseField("QDOS")),
                new(CaseField("Controlled Browser Claimant")),
                new(CaseField("BROWSER-2031-001")),
                new(
                    CaseField("AB12CDE"),
                    CaseField("Example Make"),
                    CaseField("Example Model"),
                    CaseField(12345L),
                    CaseField("miles")),
                new(
                    CaseField(new DateOnly(2031, 3, 4)),
                    CaseField("Controlled browser protocol circumstances")),
                new(CaseField("Controlled Contact"), CaseField("browser@example.invalid"), CaseField("01234567890")),
                new(CaseField(new DateOnly(2031, 3, 5)), CaseField("VAT registered")),
                new(
                    CaseField(new DateOnly(2031, 3, 6)),
                    CaseField(new DateOnly(2031, 3, 6)),
                    CaseField(CaseEvaMapping.ImageBasedAssessment),
                    CaseField(CaseInspectionMode.ImageBasedAssessment)));
        }
    }

    private static CaseField<T> CaseField<T>(T value)
        where T : notnull => new(
            new(
                value,
                CaseDataValueKind.Confirmed,
                new(
                    CaseDataSourceKind.CaseAcceptance,
                    "browser-controlled-source",
                    "Controlled browser evidence",
                    "browser-controlled-policy",
                    1),
                "staff:browser-fixture",
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero)),
            null,
            null);

    private sealed class BrowserVehicleEvidenceQueries : IVehicleEvidenceQueries
    {
        private CaseVehicleEvidence? evidence;

        public void Set(CaseVehicleEvidence value) => evidence = value;

        public Task<CaseVehicleEvidence?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult(evidence?.CaseId == caseId ? evidence : null);
    }

    private static void AssertOrdered(string value, params string[] fragments)
    {
        var previous = -1;
        foreach (var fragment in fragments)
        {
            var current = value.IndexOf(fragment, StringComparison.Ordinal);
            Assert.True(current > previous, $"Expected '{fragment}' after the prior navigation item in '{value}'.");
            previous = current;
        }
    }
}
