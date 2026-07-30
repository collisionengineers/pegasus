using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
public sealed partial class CaseAcceptanceReplayTests
{
    private const string PrincipalCode = QdosAlphaCaseActivationPolicy.PrincipalCode;
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor AcceptingActor = ActionActor.Staff(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        [StaffRole.Administrator, StaffRole.Engineer]);


    [Fact]
    public async Task ExactReplayReturnsOriginalAcceptanceAndEveryChangedCommandConflicts()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();
        var request = new AcceptIntakeRequest(
            receipt.Id,
            reviewedVersion,
            AcceptingActor,
            "acceptance:exact-replay",
            "Reviewed source evidence and confirmed the case intake.",
            CaseType.Inspection,
            PrincipalCode,
            new(true, true, true, true));

        var first = await acceptIntake.ExecuteAsync(request, CancellationToken.None);
        await SetCaseCustodyStateAsync(factory.Services, "confirmed");
        var replay = await acceptIntake.ExecuteAsync(request, CancellationToken.None);

        Assert.False(first.IsDuplicate);
        Assert.True(replay.IsDuplicate);
        Assert.Equal(first.Identity, replay.Identity);
        Assert.Equal(first.InitialState, replay.InitialState);
        Assert.Equal(first.CustodyState, replay.CustodyState);
        Assert.Equal(first.CustodyWorkId, replay.CustodyWorkId);

        var persisted = await ReadAcceptancePersistenceAsync(factory.Services);
        Assert.Equal(reviewedVersion, persisted.ExpectedIntakeVersion);
        Assert.Matches("^[0-9a-f]{64}$", persisted.CommandFingerprint);
        Assert.Contains(PrincipalCode, persisted.CommandMaterialJson, StringComparison.Ordinal);
        Assert.Contains(request.Actor.SubjectId, persisted.CommandMaterialJson, StringComparison.Ordinal);
        Assert.Contains(nameof(StaffRole.Administrator), persisted.CommandMaterialJson, StringComparison.Ordinal);
        Assert.Contains(request.Reason, persisted.CommandMaterialJson, StringComparison.Ordinal);
        Assert.Equal(nameof(ActorKind.Staff), persisted.ActorKind);
        Assert.Equal(AcceptingActor.SubjectId, persisted.ActorSubjectId);
        Assert.Equal("[\"Administrator\",\"Engineer\"]", persisted.ActorRolesJson);
        Assert.Equal(request.Reason, persisted.Reason);

        AcceptIntakeRequest[] changedRequests =
        [
            request with { OperationKey = "acceptance:different-key" },
            request with { CaseType = CaseType.InspectionAndAudit },
            request with
            {
                Completeness = request.Completeness with { ImagesComplete = false }
            },
            request with { AcceptedInspectionDeadline = new DateOnly(2031, 5, 20) },
            request with { Actor = ActionActor.Staff(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                [StaffRole.Administrator]) },
            request with { Actor = ActionActor.Staff(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                [StaffRole.Administrator]) },
            request with { Reason = "A materially different acceptance reason." },
            request with { ExpectedVersion = reviewedVersion + 1 }
        ];

        foreach (var changedRequest in changedRequests)
        {
            var conflict = await Assert.ThrowsAsync<CaseAcceptanceOperationConflictException>(
                () => acceptIntake.ExecuteAsync(changedRequest, CancellationToken.None));
            Assert.Equal(receipt.Id, conflict.IntakeReceiptId);
            Assert.Equal(changedRequest.OperationKey, conflict.OperationKey);
        }

