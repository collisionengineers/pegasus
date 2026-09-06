using System.Globalization;
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
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// C08: compose and retained-message correspondence drive the real page
/// handlers against a recording <see cref="IStaffMailSend"/> double. GET never
/// sends; invalid context and stale workflow state return without a call.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class StaffCorrespondenceWebTests
{
    private static readonly DateTimeOffset NowUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetNeverSendsAndRendersTheForm()
    {
        var send = new RecordingStaffMailSend();
        using var factory = Configure(new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true), send);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Inbox/Compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, send.SendCalls);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task FromMailboxRequiresStaffSendAndPositiveGeneration(
        bool staffSend, bool positiveGeneration, bool expected)
    {
        const string address = "capability@example.invalid";
        var scopes = staffSend
            ? new[] { ApprovedMailboxRouteScope.StaffSend }
            : new[] { ApprovedMailboxRouteScope.SentEvidence };
        var mailbox = new ApprovedMailbox(
            Guid.NewGuid(),
            address,
            scopes,
            ApprovedMailboxState.Approved,
            MailboxIdentity: null,
            InboxFolderIdentity: null,
            SentFolderIdentity: null,
            IdentityIsBound: false,
            ActivatedAtUtc: NowUtc,
            Version: 0,
            FolderBindings: [],
            Generation: positiveGeneration ? 1 : 0);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddSingleton<IApprovedMailboxStore>(new FixedApprovedMailboxStore([mailbox]));
            }));
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Inbox/Compose");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(expected, html.Contains(address, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnUnauthenticatedActorIsForbiddenAndSendsNothing()
    {
        var send = new RecordingStaffMailSend();
        using var factory = Configure(new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true), send);
        using var client = CreateClient(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/Inbox/Compose?handler=Send");
        request.Headers.Add("X-Test-Roleless", "1");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["To"] = "claimant@example.invalid",
            ["Subject"] = "Test",
            ["Body"] = "Test body."
        });
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task EmptyRecipientsReturnsTheFormWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var factory = Configure(new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true), send);
        using var client = CreateClient(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync(
            "/Inbox/Compose?handler=Send",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["To"] = string.Empty,
                ["Subject"] = "Test",
                ["Body"] = "Test body."
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("At least one recipient is required.", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task AnUnknownApprovedMailboxReturnsTheFormWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var factory = Configure(new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true), send);
        using var client = CreateClient(factory);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);

        using var response = await client.PostAsync(
            "/Inbox/Compose?handler=Send",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ApprovedMailboxId"] = Guid.NewGuid().ToString("D"),
                ["To"] = "claimant@example.invalid",
                ["Subject"] = "Test",
                ["Body"] = "Test body."
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Choose an approved mailbox", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task AStaleCaseContextReturnsTheFormWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 ABC", "SC08-CLAIM-1");
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var mailboxId = await SentEvidenceMailboxIdAsync(factory);
        var (operationKey, token) = await ComposeFormTokensAsync(
            client, $"/Inbox/Compose?caseId={caseId:D}");

        using var response = await client.PostAsync(
            "/Inbox/Compose?handler=Send",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["OperationKey"] = operationKey,
                ["CaseId"] = caseId.ToString("D"),
                ["ExpectedContextVersion"] = "-1",
                ["ApprovedMailboxId"] = mailboxId.ToString("D"),
                ["To"] = "claimant@example.invalid",
                ["Subject"] = "Test",
                ["Body"] = "Test body."
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("changed after this page was loaded", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task AMissingOperationKeyReturnsTheFormWithoutSending()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 KEY", "SC08-CLAIM-4");
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var mailboxId = await SentEvidenceMailboxIdAsync(factory);
        var expectedVersion = await CaseVersionAsync(factory, caseId);
        var (_, token) = await ComposeFormTokensAsync(
            client, $"/Inbox/Compose?caseId={caseId:D}");

        using var response = await client.PostAsync(
            "/Inbox/Compose?handler=Send",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["CaseId"] = caseId.ToString("D"),
                ["ExpectedContextVersion"] = expectedVersion.ToString(CultureInfo.InvariantCulture),
                ["ApprovedMailboxId"] = mailboxId.ToString("D"),
                ["To"] = "claimant@example.invalid",
                ["Subject"] = "Following up",
                ["Body"] = "Please find the update below."
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("The OperationKey field is required.", html, StringComparison.Ordinal);
        Assert.Equal(0, send.SendCalls);
    }

    [Fact]
    public async Task AValidComposeSendsExactlyOnceInNewModeWithNoOriginalMessage()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 XYZ", "SC08-CLAIM-2");
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var mailboxId = await SentEvidenceMailboxIdAsync(factory);
        var expectedVersion = await CaseVersionAsync(factory, caseId);
        var (operationKey, token) = await ComposeFormTokensAsync(
            client, $"/Inbox/Compose?caseId={caseId:D}");

        using var response = await client.PostAsync(
            "/Inbox/Compose?handler=Send",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["OperationKey"] = operationKey,
                ["CaseId"] = caseId.ToString("D"),
                ["ExpectedContextVersion"] = expectedVersion.ToString(CultureInfo.InvariantCulture),
                ["ApprovedMailboxId"] = mailboxId.ToString("D"),
                ["To"] = "claimant@example.invalid",
                ["Subject"] = "Following up",
                ["Body"] = "Please find the update below."
            }));

        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            var html = await response.Content.ReadAsStringAsync();
            var summary = Regex.Match(
                html,
                "<div[^>]*class=\"[^\"]*validation-summary[^\"]*\"[^>]*>.*?</div>",
                RegexOptions.Singleline);
            Assert.Fail(
                $"POST /Inbox/Compose?handler=Send returned {(int)response.StatusCode} "
                + $"{response.StatusCode} instead of a redirect. Validation summary: "
                + (summary.Success ? summary.Value : "(none found)"));
        }
        Assert.Equal(1, send.SendCalls);
        var command = Assert.Single(send.Commands);
        Assert.Equal(StaffMailComposeMode.New, command.ComposeMode);
        Assert.Null(command.OriginalMessage);
        Assert.Equal(StaffMailPurpose.GeneralCorrespondence, command.Purpose);
        Assert.Equal(caseId, command.ContextId);
        Assert.Equal("claimant@example.invalid", Assert.Single(command.To).Address);
    }

    /// <summary>
    /// C08-R-4: an ambiguous outcome must render the Reconcile action, never
    /// a resend and never a success banner. The redirect after send carries
    /// the operation id forward so the GET it lands on can actually show it.
    /// </summary>
    [Fact]
    public async Task AnUnknownOutcomeRendersReconcileWithoutResendingOrClaimingSuccess()
    {
        var send = new RecordingStaffMailSend { NextState = StaffMailState.Unknown };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 UNK", "SC08-CLAIM-5");
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var mailboxId = await SentEvidenceMailboxIdAsync(factory);
        var expectedVersion = await CaseVersionAsync(factory, caseId);
        var (operationKey, token) = await ComposeFormTokensAsync(
            client, $"/Inbox/Compose?caseId={caseId:D}");

        using var response = await client.PostAsync(
            "/Inbox/Compose?handler=Send",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["OperationKey"] = operationKey,
                ["CaseId"] = caseId.ToString("D"),
                ["ExpectedContextVersion"] = expectedVersion.ToString(CultureInfo.InvariantCulture),
                ["ApprovedMailboxId"] = mailboxId.ToString("D"),
                ["To"] = "claimant@example.invalid",
                ["Subject"] = "Following up",
                ["Body"] = "Please find the update below."
            }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, send.SendCalls);
        var location = response.Headers.Location!;
        Assert.Contains("operationId=", location.ToString(), StringComparison.Ordinal);

        using var statusPage = await client.GetAsync(location);
        Assert.Equal(HttpStatusCode.OK, statusPage.StatusCode);
        var html = await statusPage.Content.ReadAsStringAsync();

        Assert.DoesNotContain("notice--success", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.StaffMail.State(StaffMailState.Unknown), html, StringComparison.Ordinal);
        Assert.Contains("handler=Reconcile", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(OperatorLabels.StaffMail.Reconcile, html, StringComparison.Ordinal);
        // Rendering the status panel must never itself trigger a resend.
        Assert.Equal(1, send.SendCalls);
        Assert.Equal(0, send.ReconcileCalls);
    }

    /// <summary>
    /// C08-R-4: a second POST carrying the same <c>OperationKey</c> as an
    /// already-recorded send must not send again — it shows the operation
    /// the first POST recorded.
    /// </summary>
    [Fact]
    public async Task ASameKeyReplayDoesNotSendAgainAndShowsTheRecordedOperation()
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 RPL", "SC08-CLAIM-6");
        await SeedSendableMailboxAsync(baseFactory);
        using var factory = Configure(baseFactory, send);
        using var client = CreateClient(factory);
        var mailboxId = await SentEvidenceMailboxIdAsync(factory);
        var expectedVersion = await CaseVersionAsync(factory, caseId);
        var (operationKey, token) = await ComposeFormTokensAsync(
            client, $"/Inbox/Compose?caseId={caseId:D}");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["OperationKey"] = operationKey,
            ["CaseId"] = caseId.ToString("D"),
            ["ExpectedContextVersion"] = expectedVersion.ToString(CultureInfo.InvariantCulture),
            ["ApprovedMailboxId"] = mailboxId.ToString("D"),
            ["To"] = "claimant@example.invalid",
            ["Subject"] = "Following up",
            ["Body"] = "Please find the update below."
        };

        using var first = await client.PostAsync(
            "/Inbox/Compose?handler=Send", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(1, send.SendCalls);
        var firstOperationId = ExtractOperationId(first.Headers.Location!);

        using var replay = await client.PostAsync(
            "/Inbox/Compose?handler=Send", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal(1, send.SendCalls);
        Assert.Equal(firstOperationId, ExtractOperationId(replay.Headers.Location!));

        using var statusPage = await client.GetAsync(replay.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, statusPage.StatusCode);
        var html = await statusPage.Content.ReadAsStringAsync();
        // The replay redirected to the same operationId as the first send
        // (asserted above); this confirms the GET it lands on actually
        // renders that recorded operation's status panel, not an empty one.
        Assert.Contains("Send status", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.StaffMail.State(StaffMailState.Submitted), html, StringComparison.Ordinal);
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
        using var factory = Configure(
            baseFactory,
            send,
            new(seeded.MailboxId, seeded.MailboxGeneration));
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
        var version = InputValue(html, "ExpectedCorrespondenceCaseVersion");
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["CorrespondenceOperationKey"] = operationKey,
            ["ExpectedCorrespondenceCaseVersion"] = version,
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
    public async Task UnknownRetainedReplyReplaysAndReconcilesWithoutResending()
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
        var freshForm = new Dictionary<string, string>(form)
        {
            ["CorrespondenceOperationKey"] = $"fresh:{Guid.NewGuid():N}"
        };
        using var fresh = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(freshForm));
        using var replay = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal(1, send.SendCalls);
        Assert.Equal(ExtractOperationId(first.Headers.Location!), ExtractOperationId(replay.Headers.Location!));
        using var status = await client.GetAsync(replay.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Contains("Send status", statusHtml, StringComparison.Ordinal);
        Assert.Contains("ReconcileCorrespondence", statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Reply\"", statusHtml, StringComparison.OrdinalIgnoreCase);
        using var reconcile = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=ReconcileCorrespondence",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(statusHtml, "__RequestVerificationToken"),
                ["mailOperationId"] = ExtractOperationId(replay.Headers.Location!).ToString("D"),
                ["expectedOperationVersion"] = "1"
            }));
        Assert.Equal(HttpStatusCode.Redirect, reconcile.StatusCode);
        Assert.Equal(1, send.SendCalls);
        Assert.Equal(1, send.ReconcileCalls);
    }

    [Theory]
    [InlineData(StaffMailState.Prepared)]
    [InlineData(StaffMailState.DraftCreating)]
    [InlineData(StaffMailState.DraftReady)]
    [InlineData(StaffMailState.Sending)]
    [InlineData(StaffMailState.Submitted)]
    [InlineData(StaffMailState.Unknown)]
    public async Task UnresolvedRetainedReplyShowsStatusAndBlocksASecondFreshAction(
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
        using var post = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = InputValue(html, "CorrespondenceOperationKey"),
                ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
                ["CorrespondenceSubject"] = "Re: Source subject",
                ["CorrespondenceBody"] = "Reviewed response."
            }));
        using var status = await client.GetAsync(post.Headers.Location);
        var statusHtml = await status.Content.ReadAsStringAsync();
        Assert.Contains(OperatorLabels.StaffMail.State(unresolvedState), statusHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("handler=Reply\"", statusHtml, StringComparison.OrdinalIgnoreCase);
        using var fresh = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(statusHtml, "__RequestVerificationToken"),
                ["CorrespondenceOperationKey"] = $"fresh:{Guid.NewGuid():N}",
                ["ExpectedCorrespondenceCaseVersion"] = "1",
                ["CorrespondenceSubject"] = "Re: Source subject",
                ["CorrespondenceBody"] = "Second action."
            }));
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(1, send.SendCalls);
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

        using var secondGet = await client.GetAsync($"/Inbox/{seeded.MessageId:D}?compose=reply");
        var secondHtml = await secondGet.Content.ReadAsStringAsync();
        var secondKey = InputValue(secondHtml, "CorrespondenceOperationKey");
        Assert.NotEqual(firstKey, secondKey);
        using var secondPost = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(RetainedReplyForm(secondHtml, secondKey, "Second action.")));
        Assert.Equal(HttpStatusCode.Redirect, secondPost.StatusCode);
        Assert.Equal(2, send.SendCalls);
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

    [Theory]
    [InlineData(false, ApprovedMailboxState.Approved, true, 1)]
    [InlineData(true, ApprovedMailboxState.Disabled, true, 1)]
    [InlineData(true, ApprovedMailboxState.Approved, false, 1)]
    [InlineData(true, ApprovedMailboxState.Approved, true, 0)]
    public async Task RetainedReplyRequiresApprovedStaffSendMailboxWithPositiveGeneration(
        bool available,
        ApprovedMailboxState state,
        bool staffSend,
        long generation)
    {
        var send = new RecordingStaffMailSend();
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var seedClient = IntakeWebDriver.CreateClient(baseFactory);
        var caseId = await SeedSupportedCaseAsync(
            baseFactory, seedClient, "SC08 MAILBOX", $"SC08-MSG-{state}-{staffSend}-{generation}");
        var seeded = await SeedRetainedCorrespondenceAsync(baseFactory, caseId);
        var mailbox = new ApprovedMailbox(
            seeded.MailboxId,
            seeded.MailboxAddress,
            staffSend ? [ApprovedMailboxRouteScope.StaffSend] : [ApprovedMailboxRouteScope.SentEvidence],
            state,
            null, null, null, false, NowUtc, 1, [], generation);
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
                ["ExpectedCorrespondenceCaseVersion"] = "1",
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
        using var response = await client.PostAsync(
            $"/Inbox/{seeded.MessageId:D}?handler=Reply",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
                ["ExpectedCorrespondenceCaseVersion"] = "1",
                ["CorrespondenceOperationKey"] = $"fresh:{Guid.NewGuid():N}",
                ["CorrespondenceSubject"] = "Re: Source subject",
                ["CorrespondenceBody"] = "Reviewed response."
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(send.Commands);
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
                ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
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
        MailboxCapability? mailboxCapability = null) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffMailSend>();
                services.AddSingleton<IStaffMailSend>(send);
                // Stream A's ruling (PR 673 comment 5561214716) requires
                // StaffSend + a positive Generation before a mailbox is
                // offered. EfApprovedMailboxStore.Map/Routes (A-owned) does
                // not map those columns on this standalone C branch yet
                // (ASSUMPTION 2 CLOSED, scratch/c08-notes on INTK-060), so
                // this shim promotes a SentEvidence-scoped test mailbox the
                // same way the real store will once that mapping lands —
                // proving ComposeModel's own filter, not standing in for the
                // Infrastructure gap itself.
                services.RemoveAll<IApprovedMailboxStore>();
                services.AddScoped<IApprovedMailboxStore>(provider =>
                    new StaffSendCapableMailboxStore(
                        provider.GetRequiredService<EfApprovedMailboxStore>(),
                        mailboxCapability));
            }));

    /// <summary>
    /// Wraps the real EF-backed store, promoting a SentEvidence-scoped
    /// mailbox to also carry <see cref="ApprovedMailboxRouteScope.StaffSend"/>
    /// and a positive <see cref="ApprovedMailbox.Generation"/> — the mapping
    /// <c>EfApprovedMailboxStore.Map</c>/<c>Routes</c> (A-owned) does not yet
    /// perform on this branch. Every other read/write goes through the real
    /// store untouched.
    /// </summary>
    private sealed record MailboxCapability(Guid MailboxId, long Generation);

    private sealed class StaffSendCapableMailboxStore(
        IApprovedMailboxStore inner,
        MailboxCapability? mailboxCapability) : IApprovedMailboxStore
    {
        public async Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken)
        {
            var mailboxes = await inner.ListAsync(cancellationToken);
            return mailboxes
                .Select(mailbox => mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence)
                    ? mailbox with
                    {
                        RouteScopes = [.. mailbox.RouteScopes, ApprovedMailboxRouteScope.StaffSend],
                        Generation = mailboxCapability is not null
                            && mailbox.Id == mailboxCapability.MailboxId
                                ? mailboxCapability.Generation
                                : mailbox.Generation > 0 ? mailbox.Generation : 1
                    }
                    : mailbox)
                .ToArray();
        }

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            inner.UpdateAsync(request, cancellationToken);

        public Task<bool> IsApprovedAsync(
            string mailboxAddress, ApprovedMailboxRouteScope routeScope, CancellationToken cancellationToken) =>
            inner.IsApprovedAsync(mailboxAddress, routeScope, cancellationToken);
    }

    private static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The input '{name}' was not rendered.");
        var value = Regex.Match(tag.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
        Assert.True(value.Success, $"The input '{name}' had no value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    private static Dictionary<string, string> RetainedReplyForm(
        string html,
        string operationKey,
        string body) => new()
    {
        ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
        ["CorrespondenceOperationKey"] = operationKey,
        ["ExpectedCorrespondenceCaseVersion"] = InputValue(html, "ExpectedCorrespondenceCaseVersion"),
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
            throw new InvalidOperationException("The read-only compose test cannot update a mailbox.");

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

    private static async Task SeedSendableMailboxAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await TestMailboxId.EnsureApprovedAsync(
            context, "sc08-sender", "sc08-sender@collisionengineers.co.uk", NowUtc.AddDays(-1));
        await context.SaveChangesAsync();
        // AllowStaffSend/MailboxGeneration are seeded on the real columns
        // (the shape A02's store persists) even though
        // EfApprovedMailboxStore.Map/Routes does not read either one on this
        // standalone C branch yet — inert here; StaffSendCapableMailboxStore
        // above is what actually exercises ComposeModel's filter until that
        // Infrastructure mapping lands.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ApprovedMailboxes SET AllowSentEvidence = 1, AllowStaffSend = 1, MailboxGeneration = 1 WHERE Address = 'sc08-sender@collisionengineers.co.uk'");
    }

    private sealed record SeededCorrespondence(
        Guid MessageId,
        Guid MailboxId,
        string MailboxAddress,
        long MailboxGeneration,
        string ImmutableMessageId,
        string InternetMessageId,
        string ConversationId);

    private static async Task<SeededCorrespondence> SeedRetainedCorrespondenceAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId)
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
            mailbox.AllowSentEvidence = true;
            mailbox.AllowStaffSend = true;
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
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
            if (workflow.Version <= 0)
                workflow.Version = 1;
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
            context.IntakeManualAssociations.Add(new()
            {
                IntakeReceiptId = receiptId,
                CaseId = caseId,
                IsActive = true,
                Version = 1,
                LinkedAtUtc = NowUtc,
                ActorKind = ActorKind.Staff.ToString(),
                ActorSubjectId = DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
                ActorRolesJson = "[\"Administrator\"]",
                Reason = "Fixture message association.",
                LastOperationKey = $"fixture-link:{Guid.NewGuid():N}"
            });
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
            conversationId);
    }

    private static async Task<Guid> SentEvidenceMailboxIdAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IApprovedMailboxStore>();
        var mailbox = (await store.ListAsync(CancellationToken.None))
            .Single(item => item.Address == "sc08-sender@collisionengineers.co.uk");
        return mailbox.Id;
    }

    /// <summary>
    /// GETs a Compose page and reads its rendered hidden <c>OperationKey</c>
    /// and antiforgery token, so a POST carries the real idempotency key the
    /// GET issued rather than omitting it.
    /// </summary>
    private static async Task<(string OperationKey, string AntiforgeryToken)> ComposeFormTokensAsync(
        HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();

        var operationKeyTag = Regex.Match(
            html, "<input[^>]*name=\"OperationKey\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(operationKeyTag.Success, "Compose must render a hidden OperationKey field.");
        var operationKeyValue = Regex.Match(operationKeyTag.Value, "value=\"(?<value>[^\"]*)\"");
        Assert.True(operationKeyValue.Success, "The OperationKey field must have a value.");

        var antiforgeryTag = Regex.Match(
            html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(antiforgeryTag.Success, "Compose must render an antiforgery token.");
        var antiforgeryValue = Regex.Match(antiforgeryTag.Value, "value=\"(?<value>[^\"]*)\"");
        Assert.True(antiforgeryValue.Success, "The antiforgery token must have a value.");

        return (
            WebUtility.HtmlDecode(operationKeyValue.Groups["value"].Value),
            WebUtility.HtmlDecode(antiforgeryValue.Groups["value"].Value));
    }

    private static async Task<long> CaseVersionAsync(WebApplicationFactory<Program> factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var getCase = scope.ServiceProvider.GetRequiredService<IGetCase>();
        var actor = ActionActor.Staff(
            DevelopmentOfflineIdentity.AdministratorId,
            [StaffRole.Administrator]);
        var details = await getCase.ExecuteAsync(new(caseId, actor), CancellationToken.None);
        return details!.Workflow.Version;
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
        private readonly List<StaffMailSendCommand> commands = [];
        private readonly Dictionary<string, StaffMailOperation> byOperationKey = [];
        private readonly Dictionary<Guid, StaffMailOperation> byOperationId = [];

        public int SendCalls => commands.Count;

        public int ReconcileCalls { get; private set; }

        public IReadOnlyList<StaffMailSendCommand> Commands => commands;

        public StaffMailState NextState { get; set; } = StaffMailState.Submitted;

        public Task<StaffMailOperation> SendAsync(
            StaffMailSendCommand command, CancellationToken cancellationToken)
        {
            if (byOperationKey.TryGetValue(command.OperationKey, out var existing))
            {
                return Task.FromResult(existing);
            }

            commands.Add(command);
            var operation = new StaffMailOperation(
                Guid.NewGuid(),
                NextState,
                NextState == StaffMailState.Sent ? StaffMailAttemptStage.ObserveSent : StaffMailAttemptStage.CreateDraft,
                1,
                NowUtc,
                NextState == StaffMailState.Sent ? NowUtc : null,
                NextState == StaffMailState.Sent ? NowUtc : null,
                null,
                command.ApprovedMailboxId,
                command.ExpectedMailboxGeneration,
                new string('A', 64),
                NowUtc,
                null);
            byOperationKey[command.OperationKey] = operation;
            byOperationId[operation.Id] = operation;
            return Task.FromResult(operation);
        }

        public Task<StaffMailOperation?> GetAsync(
            ActionActor actor, Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult(byOperationId.TryGetValue(operationId, out var operation) ? operation : null);

        public Task<StaffMailOperation?> GetLatestForOriginalAsync(
            ActionActor actor, Guid retainedMessageId, CancellationToken cancellationToken)
        {
            var command = commands.LastOrDefault(item =>
                item.OriginalMessage?.RetainedMessageId == retainedMessageId);
            var operation = command is null ? null : byOperationKey[command.OperationKey];
            return Task.FromResult(operation);
        }

        public Task<StaffMailOperation> ReconcileAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken)
        {
            ReconcileCalls++;
            if (!byOperationId.TryGetValue(operationId, out var operation))
            {
                throw new InvalidOperationException($"No recorded operation {operationId:D}.");
            }
            return Task.FromResult(operation);
        }

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by this test.");
    }
}
