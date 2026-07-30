using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Pegasus.Core.Identity;
using Pegasus.Web.Authentication;
using Pegasus.Web.Mcp;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed partial class StaffMcpManifestCallerTests
{
    private const string ClientId = "pegasus-development-mcp";
    private const string RedirectUri = "http://127.0.0.1:7890/callback";
    private const string Resource = "https://localhost:7139/mcp";
    private static readonly Guid CaseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReceiptId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TaskId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid TriageId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid EvidenceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OccurrenceId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid VersionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid AuxiliaryId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private const string Lease = "bounded-fixture-case-lease";
    private const string Reason = "Exact MCP OAuth caller contract test.";

    public static IEnumerable<object[]> ManifestBatches()
    {
        var scenarios = CreateScenarios();
        const int BatchSize = 18;
        for (var offset = 0; offset < scenarios.Count; offset += BatchSize)
        {
            yield return
            [
                offset / BatchSize,
                scenarios.Skip(offset).Take(BatchSize).Select(item => item.Name).ToArray()
            ];
        }
    }

    [Theory]
    [MemberData(nameof(ManifestBatches))]
    public async Task EveryManifestEntryTraversesRealOAuthHttpAndItsAuthoritativeCaller(
        int batchNumber,
        string[] toolNames)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        await EnsureMatrixStaffRolesAsync(factory);
        var accessToken = await AcquireAccessTokenAsync(client, $"mcp-matrix-{batchNumber}");
        await InitializeMcpAsync(client, accessToken, 900 + batchNumber);
        var scenarios = CreateScenarios().ToDictionary(item => item.Name, StringComparer.Ordinal);
        var mutationsBefore = await ReadDomainMutationCountAsync(factory.Database);

        foreach (var name in toolNames)
        {
            var scenario = scenarios[name];
            using var request = CreateMcpRequest(
                accessToken,
                requestId: 1_000 + batchNumber * 100 + Array.IndexOf(toolNames, name),
                method: "tools/call",
                parameters: new { name, arguments = scenario.Arguments });
            using var response = await client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(
                response.IsSuccessStatusCode,
                $"{name} did not traverse the authenticated MCP endpoint: {(int)response.StatusCode} {body}");
            using var document = ParseMcpResponse(body);
            Assert.False(
                document.RootElement.TryGetProperty("error", out _),
                $"{name} was rejected before its adapter/Core caller: {body}");
            var result = document.RootElement.GetProperty("result");
            if (result.TryGetProperty("isError", out var isError))
            {
                Assert.False(isError.GetBoolean(), $"{name} returned an MCP binding error: {body}");
            }

            var structured = result.GetProperty("structuredContent");
            AssertContractValidDomainOutcome(name, structured.GetProperty("outcome"));
            Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("System.", body, StringComparison.Ordinal);
            Assert.DoesNotContain("TokenDigest", body, StringComparison.Ordinal);
            Assert.DoesNotContain("<!DOCTYPE", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(
            mutationsBefore,
            await ReadDomainMutationCountAsync(factory.Database));
    }

    [Fact]
    public async Task WriteToolWithReadOnlyGrantIsDeniedBeforeCoreMutation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var accessToken = await AcquireAccessTokenAsync(
            client,
            "mcp-read-only",
            StaffMcpClientContract.ReadScope);
        await InitializeMcpAsync(client, accessToken, 2_000);
        var mutationsBefore = await ReadDomainMutationCountAsync(factory.Database);
        var scenario = Assert.Single(
            CreateScenarios(),
            item => item.Name == AlphaMcpToolNames.IntakeResolve);
        using var request = CreateMcpRequest(
            accessToken,
            requestId: 2_001,
            method: "tools/call",
            parameters: new { name = scenario.Name, arguments = scenario.Arguments });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        using var document = ParseMcpResponse(body);
        var structured = document.RootElement
            .GetProperty("result")
            .GetProperty("structuredContent");
        Assert.Equal(
            "Denied",
            structured.GetProperty("outcome").GetString(),
            ignoreCase: true);
        Assert.Equal(
            mutationsBefore,
            await ReadDomainMutationCountAsync(factory.Database));
        Assert.DoesNotContain("System.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AccessInvalidation.DisabledAccount)]
    [InlineData(AccessInvalidation.RoleChangedAccount)]
    [InlineData(AccessInvalidation.RevokedGrant)]
    [InlineData(AccessInvalidation.RevokedClient)]
    public async Task IssuedTokenIsRejectedImmediatelyAfterDurableAccessInvalidation(
        AccessInvalidation invalidation)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var accessToken = await AcquireAccessTokenAsync(
            client,
            $"mcp-invalidation-{invalidation}");
        await InitializeMcpAsync(client, accessToken, 2_050 + (int)invalidation);
        await InvalidateAccessAsync(factory, invalidation);
        var mutationsBefore = await ReadDomainMutationCountAsync(factory.Database);
        using var request = CreateMcpRequest(
            accessToken,
            requestId: 2_100 + (int)invalidation,
            method: "tools/call",
            parameters: new
            {
                name = AlphaMcpToolNames.OperationsGet,
                arguments = new { }
            });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Equal(
            mutationsBefore,
            await ReadDomainMutationCountAsync(factory.Database));
        Assert.DoesNotContain("System.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WrongResourceCannotMintAnMcpAudienceToken()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var verifier = new string('a', 64);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUri = QueryHelpers.AddQueryString(
            "/connect/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["response_type"] = OpenIddictConstants.ResponseTypes.Code,
                ["scope"] = StaffMcpClientContract.ReadScope,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = OpenIddictConstants.CodeChallengeMethods.Sha256,
                ["resource"] = "https://localhost:7139/not-mcp",
                ["state"] = "wrong-resource"
            });
        var mutationsBefore = await ReadDomainMutationCountAsync(factory.Database);

        using var response = await client.GetAsync(authorizationUri);
        var body = await response.Content.ReadAsStringAsync();
        if (response.Headers.Location is { } callback)
        {
            Assert.Equal(
                OpenIddictConstants.Errors.InvalidTarget,
                Assert.Single(QueryHelpers.ParseQuery(callback.Query)["error"]));
            Assert.False(QueryHelpers.ParseQuery(callback.Query).ContainsKey("code"));
        }
        else
        {
            Assert.Contains("invalid_target", body, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            mutationsBefore,
            await ReadDomainMutationCountAsync(factory.Database));
    }

    [Fact]
    public void CallerMatrixCoversEveryManifestEntryExactlyOnceInManifestOrder()
    {
        var scenarios = CreateScenarios();
        Assert.Equal(
            AlphaMcpToolManifest.Tools.Select(tool => tool.Name),
            scenarios.Select(scenario => scenario.Name));
        Assert.Equal(scenarios.Count, scenarios.Select(scenario => scenario.Name).Distinct().Count());
        foreach (var (descriptor, scenario) in AlphaMcpToolManifest.Tools.Zip(scenarios))
        {
            Assert.Equal(
                descriptor.Schema.Parameters.Select(parameter => parameter.Name).Order(),
                ArgumentNames(scenario.Arguments).Order());
        }
    }

    private static IReadOnlyList<McpCallerScenario> CreateScenarios() =>
    [
        Scenario(AlphaMcpToolNames.OperationsGet, new { }),
        Scenario(AlphaMcpToolNames.IntakeList, new { decision = (int?)null, page = 1, pageSize = 10 }),
        Scenario(AlphaMcpToolNames.IntakeGet, new { receiptId = ReceiptId }),
        Scenario(AlphaMcpToolNames.CasesSearch, new { filters = new { }, page = 1, pageSize = 10 }),
        Scenario(AlphaMcpToolNames.CasesGet, new { caseId = CaseId }),
        Scenario(AlphaMcpToolNames.TriageList, new { state = (int?)null, page = 1, pageSize = 10 }),
        Scenario(AlphaMcpToolNames.TriageGet, new { triageId = TriageId }),

        Scenario(AlphaMcpToolNames.IntakeResolve, new
        {
            receiptId = ReceiptId,
            expectedVersion = 0L,
            operationKey = Operation(AlphaMcpToolNames.IntakeResolve),
            reason = Reason,
            kind = 1,
            correctedDraft = (object?)null
        }),
        Scenario(AlphaMcpToolNames.IntakeReevaluate, new
        {
            receiptId = ReceiptId,
            expectedVersion = 0L,
            operationKey = Operation(AlphaMcpToolNames.IntakeReevaluate),
            reason = Reason
        }),
        Scenario(AlphaMcpToolNames.CasesSave, new
        {
            caseId = CaseId,
            expectedVersion = 0L,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.CasesSave),
            reason = Reason,
            data = new { }
        }),
        Scenario(AlphaMcpToolNames.CasesAcquireEditLease, new
        {
            caseId = CaseId,
            expectedVersion = 0L,
            operationId = AuxiliaryId
        }),
        Scenario(AlphaMcpToolNames.CasesRenewEditLease, new
        {
            caseId = CaseId,
            expectedVersion = 0L,
            operationId = AuxiliaryId,
            editLeaseToken = Lease
        }),
        Scenario(AlphaMcpToolNames.CasesReleaseEditLease, new
        {
            caseId = CaseId,
            operationId = AuxiliaryId,
            editLeaseToken = Lease
        }),
        Scenario(AlphaMcpToolNames.CasesCreateTask, new
        {
            caseId = CaseId,
            taskId = TaskId,
            expectedCaseVersion = 0L,
            operationKey = Operation(AlphaMcpToolNames.CasesCreateTask),
            reason = Reason,
            editLeaseToken = Lease,
            description = "Bounded fixture task",
            assigneeId = (Guid?)null
        }),
        Scenario(AlphaMcpToolNames.CasesAssignTask, new
        {
            caseId = CaseId,
            taskId = TaskId,
            expectedCaseVersion = 0L,
            expectedTaskVersion = 0L,
            operationKey = Operation(AlphaMcpToolNames.CasesAssignTask),
            reason = Reason,
            editLeaseToken = Lease,
            assigneeId = (Guid?)null
        }),
        Scenario(AlphaMcpToolNames.TriageAssign, TriageMutation(
            AlphaMcpToolNames.TriageAssign,
            ("assigneeId", (object)AuxiliaryId))),
        Scenario(AlphaMcpToolNames.TriageUnassign, TriageMutation(AlphaMcpToolNames.TriageUnassign)),
        Scenario(AlphaMcpToolNames.TriageRecordFinding, TriageMutation(
            AlphaMcpToolNames.TriageRecordFinding,
            ("roadworthiness", 0),
            ("assessment", null))),
        Scenario(AlphaMcpToolNames.TriageSupersedeFinding, TriageMutation(
            AlphaMcpToolNames.TriageSupersedeFinding,
            ("supersedesFindingId", AuxiliaryId),
            ("roadworthiness", 0),
            ("assessment", null))),
        Scenario(AlphaMcpToolNames.TriageLinkResponse, TriageMutation(
            AlphaMcpToolNames.TriageLinkResponse,
            ("pollOutcomeId", AuxiliaryId),
            ("sentEvidenceId", EvidenceId))),
        Scenario(AlphaMcpToolNames.TriageUnlinkResponse, TriageMutation(
            AlphaMcpToolNames.TriageUnlinkResponse,
            ("sentEvidenceId", EvidenceId))),
        Scenario(AlphaMcpToolNames.TriageLinkCase, TriageCaseMutation(AlphaMcpToolNames.TriageLinkCase)),
        Scenario(AlphaMcpToolNames.TriageUnlinkCase, TriageCaseMutation(AlphaMcpToolNames.TriageUnlinkCase)),

        Scenario(AlphaMcpToolNames.IntakeAccept, new
        {
            receiptId = ReceiptId,
            expectedVersion = 0L,
            operationKey = Operation(AlphaMcpToolNames.IntakeAccept),
            reason = Reason,
            caseType = 0,
            principalCode = "ABC",
            completeness = Completeness(),
            standaloneAuditEvidenceId = (Guid?)null,
            acceptedInspectionDeadline = (DateOnly?)null
        }),
        Scenario(AlphaMcpToolNames.IntakeLinkCase, IntakeAssociation(AlphaMcpToolNames.IntakeLinkCase)),
        Scenario(AlphaMcpToolNames.IntakeUnlinkCase, IntakeAssociation(AlphaMcpToolNames.IntakeUnlinkCase)),
        Scenario(AlphaMcpToolNames.CasesConfirmCompleteness, new
        {
            caseId = CaseId,
            expectedVersion = 0L,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.CasesConfirmCompleteness),
            reason = Reason,
            completeness = Completeness()
        }),
        Scenario(AlphaMcpToolNames.CasesHold, CaseMutation(AlphaMcpToolNames.CasesHold)),
        Scenario(AlphaMcpToolNames.CasesReleaseHold, CaseMutation(AlphaMcpToolNames.CasesReleaseHold)),
        Scenario(AlphaMcpToolNames.CasesTransition, CaseMutation(
            AlphaMcpToolNames.CasesTransition,
            ("destination", 0),
            ("readiness", ReviewReadiness()))),
        Scenario(AlphaMcpToolNames.CasesClose, CaseMutation(
            AlphaMcpToolNames.CasesClose,
            ("outcome", 0))),
        Scenario(AlphaMcpToolNames.CasesReopen, CaseMutation(
            AlphaMcpToolNames.CasesReopen,
            ("destination", 0),
            ("readiness", null))),
        Scenario(AlphaMcpToolNames.CasesArchive, CaseMutation(AlphaMcpToolNames.CasesArchive)),
        Scenario(AlphaMcpToolNames.CasesCreateLinkedReplacement, CaseMutation(
            AlphaMcpToolNames.CasesCreateLinkedReplacement,
            ("replacementPrincipalCode", "ABC"))),
        Scenario(AlphaMcpToolNames.CasesCompleteTask, ExistingTaskMutation(AlphaMcpToolNames.CasesCompleteTask)),
        Scenario(AlphaMcpToolNames.CasesCancelTask, ExistingTaskMutation(AlphaMcpToolNames.CasesCancelTask)),
        Scenario(AlphaMcpToolNames.CasesRecordEngineerFinding, CaseMutation(
            AlphaMcpToolNames.CasesRecordEngineerFinding,
            ("assessment", 0))),
        Scenario(AlphaMcpToolNames.TriageComplete, TriageMutation(AlphaMcpToolNames.TriageComplete)),
        Scenario(AlphaMcpToolNames.TriageCancel, TriageMutation(AlphaMcpToolNames.TriageCancel)),
        Scenario(AlphaMcpToolNames.TriageReopen, TriageMutation(AlphaMcpToolNames.TriageReopen)),
        Scenario(AlphaMcpToolNames.DocumentsLogicalRemove, new
        {
            caseId = CaseId,
            occurrenceId = OccurrenceId,
            expectedCaseVersion = 0L,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.DocumentsLogicalRemove),
            reason = Reason
        }),

        Scenario(AlphaMcpToolNames.DocumentsDownload, new
        {
            caseId = CaseId,
            occurrenceId = OccurrenceId,
            versionId = VersionId,
            operationKey = Operation(AlphaMcpToolNames.DocumentsDownload)
        }),
        Scenario(AlphaMcpToolNames.DocumentsExport, new
        {
            caseId = CaseId,
            expectedCaseVersion = 0L,
            editLeaseToken = Lease,
            selections = new[] { new { occurrenceId = OccurrenceId, versionId = VersionId } },
            operationKey = Operation(AlphaMcpToolNames.DocumentsExport)
        }),

        Scenario(AlphaMcpToolNames.RequestsCreateBox, new
        {
            caseId = CaseId,
            expectedCaseVersion = 0L,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.RequestsCreateBox),
            expiresAtUtc = (DateTimeOffset?)null
        }),
        Scenario(AlphaMcpToolNames.RequestsRevokeBox, new
        {
            caseId = CaseId,
            fileRequestId = AuxiliaryId,
            expectedFileRequestVersion = 0L,
            expectedCaseVersion = 0L,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.RequestsRevokeBox),
            reason = Reason
        }),
        Scenario(AlphaMcpToolNames.RequestsCreateUpload, new
        {
            caseId = CaseId,
            expectedCaseVersion = 0L,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.RequestsCreateUpload)
        }),
        Scenario(AlphaMcpToolNames.RequestsRevokeUpload, new
        {
            caseId = CaseId,
            requestId = AuxiliaryId,
            expectedRequestVersion = 0L,
            expectedCaseVersion = 0L,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.RequestsRevokeUpload),
            reason = Reason
        }),
        Scenario(AlphaMcpToolNames.VehicleRequestLookup, new
        {
            caseId = CaseId,
            expectedCaseVersion = 0L,
            registration = "AB12CDE",
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.VehicleRequestLookup)
        }),
        Scenario(AlphaMcpToolNames.VehicleAcceptSuggestion, new
        {
            caseId = CaseId,
            expectedCaseVersion = 0L,
            lookupObservationId = AuxiliaryId,
            decision = 0,
            correction = (object?)null,
            editLeaseToken = Lease,
            operationKey = Operation(AlphaMcpToolNames.VehicleAcceptSuggestion),
            reason = Reason
        }),
        Scenario(AlphaMcpToolNames.ReportsGenerateEva, new
        {
            caseId = CaseId,
            expectedCaseVersion = 0L,
            overviewImageOccurrenceId = OccurrenceId,
            mainDamageImageOccurrenceId = AuxiliaryId,
            orderedImageOccurrenceIds = new[] { OccurrenceId, AuxiliaryId },
            operationKey = Operation(AlphaMcpToolNames.ReportsGenerateEva),
            reason = Reason,
            editLeaseToken = Lease
        }),
        Scenario(AlphaMcpToolNames.ReportsLinkEvidence, ReportEvidence(AlphaMcpToolNames.ReportsLinkEvidence)),
        Scenario(AlphaMcpToolNames.ReportsUnlinkEvidence, ReportEvidence(AlphaMcpToolNames.ReportsUnlinkEvidence))
    ];

    private static McpCallerScenario Scenario(string name, object arguments) => new(name, arguments);

    private static Dictionary<string, object?> TriageMutation(
        string name,
        params (string Name, object? Value)[] additions) =>
        Add(
            new()
            {
                ["triageId"] = TriageId,
                ["expectedVersion"] = 0L,
                ["operationKey"] = Operation(name),
                ["reason"] = Reason
            },
            additions);

    private static Dictionary<string, object?> TriageCaseMutation(string name) =>
        new()
        {
            ["triageId"] = TriageId,
            ["caseId"] = CaseId,
            ["expectedTriageVersion"] = 0L,
            ["expectedCaseVersion"] = 0L,
            ["caseEditLeaseToken"] = Lease,
            ["operationKey"] = Operation(name),
            ["reason"] = Reason
        };

    private static Dictionary<string, object?> IntakeAssociation(string name) =>
        new()
        {
            ["receiptId"] = ReceiptId,
            ["caseId"] = CaseId,
            ["expectedIntakeVersion"] = 0L,
            ["expectedCaseVersion"] = 0L,
            ["editLeaseToken"] = Lease,
            ["operationKey"] = Operation(name),
            ["reason"] = Reason
        };

    private static Dictionary<string, object?> CaseMutation(
        string name,
        params (string Name, object? Value)[] additions) =>
        Add(
            new()
            {
                ["caseId"] = CaseId,
                ["expectedVersion"] = 0L,
                ["operationKey"] = Operation(name),
                ["reason"] = Reason,
                ["editLeaseToken"] = Lease
            },
            additions);

    private static Dictionary<string, object?> ExistingTaskMutation(string name) =>
        new()
        {
            ["caseId"] = CaseId,
            ["taskId"] = TaskId,
            ["expectedCaseVersion"] = 0L,
            ["expectedTaskVersion"] = 0L,
            ["operationKey"] = Operation(name),
            ["reason"] = Reason,
            ["editLeaseToken"] = Lease
        };

    private static Dictionary<string, object?> ReportEvidence(string name) =>
        new()
        {
            ["caseId"] = CaseId,
            ["evidenceId"] = EvidenceId,
            ["expectedVersion"] = 0L,
            ["operationKey"] = Operation(name),
            ["reason"] = Reason,
            ["editLeaseToken"] = Lease
        };

    private static object Completeness() => new
    {
        instructionComplete = false,
        imagesComplete = false,
        instructionConfirmedByStaff = false,
        imagesConfirmedByStaff = false
    };

    private static object ReviewReadiness() => new
    {
        instructionsComplete = true,
        imagesComplete = true,
        instructionsReviewedByStaff = true,
        imagesReviewedByStaff = true,
        evidenceReference = "mcp-matrix-review-readiness"
    };

    private static Dictionary<string, object?> Add(
        Dictionary<string, object?> values,
        params (string Name, object? Value)[] additions)
    {
        foreach (var (name, value) in additions)
        {
            values.Add(name, value);
        }
        return values;
    }

    private static IEnumerable<string> ArgumentNames(object arguments) =>
        arguments is IReadOnlyDictionary<string, object?> dictionary
            ? dictionary.Keys
            : arguments.GetType()
                .GetProperties()
                .Select(property => property.Name);

    private static string Operation(string name) => $"matrix:{name}";

    private static void AssertContractValidDomainOutcome(string name, JsonElement outcome)
    {
        if (outcome.ValueKind == JsonValueKind.Number)
        {
            var numericOutcome = outcome.GetInt32();
            Assert.NotEqual(3, numericOutcome);
            Assert.NotEqual(5, numericOutcome);
            return;
        }

        var value = outcome.GetString();
        Assert.False(string.Equals(value, "Denied", StringComparison.OrdinalIgnoreCase), name);
        Assert.False(string.Equals(value, "Invalid", StringComparison.OrdinalIgnoreCase), name);
    }

    private static async Task<long> ReadDomainMutationCountAsync(LocalDbTestDatabase database) =>
        await database.ScalarAsync<long>(
            "SELECT COALESCE(SUM(CONVERT(bigint, partitions.rows)), 0) " +
            "FROM sys.tables AS tables " +
            "INNER JOIN sys.partitions AS partitions ON partitions.object_id = tables.object_id " +
            "AND partitions.index_id IN (0, 1) " +
            "WHERE tables.name NOT LIKE 'AspNet%' " +
            "AND tables.name NOT LIKE 'OpenIddict%' " +
            "AND tables.name <> '__EFMigrationsHistory';");

    private static async Task EnsureMatrixStaffRolesAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var administrator = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var result = await scope.ServiceProvider
            .GetRequiredService<IAssignStaffRoles>()
            .ExecuteAsync(
                new(
                    administrator,
                    DevelopmentOfflineIdentity.AdministratorId,
                    [StaffRole.Administrator, StaffRole.Engineer],
                    "Exercise every exact MCP role-bound caller.",
                    $"mcp-matrix-roles-{Guid.NewGuid():N}"),
                default);
        Assert.Contains(StaffRole.Administrator, result.Account.Roles);
        Assert.Contains(StaffRole.Engineer, result.Account.Roles);
    }

    private static async Task InvalidateAccessAsync(
        IntakeWebApplicationFactory factory,
        AccessInvalidation invalidation)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var administrator = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var suffix = Guid.NewGuid().ToString("N");

        if (invalidation is AccessInvalidation.DisabledAccount
            or AccessInvalidation.RoleChangedAccount)
        {
            var create = services.GetRequiredService<ICreateStaffAccount>();
            var successor = await create.ExecuteAsync(
                new(
                    administrator,
                    $"mcp-successor-{suffix}",
                    "temporary-password",
                    "Preserve an administrator during the access invalidation test.",
                    $"create-successor-{suffix}"),
                default);
            var assign = services.GetRequiredService<IAssignStaffRoles>();
            var promoted = await assign.ExecuteAsync(
                new(
                    administrator,
                    successor.Account.Id,
                    [StaffRole.Administrator],
                    "Preserve an administrator during the access invalidation test.",
                    $"promote-successor-{suffix}"),
                default);
            Assert.Contains(StaffRole.Administrator, promoted.Account.Roles);
        }

        switch (invalidation)
        {
            case AccessInvalidation.DisabledAccount:
            {
                var command = services.GetRequiredService<IDisableStaffAccount>();
                var result = await command.ExecuteAsync(
                    new(
                        administrator,
                        DevelopmentOfflineIdentity.AdministratorId,
                        "Disable the issued-token account.",
                        $"disable-token-account-{suffix}"),
                    default);
                Assert.True(result.RevokedTokens > 0);
                break;
            }
            case AccessInvalidation.RoleChangedAccount:
            {
                var command = services.GetRequiredService<IAssignStaffRoles>();
                var result = await command.ExecuteAsync(
                    new(
                        administrator,
                        DevelopmentOfflineIdentity.AdministratorId,
                        [StaffRole.User],
                        "Change the issued-token account role.",
                        $"change-token-role-{suffix}"),
                    default);
                Assert.True(result.RevokedTokens > 0);
                break;
            }
            case AccessInvalidation.RevokedGrant:
            {
                var command = services.GetRequiredService<IRevokeStaffMcpAuthorizations>();
                var result = await command.ExecuteAsync(
                    new(
                        administrator,
                        DevelopmentOfflineIdentity.AdministratorId,
                        "Revoke the issued-token authorization grant.",
                        $"revoke-token-grant-{suffix}"),
                    default);
                Assert.True(result.RevokedTokens > 0);
                break;
            }
            case AccessInvalidation.RevokedClient:
            {
                var command = services.GetRequiredService<IRevokePublicMcpClient>();
                var result = await command.ExecuteAsync(
                    new(
                        administrator,
                        ClientId,
                        "Revoke the issued-token public client.",
                        $"revoke-token-client-{suffix}"),
                    default);
                Assert.True(result.RevokedTokens > 0);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(invalidation));
        }
    }

    public enum AccessInvalidation
    {
        DisabledAccount,
        RoleChangedAccount,
        RevokedGrant,
        RevokedClient
    }

    private static async Task<string> AcquireAccessTokenAsync(
        HttpClient client,
        string state,
        string scopes = StaffMcpClientContract.ReadScope + " " + StaffMcpClientContract.WriteScope)
    {
        var verifier = new string('a', 64);
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizationUri = QueryHelpers.AddQueryString(
            "/connect/authorize",
            new Dictionary<string, string?>
            {

                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["response_type"] = OpenIddictConstants.ResponseTypes.Code,
                ["scope"] = scopes,
                ["code_challenge"] = challenge,
                ["code_challenge_method"] = OpenIddictConstants.CodeChallengeMethods.Sha256,
                ["resource"] = Resource,
                ["state"] = state
            });
        using var consentResponse = await client.GetAsync(authorizationUri);
        var consentHtml = await consentResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, consentResponse.StatusCode);
        var approvalForm = ReadHiddenFormValues(consentHtml);
        approvalForm.Add(KeyValuePair.Create("decision", "approve"));

        using var approvalResponse = await client.PostAsync(
            "/connect/authorize",
            new FormUrlEncodedContent(approvalForm));
        Assert.Equal(HttpStatusCode.Redirect, approvalResponse.StatusCode);
        var callback = Assert.IsType<Uri>(approvalResponse.Headers.Location);
        var code = Assert.Single(QueryHelpers.ParseQuery(callback.Query)["code"]);
        using var tokenResponse = await client.PostAsync(
            "/connect/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIddictConstants.GrantTypes.AuthorizationCode,
                ["client_id"] = ClientId,
                ["redirect_uri"] = RedirectUri,
                ["code"] = code!,
                ["code_verifier"] = verifier,
                ["resource"] = Resource
            }));
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        using var tokenDocument = JsonDocument.Parse(tokenBody);
        return Assert.IsType<string>(tokenDocument.RootElement.GetProperty("access_token").GetString());
    }

    private static async Task InitializeMcpAsync(
        HttpClient client,
        string accessToken,
        int requestId)
    {
        using var request = CreateMcpRequest(
            accessToken,
            requestId,
            method: "initialize",
            parameters: new
            {
                protocolVersion = "2025-11-25",
                capabilities = new { },
                clientInfo = new
                {
                    name = "Pegasus MCP caller contract tests",
                    version = "1"
                }
            });
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"Authenticated MCP initialization failed: {(int)response.StatusCode} {body}");
        using var document = ParseMcpResponse(body);
        Assert.False(
            document.RootElement.TryGetProperty("error", out _),
            $"Authenticated MCP initialization returned a JSON-RPC error: {body}");
        var negotiatedProtocolVersion = Assert.IsType<string>(
            document.RootElement
                .GetProperty("result")
                .GetProperty("protocolVersion")
                .GetString());
        client.DefaultRequestHeaders.Remove("MCP-Protocol-Version");
        client.DefaultRequestHeaders.Add(
            "MCP-Protocol-Version",
            negotiatedProtocolVersion);
        using var initializedRequest = CreateMcpNotification(
            accessToken,
            method: "notifications/initialized",
            parameters: new { });
        using var initializedResponse = await client.SendAsync(initializedRequest);
        var initializedBody = await initializedResponse.Content.ReadAsStringAsync();
        Assert.True(
            initializedResponse.IsSuccessStatusCode,
            $"Authenticated MCP initialized notification failed: {(int)initializedResponse.StatusCode} {initializedBody}");
        Assert.Equal(HttpStatusCode.Accepted, initializedResponse.StatusCode);
    }

    private static HttpRequestMessage CreateMcpNotification(
        string accessToken,
        string method,
        object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                method,
                @params = parameters
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static HttpRequestMessage CreateMcpRequest(
        string accessToken,
        int requestId,
        string method,
        object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = requestId,
                method,
                @params = parameters
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static JsonDocument ParseMcpResponse(string response)
    {
        const string DataPrefix = "data: ";
        if (response.TrimStart().StartsWith('{'))
        {
            return JsonDocument.Parse(response);
        }
        var data = response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Last(line => line.StartsWith(DataPrefix, StringComparison.Ordinal));
        return JsonDocument.Parse(data[DataPrefix.Length..]);
    }

    private static List<KeyValuePair<string, string>> ReadHiddenFormValues(string html)
    {
        var form = ConsentFormRegex().Match(html);
        Assert.True(form.Success, "The staff MCP consent form must be present.");
        var fields = new List<KeyValuePair<string, string>>();
        foreach (Match tag in HiddenInputTagRegex().Matches(form.Groups["content"].Value))
        {
            var name = InputNameRegex().Match(tag.Value);
            if (!name.Success)
            {
                continue;
            }
            var value = InputValueRegex().Match(tag.Value);
            fields.Add(KeyValuePair.Create(
                WebUtility.HtmlDecode(name.Groups["value"].Value),
                value.Success ? WebUtility.HtmlDecode(value.Groups["value"].Value) : string.Empty));
        }
        return fields;
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    [GeneratedRegex("<form[^>]*id=\"staff-mcp-consent\"[^>]*>(?<content>.*?)</form>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex ConsentFormRegex();

    [GeneratedRegex("<input[^>]*type=\"hidden\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HiddenInputTagRegex();

    [GeneratedRegex("\\sname=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputNameRegex();

    [GeneratedRegex("\\svalue=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputValueRegex();

    private sealed record McpCallerScenario(string Name, object Arguments);
}
