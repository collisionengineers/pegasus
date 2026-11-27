using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.AiWork;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
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

    /// <summary>The seeded instructions@ mailbox, the only one whose staff forwards are answered as the original (R652).</summary>
    private const string InstructionsGraphId = "instructions";

    private const string InstructionsAddress = "instructions@collisionengineers.co.uk";

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
        using var client = IntakeWebDriver.CreateClient(factory);

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
        using var client = IntakeWebDriver.CreateClient(factory);
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
            Recipients(html, "To"));
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

        // The status page the send lands on shows the send and offers no
        // second form for it (R1330).
        using var status = await client.GetAsync(response.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Contains("data-mail-send-status", statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"To\"", statusHtml, StringComparison.Ordinal);
        Assert.Single(recipients.CaseIds);
    }

    /// <summary>
    /// With no default sender (no instruction email to reply to and no Send
    /// from), a chaser without a chosen From mailbox is not sent.
    /// </summary>
    [Fact]
    public async Task AChaserWithoutAChosenMailboxReturnsTheFormWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(
            baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = IntakeWebDriver.CreateClient(factory);
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
    /// With no instruction email to reply to, the chaser's From starts on the
    /// Principal's Send from (operator, 3 November 2026; R654); the other
    /// Outgoing mailboxes stay offered.
    /// </summary>
    [Fact]
    public async Task AChaserWithoutAnInstructionEmailStartsFromThePrincipalsSendFrom()
    {
        const string sendFrom = "sc08-send-from@collisionengineers.co.uk";
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var otherId = await SeedSendableMailboxAsync(baseFactory);
        var sendFromId = await SeedSendableMailboxAsync(baseFactory, "sc08-send-from", sendFrom);
        await SetSendFromAsync(baseFactory, caseId, sendFrom);
        using var factory = Configure(
            baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.Equal([sendFromId.ToString("D")], SelectedOptions(html, "FromMailboxId"));
        Assert.Contains($"<option value=\"{otherId:D}\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, send.SendCalls);
    }

    /// <summary>
    /// Send chaser on a Case that arrived by an approved mailbox (operator,
    /// 12 October 2026): a reply to the instruction email, titled with the
    /// original's subject and one "Re:", its From starting on the mailbox the
    /// instruction arrived in, which staff may change (R655).
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
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.Equal([instruction.MailboxId.ToString("D")], SelectedOptions(html, "FromMailboxId"));
        Assert.Equal("Re: Source subject", InputValue(html, "Subject"));

        using var response = await client.PostAsync(
            FormAction(html, "Send"),
            new FormUrlEncodedContent(ChaserForm(html, instruction.MailboxId)));

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
    /// A chaser sent from another Outgoing mailbox than the one the
    /// instruction arrived in (operator, 4 November 2026; R655) is still the
    /// reply to the instruction email.
    /// </summary>
    [Fact]
    public async Task AChaserFromAnotherChosenMailboxStillRepliesToTheInstructionEmail()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var instruction = await SeedRetainedCorrespondenceAsync(baseFactory, caseId: null);
        await MakeInstructionEmailAsync(baseFactory, caseId, instruction.ReceiptId);
        var otherId = await SeedSendableMailboxAsync(
            baseFactory, "sc08-other-sender", "sc08-other-sender@collisionengineers.co.uk");
        using var factory = Configure(
            baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");
        var html = await get.Content.ReadAsStringAsync();
        Assert.Contains($"<option value=\"{otherId:D}\"", html, StringComparison.OrdinalIgnoreCase);

        using var response = await client.PostAsync(
            FormAction(html, "Send"),
            new FormUrlEncodedContent(ChaserForm(html, otherId)));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.Equal(otherId, command.ApprovedMailboxId);
        Assert.Equal(instruction.MessageId, command.OriginalMessage!.RetainedMessageId);
        Assert.Equal(instruction.MailboxId, command.OriginalMessage.ApprovedMailboxId);
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
        using var client = IntakeWebDriver.CreateClient(factory);
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

    /// <summary>
    /// A chaser whose provider answer is ambiguous (a Graph server error or a
    /// timeout, which the engine records Unknown and throws) keeps staff on
    /// the composer with "The send result is not yet known." and its Unknown
    /// Send status, never the error page, and offers no Send while it is
    /// unresolved, as a returned Unknown does (R1069, R1123).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUnknownChaserOutcomeKeepsStaffOnTheComposerWithItsStatus(bool timedOut)
    {
        var send = new RecordingStaffMailSend
        {
            NextState = StaffMailState.Unknown,
            ProviderFault = AmbiguousProviderAnswer(timedOut)
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var mailboxId = await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(
            baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Cases/{caseId:D}/Chaser");
        var html = await get.Content.ReadAsStringAsync();

        using var response = await client.PostAsync(
            FormAction(html, "Send"),
            new FormUrlEncodedContent(ChaserForm(html, mailboxId)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(CaseWorkspaceLabels.ReportDelivery.SendUnknown, page, StringComparison.Ordinal);
        Assert.Contains("Send status", page, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.StaffMail.State(StaffMailState.Unknown), page, StringComparison.Ordinal);
        Assert.DoesNotContain("notice--success", page, StringComparison.Ordinal);
        Assert.DoesNotContain(">Send</span>", page, StringComparison.Ordinal);
        Assert.Equal(1, send.SendCalls);
    }

    /// <summary>What Graph answers that leaves a send's outcome unknown: a 503, or no answer in time.</summary>
    /// <summary>
    /// The one send-e-mail dialogue (operator, 27 November 2026; R1329,
    /// R1330): Reply all's To and Cc are Send report's boxes, starting on the
    /// original's recipients; what staff leave in them is what is sent, and
    /// a To emptied is refused without a send.
    /// </summary>
    [Fact]
    public async Task AReplysToAndCcAreBoxesStaffChangeBeforeSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(baseFactory, seedClient, "SC08 BOXES", "SC08-BOXES");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        var html = await IntakeWebDriver.GetHtmlAsync(
            client, $"/Inbox/{seeded.MessageId:D}?handler=Dialog&compose=reply-all");

        Assert.Contains("class=\"dialog mail-compose-dialog dialog--send\"", html, StringComparison.Ordinal);
        Assert.Matches("<span class=\"recip-box\">\\s*<input name=\"CorrespondenceTo\" type=\"email\" value=\"reply@example.invalid\"", html);
        Assert.Matches("<span class=\"recip-box\">\\s*<input name=\"CorrespondenceTo\" type=\"email\" value=\"other-to@example.invalid\"", html);
        Assert.Matches("<span class=\"recip-box\">\\s*<input name=\"CorrespondenceCc\" type=\"email\" value=\"copy@example.invalid\"", html);
        Assert.Contains("data-add-mail-recipient", html, StringComparison.Ordinal);

        var form = RetainedReplyForm(html, InputValue(html, "CorrespondenceOperationKey"), "Reviewed response.");
        form["FixedCorrespondenceCaseReference"] = InputValue(html, "FixedCorrespondenceCaseReference");
        form["CorrespondenceTo"] = string.Empty;
        using (var refused = await client.PostAsync(
                   $"/Inbox/{seeded.MessageId:D}?handler=ReplyAll", new FormUrlEncodedContent(form)))
        {
            Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
            Assert.Contains("At least one recipient is required.", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        Assert.Equal(0, send.SendCalls);

        form["CorrespondenceTo"] = "changed@example.invalid";
        form["CorrespondenceCc"] = string.Empty;
        using var sent = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=ReplyAll", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailComposeMode.ReplyAll, command.ComposeMode);
        Assert.Equal("changed@example.invalid", Assert.Single(command.To).Address);
        Assert.Empty(command.Cc);
    }

    /// <summary>
    /// Send chaser opens in the one send-e-mail dialogue over the Case
    /// (R1330): the composer is fetched into the Case's e-mail dialogue mount,
    /// and a send from it lands back on the Case with the dialogue reopening
    /// on the send's status.
    /// </summary>
    [Fact]
    public async Task SendChaserOpensInTheDialogueOverTheCaseAndReturnsThere()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var mailboxId = await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(baseFactory, send, chaserRecipients: new FixedChaserRecipients(ChaserRecipients));
        using var client = IntakeWebDriver.CreateClient(factory);

        var page = WebUtility.HtmlDecode(await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        Assert.Matches("<a[^>]*data-send-chaser data-mail-compose-open[^>]*>", page);
        Assert.Contains($"data-mail-chaser=\"/Cases/{caseId:D}/Chaser\"", page, StringComparison.OrdinalIgnoreCase);

        var dialogue = await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}/Chaser?handler=Dialog");
        Assert.Contains("data-dialog=\"mailMessageDialog\"", dialogue, StringComparison.Ordinal);
        Assert.Contains("class=\"dialog mail-compose-dialog dialog--send\"", dialogue, StringComparison.Ordinal);
        Assert.Equal("true", InputValue(dialogue, "Dialog"));
        Assert.Equal(
            "instructions@principal.example; images@repairer.example; office@repairer.example",
            Recipients(dialogue, "To"));

        using var response = await client.PostAsync(
            FormAction(dialogue, "Send"),
            new FormUrlEncodedContent(new Dictionary<string, string>(ChaserForm(dialogue, mailboxId))
            {
                ["Dialog"] = "true"
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var landing = response.Headers.Location!.ToString();
        Assert.Contains($"/Cases/{caseId:D}", landing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=chaser", landing, StringComparison.Ordinal);
        Assert.Contains("mailOperationId=", landing, StringComparison.Ordinal);
        Assert.Equal(StaffMailPurpose.CaseChaser, Assert.Single(send.Commands).Purpose);
    }

    private const string RejectionReason = "Outside the area we cover.";

    /// <summary>
    /// Close case's E-mail the Principal (operator, 27 November 2026; R1321 to
    /// R1325): the tick stands in the dialog for the rejection alone; ticked,
    /// the Case closes and the landing opens the Case's e-mail dialogue as a
    /// reply to the instruction e-mail, its Message the Case rejection
    /// template rendered for the Case with the reason just given. The same
    /// address pre-fills nothing while the Case is open, and a Case with an
    /// instruction e-mail has no new message.
    /// </summary>
    [Fact]
    public async Task ClosingAsRejectedWithTheTickOpensAReplyToTheInstructionOnTheRejectionTemplate()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var instruction = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        await MakeInstructionEmailAsync(baseFactory, caseId, instruction.ReceiptId);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);
        var composerUrl =
            $"/Inbox/{instruction.MessageId:D}?handler=Dialog&compose=reply&template=CaseRejection&returnCase={caseId:D}&returnSection=files";

        var page = WebUtility.HtmlDecode(await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        Assert.Matches(
            "<div data-close-email-principal=\"CollisionEngineersRejected\">\\s*<label class=\"check\"><input type=\"checkbox\" name=\"emailPrincipal\" value=\"true\" /> E-mail the Principal</label>",
            page);
        Assert.DoesNotContain("data-mail-new-message", page, StringComparison.Ordinal);
        Assert.Equal(
            string.Empty,
            TextAreaValue(await IntakeWebDriver.GetHtmlAsync(client, composerUrl), "CorrespondenceBody"));

        using var closed = await CloseCaseAsync(
            factory, client, caseId, CaseClosureOutcome.CollisionEngineersRejected, emailPrincipal: true);

        Assert.Equal(HttpStatusCode.Redirect, closed.StatusCode);
        var landing = closed.Headers.Location!.ToString();
        Assert.Contains($"/Cases/{caseId:D}", landing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"open={instruction.MessageId:D}", landing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=reply", landing, StringComparison.Ordinal);
        Assert.Contains("template=CaseRejection", landing, StringComparison.Ordinal);
        var header = await CaseHeaderAsync(factory, caseId);
        Assert.Equal(CaseLifecycleState.CollisionEngineersRejected, header.Workflow.State);

        var composer = await IntakeWebDriver.GetHtmlAsync(client, composerUrl);
        Assert.Equal(
            RejectionBody(factory, header),
            TextAreaValue(composer, "CorrespondenceBody").Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Equal("Re: Source subject", InputValue(composer, "CorrespondenceSubject"));
        Assert.Contains("reply@example.invalid", composer, StringComparison.Ordinal);

        using var newMessage = await client.GetAsync($"/Cases/{caseId:D}/NewMessage?handler=Dialog");
        Assert.Equal(HttpStatusCode.NotFound, newMessage.StatusCode);
        Assert.Equal(0, send.SendCalls);
    }

    /// <summary>
    /// A rejected Case with no instruction e-mail (operator, 27 November 2026;
    /// R1323, R1326, R1327): the landing names no message, and the Case's
    /// e-mail dialogue opens as a new message, To on the report's recipients,
    /// the subject the registration and the claimant, Message the Case
    /// rejection template. Its send is a new general message for the Case,
    /// and the dialogue reopens on the send's status. An open Case has none.
    /// </summary>
    [Fact]
    public async Task ClosingAsRejectedWithNoInstructionEmailOpensANewMessageInTheDialogue()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        var mailboxId = await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);
        var dialogueUrl = $"/Cases/{caseId:D}/NewMessage?handler=Dialog&returnCase={caseId:D}&returnSection=files";
        using (var open = await client.GetAsync(dialogueUrl))
        {
            Assert.Equal(HttpStatusCode.NotFound, open.StatusCode);
        }

        using var closed = await CloseCaseAsync(
            factory, client, caseId, CaseClosureOutcome.CollisionEngineersRejected, emailPrincipal: true);

        Assert.Equal(HttpStatusCode.Redirect, closed.StatusCode);
        var landing = closed.Headers.Location!.ToString();
        Assert.Contains($"/Cases/{caseId:D}", landing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=new", landing, StringComparison.Ordinal);
        Assert.DoesNotContain("open=", landing, StringComparison.Ordinal);
        Assert.DoesNotContain("template=", landing, StringComparison.Ordinal);
        var header = await CaseHeaderAsync(factory, caseId);
        Assert.Equal(CaseLifecycleState.CollisionEngineersRejected, header.Workflow.State);
        var page = WebUtility.HtmlDecode(await IntakeWebDriver.GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        Assert.Contains(
            $"data-mail-new-message=\"/Cases/{caseId:D}/NewMessage\"", page, StringComparison.OrdinalIgnoreCase);

        var dialogue = await IntakeWebDriver.GetHtmlAsync(client, dialogueUrl);
        // The one send-e-mail dialogue (R1330), under the e-mail dialogue's id.
        Assert.Contains("id=\"mailMessageDialog\"", dialogue, StringComparison.Ordinal);
        Assert.Contains("class=\"dialog mail-compose-dialog dialog--send\"", dialogue, StringComparison.Ordinal);
        Assert.Matches("<h2 id=\"mailMessageDialog-title\"[^>]*>New message</h2>", dialogue);
        Assert.Equal(await ReportRecipientsAsync(factory, caseId), Recipients(dialogue, "To"));
        Assert.Equal(
            CaseMailSubject.RegistrationAndClaimant(header.Summary.Registration, header.Summary.Claimant),
            InputValue(dialogue, "Subject"));
        Assert.Equal(
            RejectionBody(factory, header),
            TextAreaValue(dialogue, "Body").Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Contains($"<option value=\"{mailboxId:D}\"", dialogue, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, send.SendCalls);

        var form = ChaserForm(dialogue, mailboxId);
        form["To"] = "claims@principal.example";
        using var response = await client.PostAsync(FormAction(dialogue, "Send"), new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var sent = response.Headers.Location!.ToString();
        Assert.Contains($"/Cases/{caseId:D}", sent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=new", sent, StringComparison.Ordinal);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailPurpose.GeneralCorrespondence, command.Purpose);
        Assert.Equal(StaffMailComposeMode.New, command.ComposeMode);
        Assert.Null(command.OriginalMessage);
        Assert.Equal(mailboxId, command.ApprovedMailboxId);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(header.Workflow.Version, command.ExpectedContextVersion);
        Assert.Equal("claims@principal.example", Assert.Single(command.To).Address);
        Assert.Equal(RejectionBody(factory, header), command.Body.Replace("\r\n", "\n", StringComparison.Ordinal));

        var status = await IntakeWebDriver.GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}/NewMessage?handler=Dialog&mailOperationId={ExtractOperationId(response.Headers.Location):D}");
        Assert.Contains("data-mail-send-status", status, StringComparison.Ordinal);
    }

    /// <summary>
    /// The tick belongs to the rejection (R1322): left on a cancellation it
    /// changes nothing, and the closed Case has no rejection e-mail. Without
    /// staff mail the dialog draws no tick.
    /// </summary>
    [Fact]
    public async Task TheEmailTickOnAnotherOutcomeClosesWithNoEmail()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(baseFactory, new(false, true));
        await SeedSendableMailboxAsync(baseFactory);
        using (var offline = IntakeWebDriver.CreateClient(baseFactory))
        {
            Assert.DoesNotContain(
                "name=\"emailPrincipal\"",
                await IntakeWebDriver.GetHtmlAsync(offline, $"/Cases/{caseId:D}"),
                StringComparison.Ordinal);
        }
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var closed = await CloseCaseAsync(
            factory, client, caseId, CaseClosureOutcome.PrincipalCancelled, emailPrincipal: true);

        Assert.Equal(HttpStatusCode.Redirect, closed.StatusCode);
        var landing = closed.Headers.Location!.ToString();
        Assert.DoesNotContain("compose=", landing, StringComparison.Ordinal);
        Assert.DoesNotContain("open=", landing, StringComparison.Ordinal);
        Assert.Equal(
            CaseLifecycleState.PrincipalCancelled,
            (await CaseHeaderAsync(factory, caseId)).Workflow.State);
        using var newMessage = await client.GetAsync($"/Cases/{caseId:D}/NewMessage");
        Assert.Equal(HttpStatusCode.NotFound, newMessage.StatusCode);
        Assert.Equal(0, send.SendCalls);
    }

    /// <summary>Closes the Case as Actions → Close case posts it from the page in read mode.</summary>
    private static async Task<HttpResponseMessage> CloseCaseAsync(
        WebApplicationFactory<Program> factory,
        HttpClient client,
        Guid caseId,
        CaseClosureOutcome outcome,
        bool emailPrincipal)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["id"] = caseId.ToString("D"),
            ["expectedVersion"] = (await CaseVersionAsync(factory, caseId)).ToString(CultureInfo.InvariantCulture),
            ["operationKey"] = Guid.NewGuid().ToString("N"),
            ["reason"] = RejectionReason,
            ["outcome"] = outcome.ToString()
        };
        if (emailPrincipal)
        {
            form["emailPrincipal"] = "true";
        }
        return await client.PostAsync($"/Cases/{caseId:D}/Closure?handler=Close", new FormUrlEncodedContent(form));
    }

    /// <summary>The Case rejection's built-in body for <paramref name="header"/>, with the reason the close gave.</summary>
    private static string RejectionBody(WebApplicationFactory<Program> factory, CaseHeader header)
    {
        var facts = new CaseRejectionFacts(
            ReportDispatchPolicy.Greeting(factory.Services.GetRequiredService<TimeProvider>().GetUtcNow()),
            header.Summary.Registration,
            header.Summary.Claimant,
            header.Summary.Reference,
            header.Summary.Principal,
            RejectionReason);
        return EmailTemplates.Render(
            EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseRejection), facts.Values());
    }

    /// <summary>The To addresses the Case's report would be sent to, as the Send report dialog plans them.</summary>
    private static async Task<string> ReportRecipientsAsync(WebApplicationFactory<Program> factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var facts = await scope.ServiceProvider.GetRequiredService<IReportRecipientSuggestionQueries>()
            .GetAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.NotNull(facts);
        return string.Join(
            "; ",
            ReportDispatchPolicy.Plan(facts, scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow())
                .To.Select(recipient => recipient.Address));
    }

    private static Exception AmbiguousProviderAnswer(bool timedOut) => timedOut
        ? new TaskCanceledException("The request timed out.", new TimeoutException())
        : new HttpRequestException("Microsoft Graph returned 503.", null, HttpStatusCode.ServiceUnavailable);

    /// <summary>The chaser page's form as rendered, with the chosen From mailbox when one is posted.</summary>
    private static Dictionary<string, string> ChaserForm(string html, Guid? fromMailboxId)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["ExpectedContextVersion"] = InputValue(html, "ExpectedContextVersion"),
            ["To"] = Recipients(html, "To"),
            ["Cc"] = Recipients(html, "Cc"),
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

    /// <summary>Sets the Case's Principal's Send from to <paramref name="address"/>.</summary>
    private static async Task SetSendFromAsync(
        IntakeWebApplicationFactory factory, Guid caseId, string address)
    {
        // Send from is the Principal's own setting since 25 November 2026 (R1267).
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Principals SET SendFromMailbox = {address} WHERE Id = (SELECT PrincipalId FROM Cases WHERE Id = {caseId})");
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
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
            ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
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
        using var client = IntakeWebDriver.CreateClient(factory);
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
            ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
            ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
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

    /// <summary>
    /// A staff forward held in instructions@ is answered as though the
    /// original had arrived in the mailbox that forwarded it (operator, 3
    /// November 2026; R651, R652): Reply all's From starts on that mailbox,
    /// or on none when it cannot send, and it leaves the mailbox staff choose
    /// (R655), goes to the original sender and the
    /// original's own Cc, drops the forward's "FW:", and still names the
    /// message it answers. The forwarded header's To names the forwarding
    /// mailbox, which is copied neither when it sends nor when another
    /// mailbox does (R653).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReplyAllToAStaffForwardHeldInInstructionsAnswersTheOriginal(bool forwarderSends)
    {
        const string forwarder = "sc08-forwarder@collisionengineers.co.uk";
        var send = new RecordingStaffMailSend { NextState = StaffMailState.Sent };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 FORWARD", "SC08-FORWARD-REPLY-ALL");
        // When the forwarder cannot send, staff choose another Outgoing mailbox.
        var senderId = forwarderSends
            ? await SeedSendableMailboxAsync(baseFactory, "sc08-forwarder", forwarder)
            : await SeedSendableMailboxAsync(baseFactory);
        var seeded = await SeedRetainedCorrespondenceAsync(
            baseFactory,
            caseId,
            sender: forwarder,
            subject: "FW: Claim PK12TMZ",
            body:
                "Please see below.\n\n" +
                "From: Robin Anderson <randerson@qdosassist.co.uk>\n" +
                "Sent: 21 August 2026 11:18 PM\n" +
                $"To: Forwarder <{forwarder}>\n" +
                "Cc: Qdos NewClaims <NewClaims@qdosassist.co.uk>\n" +
                "Subject: Claim PK12TMZ\n\n" +
                "Please inspect the vehicle.",
            mailboxGraphId: InstructionsGraphId,
            mailboxAddress: InstructionsAddress);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var recordGet = await client.GetAsync($"/Inbox/{seeded.MessageId:D}");
        var record = await recordGet.Content.ReadAsStringAsync();
        var replyAll = Regex.Match(
            record,
            $"<a[^>]*href=\"(?<href>[^\"]*compose=reply-all[^\"]*)\"[^>]*>\\s*{Regex.Escape(OperatorLabels.StaffMail.ReplyAll)}\\s*</a>",
            RegexOptions.CultureInvariant);
        Assert.True(replyAll.Success, "The record's foot did not offer Reply all.");
        using var composerGet = await client.GetAsync(WebUtility.HtmlDecode(replyAll.Groups["href"].Value));
        var composer = await composerGet.Content.ReadAsStringAsync();
        Assert.Equal("Re: Claim PK12TMZ", InputValue(composer, "CorrespondenceSubject"));
        string[] startsOn = forwarderSends ? [senderId.ToString("D")] : [];
        Assert.Equal(startsOn, SelectedOptions(composer, "CorrespondenceFromMailboxId"));

        var form = RetainedReplyForm(
            composer, InputValue(composer, "CorrespondenceOperationKey"), "Reviewed response.");
        form["FixedCorrespondenceCaseReference"] = InputValue(composer, "FixedCorrespondenceCaseReference");
        form["CorrespondenceFromMailboxId"] = senderId.ToString("D");
        using var post = await client.PostAsync(
            FormAction(composer, "ReplyAll"),
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(senderId, command.ApprovedMailboxId);
        Assert.Equal(StaffMailComposeMode.ReplyAll, command.ComposeMode);
        Assert.Equal(seeded.MessageId, command.OriginalMessage!.RetainedMessageId);
        Assert.Equal(["randerson@qdosassist.co.uk"], command.To.Select(item => item.Address));
        Assert.Equal(["NewClaims@qdosassist.co.uk"], command.Cc.Select(item => item.Address));
    }

    /// <summary>
    /// Only instructions@ answers a staff forward as though the original had
    /// arrived directly (operator, 3 November 2026; R652): a staff forward
    /// held in desk@ is answered by default from desk@, in its thread, to the
    /// message's own Reply-To, with its subject as it came, even when the
    /// mailbox that forwarded it can send.
    /// </summary>
    [Fact]
    public async Task ReplyToAStaffForwardHeldOutsideInstructionsLeavesTheMailboxHoldingIt()
    {
        const string forwarder = "sc08-desk-forwarder@collisionengineers.co.uk";
        const string desk = "desk@collisionengineers.co.uk";
        var send = new RecordingStaffMailSend { NextState = StaffMailState.Sent };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 DESK FORWARD", "SC08-FORWARD-DESK-REPLY");
        await SeedSendableMailboxAsync(baseFactory, "sc08-desk-forwarder", forwarder);
        var seeded = await SeedRetainedCorrespondenceAsync(
            baseFactory,
            caseId,
            sender: forwarder,
            subject: "FW: Claim PK12TMZ",
            body:
                "Please see below.\n\n" +
                "From: Robin Anderson <randerson@qdosassist.co.uk>\n" +
                "Sent: 21 August 2026 11:18 PM\n" +
                $"To: Desk <{desk}>\n" +
                "Subject: Claim PK12TMZ\n\n" +
                "Please inspect the vehicle.",
            mailboxGraphId: "sc08-desk",
            mailboxAddress: desk);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        Assert.Equal([seeded.MailboxId.ToString("D")], SelectedOptions(html, "CorrespondenceFromMailboxId"));
        Assert.Equal("Re: FW: Claim PK12TMZ", InputValue(html, "CorrespondenceSubject"));
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
            ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
            ["CorrespondenceSubject"] = "Re: FW: Claim PK12TMZ",
            ["CorrespondenceBody"] = "Reviewed response."
        };
        using var post = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(seeded.MailboxId, command.ApprovedMailboxId);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.Equal(seeded.MessageId, command.OriginalMessage!.RetainedMessageId);
        Assert.Equal([forwarder], command.To.Select(item => item.Address));
        Assert.Empty(command.Cc);
    }

    /// <summary>
    /// From is a default, not a rule (operator, 4 November 2026; R655): Reply
    /// starts on the mailbox holding the message, offers every send-ready
    /// mailbox, and leaves the one staff choose, still answering the message
    /// held in the other mailbox.
    /// </summary>
    [Fact]
    public async Task AReplyStartsOnTheMailboxHoldingTheMessageAndLeavesTheOneStaffChoose()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 CHOSEN FROM", "SC08-CHOSEN-FROM-REPLY");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        var otherId = await SeedSendableMailboxAsync(
            baseFactory, "sc08-other-sender", "sc08-other-sender@collisionengineers.co.uk");
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        Assert.Equal([seeded.MailboxId.ToString("D")], SelectedOptions(html, "CorrespondenceFromMailboxId"));
        Assert.Contains($"<option value=\"{otherId:D}\"", html, StringComparison.OrdinalIgnoreCase);
        var form = RetainedReplyForm(html, InputValue(html, "CorrespondenceOperationKey"), "Reviewed response.");
        form["CorrespondenceFromMailboxId"] = otherId.ToString("D");
        using var post = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(otherId, command.ApprovedMailboxId);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.Equal(seeded.MessageId, command.OriginalMessage!.RetainedMessageId);
        Assert.Equal(seeded.MailboxId, command.OriginalMessage.ApprovedMailboxId);
        Assert.Equal(["reply@example.invalid"], command.To.Select(item => item.Address));
    }

    /// <summary>
    /// Outlook's text/plain forward writes each recipient as
    /// "Name &lt;addr&lt;mailto:addr&gt;&gt;". Reply all reads the plain
    /// address from that form, never "mailto:addr", and still leaves the
    /// mailbox that forwarded it.
    /// </summary>
    [Fact]
    public async Task ReplyAllToAStaffForwardReadsOutlooksMailtoRecipientForm()
    {
        const string forwarder = "sc08-mailto-forwarder@collisionengineers.co.uk";
        const string engineers = "engineers@collisionengineers.co.uk";
        var send = new RecordingStaffMailSend { NextState = StaffMailState.Sent };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MAILTO", "SC08-FORWARD-MAILTO-REPLY-ALL");
        var forwarderId = await SeedSendableMailboxAsync(baseFactory, "sc08-mailto-forwarder", forwarder);
        var seeded = await SeedRetainedCorrespondenceAsync(
            baseFactory,
            caseId,
            sender: forwarder,
            subject: "FW: Claim PK12TMZ",
            body:
                "Please see below.\n\n" +
                "From: Robin Anderson <randerson@qdosassist.co.uk<mailto:randerson@qdosassist.co.uk>>\n" +
                "Sent: 21 August 2026 11:18 PM\n" +
                $"To: Engineers <{engineers}<mailto:{engineers}>>; Forwarder <{forwarder}<mailto:{forwarder}>>\n" +
                "Cc: Qdos NewClaims <NewClaims@qdosassist.co.uk<mailto:NewClaims@qdosassist.co.uk>>\n" +
                "Subject: Claim PK12TMZ\n\n" +
                "Please inspect the vehicle.",
            mailboxGraphId: InstructionsGraphId,
            mailboxAddress: InstructionsAddress);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var recordGet = await client.GetAsync($"/Inbox/{seeded.MessageId:D}");
        var record = await recordGet.Content.ReadAsStringAsync();
        var replyAll = Regex.Match(
            record,
            $"<a[^>]*href=\"(?<href>[^\"]*compose=reply-all[^\"]*)\"[^>]*>\\s*{Regex.Escape(OperatorLabels.StaffMail.ReplyAll)}\\s*</a>",
            RegexOptions.CultureInvariant);
        Assert.True(replyAll.Success, "The record's foot did not offer Reply all.");
        using var composerGet = await client.GetAsync(WebUtility.HtmlDecode(replyAll.Groups["href"].Value));
        var composer = await composerGet.Content.ReadAsStringAsync();

        var form = RetainedReplyForm(
            composer, InputValue(composer, "CorrespondenceOperationKey"), "Reviewed response.");
        form["FixedCorrespondenceCaseReference"] = InputValue(composer, "FixedCorrespondenceCaseReference");
        using var post = await client.PostAsync(
            FormAction(composer, "ReplyAll"),
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(forwarderId, command.ApprovedMailboxId);
        Assert.Equal(StaffMailComposeMode.ReplyAll, command.ComposeMode);
        Assert.Equal(["randerson@qdosassist.co.uk", engineers], command.To.Select(item => item.Address));
        Assert.Equal(["NewClaims@qdosassist.co.uk"], command.Cc.Select(item => item.Address));
        Assert.DoesNotContain(
            command.To.Concat(command.Cc),
            item => string.Equals(item.Address, forwarder, StringComparison.OrdinalIgnoreCase)
                || item.Address.Contains("mailto:", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reply all from the record (v41 I15, I17): the record's foot offers Reply
    /// all, following it opens the composer for the exact message and Case, its
    /// Send posts ReplyAll, and the record the send lands on reads the sent
    /// state back.
    /// </summary>
    [Fact]
    public async Task ReplyAllSendsFromTheRecord()
    {
        var send = new RecordingStaffMailSend { NextState = StaffMailState.Sent };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 RECORD", "SC08-RECORD-REPLY-ALL");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        var resolver = new StableAttachmentResolver();
        using var factory = Configure(baseFactory, send, attachmentResolver: resolver);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var recordGet = await client.GetAsync($"/Inbox/{seeded.MessageId:D}");
        Assert.Equal(HttpStatusCode.OK, recordGet.StatusCode);
        var record = await recordGet.Content.ReadAsStringAsync();
        var replyAll = Regex.Match(
            record,
            $"<a[^>]*href=\"(?<href>[^\"]*compose=reply-all[^\"]*)\"[^>]*>\\s*{Regex.Escape(OperatorLabels.StaffMail.ReplyAll)}\\s*</a>",
            RegexOptions.CultureInvariant);
        Assert.True(replyAll.Success, "The record's foot did not offer Reply all.");
        Assert.Equal(0, send.SendCalls);

        using var composerGet = await client.GetAsync(WebUtility.HtmlDecode(replyAll.Groups["href"].Value));
        Assert.Equal(HttpStatusCode.OK, composerGet.StatusCode);
        var composer = await composerGet.Content.ReadAsStringAsync();
        Assert.Equal("reply-all", InputValue(composer, "compose"));
        Assert.Equal("Re: Source subject", InputValue(composer, "CorrespondenceSubject"));
        Assert.Equal(0, send.SendCalls);
        var listsBeforeSend = resolver.ListCalls;
        Assert.True(listsBeforeSend > 0, "The composer listed the Case's files.");

        var form = RetainedReplyForm(
            composer, InputValue(composer, "CorrespondenceOperationKey"), "Reviewed response.");
        form["FixedCorrespondenceCaseReference"] = InputValue(composer, "FixedCorrespondenceCaseReference");
        using var post = await client.PostAsync(
            FormAction(composer, "ReplyAll"),
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, post.StatusCode);
        // With nothing selected the send reads none of the Case's files.
        Assert.Equal(listsBeforeSend, resolver.ListCalls);
        Assert.Equal(0, resolver.SelectCalls);
        Assert.Equal(0, resolver.ResolveCalls);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailComposeMode.ReplyAll, command.ComposeMode);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(await CaseVersionAsync(factory, caseId), command.ExpectedContextVersion);
        Assert.Equal(seeded.MessageId, command.OriginalMessage!.RetainedMessageId);
        Assert.Equal(
            ["reply@example.invalid", "other-to@example.invalid"],
            command.To.Select(item => item.Address));
        Assert.Equal(["copy@example.invalid"], command.Cc.Select(item => item.Address));

        using var landed = await client.GetAsync(post.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, landed.StatusCode);
        var landedHtml = await landed.Content.ReadAsStringAsync();
        Assert.Contains("Send status", landedHtml, StringComparison.Ordinal);
        Assert.Contains(
            $"<dd>{OperatorLabels.StaffMail.State(StaffMailState.Sent)}</dd>",
            landedHtml,
            StringComparison.Ordinal);
        Assert.Equal(1, send.SendCalls);
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
        var resolver = new StableAttachmentResolver();
        using var factory = Configure(
            baseFactory,
            send,
            attachmentResolver: resolver);
        using var client = IntakeWebDriver.CreateClient(factory);
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
            ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
            ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
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
        // The send read the Case's files once, for the options and the selection together.
        Assert.Equal(1, resolver.SelectCalls);
        Assert.Equal(0, resolver.ResolveCalls);
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
        using var client = IntakeWebDriver.CreateClient(factory);
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
    /// The Inbox's dialog composes inside itself (v41 I15): its Case / PO picker
    /// only fills the field, so choosing an option never posts
    /// SelectCorrespondenceCase and leaves the dialog for the record page, and
    /// the send lands back on the Inbox with the message open.
    /// </summary>
    [Fact]
    public async Task TheDialogComposerFillsTheCaseReferenceAndSendsBackToTheInbox()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var selectedCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 DIALOG", "SC08-MSG-DIALOG");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId: null);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Dialog&compose=reply");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.Matches(
            "<div class=\"field case-picker\" data-case-picker data-case-picker-fill\\s+data-case-picker-url=\"[^\"]*handler=CorrespondenceCaseOptions",
            html);
        var action = FormAction(html, "Reply");
        Assert.Contains("dialog=true", action, StringComparison.Ordinal);

        var form = RetainedReplyForm(
            html,
            InputValue(html, "CorrespondenceOperationKey"),
            "Reply from the dialog.");
        form["CorrespondenceCaseReference"] = await CaseReferenceAsync(factory, selectedCaseId);
        using var sent = await client.PostAsync(action, new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
        var location = sent.Headers.Location!.OriginalString;
        Assert.StartsWith("/Inbox?", location, StringComparison.Ordinal);
        Assert.Contains($"open={seeded.MessageId:D}", location, StringComparison.Ordinal);
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
        using var client = IntakeWebDriver.CreateClient(factory);

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
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
            ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
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
    /// A reply whose provider answer is ambiguous (a Graph server error or a
    /// timeout, which the engine records Unknown and throws) keeps staff on
    /// the message with "The send result is not yet known." and its Unknown
    /// send status, never the error page (R1069). The composer keeps the
    /// operation key, so sending it again replays the Unknown send.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUnknownProviderOutcomeKeepsStaffOnTheMessageWithItsStatus(bool timedOut)
    {
        var send = new RecordingStaffMailSend
        {
            NextState = StaffMailState.Unknown,
            ProviderFault = AmbiguousProviderAnswer(timedOut)
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MSG", "SC08-MSG-AMBIGUOUS");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var key = InputValue(html, "CorrespondenceOperationKey");

        using var post = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(RetainedReplyForm(html, key, "Reviewed response.")));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var redrawn = await post.Content.ReadAsStringAsync();
        var page = WebUtility.HtmlDecode(redrawn);
        Assert.Contains(CaseWorkspaceLabels.ReportDelivery.SendUnknown, page, StringComparison.Ordinal);
        Assert.Contains("Send status", page, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.StaffMail.State(StaffMailState.Unknown), page, StringComparison.Ordinal);
        Assert.Equal(key, InputValue(redrawn, "CorrespondenceOperationKey"));
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
        using var client = IntakeWebDriver.CreateClient(factory);
        var typedReference = await CaseReferenceAsync(factory, typedCaseId);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        Assert.Contains("role=\"combobox\"", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
                ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
                ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
                ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
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
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
            ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
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
        using var client = IntakeWebDriver.CreateClient(factory);
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
        using var client = IntakeWebDriver.CreateClient(factory);

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
        using var client = IntakeWebDriver.CreateClient(factory);

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
        using var client = IntakeWebDriver.CreateClient(factory);
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
        using var client = IntakeWebDriver.CreateClient(factory);
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
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("compose=reply", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compose=forward", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name=\"CorrespondenceOperationKey\"", html, StringComparison.OrdinalIgnoreCase);
        // The dialog, opened from the Case or the Inbox, offers what the record offers.
        using var content = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Dialog&returnCase={caseId:D}&returnSection=files");
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

    /// <summary>
    /// R1278: the Case page opens the Inbox's own dialog, whose Reply, Reply
    /// all and Forward carry the Case back and whose composer has the Case
    /// chosen. A Case that is not the message's own is not carried.
    /// </summary>
    [Fact]
    public async Task TheDialogOpenedFromTheCaseOffersReplyAndForwardThatReturnToIt()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var associatedCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 ASSOCIATED", "SC08-MSG-DIALOG-ASSOCIATED");
        var otherCaseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 DIALOG", "SC08-MSG-DIALOG");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, associatedCaseId);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);
        var associatedReference = await CaseReferenceAsync(factory, associatedCaseId);

        var fragment = WebUtility.HtmlDecode(await IntakeWebDriver.GetHtmlAsync(
            client, $"/Inbox/{seeded.MessageId:D}?handler=Dialog&returnCase={associatedCaseId:D}&returnSection=files"));
        foreach (var (mode, label) in new[] { ("reply", "Reply"), ("reply-all", "Reply all"), ("forward", "Forward") })
        {
            var link = Regex.Match(
                fragment,
                $"<a[^>]*href=\"/Inbox/{seeded.MessageId:D}\\?[^\"]*compose={Regex.Escape(mode)}(&[^\"]*)?\"[^>]*>[\\s\\S]*?</a>",
                RegexOptions.IgnoreCase);
            Assert.True(link.Success, $"The dialog offers no {label}.");
            Assert.Contains($"returnCase={associatedCaseId:D}", link.Value, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("returnSection=files", link.Value, StringComparison.Ordinal);
            Assert.Contains(label, link.Value, StringComparison.Ordinal);
        }

        // Reply opens the composer in the dialog with the Case chosen, and its
        // send carries the Case back.
        var composer = await IntakeWebDriver.GetHtmlAsync(
            client,
            $"/Inbox/{seeded.MessageId:D}?handler=Dialog&compose=reply&returnCase={associatedCaseId:D}&returnSection=files");
        Assert.Equal(associatedReference, InputValue(composer, "CorrespondenceCaseReference"));
        Assert.Equal(associatedCaseId.ToString("D"), InputValue(composer, "returnCase"), ignoreCase: true);
        Assert.Equal("files", InputValue(composer, "returnSection"));
        Assert.Contains("reply@example.invalid", composer, StringComparison.Ordinal);

        // Another Case is not the message's: nothing carries it.
        var elsewhere = await IntakeWebDriver.GetHtmlAsync(
            client, $"/Inbox/{seeded.MessageId:D}?handler=Dialog&returnCase={otherCaseId:D}&returnSection=files");
        Assert.DoesNotContain("returnCase=", elsewhere, StringComparison.OrdinalIgnoreCase);
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
        using var client = IntakeWebDriver.CreateClient(factory);
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

    /// <summary>
    /// A refused send redraws from what the send already read: with nothing
    /// selected the reload lists the Case's files once, and the mailbox and
    /// the message's latest operation are each read once for the request.
    /// </summary>
    [Fact]
    public async Task ARefusedSendWithNoSelectionReadsTheMailboxAndOperationOnce()
    {
        var send = new RecordingStaffMailSend();
        var counts = new ReadCounts();
        var resolver = new StableAttachmentResolver();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var caseId = await SeedSupportedCaseAsync(baseFactory, null!, string.Empty, string.Empty);
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        using var factory = CountReads(Configure(baseFactory, send, attachmentResolver: resolver), counts);
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=forward");
        var html = await get.Content.ReadAsStringAsync();
        var lists = resolver.ListCalls;
        var mailboxLists = counts.MailboxLists;
        var operations = send.LatestForOriginalCalls;

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Forward",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
                ["CorrespondenceTo"] = string.Empty,
                ["CorrespondenceSubject"] = "Fwd: Source subject",
                ["CorrespondenceBody"] = "Forwarded material."
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(send.Commands);
        Assert.Equal(1, resolver.ListCalls - lists);
        Assert.Equal(0, resolver.SelectCalls);
        Assert.Equal(0, resolver.ResolveCalls);
        Assert.Equal(1, counts.MailboxLists - mailboxLists);
        Assert.Equal(1, send.LatestForOriginalCalls - operations);
    }

    /// <summary>
    /// With files selected and a Case typed other than the message's own, a
    /// refused send reads the files once (options and selection together),
    /// resolves the Case once, and reads the mailbox and the latest operation
    /// once, so its reload repeats none of them.
    /// </summary>
    [Fact]
    public async Task ARefusedSendWithASelectionAndATypedCaseReadsEachThingOnce()
    {
        var send = new RecordingStaffMailSend();
        var counts = new ReadCounts();
        var resolver = new StableAttachmentResolver();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var associatedCaseId = await SeedSupportedCaseAsync(baseFactory, null!, string.Empty, string.Empty);
        var selectedCaseId = await SeedSupportedCaseAsync(baseFactory, null!, string.Empty, string.Empty);
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, associatedCaseId);
        using var factory = CountReads(Configure(baseFactory, send, attachmentResolver: resolver), counts);
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=forward");
        var html = await get.Content.ReadAsStringAsync();
        var selectedReference = await CaseReferenceAsync(factory, selectedCaseId);
        var lists = resolver.ListCalls;
        var searches = counts.CaseSearches;
        var mailboxLists = counts.MailboxLists;
        var operations = send.LatestForOriginalCalls;

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Forward",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
                ["CorrespondenceCaseReference"] = selectedReference,
                ["CorrespondenceTo"] = string.Empty,
                ["CorrespondenceSubject"] = "Fwd: Source subject",
                ["CorrespondenceBody"] = "Forwarded material.",
                ["SelectedAttachments"] = StableAttachmentResolver.Selection
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(send.Commands);
        Assert.Equal(1, resolver.SelectCalls);
        Assert.Equal(0, resolver.ResolveCalls);
        Assert.Equal(0, resolver.ListCalls - lists);
        Assert.Equal(1, counts.CaseSearches - searches);
        Assert.Equal(1, counts.MailboxLists - mailboxLists);
        Assert.Equal(1, send.LatestForOriginalCalls - operations);
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
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
            ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
            ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
            ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
            ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
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
    /// Mark query done (operator, 27 October 2026): the composer offers the
    /// tick only for a reply or reply all to a message that holds an open
    /// query, and ticks it when reached from Answer query
    /// (<c>markQueryDone=true</c>). A forward, a message with no query, or a
    /// Case whose open query is on another message, offers none. Delete case
    /// correspondence is offered exactly where Mark query done is (R904), and
    /// unticked on every open, Answer query's included.
    /// </summary>
    [Theory]
    [InlineData("reply", "reply", true, false, true)]
    [InlineData("reply-all", "reply-all", true, false, true)]
    [InlineData("forward", "forward", true, false, false)]
    [InlineData("no-query", "reply", false, false, false)]
    [InlineData("from-answer", "reply", true, true, true)]
    public async Task TheReplyComposerOffersMarkQueryDoneOnlyForAReplyToTheMessagesOpenQuery(
        string name, string mode, bool queryOnThisMessage, bool fromAnswerQuery, bool offered)
    {
        Assert.NotEmpty(name);
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 QUERY", "SC08-MSG-QUERY");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        if (queryOnThisMessage)
        {
            await OpenQueryOnMessageAsync(baseFactory, caseId, seeded.ReceiptId);
        }
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?compose={mode}" + (fromAnswerQuery ? "&markQueryDone=true" : string.Empty));
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();

        // The cog menu offers its own Mark query done on an open query, so the
        // words are looked for in the composer alone.
        var composer = html[html.IndexOf("id=\"mailComposerForm\"", StringComparison.Ordinal)..];
        composer = composer[..composer.IndexOf("</form>", StringComparison.Ordinal)];
        var tick = Regex.Match(composer, "<input[^>]*data-mark-query-done[^>]*>", RegexOptions.CultureInvariant);
        Assert.Equal(offered, tick.Success);
        Assert.Equal(offered, composer.Contains(OperatorLabels.StaffMail.MarkQueryDone, StringComparison.Ordinal));
        var deleteTick = Regex.Match(
            composer, "<input[^>]*data-delete-case-correspondence[^>]*>", RegexOptions.CultureInvariant);
        Assert.Equal(offered, deleteTick.Success);
        Assert.Equal(offered, composer.Contains(OperatorLabels.StaffMail.DeleteCaseCorrespondence, StringComparison.Ordinal));
        if (offered)
        {
            Assert.Contains("type=\"checkbox\"", tick.Value, StringComparison.Ordinal);
            Assert.Equal(fromAnswerQuery, tick.Value.Contains("checked", StringComparison.Ordinal));
            Assert.Contains("type=\"checkbox\"", deleteTick.Value, StringComparison.Ordinal);
            Assert.DoesNotContain("checked", deleteTick.Value, StringComparison.Ordinal);
        }
        Assert.Equal(0, send.SendCalls);
    }

    /// <summary>An open logged query on the Case does not offer the tick on a message that holds none.</summary>
    [Fact]
    public async Task AReplyDoesNotOfferMarkQueryDoneWhenOnlyALoggedQueryIsOpen()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 OTHER QUERY", "SC08-MSG-OTHER-QUERY");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        await OpenStaffQueryAsync(baseFactory, caseId);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();

        Assert.DoesNotContain("data-mark-query-done", html, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.StaffMail.MarkQueryDone, html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-delete-case-correspondence", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reply carries the tick to the send command, which marks that
    /// message's query done once the Sent item is observed; an unticked reply
    /// marks nothing done.
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
        await OpenQueryOnMessageAsync(baseFactory, caseId, seeded.ReceiptId);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = RetainedReplyForm(html, InputValue(html, "CorrespondenceOperationKey"), "Answered the query.");
        if (ticked)
        {
            form["MarkQueryDone"] = "true";
        }

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.Equal(ticked, command.MarksQueryDone);
    }

    /// <summary>
    /// Delete case correspondence (R902 to R905): a ticked reply to the
    /// message's open query carries the tick to the send command, an unticked
    /// one does not, and a tick posted on a reply to a message that holds no
    /// open query is ignored, as the composer never offered it.
    /// </summary>
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task AReplyCarriesDeleteCaseCorrespondenceOnlyWhereTheComposerOffersIt(
        bool ticked, bool queryOnThisMessage, bool carried)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 DELETE CORRESPONDENCE", "SC08-MSG-DELETE-CORRESPONDENCE");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        if (queryOnThisMessage)
        {
            await OpenQueryOnMessageAsync(baseFactory, caseId, seeded.ReceiptId);
        }
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);
        using var get = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var html = await get.Content.ReadAsStringAsync();
        var form = RetainedReplyForm(html, InputValue(html, "CorrespondenceOperationKey"), "Answered the query.");
        if (ticked)
        {
            form["DeleteCaseCorrespondence"] = "true";
        }

        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal(StaffMailComposeMode.Reply, command.ComposeMode);
        Assert.Equal(carried, command.TidiesCaseCorrespondence);
        Assert.False(command.MarksQueryDone);
    }

    /// <summary>
    /// A composer opened on an AI draft (FRD-27, FRD-21) holds the draft as
    /// its Message, says so above it with the AI word, starts Mark query done
    /// ticked, and carries the job to the send command; a send that
    /// leaves the job as it was says why beside its send status (operator,
    /// 16 October 2026). A job that is not this message's Draft ready reply
    /// prefills nothing and is forgotten.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheComposerOpenedOnAnAiDraftPrefillsTheMessageAndCarriesTheJobToTheSend(bool forThisMessage)
    {
        var send = new RecordingStaffMailSend { NextAiJobRefusal = AiJobConfirmRefusal.Cancelled };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 AI DRAFT", "SC08-MSG-AI-DRAFT");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        await OpenQueryOnMessageAsync(baseFactory, caseId, seeded.ReceiptId);
        const string draftText = "Dear Ms Patel, thank you for your e-mail of 13 October.";
        var job = await SeedDraftReplyJobAsync(
            baseFactory, caseId, forThisMessage ? seeded.MessageId : Guid.NewGuid(), draftText);
        using var factory = Configure(baseFactory, send);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var get = await client.GetAsync(
            $"/Inbox/{seeded.MessageId:D}?compose=reply&markQueryDone=true&aiJob={job.JobId:D}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var html = await get.Content.ReadAsStringAsync();

        Assert.Equal(forThisMessage, html.Contains($"data-ai-draft=\"{job.JobId:D}\"", StringComparison.Ordinal));
        Assert.Equal(forThisMessage, html.Contains("thank you for your e-mail of 13 October", StringComparison.Ordinal));
        Assert.Equal(forThisMessage, html.Contains("Edit before sending.", StringComparison.Ordinal));
        var hiddenJob = InputValue(html, "aiJob");
        Assert.Equal(forThisMessage ? job.JobId.ToString("D") : string.Empty, hiddenJob);
        var tick = Regex.Match(html, "<input[^>]*data-mark-query-done[^>]*>", RegexOptions.CultureInvariant);
        Assert.True(tick.Success);
        Assert.Contains("checked", tick.Value, StringComparison.Ordinal);

        var form = RetainedReplyForm(html, InputValue(html, "CorrespondenceOperationKey"), "Edited reply.");
        form["aiJob"] = hiddenJob;
        form["MarkQueryDone"] = "true";
        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var command = Assert.Single(send.Commands);
        Assert.Equal(caseId, command.ContextId);
        Assert.True(command.MarksQueryDone);
        Assert.Equal(forThisMessage ? job.JobId : (Guid?)null, command.AiJobId);

        using var status = await client.GetAsync(response.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Equal(forThisMessage, statusHtml.Contains(
            OperatorLabels.QueryResponseJobs.NotCompleted(AiJobConfirmRefusal.Cancelled), StringComparison.Ordinal));
    }

    /// <summary>A Draft ready Query response job for the message on the Case, as the Automation Actor writes one.</summary>
    private static async Task<AiJobRecord> SeedDraftReplyJobAsync(
        IntakeWebApplicationFactory factory, Guid caseId, Guid retainedMessageId, string text)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IAiJobStore>();
        // The lease is read against the host's clock, which the test host fixes.
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();
        var client = ActionActor.Automation("draft-client");
        var queued = await store.CreateAsync(
            new NewAiJob(
                AiJobKind.QueryResponse, AiJobSubjectKind.Case, caseId, "SC08AI",
                retainedMessageId.ToString("D"), null, null,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                $"query-response:{Guid.NewGuid():N}", AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await store.TransitionAsync(
            new AiJobTransition(
                queued.JobId, queued.Version, AiJobState.Taken, client, $"mcp:{Guid.NewGuid():N}",
                LeaseExpiresAtUtc: now + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        return await store.TransitionAsync(
            new AiJobTransition(
                taken.JobId, taken.Version, AiJobState.DraftReady, client, $"mcp:{Guid.NewGuid():N}",
                Result: new AiJobResult(AiJobResultKind.DraftReply, retainedMessageId.ToString("D"), text)),
            CancellationToken.None);
    }

    /// <summary>The Case holds an open logged query that no message owns.</summary>
    private static async Task OpenStaffQueryAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        context.CaseQueries.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Source = nameof(CaseQuerySource.Staff),
            Channel = nameof(CaseQueryChannel.Phone),
            Details = "Rang about the report.",
            RaisedAtUtc = NowUtc,
            RaisedByKind = nameof(ActorKind.Staff),
            RaisedBySubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
            RaisedOperationKey = $"fixture-staff-query:{Guid.NewGuid():N}"
        });
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.HasOpenQuery = true;
        await context.SaveChangesAsync();
    }

    /// <summary>The message holds an open query, as the query ledger would have opened it.</summary>
    private static async Task OpenQueryOnMessageAsync(
        IntakeWebApplicationFactory factory, Guid caseId, Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        context.CaseQueries.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Source = nameof(CaseQuerySource.Mail),
            IntakeReceiptId = receiptId,
            RaisedAtUtc = NowUtc,
            RaisedByKind = nameof(ActorKind.Staff),
            RaisedBySubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
            RaisedOperationKey = $"fixture-query:{Guid.NewGuid():N}"
        });
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.HasOpenQuery = true;
        await context.SaveChangesAsync();
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

    /// <summary>
    /// Every address the composer drew in the boxes named <paramref name="name"/>,
    /// as one field: a box holding several addresses is read as all of them (R1329).
    /// </summary>
    private static string Recipients(string html, string name) =>
        string.Join(
            "; ",
            Regex.Matches(
                    html,
                    $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Select(tag => Regex.Match(tag.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase))
                .Where(value => value.Success)
                .Select(value => WebUtility.HtmlDecode(value.Groups["value"].Value))
                .Where(value => value.Length > 0));

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

    /// <summary>The option values the select named <paramref name="name"/> draws selected.</summary>
    private static string[] SelectedOptions(string html, string name)
    {
        var select = Regex.Match(
            html,
            $"<select[^>]*name=\"{Regex.Escape(name)}\"[^>]*>(?<options>.*?)</select>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(select.Success, $"No {name} select was drawn.");
        return
        [
            .. Regex.Matches(
                    select.Groups["options"].Value,
                    "<option value=\"(?<id>[^\"]+)\"[^>]*\\bselected\\b[^>]*>",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Select(match => match.Groups["id"].Value)
        ];
    }

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
        ["CorrespondenceTo"] = Recipients(html, "CorrespondenceTo"),
        ["CorrespondenceCc"] = Recipients(html, "CorrespondenceCc"),
        ["CorrespondenceCaseReference"] = InputValue(html, "CorrespondenceCaseReference"),
        ["CorrespondenceSubject"] = "Re: Source subject",
        ["CorrespondenceBody"] = body
    };

    /// <summary>How many times a request read the approved mailboxes and searched Cases.</summary>
    private sealed class ReadCounts
    {
        public int MailboxLists { get; set; }

        public int CaseSearches { get; set; }
    }

    private static WebApplicationFactory<Program> CountReads(
        WebApplicationFactory<Program> factory,
        ReadCounts counts) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddScoped<IApprovedMailboxStore>(provider => new CountingApprovedMailboxStore(
                    provider.GetRequiredService<EfApprovedMailboxStore>(), counts));
                services.RemoveAll<ISearchCases>();
                services.AddScoped<ISearchCases>(provider => new CountingCaseSearch(
                    ActivatorUtilities.CreateInstance<SearchCases>(provider), counts));
            }));

    private sealed class CountingApprovedMailboxStore(IApprovedMailboxStore inner, ReadCounts counts)
        : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken)
        {
            counts.MailboxLists++;
            return inner.ListAsync(cancellationToken);
        }

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            inner.UpdateAsync(request, cancellationToken);

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) =>
            inner.IsApprovedAsync(mailboxAddress, routeScope, cancellationToken);
    }

    private sealed class CountingCaseSearch(ISearchCases inner, ReadCounts counts) : ISearchCases
    {
        public Task<SearchCasesResult> ExecuteAsync(
            SearchCasesQuery query, CancellationToken cancellationToken)
        {
            counts.CaseSearches++;
            return inner.ExecuteAsync(query, cancellationToken);
        }
    }

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

    private static Task<Guid> SeedSupportedCaseAsync(
        WebApplicationFactory<Program> factory,
        HttpClient _client,
        string _registration,
        string _claimNumber) =>
        AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);

    /// <summary>An Outgoing mailbox that can send, with no retained mail; its id.</summary>
    private static async Task<Guid> SeedSendableMailboxAsync(
        IntakeWebApplicationFactory factory,
        string graphId = "sc08-sender",
        string address = "sc08-sender@collisionengineers.co.uk")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var mailboxId = await TestMailboxId.EnsureApprovedAsync(
            context, graphId, address, NowUtc.AddDays(-1));
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
        Guid? caseId,
        string sender = "sender@example.invalid",
        string subject = "Source subject",
        string body = "Source body.",
        string mailboxGraphId = "sc08-sender",
        string mailboxAddress = "sc08-sender@collisionengineers.co.uk")
    {
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

        // A staff forward's Reply-To is the forwarder itself.
        IReadOnlyList<string> replyTo = sender == "sender@example.invalid" ? ["reply@example.invalid"] : [sender];
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
                    [],
                    sender,
                    "Sender Name",
                    [mailboxAddress, "other-to@example.invalid"],
                    ["copy@example.invalid", mailboxAddress, "other-to@example.invalid"],
                    replyTo,
                    subject,
                    body,
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

        /// <summary>How many times each kind of Case document read was made.</summary>
        public int ListCalls { get; private set; }

        public int SelectCalls { get; private set; }

        public int ResolveCalls { get; private set; }

        public Task<IReadOnlyList<StaffMailAttachmentOption>> ListCaseAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult(Options());
        }

        public Task<IReadOnlyList<StaffMailAttachment>> ResolveCaseAsync(
            ActionActor actor, Guid caseId, IReadOnlyList<string> selections,
            CancellationToken cancellationToken)
        {
            ResolveCalls++;
            return Task.FromResult(Resolve(selections));
        }

        public Task<StaffMailCaseSelection> SelectCaseAsync(
            ActionActor actor, Guid caseId, IReadOnlyList<string> selections,
            CancellationToken cancellationToken)
        {
            SelectCalls++;
            try
            {
                return Task.FromResult(new StaffMailCaseSelection(Options(), Resolve(selections), null));
            }
            catch (StaffMailAttachmentSelectionException exception)
            {
                return Task.FromResult(new StaffMailCaseSelection(Options(), [], exception.Message));
            }
        }

        private static IReadOnlyList<StaffMailAttachmentOption> Options() =>
            [new(Selection, Attachment.FileName, Attachment.MediaType, Attachment.ContentLength)];

        private static IReadOnlyList<StaffMailAttachment> Resolve(IReadOnlyList<string> selections)
        {
            if (selections.Count != 1 || selections[0] != Selection)
            {
                throw new StaffMailAttachmentSelectionException(
                    "One or more selected attachments changed or are no longer available. Review the attachments and try again.");
            }

            return [Attachment];
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
        /// The refused AI-job confirm a send that names a job returns; like the
        /// engine's, it is on the returned operation only, never stored.
        /// </summary>
        public AiJobConfirmRefusal? NextAiJobRefusal { get; set; }

        /// <summary>
        /// When set, <see cref="SendAsync"/> throws this immediately, before
        /// recording any command — simulating an offline transport
        /// (<c>UnavailableStaffMailSend</c>) or a composition invariant
        /// failure.
        /// </summary>
        public Exception? ThrowOnSend { get; set; }

        /// <summary>
        /// When set, a new send is recorded as <see cref="NextState"/> and then
        /// throws as the engine does on an ambiguous provider answer: this
        /// fault inside <see cref="StaffMailOutcomeUnknownException"/>. A
        /// replay under the same key returns the recorded operation.
        /// </summary>
        public Exception? ProviderFault { get; set; }

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
                if (ProviderFault is { } fault)
                {
                    throw new StaffMailOutcomeUnknownException(operation, fault);
                }
                return Task.FromResult(command.AiJobId is null
                    ? operation
                    : operation with { AiJobRefusal = NextAiJobRefusal });
            }
        }

        public Task<StaffMailOperation?> GetAsync(
            ActionActor actor, Guid operationId, CancellationToken cancellationToken) =>
            LockedGet(operationId);

        /// <summary>How many times a page read the latest operation of a message.</summary>
        public int LatestForOriginalCalls { get; private set; }

        public Task<StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken)
        {
            lock (sync)
            {
                LatestForOriginalCalls++;
                var matching = commands
                    .Where(item => item.OriginalMessage?.RetainedMessageId == retainedMessageId)
                    .Select(item => byOperationKey[item.OperationKey])
                    .ToArray();
                return Task.FromResult<StaffMailOperation?>(
                    matching.LastOrDefault(operation => IsUnderWay(operation.State))
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

        /// <summary>
        /// A send still under way comes first, as the store orders it; an
        /// Unknown one ranks with the finished, so the newest of those wins (R1128).
        /// </summary>
        private static bool IsUnderWay(StaffMailState state) => state is
            StaffMailState.Prepared or StaffMailState.DraftCreating or StaffMailState.DraftReady
            or StaffMailState.Sending or StaffMailState.Submitted;

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by this test.");
    }
}
