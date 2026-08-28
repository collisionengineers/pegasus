using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// INTK-009: Unidentified as a Queues tab with media-kind filters, and the
/// Not ready tab's Instruction-initiated/Image-initiated origin filter.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class TriageQueuesWebTests
{
    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task NotReadyOriginFilterReturnsOnlyTheMatchingOriginsRows()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var instructionReceiptId = await StoreMinimalReceiptAsync(services, "instruction-source.pdf");
        var instructionCaseReference = "QDOS" + DateTime.UtcNow.Ticks % 1_000_000;
        await SeedNotReadyCaseAsync(services, instructionReceiptId, instructionCaseReference);

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
        var imageIntake = await register.ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                StaffActor(),
                $"image-intake-register:{Guid.NewGuid():N}",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);

        using var instructionOnly = await client.GetAsync("/Cases?tab=not_ready&origin=instruction");
        var instructionOnlyHtml = await instructionOnly.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, instructionOnly.StatusCode);
        Assert.Contains(instructionCaseReference, instructionOnlyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(imageIntake.ImageIntakeReference, instructionOnlyHtml, StringComparison.Ordinal);

        using var imageOnly = await client.GetAsync("/Cases?tab=not_ready&origin=image");
        var imageOnlyHtml = await imageOnly.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, imageOnly.StatusCode);
        Assert.Contains(imageIntake.ImageIntakeReference, imageOnlyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(instructionCaseReference, imageOnlyHtml, StringComparison.Ordinal);

        using var all = await client.GetAsync("/Cases?tab=not_ready");
        var allHtml = await all.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Contains(instructionCaseReference, allHtml, StringComparison.Ordinal);
        Assert.Contains(imageIntake.ImageIntakeReference, allHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// INTK-013: the operator saw two Not ready cases (one instruction, one
    /// image-initiated) but a badge of 1, because the count query
    /// (<c>EfDashboardQueries.GetCaseStageCountsAsync</c>) only counted
    /// CaseWorkflows rows while the row query
    /// (<c>Triage/Index.cshtml.cs LoadNotReadyAsync</c>) also lists
    /// awaiting-instruction Image Intakes. The badge must equal the number
    /// of rows across both origins, and the Work Centre's Not ready metric
    /// reads the same count so it must agree too.
    /// </summary>
    [Fact]
    public async Task NotReadyBadgeCountMatchesRowsAcrossBothOrigins()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var instructionReceiptId = await StoreMinimalReceiptAsync(services, "instruction-source.pdf");
        var instructionCaseReference = "QDOS" + DateTime.UtcNow.Ticks % 1_000_000;
        await SeedNotReadyCaseAsync(services, instructionReceiptId, instructionCaseReference);

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
        var imageIntake = await register.ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                StaffActor(),
                $"image-intake-register:{Guid.NewGuid():N}",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);

        using var notReady = await client.GetAsync("/Cases?tab=not_ready");
        var notReadyHtml = await notReady.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, notReady.StatusCode);

        var badgeMatch = Regex.Match(notReadyHtml, "Not ready\\s*<span class=\"count\">(\\d+)</span>");
        Assert.True(badgeMatch.Success, "Not ready badge markup not found.");
        var badgeCount = int.Parse(badgeMatch.Groups[1].Value, CultureInfo.InvariantCulture);

        // The row count actually rendered: both origins must be present, and
        // the badge must equal exactly that many rows (2 — one of each
        // origin), not one or the other alone.
        Assert.Contains(instructionCaseReference, notReadyHtml, StringComparison.Ordinal);
        Assert.Contains(imageIntake.ImageIntakeReference, notReadyHtml, StringComparison.Ordinal);
        Assert.Equal(2, badgeCount);

        // The Work Centre's Not ready metric reads the same count query, so
        // it must report the identical figure — a queue whose badge disagrees
        // with its own tab's metric is exactly the defect being fixed here.
        using var dashboard = await client.GetAsync("/");
        var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        var tileMatch = Regex.Match(
            dashboardHtml,
            "data-value=\"not_ready\"[\\s\\S]*?metric-value\">(\\d+)</span>");
        Assert.True(tileMatch.Success, "Work Centre Not ready metric markup not found.");
        Assert.Equal(badgeCount, int.Parse(tileMatch.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// TICK-065 (INT-32): the Not ready tab's Image-initiated table renders a
    /// derived chase-state column (<c>ImageIntakeChaseSchedule</c>) alongside
    /// the existing Received column. A record registered moments ago is well
    /// inside the seven-day window, so it must read "Not yet due" rather than
    /// "Chase due" — the boundary itself is covered at the Core level
    /// (<c>ImageIntakeChaseScheduleTests</c>).
    /// </summary>
    [Fact]
    public async Task NotReadyImageTableRendersChaseColumnForARecentRegistration()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

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
        var imageIntake = await register.ExecuteAsync(
            new(
                origin!,
                "CD34EFG",
                StaffActor(),
                $"image-intake-register:{Guid.NewGuid():N}",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);

        using var response = await client.GetAsync("/Cases?tab=not_ready&origin=image");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
        Assert.Contains("Chase", html, StringComparison.Ordinal);
        Assert.Contains("Not yet due", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Chase due", html, StringComparison.Ordinal);
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

        var receipt = await receiptStore.StoreAsync(
            new IntakeReceiptDraft(
                "unreadable-document.pdf",
                "application/pdf",
                2048,
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
        await register.ExecuteAsync(
            new(
                UnidentifiedOrigin.Receipt(receipt.Id),
                UnidentifiedReasonCode.UnreadableOrCorruptContent,
                "The document could not be read.",
                ActionActor.SystemWorker("test-worker"),
                $"unidentified-web-test:{Guid.NewGuid():N}",
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        using var response = await client.GetAsync("/Cases?tab=unidentified");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unidentified", html, StringComparison.Ordinal);
        Assert.Contains("unreadable-document.pdf", html, StringComparison.Ordinal);
        Assert.DoesNotContain("intake", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("custody", html, StringComparison.OrdinalIgnoreCase);

        // A GUID legitimately appears in the row link's href (routing to
        // /Unidentified/Details/{id}); the design rule bans it from what the
        // operator reads, not from a URL they never see. Strip attribute
        // values before scanning so only visible text is checked.
        var visibleOnly = Regex.Replace(html, "\\s(href|asp-route-\\w+)=\"[^\"]*\"", "");
        Assert.False(
            Regex.IsMatch(visibleOnly, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"),
            "A raw GUID must never reach the operator-visible text of the Unidentified tab.");
    }

    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    /// <summary>
    /// INTK-022: the Not ready tab is one merged table across both case
    /// origins, with dropdown filters instead of pills and dash cells where a
    /// field does not apply to an image-initiated row.
    /// </summary>
    [Fact]
    public async Task NotReadyRendersOneMergedTableAcrossOrigins()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var instructionReceiptId = await StoreMinimalReceiptAsync(services, "instruction-source.pdf");
        var instructionCaseReference = "QDOS" + DateTime.UtcNow.Ticks % 1_000_000;
        await SeedNotReadyCaseAsync(services, instructionReceiptId, instructionCaseReference);

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
        var imageIntake = await register.ExecuteAsync(
            new(
                origin!,
                "EF56GHJ",
                StaffActor(),
                $"image-intake-register:{Guid.NewGuid():N}",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);

        using var response = await client.GetAsync("/Cases?tab=not_ready");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(instructionCaseReference, html, StringComparison.Ordinal);
        Assert.Contains(imageIntake.ImageIntakeReference, html, StringComparison.Ordinal);
        // One table, not one per origin.
        Assert.Equal(1, Regex.Count(html, "<table"));
        // Dropdown filters replaced the origin pills.
        Assert.Contains("name=\"origin\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"principal\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("subtabs", html, StringComparison.Ordinal);
        // The image row fills inapplicable cells with a dash and keeps its
        // TICK-065 derived chase chip.
        Assert.Contains("Awaiting definitive instruction", html, StringComparison.Ordinal);
        Assert.Contains("Not yet due", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// INTK-022: the Received header sorts newest-first by default and its
    /// link flips the direction.
    /// </summary>
    [Fact]
    public async Task NotReadySortDefaultsNewestFirstAndHeaderTogglesDirection()
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

        using var newestFirst = await client.GetAsync("/Cases?tab=not_ready");
        var newestFirstHtml = await newestFirst.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, newestFirst.StatusCode);
        Assert.True(
            newestFirstHtml.IndexOf($">{newerReference}</a>", StringComparison.Ordinal)
                < newestFirstHtml.IndexOf($">{olderReference}</a>", StringComparison.Ordinal),
            "The default order must put the newest received case first.");

        using var oldestFirst = await client.GetAsync("/Cases?tab=not_ready&sort=received_asc");
        var oldestFirstHtml = await oldestFirst.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, oldestFirst.StatusCode);
        Assert.True(
            oldestFirstHtml.IndexOf($">{olderReference}</a>", StringComparison.Ordinal)
                < oldestFirstHtml.IndexOf($">{newerReference}</a>", StringComparison.Ordinal),
            "sort=received_asc must put the oldest received case first.");
    }

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
    /// A raw-SQL Not-ready Case fixture: exercising the full instruction
    /// pipeline just to get one NotReady case row is unrelated to what this
    /// test verifies (the origin filter reads whatever the Cases table
    /// holds). This mirrors the equivalent fixture in
    /// <c>ImageIntakePersistenceTests.SeedCaseAsync</c>.
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
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Not ready fixture {reference}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {nameof(CaseLifecycleState.NotReady)}, {"pending"}, {originReceiptId}, {true}, {true}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"not-ready-fixture-reader"}, {"1"}, {"not-ready-fixture"}, {1}, {reference}, {1}, {true}, {now})");
    }
}
