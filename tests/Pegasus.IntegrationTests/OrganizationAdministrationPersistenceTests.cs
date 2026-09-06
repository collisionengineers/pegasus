using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task CreateReplayConflictDuplicateAndBoundedProjectionsUseCoreAndEf()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var createOrganization = scope.ServiceProvider.GetRequiredService<ICreateOrganization>();
        var createPrincipal = scope.ServiceProvider.GetRequiredService<ICreatePrincipal>();
        var listOrganizations = scope.ServiceProvider.GetRequiredService<IListOrganizations>();
        var getOrganization = scope.ServiceProvider.GetRequiredService<IGetOrganization>();
        var organizationRequest = new CreateOrganizationRequest(
            "Alpha Provider",
            [OrganizationRole.WorkProvider, OrganizationRole.InstructionIntermediary],
            Administrator,
            "organization:create:alpha");

        var organization = await createOrganization.ExecuteAsync(organizationRequest, default);
        var organizationReplay = await createOrganization.ExecuteAsync(organizationRequest, default);

        AssertOrganizationEquivalent(organization, organizationReplay);
        var operationConflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            createOrganization.ExecuteAsync(
                organizationRequest with { Name = "Different Provider" },
                default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, operationConflict.Error);
        var duplicateName = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            createOrganization.ExecuteAsync(
                organizationRequest with
                {
                    Name = "alpha provider",
                    OperationKey = "organization:create:duplicate"
                },
                default));
        Assert.Equal(OrganizationAdministrationError.DuplicateOrganizationName, duplicateName.Error);

        // A code the foundation migration (C-F05) has not already seeded:
        // Principal.Code is unique globally, not per organization, so a
        // fresh create using one of its 15 frozen codes (e.g. "qdos") would
        // collide with the seeded row instead of exercising this create/
        // replay/duplicate/conflict path.
        var principalRequest = new CreatePrincipalRequest(
            organization.Id,
            "alpha",
            Administrator,
            "principal:create:alpha");
        var principal = await createPrincipal.ExecuteAsync(principalRequest, default);
        var principalReplay = await createPrincipal.ExecuteAsync(principalRequest, default);

        Assert.Equal("ALPHA", principal.Code);
        Assert.Equal(principal, principalReplay);
        var duplicateCode = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            createPrincipal.ExecuteAsync(
                principalRequest with { OperationKey = "principal:create:alpha-duplicate" },
                default));
        Assert.Equal(OrganizationAdministrationError.DuplicatePrincipalCode, duplicateCode.Error);
        var principalConflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            createPrincipal.ExecuteAsync(
                principalRequest with { Code = "OTHER" },
                default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, principalConflict.Error);
        _ = await createOrganization.ExecuteAsync(
            new(
                "Beta Provider",
                [OrganizationRole.WorkProvider],
                Administrator,
                "organization:create:beta"),
            default);

        var page = await listOrganizations.ExecuteAsync(
            new(Administrator, 1, 1),
            default);
        var listedOrganization = Assert.Single(page.Organizations);
        Assert.Equal(organization.Id, listedOrganization.Id);
        Assert.Equal(
            [OrganizationRole.WorkProvider, OrganizationRole.InstructionIntermediary],
            listedOrganization.Roles);
        Assert.Equal(principal.Id, Assert.Single(listedOrganization.Principals).Id);
        Assert.True(page.HasMoreOrganizations);

        var details = await getOrganization.ExecuteAsync(
            new(Administrator, organization.Id),
            default);
        Assert.NotNull(details);
        Assert.Equal(organization.Name, details.Name);
        Assert.Equal(principal.SequenceLineageId, Assert.Single(details.Principals).SequenceLineageId);
        Assert.False(details.HasMorePrincipals);

        Assert.Equal(
            3,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM OrganizationAdministrationOperations;"));
        Assert.Equal(
            3,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE AggregateType IN ('organization', 'principal');"));
    }

    [Fact]
    public async Task ConcurrentExactReplayCommitsOneOrganizationAndAuditEntry()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var createOrganization = scope.ServiceProvider.GetRequiredService<ICreateOrganization>();
        var request = new CreateOrganizationRequest(
            "Concurrent Provider",
            [OrganizationRole.WorkProvider],
            Administrator,
            "organization:create:concurrent");

        var results = await Task.WhenAll(
            createOrganization.ExecuteAsync(request, default),
            createOrganization.ExecuteAsync(request, default));

        AssertOrganizationEquivalent(results[0], results[1]);
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM Organizations WHERE Name = 'Concurrent Provider';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = 'organization:create:concurrent';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'organization:create:concurrent';"));
    }

    [Fact]
    public async Task RoleUpdatesAreVersionedAndProtectActiveWorkProviderPrincipals()
    {
        using var factory = new IntakeWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var createOrganization = scope.ServiceProvider.GetRequiredService<ICreateOrganization>();
        var createPrincipal = scope.ServiceProvider.GetRequiredService<ICreatePrincipal>();
        var updateRoles = scope.ServiceProvider.GetRequiredService<IUpdateOrganizationRoles>();
        var intermediaryOnly = await createOrganization.ExecuteAsync(
            new(
                "Intermediary Only",
                [OrganizationRole.InstructionIntermediary],
                Administrator,
                "organization:create:intermediary-only"),
            default);
        var principalRoleGuard = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            createPrincipal.ExecuteAsync(
                new(
                    intermediaryOnly.Id,
                    "INVALIDOWNER",
                    Administrator,
                    "principal:create:invalid-owner"),
                default));
        Assert.Equal(
            OrganizationAdministrationError.OrganizationCannotOwnPrincipals,
            principalRoleGuard.Error);
        var organization = await createOrganization.ExecuteAsync(
            new(
                "Role Guard Provider",
                [OrganizationRole.WorkProvider, OrganizationRole.InstructionIntermediary],
                Administrator,
                "organization:create:role-guard"),
            default);
        _ = await createPrincipal.ExecuteAsync(
            new(
                organization.Id,
                "GUARD",
                Administrator,
                "principal:create:role-guard"),
            default);

        var workProviderOnlyRequest = new UpdateOrganizationRolesRequest(
            organization.Id,
            organization.Version,
            [OrganizationRole.WorkProvider],
            Administrator,
            "organization:roles:remove-intermediary",
            "No longer routes intermediary instructions");
        var workProviderOnly = await updateRoles.ExecuteAsync(workProviderOnlyRequest, default);
        var replay = await updateRoles.ExecuteAsync(workProviderOnlyRequest, default);

        AssertOrganizationEquivalent(workProviderOnly, replay);
        Assert.Equal(1, workProviderOnly.Version);
        Assert.Equal([OrganizationRole.WorkProvider], workProviderOnly.Roles);

        var activePrincipalGuard = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            updateRoles.ExecuteAsync(
                new(
                    organization.Id,
                    workProviderOnly.Version,
                    [OrganizationRole.InstructionIntermediary],
                    Administrator,
                    "organization:roles:remove-provider",
                    "Attempt to remove provider role"),
                default));
        Assert.Equal(
            OrganizationAdministrationError.ActivePrincipalsRequireWorkProvider,
            activePrincipalGuard.Error);

        var stale = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            updateRoles.ExecuteAsync(
                new(
                    organization.Id,
                    0,
                    [OrganizationRole.WorkProvider, OrganizationRole.InstructionIntermediary],
                    Administrator,
                    "organization:roles:stale",
                    "Stale update"),
                default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stale.Error);
    }

    [Fact]
    public async Task ReplacementDisablesAndLinksPredecessorWithoutChangingAllocatedCaseIdentity()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var replacePrincipal = scope.ServiceProvider.GetRequiredService<IReplacePrincipal>();
        var getOrganization = scope.ServiceProvider.GetRequiredService<IGetOrganization>();
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
                new(true, true, true, true)),
            default);
        var originalReference = accepted.Identity.Reference;
        var replacementRequest = new ReplacePrincipalRequest(
            predecessor.Id,
            predecessor.Version,
            predecessor.OrganizationId,
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
        var details = await getOrganization.ExecuteAsync(
            new(Administrator, predecessor.OrganizationId),
            default);
        Assert.NotNull(details);
        var persistedPredecessor = Assert.Single(
            details.Principals,
            principal => principal.Id == predecessor.Id);
        var persistedSuccessor = Assert.Single(
            details.Principals,
            principal => principal.Id == successor.Id);
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

    private static void AssertOrganizationEquivalent(
        Organization expected,
        Organization actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Roles.ToArray(), actual.Roles.ToArray());
        Assert.Equal(expected.Version, actual.Version);
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