        Assert.Equal(1, await CountRowsAsync(factory.Services, "Cases"));
        Assert.Equal(1, await CountRowsAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await CountRowsAsync(factory.Services, "CaseSequences"));
        Assert.Equal(1, await CountRowsAsync(factory.Services, "IntakeManualAssociations"));
        Assert.Equal(1, await CountRowsAsync(factory.Services, "IntakeMutationHistory"));
    }

    [Fact]
    public async Task ReceiptChangedAfterReviewCannotBeAccepted()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        await AdvanceReceiptVersionAsync(factory.Services, receipt.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => acceptIntake.ExecuteAsync(
            new(
                receipt.Id,
                reviewedVersion,
                AcceptingActor,
                "acceptance:stale-review",
                "Reviewed the intake before the concurrent change.",
                CaseType.Inspection,
                PrincipalCode,
                new(true, true, true, true)),
            CancellationToken.None));

        Assert.Equal(0, await CountRowsAsync(factory.Services, "Cases"));
        Assert.Equal(0, await CountRowsAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(0, await CountRowsAsync(factory.Services, "CaseSequences"));
    }

    [Fact]
    public async Task MissingAcceptanceReasonIsRejectedBeforePersistence()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();

        await Assert.ThrowsAsync<ArgumentException>(() => acceptIntake.ExecuteAsync(
            new(
                receipt.Id,
                reviewedVersion,
                AcceptingActor,
                "acceptance:missing-reason",
                "   ",
                CaseType.Inspection,
                PrincipalCode,
                new(true, true, true, true)),
            CancellationToken.None));
        Assert.Equal(0, await CountRowsAsync(factory.Services, "Cases"));
        Assert.Equal(0, await CountRowsAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(0, await CountRowsAsync(factory.Services, "IntakeMutationHistory"));
    }


    [Fact]
    public async Task AcceptedOriginCanBeUnlinkedAndRelinkedWithoutDeletingLineage()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var accepted = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>()
            .ExecuteAsync(
                new(
                    receipt.Id,
                    reviewedVersion,
                    AcceptingActor,
                    "acceptance:association-lifecycle",
                    "Confirmed evidence before testing association lifecycle.",
                    CaseType.Inspection,
                    PrincipalCode,
                    new(true, true, true, true)),
                CancellationToken.None);
        var caseId = accepted.Identity.CaseId;
        var acquireLease = scope.ServiceProvider.GetRequiredService<IAcquireCaseEditLease>();
        var reverse = scope.ServiceProvider.GetRequiredService<IReverseIntakeLink>();
        var link = scope.ServiceProvider.GetRequiredService<ILinkIntake>();
        var getIntake = scope.ServiceProvider.GetRequiredService<IGetIntake>();

        var unlinkLease = await acquireLease.ExecuteAsync(
            new(caseId, 0, AcceptingActor, "lease:association-unlink"),
            CancellationToken.None);
        var unlinkRequest = new ReverseIntakeLinkRequest(
            receipt.Id,
            caseId,
            reviewedVersion + 1,
            unlinkLease.Version,
            unlinkLease.Token,
            AcceptingActor,
            "association:unlink-accepted-origin",
            "The intake was associated with the wrong current case.");
        await reverse.ExecuteAsync(unlinkRequest, CancellationToken.None);
        var unlinked = Assert.IsType<IntakeReceipt>(await getIntake.ExecuteAsync(
            new(receipt.Id, AcceptingActor),
            CancellationToken.None));
        await reverse.ExecuteAsync(unlinkRequest, CancellationToken.None);
        var unlinkReplay = Assert.IsType<IntakeReceipt>(await getIntake.ExecuteAsync(
            new(receipt.Id, AcceptingActor),
            CancellationToken.None));

        Assert.Null(unlinked.CurrentCaseId);
        Assert.Equal(caseId, unlinked.AcceptedCaseId);
        Assert.Equal(unlinked.Version, unlinkReplay.Version);
        Assert.Equal(unlinked.CurrentCaseId, unlinkReplay.CurrentCaseId);
        Assert.Equal(unlinked.AcceptedCaseId, unlinkReplay.AcceptedCaseId);
        await Assert.ThrowsAsync<IntakeOperationConflictException>(() =>
            reverse.ExecuteAsync(
                unlinkRequest with { Reason = "A conflicting replay reason." },
                CancellationToken.None));

        var relinkLease = await acquireLease.ExecuteAsync(
            new(caseId, unlinkLease.Version + 1, AcceptingActor, "lease:association-relink"),
            CancellationToken.None);
        var relinkRequest = new LinkIntakeRequest(
            receipt.Id,
            caseId,
            unlinked.Version,
            relinkLease.Version,
            relinkLease.Token,
            AcceptingActor,
            "association:relink-accepted-origin",
            "The current association was re-verified against retained evidence.");
        await link.ExecuteAsync(relinkRequest, CancellationToken.None);
        var relinked = Assert.IsType<IntakeReceipt>(await getIntake.ExecuteAsync(
            new(receipt.Id, AcceptingActor),
            CancellationToken.None));
        await link.ExecuteAsync(relinkRequest, CancellationToken.None);
        var relinkReplay = Assert.IsType<IntakeReceipt>(await getIntake.ExecuteAsync(
            new(receipt.Id, AcceptingActor),
            CancellationToken.None));

        Assert.Equal(caseId, relinked.CurrentCaseId);
        Assert.Equal(caseId, relinked.AcceptedCaseId);
        Assert.Equal(relinked.Version, relinkReplay.Version);
        Assert.Equal(relinked.CurrentCaseId, relinkReplay.CurrentCaseId);
        Assert.Equal(relinked.AcceptedCaseId, relinkReplay.AcceptedCaseId);
        Assert.Equal(1, await CountRowsAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await CountRowsAsync(factory.Services, "IntakeManualAssociations"));
        Assert.Equal(3, await CountRowsAsync(factory.Services, "IntakeMutationHistory"));
    }


    [Fact]
    public async Task ReviewPostUsesVersionRenderedInAcceptanceFormInsteadOfReloadedVersion()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var addressStore = new MutableAddressResolutionStore();
        var acceptIntake = new VersionCheckingAcceptIntake();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInspectionAddressResolutionStore>();
                services.AddSingleton<IInspectionAddressResolutionStore>(addressStore);
                services.RemoveAll<IAcceptIntake>();
                services.AddSingleton<IAcceptIntake>(acceptIntake);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        addressStore.SetReceipt(receipt.Id, reviewedVersion);
        acceptIntake.CurrentReceiptVersion = reviewedVersion;

        using var reviewResponse = await client.GetAsync($"/Intake/{receipt.Id}");
        reviewResponse.EnsureSuccessStatusCode();
        var reviewHtml = await reviewResponse.Content.ReadAsStringAsync();
        var antiforgeryToken = InputValue(reviewHtml, "__RequestVerificationToken");
        var operationKey = InputValue(reviewHtml, "AcceptanceOperationKey");
        var renderedVersion = long.Parse(
            InputValue(reviewHtml, "ReviewedReceiptVersion"),
            CultureInfo.InvariantCulture);
        Assert.Equal(reviewedVersion, renderedVersion);

        addressStore.ReceiptVersion++;
        acceptIntake.CurrentReceiptVersion++;
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["AcceptanceOperationKey"] = operationKey,
            ["ReviewedReceiptVersion"] = renderedVersion.ToString(CultureInfo.InvariantCulture),
            ["AcceptanceReason"] = "Reviewed the retained evidence and confirmed acceptance.",
            ["PrincipalCode"] = PrincipalCode,
            ["CaseType"] = CaseType.Inspection.ToString(),
            ["StandaloneAuditAssessment"] = string.Empty,
            ["InstructionComplete"] = bool.TrueString,
            ["ImagesComplete"] = bool.TrueString,
            ["InstructionConfirmedByStaff"] = bool.TrueString,
            ["ImagesConfirmedByStaff"] = bool.TrueString
        });

        using var response = await client.PostAsync(
            $"/Intake/{receipt.Id}?handler=Accept",
            content);
        var responseHtml = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, acceptIntake.Attempts);
        var capturedRequest = Assert.IsType<AcceptIntakeRequest>(acceptIntake.LastRequest);
        Assert.Equal(renderedVersion, capturedRequest.ExpectedVersion);
        Assert.NotEqual(addressStore.ReceiptVersion, capturedRequest.ExpectedVersion);
        Assert.Equal(ActorKind.Staff, capturedRequest.Actor.Kind);
        Assert.Equal(
            DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
            capturedRequest.Actor.SubjectId);
        Assert.Contains(StaffRole.Administrator, capturedRequest.Actor.Roles);
        Assert.Equal($"intake-accept:{operationKey}", capturedRequest.OperationKey);
        Assert.Equal(
            "Reviewed the retained evidence and confirmed acceptance.",
            capturedRequest.Reason);
        Assert.Contains(
            "The case could not be accepted. No reference was allocated",
            responseHtml,
            StringComparison.Ordinal);
    }

    private static async Task<IntakeReceipt> CreateReadyReceiptAsync(
        IServiceProvider services,
        string principalCode)
    {
        var token = Guid.NewGuid().ToString("N");
        var sourceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        return await store.StoreAsync(
            new(
                "acceptance-review.eml",
                "message/rfc822",
                1,
                sourceHash,
                new(IntakeSourceChannel.ManualUpload, token),
                RecordedAtUtc,
                RecordedAtUtc,
                "Acceptance replay test",
                IntakeDecision.DraftReady,
                "Ready for staff review",
                [],
                [
                    SourceField("Claimant name", "Replay claimant"),
                    SourceField("Claim number", "REPLAY-001"),
                    SourceField("Vehicle registration", "AB12CDE"),
                    SourceField("Inspection address", "Image Based Assessment")
                ],
                new(
                    principalCode,
                    "Replay claimant",
                    "REPLAY-001",
                    "AB12CDE",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Image Based Assessment"),
                [],
                null,
                null,
                "acceptance_test_reader",
                "1",
                "acceptance_test_policy",
                1),
            CancellationToken.None);
    }

    private static InstructionReviewField SourceField(string name, string value) =>
        new(
            name,
            value,
            [new(value, IntakeEvidenceSource.PdfContent, "retained acceptance test evidence")],
            IsDefaulted: false,
            HasConflict: false);

    private static async Task SeedPrincipalAsync(IServiceProvider services, string principalCode)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Replay provider"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {RecordedAtUtc})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {principalCode}, {lineageId}, NULL, NULL, {true}, {0L})
            """);
    }

    private static async Task<long> ReadReceiptVersionAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT Version FROM IntakeReceipts";
            return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task AdvanceReceiptVersionAsync(IServiceProvider services, Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var updated = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE IntakeReceipts SET Version = Version + 1 WHERE Id = {receiptId}");
        Assert.Equal(1, updated);
    }

    private static async Task SetCaseCustodyStateAsync(
        IServiceProvider services,
        string custodyState)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var updated = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cases SET CustodyState = {custodyState}");
        Assert.Equal(1, updated);
    }

    private static async Task<AcceptancePersistence> ReadAcceptancePersistenceAsync(
        IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                """
                SELECT ExpectedIntakeVersion, AcceptanceCommandFingerprint,
                    AcceptanceCommandMaterialJson, ActorKind, ActorSubjectId,
                    ActorRolesJson, Reason
                FROM CaseIntakeLinks
                """;
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            var result = new AcceptancePersistence(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6));
            Assert.False(await reader.ReadAsync());
            return result;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> CountRowsAsync(IServiceProvider services, string tableName)
    {
        var allowed = tableName switch
        {
            "Cases" or "CaseIntakeLinks" or "CaseSequences"
                or "IntakeManualAssociations" or "IntakeMutationHistory" => tableName,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM [{allowed}]";
            return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static string InputValue(string html, string name)
    {
        var match = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                WebUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The review form must render input '{name}'.");
        return WebUtility.HtmlDecode(match!.Groups["value"].Value);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    private sealed record AcceptancePersistence(
        long ExpectedIntakeVersion,
        string CommandFingerprint,
        string CommandMaterialJson,
        string ActorKind,
        string ActorSubjectId,
        string ActorRolesJson,
        string Reason);

    private sealed class MutableAddressResolutionStore : IInspectionAddressResolutionStore
    {
        private static readonly Guid StaffId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private Guid receiptId;

        public long ReceiptVersion { get; set; }

        public void SetReceipt(Guid value, long version)
        {
            receiptId = value;
            ReceiptVersion = version;
        }

        public Task<InspectionAddressResolutionSnapshot?> GetAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (intakeReceiptId != receiptId)
            {
                return Task.FromResult<InspectionAddressResolutionSnapshot?>(null);
            }

            InspectionAddressResolutionSnapshot snapshot = new(
                intakeReceiptId,
                ReceiptVersion,
                InspectionAddressResolutionState.Accepted,
                new(
                    new(
                        "Image Based Assessment",
                        InspectionAddressEvidenceKind.ImageBasedAssessment,
                        [],
                        new string('a', 64)),
                    []),
                "Image Based Assessment",
                StaffId,
                RecordedAtUtc);
            return Task.FromResult<InspectionAddressResolutionSnapshot?>(snapshot);
        }

        public Task<InspectionAddressResolutionSnapshot> ResolveAsync(
            InspectionAddressResolutionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromException<InspectionAddressResolutionSnapshot>(
                new NotSupportedException("This focused test does not mutate address resolution."));
    }

    private sealed class VersionCheckingAcceptIntake : IAcceptIntake
    {
        public long CurrentReceiptVersion { get; set; }

        public int Attempts { get; private set; }

        public AcceptIntakeRequest? LastRequest { get; private set; }

        public Task<CaseAcceptanceOutcome> ExecuteAsync(
            AcceptIntakeRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            LastRequest = request;
            if (request.ExpectedVersion != CurrentReceiptVersion)
            {
                return Task.FromException<CaseAcceptanceOutcome>(
                    new DbUpdateConcurrencyException("The rendered intake version is stale."));
            }

            return Task.FromResult(new CaseAcceptanceOutcome(
                new(Guid.NewGuid(), request.PrincipalCode, 2031, 1, "REPLAY31001"),
                CaseInitialState.Review,
                CaseCustodyState.Pending,
                Guid.NewGuid(),
                false));
        }
    }
}
