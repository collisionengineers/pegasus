using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
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
        var repositoryFixture = RepositoryEvaFixture.Load();
        var vehicleEvidence = new BrowserVehicleEvidenceQueries();
        var caseDataState = new BrowserCaseDataState(repositoryFixture);
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
                });
            });
        var accepted = await SeedCustodyRecoveryCaseAsync(support.Services, repositoryFixture);
        caseDataState.Set(accepted.CaseId, accepted.Reference);
        vehicleEvidence.Set(ConfirmedVehicle(accepted.CaseId, repositoryFixture));
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

        await SeedEligibleImageAsync(
            support.Services, accepted.CaseId, repositoryFixture);
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

        await support.GoToAsync("/Operations");
        Assert.Equal(
            "Operations",
            await support.Page.GetByRole(
                AriaRole.Heading,
                new PageGetByRoleOptions { Name = "Operations", Exact = true }).InnerTextAsync());
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

    private static async Task<BrowserAcceptedCase> SeedCustodyRecoveryCaseAsync(
        IServiceProvider services,
        RepositoryEvaFixture fixture)
    {
        await using var scope = services.CreateAsyncScope();
        var scopedServices = scope.ServiceProvider;
        var now = scopedServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var email = IntakeTestEvidence.CreateEmail(
            "AX_SP58WVO.eml",
            fixture.SourceJson,
            "sender@example.test");
        var source = new IntakeSource(
            email.FileName,
            email.MediaType,
            email.Content,
            now,
            "browser-controlled-fixture",
            new(IntakeSourceChannel.ManualUpload, $"browser-custody-eva:{Guid.NewGuid():N}"));
        var receipt = await scopedServices.GetRequiredService<ProcessIntake>()
            .ExecuteAsync(source, CancellationToken.None);
        Assert.Equal(IntakeDecision.NeedsSorting, receipt.Decision);
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
                null),
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
        RepositoryEvaFixture fixture)
    {
        await using var scope = services.CreateAsyncScope();
        services = scope.ServiceProvider;
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var workflow = Assert.IsType<CaseWorkflowRecord>(await services
            .GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None));
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(caseId, workflow.Version, actor, "browser-reference-image-lease"),
            CancellationToken.None);
        var added = await services.GetRequiredService<IAddCaseDocument>().ExecuteAsync(
            new(
                caseId,
                "engineer1.png",
                "image/png",
                fixture.ImageBytes,
                DocumentSemanticRole.Image,
                DocumentSource.StaffUpload,
                "reference/eva_information/screenshots/engineer-screens/engineer1.png",
                actor,
                "browser-reference-image-add",
                lease.Version,
                lease.Token),
            CancellationToken.None);
        Assert.Equal(DocumentCustodyStatus.Confirmed, added.Version.CustodyStatus);
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

    private static CaseVehicleEvidence ConfirmedVehicle(
        Guid caseId,
        RepositoryEvaFixture fixture) => new(
        caseId,
        new(
            VehicleField(fixture.Vrm),
            null,
            VehicleField(fixture.VehicleModel),
            VehicleField(fixture.Mileage),
            VehicleField(fixture.MileageUnit)),
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

    private sealed class BrowserCaseDataState(RepositoryEvaFixture fixture)
    {
        public Guid CaseId { get; private set; }

        public string? Reference { get; private set; }

        public RepositoryEvaFixture Fixture { get; } = fixture;

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
            var fixture = state.Fixture;
            return new(
                new(caseId, QdosPrincipal.Code, 2031, 1, state.Reference),
                new(
                    Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    IntakeSourceChannel.ManualUpload,
                    "browser-controlled-source",
                    fixture.SourceSha256,
                    now,
                    "browser-controlled-reader",
                    "1",
                    "browser-controlled-policy",
                    1),
                now,
                version,
                CaseLifecycleState.Review,
                new(new(true, true, true, true), new(true, "browser-completeness", 1)),
                new(CaseField(fixture.WorkProvider)),
                new(CaseField(fixture.ClaimantName)),
                new(CaseField(fixture.Reference)),
                new(
                    CaseField(fixture.Vrm),
                    EmptyCaseField<string>(),
                    CaseField(fixture.VehicleModel),
                    CaseField(fixture.Mileage),
                    CaseField(fixture.MileageUnit.ToString())),
                new(
                    CaseField(fixture.IncidentDate),
                    CaseField(fixture.AccidentCircumstances)),
                new(CaseField(fixture.ClaimantName), EmptyCaseField<string>(), EmptyCaseField<string>()),
                new(CaseField(fixture.InstructionDate), CaseField(fixture.VatStatus)),
                new(
                    CaseField(fixture.InspectionDate),
                    CaseField(fixture.InspectionDate),
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

    private static CaseField<T> EmptyCaseField<T>()
        where T : notnull => new(null, null, null);

    private sealed class BrowserVehicleEvidenceQueries : IVehicleEvidenceQueries
    {
        private CaseVehicleEvidence? evidence;

        public void Set(CaseVehicleEvidence value) => evidence = value;

        public Task<CaseVehicleEvidence?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult(evidence?.CaseId == caseId ? evidence : null);
    }

    private sealed record RepositoryEvaFixture(
        string SourceJson,
        string SourceSha256,
        byte[] ImageBytes,
        string WorkProvider,
        string Vrm,
        string VehicleModel,
        string ClaimantName,
        string Reference,
        DateOnly IncidentDate,
        DateOnly InstructionDate,
        DateOnly InspectionDate,
        string InspectionAddress,
        string AccidentCircumstances,
        string VatStatus,
        long Mileage,
        VehicleMileageUnit MileageUnit)
    {
        public static RepositoryEvaFixture Load()
        {
            var root = FindRepositoryRoot();
            var sourcePath = Path.Combine(root, "reference", "eva_information", "AX_SP58WVO.json");
            var imagePath = Path.Combine(
                root, "reference", "eva_information", "screenshots", "engineer-screens", "engineer1.png");
            var sourceJson = File.ReadAllText(sourcePath);
            using var document = JsonDocument.Parse(sourceJson);
            string Field(string name) => document.RootElement.GetProperty(name).GetString()!;
            return new(
                sourceJson,
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant(),
                File.ReadAllBytes(imagePath),
                Field("Work Provider"),
                Field("VRM"),
                Field("Vehicle Model"),
                Field("Claimant Name"),
                Field("Reference"),
                DateOnly.ParseExact(Field("Incident Date"), "dd/MM/yyyy", CultureInfo.InvariantCulture),
                DateOnly.ParseExact(Field("Instruction Date"), "dd/MM/yyyy", CultureInfo.InvariantCulture),
                DateOnly.ParseExact(Field("Inspection Date"), "dd/MM/yyyy", CultureInfo.InvariantCulture),
                Field("Inspection Address").Trim(),
                Field("Accident Circumstances").Trim(),
                Field("VAT Status"),
                long.Parse(Field("Mileage"), CultureInfo.InvariantCulture),
                Field("Mileage Unit").Equals("Miles", StringComparison.OrdinalIgnoreCase)
                    ? VehicleMileageUnit.Miles
                    : VehicleMileageUnit.Kilometres);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                directory = directory.Parent;
            }
            return directory?.FullName
                ?? throw new InvalidOperationException("The repository root could not be resolved.");
        }
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
