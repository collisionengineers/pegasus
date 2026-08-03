using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

public sealed class ImageIntakePersistenceTests
{
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task RegistrationAllocatesSequentialReferencesAndMovesTheReceiptDecision()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var firstReceiptId = await UploadImageAsync(factory, client);
        var secondReceiptId = await UploadImageAsync(factory, client);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var actor = StaffActor();

        var firstOrigin = await resolver.ResolveOriginAsync(firstReceiptId, CancellationToken.None);
        Assert.NotNull(firstOrigin);
        var firstRequest = new RegisterImageIntakeRequest(
            firstOrigin!,
            "AB12CDE",
            actor,
            "image-intake-register-first",
            "Staff confirmed the registration from the retained image.");
        var first = await register.ExecuteAsync(firstRequest, CancellationToken.None);
        Assert.Equal("AB12CDE-01", first.ImageIntakeReference);

        var secondOrigin = await resolver.ResolveOriginAsync(secondReceiptId, CancellationToken.None);
        var second = await register.ExecuteAsync(
            new(
                secondOrigin!,
                "AB12CDE",
                actor,
                "image-intake-register-second",
                "A second arrival for the same vehicle registration."),
            CancellationToken.None);
        Assert.Equal("AB12CDE-02", second.ImageIntakeReference);

        var replay = await register.ExecuteAsync(firstRequest, CancellationToken.None);
        Assert.Equal(first, replay);
        await Assert.ThrowsAsync<ImageIntakeOperationConflictException>(
            () => register.ExecuteAsync(
                firstRequest with { Reason = "Altered request details" },
                CancellationToken.None));
        await Assert.ThrowsAsync<IntakeSourceIdentityConflictException>(
            () => register.ExecuteAsync(
                firstRequest with
                {
                    NormalizedVehicleRegistration = "XY34ZZZ",
                    OperationKey = "image-intake-register-different-vrm"
                },
                CancellationToken.None));

