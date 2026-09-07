using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-025: the Cases page's workflow rail, Principal/Missing filters,
/// per-kind rows and the D14 rule that Blocked intake rows are listed in the
/// Unidentified scope but never counted. Unidentified-as-a-scope (INTK-009)
/// and the Not ready merge across both origins (INTK-013) stay covered here.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class TriageQueuesWebTests
{
    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

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
    /// INTK-013: Not ready and Awaiting instruction are separate row lists.
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
        Assert.Equal(1, Regex.Count(notReadyHtml, "class=\"row-button\""));
        Assert.Equal(1, railCount);

        using var awaiting = await client.GetAsync("/Cases?tab=awaiting");
        var awaitingHtml = await awaiting.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, awaiting.StatusCode);
        Assert.DoesNotContain(instructionCaseReference, awaitingHtml, StringComparison.Ordinal);
        Assert.Contains(imageIntake.ImageIntakeReference, awaitingHtml, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(awaitingHtml, "class=\"row-button\""));
        var awaitingCount = Regex.Match(
            awaitingHtml,
            "scope-button[\\s\\S]*?<span>Awaiting instruction</span>\\s*<span>(\\d+)</span>");
        Assert.True(awaitingCount.Success, "Awaiting instruction rail scope markup not found.");
        Assert.Equal(1, int.Parse(awaitingCount.Groups[1].Value, CultureInfo.InvariantCulture));
        var shellCount = Regex.Match(
            notReadyHtml,
            "<span>Cases</span>\\s*<span class=\"nav-count\" aria-label=\"(\\d+) outstanding\"");
        Assert.True(shellCount.Success, "Cases shell count markup not found.");
        var railTotal = Regex.Matches(
                notReadyHtml,
                "class=\"scope-button[^\"]*\"[\\s\\S]*?<span>(\\d+)</span>\\s*</button>")
            .Sum(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
        Assert.Equal(
            railTotal,
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

    /// <summary>
    /// An image-initiated row carries its retained-image count and its
    /// derived chase state (<c>ImageIntakeChaseSchedule</c>, TICK-065): a
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
    public async Task TriageRowRendersReferenceRegistrationProviderAndAssignee()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        const string reference = "TRIAGE-032";
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
            MatcherKey: "case-032-test",
            MatcherVersion: 1);
        var receiptId = await StoreMinimalReceiptAsync(
            services,
            "triage-row.pdf",
            new InstructionDraft(
                SuggestedPrincipalCode: provider,
                ClaimantName: null,
                ClaimNumber: reference,
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
        await services.GetRequiredService<IAssignTriage>().ExecuteAsync(
            new(
                triage.Id,
                triage.Version,
                DevelopmentOfflineIdentity.AdministratorId,
                StaffActor(),
                $"triage-assign:{Guid.NewGuid():N}",
                "Assigned for the queue-row test."),
            CancellationToken.None);

        using var response = await client.GetAsync("/Cases?tab=triage");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(reference, html, StringComparison.Ordinal);
        Assert.Contains(registration, html, StringComparison.Ordinal);
        Assert.Contains(provider, html, StringComparison.Ordinal);
        Assert.Contains(
            $"{provider} · {DevelopmentOfflineIdentity.UserName}",
            WebUtility.HtmlDecode(html),
            StringComparison.Ordinal);
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
        // The host clock, not the wall clock: these rows are captured into
        // the Test UI corpus, and DateTimeOffset.UtcNow made their snapshots
        // drift on every fresh capture.
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
            "<input[^>]*type=\"hidden\"[^>]*>|\\s(href|asp-route-\\w+)=\"[^\"]*\"",
            "");
        Assert.False(
            Regex.IsMatch(visibleOnly, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"),
            "A raw GUID must never reach the operator-visible text of the Unidentified tab.");
    }

    /// <summary>
    /// D14: Blocked intake receipts are listed inside the Unidentified scope
    /// with their own chip, but the scope's count stays the Unidentified
    /// items' own — the two meanings stay distinct.
    /// </summary>
    [Fact]
    public async Task UnidentifiedTabListsBlockedIntakeRowsUncounted()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receiptStore = services.GetRequiredService<IIntakeReceiptStore>();
        // The host clock, not the wall clock: this row is captured into the
        // Test UI corpus, and DateTimeOffset.UtcNow made its snapshot drift on
        // every fresh capture.
        var receivedAt = services.GetRequiredService<TimeProvider>().GetUtcNow();

        var blocked = await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                "blocked-file.msg",
                "message/rfc822",
                2048,
                Guid.NewGuid().ToString("N"),
                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")),
                receivedAt,
                receivedAt,
                "test-actor",
                IntakeDecision.BlockedIntake,
                "blocked for the test",
                [],
                [],
                null,
                [],
                "unsupported_file_type",
                "unsupported for the test",
                "test-reader",
                "1",
                null,
                null),
            CancellationToken.None);

        using var response = await client.GetAsync("/Cases?tab=unidentified");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("blocked-file.msg", html, StringComparison.Ordinal);
        Assert.Contains("Blocked intake", html, StringComparison.Ordinal);
        Assert.Contains($"/Received/{blocked.Id:D}", html, StringComparison.Ordinal);

        // One Blocked intake row, zero Unidentified items: the scope count
        // must read zero, not one — the row is listed but never counted.
        var countMatch = Regex.Match(
            html,
            "scope-button[\\s\\S]*?<span>Unidentified</span>\\s*<span>(\\d+)</span>");
        Assert.True(countMatch.Success, "Unidentified rail scope markup not found.");
        Assert.Equal(0, int.Parse(countMatch.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// INTK-022: Not ready is one row list across both case origins, with
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
        // Rows remain links, not tables.
        Assert.DoesNotContain("<table", html, StringComparison.Ordinal);
        Assert.DoesNotContain("subtabs", html, StringComparison.Ordinal);
        // The rail groups the workflow; the filters are selects.
        Assert.Contains("Case workflow", html, StringComparison.Ordinal);
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
            factory, client, "XY34ZZZ", "CASE-042-ATTACH");
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
    public async Task AwaitingAttachFailureIsVisibleAndLeavesTheRowInPlace()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var imageIntake = await RegisterImageIntakeAsync(factory, client, scope.ServiceProvider, "WX34YZA");

        using var response = await PostAttachAsync(
            client, imageIntake.Id, imageIntake.Origin.ReceiptId, "UNKNOWN", string.Empty);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var redirected = await client.GetAsync(response.Headers.Location);
        var html = await redirected.Content.ReadAsStringAsync();
        Assert.Contains("A reason is required to add this to a case.", html, StringComparison.Ordinal);
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
                $"INSERT INTO IntakeManualAssociations (IntakeReceiptId, CaseId, IsActive, Version, LinkedAtUtc, ActorKind, ActorSubjectId, ActorRolesJson, Reason, LastOperationKey) VALUES ({imageIntake.Origin.ReceiptId}, {caseId}, {true}, {0L}, {DateTimeOffset.UtcNow}, {"Staff"}, {Guid.NewGuid().ToString("D")}, {"[]"}, {"Linked before image merge synchronisation"}, {$"case-042-linked:{Guid.NewGuid():N}"})");
        }

        using var response = await client.GetAsync("/Cases?tab=awaiting");
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
        var count = Regex.Match(
            html,
            "scope-button[\\s\\S]*?<span>Awaiting instruction</span>\\s*<span>(\\d+)</span>");
        Assert.True(count.Success);
        Assert.Equal(Regex.Count(html, "class=\"row-button\""), int.Parse(count.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The row list renders newest received first (INTK-022's default order,
    /// kept by CASE-025's single order).
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
            html.IndexOf($">{newerReference}</span>", StringComparison.Ordinal)
                < html.IndexOf($">{olderReference}</span>", StringComparison.Ordinal),
            "The row order must put the newest received case first.");
    }

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>
    /// Registers one Image-initiated Case from a fresh upload — the same
    /// sequence every Not ready merge test needs.
    /// </summary>
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
    private static async Task<Guid> StageAndCompleteEvaluationAsync(IServiceProvider services, Guid processedReceiptId)
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
        var evaluation = await workStore.CompleteProcessingAsync(
            processingClaim.WorkItem.Id,
            processingClaim.WorkItem.LeaseToken!,
            processedReceiptId,
            now,
            CancellationToken.None);
        return evaluation.Id;
    }

    private static async Task<Guid> StoreMinimalReceiptAsync(
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

    private static async Task<HttpResponseMessage> PostAttachAsync(
        HttpClient client,
        Guid id,
        Guid receiptId,
        string reference,
        string reason)
    {
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        return await client.PostAsync(
            "/Cases?handler=Attach",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["id"] = id.ToString("D"),
                ["receiptId"] = receiptId.ToString("D"),
                ["reference"] = reference,
                ["reason"] = reason
            }));
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
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {nameof(CaseLifecycleState.NotReady)}, {"pending"}, {originReceiptId}, {instructionComplete}, {imagesComplete}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"not-ready-fixture-reader"}, {"1"}, {"not-ready-fixture"}, {1}, {reference}, {1}, {true}, {now})");
        return caseId;
    }
}
