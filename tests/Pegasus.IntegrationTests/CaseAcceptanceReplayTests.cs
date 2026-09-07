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

[Trait("Category", "SqlServer")]
public sealed partial class CaseAcceptanceReplayTests
{
    private const string PrincipalCode = QdosPrincipal.Code;
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor AcceptingActor = ActionActor.Staff(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        [StaffRole.Administrator, StaffRole.Engineer]);


    [Fact]
    public async Task ExactReplayReturnsOriginalAcceptanceAndEveryChangedCommandConflicts()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
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
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var receipt = await CreateReadyReceiptAsync(factory.Services, PrincipalCode);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var reviewedVersion = await ReadReceiptVersionAsync(factory.Services);
        await AdvanceReceiptVersionAsync(factory.Services, receipt.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();

        await Assert.ThrowsAsync<IntakeVersionConflictException>(() => acceptIntake.ExecuteAsync(
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
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
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
    public async Task UnlinkingTheAcceptedOriginCancelsTheCaseAndKeepsItsLineage()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
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

        var beforeUnlink = Assert.IsType<IntakeReceipt>(await getIntake.ExecuteAsync(
            new(receipt.Id, AcceptingActor),
            CancellationToken.None));
        Assert.True(beforeUnlink.UnlinkCancelsCase);

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
            "The email that created this case was unlinked.");
        await reverse.ExecuteAsync(unlinkRequest, CancellationToken.None);
        var unlinked = Assert.IsType<IntakeReceipt>(await getIntake.ExecuteAsync(
            new(receipt.Id, AcceptingActor),
            CancellationToken.None));

        // INTK-029: the source email is gone, so the case it created is
        // cancelled — but its lineage is not. Both origins stay on the record.
        Assert.Equal(
            CaseLifecycleState.SourceEmailUnlinked,
            await ReadCaseStateAsync(factory.Services, caseId));
        Assert.Null(unlinked.CurrentCaseId);
        Assert.Equal(caseId, unlinked.AcceptedCaseId);
        Assert.False(unlinked.UnlinkCancelsCase);
        Assert.Equal(1, await CountRowsAsync(factory.Services, "CaseIntakeLinks"));

        // The unlink is idempotent under replay, and a changed replay conflicts.
        await reverse.ExecuteAsync(unlinkRequest, CancellationToken.None);
        var unlinkReplay = Assert.IsType<IntakeReceipt>(await getIntake.ExecuteAsync(
            new(receipt.Id, AcceptingActor),
            CancellationToken.None));
        Assert.Equal(unlinked.Version, unlinkReplay.Version);
        Assert.Equal(unlinked.CurrentCaseId, unlinkReplay.CurrentCaseId);
        Assert.Equal(unlinked.AcceptedCaseId, unlinkReplay.AcceptedCaseId);
        await Assert.ThrowsAsync<IntakeOperationConflictException>(() =>
            reverse.ExecuteAsync(
                unlinkRequest with { Reason = "A conflicting replay reason." },
                CancellationToken.None));

        // Recovery is a deliberate reopen, not a silent relink: a cancelled
        // case refuses the relink until it is reopened with a reason.
        var relinkLease = await acquireLease.ExecuteAsync(
            new(caseId, unlinkLease.Version + 1, AcceptingActor, "lease:association-relink"),
            CancellationToken.None);
        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            link.ExecuteAsync(
                new LinkIntakeRequest(
                    receipt.Id,
                    caseId,
                    unlinked.Version,
                    relinkLease.Version,
                    relinkLease.Token,
                    AcceptingActor,
                    "association:relink-accepted-origin",
                    "The current association was re-verified against retained evidence."),
                CancellationToken.None));
    }


    // ReviewPostUsesVersionRenderedInAcceptanceFormInsteadOfReloadedVersion
    // moved with the form it guarded. Acceptance no longer has a caller on the
    // received-item screen, so the guard now lives at
    // CaseCreateWebTests.CreatePostUsesTheVersionRenderedInTheFormInsteadOfAReloadedVersion,
    // pointed at the create screen.

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
                // The staff acceptance form is the INT-26 manual path now: a
                // definitive instruction has its case before this page opens,
                // so what is left for a person to accept is material that
                // needs sorting.
                IntakeDecision.NeedsSorting,
                "Needs staff sorting",
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
        if (principalCode == QdosPrincipal.Code)
        {
            _ = await SeededPrincipals.QdosAsync(services);
            return;
        }

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

    private static async Task<CaseLifecycleState> ReadCaseStateAsync(
        IServiceProvider services,
        Guid caseId)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var state = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.State)
            .SingleAsync();
        return Enum.Parse<CaseLifecycleState>(state);
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

    private sealed record AcceptancePersistence(
        long ExpectedIntakeVersion,
        string CommandFingerprint,
        string CommandMaterialJson,
        string ActorKind,
        string ActorSubjectId,
        string ActorRolesJson,
        string Reason);
}