        var firstReceipt = await receipts.GetAsync(firstReceiptId, CancellationToken.None);
        Assert.Equal(IntakeDecision.ImageIntakeRegistered, firstReceipt!.Decision);
        var detail = await queries.GetByOriginReceiptAsync(firstReceiptId, CancellationToken.None);
        Assert.Equal("AB12CDE-01", detail!.Record.ImageIntakeReference);
        Assert.Null(detail.AssociatedCaseId);
        var byReference = await queries.GetByReferenceAsync("ab12cde-02 ", CancellationToken.None);
        Assert.Equal(second.Id, byReference!.Record.Id);
        var byVrm = await queries.SearchByRegistrationAsync("AB12CDE", CancellationToken.None);
        Assert.Equal(2, byVrm.Count);
    }

    [Fact]
    public async Task InstructionBearingReceiptCannotRegister()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "instruction.eml",
            "QDOS instruction\r\nClaim Number: IMG-REG-001\r\nVehicle Registration: AB12 CDE");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => register.ExecuteAsync(
                new(
                    origin!,
                    "AB12CDE",
                    StaffActor(),
                    "image-intake-register-instruction",
                    "Attempted registration of instruction-bearing material."),
                CancellationToken.None));
    }

    [Fact]
    public async Task NonImageNeedsSortingMaterialCannotRegister()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var email = IntakeTestEvidence.CreateEmail(
            "loose-notes.eml",
            "Please review this ordinary correspondence; no instruction, no image.");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var receipt = await receipts.GetAsync(receiptId, CancellationToken.None);
        // The guard under test is the material shape, not the queue decision:
        // this receipt sits in Needs sorting exactly like image-only material.
        Assert.Equal(IntakeDecision.NeedsSorting, receipt!.Decision);

        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => register.ExecuteAsync(
                new(
                    origin!,
                    "AB12CDE",
                    StaffActor(),
                    "image-intake-register-non-image",
                    "Attempted registration of non-image material."),
                CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentSameVrmRegistrationsAllocateDistinctSequentialReferences()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var firstReceiptId = await UploadImageAsync(factory, client);
        var secondReceiptId = await UploadImageAsync(factory, client);

        // Both registrations race the same per-VRM sequence row under
        // serializable isolation. The winner commits; a loser may deadlock or
        // hit a serialization failure, must surface that failure rather than
        // reuse or skip a reference, and must succeed cleanly when retried.
        var outcomes = await Task.WhenAll(
            Task.Run(() => TryRegisterAsync(
                factory.Services, firstReceiptId, "concurrent-register-first")),
            Task.Run(() => TryRegisterAsync(
                factory.Services, secondReceiptId, "concurrent-register-second")));

        Assert.Contains(outcomes, outcome => outcome is null);
        foreach (var (receiptId, operationKey) in new[]
        {
            (firstReceiptId, "concurrent-register-first"),
            (secondReceiptId, "concurrent-register-second")
        })
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                if (await TryRegisterAsync(factory.Services, receiptId, operationKey) is null)
                {
                    break;
                }
            }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IImageIntakeQueries>();
        var registered = await queries.SearchByRegistrationAsync("AB12CDE", CancellationToken.None);
        Assert.Equal(2, registered.Count);
        Assert.Collection(
            registered
                .Select(intake => intake.ImageIntakeReference)
                .OrderBy(reference => reference, StringComparer.Ordinal),
            reference => Assert.Equal("AB12CDE-01", reference),
            reference => Assert.Equal("AB12CDE-02", reference));
    }

    private static async Task<Exception?> TryRegisterAsync(
        IServiceProvider services,
        Guid receiptId,
        string operationKey)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            await RegisterAsync(scope.ServiceProvider, receiptId, "AB12CDE", operationKey);
            return null;
        }
        catch (Exception exception)
        {
            // Never a replay conflict: the race loses on the sequence row,
            // not on the operation key.
            Assert.IsNotType<ImageIntakeOperationConflictException>(exception);
            return exception;
        }
    }

    [Fact]
    public async Task ReceiptLinkEnforcesEligibilityOnceAnImageIntakeExists()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceiptId = await UploadImageAsync(factory, client);
        var caseOriginReceiptId = await UploadCaseOriginAsync(factory, client, "CASE-LINK-01");
        var eligibleCaseId = await SeedCaseAsync(
            factory.Services,
            caseOriginReceiptId,
            "IMG26001",
            nameof(CaseLifecycleState.Review),
            "AB12CDE");
        var postReportReceiptId = await UploadCaseOriginAsync(factory, client, "CASE-LINK-02");
        var postReportCaseId = await SeedCaseAsync(
            factory.Services,
            postReportReceiptId,
            "IMG26002",
            nameof(CaseLifecycleState.PostReport),
            "AB12CDE");
        var actor = StaffActor();

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceiptId, "AB12CDE", "link-eligibility-register");
        var link = services.GetRequiredService<ILinkIntake>();
        var reverse = services.GetRequiredService<IReverseIntakeLink>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var queries = services.GetRequiredService<IImageIntakeQueries>();

        var ineligibleLease = await ClaimLeaseAsync(
            factory.Services,
            postReportCaseId,
            actor,
            "claim-post-report-lease");
        var receipt = await receipts.GetAsync(imageReceiptId, CancellationToken.None);
        await Assert.ThrowsAsync<ImageIntakeCaseNotEligibleException>(
            () => link.ExecuteAsync(
                new(
                    imageReceiptId,
                    postReportCaseId,
                    receipt!.Version,
                    0,
                    ineligibleLease.Token,
                    actor,
                    "link-post-report-case",
                    "A post-report case must be rejected."),
                CancellationToken.None));

        var lease = await ClaimLeaseAsync(
            factory.Services,
            eligibleCaseId,
            actor,
            "claim-eligible-lease");
        receipt = await receipts.GetAsync(imageReceiptId, CancellationToken.None);
        await link.ExecuteAsync(
            new(
                imageReceiptId,
                eligibleCaseId,
                receipt!.Version,
                0,
                lease.Token,
                actor,
                "link-eligible-case",
                "The registration matches this pre-report case."),
            CancellationToken.None);

        var associated = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.Equal(eligibleCaseId, associated!.AssociatedCaseId);
        Assert.Equal("IMG26001", associated.AssociatedCaseReference);
        var forCase = await queries.ListForCaseAsync(eligibleCaseId, CancellationToken.None);
        Assert.Single(forCase);

        var unlinkLease = await ClaimLeaseAsync(
            factory.Services,
            eligibleCaseId,
            actor,
            "claim-unlink-lease");
        receipt = await receipts.GetAsync(imageReceiptId, CancellationToken.None);
        await reverse.ExecuteAsync(
            new(
                imageReceiptId,
                eligibleCaseId,
                receipt!.Version,
                1,
                unlinkLease.Token,
                actor,
                "unlink-eligible-case",
                "Reasoned reversal of the association."),
            CancellationToken.None);

        var afterUnlink = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.Null(afterUnlink!.AssociatedCaseId);
        Assert.Equal("AB12CDE-01", afterUnlink.Record.ImageIntakeReference);
    }

    [Fact]
    public async Task AutomaticAssociationWritesTheSameAssociationWithSystemAttribution()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var imageReceiptId = await UploadImageAsync(factory, client);
        var caseOriginReceiptId = await UploadCaseOriginAsync(factory, client, "AUTO-LINK-01");
        var caseId = await SeedCaseAsync(
            factory.Services,
            caseOriginReceiptId,
            "IMG26011",
            nameof(CaseLifecycleState.Review),
            "AB12CDE");

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await RegisterAsync(services, imageReceiptId, "AB12CDE", "auto-link-register");
        var mutationStore = services.GetRequiredService<IIntakeMutationStore>();
        var systemActor = ActionActor.SystemWorker("image-intake-automation");
        var request = new AutomaticIntakeLinkRequest(
            imageReceiptId,
            caseId,
            0,
            systemActor,
            "image-intake-associate-test",
            "Automatic association: unambiguous registration match.");

        await mutationStore.AutoLinkAsync(request, DateTimeOffset.UtcNow, CancellationToken.None);
        await mutationStore.AutoLinkAsync(request, DateTimeOffset.UtcNow, CancellationToken.None);
        await Assert.ThrowsAsync<IntakeOperationConflictException>(
            () => mutationStore.AutoLinkAsync(
                request with { Reason = "Altered automatic request" },
                DateTimeOffset.UtcNow,
                CancellationToken.None));

        var queries = services.GetRequiredService<IImageIntakeQueries>();
        var detail = await queries.GetByOriginReceiptAsync(imageReceiptId, CancellationToken.None);
        Assert.Equal(caseId, detail!.AssociatedCaseId);

        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var workflowEvent = await context.Database
            .SqlQuery<string>(
                $"SELECT EventType AS Value FROM CaseWorkflowEvents WHERE CaseId = {caseId}")
            .ToListAsync();
        Assert.Contains("intake_case_auto_linked", workflowEvent);
        var actorKind = await context.Database
            .SqlQuery<string>(
                $"SELECT ActorKind AS Value FROM IntakeMutationHistory WHERE OperationKey = {"image-intake-associate-test"}")
            .SingleAsync();
        Assert.Equal(nameof(ActorKind.SystemWorker), actorKind);
    }

    private static async Task<Guid> UploadImageAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client)
    {
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        return IntakeWebDriver.ReceiptId(upload);
    }

    private static async Task<Guid> UploadCaseOriginAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        string claimNumber)
    {
        var email = IntakeTestEvidence.CreateEmail(
            $"{claimNumber}.eml",
            $"QDOS instruction\r\nClaim Number: {claimNumber}\r\nVehicle Registration: AB12 CDE");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            email.FileName,
            email.MediaType,
            email.Content);
        return IntakeWebDriver.ReceiptId(upload);
    }

    private static async Task RegisterAsync(
        IServiceProvider services,
        Guid receiptId,
        string registration,
        string operationKey)
    {
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);
        await register.ExecuteAsync(
            new(
                origin!,
                registration,
                StaffActor(),
                operationKey,
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);
    }

    private static async Task<CaseEditLease> ClaimLeaseAsync(
        IServiceProvider services,
        Guid caseId,
        ActionActor actor,
        string operationKey)
    {
        await using var scope = services.CreateAsyncScope();
        var workflows = scope.ServiceProvider.GetRequiredService<ICaseWorkflowQueries>();
        var workflow = await workflows.GetAsync(caseId, CancellationToken.None);
        return await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new ClaimCaseEditLeaseRequest(
                caseId,
                workflow!.Version,
                actor,
                operationKey),
            CancellationToken.None);
    }

    private static async Task<Guid> SeedCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference,
        string workflowState,
        string draftRegistration)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Image intake provider {reference}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {"not_ready"}, {"pending"}, {originReceiptId}, {true}, {true}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {workflowState}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (CaseId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"image-intake-test-reader"}, {"1"}, {"image-intake-fixture"}, {1}, {reference}, {1}, {true}, {now})");
        _ = draftRegistration;
        return caseId;
    }
}
