using Pegasus.Core.Cases;
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
    public async Task CanonicalEstimateImportThroughMcpPersistsSourceBackedRowsAndRejectsForeignOrStaleAuthority()
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
        // Review is assessment-writable since the 17 September ruling; the
        // read-only refusal is exercised from Held, then the Case returns to Review.
        await SetWorkflowStateAsync(mcpFactory.Services, caseId, CaseLifecycleState.Held);
        using (var beforeHandoff = await PostMcpAsync(client, token, ToolCallPayload(86, "pegasus_estimate_import",
            Arguments(0, lease.LeaseToken, occurrenceId, hash, "mcp:before-handoff"))))
        {
            using var refusal = await ReadJsonRpcAsync(beforeHandoff);
            Assert.Contains("read-only", refusal.RootElement.ToString(), StringComparison.Ordinal);
        }
        await SetWorkflowStateAsync(mcpFactory.Services, caseId, CaseLifecycleState.Review);
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
        // The hand-off kept the Automation lease, so its token carries on at the new version.
        lease = (1, lease.LeaseToken);
        // Under a proven lease only a version the Case has not reached is
        // refused; an older one is system work having moved the Case.
        foreach (var arguments in new[]
        {
            Arguments(2, lease.LeaseToken, occurrenceId, hash, "mcp:future-version-import"),
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
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(85, "pegasus_estimate_import",
            Arguments(2, lease.LeaseToken, occurrenceId, hash, "mcp:canonical-import-replay"))))
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
            Assert.Equal(ActorKind.Automation, line.RecordedByKind);
            Assert.Equal(versionId, line.SourceDocumentVersionId);
            Assert.Equal(hash, line.SourceDocumentSha256);
        });
        Assert.Single(await store.ListEstimatesAsync(caseId, CaseWorkSelector.Current, default));
        await store.RequireImportAuthorityAsync(new(ActionActor.Automation(imported.CreatedBy), caseId, 2, lease.LeaseToken,
            occurrenceId, versionId, hash, "mcp:replay-authority", "Glass's 1"), default);
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE EventKind = N'estimate_created' AND ActorKind = N'Automation'"));

        // The Automation actor edits an imported estimate too (operator, 7 October 2026): an
        // edit naming every line's lineId keeps the estimate's route and each line's source
        // evidence.
        object[] keptLines;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(88, "pegasus_estimate_get",
            new { caseId, estimateId = importedId })))
        {
            keptLines = (await ReadStructuredContentAsync(response))
                .GetProperty("estimate")
                .GetProperty("lines")
                .EnumerateArray()
                .Select(line => (object)line.EnumerateObject()
                    .Where(property => property.Name is not ("position" or "recordedByKind" or "amendedBy"))
                    .ToDictionary(property => property.Name, property => property.Value.Clone()))
                .ToArray();
        }
        Assert.Equal(14, keptLines.Length);
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(89, "pegasus_estimate_save", new
        {
            caseId,
            expectedVersion = 2,
            editLeaseToken = lease.LeaseToken,
            operationKey = "mcp:edit-imported-estimate",
            reason = "Automation reviewed the imported estimate.",
            estimateId = importedId,
            name = "Glass's 1",
            lines = keptLines
        })))
        {
            _ = await ReadStructuredContentAsync(response);
        }
        var edited = Assert.IsType<RepairSpecificationVersion>(await store.GetVersionAsync(caseId, importedId, default));
        Assert.Equal(imported.Source.Route, edited.Source.Route);
        Assert.Equal(14, edited.Lines.Count);
        Assert.All(edited.Lines, line =>
        {
            Assert.Equal(versionId, line.SourceDocumentVersionId);
            Assert.Equal(hash, line.SourceDocumentSha256);
        });
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
            $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND RecordedBy <> N'{PrincipalDefaultFeePolicy.RecorderId}'"));

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
        Assert.Equal(HttpStatusCode.OK, estimateResponse.StatusCode);
        // The generic command takes fields only: estimate lines change through
        // the named estimate commands, so nothing is written here.
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseEstimateLines WHERE WorkId = '{caseId:D}'"));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseRepairSpecifications WHERE WorkId = '{caseId:D}'"));

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
        Assert.Contains("no staff editor on the Case", rateDocument.RootElement.ToString(), StringComparison.Ordinal);

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
        Assert.Contains("no staff editor on the Case", signatoryDocument.RootElement.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssessmentUpdateWritesWhatStaffRecordFindingsIncluded()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 50);

        // An automation value is one staff can change or clear on its Case
        // section, so a fact the vehicle lookup records, a case-owned fact and
        // the retired paths (the statement of truth is the report contract's
        // wording) are each refused, naming the field. A path no section
        // edits, the Engineer signature, is refused in the test above.
        var refusals = new (string Path, string Value, string Refusal)[]
        {
            (AssessmentVocabulary.VehicleFuel, "Petrol", "filled by the DVLA/DVSA vehicle lookup"),
            ("statement_of_truth", "I believe the facts stated are true.", "not part of the assessment vocabulary"),
            ("incident.assessed", "2031-05-06", "case-detail edit path"),
            ("costs.repairer_vat_registered", "true", "not part of the assessment vocabulary")
        };
        var rpcId = 51;
        foreach (var (path, value, refusal) in refusals)
        {
            using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId++,
                "pegasus_assessment_update", new
                {
                    caseId,
                    expectedVersion = lease.CaseVersion,
                    editLeaseToken = lease.LeaseToken,
                    operationKey = $"mcp:refused-{path}",
                    reason = "Attempt a write staff cannot change on the Case.",
                    fields = new Dictionary<string, string?> { [path] = value }
                }));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var document = await ReadJsonRpcAsync(response);
            Assert.Contains(refusal, document.RootElement.ToString(), StringComparison.Ordinal);
            Assert.Equal(0, await factory.Database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND RecordedBy <> N'{PrincipalDefaultFeePolicy.RecorderId}'"));
        }

        // The Inspection section records the recovery charge, so automation
        // may write it, attributed to the Automation actor.
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:recovery-charge",
                reason = "Automation recorded the recovery charge.",
                fields = new Dictionary<string, string?> { [AssessmentVocabulary.CostRecoveryCharge] = "120" }
            })))
        {
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(lease.CaseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
        }
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseAssessmentFields WHERE WorkId = '{caseId:D}' AND RecordedBy <> N'{PrincipalDefaultFeePolicy.RecorderId}'"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseAssessmentFields
            WHERE WorkId = '{caseId:D}'
              AND FieldPath = N'{AssessmentVocabulary.CostRecoveryCharge}'
              AND Value = N'120.00'
              AND RecordedByKind = N'Automation'
            """));

        // Professional findings are casework the Automation actor records as
        // staff do (operator, 7 October 2026), attributed to it. The save
        // above kept its lease, so this write presents the same token.
        var findings = new Dictionary<string, string?>
        {
            [AssessmentVocabulary.Outcome] = "total_loss",
            [AssessmentVocabulary.LegalStatus] = "unroadworthy",
            [AssessmentVocabulary.UnroadworthyReason] = "Structural damage to the offside sill.",
            [AssessmentVocabulary.SalvageCategory] = "S",
            [AssessmentVocabulary.SalvageValue] = "450",
            [AssessmentVocabulary.ValueRetail] = "10000",
            [AssessmentVocabulary.ValueTrade] = "9000",
            [AssessmentVocabulary.ValueEngineer] = "9500"
        };
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId + 1,
            "pegasus_assessment_update", new
            {
                caseId,
                expectedVersion = lease.CaseVersion + 1,
                editLeaseToken = lease.LeaseToken,
                operationKey = "mcp:findings",
                reason = "Automation recorded the findings.",
                fields = findings
            })))
        {
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(lease.CaseVersion + 2, structured.GetProperty("caseVersion").GetInt64());
        }
        var findingPaths = string.Join(", ", findings.Keys.Select(path => $"N'{path}'"));
        Assert.Equal(findings.Count, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseAssessmentFields
            WHERE WorkId = '{caseId:D}'
              AND FieldPath IN ({findingPaths})
              AND RecordedByKind = N'Automation'
            """));
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
    public async Task AssessmentUpdateOverHttpMutatesUnderLeaseWithAttribution()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
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
                "pegasus_edit_begin",
                new
                {
                    recordKind = "Case",
                    recordId = caseId,
                    expectedVersion = 0,
                    operationKey = "mcp:ingress-lease-1"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, leaseResponse.StatusCode);
            using var leaseDocument = await ReadJsonRpcAsync(leaseResponse);
            var lease = leaseDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            caseVersion = lease.GetProperty("version").GetInt64();
            leaseToken = lease.GetProperty("editLeaseToken").GetString()!;
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
                        [AssessmentVocabulary.VehicleBody] = "Hatchback"
                    }
                })))
        {
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            using var updateDocument = await ReadJsonRpcAsync(updateResponse);
            var result = updateDocument.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
            Assert.Equal(
                "mcp:ingress-assessment-1",
                structured.GetProperty("correlationId").GetString());
            var fields = structured.GetProperty("fields").EnumerateArray().ToArray();
        }

        // Stored values carry the automation provenance. The agreed fee the
        // Case took from its Principal at creation is not this save's.
        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseAssessmentFields
            WHERE RecordedByKind = N'Automation'
              AND RecordedBy <> N'{PrincipalDefaultFeePolicy.RecorderId}'
            """));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM CaseEstimateLines WHERE RecordedByKind = N'Automation'"));

        // Logging parity: the business save is recorded exactly like a staff
        // save, and the ingress attribution row correlates to the operation key.
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
              AND CorrelationId = N'mcp:ingress-assessment-1'
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
                        [AssessmentVocabulary.VehicleBody] = "Hatchback"
                    }
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
            var caseOwned = structured.GetProperty("caseOwned");
            Assert.Equal("AB12CDE", caseOwned.GetProperty("registration").GetString());
            // Every Case has a received date; the report prints it as the
            // date instructions were received.
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", caseOwned.GetProperty("receivedDate").GetString());
        }

        // A null member is omitted from the structured content, so the
        // inspection date is recorded through the case-detail path before the
        // read-back can carry it. The assessment save kept the lease, so the
        // case-detail write presents the same token.
        var detailsLease = (CaseVersion: caseVersion + 1, LeaseToken: leaseToken);
        using (var detailsResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                6,
                "pegasus_case_update_details",
                new
                {
                    caseId,
                    expectedVersion = detailsLease.CaseVersion,
                    editLeaseToken = detailsLease.LeaseToken,
                    operationKey = "mcp:ingress-inspection-date",
                    reason = "Automation recorded the inspection date.",
                    inspectionDate = "2031-05-06"
                })))
        {
            _ = await ReadStructuredContentAsync(detailsResponse);
        }
        using (var getResponse = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(7, "pegasus_assessment_get", new { caseId })))
        {
            var structured = await ReadStructuredContentAsync(getResponse);
            Assert.Equal(
                "2031-05-06",
                structured.GetProperty("caseOwned").GetProperty("inspectionDate").GetString());
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
    public async Task CaseUpdateDetailsOverHttpSavesThroughTheCaseSaveWithLoggingParity()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
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
                "pegasus_edit_begin",
                new
                {
                    recordKind = "Case",
                    recordId = caseId,
                    expectedVersion = 0,
                    operationKey = "mcp:ingress-details-lease-1"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, leaseResponse.StatusCode);
            using var leaseDocument = await ReadJsonRpcAsync(leaseResponse);
            var lease = leaseDocument.RootElement
                .GetProperty("result")
                .GetProperty("structuredContent");
            caseVersion = lease.GetProperty("version").GetInt64();
            leaseToken = lease.GetProperty("editLeaseToken").GetString()!;
        }
        var stateBefore = await factory.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseWorkflows WHERE CaseId = '{caseId:D}'");

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
                    contactEmailAddress = "automation-qa@example.test"
                })))
        {
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
            using var updateDocument = await ReadJsonRpcAsync(updateResponse);
            var result = updateDocument.RootElement.GetProperty("result");
            Assert.False(result.TryGetProperty("isError", out var isError) && isError.GetBoolean());
            var structured = result.GetProperty("structuredContent");
            Assert.Equal(caseVersion + 1, structured.GetProperty("caseVersion").GetInt64());
            Assert.Equal(stateBefore, structured.GetProperty("state").GetString());
            Assert.Equal(
                "mcp:ingress-details-1",
                structured.GetProperty("correlationId").GetString());
        }

        // Case-detail values save through the staff Case save: they land as the
        // Case's confirmed case-data value, attributed to the automation.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseDataFields
            WHERE WorkId = '{caseId:D}'
              AND FieldName = N'contact_name'
              AND ValueKind = N'confirmed'
              AND Value = N'Automation QA Contact'
              AND SourceKind = N'staff_correction'
              AND ConfirmedByActor = N'pegasus-automation'
            """));

        // Like a staff Case save, it neither demotes the Case nor reopens
        // completeness: readiness is evaluated from what the save wrote.
        Assert.Equal(
            stateBefore,
            await factory.Database.ScalarAsync<string>(
                $"SELECT State FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT CAST(InstructionComplete AS INT) FROM Cases WHERE Id = '{caseId:D}'"));

        // Logging parity: the business save is recorded exactly like a staff save, and the
        // ingress attribution row correlates to the operation key.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'case_workspace_saved'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_update_details'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'mcp:ingress-details-1'
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
                    contactEmailAddress = "automation-qa@example.test"
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
            WHERE WorkId = '{caseId:D}' AND FieldName = N'contact_name'
            """));
    }

    /// <summary>
    /// Staff holds and the Automation Actor competes over real HTTP: begin, a write
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
                "pegasus_edit_begin",
                new { recordKind = "Case", recordId = caseId, expectedVersion = 0, operationKey = "mcp:staff-holds-begin" }));
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
                    operationKey = "mcp:staff-holds-write",
                    reason = "Automation attempted a write under a staff lease.",
                    fields = new Dictionary<string, string?> { ["vehicle.condition"] = "good" }
                }));
        await AssertRefusedByAnotherHolderAsync(
            client,
            token,
            ToolCallPayload(
                33,
                "pegasus_edit_end",
                new { recordKind = "Case", recordId = caseId, operationKey = "mcp:staff-holds-end", editLeaseToken = staffLease.Token }));

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
    /// The Automation Actor may do what a member of staff can (operator, 7 October 2026), Take
    /// over included: a staff-held Case lease refuses its plain claim and passes to it when it
    /// asks to take the lease over, the Case's history records the takeover, and the lease it
    /// took writes.
    /// </summary>
    [Fact]
    public async Task AutomationTakesOverAStaffHeldLeaseOnlyWhenItAsks()
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
                35,
                "pegasus_edit_begin",
                new { recordKind = "Case", recordId = caseId, expectedVersion = 0, operationKey = "mcp:takeover-plain-claim" }));
        Assert.Equal(
            "Staff",
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolderKind FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));

        string automationToken;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                36,
                "pegasus_edit_begin",
                new { recordKind = "Case", recordId = caseId, expectedVersion = 0, operationKey = "mcp:takeover-claim", takeOver = true })))
        {
            var lease = await ReadStructuredContentAsync(response);
            Assert.Equal(0, lease.GetProperty("version").GetInt64());
            automationToken = lease.GetProperty("editLeaseToken").GetString()!;
        }
        Assert.NotEqual(staffLease.Token, automationToken);
        Assert.Equal(
            "Automation",
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolderKind FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseWorkflowEvents
            WHERE CaseId = '{caseId:D}'
              AND EventType = N'edit_lease_taken_over'
              AND ActorKind = N'Automation'
            """));

        using (var write = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                37,
                "pegasus_assessment_update",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    editLeaseToken = automationToken,
                    operationKey = "mcp:takeover-write",
                    reason = "Automation wrote under the lease it took over.",
                    fields = new Dictionary<string, string?> { ["vehicle.condition"] = "good" }
                })))
        {
            Assert.Equal(1, (await ReadStructuredContentAsync(write)).GetProperty("caseVersion").GetInt64());
        }
    }

    /// <summary>
    /// The reported direction: the Automation Actor holds the lease over real HTTP,
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
        // v26: the holder is named by the ribbon's colleague chip, with no claim control.
        Assert.Contains("data-edit-authority", html, StringComparison.Ordinal);
        Assert.Contains("AI is editing", html, StringComparison.Ordinal);
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
                "pegasus_edit_end",
                new { recordKind = "Case", recordId = caseId, operationKey = "mcp:automation-holder-ends", editLeaseToken = automationLease.LeaseToken })))
        {
            Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);
            _ = await ReadStructuredContentAsync(endResponse);
        }

        var staffLease = await ClaimAsStaffAsync(mcpFactory, caseId, staff);
        Assert.Equal(staff.SubjectId, staffLease.Holder);
        Assert.Equal(0, await GetWorkflowVersionAsync(mcpFactory, caseId));
    }

    private static async Task SetWorkflowStateAsync(IServiceProvider services, Guid caseId, CaseLifecycleState state)
    {
        await using var scope = services.CreateAsyncScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(row => row.CaseId == caseId);
        workflow.State = state.ToString();
        await db.SaveChangesAsync();
    }

    private static async Task<CaseEditLease> ClaimAsStaffAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        ActionActor staff,
        long expectedVersion = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAcquireCaseEditLease>().ExecuteAsync(
            new(caseId, expectedVersion, staff, Guid.NewGuid().ToString("N")),
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
            "case edit authority is already held",
            document.RootElement.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A write with no lease token holds the lease for its one command
    /// (operator, 1 October 2026): the same Core claim staff make, the save,
    /// and nothing left on the Case afterwards. While staff hold the lease the
    /// same call is refused with the held-by-another-actor mapping and writes
    /// nothing.
    /// </summary>
    [Fact]
    public async Task CaseUpdateDetailsWithoutALeaseTokenHoldsTheLeaseForOneCommand()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

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
                    operationKey = "mcp:ingress-details-implicit-lease",
                    reason = "Automation corrected the contact.",
                    contactName = "Recorded under a one-command lease"
                })))
        {
            var saved = await ReadStructuredContentAsync(response);
            Assert.Equal(caseId, saved.GetProperty("caseId").GetGuid());
            Assert.True(saved.GetProperty("caseVersion").GetInt64() > 0);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_case_update_details'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseDataFields
            WHERE WorkId = '{caseId:D}' AND FieldName = N'contact_name' AND ValueKind = N'confirmed'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{caseId:D}' AND EditLeaseHolderKind IS NULL"));

        // Staff now hold the lease: the one-command claim is refused like any other claim.
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var currentVersion = await GetWorkflowVersionAsync(mcpFactory, caseId);
        _ = await ClaimAsStaffAsync(mcpFactory, caseId, staff, currentVersion);
        await AssertRefusedByAnotherHolderAsync(
            client,
            token,
            ToolCallPayload(
                14,
                "pegasus_case_update_details",
                new
                {
                    caseId,
                    expectedVersion = currentVersion,
                    operationKey = "mcp:ingress-details-implicit-lease-held",
                    reason = "Automation attempted a save while staff edit.",
                    contactName = "Should not be recorded"
                }));
        Assert.Equal(currentVersion, await GetWorkflowVersionAsync(mcpFactory, caseId));
        Assert.Equal(
            "Staff",
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolderKind FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
    }

    /// <summary>
    /// pegasus_case_update_details is the staff Case save with the caller's changes over the
    /// Case's current values (operator, 7 October 2026): the one fact it names changes, and
    /// every fact it does not name — the repairer, the notes, the vehicle, the inspection and
    /// storage location a member of staff recorded — stays as it was. A call that names
    /// nothing is refused.
    /// </summary>
    [Fact]
    public async Task CaseUpdateDetailsChangesOnlyTheNamedFactAndKeepsEveryOther()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var staffLease = await ClaimAsStaffAsync(mcpFactory, caseId, staff);
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICaseWorkspaceStore>().SaveAsync(
                new SaveCaseWorkspaceRequest(caseId, 0, staff, "staff-recorded-facts", null, staffLease.Token)
                {
                    Overview = new(
                        "Jane Recorded",
                        null,
                        null,
                        "CLM-778",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        "1 Repair Road, Leeds",
                        null,
                        new CaseWorkspaceRepairer(null, null, "Leeds Bodyshop"),
                        PrincipalNotes: "Principal asked for photos first.",
                        ClientNotes: "Client is away until Friday."),
                    Inspection = new(
                        CaseReportAddressTreatment.PhysicalVehicleLocation,
                        "5 Yard Lane, Leeds",
                        null,
                        "Bay 4",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null),
                    Vehicle = new("AB12 CDE", "Ford", "Focus", null, null, "2019")
                },
                CancellationToken.None);
        }
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        using (var empty = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                15,
                "pegasus_case_update_details",
                new { caseId, expectedVersion = version, operationKey = "mcp:details-nothing-named" })))
        {
            Assert.Contains(
                "Name at least one value to change.",
                await ReadErrorTextAsync(empty),
                StringComparison.Ordinal);
        }
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                16,
                "pegasus_case_update_details",
                new { caseId, expectedVersion = version, operationKey = "mcp:details-one-fact", claimantName = "Janet Corrected" })))
        {
            Assert.Equal(version + 1, (await ReadStructuredContentAsync(response)).GetProperty("caseVersion").GetInt64());
        }

        await using var readScope = mcpFactory.Services.CreateAsyncScope();
        var data = await readScope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(data);
        Assert.Equal("Janet Corrected", data.Claimant.Name.Current?.Value);
        Assert.Equal("CLM-778", data.Claim.Number.Current?.Value);
        Assert.Equal("Leeds Bodyshop", data.Inspection.RepairerName?.Current?.Value);
        Assert.Equal("1 Repair Road, Leeds", data.Inspection.RepairerAddress?.Current?.Value);
        Assert.Equal("Principal asked for photos first.", data.Workspace?.PrincipalNotes);
        Assert.Equal("Client is away until Friday.", data.Workspace?.ClientNotes);
        Assert.Equal("5 Yard Lane, Leeds", data.Inspection.Address.Current?.Value);
        Assert.Equal("Bay 4", data.Inspection.StorageLocation?.Current?.Value);
        Assert.Equal("Ford", data.Vehicle.Make.Current?.Value);
        Assert.Equal("Focus", data.Vehicle.Model.Current?.Value);
        Assert.Equal("2019", data.Vehicle.Year.Current?.Value);
    }

    /// <summary>
    /// The Automation Actor adds a Case note as a member of staff does (operator, 7 October
    /// 2026): one operator note on the Case's timeline, attributed to it, written once for a
    /// replayed operation key and changing no Case value.
    /// </summary>
    [Fact]
    public async Task CaseNoteAddRecordsOneAutomationNoteOnTheCaseTimeline()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var note = new
        {
            caseId,
            operationKey = "mcp:note-1",
            note = "Chased the repairer for the estimate."
        };

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(17, "pegasus_case_note_add", note)))
        {
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal("mcp:note-1", structured.GetProperty("correlationId").GetString());
        }
        using (var replay = await PostMcpAsync(client, token, ToolCallPayload(18, "pegasus_case_note_add", note)))
        {
            _ = await ReadStructuredContentAsync(replay);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseWorkflowEvents
            WHERE CaseId = '{caseId:D}'
              AND EventType = N'{AddCaseNote.EventType}'
              AND ActorKind = N'Automation'
              AND Reason = N'Chased the repairer for the estimate.'
            """));
        Assert.Equal(0, await GetWorkflowVersionAsync(mcpFactory, caseId));
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
    /// FRD-10 § AI job and estimate tools: an estimate the Automation actor
    /// types in lands as an AI draft in Draft, never as Current, and is
    /// listed with Pegasus-computed totals. Citing the Estimate job it
    /// fulfils is optional (operator, 7 October 2026): a cited job must be an
    /// Estimate job on the case, but need not be taken by this client.
    /// </summary>
    [Fact]
    public async Task EstimateSaveLandsAsAnAiDraftWithOrWithoutACitedJob()
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
        }

        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var lease = await BeginEditAsync(client, token, caseId, 0, rpcId: 10);
        var lines = new object[]
        {
            new { type = "new_part", description = "Door skin", price = 220.40, quantity = 1 },
            new { type = "repair", description = "Repair nearside door", workUnits = 2.5 },
            new { type = "paint_repair", description = "Paint door", paintWorkUnits = 1.5, materials = 25 }
        };

        // A cited job that does not exist is refused before anything is written.
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
            $"SELECT COUNT(*) FROM CaseRepairSpecifications WHERE WorkId = '{caseId:D}'"));

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
                    name = "Claude draft",
                    labourRate = 40,
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
            Assert.True(!estimate.TryGetProperty("aiJobId", out var citedJob)
                || citedJob.ValueKind == JsonValueKind.Null);
            Assert.Equal("mcp:ingress-estimate-1", structured.GetProperty("correlationId").GetString());
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
            Assert.True(saved.Details.VatPolicy.TreatmentPending);
            // v28 P10: the unknown VAT status is no refusal, and the AI lines
            // are the Draft's own; Use repair spec is a staff member's choice.
            EstimatePolicy.ValidateSetCurrent(
                saved, ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]));
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseRepairSpecifications
            WHERE Id = '{estimateId:D}' AND WorkId = '{caseId:D}' AND State = N'Draft'
              AND SourceRoute = N'AiDraft' AND IsCurrent = 0 AND AiJobId IS NULL
              AND Name = N'Claude draft' AND VatPercent = 20
            """));
        Assert.Equal(3, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseEstimateLines
            WHERE RepairSpecificationId = '{estimateId:D}' AND RecordedByKind = N'Automation'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation' AND EventKind = N'pegasus_estimate_save'
              AND Outcome = N'Succeeded' AND CorrelationId = N'mcp:ingress-estimate-1'
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

        // An Estimate job on this case may be cited without being taken. The
        // save above kept its lease, so this one presents the same token.
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                14,
                "pegasus_estimate_save",
                new
                {
                    caseId,
                    expectedVersion = lease.CaseVersion + 1,
                    editLeaseToken = lease.LeaseToken,
                    operationKey = "mcp:ingress-estimate-cited",
                    reason = "Automation drafted the estimate the job asked for.",
                    aiJobId = jobId,
                    name = "Claude cited draft",
                    lines
                })))
        {
            var estimate = (await ReadStructuredContentAsync(response)).GetProperty("estimate");
            Assert.Equal(jobId, estimate.GetProperty("aiJobId").GetGuid());
            Assert.Equal("Draft", estimate.GetProperty("state").GetString());
            Assert.Equal("AiDraft", estimate.GetProperty("sourceRoute").GetString());
        }
    }

    /// <summary>
    /// A line sent To be confirmed with a price is a priced line, and the
    /// repairer's VAT status decides the VAT: a Registered repairer is
    /// charged on everything.
    /// </summary>
    [Fact]
    public async Task EstimateSavePricesAToBeConfirmedLineAndChargesVatForARegisteredRepairer()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        using var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                20,
                "pegasus_estimate_save",
                new
                {
                    caseId,
                    expectedVersion = 0,
                    operationKey = "mcp:estimate-registered",
                    reason = "Automation priced the estimate.",
                    name = "Registered draft",
                    vatPercent = 20,
                    repairerVatStatus = "Registered",
                    lines = new object[]
                    {
                        new { type = "new_part", description = "Bumper", price = 100, unpriced = true }
                    }
                }));
        var estimate = (await ReadStructuredContentAsync(response)).GetProperty("estimate");
        Assert.Equal("Registered", estimate.GetProperty("repairerVatStatus").GetString());
        Assert.False(estimate.GetProperty("vatTreatmentPending").GetBoolean());
        var line = Assert.Single(estimate.GetProperty("lines").EnumerateArray());
        Assert.False(line.GetProperty("unpriced").GetBoolean());
        Assert.Equal(100m, line.GetProperty("price").GetDecimal());
        var totals = estimate.GetProperty("totals");
        Assert.Equal(100m, totals.GetProperty("net").GetDecimal());
        Assert.Equal(20m, totals.GetProperty("vat").GetDecimal());
        Assert.Equal(120m, totals.GetProperty("gross").GetDecimal());
    }

    /// <summary>
    /// The Automation actor edits any live estimate in place, a member of
    /// staff's Current one included (operator, 7 October 2026). Naming the
    /// kept line's lineId keeps that line's evidence, the estimate keeps its
    /// route, and a header value the edit omits — the discounts here — keeps
    /// its recorded value.
    /// </summary>
    [Fact]
    public async Task EstimateSaveEditsAStaffEstimateKeepingItsRouteLineEvidenceAndDiscounts()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var staffLease = await ClaimAsStaffAsync(mcpFactory, caseId, staff);
        Guid estimateId;
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var created = await scope.ServiceProvider.GetRequiredService<ISaveEstimate>().ExecuteAsync(
                new(
                    caseId,
                    0,
                    staff,
                    "staff-typed-estimate",
                    "Staff typed the estimate.",
                    staffLease.Token,
                    null,
                    new EstimateDetails("Staff spec", 40m, null, 20m, new EstimateDiscounts(0.1m, 0m, 0m, 0m)),
                    [new EstimateLineInput("new_part", null, "Wing", null, 200m, false, "PN-1", null, "official", "Manufacturer price list.")],
                    new(RepairSpecificationSourceRoute.Manual, null, null, null)),
                CancellationToken.None);
            estimateId = created.SpecificationId;
        }
        var version = await GetWorkflowVersionAsync(mcpFactory, caseId);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        Guid lineId;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(21, "pegasus_estimate_get", new { caseId, estimateId })))
        {
            var estimate = (await ReadStructuredContentAsync(response)).GetProperty("estimate");
            lineId = Assert.Single(estimate.GetProperty("lines").EnumerateArray()).GetProperty("lineId").GetGuid();
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                22,
                "pegasus_estimate_save",
                new
                {
                    caseId,
                    expectedVersion = version,
                    operationKey = "mcp:edit-staff-estimate",
                    reason = "Automation corrected the wing price.",
                    estimateId,
                    name = "Staff spec",
                    lines = new object[] { new { lineId, type = "new_part", description = "Wing", price = 180 } }
                })))
        {
            var estimate = (await ReadStructuredContentAsync(response)).GetProperty("estimate");
            Assert.Equal(estimateId, estimate.GetProperty("estimateId").GetGuid());
            Assert.Equal("Manual", estimate.GetProperty("sourceRoute").GetString());
            Assert.Equal(0.1m, estimate.GetProperty("discounts").GetProperty("parts").GetDecimal());
            Assert.Equal(40m, estimate.GetProperty("labourRate").GetDecimal());
            var line = Assert.Single(estimate.GetProperty("lines").EnumerateArray());
            Assert.Equal(180m, line.GetProperty("price").GetDecimal());
            Assert.Equal("official", line.GetProperty("evidenceLabel").GetString());
            Assert.Equal("Manufacturer price list.", line.GetProperty("justification").GetString());
            Assert.Equal(ClientId, line.GetProperty("amendedBy").GetString());
        }
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseRepairSpecifications
            WHERE Id = '{estimateId:D}' AND SourceRoute = N'Manual'
            """));
    }

    /// <summary>
    /// One pegasus_edit_begin token carries every write of the work until
    /// pegasus_edit_end (operator, 8 October 2026): the QDOS26082 run's
    /// details, assessment and valuation writes under one lease, then its
    /// end. Get valuation records the connected source's card as the
    /// section's Save would, and ending a lease already gone answers
    /// released false.
    /// </summary>
    [Fact]
    public async Task AnEditBeginTokenCarriesEveryWriteUntilEditEnd()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        var provider = new ScriptedGuideValuationProvider(ValuationSource.Glasses, 12_500m, 10_250m);
        using var mcpFactory = WithAutomationMcp(factory).WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IGuideValuationProvider>(provider)));
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        var (version, leaseToken) = await BeginEditAsync(client, token, caseId, 0, 2);

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_case_update_details", new
        {
            caseId,
            expectedVersion = version,
            editLeaseToken = leaseToken,
            operationKey = "mcp:one-lease-details",
            reason = "Automation recorded the vehicle.",
            vehicleRegistration = "AB12CDE",
            vehicleMileage = 52_000,
            vehicleMileageUnit = "miles"
        })))
        {
            version = (await ReadStructuredContentAsync(response)).GetProperty("caseVersion").GetInt64();
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_assessment_update", new
        {
            caseId,
            expectedVersion = version,
            editLeaseToken = leaseToken,
            operationKey = "mcp:one-lease-assessment",
            reason = "Automation recorded the condition and damage.",
            fields = new Dictionary<string, string?>
            {
                ["vehicle.condition"] = "average",
                [AssessmentVocabulary.DamageImpacts] = "[{\"areas\":[\"left_front\"],\"severity\":\"light\",\"note\":\"\"}]"
            }
        })))
        {
            version = (await ReadStructuredContentAsync(response)).GetProperty("caseVersion").GetInt64();
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_valuation_get", new
        {
            caseId,
            expectedVersion = version,
            editLeaseToken = leaseToken,
            operationKey = "mcp:one-lease-valuation",
            source = "Glasses",
            guideMonth = "2031-05",
            reason = "Automation fetched the Glass's valuation."
        })))
        {
            var fetched = await ReadStructuredContentAsync(response);
            Assert.Equal("Recorded", fetched.GetProperty("outcome").GetString());
            Assert.Equal(12_500m, fetched.GetProperty("retailValue").GetDecimal());
            Assert.Equal(10_250m, fetched.GetProperty("tradeValue").GetDecimal());
            Assert.Equal("2031-05", fetched.GetProperty("guideMonth").GetString());
            var card = Assert.Single(fetched.GetProperty("valuations").EnumerateArray());
            Assert.Equal("Glasses", card.GetProperty("source").GetString());
            Assert.Equal(12_500m, card.GetProperty("retailValue").GetDecimal());
            Assert.True(fetched.GetProperty("caseVersion").GetInt64() > version);
        }
        var request = Assert.Single(provider.Requests);
        Assert.Equal(52_000L, request.Mileage);

        // Every write kept the Automation lease, so the begin token ends it.
        Assert.Equal(
            "Automation",
            await factory.Database.ScalarAsync<string>(
                $"SELECT EditLeaseHolderKind FROM CaseWorkflows WHERE CaseId = '{caseId:D}'"));
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_edit_end", new
        {
            recordKind = "Case",
            recordId = caseId,
            editLeaseToken = leaseToken,
            operationKey = "mcp:one-lease-end"
        })))
        {
            Assert.True((await ReadStructuredContentAsync(response)).GetProperty("released").GetBoolean());
        }
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{caseId:D}' AND EditLeaseHolderKind IS NULL"));

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(7, "pegasus_edit_end", new
        {
            recordKind = "Case",
            recordId = caseId,
            editLeaseToken = leaseToken,
            operationKey = "mcp:one-lease-end-again"
        })))
        {
            Assert.False((await ReadStructuredContentAsync(response)).GetProperty("released").GetBoolean());
        }
    }

    /// <summary>
    /// A source with no connected provider answers Unavailable with the card's
    /// own sentence and records nothing; the one-command lease is released.
    /// </summary>
    [Fact]
    public async Task ValuationGetRecordsNothingWhenTheSourceIsUnavailable()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var caseId = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_valuation_get", new
        {
            caseId,
            expectedVersion = 0,
            operationKey = "mcp:valuation-unavailable",
            source = "Brego"
        })))
        {
            var fetched = await ReadStructuredContentAsync(response);
            Assert.Equal("Unavailable", fetched.GetProperty("outcome").GetString());
            Assert.Equal(
                Pegasus.Web.Presentation.CaseWorkspaceLabels.Valuation.Unavailable(ValuationSource.Brego),
                fetched.GetProperty("message").GetString());
            Assert.Empty(fetched.GetProperty("valuations").EnumerateArray());
        }

        Assert.Equal(0, await GetWorkflowVersionAsync(mcpFactory, caseId));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflows WHERE CaseId = '{caseId:D}' AND EditLeaseHolderKind IS NULL"));
    }

    /// <summary>A connected guide source that answers fixed figures and records what it was asked.</summary>
    private sealed class ScriptedGuideValuationProvider(ValuationSource source, decimal retail, decimal trade)
        : IGuideValuationProvider
    {
        public List<GuideValuationRequest> Requests { get; } = [];

        public ValuationSource Source => source;

        public Task<GuideValuationQuote> GetAsync(GuideValuationRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new GuideValuationQuote(retail, trade, request.GuideMonth, request.Mileage));
        }
    }
}
