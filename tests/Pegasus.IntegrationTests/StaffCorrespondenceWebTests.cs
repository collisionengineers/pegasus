using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// C08: <c>/Inbox/Compose</c> drives the real <c>ComposeModel</c> handlers
/// against a C-owned recording <see cref="IStaffMailSend"/> double. GET never
/// sends; an invalid actor, an unknown mailbox, empty recipients, or a Case
/// whose version has moved on all return the form without a call.
/// </summary>
/// <remarks>
/// Reply/ReplyAll/Forward (<c>Message.cshtml.cs</c>) are not implemented in
/// this slice: building their <see cref="StaffMailOriginalMessage"/> needs
/// the retained message's immutable mailbox/message/internet-message/
/// conversation identity, and no Core query exposes it to Web —
/// <c>RetainedMailDetail</c> (<c>Pegasus.Core.Intake.RetainedMail</c>, out of
/// this ticket's file scope) carries none of the four fields. Recorded as a
/// C08 deviation.
/// </remarks>
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
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
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
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
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
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
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

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory, RecordingStaffMailSend send) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStaffMailSend>();
                services.AddSingleton<IStaffMailSend>(send);
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

    private static async Task SeedSendableMailboxAsync(IntakeWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await TestMailboxId.EnsureApprovedAsync(
            context, "sc08-sender", "sc08-sender@collisionengineers.co.uk", NowUtc.AddDays(-1));
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ApprovedMailboxes SET AllowSentEvidence = 1 WHERE Address = 'sc08-sender@collisionengineers.co.uk'");
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

    private sealed class RecordingStaffMailSend : IStaffMailSend
    {
        private readonly List<StaffMailSendCommand> commands = [];

        public int SendCalls => commands.Count;

        public IReadOnlyList<StaffMailSendCommand> Commands => commands;

        public Task<StaffMailOperation> SendAsync(
            StaffMailSendCommand command, CancellationToken cancellationToken)
        {
            commands.Add(command);
            return Task.FromResult(new StaffMailOperation(
                Guid.NewGuid(),
                StaffMailState.Submitted,
                StaffMailAttemptStage.CreateDraft,
                1,
                NowUtc,
                null,
                null,
                null,
                command.ApprovedMailboxId,
                command.ExpectedMailboxGeneration,
                new string('A', 64),
                NowUtc,
                null));
        }

        public Task<StaffMailOperation?> GetAsync(
            ActionActor actor, Guid operationId, CancellationToken cancellationToken) =>
            Task.FromResult<StaffMailOperation?>(null);

        public Task<StaffMailOperation> ReconcileAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by this test.");

        public Task<StaffMailOperation> CancelAsync(
            ActionActor actor, Guid operationId, long expectedVersion, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not exercised by this test.");
    }
}
