using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// C08: Send chaser and retained-message correspondence drive the real page
/// handlers against a recording <see cref="IStaffMailSend"/> double. GET never
/// sends; invalid context and stale workflow state return without a call.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class StaffCorrespondenceWebTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private static readonly CaseChaserRecipients ChaserRecipients = new(
        "instructions@principal.example",
        ["images@repairer.example", " Instructions@Principal.example "],
        "office@repairer.example");

    /// <summary>Without staff mail the chaser page offers no Send and refuses a forged one.</summary>
    [Fact]
    public async Task OfflineChaserRendersNoSendControlAndRefusesForgedSend()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory, new(false, true));
        using var client = CreateClient(factory);

        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("handler=Send", html, StringComparison.OrdinalIgnoreCase);

        using var post = await client.PostAsync(
            $"/Cases/{caseId:D}/Chaser?handler=Send",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client)
            }));
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    /// <summary>
    /// Send chaser on a Case with no instruction email to reply to (operator,
    /// 12 October 2026): it opens addressed, titled and worded from the Case
    /// and staff choose the Outgoing mailbox; the send is a new message
    /// recorded as the Case's chaser.
    /// </summary>
    [Fact]
    public async Task AChaserWithoutAnInstructionEmailIsANewMessageFromTheChosenMailbox()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var mailboxId = await SeedSendableMailboxAsync(baseFactory);
        var recipients = new FixedChaserRecipients(ChaserRecipients);
        using var factory = Configure(baseFactory, send, chaserRecipients: recipients);
        using var client = CreateClient(factory);
        var header = await CaseHeaderAsync(factory, caseId);
        Assert.NotNull(header.Workflow.DueWork);
        var facts = new CaseChaserFacts(
            header.Summary.Registration,
            header.Workflow.DueWork.MissingMaterialReason,
            header.Summary.Principal,
            header.Summary.Claimant);

        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.Matches(
            new Regex(
                $"<select[^>]*name=\"FromMailboxId\"[^>]*>.*?<option value=\"{mailboxId:D}\"[^>]*>sc08-sender@collisionengineers.co.uk</option>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline),
            html);
        Assert.Equal(
            "instructions@principal.example; images@repairer.example; office@repairer.example",
            InputValue(html, "To"));
        Assert.Equal(facts.Subject(), InputValue(html, "Subject"));
        var body = TextAreaValue(html, "Body").Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(
            EmailTemplates.Render(EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseChaser), facts.Values()),
            body);
        Assert.Equal(caseId, Assert.Single(recipients.CaseIds));
        Assert.Equal(0, send.SendCalls);

        using var response = await client.PostAsync(
            FormAction(html, "Send"),
            new FormUrlEncodedContent(ChaserForm(html, mailboxId)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("operationId=", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailPurpose.CaseChaser, command.Purpose);
        Assert.Equal(StaffMailComposeMode.New, command.ComposeMode);
        Assert.Null(command.OriginalMessage);
        Assert.Equal(mailboxId, command.ApprovedMailboxId);
        Assert.Equal(1, command.ExpectedMailboxGeneration);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(header.Workflow.Version, command.ExpectedContextVersion);
        Assert.Equal(facts.Subject(), command.Subject);
        Assert.Equal(
            ["instructions@principal.example", "images@repairer.example", "office@repairer.example"],
            command.To.Select(recipient => recipient.Address));

        // The status page the send lands on fills nothing in again.
        using var status = await client.GetAsync(response.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Equal(string.Empty, InputValue(statusHtml, "To"));
        Assert.Single(recipients.CaseIds);
    }

    /// <summary>With no instruction email to reply to, a chaser without a chosen From mailbox is not sent.</summary>
    [Fact]
    public async Task AChaserWithoutAChosenMailboxReturnsTheFormWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(
            baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");
        var html = await get.Content.ReadAsStringAsync();

        using var response = await client.PostAsync(
            FormAction(html, "Send"),
            new FormUrlEncodedContent(ChaserForm(html, fromMailboxId: null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            new CaseMailSenderRequiredException().Message,
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    /// <summary>
    /// Send chaser on a Case that arrived by an approved mailbox (operator,
    /// 12 October 2026): a reply to the instruction email, from the mailbox it
    /// arrived in, titled with the original's subject and one "Re:". No From
    /// mailbox is offered.
    /// </summary>
    [Fact]
    public async Task AChaserRepliesToTheInstructionEmailFromTheMailboxItArrivedIn()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var instruction = await SeedRetainedCorrespondenceAsync(baseFactory, caseId: null);
        await MakeInstructionEmailAsync(baseFactory, caseId, instruction.ReceiptId);
        using var factory = Configure(
            baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = CreateClient(factory);

        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("name=\"FromMailboxId\"", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{instruction.MailboxAddress}\"", html, StringComparison.Ordinal);
        Assert.Equal("Re: Source subject", InputValue(html, "Subject"));

        using var response = await client.PostAsync(
            FormAction(html, "Send"),
            new FormUrlEncodedContent(ChaserForm(html, fromMailboxId: null)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailPurpose.CaseChaser, command.Purpose);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.Equal(instruction.MailboxId, command.ApprovedMailboxId);
        Assert.Equal(instruction.MailboxGeneration, command.ExpectedMailboxGeneration);
        Assert.Equal(
            new StaffMailOriginalMessage(
                instruction.MessageId,
                instruction.MailboxId,
                instruction.ImmutableMessageId,
                instruction.InternetMessageId,
                instruction.ConversationId),
            command.OriginalMessage);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal("Re: Source subject", command.Subject);
    }

    /// <summary>
    /// C08-R-4: an unresolved chaser outcome shows its state and nothing else:
    /// no success banner, no Send and no Reconcile (the Worker's Sent poll
    /// settles it).
    /// </summary>
    [Theory]
    [InlineData(StaffMailState.Submitted)]
    [InlineData(StaffMailState.Unknown)]
    public async Task UnresolvedChaserOutcomeShowsItsStateWithoutResendOrReconcile(
        StaffMailState unresolvedState)
    {
        var send = new RecordingStaffMailSend { NextState = unresolvedState };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var mailboxId = await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(
            baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");
        var html = await get.Content.ReadAsStringAsync();

        using var response = await client.PostAsync(
            FormAction(html, "Send"),
            new FormUrlEncodedContent(ChaserForm(html, mailboxId)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, send.SendCalls);
        using var statusPage = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, statusPage.StatusCode);
        var statusHtml = await statusPage.Content.ReadAsStringAsync();
        Assert.DoesNotContain("notice--success", statusHtml, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.StaffMail.State(unresolvedState), statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Reconcile", statusHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Send</span>", statusHtml, StringComparison.Ordinal);
        Assert.Equal(1, send.SendCalls);
    }

    /// <summary>The chaser page's form as rendered, with the chosen From mailbox when one is posted.</summary>
    private static Dictionary<string, string> ChaserForm(string html, Guid? fromMailboxId)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["ExpectedContextVersion"] = InputValue(html, "ExpectedContextVersion"),
            ["To"] = InputValue(html, "To"),
            ["Subject"] = InputValue(html, "Subject"),
            ["Body"] = TextAreaValue(html, "Body")
        };
        if (fromMailboxId is { } id)
        {
            form["FromMailboxId"] = id.ToString("D");
        }
        return form;
    }

    /// <summary>Makes the seeded retained message the Case's instruction email: its receipt is the Case's origin.</summary>
    private static async Task MakeInstructionEmailAsync(
        IntakeWebApplicationFactory factory, Guid caseId, Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cases SET OriginIntakeReceiptId = {receiptId} WHERE Id = {caseId}");
    }

    /// <summary>A failed retained reply names its reason in operator words.</summary>
    [Fact]
    public async Task AFailedRetainedReplyNamesItsReason()
    {
        var send = new RecordingStaffMailSend
        {
            NextState = StaffMailState.Failed,
            NextFailureCode = "graph_rejected_403"
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 REPLY REASON", "SC08-MSG-REASON");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceSubject"] = "Re: Source subject",
            ["CorrespondenceBody"] = "Reviewed response."
        };

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, send.SendCalls);
        using var status = await client.GetAsync(response.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Contains(OperatorLabels.StaffMail.State(StaffMailState.Failed), statusHtml, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.StaffMail.Failure("graph_rejected_403"), statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Correspondence sent.", statusHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Reply", StaffMailComposeMode.Reply)]
    [InlineData("ReplyAll", StaffMailComposeMode.ReplyAll)]
    [InlineData("Forward", StaffMailComposeMode.Forward)]
    public async Task RetainedMessageCorrespondenceUsesExactIdentityCaseAndRecipients(
        string handler,
        StaffMailComposeMode expectedMode)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, $"SC08 {handler}", $"SC08-{handler}");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var mode = expectedMode switch
        {
            StaffMailComposeMode.Reply => "reply",
            StaffMailComposeMode.ReplyAll => "reply-all",
            _ => "forward"
        };

        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose={mode}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.Equal(0, send.SendCalls);
        var token = InputValue(html, "__RequestVerificationToken");
        var operationKey = InputValue(html, "CorrespondenceOperationKey");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["CorrespondenceOperationKey"] = operationKey,
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceSubject"] = "Re: Source subject",
            ["CorrespondenceBody"] = "Reviewed response."
        };
        if (expectedMode == StaffMailComposeMode.Forward)
        {
            form["CorrespondenceTo"] = "forward@example.invalid";
            form["CorrespondenceCc"] = string.Empty;
        }

        using var post = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler={handler}",
            new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(expectedMode, command.ComposeMode);
        Assert.Equal(caseId, command.ContextId);
        Assert.True(command.ExpectedContextVersion > 0);
        Assert.Equal(seeded.MailboxId, command.ApprovedMailboxId);
        Assert.Equal(seeded.MailboxGeneration, command.ExpectedMailboxGeneration);
        Assert.Equal(
            new StaffMailOriginalMessage(
                seeded.MessageId,
                seeded.MailboxId,
                seeded.ImmutableMessageId,
                seeded.InternetMessageId,
                seeded.ConversationId),
            command.OriginalMessage);
        Assert.DoesNotContain(command.To.Concat(command.Cc), item =>
            string.Equals(item.Address, seeded.MailboxAddress, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            expectedMode == StaffMailComposeMode.Forward
                ? ["forward@example.invalid"]
                : expectedMode == StaffMailComposeMode.Reply
                    ? ["reply@example.invalid"]
                    : ["reply@example.invalid", "other-to@example.invalid"],
            command.To.Select(item => item.Address));
        Assert.Equal(
            expectedMode == StaffMailComposeMode.ReplyAll
                ? ["copy@example.invalid"]
                : [],
            command.Cc.Select(item => item.Address));
    }

    [Fact]
    public async Task RetainedForwardSearchAndSelectionUseRenderedActionsAndRetainFormState()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var associatedCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 ASSOCIATED", "SC08-MSG-ASSOCIATED");
        var selectedCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 SELECTED", "SC08-MSG-SELECTED");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, associatedCaseId);
        using var factory = Configure(
            baseFactory,
            send,
            attachmentResolver: new StableAttachmentResolver());
        using var client = CreateClient(factory);
        // No unread scope: the v26 Inbox has no Unread filter (read state is not a queue).
        var listContext = $"mailbox={seeded.MailboxId:D}&folder=inbox&pageNumber=2&sort=oldest";
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=forward&{listContext}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        var associatedReference = await CaseReferenceAsync(factory, associatedCaseId);
        var selectedReference = await CaseReferenceAsync(factory, selectedCaseId);
        var draft = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["mailbox"] = InputValue(html, "mailbox"),
            ["folder"] = InputValue(html, "folder"),
            ["pageNumber"] = InputValue(html, "pageNumber"),
            ["search"] = InputValue(html, "search"),
            ["queue"] = InputValue(html, "queue"),
            ["sort"] = InputValue(html, "sort"),
            ["compose"] = InputValue(html, "compose"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceCaseReference"] = associatedReference,
            ["CorrespondenceCaseQuery"] = selectedReference,
            ["CorrespondenceTo"] = "selected@example.invalid",
            ["CorrespondenceCc"] = "copy@example.invalid",
            ["CorrespondenceSubject"] = "Fwd: Source subject",
            ["CorrespondenceBody"] = "Forward for the selected Case.",
            ["SelectedAttachments"] = StableAttachmentResolver.Selection
        };

        // Typing in Find a Case fetches the option list; the page and its
        // draft are untouched until an option is chosen.
        Assert.Contains(
            $"data-case-picker-url=\"/Inbox/{seeded.MessageId:D}?handler=CorrespondenceCaseOptions\"",
            html,
            StringComparison.Ordinal);
        using var options = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=CorrespondenceCaseOptions&q={Uri.EscapeDataString(selectedReference)}");
        Assert.Equal(HttpStatusCode.OK, options.StatusCode);
        var optionsHtml = await options.Content.ReadAsStringAsync();
        Assert.Contains("role=\"listbox\"", optionsHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"SelectedCorrespondenceCaseReference\"", optionsHtml, StringComparison.Ordinal);
        Assert.Contains($"value=\"{selectedReference}\"", optionsHtml, StringComparison.Ordinal);
        draft["SelectedCorrespondenceCaseReference"] = selectedReference;

        using var selectionResponse = await client.PostAsync(
            ButtonFormAction(optionsHtml, "SelectCorrespondenceCase"),
            new FormUrlEncodedContent(draft));
        Assert.Equal(HttpStatusCode.OK, selectionResponse.StatusCode);
        var selectedHtml = await selectionResponse.Content.ReadAsStringAsync();
        Assert.Equal(
            selectedReference,
            InputValue(selectedHtml, "CorrespondenceCaseReference"));
        Assert.Equal(
            (await CaseVersionAsync(factory, selectedCaseId)).ToString(System.Globalization.CultureInfo.InvariantCulture),
            InputValue(selectedHtml, "ExpectedCorrespondenceCaseVersion"));
        Assert.Equal("forward", InputValue(selectedHtml, "compose"));
        Assert.Equal(seeded.MailboxId.ToString("D"), InputValue(selectedHtml, "mailbox"));
        Assert.Equal(string.Empty, InputValue(selectedHtml, "folder"));
        Assert.Equal("2", InputValue(selectedHtml, "pageNumber"));
        Assert.Equal(string.Empty, InputValue(selectedHtml, "search"));
        Assert.Equal(string.Empty, InputValue(selectedHtml, "queue"));
        Assert.Equal("oldest", InputValue(selectedHtml, "sort"));
        Assert.Equal(draft["CorrespondenceOperationKey"], InputValue(selectedHtml, "CorrespondenceOperationKey"));
        Assert.Equal("selected@example.invalid", InputValue(selectedHtml, "CorrespondenceTo"));
        Assert.Equal("copy@example.invalid", InputValue(selectedHtml, "CorrespondenceCc"));
        Assert.Equal("Fwd: Source subject", InputValue(selectedHtml, "CorrespondenceSubject"));
        Assert.Equal("Forward for the selected Case.", TextAreaValue(selectedHtml, "CorrespondenceBody"));
        AssertSelectedAttachment(selectedHtml, StableAttachmentResolver.Selection);

        draft["__RequestVerificationToken"] = InputValue(selectedHtml, "__RequestVerificationToken");
        draft["CorrespondenceOperationKey"] = InputValue(selectedHtml, "CorrespondenceOperationKey");
        draft["ExpectedCorrespondenceCaseVersion"] = InputValue(
            selectedHtml, "ExpectedCorrespondenceCaseVersion");
        draft["CorrespondenceCaseReference"] = InputValue(
            selectedHtml, "CorrespondenceCaseReference");
        draft.Remove("SelectedCorrespondenceCaseReference");

        using var post = await client.PostAsync(
            FormAction(selectedHtml, "Forward"),
            new FormUrlEncodedContent(draft));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(selectedCaseId, command.ContextId);
        Assert.Equal(["selected@example.invalid"], command.To.Select(item => item.Address));
        Assert.Equal(["copy@example.invalid"], command.Cc.Select(item => item.Address));
        Assert.Single(command.Attachments);
    }

    [Fact]
    public async Task UnassociatedRetainedReplyRequiresACaseReferenceBeforeSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var selectedCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 UNASSOCIATED", "SC08-MSG-UNASSOCIATED");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId: null);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = RetainedReplyForm(
            html,
            InputValue(html, "CorrespondenceOperationKey"),
            "Reply for an unassociated message.");

        using var refused = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Empty(send.Commands);

        form["SelectedCorrespondenceCaseReference"] = await CaseReferenceAsync(factory, selectedCaseId);
        using var selection = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=SelectCorrespondenceCase&compose=reply",
            new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        var selectedHtml = await selection.Content.ReadAsStringAsync();
        Assert.Equal(
            await CaseReferenceAsync(factory, selectedCaseId),
            InputValue(selectedHtml, "CorrespondenceCaseReference"));
        Assert.Contains("reply@example.invalid", selectedHtml, StringComparison.Ordinal);
        var selectedForm = RetainedReplyForm(
            selectedHtml,
            InputValue(selectedHtml, "CorrespondenceOperationKey"),
            "Reply for an unassociated message.");
        using var sent = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply&compose=reply",
            new FormUrlEncodedContent(selectedForm));
        Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        Assert.Equal(selectedCaseId, Assert.Single(send.Commands).ContextId);
    }

    /// <summary>
    /// A message on a Triage Case: Reply, Reply all and Forward leave the
    /// composer's Case empty rather than defaulting the Triage's t. reference,
    /// which never resolves to an instruction Case and would fail the send.
    /// </summary>
    [Theory]
    [InlineData("reply")]
    [InlineData("reply-all")]
    [InlineData("forward")]
    public async Task ATriageMessageLeavesTheComposersCaseForStaffToChoose(string mode)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId: null);
        await MailboxIntakeTestData.SeedTriageCaseAsync(baseFactory.Services, seeded.ReceiptId, "t.QDOS31009");
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);

        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose={mode}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();

        Assert.Contains("t.QDOS31009", html, StringComparison.Ordinal);
        Assert.Equal(string.Empty, InputValue(html, "CorrespondenceCaseReference"));
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task UnknownRetainedReplyReplaysWithoutResendingOrReconcile()
    {
        var send = new RecordingStaffMailSend { NextState = StaffMailState.Unknown };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MSG", "SC08-MSG-UNKNOWN");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceSubject"] = "Re: Source subject",
            ["CorrespondenceBody"] = "Reviewed response."
        };

        using var first = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));
        using var replay = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal(1, send.SendCalls);
        Assert.Equal(ExtractOperationId(first.Headers.Location!), ExtractOperationId(replay.Headers.Location!));
        using var status = await client.GetAsync(replay.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Contains("Send status", statusHtml, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.StaffMail.State(StaffMailState.Unknown), statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Reconcile", statusHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, send.SendCalls);
    }

    /// <summary>
    /// The retained-message composer's one Case / PO field takes a reference
    /// typed in full: the reply goes to that Case, not to the message's own.
    /// </summary>
    [Fact]
    public async Task ATypedReferenceOnTheRetainedReplySendsToThatCase()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MSG OWN", "SC08-MSG-OWN");
        var typedCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MSG TYPED", "SC08-MSG-TYPED");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var typedReference = await CaseReferenceAsync(factory, typedCaseId);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("CorrespondenceCaseQuery", html, StringComparison.Ordinal);
        Assert.Contains("role=\"combobox\"", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
                ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
                ["FixedCorrespondenceCaseReference"] = InputValue(html, "FixedCorrespondenceCaseReference"),
                ["CorrespondenceCaseReference"] = typedReference,
                ["CorrespondenceSubject"] = "Re: Source subject",
                ["CorrespondenceBody"] = "Reviewed response."
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(typedCaseId, command.ContextId);
        Assert.Equal(await CaseVersionAsync(factory, typedCaseId), command.ExpectedContextVersion);
    }

    /// <summary>
    /// A failure of the send port itself (an offline transport, a composition
    /// invariant) surfaces as a server error; the reply handler does not turn
    /// it into a validation message.
    /// </summary>
    [Fact]
    public async Task ASendPortFailureSurfacesAsAServerError()
    {
        var send = new RecordingStaffMailSend
        {
            ThrowOnSend = new InvalidOperationException(
                "Staff mail delivery is unavailable in the DevelopmentOffline runtime profile.")
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 OFFLINE", "SC08-MSG-OFFLINE");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceSubject"] = "Re: Source subject",
            ["CorrespondenceBody"] = "Reviewed response."
        };

        // The test host runs in the Development environment, where the
        // developer exception page turns the unhandled exception into a 500
        // (see GraphMailWebhookTests.ValidNotificationQueueFailureReturnsRetryableServerError).
        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(
            "Staff mail delivery is unavailable in the DevelopmentOffline runtime profile.",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    /// <summary>
    /// An unresolved reply shows its state, and a second reply to the same
    /// message is still accepted (operator, 12 October 2026): an earlier
    /// send on the original no longer blocks another.
    /// </summary>
    [Theory]
    [InlineData(StaffMailState.Prepared)]
    [InlineData(StaffMailState.DraftCreating)]
    [InlineData(StaffMailState.DraftReady)]
    [InlineData(StaffMailState.Sending)]
    [InlineData(StaffMailState.Submitted)]
    [InlineData(StaffMailState.Unknown)]
    public async Task UnresolvedRetainedReplyShowsStatusAndAcceptsASecondReply(
        StaffMailState unresolvedState)
    {
        var send = new RecordingStaffMailSend { NextState = unresolvedState };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 SUBMITTED", "SC08-MSG-SUBMITTED");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var firstKey = InputValue(html, "CorrespondenceOperationKey");
        using var post = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(RetainedReplyForm(html, firstKey, "Reviewed response.")));
        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        using var status = await client.GetAsync(post.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Contains(OperatorLabels.StaffMail.State(unresolvedState), statusHtml, StringComparison.Ordinal);

        using var secondGet = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var secondHtml = await secondGet.Content.ReadAsStringAsync();
        var secondKey = InputValue(secondHtml, "CorrespondenceOperationKey");
        Assert.NotEqual(firstKey, secondKey);
        using var second = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(RetainedReplyForm(secondHtml, secondKey, "Second action.")));
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(2, send.SendCalls);
        Assert.All(send.Commands, command => Assert.Equal(seeded.MessageId, command.OriginalMessage!.RetainedMessageId));
    }

    [Theory]
    [InlineData(StaffMailState.Sent)]
    [InlineData(StaffMailState.Failed)]
    [InlineData(StaffMailState.Cancelled)]
    public async Task TerminalRetainedOperationAllowsASecondServerIssuedAction(
        StaffMailState terminalState)
    {
        var send = new RecordingStaffMailSend { NextState = terminalState };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 TERMINAL", $"SC08-MSG-{terminalState}");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);

        using var firstGet = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var firstHtml = await firstGet.Content.ReadAsStringAsync();
        var firstKey = InputValue(firstHtml, "CorrespondenceOperationKey");
        using var firstPost = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(RetainedReplyForm(firstHtml, firstKey, "First action.")));
        Assert.Equal(HttpStatusCode.Redirect, firstPost.StatusCode);
        Assert.Equal(1, send.SendCalls);

        send.NextState = StaffMailState.Submitted;
        send.NextPreparedAtUtc = NowUtc.AddDays(-1);
        using var secondGet = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var secondHtml = await secondGet.Content.ReadAsStringAsync();
        var secondKey = InputValue(secondHtml, "CorrespondenceOperationKey");
        Assert.NotEqual(firstKey, secondKey);
        using var secondPost = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(RetainedReplyForm(secondHtml, secondKey, "Second action.")));
        Assert.Equal(HttpStatusCode.Redirect, secondPost.StatusCode);
        Assert.Equal(2, send.SendCalls);
        using var activeStatus = await client.GetAsync(secondPost.Headers.Location);
        var activeHtml = await activeStatus.Content.ReadAsStringAsync();
        Assert.Contains(OperatorLabels.StaffMail.State(StaffMailState.Submitted), activeHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidRetainedComposeModeIsNotFoundWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 INVALID", "SC08-MSG-INVALID");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=resend");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task RolelessRetainedReplyIsForbiddenWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 ACTOR", "SC08-MSG-ACTOR");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/Inbox/{seeded.MessageId:D}?handler=Reply");
        request.Headers.Add("X-Test-Roleless", "1");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(send.Commands);
    }

    /// <summary>
    /// Reply leaves only the message's own mailbox, and only while it can
    /// send: Approved, Outgoing, activated, identified, its Sent folder bound
    /// and a positive generation.
    /// </summary>
    [Theory]
    [InlineData("absent", false, ApprovedMailboxState.Approved, true, "sent-items", 1)]
    [InlineData("disabled", true, ApprovedMailboxState.Disabled, true, "sent-items", 1)]
    [InlineData("incoming", true, ApprovedMailboxState.Approved, false, "sent-items", 1)]
    [InlineData("no-sent", true, ApprovedMailboxState.Approved, true, null, 1)]
    [InlineData("generation", true, ApprovedMailboxState.Approved, true, "sent-items", 0)]
    public async Task RetainedReplyRequiresASendReadyOutgoingMailbox(
        string row,
        bool available,
        ApprovedMailboxState state,
        bool outgoing,
        string? sentFolderIdentity,
        long generation)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MAILBOX", $"SC08-MSG-{row}");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        var mailbox = new ApprovedMailbox(
            seeded.MailboxId,
            seeded.MailboxAddress,
            outgoing
                ? [ApprovedMailboxRouteScope.Incoming, ApprovedMailboxRouteScope.Outgoing]
                : [ApprovedMailboxRouteScope.Incoming],
            state,
            "mailbox-identity", "inbox", sentFolderIdentity, true, NowUtc, 1, generation);
        using var configured = Configure(baseFactory, send);
        using var factory = configured.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddSingleton<IApprovedMailboxStore>(
                    new FixedApprovedMailboxStore(available ? [mailbox] : []));
            }));
        using var client = CreateClient(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["CorrespondenceOperationKey"] = $"mailbox-negative:{Guid.NewGuid():N}",
                ["CorrespondenceSubject"] = "Re: Source subject",
                ["CorrespondenceBody"] = "Reviewed response."
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(send.Commands);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("[]")]
    [InlineData("[\"not-an-address\"]")]
    public async Task RetainedReplyIsUnavailableWithoutValidStoredReplyTargets(string? replyTargetsJson)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 SENDER", $"SC08-MSG-SENDER-{Guid.NewGuid():N}");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        await using (var scope = baseFactory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE RetainedMailboxMessages SET ReplyToAddressesJson = {replyTargetsJson} WHERE Id = {seeded.MessageId}");
        }
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("compose=reply", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=forward", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"CorrespondenceOperationKey\"", html, StringComparison.OrdinalIgnoreCase);
        // The Case's dialog offers what the record offers.
        using var content = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?handler=Content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        var fragment = await content.Content.ReadAsStringAsync();
        Assert.DoesNotContain("compose=reply", fragment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=forward", fragment, StringComparison.OrdinalIgnoreCase);
        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = $"fresh:{Guid.NewGuid():N}",
                ["CorrespondenceSubject"] = "Re: Source subject",
                ["CorrespondenceBody"] = "Reviewed response."
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task CaseDialogContentOffersTheRecordsReplyAndForwardWithItsCaseChosen()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var associatedCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 ASSOCIATED", "SC08-MSG-DIALOG-ASSOCIATED");
        var dialogCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 DIALOG", "SC08-MSG-DIALOG");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, associatedCaseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var dialogReference = await CaseReferenceAsync(factory, dialogCaseId);
        Assert.NotEqual(await CaseReferenceAsync(factory, associatedCaseId), dialogReference);

        using var content = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Content&correspondenceCaseReference={dialogReference}");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        var fragment = await content.Content.ReadAsStringAsync();
        Assert.Contains("data-message-actions", fragment, StringComparison.Ordinal);
        foreach (var (mode, label) in new[] { ("reply", "Reply"), ("reply-all", "Reply all"), ("forward", "Forward") })
        {
            Assert.Contains(
                $"href=\"/Inbox/{seeded.MessageId:D}?compose={mode}&amp;correspondenceCaseReference={dialogReference}\"",
                fragment,
                StringComparison.Ordinal);
            Assert.Contains($">{label}</a>", fragment, StringComparison.Ordinal);
        }
        Assert.DoesNotContain("<form", fragment, StringComparison.OrdinalIgnoreCase);

        // Reply opens the record's composer with the dialog's Case chosen.
        using var composer = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?compose=reply&correspondenceCaseReference={dialogReference}");
        Assert.Equal(HttpStatusCode.OK, composer.StatusCode);
        var html = await composer.Content.ReadAsStringAsync();
        Assert.Equal(dialogReference, InputValue(html, "CorrespondenceCaseReference"));
        Assert.Contains("reply@example.invalid", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task RetainedForwardRejectsEmptyRecipients()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 FORWARD", "SC08-MSG-FORWARD-EMPTY");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=forward");
        var html = await get.Content.ReadAsStringAsync();

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Forward",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
                ["CorrespondenceTo"] = string.Empty,
                ["CorrespondenceCc"] = string.Empty,
                ["CorrespondenceSubject"] = "Fwd: Source subject",
                ["CorrespondenceBody"] = "Forwarded material."
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(send.Commands);
    }

    [Fact]
    public async Task RetainedReplyRejectsAStaleCaseContextWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MSG", "SC08-MSG-STALE");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceSubject"] = "Re: Source subject",
            ["CorrespondenceBody"] = "Reviewed response."
        };
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
            workflow.Version++;
            await context.SaveChangesAsync();
        }

        using var post = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        post.EnsureSuccessStatusCode();
        Assert.Empty(send.Commands);
    }

    /// <summary>
    /// Also resolve query (operator, 11 October 2026): the composer offers the
    /// tick only for a reply or reply all whose Case holds an open query, and
    /// ticks it when reached from the Case's Resolve query step
    /// (<c>resolveQuery=true</c>). A forward, or a Case without an open query,
    /// offers none.
    /// </summary>
    [Theory]
    [InlineData("reply", true, false, true)]
    [InlineData("reply-all", true, false, true)]
    [InlineData("forward", true, false, false)]
    [InlineData("reply", false, false, false)]
    [InlineData("reply", true, true, true)]
    public async Task TheReplyComposerOffersAlsoResolveQueryOnlyForAReplyToACaseWithAnOpenQuery(
        string mode, bool hasOpenQuery, bool fromResolveQuery, bool offered)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 QUERY", "SC08-MSG-QUERY");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        if (hasOpenQuery)
        {
            await MarkOpenQueryAsync(baseFactory, caseId);
        }
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);

        using var get = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?compose={mode}" + (fromResolveQuery ? "&resolveQuery=true" : string.Empty));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();

        var tick = Regex.Match(html, "<input[^>]*data-resolve-query[^>]*>", RegexOptions.CultureInvariant);
        Assert.Equal(offered, tick.Success);
        Assert.Equal(offered, html.Contains(OperatorLabels.StaffMail.AlsoResolveQuery, StringComparison.Ordinal));
        if (offered)
        {
            Assert.Contains("type=\"checkbox\"", tick.Value, StringComparison.Ordinal);
            Assert.Equal(fromResolveQuery, tick.Value.Contains("checked", StringComparison.Ordinal));
        }
        Assert.Equal(0, send.SendCalls);
    }

    /// <summary>
    /// The reply carries the tick to the send command, which resolves the
    /// Case's open queries once the Sent item is observed; an unticked reply
    /// resolves nothing.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AReplyCarriesAlsoResolveQueryToTheSendCommand(bool ticked)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 QUERY SEND", "SC08-MSG-QUERY-SEND");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        await MarkOpenQueryAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = RetainedReplyForm(html, InputValue(html, "CorrespondenceOperationKey"), "Answered the query.");
        if (ticked)
        {
            form["ResolveQuery"] = "true";
        }

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.Equal(ticked, command.ResolvesCaseQueries);
    }

    /// <summary>The Case holds an open query, as the query ledger marks it.</summary>
    private static async Task MarkOpenQueryAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET HasOpenQuery = 1 WHERE CaseId = {caseId}");
    }

    private static Guid ExtractOperationId(Uri location)
    {
        var match = Regex.Match(
            location.ToString(),
            "(?:mailOperationId|operationId)=(?<id>[0-9a-fA-F-]{36})");
        Assert.True(match.Success, $"'{location}' did not carry an operationId.");
        return Guid.Parse(match.Groups["id"].Value);
    }

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory,
        RecordingStaffMailSend send,
        IStaffMailAttachmentResolver? attachmentResolver = null,
        ICaseChaserRecipientQueries? chaserRecipients = null) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffMailSend>();
                services.AddSingleton<IStaffMailSend>(send);
                if (chaserRecipients is not null)
                {
                    services.RemoveAll<ICaseChaserRecipientQueries>();
                    services.AddSingleton(chaserRecipients);
                }
                if (attachmentResolver is not null)
                {
                    services.RemoveAll<IStaffMailAttachmentResolver>();
                    services.AddSingleton(attachmentResolver);
                }
            }));

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The input '{name}' was not rendered.");
        var value = Regex.Match(tag.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
        // A present input without a value attribute submits an empty value.
        return value.Success ? WebUtility.HtmlDecode(value.Groups["value"].Value) : string.Empty;
    }

    private static string ButtonFormAction(string html, string handler)
    {
        var button = Regex.Match(
            html,
            $"<button[^>]*formaction=\"(?<action>[^\"]*handler={Regex.Escape(handler)}[^\"]*)\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(button.Success, $"The '{handler}' button had no form action.");
        return WebUtility.HtmlDecode(button.Groups["action"].Value);
    }

    private static string TextAreaValue(string html, string name)
    {
        var area = Regex.Match(
            html,
            $"<textarea[^>]*name=\"{Regex.Escape(name)}\"[^>]*>(?<value>.*?)</textarea>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(area.Success, $"The textarea '{name}' was not rendered.");
        var value = WebUtility.HtmlDecode(area.Groups["value"].Value);
        // HTML ignores one newline immediately after the textarea start tag.
        // Preserve any additional newline that belongs to the entered draft.
        if (value.StartsWith("\r\n", StringComparison.Ordinal))
        {
            return value[2..];
        }
        return value.StartsWith('\n') || value.StartsWith('\r') ? value[1..] : value;
    }

    private static void AssertSelectedAttachment(string html, string selection) =>
        Assert.Matches(new Regex(
                $"<input[^>]*name=\"SelectedAttachments\"[^>]*value=\"{Regex.Escape(selection)}\"[^>]*checked",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            html);

    private static string FormAction(string html, string handler)
    {
        var form = Regex.Match(
            html,
            $"<form[^>]*action=\"(?<action>[^\"]*handler={Regex.Escape(handler)}[^\"]*)\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(form.Success, $"The '{handler}' form had no action.");
        return WebUtility.HtmlDecode(form.Groups["action"].Value);
    }

    private static Dictionary<string, string> RetainedReplyForm(
        string html,
        string operationKey,
        string body) => new()
    {
        ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
        ["CorrespondenceOperationKey"] = operationKey,
        ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
        ["CorrespondenceCaseReference"] = InputValue(html, "CorrespondenceCaseReference"),
        ["CorrespondenceSubject"] = "Re: Source subject",
        ["CorrespondenceBody"] = body
    };

    private sealed class FixedApprovedMailboxStore(IReadOnlyList<ApprovedMailbox> mailboxes)
        : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult(mailboxes);

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The read-only mailbox test cannot update a mailbox.");

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    private static Task<Guid> SeedSupportedCaseAsync(
        WebApplicationFactory<Program> factory,
        HttpClient _client,
        string _registration,
        string _claimNumber) =>
        AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);

    /// <summary>An Outgoing mailbox that can send, with no retained mail; its id.</summary>
    private static async Task<Guid> SeedSendableMailboxAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var mailboxId = await TestMailboxId.EnsureApprovedAsync(
            context, "sc08-sender", "sc08-sender@collisionengineers.co.uk", NowUtc.AddDays(-1));
        await context.SaveChangesAsync();
        var mailbox = await context.ApprovedMailboxes.SingleAsync(item => item.Id == mailboxId);
        mailbox.AllowOutgoing = true;
        mailbox.SentFolderIdentity = "sent-items";
        mailbox.MailboxGeneration = 1;
        await context.SaveChangesAsync();
        return mailboxId;
    }

    private sealed record SeededCorrespondence(
        Guid MessageId,
        Guid MailboxId,
        string MailboxAddress,
        long MailboxGeneration,
        string ImmutableMessageId,
        string InternetMessageId,
        string ConversationId,
        Guid ReceiptId);

    private static async Task<SeededCorrespondence> SeedRetainedCorrespondenceAsync(
        IntakeWebApplicationFactory factory,
        Guid? caseId)
    {
        const string mailboxGraphId = "sc08-sender";
        const string mailboxAddress = "sc08-sender@collisionengineers.co.uk";
        const long mailboxGeneration = 7;
        var mailboxId = TestMailboxId.From(mailboxGraphId);
        var immutableMessageId = $"retained-{Guid.NewGuid():N}";
        var internetMessageId = $"<{immutableMessageId}@example.invalid>";
        var conversationId = $"conversation-{Guid.NewGuid():N}";
        var externalToken = $"retained-token:{Guid.NewGuid():N}";
        var receiptId = Guid.NewGuid();

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await TestMailboxId.EnsureApprovedAsync(
                context, mailboxGraphId, mailboxAddress, NowUtc.AddDays(-1));
            await context.SaveChangesAsync();
            var mailbox = await context.ApprovedMailboxes.SingleAsync(item => item.Id == mailboxId);
            mailbox.AllowOutgoing = true;
            mailbox.SentFolderIdentity = "sent-items";
            mailbox.MailboxGeneration = mailboxGeneration;
            if (!await context.ApprovedInboxPollStates.AnyAsync(
                    item => item.ApprovedMailboxId == mailboxId))
            {
                context.ApprovedInboxPollStates.Add(new()
                {
                    ApprovedMailboxId = mailboxId,
                    MailboxAddress = mailboxAddress,
                    ScopeFingerprint = new string('A', 64),
                    ActivatedAtUtc = NowUtc.AddDays(-1),
                    DueAtUtc = NowUtc,
                    LastCompletedAtUtc = NowUtc.AddMinutes(-1)
                });
            }
            if (caseId is { } associatedCaseId)
            {
                var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == associatedCaseId);
                if (workflow.Version <= 0)
                    workflow.Version = 1;
            }
            context.IntakeReceipts.Add(new()
            {
                Id = receiptId,
                SourceFileName = "retained-message.msg",
                MediaType = "application/vnd.ms-outlook",
                SourceLength = 1024,
                SourceHash = new string('A', 64),
                SourceChannel = "mailbox",
                ExternalReceiptToken = externalToken,
                ReceivedAtUtc = NowUtc,
                ProcessedAtUtc = NowUtc,
                SourceReaderKey = "fixture-reader",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.NeedsSorting),
                DecisionReason = "Fixture retained correspondence.",
                EvidenceJson = EfIntakeReceiptStore.SerializeEvidence([]),
                FieldsJson = EfIntakeReceiptStore.SerializeFields([]),
                OcrCandidatesJson = EfIntakeReceiptStore.SerializeEnvelope<
                    IReadOnlyList<ScannedPdfOcrCandidate>>([])
            });
            if (caseId is { } linkedCaseId)
            {
                context.IntakeManualAssociations.Add(new()
                {
                    IntakeReceiptId = receiptId,
                    CaseId = linkedCaseId,
                    IsActive = true,
                    Version = 1,
                    LinkedAtUtc = NowUtc,
                    ActorKind = ActorKind.Staff.ToString(),
                    ActorSubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
                    ActorRolesJson = "[\"Administrator\"]",
                    Reason = "Fixture message association.",
                    LastOperationKey = $"fixture-link:{Guid.NewGuid():N}"
                });
            }
            await context.SaveChangesAsync();
        }

        await scope.ServiceProvider.GetRequiredService<EfRetainedMailboxMessageStore>().RetainAsync(
            new(
                mailboxId,
                mailboxAddress,
                immutableMessageId,
                externalToken,
                NowUtc,
                1024,
                new string('B', 64),
                new(
                    "inbox",
                    conversationId,
                    internetMessageId,
                    "sender@example.invalid",
                    "Sender Name",
                    [mailboxAddress, "other-to@example.invalid"],
                    ["copy@example.invalid", mailboxAddress, "other-to@example.invalid"],
                    ["reply@example.invalid"],
                    "Source subject",
                    "Source body.",
                    [],
                    IsRead: false),
                NowUtc),
            CancellationToken.None);

        await using var read = await contextFactory.CreateDbContextAsync();
        var messageId = await read.RetainedMailboxMessages
            .Where(item => item.MailboxId == mailboxId && item.ImmutableMessageId == immutableMessageId)
            .Select(item => item.Id)
            .SingleAsync();
        return new(
            messageId,
            mailboxId,
            mailboxAddress,
            mailboxGeneration,
            immutableMessageId,
            internetMessageId,
            conversationId,
            receiptId);
    }

    private static async Task<long> CaseVersionAsync(WebApplicationFactory<Program> factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var getCase = scope.ServiceProvider.GetRequiredService<IGetCaseHeader>();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var details = await getCase.ExecuteAsync(new(caseId, actor), CancellationToken.None);
        return details!.Workflow.Version;
    }

    private sealed class StableAttachmentResolver : IStaffMailAttachmentResolver
    {
        public const string Selection = "stable-attachment";

        private static readonly StaffMailAttachment Attachment = new(
            Guid.Parse("5a1f0586-9a5f-4a90-866a-99552f7e6b20"),
            Guid.Parse("8c2bc8f2-d8c5-4cce-8602-17e6b27464e9"),
            new string('A', 64), 1, "instruction.pdf", "application/pdf");

        public Task<IReadOnlyList<StaffMailAttachmentOption>> ListCaseAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffMailAttachmentOption>>(
                [new(Selection, Attachment.FileName, Attachment.MediaType, Attachment.ContentLength)]);

        public Task<IReadOnlyList<StaffMailAttachment>> ResolveCaseAsync(
            ActionActor actor, Guid caseId, IReadOnlyList<string> selections,
            CancellationToken cancellationToken)
        {
            if (selections.Count != 1 || selections[0] != Selection)
            {
                throw new StaffMailAttachmentSelectionException(
                    "One or more selected attachments changed or are no longer available. Review the attachments and try again.");
            }

            return Task.FromResult<IReadOnlyList<StaffMailAttachment>>([Attachment]);
        }

        public Task<IReadOnlyList<StaffMailAttachmentOption>> ListIntakeAsync(
            ActionActor actor, Guid receiptId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffMailAttachmentOption>>([]);

        public Task<IReadOnlyList<StaffMailAttachment>> ResolveIntakeAsync(
            ActionActor actor, Guid receiptId, IReadOnlyList<string> selections,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffMailAttachment>>([]);
    }

    private static async Task<CaseHeader> CaseHeaderAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var getCase = scope.ServiceProvider.GetRequiredService<IGetCaseHeader>();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        return (await getCase.ExecuteAsync(new(caseId, actor), CancellationToken.None))!;
    }

    /// <summary>The recorded chaser addresses, the same for every Case, and the Cases asked about.</summary>
    private sealed class FixedChaserRecipients(CaseChaserRecipients recipients) : ICaseChaserRecipientQueries
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Guid> caseIds = new();

        public IReadOnlyCollection<Guid> CaseIds => caseIds.ToArray();

        public Task<CaseChaserRecipients?> GetAsync(Guid caseId, CancellationToken cancellationToken)
        {
            caseIds.Enqueue(caseId);
            return Task.FromResult<CaseChaserRecipients?>(recipients);
        }
    }

    private static async Task<string> CaseReferenceAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var getCase = scope.ServiceProvider.GetRequiredService<IGetCaseHeader>();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var details = await getCase.ExecuteAsync(new(caseId, actor), CancellationToken.None);
        return details!.Summary.Reference;
    }

    /// <summary>
    /// Records every genuinely-new send and, keyed by <c>OperationKey</c>,
    /// returns the already-recorded operation on a replay without adding a
    /// second command — the exact idempotency S12 requires of a real
    /// <see cref="IStaffMailSend"/> implementation. <see cref="NextState"/>
    /// lets a test choose the outcome the next new send observes.
    /// </summary>
    private sealed class RecordingStaffMailSend : IStaffMailSend
    {
        private readonly object sync = new();
        private readonly List<StaffMailSendCommand> commands = [];
        private readonly Dictionary<string, StaffMailOperation> byOperationKey = [];
        private readonly Dictionary<Guid, StaffMailOperation> byOperationId = [];

        public int SendCalls
        {
            get { lock (sync) return commands.Count; }
        }

        public IReadOnlyList<StaffMailSendCommand> Commands
        {
            get { lock (sync) return commands.ToArray(); }
        }

        public StaffMailState NextState { get; set; } = StaffMailState.Submitted;

        /// <summary>The engine's failure code a Failed <see cref="NextState"/> carries.</summary>
        public string? NextFailureCode { get; set; } = "graph_rejected_403";

        public DateTimeOffset NextPreparedAtUtc { get; set; } = NowUtc;

        /// <summary>
        /// When set, <see cref="SendAsync"/> throws this immediately, before
        /// recording any command — simulating an offline transport
        /// (<c>UnavailableStaffMailSend</c>) or a composition invariant
        /// failure.
        /// </summary>
        public Exception? ThrowOnSend { get; set; }

        public Task<StaffMailOperation> SendAsync(
            StaffMailSendCommand command, CancellationToken cancellationToken)
        {
            if (ThrowOnSend is { } failure)
            {
                throw failure;
            }
            lock (sync)
            {
                if (byOperationKey.TryGetValue(command.OperationKey, out var existing))
                {
                    return Task.FromResult(existing);
                }

                commands.Add(command);
                var preparedAtUtc = NextPreparedAtUtc;
                var operation = new StaffMailOperation(
                    Guid.NewGuid(),
                    NextState,
                    NextState == StaffMailState.Sent ? StaffMailAttemptStage.ObserveSent : StaffMailAttemptStage.CreateDraft,
                    1,
                    preparedAtUtc,
                    NextState == StaffMailState.Sent ? preparedAtUtc : null,
                    NextState == StaffMailState.Sent ? preparedAtUtc : null,
                    NextState == StaffMailState.Failed ? NextFailureCode : null,
                    command.ApprovedMailboxId,
                    command.ExpectedMailboxGeneration,
                    new string('A', 64),
                    preparedAtUtc,
                    null,
                    command.Purpose,
                    command.ContextId,
                    command.ExpectedContextVersion,
                    command.OriginalMessage?.RetainedMessageId);
                byOperationKey[command.OperationKey] = operation;
                byOperationId[operation.Id] = operation;
                return Task.FromResult(operation);
            }
        }

        public Task<StaffMailOperation?> GetAsync(
            ActionActor actor, Guid operationId, CancellationToken cancellationToken) =>
            LockedGet(operationId);

        public Task<StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                var matching = commands
                    .Where(item => item.OriginalMessage?.RetainedMessageId == retainedMessageId)
                    .Select(item => byOperationKey[item.OperationKey])
                    .ToArray();
                return Task.FromResult<StaffMailOperation?>(
                    matching.LastOrDefault(operation => IsActive(operation.State))
                    ?? matching.LastOrDefault());
            }
        }

        private Task<StaffMailOperation?> LockedGet(Guid operationId)
        {
            lock (sync)
            {
                return Task.FromResult<StaffMailOperation?>(
                    byOperationId.TryGetValue(operationId, out var operation) ? operation : null);
            }
        }

        private static bool IsActive(StaffMailState state) => state is
            StaffMailState.Prepared or StaffMailState.DraftCreating or StaffMailState.DraftReady
            or StaffMailState.Sending or StaffMailState.Submitted or StaffMailState.Unknown;

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by this test.");
    }
}
