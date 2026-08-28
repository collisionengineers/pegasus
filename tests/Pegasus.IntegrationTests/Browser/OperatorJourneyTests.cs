using System.Text.RegularExpressions;
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
    public async Task CustodyRecoveryAndExportAreKeyboardUsableWithoutInternalIdentifiersOrExternalClaims()
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
        // CASE-007 carried the rule here and CASE-012 keeps it on the new
        // workspace: EVA is named exactly once — on the Review handoff
        // control — and nothing about a submission (its state, its
        // references, the fields it would carry) reaches a case that is not
        // ready to send. The dialog's own wording is hidden markup, not
        // rendered text, so it does not count. The case speaks its own
        // identity, never its identifier.
        Assert.Contains(accepted.Reference, initialText, StringComparison.Ordinal);
        Assert.Equal(
            1,
            Regex.Count(initialText, "EVA", RegexOptions.None, TimeSpan.FromSeconds(1)));
        Assert.Contains("Send to EVA", initialText, StringComparison.Ordinal);
        Assert.DoesNotContain("File reference", initialText, StringComparison.Ordinal);
        Assert.DoesNotContain("Sent to EVA", initialText, StringComparison.Ordinal);
        AssertOperatorSafe(initialText, accepted.CaseId);

        await EnterEditModeByKeyboardAsync(support.Page);

        // The seeder takes its own edit authority, so finish editing first.
        var finishButton = support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Finish editing", Exact = true });
        await finishButton.FocusAsync();
        await finishButton.PressAsync("Enter");
        await support.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await SeedEligibleImageAsync(
            support.Services, accepted.CaseId, repositoryFixture);

        // CASE-012: custody recovery is Operations work now. Attention
        // required carries the failure where the case page used to, and its
        // retry is one keyboard-operable control — no reason field, because
        // requeueing operational work is not a case edit.
        var operationsResponse = await support.GoToAsync("/Operations");
        Assert.Equal(200, operationsResponse.Status);
        var attentionText = await support.Page.Locator("main").InnerTextAsync();
        Assert.Contains("Attention required", attentionText, StringComparison.Ordinal);
        Assert.Contains(accepted.Reference, attentionText, StringComparison.Ordinal);
        Assert.Contains("temporarily unavailable", attentionText, StringComparison.OrdinalIgnoreCase);
        AssertOperatorSafe(attentionText, accepted.CaseId);
        var retryButton = support.Page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Retry this work", Exact = true });
        await retryButton.FocusAsync();
        await retryButton.PressAsync("Enter");
        await support.Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("scheduled for retry", await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.OrdinalIgnoreCase);

        // The retried work leaves the retryable list whether it is pending
        // or completed, and it stays gone once the queued custody has run.
        await support.GoToAsync("/Operations");
        Assert.DoesNotContain("Retry this work", await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.Ordinal);

        await using (var scope = support.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>()
                .ExecuteAsync(accepted.CustodyWorkId, CancellationToken.None);
        }
        await support.GoToAsync("/Operations");
        Assert.DoesNotContain("Retry this work", await support.Page.Locator("main").InnerTextAsync(),
            StringComparison.Ordinal);

        // The recovered custody is confirmed where the case carries its
        // files: the Box folder link replaces the preparing state (the old
        // custody row went with the panel that showed it).
        await support.GoToAsync($"/Cases/{accepted.CaseId:D}?section=case-files");
        var filesText = await support.Page.Locator("main").InnerTextAsync();
        Assert.Contains("Open Box case folder", filesText, StringComparison.Ordinal);
        Assert.DoesNotContain("preparing", filesText, StringComparison.OrdinalIgnoreCase);
        AssertOperatorSafe(filesText, accepted.CaseId);

        // ENG-016: one act, and it answers with the file rather than a
        // redirect. The case's handoff dialog needs script and this journey
        // runs without it, so the export starts from the Send page — the
        // one route that still works scriptless (reported to review: with
        // script off, nothing on the case links there).
        await support.GoToAsync($"/Cases/{accepted.CaseId:D}/Eva/Send");
        var firstDownload = await ExportByKeyboardAsync(support.Page);
        Assert.Equal($"EVA-{accepted.Reference}.zip", firstDownload.SuggestedFilename);
        Assert.DoesNotContain(accepted.CaseId.ToString("D"), firstDownload.SuggestedFilename,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch("[0-9a-f]{32,64}", firstDownload.SuggestedFilename);
        var firstBytes = await File.ReadAllBytesAsync(
            Assert.IsType<string>(await firstDownload.PathAsync()));
        AssertOperatorSafe(
            await support.Page.Locator("main").InnerTextAsync(),
            accepted.CaseId);

        // Exporting again is the same act, not a revision: same archive, same
        // name. The once-per-case proxy behind it is asserted in
        // CustodyOutboxIntegrationTests, which can read the row.
        var secondDownload = await ExportByKeyboardAsync(support.Page);
        Assert.Equal(firstDownload.SuggestedFilename, secondDownload.SuggestedFilename);
        Assert.Equal(
            SHA256.HashData(firstBytes),
            SHA256.HashData(await File.ReadAllBytesAsync(
                Assert.IsType<string>(await secondDownload.PathAsync()))));

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
        //
        // The signed-in identity is no longer part of this list. In the top bar
        // it sat inside the primary nav; in the rail it is its own named group,
        // which is what it always was — who you are is not a route. It is
        // asserted directly above through [aria-label='User'].
        AssertOrdered(
            navigation,
            "Work Centre",
            "Inbox",
            "Upload",
            "Cases",
            "Search",
            "Operations",
            "Administration");

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
        Assert.Equal("/Cases?tab=review", new Uri(support.Page.Url).PathAndQuery);

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

    [Fact]
    public async Task PageRenderedReasonDialogStaysReachableWhileOpen()
    {
        // PLAT-029: a reason dialog rendered inside the page (here the
        // remove-document dialog on the evidence tab), not in the shell, must
        // stay reachable while open: site.js sets inert on what is outside
        // the dialog, never on an ancestor of it. The close click is a real
        // pointer click, which an inert dialog would refuse. Same fixture as
        // the keyboard journey above, with script enabled.
        var repositoryFixture = RepositoryEvaFixture.Load();
        var vehicleEvidence = new BrowserVehicleEvidenceQueries();
        var caseDataState = new BrowserCaseDataState(repositoryFixture);
        await using var support = await BrowserTestSupport.StartAsync(
            width: 1440,
            height: 900,
            javaScriptEnabled: true,
            configureWebHost: builder =>
            {
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
        await SeedEligibleImageAsync(support.Services, accepted.CaseId, repositoryFixture);

        await support.GoToAsync($"/Cases/{accepted.CaseId:D}");
        await EnterEditModeByKeyboardAsync(support.Page);
        // CASE-012: the documents live on the case-files section of the
        // workspace; the remove-document dialog is the page-rendered reason
        // dialog this journey exercises.
        await support.GoToAsync($"/Cases/{accepted.CaseId:D}?section=case-files");

        var removeTrigger = support.Page.Locator("[data-dialog-open^='remove-doc-']").First;
        await removeTrigger.ClickAsync();
        var reasonDialog = support.Page.Locator("#" + await removeTrigger.GetAttributeAsync("data-dialog-open"));
        Assert.True(await reasonDialog.IsVisibleAsync());
        Assert.True(await reasonDialog.EvaluateAsync<bool>(
            "dialog => dialog.contains(document.activeElement) && dialog.closest('[inert]') === null"));
        Assert.True(await reasonDialog.Locator("button[type='submit']").IsEnabledAsync());
        await reasonDialog.Locator("[data-dialog-close]").First.ClickAsync();
        Assert.True(await reasonDialog.IsHiddenAsync());
        Assert.Equal(0, await support.Page.Locator("[inert]").CountAsync());
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

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static async Task EnterEditModeByKeyboardAsync(IPage page)
    {
        var button = page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Edit Case", Exact = true });
        await button.FocusAsync();
        await button.PressAsync("Enter");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        Assert.Contains("Finish editing", await page.Locator("main").InnerTextAsync(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Exports by keyboard alone and returns the file it answers with.
    ///
    /// CASE-012: the case's handoff is a dialog that needs script, and this
    /// journey runs without it, so the caller navigates to the Send page —
    /// the one route that still works with script off — before the export.
    /// The act itself is unchanged and must stay reachable and operable by
    /// keyboard: neither step carries a reason field, because an export is
    /// a label and a control, and the design authority bans copy beyond
    /// that.
    /// </summary>
    private static async Task<IDownload> ExportByKeyboardAsync(IPage page)
    {
        Assert.True(
            page.Url.Contains("/Eva/Send", StringComparison.OrdinalIgnoreCase),
            "The export journey must be on the Send page.");
        var button = page.GetByRole(
            AriaRole.Button,
            new PageGetByRoleOptions { Name = "Download export", Exact = true });
        Assert.True(await button.IsVisibleAsync(), await page.Locator("main").InnerTextAsync());
        Assert.True(await button.IsEnabledAsync(), await page.Locator("main").InnerTextAsync());
        var responseTask = page.WaitForResponseAsync(value =>
            value.Request.Method == "POST"
            && value.Url.Contains("/Documents/Export", StringComparison.OrdinalIgnoreCase));
        var downloadTask = page.WaitForDownloadAsync();
        await button.FocusAsync();
        await button.PressAsync("Enter");
        Assert.Equal(200, (await responseTask).Status);
        return await downloadTask;
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
