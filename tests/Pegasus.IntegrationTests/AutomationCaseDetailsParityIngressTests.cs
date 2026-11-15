using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Caller evidence that the Automation tools follow what the Case page gained
/// between 10 and 12 November 2026: a Principal's linked contacts first
/// (R1013, R1016), the Repairer link the Case holds kept as held (R1022), the
/// Sign-off Engineer pick removed (R1019), and the one unroadworthy wordings
/// bank (R952). Real HTTP against /mcp and real persistence.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationCaseDetailsParityIngressTests
{
    [Fact]
    public async Task ALinkedRepairerIsListedFirstAndTheLinkTheCaseHoldsIsKeptAsHeld()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var linkedId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        await SeedRepairersAsync(mcpFactory, linkedId, otherId);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        // Name order alone would put the unlinked "Alpha" first.
        using (var search = await PostMcpAsync(client, token, ToolCallPayload(1,
            "pegasus_directory_search", new
            {
                role = "Repairer",
                name = "Parity Fixture",
                principalCode = QdosPrincipal.Code
            })))
        {
            Assert.Equal(HttpStatusCode.OK, search.StatusCode);
            var items = (await ReadStructuredContentAsync(search)).GetProperty("items");
            Assert.Equal(2, items.GetArrayLength());
            Assert.Equal(linkedId, items[0].GetProperty("organizationId").GetGuid());
            Assert.Contains(
                items[0].GetProperty("linkedPrincipalCodes").EnumerateArray(),
                code => string.Equals(code.GetString(), QdosPrincipal.Code, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(otherId, items[1].GetProperty("organizationId").GetGuid());
            Assert.Equal(0, items[1].GetProperty("linkedPrincipalCodes").GetArrayLength());
        }

        var lease = await BeginEditAsync(
            client, token, caseId, await GetWorkflowVersionAsync(mcpFactory, caseId), rpcId: 2);
        long version;
        using (var link = await PostMcpAsync(client, token, ToolCallPayload(3,
            "pegasus_case_update_details", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:parity-repairer-link",
                reason = "Automation linked the repairer.",
                repairerDirectoryId = linkedId
            })))
        {
            version = (await ReadStructuredContentAsync(link)).GetProperty("caseVersion").GetInt64();
        }

        // The directory record goes inactive after the Case copied it.
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Organizations SET Active = 0 WHERE Id = {linkedId}");
        }

        // Naming the link the Case already holds is no new link: the name
        // typed beside it is saved, and the inactive record refuses nothing.
        using (var kept = await PostMcpAsync(client, token, ToolCallPayload(4,
            "pegasus_case_update_details", new
            {
                caseId,
                expectedVersion = version,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:parity-repairer-kept",
                reason = "Automation corrected the repairer's name.",
                repairerDirectoryId = linkedId,
                repairerName = "Typed Parity Repairer"
            })))
        {
            _ = await ReadStructuredContentAsync(kept);
        }

        using var get = await PostMcpAsync(client, token, ToolCallPayload(5,
            "pegasus_case_get", new { caseId, limit = 1 }));
        var structured = await ReadStructuredContentAsync(get);
        var repairer = structured.GetProperty("repairer");
        Assert.Equal(linkedId, repairer.GetProperty("directoryOrganizationId").GetGuid());
        Assert.Equal("Typed Parity Repairer", repairer.GetProperty("name").GetString());
        var holds = structured.GetProperty("holds");
        Assert.False(holds.GetProperty("documentStorageFailed").GetBoolean());
        Assert.False(holds.GetProperty("originalReportMissing").GetBoolean());
    }

    [Fact]
    public async Task TheEmptyIdentifierRemovesTheRecordedSignOffEngineerPick()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        _ = await SeedSignOffEngineerAsync(mcpFactory, isDefault: true);
        var chosenId = await SeedSignOffEngineerAsync(mcpFactory, isDefault: false);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var lease = await BeginEditAsync(
            client, token, caseId, await GetWorkflowVersionAsync(mcpFactory, caseId), rpcId: 1);

        long version;
        using (var picked = await PostMcpAsync(client, token, ToolCallPayload(2,
            "pegasus_case_update_details", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:parity-sign-off-pick",
                reason = "Automation chose the Sign-off Engineer.",
                signOffEngineerId = chosenId
            })))
        {
            version = (await ReadStructuredContentAsync(picked)).GetProperty("caseVersion").GetInt64();
        }
        Assert.Equal(chosenId, await RecordedSignOffEngineerAsync(mcpFactory, caseId));

        using (var cleared = await PostMcpAsync(client, token, ToolCallPayload(3,
            "pegasus_case_update_details", new
            {
                caseId,
                expectedVersion = version,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:parity-sign-off-clear",
                reason = "Automation removed the Sign-off Engineer pick.",
                signOffEngineerId = Guid.Empty
            })))
        {
            _ = await ReadStructuredContentAsync(cleared);
        }
        Assert.Null(await RecordedSignOffEngineerAsync(mcpFactory, caseId));
    }

    [Fact]
    public async Task AnUnroadworthyReasonSavedToTheBankIsListedByTheVocabularyOnce()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var lease = await BeginEditAsync(
            client, token, caseId, await GetWorkflowVersionAsync(mcpFactory, caseId), rpcId: 1);
        var fields = new Dictionary<string, string?>
        {
            [AssessmentVocabulary.LegalStatus] = "unroadworthy",
            [AssessmentVocabulary.UnroadworthyReason] = "The parity fixture offside front tyre is below the legal limit."
        };

        long version;
        using (var saved = await PostMcpAsync(client, token, ToolCallPayload(2,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:parity-bank-1",
                reason = "Automation recorded the roadworthiness.",
                fields,
                saveUnroadworthyReasonToBank = true
            })))
        {
            var structured = await ReadStructuredContentAsync(saved);
            Assert.Equal("Saved", structured.GetProperty("unroadworthyReasonBank").GetString());
            version = structured.GetProperty("caseVersion").GetInt64();
        }

        // The bank holds the wording in its one shape: no trailing stop, lower-case start.
        using (var vocabulary = await PostMcpAsync(client, token, ToolCallPayload(3,
            "pegasus_vocabulary_get", new { })))
        {
            var wordings = (await ReadStructuredContentAsync(vocabulary)).GetProperty("unroadworthyReasons");
            Assert.Contains(
                wordings.EnumerateArray(),
                wording => wording.GetString() == "the parity fixture offside front tyre is below the legal limit");
        }

        using (var again = await PostMcpAsync(client, token, ToolCallPayload(4,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = version,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:parity-bank-2",
                reason = "Automation recorded the roadworthiness again.",
                fields,
                saveUnroadworthyReasonToBank = true
            })))
        {
            Assert.Equal(
                "AlreadyOffered",
                (await ReadStructuredContentAsync(again)).GetProperty("unroadworthyReasonBank").GetString());
        }

        // Asking for the bank without naming the reason refuses the whole call.
        using var refused = await PostMcpAsync(client, token, ToolCallPayload(5,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = version,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:parity-bank-3",
                reason = "Automation asked for the bank with no wording.",
                fields = new Dictionary<string, string?> { [AssessmentVocabulary.LegalStatus] = "unroadworthy" },
                saveUnroadworthyReasonToBank = true
            }));
        Assert.Contains(
            "A bank wording is required.",
            await ReadErrorTextAsync(refused),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Two active Repairers whose names share "Parity Fixture": "Zulu" linked
    /// to the seeded Principal and "Alpha" linked to none.
    /// </summary>
    private static async Task SeedRepairersAsync(
        WebApplicationFactory<Program> factory,
        Guid linkedId,
        Guid otherId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var principalId = await context.Principals
            .Where(principal => principal.Code == QdosPrincipal.Code && principal.IsActive)
            .Select(principal => principal.Id)
            .SingleAsync();
        var administration = scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        await administration.SaveAsync(
            new(
                actor, linkedId, 0, "Zulu Parity Fixture Repairer", null, null, null, null, true,
                ContactRole.Repairer, null, CaseInspectionMode.PhysicalAddress, [principalId],
                $"parity-repairer:{linkedId:N}"),
            CancellationToken.None);
        await administration.SaveAsync(
            new(
                actor, otherId, 0, "Alpha Parity Fixture Repairer", null, null, null, null, true,
                ContactRole.Repairer, null, CaseInspectionMode.PhysicalAddress, [],
                $"parity-repairer:{otherId:N}"),
            CancellationToken.None);
    }

    /// <summary>One enabled, eligible Sign-off Engineer written straight to the database.</summary>
    private static async Task<Guid> SeedSignOffEngineerAsync(
        WebApplicationFactory<Program> factory,
        bool isDefault)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var engineerRole = await context.Roles.SingleAsync(role => role.NormalizedName == "ENGINEER");
        var staffId = Guid.NewGuid();
        var signature = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        context.Users.Add(new PegasusIdentityUser
        {
            Id = staffId,
            UserName = $"parity-signatory-{staffId:N}",
            NormalizedUserName = $"PARITY-SIGNATORY-{staffId:N}",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            IsEnabled = true,
            IsSignOffEngineer = true,
            IsDefaultSignOffEngineer = isDefault,
            SignOffPrintedName = isDefault ? "Parity Default Signatory" : "Parity Chosen Signatory",
            SignOffSignature = signature,
            SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(signature))
        });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = engineerRole.Id });
        await context.SaveChangesAsync();
        return staffId;
    }

    private static async Task<Guid?> RecordedSignOffEngineerAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var workflow = await scope.ServiceProvider
            .GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None);
        return workflow?.SignOffEngineerId;
    }
}
