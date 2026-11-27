using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;
using static Pegasus.IntegrationTests.DesktopSignInTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The connector as the member of staff (R1300, ADR-0092): a token issued by
/// a Pegasus Desktop sign-in resolves to that person, every tool acts with
/// their rights, and what they do is recorded under their name, never the
/// Automation Actor's, with what is filed still marked AI (R1302). Each fact
/// is driven over real HTTP with a token from the real sign-in.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DesktopConnectorIngressTests
{
    private static readonly object[] EstimateLines =
    [
        new { type = "new_part", description = "Door skin", price = 220.40, quantity = 1 },
        new { type = "repair", description = "Repair nearside door", workUnits = 2.5 }
    ];

    /// <summary>
    /// The session names the person, the list holds every tool, and a write
    /// is the person's in Case history and in Pegasus Desktop's area of the
    /// Action logs.
    /// </summary>
    [Fact]
    public async Task ADesktopTokenActsAsTheMemberOfStaffAndItsWritesAreRecordedUnderTheirName()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await SeedAcceptedCaseAsync(host);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-engineer", StaffRole.Engineer);

        using (var session = await PostMcpAsync(desktop, token, ToolCallPayload(1, "pegasus_session_get", new { })))
        {
            var who = await ReadStructuredContentAsync(session);
            Assert.Equal(nameof(ActorKind.Staff), who.GetProperty("actorKind").GetString());
            Assert.Equal(staffId, who.GetProperty("staffId").GetGuid());
            Assert.Equal("desktop-engineer", who.GetProperty("userName").GetString());
            Assert.Equal(nameof(StaffRole.Engineer), who.GetProperty("role").GetString());
            Assert.Equal(
                AutomationMcp.Scopes,
                who.GetProperty("scopes").EnumerateArray().Select(scope => scope.GetString()!).ToArray());
        }

        using (var list = await PostMcpAsync(desktop, token, ToolsListPayload(2)))
        {
            using var document = await ReadJsonRpcAsync(list);
            Assert.Equal(
                AutomationMcpIngressTests.ExpectedTools,
                document.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
                    .Select(tool => tool.GetProperty("name").GetString())
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray());
        }

        using (var noted = await PostMcpAsync(desktop, token, ToolCallPayload(3, "pegasus_case_note_add", new
        {
            caseId,
            operationKey = "mcp:desktop-note-1",
            note = "Spoke to the repairer about the estimate."
        })))
        {
            _ = await ReadStructuredContentAsync(noted);
        }

        var database = await factory.DatabaseAsync;
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM CaseWorkflowEvents
            WHERE CaseId = '{caseId:D}'
              AND EventType = N'{AddCaseNote.EventType}'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{staffId:D}'
              AND Reason = N'Spoke to the repairer about the estimate.'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'{AutomationClientRegistry.DesktopAggregateType}'
              AND EventKind = N'pegasus_case_note_add'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'mcp:desktop-note-1'
            """));

        // The read of the connection itself is no history; nothing is the Automation Actor's.
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE EventKind = N'pegasus_session_get'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE ActorKind = N'Automation'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = N'{AutomationMcpAuditor.AutomationAggregateType}'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM SecurityEvents WHERE ReasonCode LIKE N'automation[_]%'"));
    }

    /// <summary>
    /// Under their name, but marked AI (R1302): an estimate typed in is the
    /// person's and an AI draft that waits for Use repair spec, a document
    /// added is the person's act and keeps the Automation source that draws
    /// its AI tag, and a staged upload belongs to the person who staged it.
    /// </summary>
    [Fact]
    public async Task AnEstimateIsTheirsAndAnAiDraftAndADocumentIsTheirsAndMarkedAi()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await SeedAcceptedCaseAsync(host);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-estimator", StaffRole.Engineer);
        var (_, colleagueToken) = await SignedInStaffAsync(host, desktop, "desktop-colleague", StaffRole.User);

        using (var saved = await PostMcpAsync(desktop, token, ToolCallPayload(1, "pegasus_estimate_save", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(host, caseId),
            operationKey = "mcp:desktop-estimate-1",
            reason = "Typed in from the repairer's estimate.",
            name = "Desktop estimate",
            labourRate = 40,
            vatPercent = 20,
            lines = EstimateLines
        })))
        {
            var estimate = (await ReadStructuredContentAsync(saved)).GetProperty("estimate");
            Assert.Equal(
                nameof(Pegasus.Core.Assessment.RepairSpecificationSourceRoute.AiDraft),
                estimate.GetProperty("sourceRoute").GetString());
            Assert.Equal(
                nameof(Pegasus.Core.Assessment.RepairSpecificationState.Draft),
                estimate.GetProperty("state").GetString());
            Assert.False(estimate.GetProperty("isCurrent").GetBoolean());
            Assert.Contains(staffId.ToString("D"), estimate.GetProperty("createdBy").GetString(), StringComparison.Ordinal);
        }

        // A document with no identity of its own takes one under the connector's prefix.
        var content = "desktop document fixture"u8.ToArray();
        using (var added = await PostMcpAsync(
            desktop,
            token,
            AddDocumentPayload(2, caseId, await GetWorkflowVersionAsync(host, caseId), "mcp:desktop-document-1", content)))
        {
            var structured = await ReadStructuredContentAsync(added);
            Assert.Equal(
                "automation:mcp:desktop-document-1",
                structured.GetProperty("sourceOccurrenceIdentity").GetString());
        }

        // The document is marked as the Automation Actor's are, and adding it is the person's act.
        var database = await factory.DatabaseAsync;
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM DocumentOccurrences
            WHERE CaseId = '{caseId:D}'
              AND Source = N'{nameof(DocumentSource.Automation)}'
              AND SourceOccurrenceIdentity = N'automation:mcp:desktop-document-1'
            """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM DocumentOccurrences
            WHERE CaseId = '{caseId:D}' AND Source = N'{nameof(DocumentSource.StaffUpload)}'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'{AutomationClientRegistry.DesktopAggregateType}'
              AND EventKind = N'pegasus_document_add'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM ActionHistory WHERE ActorKind = N'Automation'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE EventKind = N'estimate_created'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Succeeded'
            """));

        // An identity of the caller's own carries the same prefix, for either kind.
        using (var refused = await PostMcpAsync(desktop, token, ToolCallPayload(3, "pegasus_document_add", new
        {
            caseId,
            fileName = "note.txt",
            mediaType = "text/plain",
            contentBase64 = Convert.ToBase64String(content),
            semanticRole = "Other",
            expectedCaseVersion = await GetWorkflowVersionAsync(host, caseId),
            operationKey = "mcp:desktop-document-2",
            sourceOccurrenceIdentity = "staff:desktop-document-2"
        })))
        {
            // The answer is JSON, which writes the sentence's quotation marks as escapes.
            var refusal = await ReadErrorTextAsync(refused);
            Assert.Contains("The source occurrence identity must start with", refusal, StringComparison.Ordinal);
            Assert.Contains("automation:", refusal, StringComparison.Ordinal);
        }

        // A staged upload is the person's: a colleague cannot use it, the person can.
        var staged = "desktop staged document fixture"u8.ToArray();
        var uploadId = await StageUploadAsync(desktop, token, staged, "mcp:desktop-stage-1");
        using (var foreign = await PostMcpAsync(
            desktop,
            colleagueToken,
            AddStagedDocumentPayload(4, caseId, await GetWorkflowVersionAsync(host, caseId), "mcp:desktop-staged-foreign", uploadId)))
        {
            Assert.Contains(DocumentUploadRefusals.Unavailable, await ReadErrorTextAsync(foreign), StringComparison.Ordinal);
        }

        using (var own = await PostMcpAsync(
            desktop,
            token,
            AddStagedDocumentPayload(5, caseId, await GetWorkflowVersionAsync(host, caseId), "mcp:desktop-staged-own", uploadId)))
        {
            Assert.Equal(staged.Length, (await ReadStructuredContentAsync(own)).GetProperty("contentLength").GetInt64());
        }
    }

    /// <summary>
    /// An AI job is for an automated agent to take: a member of staff is
    /// refused the take, as in Pegasus itself, and may cancel the job.
    /// </summary>
    [Fact]
    public async Task AMemberOfStaffIsRefusedAnAiJobTakeAndMayCancelTheJob()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-jobs", StaffRole.User);

        Guid jobId;
        using (var created = await PostMcpAsync(desktop, token, ToolCallPayload(1, "pegasus_ai_job_create", new
        {
            kind = "UnidentifiedQueuePass",
            instruction = "Work the open Unidentified items.",
            operationKey = "mcp:desktop-job-1"
        })))
        {
            var job = await ReadStructuredContentAsync(created);
            Assert.Equal("Queued", job.GetProperty("state").GetString());
            Assert.Contains(staffId.ToString("D"), job.GetProperty("createdBy").GetString(), StringComparison.Ordinal);
            jobId = job.GetProperty("jobId").GetGuid();
        }

        using (var take = await PostMcpAsync(desktop, token, ToolCallPayload(2, "pegasus_ai_job_transition", new
        {
            jobId,
            expectedVersion = 0,
            action = "Take",
            operationKey = "mcp:desktop-job-take"
        })))
        {
            Assert.Contains(
                "Only the Automation Actor takes, releases, progresses, completes or fails an AI job.",
                await ReadErrorTextAsync(take),
                StringComparison.Ordinal);
        }

        using (var cancelled = await PostMcpAsync(desktop, token, ToolCallPayload(3, "pegasus_ai_job_transition", new
        {
            jobId,
            expectedVersion = 0,
            action = "Cancel",
            reason = "No longer needed.",
            operationKey = "mcp:desktop-job-cancel"
        })))
        {
            Assert.Equal("Cancelled", (await ReadStructuredContentAsync(cancelled)).GetProperty("state").GetString());
        }
    }

    /// <summary>
    /// Leases follow the staff rule: a call from Pegasus Desktop resumes the
    /// lease the person already holds, as a second browser tab does, a
    /// colleague is refused meanwhile, and the write ends the lease.
    /// </summary>
    [Fact]
    public async Task ADesktopCallResumesTheLeaseThePersonHoldsAndTheWriteEndsIt()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await SeedAcceptedCaseAsync(host);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-holder", StaffRole.User);
        var (_, colleagueToken) = await SignedInStaffAsync(host, desktop, "desktop-waiting", StaffRole.Engineer);
        var content = "desktop lease fixture"u8.ToArray();

        // The person is editing the Case in the browser.
        string browserToken;
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var lease = await scope.ServiceProvider.GetRequiredService<CaseEditLeases>().ClaimAsync(
                new(caseId, 0, ActionActor.Staff(staffId, [StaffRole.User]), "browser-edit-1"),
                CancellationToken.None);
            browserToken = lease.Token;
        }

        // Pegasus Desktop is handed the lease they hold, not a new one.
        var resumed = await BeginEditAsync(desktop, token, caseId, 0, rpcId: 1);
        Assert.Equal(browserToken, resumed.LeaseToken);

        using (var held = await PostMcpAsync(
            desktop,
            colleagueToken,
            AddDocumentPayload(2, caseId, resumed.CaseVersion, "mcp:desktop-lease-colleague-1", content)))
        {
            Assert.Contains("case edit authority is already held", await ReadErrorTextAsync(held), StringComparison.Ordinal);
        }

        // A write that presents no token runs under the lease the person holds...
        using (var written = await PostMcpAsync(
            desktop,
            token,
            AddDocumentPayload(3, caseId, resumed.CaseVersion, "mcp:desktop-lease-own-1", content)))
        {
            Assert.False((await ReadStructuredContentAsync(written)).GetProperty("isReplay").GetBoolean());
        }

        // ...and ends it, as every staff change does: the token carries no second write,
        using (var spent = await PostMcpAsync(desktop, token, ToolCallPayload(4, "pegasus_document_add", new
        {
            caseId,
            fileName = "second.txt",
            mediaType = "text/plain",
            contentBase64 = Convert.ToBase64String(content),
            semanticRole = "Other",
            expectedCaseVersion = await GetWorkflowVersionAsync(host, caseId),
            operationKey = "mcp:desktop-lease-own-2",
            editLeaseToken = browserToken
        })))
        {
            Assert.Contains("no active edit authority is held", await ReadErrorTextAsync(spent), StringComparison.Ordinal);
        }

        // and the Case is free for the colleague.
        using (var free = await PostMcpAsync(
            desktop,
            colleagueToken,
            AddDocumentPayload(
                5, caseId, await GetWorkflowVersionAsync(host, caseId), "mcp:desktop-lease-colleague-2", content)))
        {
            Assert.False((await ReadStructuredContentAsync(free)).GetProperty("isReplay").GetBoolean());
        }

        // Resuming one's own lease is nobody's takeover.
        Assert.Equal(0, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseWorkflowEvents WHERE CaseId = '{caseId:D}' AND EventType = N'edit_lease_taken_over'"));
    }

    /// <summary>
    /// Stopping the Automation client stops the Automation Actor and not a
    /// Pegasus Desktop session; disabling the account or forcing its sign-out
    /// ends one at the next call, on the tools and on the content routes.
    /// </summary>
    [Fact]
    public async Task StopAutomationDoesNotStopADesktopSessionAndADisabledOrSignedOutAccountIsRefusedAtTheNextCall()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        var caseId = await SeedAcceptedCaseAsync(host);
        using var desktop = CreateConnector(host);
        var (disabledId, disabledToken) = await SignedInStaffAsync(host, desktop, "desktop-to-disable", StaffRole.User);
        var (signedOutId, signedOutToken) = await SignedInStaffAsync(host, desktop, "desktop-to-sign-out", StaffRole.Engineer);
        var automationToken = await RequestTokenAsync(desktop, AllScopes);
        var content = "desktop content route fixture"u8.ToArray();
        string contentUrl;
        using (var added = await PostMcpAsync(
            desktop, disabledToken, AddDocumentPayload(9, caseId, 0, "mcp:desktop-content-1", content)))
        {
            var document = await ReadStructuredContentAsync(added);
            contentUrl = $"{AutomationMcp.DocumentContentPath}/{document.GetProperty("occurrenceId").GetGuid():D}"
                + $"/versions/{document.GetProperty("versionId").GetGuid():D}?caseId={caseId:D}";
        }

        await AdministerAsync<AutomationClientRegistry>(host, registry => registry.SetEnabledAsync(
            enabled: false,
            Administrator,
            "Integration-test kill switch",
            OperationKey(),
            CancellationToken.None));

        using (var stopped = await PostMcpAsync(
            desktop, automationToken, ToolCallPayload(1, "pegasus_intake_queue_list", new { limit = 10 })))
        {
            Assert.Contains("disabled", await ReadErrorTextAsync(stopped), StringComparison.OrdinalIgnoreCase);
        }

        foreach (var staffToken in new[] { disabledToken, signedOutToken })
        {
            using var running = await PostMcpAsync(
                desktop, staffToken, ToolCallPayload(2, "pegasus_intake_queue_list", new { limit = 10 }));
            Assert.Equal(10, (await ReadStructuredContentAsync(running)).GetProperty("limit").GetInt32());
        }

        // The content route resolves the same person and hands them the bytes.
        using (var found = await GetWithBearerAsync(desktop, disabledToken, contentUrl))
        {
            Assert.Equal(HttpStatusCode.OK, found.StatusCode);
            Assert.Equal(content, await found.Content.ReadAsByteArrayAsync());
        }

        var disabledVersion = await VersionAsync(host, disabledId);
        await AdministerAsync<IDisableStaffAccount>(host, service => service.ExecuteAsync(
            new(Administrator, disabledId, "Left the company", OperationKey(), disabledVersion),
            CancellationToken.None));
        var signedOutVersion = await VersionAsync(host, signedOutId);
        await AdministerAsync<IForceStaffLogout>(host, service => service.ExecuteAsync(
            new(Administrator, signedOutId, "Lost laptop", OperationKey(), signedOutVersion),
            CancellationToken.None));

        foreach (var staffToken in new[] { disabledToken, signedOutToken })
        {
            using var ended = await PostMcpAsync(
                desktop, staffToken, ToolCallPayload(3, "pegasus_intake_queue_list", new { limit = 10 }));
            Assert.Contains(
                "The automation authorization is not valid.",
                await ReadErrorTextAsync(ended),
                StringComparison.Ordinal);
        }

        using (var refused = await GetWithBearerAsync(desktop, disabledToken, contentUrl))
        {
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        // Each refusal is recorded once, under the person, and is not Automation activity.
        var database = await factory.DatabaseAsync;
        Assert.Equal(2, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'{AutomationMcp.DesktopAccessDenied}'
              AND Outcome = N'Denied'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{disabledId:D}'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'{AutomationMcp.DesktopAccessDenied}'
              AND Outcome = N'Denied'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{signedOutId:D}'
            """));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM SecurityEvents WHERE ReasonCode = N'automation_client_disabled'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode LIKE N'automation[_]%' AND ReasonCode <> N'automation_client_disabled'
            """));
    }

    /// <summary>
    /// Case lookup as Pegasus Desktop reads it (issue #2166): Cases by
    /// registration, newest first, each with its reference, Principal, state,
    /// claimant, received date and registration; then the vehicle's details
    /// and the guide valuations of one.
    /// </summary>
    [Fact]
    public async Task ARegistrationSearchListsNewestFirstWithTheVehicleAndGuideValuationsOfACase()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var host = WithDesktopSignIn(factory);
        _ = await SeedAcceptedCaseAsync(host);
        using var desktop = CreateConnector(host);
        var (staffId, token) = await SignedInStaffAsync(host, desktop, "desktop-lookup", StaffRole.User);

        var created = new List<(Guid CaseId, string Reference)>();
        foreach (var (claimant, claimNumber) in new[] { ("First Claimant", "LOOKUP-1"), ("Second Claimant", "LOOKUP-2") })
        {
            using var response = await PostMcpAsync(desktop, token, ToolCallPayload(1, "pegasus_case_create", new
            {
                principalCode = QdosPrincipal.Code,
                caseType = "Audit",
                vehicleRegistration = "DK12TOP",
                claimantName = claimant,
                claimNumber,
                vehicleMake = "Ford",
                vehicleModel = "Focus",
                vehicleMileage = 42000,
                vehicleMileageUnit = "miles",
                operationKey = $"mcp:desktop-create-{claimNumber}"
            }));
            var identity = await ReadStructuredContentAsync(response);
            created.Add((identity.GetProperty("caseId").GetGuid(), identity.GetProperty("reference").GetString()!));
        }

        // A Case entered from Pegasus Desktop is entered by the member of staff.
        Assert.Equal(2, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE AggregateType = N'{AutomationClientRegistry.DesktopAggregateType}'
              AND EventKind = N'pegasus_case_create'
              AND ActorKind = N'Staff'
              AND ActorSubjectId = N'{staffId:D}'
              AND Outcome = N'Succeeded'
            """));

        using (var search = await PostMcpAsync(
            desktop, token, ToolCallPayload(2, "pegasus_case_search", new { registration = "DK12TOP" })))
        {
            var items = (await ReadStructuredContentAsync(search)).GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(
                new[] { created[1].CaseId, created[0].CaseId },
                items.Select(item => item.GetProperty("caseId").GetGuid()).ToArray());
            var newest = items[0];
            Assert.Equal(created[1].Reference, newest.GetProperty("reference").GetString());
            Assert.False(string.IsNullOrWhiteSpace(newest.GetProperty("principal").GetString()));
            Assert.Equal("Second Claimant", newest.GetProperty("claimant").GetString());
            Assert.Equal("DK12TOP", Compact(newest.GetProperty("registration").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(newest.GetProperty("state").GetString()));
            Assert.True(
                newest.GetProperty("receivedAtUtc").GetDateTimeOffset()
                    >= items[1].GetProperty("receivedAtUtc").GetDateTimeOffset());
        }

        var caseId = created[1].CaseId;
        using (var assessment = await PostMcpAsync(
            desktop, token, ToolCallPayload(3, "pegasus_assessment_get", new { caseId })))
        {
            var vehicle = (await ReadStructuredContentAsync(assessment)).GetProperty("caseOwned");
            Assert.Equal("DK12TOP", Compact(vehicle.GetProperty("registration").GetString()));
            Assert.Equal("Ford", vehicle.GetProperty("make").GetString());
            Assert.Equal("Focus", vehicle.GetProperty("model").GetString());
            Assert.Equal(42000, vehicle.GetProperty("mileage").GetInt64());
            Assert.False(string.IsNullOrWhiteSpace(vehicle.GetProperty("receivedDate").GetString()));
        }

        var guideMonth = DateTimeOffset.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        using (var saved = await PostMcpAsync(desktop, token, ToolCallPayload(4, "pegasus_valuation_save", new
        {
            caseId,
            expectedVersion = await GetWorkflowVersionAsync(host, caseId),
            operationKey = "mcp:desktop-valuation-1",
            reason = "Recorded the guide value.",
            guideCards = new[]
            {
                new { source = "Glasses", retailValue = 9500, tradeValue = 8200, guideMonth }
            }
        })))
        {
            _ = await ReadStructuredContentAsync(saved);
        }

        using (var valuations = await PostMcpAsync(
            desktop, token, ToolCallPayload(5, "pegasus_valuation_list", new { caseId })))
        {
            var card = Assert.Single(
                (await ReadStructuredContentAsync(valuations)).GetProperty("valuations").EnumerateArray(),
                item => item.GetProperty("source").GetString() == "Glasses");
            Assert.Equal(9500m, card.GetProperty("retailValue").GetDecimal());
            Assert.Equal(8200m, card.GetProperty("tradeValue").GetDecimal());
            Assert.Equal(guideMonth, card.GetProperty("guideMonth").GetString());
            Assert.Contains(staffId.ToString("D"), card.GetProperty("recordedBy").GetString(), StringComparison.Ordinal);
        }
    }

    // A registration as typed, whatever spacing it is shown with.
    private static string? Compact(string? registration) =>
        registration?.Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string AddDocumentPayload(
        int id,
        Guid caseId,
        long expectedCaseVersion,
        string operationKey,
        byte[] content) =>
        ToolCallPayload(id, "pegasus_document_add", new
        {
            caseId,
            fileName = "note.txt",
            mediaType = "text/plain",
            contentBase64 = Convert.ToBase64String(content),
            semanticRole = "Other",
            expectedCaseVersion,
            operationKey
        });

    private static string AddStagedDocumentPayload(
        int id,
        Guid caseId,
        long expectedCaseVersion,
        string operationKey,
        Guid uploadId) =>
        ToolCallPayload(id, "pegasus_document_add", new
        {
            caseId,
            fileName = "staged.txt",
            mediaType = "text/plain",
            uploadId,
            semanticRole = "Other",
            expectedCaseVersion,
            operationKey
        });

    private static async Task<HttpResponseMessage> GetWithBearerAsync(HttpClient client, string token, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }
}
