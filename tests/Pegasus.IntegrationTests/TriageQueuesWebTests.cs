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

        using var instructionOnly = await client.GetAsync("/Triage?queue=not_ready&origin=instruction");
        var instructionOnlyHtml = await instructionOnly.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, instructionOnly.StatusCode);
        Assert.Contains(instructionCaseReference, instructionOnlyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(imageIntake.ImageIntakeReference, instructionOnlyHtml, StringComparison.Ordinal);

        using var imageOnly = await client.GetAsync("/Triage?queue=not_ready&origin=image");
        var imageOnlyHtml = await imageOnly.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, imageOnly.StatusCode);
        Assert.Contains(imageIntake.ImageIntakeReference, imageOnlyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(instructionCaseReference, imageOnlyHtml, StringComparison.Ordinal);

        using var all = await client.GetAsync("/Triage?queue=not_ready");
        var allHtml = await all.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Contains(instructionCaseReference, allHtml, StringComparison.Ordinal);
        Assert.Contains(imageIntake.ImageIntakeReference, allHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnidentifiedRouteRedirectsPermanentlyToTheQueuesTab()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Unidentified");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/Triage?queue=unidentified", response.Headers.Location?.OriginalString);
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

        using var response = await client.GetAsync("/Triage?queue=unidentified");
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
