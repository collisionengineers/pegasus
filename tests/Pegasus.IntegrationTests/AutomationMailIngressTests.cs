using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// MCP-05 caller evidence: the classified-email workspace through the gated
/// /mcp host — the same Core queries and correction command as the staff mail
/// pages, behind the automation.mail scope. Ingress gate/token/inventory
/// stays in <see cref="AutomationMcpIngressTests"/>; the staff pages
/// themselves are covered by <see cref="MailWorkspaceWebTests"/>.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationMailIngressTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private const string MailboxId = "instructions";
    private const string MailboxAddress = "instructions@collisionengineers.co.uk";

    [Fact]
    public async Task ListAndDetailMirrorTheStaffWorkspaceQueriesBehindTheMailScope()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var ids = await SeedRetainedAsync(mcpFactory, count: 2);
        await SeedClassificationAsync(mcpFactory, $"{MailboxId}-1");
        using var client = mcpFactory.CreateClient();

        // Out-of-scope first: a token without automation.mail cannot read the
        // workspace, and the denial writes an attributable security event.
        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");
        using (var denied = await PostMcpAsync(
            client,
            casesOnlyToken,
            ToolCallPayload(1, "pegasus_mail_list", new { })))
        {
            Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
            using var document = await ReadJsonRpcAsync(denied);
            Assert.Contains(
                "automation.mail",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM SecurityEvents
            WHERE ReasonCode = N'automation_scope_denied'
              AND Outcome = N'Denied'
              AND SubjectId = N'pegasus-automation'
            """));

        var token = await RequestTokenAsync(client, AllScopes);
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(2, "pegasus_mail_list", new { })))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var structured = await ReadStructuredContentAsync(response);
            var items = structured.GetProperty("items").EnumerateArray().ToArray();
            Assert.Equal(2, items.Length);
            Assert.Equal(ids[0], items[0].GetProperty("id").GetGuid());
            Assert.Equal(
                "Message 1 from instructions",
                items[0].GetProperty("subject").GetString());
            Assert.Equal(
                MailboxAddress,
                items[0].GetProperty("mailboxAddress").GetString());
            // Beside caseId and caseReference, a client can tell a Triage
            // Case from an instruction Case.
            Assert.False(items[0].GetProperty("isTriageCase").GetBoolean());
            var mailboxes = structured.GetProperty("mailboxes").EnumerateArray().ToArray();
            Assert.Contains(
                mailboxes,
                item => item.GetProperty("mailboxId").GetString() == TestMailboxId.From(MailboxId).ToString("D"));
            Assert.Equal(
                "current",
                structured.GetProperty("freshness").GetProperty("state").GetString());
        }

        Guid firstPageId;
        string continuation;
        using (var firstPage = await PostMcpAsync(client, token,
            ToolCallPayload(20, "pegasus_mail_list", new { pageSize = 1 })))
        {
            var structured = await ReadStructuredContentAsync(firstPage);
            firstPageId = Assert.Single(structured.GetProperty("items").EnumerateArray())
                .GetProperty("id").GetGuid();
            continuation = structured.GetProperty("continuation").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(continuation));
        }
        using (var secondPage = await PostMcpAsync(client, token,
            ToolCallPayload(21, "pegasus_mail_list", new { pageSize = 1, continuation })))
        {
            var structured = await ReadStructuredContentAsync(secondPage);
            var secondId = Assert.Single(structured.GetProperty("items").EnumerateArray())
                .GetProperty("id").GetGuid();
            Assert.NotEqual(firstPageId, secondId);
            Assert.True(!structured.TryGetProperty("continuation", out var terminal)
                || terminal.ValueKind == System.Text.Json.JsonValueKind.Null);
        }
        using (var foreignFilter = await PostMcpAsync(client, token,
            ToolCallPayload(22, "pegasus_mail_list", new { folder = "sent", continuation })))
        {
            using var document = await ReadJsonRpcAsync(foreignFilter);
            Assert.True(document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Contains("The cursor is invalid or no longer applies to this query.",
                document.RootElement.ToString(), StringComparison.Ordinal);
        }

        // A mailbox scope outside the supported range is a content-safe
        // refusal from the same Core validation the staff page relies on.
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(3, "pegasus_mail_list", new { folder = "junk" })))
        {
            using var document = await ReadJsonRpcAsync(response);
            Assert.Contains(
                "inbox, sent, or deleted",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(4, "pegasus_mail_get", new { messageId = ids[0] })))
        {
            var structured = await ReadStructuredContentAsync(response);
            var summary = structured.GetProperty("summary");
            Assert.Equal(ids[0], summary.GetProperty("id").GetGuid());
            Assert.Equal("inbox", structured.GetProperty("folder").GetString());
            Assert.Equal(
                "Please inspect the vehicle at the address supplied.",
                structured.GetProperty("bodyPlainText").GetString());
            var attachments = structured.GetProperty("attachments").EnumerateArray().ToArray();
            Assert.Single(attachments);
            Assert.Equal("estimate.pdf", attachments[0].GetProperty("fileName").GetString());
            var classification = structured.GetProperty("classification");
            Assert.Equal(1, classification.GetProperty("version").GetInt32());
            Assert.Equal(
                "Classified",
                classification.GetProperty("current").GetProperty("outcome").GetString());
            Assert.Equal(
                "new-instruction",
                classification.GetProperty("current").GetProperty("category").GetString());
            Assert.Empty(classification.GetProperty("history").EnumerateArray());
            Assert.Contains(
                classification.GetProperty("correctionOptions").EnumerateArray(),
                option => option.GetProperty("value").GetString() == "new-instruction");
        }

        // An unknown message is a content-safe not-found, recorded as failed.
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(5, "pegasus_mail_get", new { messageId = Guid.NewGuid() })))
        {
            using var document = await ReadJsonRpcAsync(response);
            Assert.Contains(
                "not found",
                document.RootElement.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(3, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_list'
              AND Outcome = N'Succeeded'
              AND ActorSubjectId = N'pegasus-automation'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_get'
              AND Outcome = N'Succeeded'
            """));
        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_get'
              AND Outcome = N'Failed'
            """));
    }

    [Fact]
    public async Task ClassificationCorrectionCarriesTheCaseType()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var ids = await SeedRetainedAsync(mcpFactory, count: 2);
        await SeedClassificationAsync(mcpFactory, $"{MailboxId}-1");
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);

        // The offered options are every category and Unclassified.
        using (var read = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(20, "pegasus_mail_get", new { messageId = ids[0] })))
        {
            var structured = await ReadStructuredContentAsync(read);
            var options = structured.GetProperty("classification").GetProperty("correctionOptions")
                .EnumerateArray()
                .Select(option => option.GetProperty("value").GetString())
                .ToArray();
            Assert.Equal(
                MailCategories.All.Select(MailCategories.Code).Append("unclassified").ToArray(),
                options);
        }

        // A New instruction correction carries the case type the Worker's
        // allocation reads.
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                21,
                "pegasus_mail_correct_classification",
                new
                {
                    messageId = ids[0],
                    expectedClassificationVersion = 1,
                    classificationKey = "new-instruction",
                    workType = "Audit",
                    reason = "The attached instruction is an Audit.",
                    operationKey = "mcp:mail-correct-work-type"
                })))
        {
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(2, structured.GetProperty("version").GetInt32());
            Assert.Equal("Audit", structured.GetProperty("current").GetProperty("caseType").GetString());
        }

        // A New instruction without its case type is refused and writes nothing.
        using (var noWorkType = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                23,
                "pegasus_mail_correct_classification",
                new
                {
                    messageId = ids[0],
                    expectedClassificationVersion = 2,
                    classificationKey = "new-instruction",
                    reason = "Missing case type.",
                    operationKey = "mcp:mail-correct-no-work-type"
                })))
        {
            using var document = await ReadJsonRpcAsync(noWorkType);
            Assert.Contains("requires a case type", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        using (var final = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(24, "pegasus_mail_get", new { messageId = ids[0] })))
        {
            var structured = await ReadStructuredContentAsync(final);
            Assert.Equal(2, structured.GetProperty("classification").GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task ClassificationCorrectionIsVersionedReplaySafeAndAttributed()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var ids = await SeedRetainedAsync(mcpFactory, count: 2);
        await SeedClassificationAsync(mcpFactory, $"{MailboxId}-1");
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, AllScopes);
        const string operationKey = "mcp:mail-correct-1";

        // The staff-equivalent correction: same Core command, permanent
        // history, next version.
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                11,
                "pegasus_mail_correct_classification",
                new
                {
                    messageId = ids[0],
                    expectedClassificationVersion = 1,
                    classificationKey = "query",
                    reason = "The message is a query, not a new instruction.",
                    operationKey
                })))
        {
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(2, structured.GetProperty("version").GetInt32());
            Assert.Equal(
                "automation:pegasus-automation",
                structured.GetProperty("currentActor").GetString());
            Assert.Equal(
                "query",
                structured.GetProperty("current").GetProperty("category").GetString());
            var history = structured.GetProperty("history").EnumerateArray().ToArray();
            Assert.Single(history);
            Assert.Equal(
                "new-instruction",
                history[0].GetProperty("before").GetProperty("category").GetString());
        }

        // A stale expected version is refused with the reload guidance, and
        // nothing further is appended.
        using (var stale = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                12,
                "pegasus_mail_correct_classification",
                new
                {
                    messageId = ids[0],
                    expectedClassificationVersion = 1,
                    classificationKey = "query",
                    reason = "Retry with a stale version.",
                    operationKey = "mcp:mail-correct-stale"
                })))
        {
            using var document = await ReadJsonRpcAsync(stale);
            Assert.Contains(
                "classification changed",
                document.RootElement.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        // A key outside the correction options is refused before the command.
        using (var invalid = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                13,
                "pegasus_mail_correct_classification",
                new
                {
                    messageId = ids[0],
                    expectedClassificationVersion = 2,
                    classificationKey = "not-a-key",
                    reason = "Invalid key.",
                    operationKey = "mcp:mail-correct-invalid"
                })))
        {
            using var document = await ReadJsonRpcAsync(invalid);
            Assert.Contains(
                "not a correction option",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        // A message with no classification decision cannot be corrected.
        using (var unclassified = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                14,
                "pegasus_mail_correct_classification",
                new
                {
                    messageId = ids[1],
                    expectedClassificationVersion = 1,
                    classificationKey = "query",
                    reason = "No decision exists for this message.",
                    operationKey = "mcp:mail-correct-unclassified"
                })))
        {
            using var document = await ReadJsonRpcAsync(unclassified);
            Assert.Contains(
                "no classification decision",
                document.RootElement.ToString(),
                StringComparison.Ordinal);
        }

        // Mutations demand the mcp:-prefixed caller idempotency key.
        using (var missingKey = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(
                15,
                "pegasus_mail_correct_classification",
                new
                {
                    messageId = ids[0],
                    expectedClassificationVersion = 2,
                    classificationKey = "query",
                    reason = "No operation key.",
                    operationKey = "not-prefixed"
                })))
        {
            using var document = await ReadJsonRpcAsync(missingKey);
            Assert.Contains(
                "operation key",
                document.RootElement.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            $"""
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_correct_classification'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'{operationKey}'
              AND ActorSubjectId = N'pegasus-automation'
            """));
        Assert.Equal(3, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_correct_classification'
              AND Outcome = N'Failed'
            """));

        // The corrected dossier is what the workspace now reads: version 2,
        // one permanent history entry, prior decision preserved.
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(16, "pegasus_mail_get", new { messageId = ids[0] })))
        {
            var structured = await ReadStructuredContentAsync(response);
            var classification = structured.GetProperty("classification");
            Assert.Equal(2, classification.GetProperty("version").GetInt32());
            Assert.Equal(
                "query",
                classification.GetProperty("current").GetProperty("category").GetString());
            Assert.Single(classification.GetProperty("history").EnumerateArray());
        }
    }

    /// <summary>
    /// Link to case is the staff act on a message (FRD-20): the Automation
    /// Actor reads the destinations the dialog offers and links through the
    /// same Core command, under a lease held for that one command.
    /// </summary>
    [Fact]
    public async Task TheActorLinksAMessageToACaseThroughTheStaffCommand()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var ids = await SeedRetainedAsync(mcpFactory, count: 2);
        await SeedClassificationAsync(mcpFactory, $"{MailboxId}-1");
        await SeedClassificationAsync(mcpFactory, $"{MailboxId}-0", sourceHash: 'F');
        var receiptId = await ReceiptIdAsync(mcpFactory, $"{MailboxId}-1");
        var caseId = await ImageIntakeTestData.SeedCaseAsync(
            mcpFactory.Services,
            await ReceiptIdAsync(mcpFactory, $"{MailboxId}-0"),
            "MAIL41001",
            nameof(Pegasus.Core.Workflow.CaseLifecycleState.Review));
        using var client = mcpFactory.CreateClient();

        var casesOnlyToken = await RequestTokenAsync(client, "automation.cases");
        using (var denied = await PostMcpAsync(
            client,
            casesOnlyToken,
            ToolCallPayload(30, "pegasus_mail_link_destinations", new { messageId = ids[0] })))
        {
            using var document = await ReadJsonRpcAsync(denied);
            Assert.Contains("automation.mail", document.RootElement.ToString(), StringComparison.Ordinal);
        }

        var token = await RequestTokenAsync(client, AllScopes);
        long intakeVersion;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(31, "pegasus_mail_get", new { messageId = ids[0] })))
        {
            var structured = await ReadStructuredContentAsync(response);
            intakeVersion = structured.GetProperty("intakeVersion").GetInt64();
            Assert.True(structured.GetProperty("canLinkCase").GetBoolean());
        }

        long caseVersion;
        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(32, "pegasus_mail_link_destinations", new { messageId = ids[0], query = "MAIL41001" })))
        {
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(intakeVersion, structured.GetProperty("intakeVersion").GetInt64());
            var destination = Assert.Single(structured.GetProperty("items").EnumerateArray());
            Assert.Equal(caseId, destination.GetProperty("caseId").GetGuid());
            Assert.False(destination.GetProperty("isTriageCase").GetBoolean());
            caseVersion = destination.GetProperty("version").GetInt64();
        }

        // A stale intake version is refused with the reload guidance, and
        // nothing is linked.
        using (var stale = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(33, "pegasus_mail_link_case", new
            {
                messageId = ids[0],
                caseId,
                expectedIntakeVersion = intakeVersion + 1,
                expectedCaseVersion = caseVersion,
                reason = "The message names this Case.",
                operationKey = "mcp:mail-link-stale"
            })))
        {
            using var document = await ReadJsonRpcAsync(stale);
            Assert.True(document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
            Assert.Contains("reload it with pegasus_mail_get", document.RootElement.ToString(), StringComparison.Ordinal);
        }
        await AssertLinkedAsync(mcpFactory, receiptId, expectedCaseId: null, expectedHistoryCount: 0);

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(34, "pegasus_mail_link_case", new
            {
                messageId = ids[0],
                caseId,
                expectedIntakeVersion = intakeVersion,
                expectedCaseVersion = caseVersion,
                reason = "The message names this Case.",
                operationKey = "mcp:mail-link-1"
            })))
        {
            var structured = await ReadStructuredContentAsync(response);
            Assert.Equal(caseId, structured.GetProperty("summary").GetProperty("caseId").GetGuid());
            Assert.False(structured.GetProperty("canLinkCase").GetBoolean());
        }
        await AssertLinkedAsync(mcpFactory, receiptId, caseId, expectedHistoryCount: 1);

        // A linked message is no longer a destination's source; a second link
        // is refused and changes nothing.
        using (var again = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(35, "pegasus_mail_link_case", new
            {
                messageId = ids[0],
                caseId,
                expectedIntakeVersion = intakeVersion + 1,
                expectedCaseVersion = caseVersion,
                reason = "Linking again.",
                operationKey = "mcp:mail-link-2"
            })))
        {
            using var document = await ReadJsonRpcAsync(again);
            Assert.True(document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean());
        }
        await AssertLinkedAsync(mcpFactory, receiptId, caseId, expectedHistoryCount: 1);

        Assert.Equal(1, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_link_case'
              AND Outcome = N'Succeeded'
              AND CorrelationId = N'mcp:mail-link-1'
            """));
        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND EventKind = N'pegasus_mail_link_case'
              AND Outcome = N'Failed'
            """));
    }

    /// <summary>
    /// Dismiss and Restore are the staff acts on a message (ADR-0064): the
    /// Automation Actor takes a message out of the incoming scopes and brings
    /// it back, in its own name.
    /// </summary>
    [Fact]
    public async Task TheActorDismissesAndRestoresAMessageInItsOwnName()
    {
        using var factory = new IntakeWebApplicationFactory(TimeProvider.System);
        using var mcpFactory = WithAutomationMcp(factory);
        var ids = await SeedRetainedAsync(mcpFactory, count: 1);
        using var client = mcpFactory.CreateClient();
        var token = await RequestTokenAsync(client, "automation.mail");

        using (var refused = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(2, "pegasus_mail_action", new
            {
                messageId = ids[0],
                action = "archive",
                operationKey = "mcp:mail-unknown-action"
            })))
        {
            Assert.Contains("dismiss or restore", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(3, "pegasus_mail_action", new
            {
                messageId = ids[0],
                action = "dismiss",
                operationKey = "mcp:mail-dismiss"
            })))
        {
            var dismissed = await ReadStructuredContentAsync(response);
            Assert.True(dismissed.GetProperty("isDismissed").GetBoolean());
        }

        using (var response = await PostMcpAsync(
            client,
            token,
            ToolCallPayload(4, "pegasus_mail_action", new
            {
                messageId = ids[0],
                action = "restore",
                operationKey = "mcp:mail-restore"
            })))
        {
            var restored = await ReadStructuredContentAsync(response);
            Assert.False(restored.GetProperty("isDismissed").GetBoolean());
        }

        Assert.Equal(2, await factory.Database.ScalarAsync<int>(
            """
            SELECT COUNT(*) FROM ActionHistory
            WHERE ActorKind = N'Automation'
              AND AggregateType = N'automation_mcp'
              AND EventKind = N'pegasus_mail_action'
              AND Outcome = N'Succeeded'
            """));
    }

    /// <summary>
    /// Seeds retained inbox messages exactly as the poller's retention path
    /// writes them, plus the poll state the freshness read model consumes —
    /// the same fixture shape as <see cref="MailWorkspaceWebTests"/>.
    /// </summary>
    private static async Task<Guid[]> SeedRetainedAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var approvedMailboxId = await TestMailboxId.EnsureApprovedAsync(
                context, MailboxId, MailboxAddress, NowUtc.AddDays(-1));
            if (!await context.ApprovedInboxPollStates.AnyAsync(item => item.ApprovedMailboxId == approvedMailboxId))
            {
                context.ApprovedInboxPollStates.Add(new()
                {
                    ApprovedMailboxId = approvedMailboxId,
                    MailboxAddress = MailboxAddress,
                    ScopeFingerprint = new string('A', 64),
                    ActivatedAtUtc = NowUtc.AddDays(-1),
                    DueAtUtc = NowUtc,
                    LastCompletedAtUtc = NowUtc.AddMinutes(-1)
                });
                await context.SaveChangesAsync();
            }
        }

        var store = scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>();
        for (var index = 0; index < count; index++)
        {
            var identity = $"{MailboxId}-{index}";
            await store.RetainAsync(
                new(
                    TestMailboxId.From(MailboxId),
                    MailboxAddress,
                    identity,
                    $"{MailboxId.Length}:{MailboxId}{identity}",
                    NowUtc.AddMinutes(-count + index),
                    1024,
                    new string('A', 64),
                    new(
                        "inbox",
                        $"conversation-{MailboxId}",
                        $"<{identity}@example.invalid>",
                        "sender@example.invalid",
                        "A Sender",
                        ["intake@collisionengineers.co.uk"],
                        [],
                        [],
                        $"Message {index} from {MailboxId}",
                        "Please inspect the vehicle at the address supplied.",
                        [new("estimate.pdf", "application/pdf", 2048)],
                        IsRead: false),
                    NowUtc),
                CancellationToken.None);
        }

        await using var readContext = await contextFactory.CreateDbContextAsync();
        return await readContext.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.MailboxId == TestMailboxId.From(MailboxId))
            .OrderByDescending(item => item.ReceivedAtUtc)
            .Select(item => item.Id)
            .ToArrayAsync();
    }

    /// <summary>
    /// Stores the classified-instruction intake receipt the classification
    /// read model joins on for the seeded message identity.
    /// </summary>
    private static async Task SeedClassificationAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string messageId,
        char sourceHash = 'E')
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>().StoreAsync(
            new(
                SourceFileName: "classified-instruction.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string(sourceHash, 64),
                SourceIdentity: new(
                    IntakeSourceChannel.Mailbox,
                    MailboxId.Length + ":" + MailboxId + messageId),
                ReceivedAtUtc: NowUtc,
                ProcessedAtUtc: NowUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: IntakeDecision.NeedsSorting,
                DecisionReason: "Fixture evaluation.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                Assets: [],
                MailClassificationDecision: MailClassificationResult.Classified(
                    MailCategory.NewInstruction,
                    [new("attachment.engineer-notification", true, "An attached document contains the generated title.")],
                    "An accepted Inspection instruction was recognised.",
                    "qdos_mail_classification",
                    3)),
            CancellationToken.None);
    }

    private static async Task<Guid> ReceiptIdAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        string messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var externalToken = $"{MailboxId.Length}:{MailboxId}{messageId}";
        return await context.IntakeReceipts
            .Where(item => item.SourceChannel == "mailbox" && item.ExternalReceiptToken == externalToken)
            .Select(item => item.Id)
            .SingleAsync();
    }

    private static async Task AssertLinkedAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory,
        Guid receiptId,
        Guid? expectedCaseId,
        int expectedHistoryCount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var association = await context.IntakeManualAssociations
            .SingleOrDefaultAsync(item => item.IntakeReceiptId == receiptId && item.IsActive);
        Assert.Equal(expectedCaseId, association?.CaseId);
        Assert.Equal(
            expectedHistoryCount,
            await context.IntakeMutationHistory.CountAsync(item => item.IntakeReceiptId == receiptId));
    }
}
