using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Documents;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Tier-4 caller-equivalent evidence for the assessment tranche of the
/// Automation Actor toolset: real HTTP against the gated /mcp surface, real
/// LocalDB persistence, and the same attribution assertions as the original
/// nine-tool ingress tests.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationAssessmentIngressTests
{
    [Fact]
    public async Task CanonicalEstimateImportThroughMcpPersistsUnconfirmedSourceBackedRowsAndRejectsForeignOrStaleAuthority()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var bytes = Encoding.UTF8.GetBytes(GlassEstimateXmlParserTests.GlassExport.BuildXml());
        var content = new RetainedEstimateContent(bytes);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReadLogicalDocumentVersion>();
            services.AddSingleton<IReadLogicalDocumentVersion>(content);
        }));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var documentId = Guid.NewGuid(); var versionId = Guid.NewGuid(); var occurrenceId = Guid.NewGuid();
        var currentVersionId = Guid.NewGuid(); var currentOccurrenceId = Guid.NewGuid();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            db.AddRange(
                new CaseDocumentEntity { Id = documentId, CaseId = caseId, Ordinal = 1, SourceOccurrenceIdentity = "estimate-import:mcp-caller" },
                new DocumentVersionEntity
                {
                    Id = versionId, DocumentId = documentId, Version = 1, FileName = "estimate.xml", MediaType = "application/xml",
                    ContentLength = bytes.Length, Sha256 = hash, CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = ClientId, IsCurrent = false
                },
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId, CaseId = caseId, DocumentId = documentId, VersionId = versionId, Ordinal = 1,
                    SourceOccurrenceIdentity = "estimate-import:mcp-caller", OperationKey = "mcp-source",
                    SemanticRole = DocumentSemanticRole.Other, Source = DocumentSource.StaffUpload, RecordedAtUtc = DateTimeOffset.UtcNow
                },
                new DocumentVersionEntity
                {
                    Id = currentVersionId, DocumentId = documentId, Version = 2, FileName = "estimate.xml", MediaType = "application/xml",
                    ContentLength = bytes.Length, Sha256 = hash, CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = DateTimeOffset.UtcNow, CreatedBy = ClientId, IsCurrent = true
                },
                new DocumentOccurrenceEntity
                {
                    Id = currentOccurrenceId, CaseId = caseId, DocumentId = documentId, VersionId = currentVersionId, Ordinal = 2,
                    SourceOccurrenceIdentity = "estimate-import:mcp-current", OperationKey = "mcp-source-current",
                    SemanticRole = DocumentSemanticRole.Other, Source = DocumentSource.StaffUpload, RecordedAtUtc = DateTimeOffset.UtcNow
                });
            await db.SaveChangesAsync();
            var metadata = scope.ServiceProvider.GetRequiredService<IGetCaseDocumentMetadata>();
            var actor = ActionActor.Automation(ClientId);
            Assert.NotNull(await metadata.ExecuteAsync(new(caseId, occurrenceId, versionId, actor), default));
            Assert.NotNull(await metadata.ExecuteAsync(new(caseId, currentOccurrenceId, currentVersionId, actor), default));
            Assert.Null(await metadata.ExecuteAsync(new(caseId, currentOccurrenceId, versionId, actor), default));
        }
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 80);
        object Arguments(long version, string leaseToken, Guid occurrence, string sha, string key) => new
        {
            caseId, expectedVersion = version, editLeaseToken = leaseToken, operationKey = key,
            name = "Glass's 1", occurrenceId = occurrence, documentVersionId = versionId, sha256 = sha
        };
        using (var wrongScope = await PostMcpAsync(client, await RequestTokenAsync(client, "automation.cases"),
            ToolCallPayload(81, "pegasus_estimate_import", Arguments(0, lease.LeaseToken, occurrenceId, hash, "mcp:wrong-scope"))))
        {
            using var refusal = await ReadJsonRpcAsync(wrongScope);
            Assert.Contains("error", refusal.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        using (var beforeHandoff = await PostMcpAsync(client, token, ToolCallPayload(86, "pegasus_estimate_import",
            Arguments(0, lease.LeaseToken, occurrenceId, hash, "mcp:before-handoff"))))
        {
            using var refusal = await ReadJsonRpcAsync(beforeHandoff);
            Assert.Contains("read-only", refusal.RootElement.ToString(), StringComparison.Ordinal);
        }
        Assert.Equal(0, content.Reads);
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseRepairSpecifications"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>("SELECT COUNT(*) FROM IntakeOcrOperations"));
        // Exercise the real native handoff; accepted Review alone is not
        // engineering authority, and a fixture state assignment would hide it.
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
            var engineerId = Guid.NewGuid();
            var role = await db.Roles.SingleOrDefaultAsync(row => row.NormalizedName == "ENGINEER");
            if (role is null)
            {
                role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Engineer", NormalizedName = "ENGINEER" };
                db.Roles.Add(role);
            }
            db.Users.Add(new PegasusIdentityUser
            {
                Id = engineerId, UserName = "import-engineer", NormalizedUserName = "IMPORT-ENGINEER",
                IsEnabled = true, MustChangePassword = false,
                SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = engineerId, RoleId = role.Id });
            await db.SaveChangesAsync();
            var workflow = await db.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId);
            Assert.Equal(CaseLifecycleState.Review.ToString(), workflow.State);
            var handedOff = await scope.ServiceProvider.GetRequiredService<IAssignCaseEngineer>().ExecuteAsync(
                new(caseId, 0, ActionActor.Automation(workflow.EditLeaseHolder!), "mcp:import-handoff",
                    "Hand off to Engineer", lease.LeaseToken, engineerId, new(true, true, "accepted-readiness")), default);
            Assert.Equal(CaseLifecycleState.ReportPreparation, handedOff.State);
            Assert.Equal(1, handedOff.Version);
        }
        lease = await BeginEditAsync(client, token, caseId, 1, rpcId: 87);
        foreach (var arguments in new[]
        {
            Arguments(0, lease.LeaseToken, occurrenceId, hash, "mcp:stale-import"),
            Arguments(1, "wrong-lease", occurrenceId, hash, "mcp:wrong-lease"),
            Arguments(1, lease.LeaseToken, Guid.NewGuid(), hash, "mcp:foreign-source"),
            Arguments(1, lease.LeaseToken, currentOccurrenceId, hash, "mcp:mismatched-version"),
            Arguments(1, lease.LeaseToken, occurrenceId, new string('a', 64), "mcp:wrong-hash")
        })
        {
            using var refused = await PostMcpAsync(client, token, ToolCallPayload(82, "pegasus_estimate_import", arguments));
            using var refusal = await ReadJsonRpcAsync(refused);
            Assert.Contains("error", refusal.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(0, content.Reads);
        Guid importedId;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(83, "pegasus_estimate_import",
            Arguments(1, lease.LeaseToken, occurrenceId, hash, "mcp:canonical-import"))))
        {
            importedId = (await ReadStructuredContentAsync(response)).GetProperty("estimateId").GetGuid();
        }
        Assert.Equal(1, content.Reads);
        var replayLease = await BeginEditAsync(client, token, caseId, 2, rpcId: 84);
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(85, "pegasus_estimate_import",
            Arguments(2, replayLease.LeaseToken, occurrenceId, hash, "mcp:canonical-import-replay"))))
            Assert.Equal(importedId, (await ReadStructuredContentAsync(response)).GetProperty("estimateId").GetGuid());
        Assert.Equal(1, content.Reads);
        await using var readScope = mcpFactory.Services.CreateAsyncScope();
        var store = readScope.ServiceProvider.GetRequiredService<IRepairSpecificationStore>();
        var imported = Assert.IsType<RepairSpecificationVersion>(await store.GetVersionAsync(caseId, importedId, default));
        Assert.False(imported.IsCurrent);
        Assert.Null(imported.AiJobId);
        Assert.Equal(RepairerVatStatus.Unknown, imported.Details.VatPolicy.RepairerStatus);
        Assert.Equal(14, imported.Lines.Count);
        Assert.All(imported.Lines, line =>
        {
            Assert.False(line.IsConfirmed);
            Assert.Equal(ActorKind.Automation, line.RecordedByKind);
            Assert.Equal(versionId, line.SourceDocumentVersionId);
            Assert.Equal(hash, line.SourceDocumentSha256);
        });
        Assert.Single(await store.ListEstimatesAsync(caseId, default));
        await store.RequireImportAuthorityAsync(new(ActionActor.Automation(imported.CreatedBy), caseId, 2, replayLease.LeaseToken,
            occurrenceId, versionId, hash, "mcp:replay-authority", "Glass's 1"), default);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE EventKind = N'estimate_created' AND ActorKind = N'Automation'"));
    }

    private sealed class RetainedEstimateContent(byte[] bytes) : IReadLogicalDocumentVersion
    {
        public int Reads { get; private set; }
        public Task<LogicalDocumentContent> OpenAsync(ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken)
        {
            Reads++;
            Assert.Equal(bytes.Length, request.ExpectedContentLength);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), request.ExpectedSha256);
            return Task.FromResult(new LogicalDocumentContent(new MemoryStream(bytes), request.DocumentId,
                request.VersionId, null, request.ExpectedSha256, bytes.Length, "estimate.xml", "application/xml"));
        }
    }

    [Fact]
    public async Task EstimateImportInvokesTheCanonicalTypedBoundary()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var importer = new CapturingEstimateImporter();
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IImportRawEstimate>();
                services.AddSingleton<IImportRawEstimate>(importer);
            }));
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AutomationMcp.AssessmentScope);
        var caseId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        const string hash = "D4A5AA6B20AE98EE062CC5852A1B1A447B3DA98B0D468DD7E3360A5CC3D2A72C";

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(
            80, "pegasus_estimate_import", new
            {
                caseId,
                expectedVersion = 7,
                editLeaseToken = "lease-token",
                operationKey = "mcp:estimate-import",
                name = "Audatex 1",
                occurrenceId,
                documentVersionId = versionId,
                sha256 = hash
            })))
        {
            var result = await ReadStructuredContentAsync(response);
            Assert.Equal(CapturingEstimateImporter.EstimateId, result.GetProperty("estimateId").GetGuid());
            Assert.Equal("Audatex 1", result.GetProperty("name").GetString());
        }
        var request = Assert.IsType<ImportRawEstimateRequest>(importer.Request);
        Assert.Equal(ActorKind.Automation, request.Actor.Kind);
        Assert.Equal(caseId, request.CaseId);
        Assert.Equal(occurrenceId, request.OccurrenceId);
        Assert.Equal(versionId, request.DocumentVersionId);
        Assert.Equal(hash, request.Sha256);

        Assert.Equal(1, importer.Calls);
    }

    [Fact]
    public async Task AssessmentUpdateRejectsDirectWritesToDerivedImpactFields()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 40);

        using var response = await PostMcpAsync(client, token, ToolCallPayload(41,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:derived-impact-rejected",
                reason = "Attempt a direct derived write.",
                fields = new Dictionary<string, string?> { ["assessment.impact_location"] = "front" }
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains("derived from damage.impacts", document.RootElement.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE CaseId = '{caseId:D}'"));

        using var estimateResponse = await PostMcpAsync(client, token, ToolCallPayload(42,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:generic-estimate-rejected",
                reason = "Attempt a generic estimate write.",
                estimateLines = new[] { new { description = "Repair" } }
            }));
        using var estimateDocument = await ReadJsonRpcAsync(estimateResponse);
        Assert.Contains("named estimate command", estimateDocument.RootElement.ToString(), StringComparison.Ordinal);

        using var rateResponse = await PostMcpAsync(client, token, ToolCallPayload(43,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:generic-rate-rejected",
                reason = "Attempt an estimate-owned rate write.",
                fields = new Dictionary<string, string?> { [AssessmentVocabulary.RateCard] = "standard" }
            }));
        using var rateDocument = await ReadJsonRpcAsync(rateResponse);
        Assert.Contains("named estimate command", rateDocument.RootElement.ToString(), StringComparison.Ordinal);

        using var findingResponse = await PostMcpAsync(client, token, ToolCallPayload(44,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:generic-finding-rejected",
                reason = "Attempt a professional finding write.",
                fields = new Dictionary<string, string?> { [AssessmentVocabulary.ValueEngineer] = "12000" }
            }));
        using var findingDocument = await ReadJsonRpcAsync(findingResponse);
        Assert.Contains("named professional command", findingDocument.RootElement.ToString(), StringComparison.Ordinal);

        using var signatoryResponse = await PostMcpAsync(client, token, ToolCallPayload(45,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:generic-signatory-rejected",
                reason = "Attempt a signatory write.",
                fields = new Dictionary<string, string?> { [AssessmentVocabulary.EngineerSignature] = "signed" }
            }));
        using var signatoryDocument = await ReadJsonRpcAsync(signatoryResponse);
        Assert.Contains("named signatory command", signatoryDocument.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssessmentToolsEnforceTheAssessmentScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();

        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");
        using var response = await PostMcpAsync(
            client,
            casesOnlyToken,
            ToolCallPayload(
                1,
                "pegasus_assessment_get",
                new { caseId = Guid.NewGuid() }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains(
            "automation.assessment",
            document.RootElement.ToString(),
            StringComparison.Ordinal);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task AssessmentUpdateOverHttpMutatesUnderLeaseWithCorrelatedAttribution()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        Guid workRequestId;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var created = await scope.ServiceProvider
                .GetRequiredService<IAiWorkRequestStore>()
                .CreateAsync(
                    new(
                        caseId,
                        "fixture-reference",
                        0,
                        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                        "ingress-send-op",
                        "Work the assessment.",
                        TimeSpan.FromHours(24)),
                    CancellationToken.None);
            workRequestId = created.RequestId;
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        // The automation claims the same server-owned edit lease as staff.
        long caseVersion;
        string leaseToken;
        using (var leaseResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                2,
                "pegasus_case_edit_begin",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    operationKey = "mcp:ingress-lease-1"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, leaseResponse.StatusCode);
            using var leaseDocument = await ReadJsonRpcAsync(leaseResponse);
            var lease = leaseDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            caseVersion = lease.GetProperty("caseVersion").GetInt64();
            leaseToken = lease.GetProperty("leaseToken").GetString()!;
        }

        using (var updateResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                3,
                "pegasus_assessment_update",
                new
                {
                    caseId,
                    expectedVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:ingress-assessment-1",
                    reason = "Automation recorded the assessment draft.",
                    fields = new Dictionary<string, string?>
                    {
                        ["vehicle.condition"] = "good",
                        ["vehicle.colour"] = "Blue"
                    },
                    workRequestId = workRequestId.ToString("D")
                })))
        {
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            using var updateDocument = await ReadJsonRpcAsync(updateResponse);
            var result = updateDocument.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
            Assert.Equal(
                workRequestId.ToString("D"),
                structured.GetProperty("correlationId").GetString());
            var fields = structured.GetProperty("fields").EnumerateArray().ToArray();
            Assert.All(fields, field => Assert.False(field.GetProperty("isConfirmed").GetBoolean()));
        }

        // Stored values carry the unconfirmed automation provenance.
        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM CaseAssessmentFields
            WHERE RecordedByKind = N'Automation' AND ConfirmedBy IS NULL
            """));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM CaseEstimateLines WHERE RecordedByKind = N'Automation'"));

        // Logging parity: the business save is recorded exactly like a staff
        // save, and the ingress attribution row correlates to the hand-off.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'case_assessment_saved'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_assessment_update'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'{workRequestId:D}'
            """));

        // A replayed operation key returns the original result.
        using (var replayResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                4,
                "pegasus_assessment_update",
                new
                {
                    caseId,
                    expectedVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:ingress-assessment-1",
                    reason = "Automation recorded the assessment draft.",
                    fields = new Dictionary<string, string?>
                    {
                        ["vehicle.condition"] = "good",
                        ["vehicle.colour"] = "Blue"
                    },
                    workRequestId = workRequestId.ToString("D")
                })))
        {
            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
            using var replayDocument = await ReadJsonRpcAsync(replayResponse);
            var structured = replayDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
        }

        // The read-back tool exposes the recorded surface with readiness.
        using (var getResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(5, "pegasus_assessment_get", new { caseId })))
        {
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            using var getDocument = await ReadJsonRpcAsync(getResponse);
            var structured = getDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            Assert.True(structured.GetProperty("readiness").GetArrayLength() > 0);
            Assert.Equal(
                "AB12CDE",
                structured.GetProperty("caseOwned").GetProperty("registration").GetString());
        }
    }

    [Fact]
    public async Task CaseUpdateDetailsRequiresTheCasesScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();

        // pegasus_case_update_details sits under automation.cases, not
        // automation.assessment: a token scoped only for the assessment
        // tranche is refused before the case is even read.
        var assessmentOnlyToken = await RequestTokenAsync(client, "automation.assessment");
        using var response = await PostMcpAsync(
            client,
            assessmentOnlyToken,
            ToolCallPayload(
                9,
                "pegasus_case_update_details",
                new
                {
                    caseId = Guid.NewGuid(),
                    expectedVersion = 0,
                    editLeaseToken = new string('a', 64),
                    operationKey = "mcp:case-details-scope",
                    reason = "Automation scope probe."
                }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains(
            "automation.cases",
            document.RootElement.ToString(),
            StringComparison.Ordinal);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));
    }

    [Fact]
    public async Task CaseUpdateDetailsOverHttpMutatesUnderLeaseWithLoggingParityAndReopensCompleteness()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        Guid workRequestId;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var created = await scope.ServiceProvider
                .GetRequiredService<IAiWorkRequestStore>()
                .CreateAsync(
                    new(
                        caseId,
                        "fixture-reference",
                        0,
                        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                        "ingress-details-op",
                        "Confirm the contact details.",
                        TimeSpan.FromHours(24)),
                    CancellationToken.None);
            workRequestId = created.RequestId;
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        // The automation claims the same server-owned edit lease as staff.
        long caseVersion;
        string leaseToken;
        using (var leaseResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                10,
                "pegasus_case_edit_begin",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    operationKey = "mcp:ingress-details-lease-1"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, leaseResponse.StatusCode);
            using var leaseDocument = await ReadJsonRpcAsync(leaseResponse);
            var lease = leaseDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            caseVersion = lease.GetProperty("caseVersion").GetInt64();
            leaseToken = lease.GetProperty("leaseToken").GetString()!;
        }

        using (var updateResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                11,
                "pegasus_case_update_details",
                new
                {
                    caseId,
                    expectedVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:ingress-details-1",
                    reason = "Automation recorded the contact details.",
                    contactName = "Automation QA Contact",
                    contactEmailAddress = "automation-qa@example.test",
                    workRequestId = workRequestId.ToString("D")
                })))
        {
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            using var updateDocument = await ReadJsonRpcAsync(updateResponse);
            var result = updateDocument.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
            Assert.Equal("NotReady", structured.GetProperty("state").GetString());
            Assert.Equal(
                workRequestId.ToString("D"),
                structured.GetProperty("correlationId").GetString());
        }

        // Case-detail values save through the same Core path as a staff edit: they land
        // Confirmed and attributed to the automation immediately, with no unconfirmed mark —
        // unlike the assessment tranche's staff-review boundary, ordinary case-detail
        // editing carries no separate confirmation gate.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseDataFields
            WHERE CaseId = '{caseId:D}'
              AND FieldName = N'contact_name'
              AND ValueKind = N'confirmed'
              AND Value = N'Automation QA Contact'
              AND SourceKind = N'staff_correction'
              AND ConfirmedByActor = N'pegasus-automation'
            """));

        // The save re-opens completeness review exactly as a staff edit does.
        Assert.Equal(
            "NotReady",
            await factory.Database.ScalarAsync<string>(
                $"SELECT State FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT CAST(InstructionComplete AS INT) FROM Cases WHERE Id = '{caseId:D}'"));

        // Logging parity: the business save is recorded exactly like a staff save, and the
        // ingress attribution row correlates to the hand-off.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'case_data_saved'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_update_details'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'{workRequestId:D}'
            """));

        // A replayed operation key returns the original result rather than re-saving.
        using (var replayResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                12,
                "pegasus_case_update_details",
                new
                {
                    caseId,
                    expectedVersion = caseVersion,
                    editLeaseToken = leaseToken,
                    operationKey = "mcp:ingress-details-1",
                    reason = "Automation recorded the contact details.",
                    contactName = "Automation QA Contact",
                    contactEmailAddress = "automation-qa@example.test",
                    workRequestId = workRequestId.ToString("D")
                })))
        {
            Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
            using var replayDocument = await ReadJsonRpcAsync(replayResponse);
            var structured = replayDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseDataFields
            WHERE CaseId = '{caseId:D}' AND FieldName = N'contact_name'
            """));
    }

    /// <summary>
    /// KANMER-005, staff holds and the Automation Actor competes over real HTTP: begin, a write
    /// presenting the staff holder's own token, and end are each refused with the existing
    /// held-by-another-actor mapping; nothing moves; the staff holder then releases and the
    /// Automation Actor claims the free lease.
    /// </summary>
    [Fact]
    public async Task AStaffHeldLeaseRefusesAutomationBeginWriteAndEndOverHttp()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var staffLease = await ClaimAsStaffAsync(mcpFactory, caseId, staff);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        await AssertRefusedByAnotherHolderAsync(
            client,
            token,
            ToolCallPayload(
                31,
                "pegasus_case_edit_begin",
                new { caseId, expectedVersion = 0, operationKey = "mcp:kanmer-005-begin" }));
        await AssertRefusedByAnotherHolderAsync(
            client,
            token,
            ToolCallPayload(
                32,
                "pegasus_assessment_update",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    editLeaseToken = staffLease.Token,
                    operationKey = "mcp:kanmer-005-write",
                    reason = "Automation attempted a write under a staff lease.",
                    fields = new Dictionary<string, string?> { ["vehicle.condition"] = "good" }
                }));
        await AssertRefusedByAnotherHolderAsync(
            client,
            token,
            ToolCallPayload(
                33,
                "pegasus_case_edit_end",
                new { caseId, operationKey = "mcp:kanmer-005-end", leaseToken = staffLease.Token }));

        Assert.Equal(0, await GetWorkflowVersionAsync(mcpFactory, caseId));
        Assert.Equal(
            "Staff",
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolderKind FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(
            staff.SubjectId,
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolder FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IReleaseCaseEditLease>().ExecuteAsync(
                new(caseId, staff, Guid.NewGuid().ToString("N"), staffLease.Token),
                CancellationToken.None);
        }

        var automationLease = await BeginEditAsync(client, token, caseId, 0, rpcId: 34);
        Assert.Equal(0, automationLease.CaseVersion);
        Assert.Equal(
            "Automation",
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolderKind FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
    }

    /// <summary>
    /// KANMER-005, the reported direction: the Automation Actor holds the lease over real HTTP,
    /// the staff claim through the same Core port the workspace posts to is refused, the
    /// workspace renders the case read-only with no claim control, and the holder still ends
    /// its own lease afterwards — after which staff claim normally.
    /// </summary>
    [Fact]
    public async Task AnAutomationHeldLeaseRefusesTheStaffClaimAndLeavesTheWorkspaceReadOnly()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            TimeProvider.System,
            useIntegrationTestAuthentication: true);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var automationLease = await BeginEditAsync(client, token, caseId, 0, rpcId: 41);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            ClaimAsStaffAsync(mcpFactory, caseId, staff));

        using var staffClient = mcpFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var page = await staffClient.GetAsync($"/Cases/{caseId:D}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Case locked - AI is editing", html, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=ClaimLease", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientId, html, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(
            "Automation",
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolderKind FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));

        using (var endResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                42,
                "pegasus_case_edit_end",
                new { caseId, operationKey = "mcp:kanmer-005-holder-ends", leaseToken = automationLease.LeaseToken })))
        {
            Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
            _ = await ReadStructuredContentAsync(endResponse);
        }

        var staffLease = await ClaimAsStaffAsync(mcpFactory, caseId, staff);
        Assert.Equal(staff.SubjectId, staffLease.Holder);
        Assert.Equal(0, await GetWorkflowVersionAsync(mcpFactory, caseId));
    }

    private static async Task<CaseEditLease> ClaimAsStaffAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        ActionActor staff)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAcquireCaseEditLease>().ExecuteAsync(
            new(caseId, 0, staff, Guid.NewGuid().ToString("N")),
            CancellationToken.None);
    }

    private static async Task AssertRefusedByAnotherHolderAsync(
        HttpClient client,
        string token,
        string payload)
    {
        using var response = await PostMcpAsync(client, token, payload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = await ReadJsonRpcAsync(response);
        Assert.Contains(
            "case edit authority is held by another actor",
            document.RootElement.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CaseUpdateDetailsRefusesAMissingEditLeaseWithFailedHistoryAndNoTokenDisclosed()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        // Same validation guard the assessment-update tool uses: an absent edit
        // lease token fails closed before any Core save is attempted.
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                13,
                "pegasus_case_update_details",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    editLeaseToken = string.Empty,
                    operationKey = "mcp:ingress-details-missing-lease",
                    reason = "Automation attempted an unleased save.",
                    contactName = "Should not be recorded"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            var body = document.RootElement.ToString();
            Assert.Contains("edit lease token is required", body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_update_details'
              AND Outcome = N'Failed'
            """));

        // Nothing was written to the confirmed case data, and the refusal never
        // had a token to disclose in the first place.
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseDataFields
            WHERE CaseId = '{caseId:D}' AND FieldName = N'contact_name' AND ValueKind = N'confirmed'
            """));
    }

    private sealed class CapturingEstimateImporter : IImportRawEstimate
    {
        public static readonly Guid EstimateId = Guid.Parse("80b99604-d8b3-4028-9bc4-b744f82c297f");
        public ImportRawEstimateRequest? Request { get; private set; }
        public int Calls { get; private set; }
        public EstimateImportResult Result { get; set; } = new(EstimateId);
        public Task<EstimateImportResult> ExecuteAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            Calls++;
            return Task.FromResult(Result);
        }
    }

    /// <summary>
    /// ENG-026 / FRD-10 § AI job and estimate tools: an AI-draft estimate
    /// must cite the Estimate job this client holds, always lands as a
    /// Draft with unconfirmed lines and never as Current, and is listed
    /// with Pegasus-computed totals.
    /// </summary>
    [Fact]
    public async Task EstimateSaveRequiresTheHeldEstimateJobAndLandsAsAnUnconfirmedAiDraft()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        Guid jobId;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IAiJobStore>();
            var job = await jobs.CreateAsync(
                new(
                    AiJobKind.Estimate, AiJobSubjectKind.Case, caseId, "fixture-reference",
                    "Draft an estimate.", 60, 12000m,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
                    "ingress-estimate-job", AiJobPolicy.DefaultExpiry),
                CancellationToken.None);
            jobId = job.JobId;
            await scope.ServiceProvider.GetRequiredService<IWorkAiJob>()
                .TakeAsync(new(jobId, job.Version, ActionActor.Automation(ClientId), "ingress-estimate-take"), CancellationToken.None);
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 10);
        var lines = new object[]
        {
            new { type = "new_part", description = "Door skin", price = 220.40, quantity = 1 },
            new { type = "repair", description = "Repair nearside door", workUnits = 2.5 },
            new { type = "paint_repair", description = "Paint door", paintWorkUnits = 1.5 }
        };

        // Without the job the save is refused before anything is written.
        using (var refused = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                11,
                "pegasus_estimate_save",
                new
                {
                    caseId,
                    expectedVersion = lease.CaseVersion,
                    editLeaseToken = lease.LeaseToken,
                    operationKey = "mcp:ingress-estimate-refused",
                    reason = "Automation drafted an estimate.",
                    aiJobId = Guid.NewGuid(),
                    name = "Claude draft",
                    lines
                })))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            using var document = await ReadJsonRpcAsync(refused);
            Assert.Contains("The cited AI job was not found.", document.RootElement.ToString(), StringComparison.Ordinal);
        }
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseRepairSpecifications WHERE CaseId = '{caseId:D}'"));

        Guid estimateId;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                12,
                "pegasus_estimate_save",
                new
                {
                    caseId,
                    expectedVersion = lease.CaseVersion,
                    editLeaseToken = lease.LeaseToken,
                    operationKey = "mcp:ingress-estimate-1",
                    reason = "Automation drafted an estimate.",
                    aiJobId = jobId,
                    name = "Claude draft",
                    labourRate = 40,
                    paintMaterials = 25,
                    vatPercent = 20,
                    lines
                })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(lease.CaseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
            var estimate = structured.GetProperty("estimate");
            estimateId = estimate.GetProperty("estimateId").GetGuid();
            Assert.Equal("Draft", estimate.GetProperty("state").GetString());
            Assert.Equal("AiDraft", estimate.GetProperty("sourceRoute").GetString());
            Assert.False(estimate.GetProperty("isCurrent").GetBoolean());
            Assert.Equal(jobId, estimate.GetProperty("aiJobId").GetGuid());
            Assert.All(
                estimate.GetProperty("lines").EnumerateArray(),
                line => Assert.False(line.GetProperty("isConfirmed").GetBoolean()));
            var totals = estimate.GetProperty("totals");
            Assert.Equal(220.40m, totals.GetProperty("parts").GetDecimal());
            Assert.Equal(100m, totals.GetProperty("panelLabour").GetDecimal());
            Assert.Equal(60m, totals.GetProperty("paintLabour").GetDecimal());
            Assert.Equal(25m, totals.GetProperty("materials").GetDecimal());
            Assert.Equal(0m, totals.GetProperty("specialist").GetDecimal());
            Assert.Equal(405.40m, totals.GetProperty("net").GetDecimal());
            Assert.Equal(20m, totals.GetProperty("vatPercent").GetDecimal());
            Assert.Equal(0m, totals.GetProperty("vat").GetDecimal());
            Assert.Equal(405.40m, totals.GetProperty("gross").GetDecimal());
            Assert.False(totals.TryGetProperty("labour", out _));
            Assert.False(totals.TryGetProperty("paint", out _));
            Assert.False(totals.TryGetProperty("other", out _));
            Assert.False(totals.TryGetProperty("subtotal", out _));
            Assert.False(totals.TryGetProperty("total", out _));
            Assert.Equal(40m, estimate.GetProperty("labourRate").GetDecimal());
            Assert.False(estimate.TryGetProperty("paintLabourRate", out _));
        }

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRepairSpecificationStore>();
            var saved = await store.GetVersionAsync(caseId, estimateId, CancellationToken.None);
            Assert.NotNull(saved);
            Assert.Equal(RepairerVatStatus.Unknown, saved.Details.VatPolicy.RepairerStatus);
            Assert.True(saved.Details.VatPolicy.BlocksAcceptance);
            var refusal = Assert.Throws<InvalidOperationException>(() =>
                EstimatePolicy.ValidateSetCurrent(
                    saved, ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer])));
            Assert.Contains("VAT status", refusal.Message, StringComparison.Ordinal);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseRepairSpecifications
            WHERE Id = '{estimateId:D}' AND CaseId = '{caseId:D}' AND State = N'Draft'
              AND SourceRoute = N'AiDraft' AND IsCurrent = 0 AND AiJobId = '{jobId:D}'
              AND Name = N'Claude draft' AND VatPercent = 20
            """));
        Assert.Equal(3, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseEstimateLines
            WHERE RepairSpecificationId = '{estimateId:D}' AND RecordedByKind = N'Automation' AND ConfirmedBy IS NULL
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation' AND EventKind = N'pegasus_estimate_save'
              AND Outcome = N'Succeeded' AND CorrelationId = N'{jobId:D}'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation' AND EventKind = N'estimate_created' AND Outcome = N'Succeeded'
            """));

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(13, "pegasus_estimate_list", new { caseId, limit = 1 })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(1, structured.GetProperty("limit").GetInt32());
            Assert.True(!structured.TryGetProperty("nextCursor", out var nextCursor)
                || nextCursor.ValueKind == JsonValueKind.Null);
            var listed = Assert.Single(structured.GetProperty("estimates").EnumerateArray());
            Assert.Equal(estimateId, listed.GetProperty("estimateId").GetGuid());
            Assert.False(listed.TryGetProperty("lines", out _));
            Assert.False(listed.TryGetProperty("totals", out _));
        }
    }
}
