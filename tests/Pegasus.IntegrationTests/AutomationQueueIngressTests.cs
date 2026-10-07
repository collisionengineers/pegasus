using Pegasus.Core.Cases;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Queue and intake caller evidence for the Automation Actor's casework
/// parity (ADR-0064): reading and accepting a received item, creating a Case
/// directly and dismissing a Work Centre record, through the gated /mcp host
/// and the same Core commands as staff. Unidentified reopen and close are in
/// <see cref="AutomationIntakeSubmitIngressTests"/>, Triage assignment and
/// notes in <see cref="AutomationIntakeParityIngressTests"/>, mail in
/// <see cref="AutomationMailIngressTests"/> and AI jobs in
/// <see cref="AutomationAiJobIngressTests"/>.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationQueueIngressTests
{
    [Fact]
    public async Task TheActorReadsAndAcceptsAReceivedItemOnlyOnceItsAddressIsSettled()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        _ = await SeedAcceptedCaseAsync(mcpFactory);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            mcpFactory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, "automation.intake");

        long version;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_intake_get", new { receiptId = receipt.Id })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.Equal("case_created", detail.GetProperty("decision").GetString());
            Assert.Equal("Inspection", detail.GetProperty("classifiedCaseType").GetString());
            Assert.Contains(
                detail.GetProperty("missingIdentityFields").EnumerateArray(),
                field => field.GetString() == "Claimant name");
            version = detail.GetProperty("version").GetInt64();
        }

        // A physical-address Principal needs its address settled first
        // (resolve_inspection_address): accept refuses and writes nothing.
        await factory.Database.ExecuteAsync(
            $"UPDATE Principals SET InspectionMode = N'{PrincipalInspectionModePolicy.PhysicalAddressCode}' WHERE Code = N'{QdosPrincipal.Code}'");
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "accept",
            expectedReceiptVersion = version,
            claimantName = "Automation Claimant",
            claimNumber = "AUTO-1",
            operationKey = "mcp:intake-accept-address"
        })))
        {
            Assert.Contains("inspection address", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        // allocate takes no draft field: it retries the recorded command.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "allocate",
            expectedReceiptVersion = version,
            expectedAttemptId = Guid.NewGuid(),
            reason = "Retry.",
            claimantName = "Automation Claimant",
            operationKey = "mcp:intake-allocate-draft"
        })))
        {
            Assert.Contains("recorded command", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        // An image-based Principal settles the address by its own mode.
        await factory.Database.ExecuteAsync(
            $"UPDATE Principals SET InspectionMode = N'{PrincipalInspectionModePolicy.ImageBasedAssessmentCode}' WHERE Code = N'{QdosPrincipal.Code}'");

        // The identity-critical facts are refused by name while missing.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "accept",
            expectedReceiptVersion = version,
            operationKey = "mcp:intake-accept-missing"
        })))
        {
            Assert.Contains("Claimant name", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        Guid caseId;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "accept",
            expectedReceiptVersion = version,
            claimantName = "Automation Claimant",
            claimNumber = "AUTO-1",
            operationKey = "mcp:intake-accept"
        })))
        {
            var accepted = await ReadStructuredContentAsync(response);
            Assert.Equal("Succeeded", accepted.GetProperty("allocationStatus").GetString());
            caseId = accepted.GetProperty("caseId").GetGuid();
            Assert.False(string.IsNullOrWhiteSpace(accepted.GetProperty("caseReference").GetString()));
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_intake_get", new { receiptId = receipt.Id })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.Equal(caseId, detail.GetProperty("caseId").GetGuid());
            Assert.Equal("Automation Claimant", detail.GetProperty("draft").GetProperty("claimantName").GetString());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseHistory
            WHERE CaseId = '{caseId:D}'
              AND EventType = N'case_accepted'
              AND Actor = N'{ClientId}'
            """));
    }

    /// <summary>
    /// ADR-0064: the Actor settles an inspection address as the Create case
    /// page does. Its settlement is its own, in the intake history and in the
    /// Case's provenance, and never reads as a member of staff's.
    /// </summary>
    [Fact]
    public async Task TheActorSuppliesAnInspectionAddressAndTheCaseRecordsItAsTheAutomationActors()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        _ = await SeedAcceptedCaseAsync(mcpFactory);
        // A physical-address Principal: its address needs settling.
        await factory.Database.ExecuteAsync(
            $"UPDATE Principals SET InspectionMode = N'{PrincipalInspectionModePolicy.PhysicalAddressCode}' WHERE Code = N'{QdosPrincipal.Code}'");
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            mcpFactory.Services,
            CaseType.Inspection,
            QdosPrincipal.Code);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, "automation.intake");

        long version;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_intake_get", new { receiptId = receipt.Id })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.Equal("Unresolved", detail.GetProperty("inspectionAddressState").GetString());
            version = detail.GetProperty("version").GetInt64();
        }

        // Nothing was suggested, so the address is supplied: without one, or
        // with a fingerprint for a suggestion that does not exist, nothing is
        // written.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "resolve_inspection_address",
            expectedReceiptVersion = version,
            operationKey = "mcp:address-none"
        })))
        {
            Assert.Contains("give inspectionAddress", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "resolve_inspection_address",
            expectedReceiptVersion = version,
            inspectionAddress = "1 Example Street, Exampleton EX1 1EX",
            addressSuggestionFingerprint = new string('a', 64),
            operationKey = "mcp:address-fingerprint"
        })))
        {
            Assert.Contains("no addressSuggestionFingerprint", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "accept",
            expectedReceiptVersion = version,
            inspectionAddress = "1 Example Street, Exampleton EX1 1EX",
            operationKey = "mcp:address-on-accept"
        })))
        {
            Assert.Contains("belong to resolve_inspection_address", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        long settledVersion;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "resolve_inspection_address",
            expectedReceiptVersion = version,
            inspectionAddress = "1 Example Street, Exampleton EX1 1EX",
            operationKey = "mcp:address-supply"
        })))
        {
            var settled = await ReadStructuredContentAsync(response);
            Assert.Equal("Supplied", settled.GetProperty("inspectionAddressState").GetString());
            Assert.Equal("1 Example Street, Exampleton EX1 1EX", settled.GetProperty("inspectionAddressResolvedValue").GetString());
            settledVersion = settled.GetProperty("receiptVersion").GetInt64();
            Assert.Equal(version + 1, settledVersion);
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_intake_get", new { receiptId = receipt.Id })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.Equal("Supplied", detail.GetProperty("inspectionAddressState").GetString());
            Assert.Equal("Automation", detail.GetProperty("inspectionAddressResolvedByKind").GetString());
            Assert.Equal(ClientId, detail.GetProperty("inspectionAddressResolvedBy").GetString());
        }
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'intake_receipt'
              AND AggregateId = '{receipt.Id:D}'
              AND EventKind = N'inspection_address_supplied'
              AND ActorKind = N'Automation'
              AND ActorSubjectId = N'{ClientId}'
              AND Reason LIKE N'%supplied by Automation%'
            """));

        Guid caseId;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(7, "pegasus_intake_action", new
        {
            receiptId = receipt.Id,
            action = "accept",
            expectedReceiptVersion = settledVersion,
            claimantName = "Automation Claimant",
            claimNumber = "AUTO-ADDR-1",
            operationKey = "mcp:address-accept"
        })))
        {
            var accepted = await ReadStructuredContentAsync(response);
            Assert.Equal("Succeeded", accepted.GetProperty("allocationStatus").GetString());
            caseId = accepted.GetProperty("caseId").GetGuid();
        }

        // The Case's address is the Actor's, labelled as such, never staff's.
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseDataFields AS f
            INNER JOIN CaseWorks AS w ON w.Id = f.WorkId
            WHERE w.CaseId = '{caseId:D}'
              AND f.FieldName = N'inspection_address'
              AND f.ValueKind = N'confirmed'
              AND f.Value = N'1 Example Street, Exampleton EX1 1EX'
              AND f.ConfirmedByActor = N'{ClientId}'
              AND f.SourceLabel = N'Automation-supplied inspection address'
            """));
        Assert.Equal(0, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseDataFields AS f
            INNER JOIN CaseWorks AS w ON w.Id = f.WorkId
            WHERE w.CaseId = '{caseId:D}' AND f.SourceLabel LIKE N'staff-%inspection%'
            """));
    }

    [Fact]
    public async Task TheActorCreatesACaseDirectlyAndATriageCaseTakesOnlyItsRegistration()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        _ = await SeedAcceptedCaseAsync(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, "automation.intake");

        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_case_create", new
        {
            principalCode = QdosPrincipal.Code,
            caseType = "Audit",
            vehicleRegistration = "AB12CDE",
            operationKey = "mcp:case-create-audit"
        })))
        {
            Assert.Contains("Inspection, InspectionAndAudit or Triage", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_case_create", new
        {
            principalCode = QdosPrincipal.Code,
            caseType = "Triage",
            vehicleRegistration = "AB12CDE",
            claimantName = "Not for Triage",
            operationKey = "mcp:case-create-triage-claimant"
        })))
        {
            Assert.Contains("only principalCode and vehicleRegistration", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        Guid caseId;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_case_create", new
        {
            principalCode = QdosPrincipal.Code,
            caseType = "Triage",
            vehicleRegistration = "AB12CDE",
            operationKey = "mcp:case-create-triage"
        })))
        {
            var created = await ReadStructuredContentAsync(response);
            caseId = created.GetProperty("caseId").GetGuid();
            Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("reference").GetString()));
        }

        // A replay of the same key and facts returns the same Case.
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_case_create", new
        {
            principalCode = QdosPrincipal.Code,
            caseType = "Triage",
            vehicleRegistration = "AB12CDE",
            operationKey = "mcp:case-create-triage"
        })))
        {
            Assert.Equal(caseId, (await ReadStructuredContentAsync(response)).GetProperty("caseId").GetGuid());
        }

        // Each call is its own history line: the creation and its replay.
        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'automation_mcp'
              AND EventKind = N'pegasus_case_create'
              AND ActorKind = N'Automation'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'mcp:case-create-triage'
            """));
    }

    [Fact]
    public async Task WorkCentreDismissNeedsTheCasesScopeAndHidesTheRecordInTheActorsName()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var recordId = Guid.NewGuid();

        var intakeOnly = await RequestTokenAsync(client, "automation.intake");
        using (var denied = await PostMcpAsync(client, intakeOnly, ToolCallPayload(1, "pegasus_work_centre_dismiss", new
        {
            recordId,
            operationKey = "mcp:dismiss-scope"
        })))
        {
            Assert.Contains("automation.cases", await ReadErrorTextAsync(denied), StringComparison.Ordinal);
        }

        var token = await RequestTokenAsync(client, "automation.cases");
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_work_centre_dismiss", new
        {
            recordId,
            operationKey = "mcp:dismiss-1"
        })))
        {
            Assert.Equal(recordId, (await ReadStructuredContentAsync(response)).GetProperty("recordId").GetGuid());
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM WorkCentreDismissals
            WHERE RecordId = '{recordId:D}' AND DismissedBySubjectId = N'{ClientId}'
            """));
    }

    [Fact]
    public async Task IntakeActsNeedTheIntakeScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        using var client = mcpFactory.CreateClient();
        var casesOnly = await RequestTokenAsync(client, "automation.cases");

        var payloads = new[]
        {
            ToolCallPayload(1, "pegasus_intake_get", new { receiptId = Guid.NewGuid() }),
            ToolCallPayload(2, "pegasus_intake_action", new
            {
                receiptId = Guid.NewGuid(),
                action = "accept",
                expectedReceiptVersion = 0,
                operationKey = "mcp:intake-scope"
            }),
            ToolCallPayload(3, "pegasus_case_create", new
            {
                principalCode = QdosPrincipal.Code,
                caseType = "Triage",
                vehicleRegistration = "AB12CDE",
                operationKey = "mcp:case-create-scope"
            })
        };
        foreach (var payload in payloads)
        {
            using var response = await PostMcpAsync(client, casesOnly, payload);
            Assert.Contains("automation.intake", await ReadErrorTextAsync(response), StringComparison.Ordinal);
        }
    }
}
