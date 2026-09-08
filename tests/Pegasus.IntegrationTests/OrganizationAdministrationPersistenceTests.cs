using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class OrganizationAdministrationPersistenceTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("0f149cac-e1d4-4a57-925f-7c35d33d7f5b"),
        [StaffRole.Administrator]);
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task PrincipalCreationIsAtomicReplaySafeAndProjectsOneCustomer()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var create = scope.ServiceProvider.GetRequiredService<ICreatePrincipal>();
        var list = scope.ServiceProvider.GetRequiredService<IListPrincipals>();
        var get = scope.ServiceProvider.GetRequiredService<IGetPrincipal>();
        var request = new CreatePrincipalRequest(
            "  pegasustest  ", "pegasustest", Administrator, "principal:create:pegasustest");

        var principal = await create.ExecuteAsync(request, default);
        var replay = await create.ExecuteAsync(request, default);
        Assert.Equal(principal, replay);
        Assert.Equal("PEGASUSTEST", principal.Code);
        var details = await get.ExecuteAsync(Administrator, principal.Id, default);
        Assert.NotNull(details);
        Assert.Equal("pegasustest", details.Name);
        Assert.Equal(principal.OrganizationId, details.Principal.OrganizationId);
        Assert.Equal(principal.SequenceLineageId, details.Principal.SequenceLineageId);
        Assert.Equal(0, details.Principal.AllocatedCaseCount);

        var duplicateName = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            create.ExecuteAsync(request with
            {
                Name = "PEGASUSTEST", Code = "NEXT", OperationKey = "principal:duplicate-name"
            }, default));
        Assert.Equal(OrganizationAdministrationError.DuplicateOrganizationName, duplicateName.Error);
        var duplicateCode = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            create.ExecuteAsync(request with
            {
                Name = "Other Provider", OperationKey = "principal:duplicate-code"
            }, default));
        Assert.Equal(OrganizationAdministrationError.DuplicatePrincipalCode, duplicateCode.Error);
        var conflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            create.ExecuteAsync(request with { Name = "Other Provider" }, default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, conflict.Error);

        // A separate directory entry is not a customer and never appears on the Principal list.
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.Organizations.Add(new OrganizationEntity
            {
                Id = Guid.NewGuid(), Name = "Intermediary Only", Version = 0
            });
            await context.SaveChangesAsync();
        }
        var page = await list.ExecuteAsync(Administrator, 1, default);
        Assert.Equal(principal.Id, Assert.Single(page.Principals, item => item.Name == "pegasustest").Principal.Id);
        Assert.DoesNotContain(page.Principals, item => item.Name == "Intermediary Only");
        Assert.False(page.HasMore);
        Assert.Empty((await list.ExecuteAsync(Administrator, 2, default)).Principals);
        Assert.Null(await get.ExecuteAsync(Administrator, Guid.NewGuid(), default));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Organizations WHERE Name = 'pegasustest';"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Organizations WHERE Name = 'Other Provider';"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM OrganizationRoles WHERE OrganizationId = '{principal.OrganizationId:D}' AND Role = 'work_provider';"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = 'principal:create:pegasustest';"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'principal:create:pegasustest';"));
    }

    [Fact]
    public async Task ConcurrentExactReplayCommitsOneCustomerPrincipalAndAuditEntry()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var createPrincipal = scope.ServiceProvider.GetRequiredService<ICreatePrincipal>();
        var request = new CreatePrincipalRequest(
            "Concurrent Provider",
            "CONCURRENT",
            Administrator,
            "principal:create:concurrent");

        var results = await Task.WhenAll(
            createPrincipal.ExecuteAsync(request, default),
            createPrincipal.ExecuteAsync(request, default));

        Assert.Equal(results[0], results[1]);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Principals WHERE Code = 'CONCURRENT';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM Organizations WHERE Name = 'Concurrent Provider';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = 'principal:create:concurrent';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'principal:create:concurrent';"));
    }

    [Fact]
    public async Task ReplacementDisablesAndLinksPredecessorWithoutChangingAllocatedCaseIdentity()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var replacePrincipal = scope.ServiceProvider.GetRequiredService<IReplacePrincipal>();
        var getPrincipal = scope.ServiceProvider.GetRequiredService<IGetPrincipal>();
        var acceptIntake = scope.ServiceProvider.GetRequiredService<IAcceptIntake>();

        // The foundation migration (C-F05) already seeds the QDOS principal
        // and its owning work-provider organization exactly once; a fresh
        // create with the same principal code would collide with it
        // globally (Principal.Code has no per-organization scope), so the
        // predecessor here is the seeded row itself, replaced within its
        // own already-seeded, already-eligible organization.
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var seedContext = await contextFactory.CreateDbContextAsync();
        var predecessor = await seedContext.Principals.AsNoTracking()
            .SingleAsync(item => item.Code == QdosPrincipal.Code);
        var configuredLocation = await scope.ServiceProvider
            .GetRequiredService<IUpdatePrincipalDefaultInspectionLocation>()
            .ExecuteAsync(new(
                Administrator, predecessor.Id, predecessor.Version,
                "principal:default-location:replacement", "Keep the customer default across code replacement",
                InspectionAddressEvidenceKind.PhysicalAddress,
                "Directory Web Caller Yard", "1 Directory Way, DW1 2EF", "DW1 2EF",
                "manual", null, null), default);
        var receipt = await CreateReadyReceiptAsync(factory.Services);
        var receiptVersion = await factory.Database.ScalarAsync<long>(
            $"SELECT Version FROM IntakeReceipts WHERE Id = '{receipt.Id:D}';");
        var accepted = await acceptIntake.ExecuteAsync(
            new(
                receipt.Id,
                receiptVersion,
                Administrator,
                "case:accept:replacement-test",
                "Confirmed intake before principal replacement testing.",
                CaseType.Inspection,
                predecessor.Code,
                new(true, true)),
            default);
        var originalReference = accepted.Identity.Reference;
        var replacementRequest = new ReplacePrincipalRequest(
            predecessor.Id,
            configuredLocation.Version,
            "QDOSNEXT",
            Administrator,
            "principal:replace:qdos",
            "Provider issued a successor code");

        var successor = await replacePrincipal.ExecuteAsync(replacementRequest, default);
        var replay = await replacePrincipal.ExecuteAsync(replacementRequest, default);

        Assert.Equal(successor, replay);
        Assert.Equal(predecessor.SequenceLineageId, successor.SequenceLineageId);
        Assert.Equal(predecessor.Id, successor.PredecessorId);
        Assert.True(successor.IsActive);
        var predecessorDetails = await getPrincipal.ExecuteAsync(Administrator, predecessor.Id, default);
        var successorDetails = await getPrincipal.ExecuteAsync(Administrator, successor.Id, default);
        Assert.NotNull(predecessorDetails);
        Assert.NotNull(successorDetails);
        var persistedPredecessor = predecessorDetails.Principal;
        var persistedSuccessor = successorDetails.Principal;
        Assert.Equal(predecessorDetails.Name, successorDetails.Name);
        Assert.Equal(persistedPredecessor.OrganizationId, persistedSuccessor.OrganizationId);
        Assert.Equal(
            (configuredLocation.DefaultInspectionLocationLabel,
                configuredLocation.DefaultInspectionAddress,
                configuredLocation.DefaultInspectionPostcode,
                configuredLocation.DefaultInspectionSourceKind,
                configuredLocation.DefaultInspectionSourceRecordId,
                configuredLocation.DefaultInspectionSourceVersion),
            (persistedSuccessor.DefaultInspectionLocationLabel,
                persistedSuccessor.DefaultInspectionAddress,
                persistedSuccessor.DefaultInspectionPostcode,
                persistedSuccessor.DefaultInspectionSourceKind,
                persistedSuccessor.DefaultInspectionSourceRecordId,
                persistedSuccessor.DefaultInspectionSourceVersion));
        Assert.False(persistedPredecessor.IsActive);
        Assert.Equal(successor.Id, persistedPredecessor.SuccessorId);
        Assert.Equal("QDOS", persistedPredecessor.Code);
        Assert.Equal(1, persistedPredecessor.AllocatedCaseCount);
        Assert.Equal(0, persistedSuccessor.AllocatedCaseCount);

        Assert.Equal(
            predecessor.Id,
            await factory.Database.ScalarAsync<Guid>(
                $"SELECT PrincipalId FROM Cases WHERE Id = '{accepted.Identity.CaseId:D}';"));
        Assert.Equal(
            originalReference,
            await factory.Database.ScalarAsync<string>(
                $"SELECT Reference FROM Cases WHERE Id = '{accepted.Identity.CaseId:D}';"));
        Assert.Equal(
            2,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'principal:replace:qdos';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = 'principal:replace:qdos';"));

        var conflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            replacePrincipal.ExecuteAsync(
                replacementRequest with { SuccessorCode = "CHANGED" },
                default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, conflict.Error);
        var stale = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            replacePrincipal.ExecuteAsync(
                replacementRequest with
                {
                    OperationKey = "principal:replace:stale",
                    SuccessorCode = "ANOTHER"
                },
                default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stale.Error);
    }

    private static async Task<IntakeReceipt> CreateReadyReceiptAsync(IServiceProvider services)
    {
        var token = Guid.NewGuid().ToString("N");
        var sourceHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        return await store.StoreAsync(
            new(
                "principal-replacement.eml",
                "message/rfc822",
                1,
                sourceHash,
                new(IntakeSourceChannel.ManualUpload, token),
                RecordedAtUtc,
                RecordedAtUtc,
                "Principal replacement test",
                IntakeDecision.CaseCreated,
                "Ready for staff review",
                [],
                [
                    new(
                        "Claimant name",
                        "Replacement claimant",
                        [
                            new(
                                "Replacement claimant",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false),
                    new(
                        "Claim number",
                        "REPLACEMENT-001",
                        [
                            new(
                                "REPLACEMENT-001",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false),
                    new(
                        "Vehicle registration",
                        "AB12CDE",
                        [
                            new(
                                "AB12CDE",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false),
                    new(
                        "Inspection address",
                        "Image Based Assessment",
                        [
                            new(
                                "Image Based Assessment",
                                IntakeEvidenceSource.EmailBody,
                                "principal-replacement.eml")
                        ],
                        false,
                        false)
                ],
                new(
                    QdosPrincipal.Code,
                    "Replacement claimant",
                    "REPLACEMENT-001",
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
                "replacement_test_reader",
                "1",
                "replacement_test_policy",
                1),
            default);
    }

    /// <summary>
    /// C-F05/EXT-18 item 1: the fresh-database migration seeds the current
    /// top-15 principal identities exactly once, by the exact code/GUID the
    /// foundation handoff froze — and YML (the confirmed HDUK-branded route)
    /// stays a distinct principal role rather than merging with a document
    /// issuer or any other principal.
    /// </summary>
    [Fact]
    public async Task FreshDatabaseSeedsExactlyTheFifteenFrozenPrincipalsOnce()
    {
        var expected = new (string Code, Guid PrincipalId)[]
        {
            ("QDOS", Guid.Parse("00000000-0000-4000-8000-00000000c001")),
            ("PCH", Guid.Parse("00000000-0000-4000-8000-00000000c002")),
            ("AX", Guid.Parse("00000000-0000-4000-8000-00000000c003")),
            ("FW", Guid.Parse("00000000-0000-4000-8000-00000000c004")),
            ("QCL", Guid.Parse("00000000-0000-4000-8000-00000000c005")),
            ("OAK", Guid.Parse("00000000-0000-4000-8000-00000000c006")),
            ("SBL", Guid.Parse("00000000-0000-4000-8000-00000000c007")),
            ("BLACK", Guid.Parse("00000000-0000-4000-8000-00000000c008")),
            ("RJS", Guid.Parse("00000000-0000-4000-8000-00000000c009")),
            ("DFD", Guid.Parse("00000000-0000-4000-8000-00000000c00a")),
            ("KBS", Guid.Parse("00000000-0000-4000-8000-00000000c00b")),
            ("MP", Guid.Parse("00000000-0000-4000-8000-00000000c00c")),
            ("YML", Guid.Parse("00000000-0000-4000-8000-00000000c00d")),
            ("ALS", Guid.Parse("00000000-0000-4000-8000-00000000c00e")),
            ("BC", Guid.Parse("00000000-0000-4000-8000-00000000c00f"))
        };

        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var seeded = await context.Principals
            .AsNoTracking()
            .Select(principal => new { principal.Id, principal.Code })
            .ToListAsync();

        Assert.Equal(15, seeded.Count);
        foreach (var (code, principalId) in expected)
        {
            var matches = seeded.Where(row => row.Id == principalId).ToArray();
            Assert.True(
                matches.Length == 1,
                $"Expected exactly one seeded principal with id {principalId}, found {matches.Length}.");
            Assert.Equal(code, matches[0].Code);
        }

        // YML is the confirmed HDUK-branded route, but HDUK itself is a
        // document issuer, never a second principal identity: no seeded
        // principal may carry that code, and every seeded code above is
        // unique.
        Assert.DoesNotContain(seeded, row => row.Code == "HDUK");
        Assert.Equal(seeded.Count, seeded.Select(row => row.Code).Distinct().Count());
        Assert.Equal(seeded.Count, seeded.Select(row => row.Id).Distinct().Count());
    }
}
