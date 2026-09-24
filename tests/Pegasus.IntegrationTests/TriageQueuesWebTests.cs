using System.Globalization;
using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Operations;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Cases page's workflow rail, Principal/Missing filters,
/// per-kind rows and the rule that closed Unidentified items are not counted
/// among open work. Unidentified-as-a-scope
/// and the Not ready merge across both origins stay covered here.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class TriageQueuesWebTests
{
    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.User]);

    /// <summary>
    /// The Missing filter is exclusive: "Instructions" lists the cases whose
    /// instruction is the only thing missing, "Images" the converse, and
    /// "Both missing" the remainder — an image-initiated row is
    /// instruction-missing with images present, so it is listed for All and
    /// Instructions only.
    /// </summary>
    [Fact]
    public async Task NotReadyMissingFilterReturnsOnlyTheMatchingRows()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var ticks = DateTime.UtcNow.Ticks % 1_000_000;
        var instructionOnly = $"QDOSA{ticks}";
        var imagesOnly = $"QDOSB{ticks}";
        var bothMissing = $"QDOSC{ticks}";
        await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "instruction-only.pdf"),
            instructionOnly,
            instructionComplete: false,
            imagesComplete: true);
        await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "images-only.pdf"),
            imagesOnly,
            instructionComplete: true,
            imagesComplete: false);
        await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "both-missing.pdf"),
            bothMissing,
            instructionComplete: false,
            imagesComplete: false);

        using var instructions = await client.GetAsync("/Cases?tab=not_ready&missing=instructions");
        var instructionsHtml = await instructions.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, instructions.StatusCode);
        Assert.Contains(instructionOnly, instructionsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(imagesOnly, instructionsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(bothMissing, instructionsHtml, StringComparison.Ordinal);

        using var images = await client.GetAsync("/Cases?tab=not_ready&missing=images");
        var imagesHtml = await images.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, images.StatusCode);
        Assert.Contains(imagesOnly, imagesHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(instructionOnly, imagesHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(bothMissing, imagesHtml, StringComparison.Ordinal);

        using var both = await client.GetAsync("/Cases?tab=not_ready&missing=both");
        var bothHtml = await both.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, both.StatusCode);
        Assert.Contains(bothMissing, bothHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(instructionOnly, bothHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(imagesOnly, bothHtml, StringComparison.Ordinal);

        using var all = await client.GetAsync("/Cases?tab=not_ready");
        var allHtml = await all.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Contains(instructionOnly, allHtml, StringComparison.Ordinal);
        Assert.Contains(imagesOnly, allHtml, StringComparison.Ordinal);
        Assert.Contains(bothMissing, allHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// Not ready and Awaiting instruction are separate row lists.
    /// Each rail count must equal its own row count, and the Work Centre's
    /// Not ready metric must equal the Not ready row count.
    /// </summary>
    [Fact]
    public async Task NotReadyAndAwaitingRailCountsMatchTheirRows()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var instructionCaseReference = "QDOS" + DateTime.UtcNow.Ticks % 1_000_000;
        await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "instruction-source.pdf"),
            instructionCaseReference);
        var imageIntake = await RegisterImageIntakeAsync(factory, client, services, "AB12CDE");
        var stages = await services.GetRequiredService<IDashboardQueries>()
            .GetCaseStageCountsAsync(CancellationToken.None);
        Assert.Equal(1, stages.NotReady);
        Assert.Equal(1, stages.AwaitingInstruction);
        Assert.Equal(0, stages.Complete);
        Assert.Equal(0, stages.Query);
        Assert.Equal(0, stages.Review);
        Assert.Equal(0, stages.Held);
        Assert.Equal(0, stages.WithEngineer);
        var triageCount = await services.GetRequiredService<IListTriage>().CountAsync(
            StaffActor(),
            state: null,
            cancellationToken: CancellationToken.None);
        var openUnidentifiedCount = await services.GetRequiredService<IUnidentifiedStore>()
            .CountOpenAsync(CancellationToken.None);
        Assert.Equal(0, triageCount);
        // The no-registration recognition fake reaches the image group's
        // terminal Unidentified route. Staff registration then establishes
        // Awaiting instruction without erasing that separate open exception.
        Assert.Equal(1, openUnidentifiedCount);
        var expectedShellCount = stages.NotReady
            + stages.Review
            + stages.WithEngineer
            + stages.Query
            + stages.Held
            + triageCount
            + openUnidentifiedCount;
        // Completed and Awaiting instruction intentionally are not shell work.
        Assert.Equal(2, expectedShellCount);

        using var notReady = await client.GetAsync("/Cases?tab=not_ready");
        var notReadyHtml = await notReady.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, notReady.StatusCode);

        // The rail scope button's count span: label span then figure span.
        var countMatch = Regex.Match(
            notReadyHtml,
            "scope-button[\\s\\S]*?<span>Not ready</span>\\s*<span>(\\d+)</span>");
        Assert.True(countMatch.Success, "Not ready rail scope markup not found.");
        var railCount = int.Parse(countMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        // Not ready now contains formal Cases only.
        Assert.Contains(instructionCaseReference, notReadyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(imageIntake.ImageIntakeReference, notReadyHtml, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(notReadyHtml, "data-cases-row=\""));
        Assert.Equal(1, railCount);

        using var awaiting = await client.GetAsync("/Cases?tab=awaiting");
        var awaitingHtml = await awaiting.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, awaiting.StatusCode);
        Assert.DoesNotContain(instructionCaseReference, awaitingHtml, StringComparison.Ordinal);
        Assert.Contains(imageIntake.ImageIntakeReference, awaitingHtml, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(awaitingHtml, "data-cases-row=\""));
        var awaitingCount = Regex.Match(
            awaitingHtml,
            "scope-button[\\s\\S]*?<span>Awaiting instruction</span>\\s*<span>(\\d+)</span>");
        Assert.True(awaitingCount.Success, "Awaiting instruction rail scope markup not found.");
        Assert.Equal(1, int.Parse(awaitingCount.Groups[1].Value, CultureInfo.InvariantCulture));
        var shellCount = Regex.Match(
            notReadyHtml,
            "<span>Cases</span>\\s*<span class=\"nav-count\" aria-label=\"(\\d+) outstanding\"");
        Assert.True(shellCount.Success, "Cases shell count markup not found.");
        // Awaiting instruction has its own tab but remains pre-Case work. The
        // shell count includes only the FRD's Case and exception queues.
        Assert.Equal(
            expectedShellCount,
            int.Parse(shellCount.Groups[1].Value, CultureInfo.InvariantCulture));

        // The Work Centre's Not ready metric reads the same count query, so
        // it must report the identical figure — a rail count that disagrees
        // with its own metric is exactly the defect being fixed here.
        using var dashboard = await client.GetAsync("/");
        var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        var tileMatch = Regex.Match(
            dashboardHtml,
            "data-value=\"not_ready\"[\\s\\S]*?metric-value\">(\\d+)</span>");
        Assert.True(tileMatch.Success, "Work Centre Not ready metric markup not found.");
        Assert.Equal(railCount, int.Parse(tileMatch.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task WorkflowQueuesKeepCompletedAndQuerySeparateAndTheShellCountsQueryOnly()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var suffix = DateTime.UtcNow.Ticks % 1_000_000;
        var notReadyReference = $"QDOSN{suffix}";
        var completeReference = $"QDOSC{suffix}";
        var queryReference = $"QDOSQ{suffix}";

        await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "query-not-ready.pdf"),
            notReadyReference);
        var completeId = await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "query-complete.pdf"),
            completeReference);
        var queryId = await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "query-open.pdf"),
            queryReference);
        await SetWorkflowStateAsync(services, completeId, CaseLifecycleState.PostReportComplete);
        await SetWorkflowStateAsync(services, queryId, CaseLifecycleState.Query);
        await RegisterImageIntakeAsync(factory, client, services, "AB12CDE");

        var stages = await services.GetRequiredService<IDashboardQueries>()
            .GetCaseStageCountsAsync(CancellationToken.None);
        Assert.Equal(1, stages.NotReady);
        Assert.Equal(1, stages.Complete);
        Assert.Equal(1, stages.Query);
        Assert.Equal(1, stages.AwaitingInstruction);
        Assert.Equal(0, stages.Review);
        Assert.Equal(0, stages.Held);
        Assert.Equal(0, stages.WithEngineer);
        var triageCount = await services.GetRequiredService<IListTriage>().CountAsync(
            StaffActor(),
            state: null,
            cancellationToken: CancellationToken.None);
        var openUnidentifiedCount = await services.GetRequiredService<IUnidentifiedStore>()
            .CountOpenAsync(CancellationToken.None);
        Assert.Equal(0, triageCount);
        // The no-registration recognition fake reaches the image group's
        // terminal Unidentified route. Staff registration then establishes
        // Awaiting instruction without erasing that separate open exception.
        Assert.Equal(1, openUnidentifiedCount);
        var expectedShellCount = stages.NotReady
            + stages.Review
            + stages.WithEngineer
            + stages.Query
            + stages.Held
            + triageCount
            + openUnidentifiedCount;
        // Completed and Awaiting instruction intentionally are not shell work.
        Assert.Equal(3, expectedShellCount);

        using var queryResponse = await client.GetAsync("/Cases?tab=query");
        var queryHtml = await queryResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        Assert.Contains(queryReference, queryHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(completeReference, queryHtml, StringComparison.Ordinal);
        Assert.Equal(1, QueueCount(queryHtml, "Query"));
        Assert.Equal(expectedShellCount, ShellCasesCount(queryHtml));

        using var completeResponse = await client.GetAsync("/Cases?tab=complete");
        var completeHtml = await completeResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        Assert.Contains(completeReference, completeHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(queryReference, completeHtml, StringComparison.Ordinal);
        Assert.Equal(1, QueueCount(completeHtml, "Completed"));
        Assert.Equal(expectedShellCount, ShellCasesCount(completeHtml));
    }

    [Fact]
    public async Task CasesShellCountsOpenTriageAndUnidentifiedButExcludesClosedUnidentified()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var register = services.GetRequiredService<IRegisterUnidentified>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();

        await OpenTriageForPagingAsync(services, 1);
        var open = await register.ExecuteAsync(
            new(
                UnidentifiedOrigin.Receipt(await StoreMinimalReceiptAsync(services, "open-unidentified.pdf")),
                UnidentifiedReasonCode.UnreadableOrCorruptContent,
                "The item remains open for the queue boundary test.",
                ActionActor.SystemWorker("test-worker"),
                $"open-unidentified:{Guid.NewGuid():N}",
                now),
            CancellationToken.None);
        var closed = await register.ExecuteAsync(
            new(
                UnidentifiedOrigin.Receipt(await StoreMinimalReceiptAsync(services, "closed-unidentified.pdf")),
                UnidentifiedReasonCode.UnreadableOrCorruptContent,
                "The item is closed for the queue boundary test.",
                ActionActor.SystemWorker("test-worker"),
                $"closed-unidentified:{Guid.NewGuid():N}",
                now),
            CancellationToken.None);
        await services.GetRequiredService<ICloseUnidentified>().ExecuteAsync(
            new(
                closed.Item.Id,
                closed.Item.Version,
                StaffActor(),
                $"close-unidentified:{Guid.NewGuid():N}",
                "No further action is required.",
                now),
            CancellationToken.None);

        var unidentified = services.GetRequiredService<IUnidentifiedStore>();
        Assert.Equal(1, await services.GetRequiredService<IListTriage>().CountAsync(
            StaffActor(),
            state: null,
            cancellationToken: CancellationToken.None));
        Assert.Equal(1, await unidentified.CountOpenAsync(CancellationToken.None));
        Assert.Single(await unidentified.ListClosedQueueAsync(null, CancellationToken.None));

        using var openResponse = await client.GetAsync("/Cases?tab=unidentified");
        var openHtml = await openResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, openResponse.StatusCode);
        // Match the row's detail link, not the bare reference: a short reference
        // such as "U1" can occur inside an antiforgery token.
        Assert.Contains($"href=\"/Unidentified/{open.Item.Id:D}\"", openHtml, StringComparison.Ordinal);
        Assert.DoesNotContain($"href=\"/Unidentified/{closed.Item.Id:D}\"", openHtml, StringComparison.Ordinal);
        Assert.Equal(1, QueueCount(openHtml, "Unidentified"));
        Assert.Equal(2, ShellCasesCount(openHtml));

        using var closedResponse = await client.GetAsync("/Cases?tab=unidentified&show=closed");
        var closedHtml = await closedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, closedResponse.StatusCode);
        Assert.Contains($"href=\"/Unidentified/{closed.Item.Id:D}\"", closedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain($"href=\"/Unidentified/{open.Item.Id:D}\"", closedHtml, StringComparison.Ordinal);
        Assert.Equal(1, QueueCount(closedHtml, "Unidentified"));
        Assert.Equal(2, ShellCasesCount(closedHtml));
    }

    /// <summary>
    /// An image-initiated row carries its retained-image count and its
    /// derived chase state (<c>ImageIntakeChaseSchedule</c>): a
    /// record registered moments ago is well inside the seven-day window, so
    /// it must read "Not yet due" rather than "Chase due" — the boundary
    /// itself is covered at the Core level
    /// (<c>ImageIntakeChaseScheduleTests</c>).
    /// </summary>
    [Fact]
    public async Task AwaitingImageRowRendersRetainedImageCountSourceAndChaseState()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var imageIntake = await RegisterImageIntakeAsync(factory, client, services, "CD34EFG");

        using var response = await client.GetAsync("/Cases?tab=awaiting");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
        Assert.Contains(imageIntake.NormalizedVehicleRegistration, html, StringComparison.Ordinal);
        Assert.Contains("1 retained image", html, StringComparison.Ordinal);
        Assert.Contains("Storing", html, StringComparison.Ordinal);
        Assert.Contains("Manual upload", html, StringComparison.Ordinal);
        Assert.Contains("Not yet due", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Chase due", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TriageRowRendersTheTriageReferenceRegistrationProviderAndAssignee()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        // The provider's claim number, which used to be what the row called
        // the reference. The row's reference is now the Triage Case's Case/PO.
        const string claimNumber = "TRIAGE-032";
        const string registration = "TR32AGE";
        const string provider = "QDOS";
        var sourceIdentity = new IntakeSourceIdentity(
            IntakeSourceChannel.ManualUpload,
            Guid.NewGuid().ToString("N"));
        var sourceHash = new string('a', 64);
        var acceptedMatch = new IntakeEvidence(
            IntakeEvidenceSource.SystemDefault,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            registration,
            "Accepted Triage match for the queue-row test.",
            MatcherKey: "queue-filter-test",
            MatcherVersion: 1);
        var receiptId = await StoreMinimalReceiptAsync(
            services,
            "triage-row.pdf",
            new InstructionDraft(
                SuggestedPrincipalCode: provider,
                ClaimantName: null,
                ClaimNumber: claimNumber,
                VehicleRegistration: registration,
                VehicleMake: null,
                VehicleModel: null,
                VehicleMileage: null,
                AccidentCircumstances: null,
                DateOfIncident: null,
                InstructionDate: null,
                InspectionAddress: null),
            [acceptedMatch],
            sourceIdentity,
            sourceHash);
        var evaluationRevisionId = await StageAndCompleteEvaluationAsync(services, receiptId);
        var triage = await services.GetRequiredService<ICreateTriageFromIntake>().ExecuteAsync(
            new(
                new TriageOrigin(receiptId, sourceIdentity, sourceHash, evaluationRevisionId),
                registration,
                acceptedMatch,
                ActionActor.SystemWorker("test-worker"),
                $"triage-create:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var actor = StaffActor();
        var triageEditLeaseToken = (await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.Triage, triage.CaseId, triage.Version, actor,
                $"triage-assign-edit:{Guid.NewGuid():N}"), CancellationToken.None)).Token;
        await services.GetRequiredService<IAssignTriage>().ExecuteAsync(
            new(
                triage.CaseId,
                triage.Version,
                DevelopmentOfflineIdentity.AdministratorId,
                actor,
                $"triage-assign:{Guid.NewGuid():N}",
                "Assigned for the queue-row test.")
            {
                EditLeaseToken = triageEditLeaseToken
            },
            CancellationToken.None);

        using var response = await client.GetAsync("/Cases?tab=triage");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The factory clock is in 2031: the first QDOS Case/PO of the year.
        const string triageReference = "t.QDOS31001";
        Assert.Equal(triageReference, triage.Reference);
        Assert.Contains(triageReference, html, StringComparison.Ordinal);
        Assert.Contains($"/Cases/{triage.CaseId:D}", html, StringComparison.Ordinal);
        Assert.Contains(registration, html, StringComparison.Ordinal);
        Assert.Contains(provider, html, StringComparison.Ordinal);
        // The table gives Provider and Assignee their own cells.
        Assert.Contains($"<td>{provider}</td>", html, StringComparison.Ordinal);
        Assert.Contains($"<td>{DevelopmentOfflineIdentity.UserName}</td>", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        // The claim number is retained on the summary as its own member, and
        // is no longer what the row calls the reference.
        var summary = Assert.Single(
            await services.GetRequiredService<ITriageQueries>()
                .ListAsync(null, CancellationToken.None));
        Assert.Equal(claimNumber, summary.ClaimNumber);
        Assert.Equal(triageReference, summary.Reference);
    }

    [Fact]
    public async Task UnidentifiedRouteRedirectsPermanentlyToTheQueuesTab()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Unidentified");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/Cases?tab=unidentified", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task UnidentifiedTabRendersNoBannedVocabularyOrRawIdentifiers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receiptStore = services.GetRequiredService<IIntakeReceiptStore>();
        var register = services.GetRequiredService<IRegisterUnidentified>();
        // Use the host clock to keep the test data deterministic.
        var receivedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();

        var receipt = await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                "unreadable-document.pdf",
                "application/pdf",
                2048,
                Guid.NewGuid().ToString("N"),
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
                receivedAt,
                receivedAt,
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
        await register.ExecuteAsync(
            new(
                UnidentifiedOrigin.Receipt(receipt.Id),
                UnidentifiedReasonCode.UnreadableOrCorruptContent,
                "The document could not be read.",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-web-test:{Guid.NewGuid():N}",
                receivedAt),
            CancellationToken.None);

        using var response = await client.GetAsync("/Cases?tab=unidentified");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unidentified", html, StringComparison.Ordinal);
        Assert.Contains("unreadable-document.pdf", html, StringComparison.Ordinal);
        Assert.DoesNotContain("intake", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("custody", html, StringComparison.OrdinalIgnoreCase);

        // A GUID legitimately appears in the row link's href and in the
        // freshness form's hidden `selected` input (both routing state the
        // operator never sees as text); the design rule bans it from what
        // the operator reads. Strip attribute values and hidden inputs
        // before scanning so only visible text is checked.
        var visibleOnly = Regex.Replace(
            html,
            "<input[^>]*type=\"hidden\"[^>]*>|\\s(href|asp-route-\\w+|data-[\\w-]+)=\"[^\"]*\"",
            "");
        Assert.False(
            Regex.IsMatch(visibleOnly, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"),
            "A raw GUID must never reach the operator-visible text of the Unidentified tab.");
    }

    /// <summary>
    /// A reasoned refusal is a closed Unidentified item, not an intake
    /// decision. The Cases tab lists open Unidentified items only.
    /// </summary>
    [Fact]
    public async Task UnidentifiedTabExcludesAClosedItemAndKeepsItsCountAtZero()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receiptStore = services.GetRequiredService<IIntakeReceiptStore>();
        // Use the host clock to keep the test data deterministic.
        var receivedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();

        var receipt = await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                "refused-file.msg",
                "message/rfc822",
                2048,
                Guid.NewGuid().ToString("N"),
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
                receivedAt,
                receivedAt,
                "test-actor",
                IntakeDecision.NeedsSorting,
                "requires staff decision",
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
        var registered = await services.GetRequiredService<IRegisterUnidentified>().ExecuteAsync(
            new RegisterUnidentifiedRequest(
                UnidentifiedOrigin.Receipt(receipt.Id),
                UnidentifiedReasonCode.AmbiguousOwnershipOrDestination,
                "The source needs a decision.",
                ActionActor.SystemWorker("test-intake"),
                Guid.NewGuid().ToString("N"),
                receivedAt));
        var closed = await services.GetRequiredService<ICloseUnidentified>().ExecuteAsync(
            new CloseUnidentifiedRequest(
                registered.Item.Id,
                registered.Item.Version,
                StaffActor(),
                Guid.NewGuid().ToString("N"),
                "The material is not an instruction for this office.",
                receivedAt));
        Assert.True(closed.Item.IsClosed);
        Assert.Equal(registered.Item.Reference, closed.Item.Reference);

        using var response = await client.GetAsync("/Cases?tab=unidentified");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("refused-file.msg", html, StringComparison.Ordinal);
        // The reference as text on the page; a short reference such as "U1"
        // can occur by chance inside the random antiforgery token.
        Assert.DoesNotMatch($">\\s*{Regex.Escape(registered.Item.Reference)}\\s*<", html);
        Assert.DoesNotContain("Open received item", html, StringComparison.Ordinal);

        // Zero open Unidentified items: the scope count reads zero.
        var countMatch = Regex.Match(
            html,
            "scope-button[\\s\\S]*?<span>Unidentified</span>\\s*<span>(\\d+)</span>");
        Assert.True(countMatch.Success, "Unidentified rail scope markup not found.");
        Assert.Equal(0, int.Parse(countMatch.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Not ready is one row list across both case origins, with
    /// dropdown filters rather than pills, and the rail replaces the old tab
    /// strip.
    /// </summary>
    [Fact]
    public async Task NotReadyAndAwaitingRenderSeparateRowLists()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var instructionCaseReference = "QDOS" + DateTime.UtcNow.Ticks % 1_000_000;
        await SeedNotReadyCaseAsync(
            services,
            await StoreMinimalReceiptAsync(services, "instruction-source.pdf"),
            instructionCaseReference);
        var imageIntake = await RegisterImageIntakeAsync(factory, client, services, "EF56GHJ");

        using var response = await client.GetAsync("/Cases?tab=not_ready");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(instructionCaseReference, html, StringComparison.Ordinal);
        Assert.DoesNotContain(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
        // v26 decision L: the scope lists its rows as a table.
        Assert.Contains("<table", html, StringComparison.Ordinal);
        Assert.DoesNotContain("subtabs", html, StringComparison.Ordinal);
        // The rail groups the workflow; the filters are selects.
        Assert.DoesNotContain(">Case workflow<", html, StringComparison.Ordinal);
        Assert.Contains("Workflow", html, StringComparison.Ordinal);
        Assert.Contains("Exceptions", html, StringComparison.Ordinal);
        Assert.Contains("name=\"principal\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"missing\"", html, StringComparison.Ordinal);
        using var awaiting = await client.GetAsync("/Cases?tab=awaiting");
        var awaitingHtml = await awaiting.Content.ReadAsStringAsync();
        Assert.Contains(imageIntake.ImageIntakeReference, awaitingHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(instructionCaseReference, awaitingHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Awaiting definitive instruction", awaitingHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AwaitingSecondRowSelectionShowsThatRowsQuickDetailWithoutScript()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var selected = await RegisterImageIntakeAsync(factory, client, services, "GH67JKL");
        _ = await RegisterImageIntakeAsync(factory, client, services, "MN89PQR");

        using var response = await client.GetAsync($"/Cases?tab=awaiting&selected={selected.Id:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"selected={selected.Id:D}", html, StringComparison.OrdinalIgnoreCase);
        Assert.Matches(
            $"<h2>{selected.ImageIntakeReference}[^<]*{selected.NormalizedVehicleRegistration}</h2>",
            html);
    }

    [Fact]
    public async Task AwaitingNonexistentSelectionReturnsNotFound()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        _ = await RegisterImageIntakeAsync(factory, client, scope.ServiceProvider, "QR12STU");

        using var response = await client.GetAsync($"/Cases?tab=awaiting&selected={Guid.NewGuid():D}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AwaitingAttachMovesTheImageIntakeToAnExistingCase()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY34ZZZ", "ATTACH-REF-01");
        var reference = await CaseReferenceAsync(services, caseId);
        var imageIntake = await RegisterImageIntakeAsync(factory, client, services, "ST12UVW");

        using var response = await PostAttachAsync(
            client,
            imageIntake.Id,
            imageIntake.Origin.ReceiptId,
            reference,
            "Staff matched the images to the instructed case.");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var redirected = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, redirected.StatusCode);
        var html = await redirected.Content.ReadAsStringAsync();
        Assert.Contains($"This was added to case {reference}.", html, StringComparison.Ordinal);
        Assert.DoesNotContain(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AwaitingIncompleteConfirmationIsVisibleAndLeavesTheRowInPlace()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var imageIntake = await RegisterImageIntakeAsync(
            factory, client, scope.ServiceProvider, "WX34YZA");

        using var response = await PostIncompleteAttachAsync(
            client, imageIntake.Id, imageIntake.Origin.ReceiptId, "UNKNOWN");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("This confirmation is incomplete. Refresh and try again.", html, StringComparison.Ordinal);
        Assert.Contains(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AwaitingCountExcludesReceiptLinkedBeforeMergeSynchronises()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var caseReceiptId = await StoreMinimalReceiptAsync(services, "linked-case.pdf");
        var caseId = await SeedNotReadyCaseAsync(services, caseReceiptId, "QDOSCASE042");
        var imageIntake = await RegisterImageIntakeAsync(factory, client, services, "BC56DEF");
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeManualAssociations (IntakeReceiptId, CaseId, IsActive, Version, LinkedAtUtc, ActorKind, ActorSubjectId, ActorRolesJson, Reason, LastOperationKey) VALUES ({imageIntake.Origin.ReceiptId}, {caseId}, {true}, {0L}, {DateTimeOffset.UtcNow}, {"Staff"}, {Guid.NewGuid().ToString("D")}, {"[]"}, {"Linked before image merge synchronisation"}, {$"attach-linked:{Guid.NewGuid():N}"})");
        }

        using var response = await client.GetAsync("/Cases?tab=awaiting");
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
        var count = Regex.Match(
            html,
            "scope-button[\\s\\S]*?<span>Awaiting instruction</span>\\s*<span>(\\d+)</span>");
        Assert.True(count.Success);
        Assert.Equal(Regex.Count(html, "data-cases-row=\""), int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The row list renders newest received first (the queue filters' default order,
    /// kept by the workflow rail's single order).
    /// </summary>
    [Fact]
    public async Task NotReadyRowsRenderNewestReceivedFirst()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var olderReceiptId = await StoreMinimalReceiptAsync(services, "older-source.pdf");
        var newerReceiptId = await StoreMinimalReceiptAsync(services, "newer-source.pdf");
        var ticks = DateTime.UtcNow.Ticks % 1_000_000;
        var olderReference = $"QDOSA{ticks}";
        var newerReference = $"QDOSB{ticks}";
        await SeedNotReadyCaseAsync(services, olderReceiptId, olderReference);
        await SeedNotReadyCaseAsync(services, newerReceiptId, newerReference);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IntakeReceipts SET ReceivedAtUtc = {new DateTimeOffset(2031, 5, 1, 9, 0, 0, TimeSpan.Zero)} WHERE Id = {olderReceiptId}");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IntakeReceipts SET ReceivedAtUtc = {new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero)} WHERE Id = {newerReceiptId}");
        }

        using var response = await client.GetAsync("/Cases?tab=not_ready");
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            html.IndexOf($">{newerReference}</a>", StringComparison.Ordinal)
                < html.IndexOf($">{olderReference}</a>", StringComparison.Ordinal),
            "The row order must put the newest received case first.");
    }

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>
    /// Registers one Image-initiated Case from a fresh upload — the same
    /// sequence every Not ready merge test needs.
    /// </summary>
    /// <summary>
    /// The Awaiting-instruction read carries each row's principal code out of
    /// its one set-based projection as a LEFT JOIN, so the number of database
    /// reads one request performs does not grow with the number of rows.
    /// </summary>
    /// <remarks>
    /// The proof is the comparison, not a pinned number: an absolute count is
    /// a fact about one base and stops being evidence the moment anything else
    /// on the page changes, whereas the same request over three rows and over
    /// six rows must cost the same reads if and only if nothing is read per
    /// row. The observed count travels in the assertion message so a run
    /// records it.
    /// </remarks>
    [Fact]
    public async Task AwaitingReadCountDoesNotGrowWithTheNumberOfImageRows()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var alpha = await ImageIntakeTestData.SeedPrincipalAsync(factory.Services, "ALPHA");
        var store = services.GetRequiredService<IImageIntakeStore>();
        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var actor = StaffActor();

        // Three rows of mixed principal state: one recorded, two `Not known`.
        var first = await RegisterImageIntakeAsync(factory, client, services, "AA11AAA");
        await RegisterImageIntakeAsync(factory, client, services, "BB22BBB");
        await RegisterImageIntakeAsync(factory, client, services, "CC33CCC");
        var firstDetail = Assert.IsType<ImageIntakeDetail>(
            await queries.GetAsync(first.Id, CancellationToken.None));
        var imageEditLeaseToken = (await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.ImageIntake, first.Id, firstDetail.LifecycleVersion, actor,
                $"image-intake-principal-edit:{Guid.NewGuid():N}"), CancellationToken.None)).Token;
        await store.SetPrincipalAsync(
            new(first.Id, alpha, actor, firstDetail.LifecycleVersion)
            {
                EditLeaseToken = imageEditLeaseToken
            },
            CancellationToken.None);

        var summaries = await queries.ListAsync(false, CancellationToken.None);
        Assert.Contains(summaries, item => item.PrincipalCode == "ALPHA");
        Assert.Contains(summaries, item => item.Id != first.Id && item.PrincipalCode is null);

        var counter = new AwaitingRequestCommandCounter();
        using var countingFactory = CreateCountingFactory(factory, counter);
        using var countingClient = countingFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost:7139")
            });
        var withThreeRows = await MeasureAwaitingReadsAsync(countingClient, counter, first.Id);

        // Three more rows, same request.
        await RegisterImageIntakeAsync(factory, client, services, "DD44DDD");
        await RegisterImageIntakeAsync(factory, client, services, "EE55EEE");
        await RegisterImageIntakeAsync(factory, client, services, "FF66FFF");
        var withSixRows = await MeasureAwaitingReadsAsync(countingClient, counter, first.Id);

        Assert.True(withThreeRows > 0, "The interceptor observed no reads at all.");
        Assert.True(
            withThreeRows == withSixRows,
            $"The Awaiting read count grew with the rows: {withThreeRows} reads for three "
            + $"image rows and {withSixRows} for six.");
    }

    private static WebApplicationFactory<Program> CreateCountingFactory(
        IntakeWebApplicationFactory factory,
        AwaitingRequestCommandCounter counter)
    {
        var connectionString = factory.Database.ConnectionString;
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            // The host's own context factory is what the request pipeline
            // resolves; an interceptor anywhere else counts nothing a request
            // does.
            services.RemoveAll<IDbContextFactory<PegasusDbContext>>();
            services.RemoveAll<DbContextOptions<PegasusDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.AddDbContextFactory<PegasusDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
                options.AddInterceptors(counter);
            });
        }));
    }

    private static async Task<int> MeasureAwaitingReadsAsync(
        HttpClient client,
        AwaitingRequestCommandCounter counter,
        Guid selectedId)
    {
        counter.Reset();
        using var response = await client.GetAsync($"/Cases?tab=awaiting&selected={selectedId:D}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return counter.ExecutedReaderCommands;
    }

    /// <summary>
    /// Counts the reader commands one request executes. Registered on the
    /// host's own context factory, so it observes exactly what the request
    /// pipeline runs.
    /// </summary>
    private sealed class AwaitingRequestCommandCounter : DbCommandInterceptor
    {
        private int executedReaderCommands;

        public int ExecutedReaderCommands => Volatile.Read(ref executedReaderCommands);

        public void Reset() => Interlocked.Exchange(ref executedReaderCommands, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref executedReaderCommands);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private static async Task<ImageIntakeRecord> RegisterImageIntakeAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        IServiceProvider services,
        string registration)
    {
        var imageUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            TinyPng,
            Guid.NewGuid().ToString("N"));
        var imageReceiptId = IntakeWebDriver.ReceiptId(imageUpload);
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(imageReceiptId, CancellationToken.None);
        return await register.ExecuteAsync(
            new(
                origin!,
                registration,
                StaffActor(),
                $"image-intake-register:{Guid.NewGuid():N}",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);
    }

    /// <summary>
    /// Stages and completes one durable intake work item so the receipt has a
    /// real <c>IntakeEvaluations</c> row, the FK <see cref="TriageOrigin"/>
    /// requires. Mirrors the queued-intake completion path
    /// (<c>IIntakeWorkStore.ReceiveAsync</c>/<c>CompleteProcessingAsync</c>)
    /// without going through the full mail-decision pipeline.
    /// </summary>
    internal static async Task<Guid> StageAndCompleteEvaluationAsync(IServiceProvider services, Guid processedReceiptId)
    {
        var workStore = services.GetRequiredService<IIntakeWorkStore>();
        var now = DateTimeOffset.UtcNow;
        var staged = new IntakeStagedReceipt(
            Guid.NewGuid(),
            "triage-row-evaluation.pdf",
            "application/pdf",
            1024,
            Guid.NewGuid().ToString("N"),
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
            now,
            "test-actor",
            $"test-storage-key/{Guid.NewGuid():N}",
            now);
        await workStore.ReceiveAsync(staged, $"triage-row-evaluation-receive:{Guid.NewGuid():N}", CancellationToken.None);
        var dispatchClaim = await workStore.ClaimDispatchAsync(now, TimeSpan.FromMinutes(1), CancellationToken.None)
            ?? throw new InvalidOperationException("Expected the staged evaluation work item to be claimable.");
        await workStore.MarkDispatchedAsync(dispatchClaim.Id, dispatchClaim.LeaseToken!, now, CancellationToken.None);
        var processingClaim = await workStore.ClaimProcessingAsync(staged.Id, now, TimeSpan.FromMinutes(1), CancellationToken.None)
            ?? throw new InvalidOperationException("Expected the dispatched evaluation work item to be claimable for processing.");
        var evaluation = await workStore.RecordEvaluationAsync(
            processingClaim.WorkItem.Id,
            processingClaim.WorkItem.LeaseToken!,
            processedReceiptId,
            now,
            false,
            CancellationToken.None);
        await workStore.CompleteProcessingAsync(processingClaim.WorkItem.Id,
            processingClaim.WorkItem.LeaseToken!, now, CancellationToken.None);
        return evaluation.Id;
    }

    /// <summary>
    /// The keyset continuation over real SQL: the pages partition the list in
    /// the newest-first order with no repeat and no gap, the database applies
    /// both the bound and the limit, and a cursor minted for one query is
    /// refused by another.
    /// </summary>
    [Fact]
    public async Task TriageKeysetPagesArePartitionedDeterministicallyOverRealSql()
    {
        const int total = 7;
        const int limit = 3;
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        for (var index = 0; index < total; index++)
        {
            await OpenTriageForPagingAsync(services, index);
        }

        var queries = services.GetRequiredService<ITriageQueries>();
        var listPage = new ListTriagePage(
            queries,
            services.GetRequiredService<ICursorProtector>());
        var actor = StaffActor();

        var seen = new List<TriageSummary>();
        string? cursor = null;
        var requests = 0;
        do
        {
            var page = await listPage.ExecuteAsync(
                new(actor, State: null, cursor, limit),
                CancellationToken.None);
            Assert.True(page.Items.Count <= limit);
            seen.AddRange(page.Items);
            cursor = page.NextCursor;
            requests++;
            Assert.True(requests <= total, "The continuation did not terminate.");
        }
        while (cursor is not null);

        // Every Triage exactly once, in the same newest-first order the
        // unpaged read returns.
        var expected = await queries.ListAsync(null, CancellationToken.None);
        Assert.Equal(total, seen.Count);
        Assert.Equal(
            expected.Select(item => item.Id).ToArray(),
            seen.Select(item => item.Id).ToArray());
        Assert.Equal(total, seen.Select(item => item.Id).Distinct().Count());

        // A cursor is bound to its query: the same page under a state filter
        // is a different scope and is refused rather than answered.
        var firstPage = await listPage.ExecuteAsync(
            new(actor, State: null, null, limit),
            CancellationToken.None);
        Assert.NotNull(firstPage.NextCursor);
        await Assert.ThrowsAsync<CursorRejectedException>(() =>
            listPage.ExecuteAsync(
                new(actor, TriageState.Open, firstPage.NextCursor, limit),
                CancellationToken.None));
    }

    private static async Task OpenTriageForPagingAsync(IServiceProvider services, int index)
    {
        var registration = $"PG{index:00}AGE";
        var sourceIdentity = new IntakeSourceIdentity(
            IntakeSourceChannel.ManualUpload,
            Guid.NewGuid().ToString("N"));
        var sourceHash = new string((char)('a' + (index % 6)), 64);
        var acceptedMatch = new IntakeEvidence(
            IntakeEvidenceSource.SystemDefault,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            registration,
            "Accepted Triage match for the keyset paging test.",
            MatcherKey: "triage-paging-test",
            MatcherVersion: 1);
        var receiptId = await StoreMinimalReceiptAsync(
            services,
            $"triage-paging-{index}.pdf",
            new InstructionDraft(
                SuggestedPrincipalCode: "QDOS",
                ClaimantName: null,
                ClaimNumber: $"PAGING-{index:00}",
                VehicleRegistration: registration,
                VehicleMake: null,
                VehicleModel: null,
                VehicleMileage: null,
                AccidentCircumstances: null,
                DateOfIncident: null,
                InstructionDate: null,
                InspectionAddress: null),
            [acceptedMatch],
            sourceIdentity,
            sourceHash);
        var evaluationRevisionId = await StageAndCompleteEvaluationAsync(services, receiptId);
        await services.GetRequiredService<ICreateTriageFromIntake>().ExecuteAsync(
            new(
                new TriageOrigin(receiptId, sourceIdentity, sourceHash, evaluationRevisionId),
                registration,
                acceptedMatch,
                ActionActor.SystemWorker("test-worker"),
                $"triage-paging-create:{Guid.NewGuid():N}"),
            CancellationToken.None);
    }

    internal static async Task<Guid> StoreMinimalReceiptAsync(
        IServiceProvider services,
        string sourceFileName,
        InstructionDraft? instructionDraft = null,
        IReadOnlyList<IntakeEvidence>? evidence = null,
        IntakeSourceIdentity? sourceIdentity = null,
        string? sourceHash = null)
    {
        var receiptStore = services.GetRequiredService<IIntakeReceiptStore>();
        var receipt = await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                sourceFileName,
                "application/pdf",
                1024,
                sourceHash ?? Guid.NewGuid().ToString("N"),
                sourceIdentity ?? new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                "test-actor",
                IntakeDecision.NeedsSorting,
                "test decision reason",
                evidence ?? [],
                [],
                instructionDraft,
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

    private static async Task<string> CaseReferenceAsync(IServiceProvider services, Guid caseId)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await context.Cases
            .Where(item => item.Id == caseId)
            .Select(item => item.Reference)
            .SingleAsync();
    }

    private static async Task SetWorkflowStateAsync(
        IServiceProvider services,
        Guid caseId,
        CaseLifecycleState state)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {state.ToString()} WHERE CaseId = {caseId}");
    }

    private static int QueueCount(string html, string label)
    {
        var match = Regex.Match(
            html,
            "scope-button[\\s\\S]*?<span>" + Regex.Escape(label) + "</span>\\s*<span>(\\d+)</span>");
        Assert.True(match.Success, $"{label} rail scope markup not found.");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static int ShellCasesCount(string html)
    {
        var match = Regex.Match(
            html,
            "<span>Cases</span>\\s*<span class=\"nav-count\" aria-label=\"(\\d+) outstanding\"");
        Assert.True(match.Success, "Cases shell count markup not found.");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static async Task<HttpResponseMessage> PostAttachAsync(
        HttpClient client,
        Guid id,
        Guid receiptId,
        string reference,
        string reason)
    {
        using var surface = await client.GetAsync($"/Cases?tab=awaiting&selected={id:D}");
        Assert.Equal(HttpStatusCode.OK, surface.StatusCode);
        var surfaceHtml = await surface.Content.ReadAsStringAsync();
        var receiptVersion = InputValue(surfaceHtml, "receiptVersion");
        var operationId = Guid.NewGuid().ToString("D");
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var prepared = await client.PostAsync(
            "/Cases?handler=Attach",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id.ToString("D"),
                ["receiptId"] = receiptId.ToString("D"),
                ["operationId"] = operationId,
                ["receiptVersion"] = receiptVersion,
                ["reference"] = reference,
                ["reason"] = reason
            }));
        Assert.Equal(HttpStatusCode.OK, prepared.StatusCode);
        var preparedHtml = await prepared.Content.ReadAsStringAsync();
        var caseVersion = long.Parse(InputValue(preparedHtml, "caseVersion"), CultureInfo.InvariantCulture);
        return await client.PostAsync(
            "/Cases?handler=Attach",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id.ToString("D"),
                ["receiptId"] = receiptId.ToString("D"),
                ["operationId"] = operationId,
                ["receiptVersion"] = receiptVersion,
                ["caseId"] = InputValue(preparedHtml, "caseId"),
                ["caseVersion"] = caseVersion.ToString(CultureInfo.InvariantCulture),
                ["reference"] = reference,
                ["reason"] = reason
            }));
    }

    private static async Task<HttpResponseMessage> PostIncompleteAttachAsync(
        HttpClient client,
        Guid id,
        Guid receiptId,
        string reference)
    {
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        return await client.PostAsync(
            "/Cases?handler=Attach",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id.ToString("D"),
                ["receiptId"] = receiptId.ToString("D"),
                ["reference"] = reference
            }));
    }

    private static string InputValue(string html, string name)
    {
        var match = Regex.Match(
            html,
            $"<input\\b(?=[^>]*\\bname=\\\"{Regex.Escape(name)}\\\")(?=[^>]*\\bvalue=\\\"(?<value>[^\\\"]*)\\\")[^>]*>",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Expected hidden input '{name}' in rendered confirmation.");
        return WebUtility.HtmlDecode(match.Groups["value"].Value);
    }

    /// <summary>
    /// A raw-SQL Not-ready Case fixture: exercising the full instruction
    /// pipeline just to get one NotReady case row is unrelated to what these
    /// tests verify (the queue reads whatever the Cases table holds). The
    /// completeness flags are the Missing filter's entire input. This
    /// mirrors the equivalent fixture in
    /// <c>ImageIntakePersistenceTests.SeedCaseAsync</c>.
    /// </summary>
    private static async Task<Guid> SeedNotReadyCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference,
        bool instructionComplete = true,
        bool imagesComplete = true)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Not ready fixture {reference}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {nameof(CaseLifecycleState.NotReady)}, {"pending"}, {originReceiptId}, {instructionComplete}, {imagesComplete}, {now}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (WorkId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"not-ready-fixture-reader"}, {"1"}, {"not-ready-fixture"}, {1}, {reference}, {1}, {true}, {now})");
        return caseId;
    }
}
