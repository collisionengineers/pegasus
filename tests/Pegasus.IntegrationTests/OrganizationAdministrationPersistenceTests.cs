using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
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
    public async Task ExistingPrincipalSettingsAdvanceAndCheckTheOwningContactVersion()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var principal = await context.Principals.AsNoTracking()
            .Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => new { item.Id, item.Version, item.OrganizationId, item.ReportGenerationPolicy, ContactVersion = item.Organization.Version })
            .SingleAsync();
        var update = services.GetRequiredService<IUpdatePrincipalReportSettings>();
        var changedPolicy = principal.ReportGenerationPolicy == PrincipalReportGenerationPolicy.Pegasus.ToString()
            ? PrincipalReportGenerationPolicy.EvaManualApi
            : PrincipalReportGenerationPolicy.Pegasus;

        UpdatePrincipalReportSettingsRequest Request(long principalVersion, long contactVersion, string operationKey) => new(
            principal.Id,
            principalVersion,
            Administrator,
            operationKey,
            "Confirm contact-owned report settings",
            changedPolicy,
            PrincipalReportRecipientSettings.None,
            205.00m,
            contactVersion);

        var updated = await update.ExecuteAsync(
            Request(principal.Version, principal.ContactVersion, "principal:report-settings:direct"), default);
        Assert.Equal(principal.Version + 1, updated.Version);
        Assert.Equal(205.00m, updated.DefaultFee);
        Assert.Equal(205.00m, await factory.Database.ScalarAsync<decimal>(
            $"SELECT DefaultFee FROM Principals WHERE Id = '{principal.Id:D}';"));
        Assert.Equal(
            principal.ContactVersion + 1,
            await factory.Database.ScalarAsync<long>(
                $"SELECT Version FROM Organizations WHERE Id = '{principal.OrganizationId:D}';"));

        var staleContact = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(updated.Version, principal.ContactVersion, "principal:report-settings:stale-contact"), default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, staleContact.Error);

        var stalePrincipal = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(principal.Version, principal.ContactVersion + 1, "principal:report-settings:stale-principal"), default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stalePrincipal.Error);
    }

    [Fact]
    public async Task ReportSendingRulesAreKeptOnceReplayedAndRecordedInHistory()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var principal = await context.Principals.AsNoTracking()
            .Where(item => item.Code == QdosPrincipal.Code)
            .Select(item => new { item.Id, item.Version, item.OrganizationId, ContactVersion = item.Organization.Version })
            .SingleAsync();
        var update = services.GetRequiredService<IUpdatePrincipalReportSending>();
        var getPrincipal = services.GetRequiredService<IGetPrincipal>();
        var rules = PrincipalReportSendingRules.Default with
        {
            SendFromMailbox = "engineers@collisionengineers.co.uk",
            SendTo = ["to@example.com"],
            Cc = ["cc@example.com"],
            Attach = new(FeeNoteSeparate: false, Estimate: true),
            Hold = "Check first.",
            Rules =
            [
                new(
                    ReportSendingRuleMatch.Any,
                    [
                        new(ReportSendingConditionKind.Outcome, ["repairable"]),
                        new(ReportSendingConditionKind.Mentions, ["Luton"])
                    ],
                    new(["rule@example.com"], [], Stop: "Stop."))
            ],
            AttachmentName = new("{reg} Initial", "{reg} Supplementary")
        };

        UpdatePrincipalReportSendingRequest Request(
            long principalVersion, long contactVersion, string operationKey, PrincipalReportSendingRules value) => new(
            principal.Id, principalVersion, Administrator, operationKey, value, contactVersion);

        // The seed gave QDOS its rules; the update replaces them.
        var before = await getPrincipal.ExecuteAsync(Administrator, principal.Id, default);
        Assert.NotNull(before?.Principal.ReportSending);

        var request = Request(principal.Version, principal.ContactVersion, "principal:report-sending:first", rules);
        var updated = await update.ExecuteAsync(request, default);
        var replay = await update.ExecuteAsync(request, default);

        Assert.Equal(updated, replay);
        Assert.Equal(principal.Version + 1, updated.Version);
        Assert.Equal(rules, updated.ReportSending);
        Assert.Equal(
            principal.ContactVersion + 1,
            await factory.Database.ScalarAsync<long>(
                $"SELECT Version FROM Organizations WHERE Id = '{principal.OrganizationId:D}';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM OrganizationAdministrationOperations WHERE OperationKey = 'principal:report-sending:first' AND CommandKind = 'update_principal_report_sending';"));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'principal:report-sending:first' AND EventKind = 'principal_report_sending_updated';"));
        var after = await getPrincipal.ExecuteAsync(Administrator, principal.Id, default);
        Assert.Equal(rules, after!.Principal.ReportSending);
        Assert.Equal(
            "Check first.",
            await factory.Database.ScalarAsync<string>(
                $"SELECT JSON_VALUE(ReportSendingRulesJson, '$.hold') FROM Principals WHERE Id = '{principal.Id:D}';"));

        var conflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(principal.Version, principal.ContactVersion, "principal:report-sending:first", rules with { Hold = "Different." }),
                default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, conflict.Error);

        var staleContact = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(updated.Version, principal.ContactVersion, "principal:report-sending:stale-contact", rules with { Hold = "Other." }),
                default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, staleContact.Error);

        var stalePrincipal = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            update.ExecuteAsync(
                Request(principal.Version, principal.ContactVersion + 1, "principal:report-sending:stale-principal", rules with { Hold = "Other." }),
                default));
        Assert.Equal(OrganizationAdministrationError.StaleVersion, stalePrincipal.Error);

        // Saving the same rules again changes nothing about the Principal.
        var unchanged = await update.ExecuteAsync(
            Request(updated.Version, principal.ContactVersion + 1, "principal:report-sending:unchanged", rules), default);
        Assert.Equal(updated.Version, unchanged.Version);

        var refused = await Assert.ThrowsAsync<ReportSendingRulesException>(() =>
            update.ExecuteAsync(
                Request(updated.Version, principal.ContactVersion + 2, "principal:report-sending:refused", rules with { Cc = ["not an address"] }),
                default));
        Assert.Equal(ReportSendingRulesRule.InvalidAddress, refused.Rule);
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
        await seedContext.Principals
            .Where(item => item.Id == predecessor.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DefaultFee, 210.00m));
        var initialContactVersion = await seedContext.Organizations.AsNoTracking()
            .Where(item => item.Id == predecessor.OrganizationId)
            .Select(item => item.Version)
            .SingleAsync();
        var configuredLocation = await scope.ServiceProvider
            .GetRequiredService<IUpdatePrincipalDefaultInspectionLocation>()
            .ExecuteAsync(new(
                Administrator, predecessor.Id, predecessor.Version,
                "principal:default-location:replacement",
                InspectionAddressEvidenceKind.PhysicalAddress,
                "Directory Web Caller Yard", "1 Directory Way, DW1 2EF", "DW1 2EF",
                "manual", null, null,
                initialContactVersion), default);
        var salvageMatrix = SalvageMatrix.Normalize([new("N", 0.01m, 9999999.99m, 20m)]);
        var configuredMatrix = await scope.ServiceProvider
            .GetRequiredService<IUpdatePrincipalSalvageMatrix>()
            .ExecuteAsync(new(
                predecessor.Id, configuredLocation.Version, Administrator,
                "principal:salvage-matrix:replacement", salvageMatrix,
                initialContactVersion + 1), default);
        var receipt = await CreateReadyReceiptAsync(factory.Services);
        var receiptVersion = await factory.Database.ScalarAsync<long>(
            $"SELECT Version FROM IntakeReceipts WHERE Id = '{receipt.Id:D}';");
        var accepted = await acceptIntake.ExecuteAsync(
            new(
                receipt.Id,
                receiptVersion,
                Administrator,
                "case:accept:replacement-test",
                CaseType.Inspection,
                predecessor.Code,
                new(true, true)),
            default);
        var originalReference = accepted.Identity.Reference;
        var replacementContactVersion = await factory.Database.ScalarAsync<long>(
            $"SELECT Version FROM Organizations WHERE Id = '{predecessor.OrganizationId:D}';");
        var replacementRequest = new ReplacePrincipalRequest(
            predecessor.Id,
            configuredMatrix.Version,
            "QDOSNEXT",
            Administrator,
            "principal:replace:qdos",
            "Provider issued a successor code",
            replacementContactVersion);

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
        Assert.Equal(salvageMatrix, persistedSuccessor.SalvageMatrix);
        Assert.Equal(salvageMatrix, persistedPredecessor.SalvageMatrix);
        Assert.Equal(210.00m, persistedSuccessor.DefaultFee);
        var caseMatrices = scope.ServiceProvider.GetRequiredService<IPrincipalSalvageMatrixQueries>();
        Assert.Equal(salvageMatrix, await caseMatrices.GetForCaseAsync(accepted.Identity.CaseId, default));
        Assert.Null(await caseMatrices.GetForCaseAsync(Guid.NewGuid(), default));
        Assert.Equal(
            1,
            await factory.Database.ScalarAsync<int>(
                "SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = 'principal:salvage-matrix:replacement' AND EventKind = 'principal_salvage_matrix_updated';"));
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
        Assert.Equal(
            initialContactVersion + 3,
            await factory.Database.ScalarAsync<long>(
                $"SELECT Version FROM Organizations WHERE Id = '{predecessor.OrganizationId:D}';"));

        var conflict = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            replacePrincipal.ExecuteAsync(
                replacementRequest with { SuccessorCode = "CHANGED" },
                default));
        Assert.Equal(OrganizationAdministrationError.OperationConflict, conflict.Error);
        var currentPrincipalVersion = await factory.Database.ScalarAsync<long>(
            $"SELECT Version FROM Principals WHERE Id = '{predecessor.Id:D}';");
        var stale = await Assert.ThrowsAsync<OrganizationAdministrationException>(() =>
            replacePrincipal.ExecuteAsync(
                replacementRequest with
                {
                    OperationKey = "principal:replace:stale",
                    SuccessorCode = "ANOTHER",
                    ExpectedVersion = currentPrincipalVersion
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
