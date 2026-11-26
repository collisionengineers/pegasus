using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Brego;
using Pegasus.Infrastructure.Persistence;
using static Pegasus.IntegrationTests.BregoGuideValuationProviderTests;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Brego valuation account signed in on the Integrations page
/// (26 November 2026, R1290, issue #2180): the e-mailed link asked for and
/// redeemed, the saved password, and the six-digit code Brego e-mails when
/// it asks to approve Pegasus's device, against a scripted Brego and Cognito
/// and the login's row in the database. A finished sign-in is stored under
/// the login's next generation; one that waits for a code is kept protected
/// until it is finished or expires; and no token, password, link or code
/// reaches a history row, a log line or a refusal's message.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class BregoAccountLinkTests
{
    private const string LinkToken = "synthetic-magic-link-token-3318";
    private const string LinkAccessToken = "synthetic-link-access-token-7702";
    private const string LinkRefreshToken = "synthetic-link-refresh-token-6120";
    private const string Code = "482913";
    private const string CognitoSession = "synthetic-cognito-session";
    private const string CustomAuthTarget = "AWSCognitoIdentityProviderService.InitiateAuth";
    private const string ChallengePath = "/login/api/brego/users/me/device-approval/challenge";
    private const string VerifyPath = "/login/api/brego/users/me/device-approval/verify";
    private const string SessionsPath = "/login/api/brego/v1/sessions";

    private static readonly string Link =
        $"https://www.brego.ai/login#magic-link={LinkToken}&email={Uri.EscapeDataString(Email)}";

    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private static readonly string CustomChallengeJson = $$$"""
        {
          "ChallengeName": "CUSTOM_CHALLENGE",
          "Session": "{{{CognitoSession}}}",
          "ChallengeParameters": { "USERNAME": "{{{CognitoUsername}}}" }
        }
        """;

    private static readonly string LinkSentJson = $$$"""
        {
          "ChallengeName": "CUSTOM_CHALLENGE",
          "Session": "{{{CognitoSession}}}-2",
          "ChallengeParameters": { "step": "MAGIC_LINK_SENT" }
        }
        """;

    private static readonly string LinkTokensJson = $$$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{{LinkAccessToken}}}",
            "IdToken": "{{{IdToken}}}",
            "RefreshToken": "{{{LinkRefreshToken}}}",
            "ExpiresIn": 3600,
            "TokenType": "Bearer"
          },
          "ChallengeParameters": {}
        }
        """;

    [Fact]
    public async Task SendingALinkAsksBregoToEmailOneAndWritesNothing()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database);
        await SaveAccountAsync(harness);

        await harness.Link.SendLinkAsync(Administrator, "send", default);

        Assert.Equal(
            ["/login/api/getAuthorizer", "/", "/"],
            harness.Requests.Select(request => request.Uri.AbsolutePath).ToArray());
        Assert.Equal(CustomAuthTarget, Assert.Single(harness.Requests[1].Headers["X-Amz-Target"]));
        Assert.Equal(RespondTarget, Assert.Single(harness.Requests[2].Headers["X-Amz-Target"]));
        using (var started = JsonDocument.Parse(harness.Requests[1].Body!))
        {
            Assert.Equal("CUSTOM_AUTH", started.RootElement.GetProperty("AuthFlow").GetString());
            Assert.Equal(ClientId, started.RootElement.GetProperty("ClientId").GetString());
            Assert.Equal(Email, started.RootElement.GetProperty("AuthParameters").GetProperty("USERNAME").GetString());
        }

        using (var asked = JsonDocument.Parse(harness.Requests[2].Body!))
        {
            var root = asked.RootElement;
            Assert.Equal("CUSTOM_CHALLENGE", root.GetProperty("ChallengeName").GetString());
            Assert.Equal(CognitoSession, root.GetProperty("Session").GetString());
            Assert.Equal(CognitoUsername, root.GetProperty("ChallengeResponses").GetProperty("USERNAME").GetString());
            Assert.Equal("request", root.GetProperty("ChallengeResponses").GetProperty("ANSWER").GetString());
            Assert.Equal("request", root.GetProperty("ClientMetadata").GetProperty("action").GetString());
            Assert.Equal("https://www.brego.ai", root.GetProperty("ClientMetadata").GetProperty("origin").GetString());
        }

        Assert.Equal(1, await VersionAsync(harness));
        Assert.Null(await harness.Link.ReadPendingAsync(Administrator, default));
    }

    [Theory]
    [InlineData("no challenge")]
    [InlineData("no step")]
    public async Task ALinkBregoDoesNotConfirmSendingIsNotOffered(string row)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database)
        {
            Override = (request, body) => row == "no challenge"
                ? HasTarget(request, CustomAuthTarget)
                    ? Json(HttpStatusCode.OK, """{"ChallengeName":"PASSWORD_VERIFIER","ChallengeParameters":{}}""")
                    : null
                : HasTarget(request, RespondTarget)
                    ? Json(HttpStatusCode.OK, """{"ChallengeName":"CUSTOM_CHALLENGE","ChallengeParameters":{"step":"OTHER"}}""")
                    : null,
        };
        await SaveAccountAsync(harness);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.SendLinkAsync(Administrator, "send", default));

        Assert.Equal(BregoLinkRefusal.NotOffered, refused.Refusal);
    }

    [Fact]
    public async Task ALinkOnAnApprovedDeviceStoresTheSignInAndMovesTheGeneration()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database);
        await SaveAccountAsync(harness);

        var step = await harness.Link.LinkAsync(Administrator, Link, 1, "link", default);

        Assert.Equal(BregoLinkStep.Linked, step);
        Assert.Equal(
            ["/login/api/getAuthorizer", "/", "/", ChallengePath, SessionsPath],
            harness.Requests.Select(request => request.Uri.AbsolutePath).ToArray());
        using (var redeemed = JsonDocument.Parse(harness.Requests[2].Body!))
        {
            var root = redeemed.RootElement;
            Assert.Equal(CognitoSession, root.GetProperty("Session").GetString());
            Assert.Equal(LinkToken, root.GetProperty("ChallengeResponses").GetProperty("ANSWER").GetString());
            Assert.Equal("redeem", root.GetProperty("ClientMetadata").GetProperty("action").GetString());
            Assert.Equal(BregoClient.DefaultDeviceId, root.GetProperty("ClientMetadata").GetProperty("deviceId").GetString());
        }

        using (var minted = JsonDocument.Parse(harness.Requests[4].Body!))
        {
            Assert.Equal(LinkRefreshToken, minted.RootElement.GetProperty("refreshToken").GetString());
            Assert.Equal(Slug, minted.RootElement.GetProperty("client").GetString());
        }

        var stored = await harness.Store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(stored);
        Assert.Equal(2, stored.Generation);
        Assert.Equal(Email, stored.Value(IntegrationLoginField.Email));
        Assert.Equal(LinkRefreshToken, stored.RefreshToken);
        Assert.Equal(BregoSessionCookie, stored.SessionCookie);
        Assert.Equal(2, await VersionAsync(harness));
        Assert.Null(await harness.Link.ReadPendingAsync(Administrator, default));
        Assert.Equal("", await PendingColumnAsync(database));
        Assert.Contains("integration_login_linked", await HistoryAsync(database), StringComparison.Ordinal);
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task ALinkOnADeviceBregoWantsACodeForWaitsForItAndStoresNoSignIn()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database) { DeviceApproved = false };
        await SaveAccountAsync(harness);

        var step = await harness.Link.LinkAsync(Administrator, Link, 1, "link", default);

        Assert.Equal(BregoLinkStep.CodeSent, step);
        Assert.DoesNotContain(harness.Requests, request => request.Uri.AbsolutePath == SessionsPath);
        var stored = await harness.Store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(stored);
        Assert.Equal(1, stored.Generation);
        Assert.Null(stored.RefreshToken);
        Assert.Null(stored.SessionCookie);
        Assert.Equal(2, await VersionAsync(harness));
        var pending = await harness.Link.ReadPendingAsync(Administrator, default);
        Assert.NotNull(pending);
        Assert.Equal(Email, pending.Email);
        Assert.Equal(harness.Now.AddSeconds(3600), pending.ExpiresAtUtc);
        var column = await PendingColumnAsync(database);
        Assert.NotEmpty(column);
        Assert.DoesNotContain(LinkAccessToken, column, StringComparison.Ordinal);
        Assert.DoesNotContain(LinkRefreshToken, column, StringComparison.Ordinal);
        Assert.DoesNotContain("integration_login_linked", await HistoryAsync(database), StringComparison.Ordinal);
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task TheCodeFinishesAWaitingSignInAndClearsIt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database) { DeviceApproved = false };
        await SaveAccountAsync(harness);
        Assert.Equal(BregoLinkStep.CodeSent, await harness.Link.LinkAsync(Administrator, Link, 1, "link", default));
        var before = harness.Requests.Count;

        var step = await harness.Link.ConfirmCodeAsync(Administrator, $" {Code} ", 2, "code", default);

        Assert.Equal(BregoLinkStep.Linked, step);
        var later = harness.Requests.Skip(before).ToArray();
        Assert.Equal(
            [VerifyPath, ChallengePath, SessionsPath],
            later.Select(request => request.Uri.AbsolutePath).ToArray());
        Assert.Contains(LinkAccessToken, Assert.Single(later[0].Headers["X-Brego-Bootstrap-Access-Token"]), StringComparison.Ordinal);
        using (var verified = JsonDocument.Parse(later[0].Body!))
        {
            Assert.Equal(Code, verified.RootElement.GetProperty("code").GetString());
            Assert.Equal(BregoClient.DefaultDeviceId, verified.RootElement.GetProperty("deviceId").GetString());
        }

        var stored = await harness.Store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(stored);
        Assert.Equal(2, stored.Generation);
        Assert.Equal(LinkRefreshToken, stored.RefreshToken);
        Assert.Equal(BregoSessionCookie, stored.SessionCookie);
        Assert.Equal(3, await VersionAsync(harness));
        Assert.Null(await harness.Link.ReadPendingAsync(Administrator, default));
        Assert.Equal("", await PendingColumnAsync(database));
        await AssertNoSecretsAsync(database, harness);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("12a456")]
    [InlineData("1234567")]
    [InlineData("")]
    public async Task ACodeThatIsNotSixDigitsIsRefusedWithoutAskingBrego(string code)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database);
        await SaveAccountAsync(harness);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.ConfirmCodeAsync(Administrator, code, 1, "code", default));

        Assert.Equal(BregoLinkRefusal.CodeFormat, refused.Refusal);
        Assert.Empty(harness.Requests);
    }

    [Theory]
    [InlineData("other account", "https://www.brego.ai/login#magic-link=synthetic-token&email=someone%40example.test", BregoLinkRefusal.AnotherAccount)]
    [InlineData("no token", "https://www.brego.ai/login#email=digital%40collisionengineers.test", BregoLinkRefusal.NoToken)]
    [InlineData("blank", "   ", BregoLinkRefusal.NoToken)]
    public async Task APastedLinkWithoutTheAccountsTokenIsRefusedWithoutAskingBrego(
        string row, string link, BregoLinkRefusal expected)
    {
        Assert.NotEmpty(row);
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database);
        await SaveAccountAsync(harness);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.LinkAsync(Administrator, link, 1, "link", default));

        Assert.Equal(expected, refused.Refusal);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ALinkBregoRefusesStoresNothing()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database)
        {
            Override = (request, body) => HasTarget(request, RespondTarget)
                ? Json(HttpStatusCode.BadRequest, """{"__type":"NotAuthorizedException","message":"Incorrect username or password."}""")
                : null,
        };
        await SaveAccountAsync(harness);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.LinkAsync(Administrator, Link, 1, "link", default));

        Assert.Equal(BregoLinkRefusal.LinkRefused, refused.Refusal);
        Assert.DoesNotContain(LinkToken, refused.ToString(), StringComparison.Ordinal);
        Assert.Null((await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.RefreshToken);
        Assert.Equal(1, await VersionAsync(harness));
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("refused as LinkRefused at stage=link_redeem", StringComparison.Ordinal));
        await AssertNoSecretsAsync(database, harness);
    }

    [Theory]
    [InlineData("no answer")]
    [InlineData("server error")]
    public async Task BregoNotAnsweringIsUnreachable(string row)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database)
        {
            Override = (request, body) => !HasTarget(request, RespondTarget)
                ? null
                : row == "no answer"
                    ? throw new HttpRequestException("connection refused")
                    : Json(HttpStatusCode.InternalServerError, """{"message":"Internal server error"}"""),
        };
        await SaveAccountAsync(harness);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.LinkAsync(Administrator, Link, 1, "link", default));

        Assert.Equal(BregoLinkRefusal.Unreachable, refused.Refusal);
        Assert.Equal(1, await VersionAsync(harness));
    }

    [Fact]
    public async Task NoSignInWaitingOrOneThatExpiredIsNoLinkWaiting()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database) { DeviceApproved = false };
        await SaveAccountAsync(harness);

        var none = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.ConfirmCodeAsync(Administrator, Code, 1, "code", default));
        Assert.Equal(BregoLinkRefusal.NoLinkWaiting, none.Refusal);
        Assert.Empty(harness.Requests);

        Assert.Equal(BregoLinkStep.CodeSent, await harness.Link.LinkAsync(Administrator, Link, 1, "link", default));
        var before = harness.Requests.Count;
        harness.Now = harness.Now.AddSeconds(3601);

        var expired = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.ConfirmCodeAsync(Administrator, Code, 2, "code", default));

        Assert.Equal(BregoLinkRefusal.NoLinkWaiting, expired.Refusal);
        Assert.Equal(before, harness.Requests.Count);
        Assert.Null(await harness.Link.ReadPendingAsync(Administrator, default));
    }

    [Fact]
    public async Task ACodeBregoRefusesLeavesTheSignInWaiting()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database) { DeviceApproved = false };
        await SaveAccountAsync(harness);
        Assert.Equal(BregoLinkStep.CodeSent, await harness.Link.LinkAsync(Administrator, Link, 1, "link", default));

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.ConfirmCodeAsync(Administrator, "111111", 2, "code", default));

        Assert.Equal(BregoLinkRefusal.CodeRefused, refused.Refusal);
        Assert.NotNull(await harness.Link.ReadPendingAsync(Administrator, default));
        Assert.Equal(2, await VersionAsync(harness));
        Assert.Equal(BregoLinkStep.Linked, await harness.Link.ConfirmCodeAsync(Administrator, Code, 2, "code-again", default));
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task ThePasswordSignsInOnAnApprovedDevice()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database);
        await SaveAccountAsync(harness, Password);

        var step = await harness.Link.SignInWithPasswordAsync(Administrator, 1, "password", default);

        Assert.Equal(BregoLinkStep.Linked, step);
        Assert.Equal(
            [CustomAuthTarget, RespondTarget],
            harness.Requests
                .Where(request => request.Uri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal))
                .Select(request => Assert.Single(request.Headers["X-Amz-Target"]))
                .ToArray());
        Assert.Contains("USER_SRP_AUTH", harness.Requests[1].Body!, StringComparison.Ordinal);
        var stored = await harness.Store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(stored);
        Assert.Equal(Password, stored.Value(IntegrationLoginField.Password));
        Assert.Equal(PasswordRefreshToken, stored.RefreshToken);
        Assert.Equal(BregoSessionCookie, stored.SessionCookie);
        Assert.Equal(2, stored.Generation);
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task AWrongPasswordIsPasswordRefused()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database)
        {
            Override = (request, body) => HasTarget(request, RespondTarget)
                ? Json(HttpStatusCode.BadRequest, """{"__type":"NotAuthorizedException","message":"Incorrect username or password."}""")
                : null,
        };
        await SaveAccountAsync(harness, Password);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.SignInWithPasswordAsync(Administrator, 1, "password", default));

        Assert.Equal(BregoLinkRefusal.PasswordRefused, refused.Refusal);
        Assert.Null((await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.RefreshToken);
        Assert.Equal(1, await VersionAsync(harness));
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task APasswordSignInOnADeviceBregoWantsACodeForWaitsForIt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database) { DeviceApproved = false };
        await SaveAccountAsync(harness, Password);

        Assert.Equal(
            BregoLinkStep.CodeSent,
            await harness.Link.SignInWithPasswordAsync(Administrator, 1, "password", default));
        Assert.NotNull(await harness.Link.ReadPendingAsync(Administrator, default));
        Assert.Null((await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.RefreshToken);

        Assert.Equal(BregoLinkStep.Linked, await harness.Link.ConfirmCodeAsync(Administrator, Code, 2, "code", default));
        Assert.Equal(PasswordRefreshToken, (await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.RefreshToken);
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task NoAccountNoPasswordAStaleVersionOrAnotherRoleIsRefusedBeforeBrego()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database);

        var noAccount = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.SendLinkAsync(Administrator, "send", default));
        Assert.Equal(BregoLinkRefusal.NoAccount, noAccount.Refusal);

        await SaveAccountAsync(harness);
        var noPassword = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.SignInWithPasswordAsync(Administrator, 1, "password", default));
        Assert.Equal(BregoLinkRefusal.NoAccount, noPassword.Refusal);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            harness.Link.LinkAsync(Administrator, Link, 0, "stale", default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            harness.Link.LinkAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), Link, 1, "engineer", default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            harness.Link.ReadPendingAsync(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), default));

        Assert.Empty(harness.Requests);
    }

    /// <summary>
    /// Brego's password is optional (R1290): saved with or without one, and
    /// any replacement clears the stored sign-in and a sign-in waiting for a
    /// code. The other logins still need every field.
    /// </summary>
    [Fact]
    public async Task BregosPasswordIsOptionalAndAReplacementClearsTheSignInAndAWaitingCode()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var harness = new Harness(database) { DeviceApproved = false };
        await SaveAccountAsync(harness, " ");
        Assert.Null((await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.Value(IntegrationLoginField.Password));
        Assert.Equal(BregoLinkStep.CodeSent, await harness.Link.LinkAsync(Administrator, Link, 1, "link", default));

        var replaced = await harness.Store.ReplaceAsync(
            Administrator,
            IntegrationLogin.Brego,
            2,
            new Dictionary<IntegrationLoginField, string>
            {
                [IntegrationLoginField.Email] = Email,
                [IntegrationLoginField.Password] = Password
            },
            "replace",
            default);

        Assert.Equal(3, replaced.Version);
        Assert.Equal(Email, replaced.AccountName);
        Assert.Null(await harness.Link.ReadPendingAsync(Administrator, default));
        var stored = await harness.Store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(stored);
        Assert.Equal(Password, stored.Value(IntegrationLoginField.Password));
        Assert.Null(stored.RefreshToken);
        Assert.Equal(2, stored.Generation);
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.ReplaceAsync(
            Administrator,
            IntegrationLogin.Cazana,
            0,
            new Dictionary<IntegrationLoginField, string> { [IntegrationLoginField.Email] = Email },
            "cazana-without-password",
            default));
        await Assert.ThrowsAsync<ArgumentException>(() => harness.Store.ReplaceAsync(
            Administrator,
            IntegrationLogin.Brego,
            3,
            new Dictionary<IntegrationLoginField, string> { [IntegrationLoginField.Username] = "someone" },
            "brego-wrong-field",
            default));
    }

    /// <summary>
    /// Refresh login (R1294): Brego is asked for a link, the link is read from
    /// the account's mailbox and redeemed, and the account is linked with one
    /// history row. A stale version is refused before the mailbox or Brego.
    /// </summary>
    [Fact]
    public async Task RefreshLoginReadsTheLinkFromBregosEmailAndLinksTheAccount()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var mail = new FakeBregoMailReader { Link = Link };
        var harness = new Harness(database, mail);
        await SaveAccountAsync(harness);
        Assert.True(harness.Link.ReadsSignInMail);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            harness.Link.RefreshAsync(Administrator, 0, "stale", default));
        Assert.Empty(harness.Requests);
        Assert.Equal(0, mail.CanReads);

        var step = await harness.Link.RefreshAsync(Administrator, 1, "refresh", default);

        Assert.Equal(BregoLinkStep.Linked, step);
        Assert.Single(harness.Requests, request => IsLinkRequest(request.Body));
        var redeemed = Assert.Single(harness.Requests, request => IsRedemption(request.Body));
        Assert.Contains(LinkToken, redeemed.Body!, StringComparison.Ordinal);
        Assert.InRange(mail.LinkReads, 2, 16);
        Assert.All(mail.Since, since => Assert.True(since <= BregoGuideValuationProviderTests.Now));
        var stored = await harness.Store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(stored);
        Assert.Equal(2, stored.Generation);
        Assert.Equal(LinkRefreshToken, stored.RefreshToken);
        Assert.Equal(BregoSessionCookie, stored.SessionCookie);
        Assert.Equal(2, await VersionAsync(harness));
        Assert.Single(
            (await HistoryAsync(database)).Split('\n'),
            line => line.StartsWith("integration_login_linked|", StringComparison.Ordinal));
        await AssertNoSecretsAsync(database, harness);
    }

    [Theory]
    [InlineData("no reader")]
    [InlineData("unreadable")]
    public async Task RefreshLoginOnAHostThatCannotReadTheMailboxAsksNothingOfBrego(string row)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var mail = row == "no reader" ? null : new FakeBregoMailReader { Readable = false, Link = Link };
        var harness = new Harness(database, mail);
        await SaveAccountAsync(harness);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.RefreshAsync(Administrator, 1, "refresh", default));

        Assert.Equal(mail is not null, harness.Link.ReadsSignInMail);
        Assert.Equal(BregoLinkRefusal.MailUnreadable, refused.Refusal);
        Assert.Empty(harness.Requests);
        Assert.Equal(1, await VersionAsync(harness));
    }

    [Fact]
    public async Task ALinkThatDoesNotArriveIsMailNotArrivedAndStoresNothing()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var mail = new FakeBregoMailReader();
        var harness = new Harness(database, mail);
        await SaveAccountAsync(harness);

        var refused = await Assert.ThrowsAsync<BregoLinkRefusedException>(() =>
            harness.Link.RefreshAsync(Administrator, 1, "refresh", default));

        Assert.Equal(BregoLinkRefusal.MailNotArrived, refused.Refusal);
        Assert.Single(harness.Requests, request => IsLinkRequest(request.Body));
        Assert.DoesNotContain(harness.Requests, request => IsRedemption(request.Body));
        Assert.InRange(mail.LinkReads, 2, 16);
        Assert.Equal(1, await VersionAsync(harness));
        Assert.Null((await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.RefreshToken);
    }

    [Fact]
    public async Task RefreshLoginEntersTheCodeFromBregosEmailAndKeepsNothingWaiting()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var mail = new FakeBregoMailReader { Link = Link, Code = Code };
        var harness = new Harness(database, mail) { DeviceApproved = false };
        await SaveAccountAsync(harness);

        var step = await harness.Link.RefreshAsync(Administrator, 1, "refresh", default);

        Assert.Equal(BregoLinkStep.Linked, step);
        var verified = Assert.Single(harness.Requests, request => request.Uri.AbsolutePath == VerifyPath);
        using (var body = JsonDocument.Parse(verified.Body!))
        {
            Assert.Equal(Code, body.RootElement.GetProperty("code").GetString());
        }

        Assert.Null(await harness.Link.ReadPendingAsync(Administrator, default));
        Assert.Equal("", await PendingColumnAsync(database));
        Assert.Equal(2, await VersionAsync(harness));
        Assert.Equal(LinkRefreshToken, (await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.RefreshToken);
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task ACodeThatDoesNotArriveLeavesTheSignInWaitingForIt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var mail = new FakeBregoMailReader { Link = Link };
        var harness = new Harness(database, mail) { DeviceApproved = false };
        await SaveAccountAsync(harness);

        var step = await harness.Link.RefreshAsync(Administrator, 1, "refresh", default);

        Assert.Equal(BregoLinkStep.CodeSent, step);
        Assert.InRange(mail.CodeReads, 2, 16);
        Assert.NotNull(await harness.Link.ReadPendingAsync(Administrator, default));
        Assert.Equal(2, await VersionAsync(harness));
        var stored = await harness.Store.ReadAsync(IntegrationLogin.Brego, default);
        Assert.NotNull(stored);
        Assert.Equal(1, stored.Generation);
        Assert.Null(stored.RefreshToken);
        await AssertNoSecretsAsync(database, harness);
    }

    [Fact]
    public async Task RefreshLoginWithASavedPasswordAsksForNoLink()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var mail = new FakeBregoMailReader { Link = Link };
        var harness = new Harness(database, mail);
        await SaveAccountAsync(harness, Password);

        var step = await harness.Link.RefreshAsync(Administrator, 1, "refresh", default);

        Assert.Equal(BregoLinkStep.Linked, step);
        Assert.DoesNotContain(harness.Requests, request => IsLinkRequest(request.Body));
        Assert.Contains(harness.Requests, request => request.Body?.Contains("USER_SRP_AUTH", StringComparison.Ordinal) == true);
        Assert.Equal(0, mail.LinkReads);
        Assert.Equal(PasswordRefreshToken, (await harness.Store.ReadAsync(IntegrationLogin.Brego, default))!.RefreshToken);
        await AssertNoSecretsAsync(database, harness);
    }

    [Theory]
    [InlineData("link")]
    [InlineData("password")]
    public async Task ALinkOrPasswordSignInReadsTheCodeFromBregosEmail(string row)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var mail = new FakeBregoMailReader { Code = Code };
        var harness = new Harness(database, mail) { DeviceApproved = false };
        await SaveAccountAsync(harness, row == "password" ? Password : null);

        var step = row == "password"
            ? await harness.Link.SignInWithPasswordAsync(Administrator, 1, "password", default)
            : await harness.Link.LinkAsync(Administrator, Link, 1, "link", default);

        Assert.Equal(BregoLinkStep.Linked, step);
        Assert.Contains(harness.Requests, request => request.Uri.AbsolutePath == VerifyPath);
        Assert.Null(await harness.Link.ReadPendingAsync(Administrator, default));
        Assert.Equal(2, await VersionAsync(harness));
        await AssertNoSecretsAsync(database, harness);
    }

    private static bool IsLinkRequest(string? body) =>
        body?.Contains("\"action\":\"request\"", StringComparison.Ordinal) == true;

    private static bool IsRedemption(string? body) =>
        body?.Contains("\"action\":\"redeem\"", StringComparison.Ordinal) == true;

    private static async Task SaveAccountAsync(Harness harness, string? password = null)
    {
        var values = new Dictionary<IntegrationLoginField, string> { [IntegrationLoginField.Email] = Email };
        if (password is not null)
        {
            values[IntegrationLoginField.Password] = password;
        }

        await harness.Store.ReplaceAsync(Administrator, IntegrationLogin.Brego, 0, values, "save", default);
    }

    private static async Task<long> VersionAsync(Harness harness) =>
        (await harness.Store.ListAsync(Administrator, default))
            .Single(item => item.Login == IntegrationLogin.Brego)
            .Version;

    private static Task<string> PendingColumnAsync(LocalDbTestDatabase database) =>
        ScalarAsync(database, "SELECT ISNULL(ProtectedPendingLink, N'') FROM IntegrationLogins WHERE Id = N'brego';");

    private static async Task<string> HistoryAsync(LocalDbTestDatabase database) =>
        string.Join(
            "\n",
            await RowsAsync(
                database,
                "SELECT EventKind + N'|' + CorrelationId + N'|' + ISNULL(BeforeJson, N'') + N'|' + ISNULL(AfterJson, N'') "
                + "FROM ActionHistory WHERE AggregateType = N'integration_login';"));

    /// <summary>The literal test values appear in no history row and no log line.</summary>
    private static async Task AssertNoSecretsAsync(LocalDbTestDatabase database, Harness harness)
    {
        var history = await HistoryAsync(database);
        var logged = string.Join("\n", harness.Logger.Messages);
        foreach (var secret in new[]
        {
            LinkToken, Code, Password, LinkAccessToken, LinkRefreshToken, IdToken,
            AccessToken, PasswordRefreshToken, BregoSessionCookie, CognitoSession
        })
        {
            Assert.DoesNotContain(secret, history, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, logged, StringComparison.Ordinal);
        }
    }

    private static async Task<string> ScalarAsync(LocalDbTestDatabase database, string commandText)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task<List<string>> RowsAsync(LocalDbTestDatabase database, string commandText)
    {
        await using var connection = database.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    /// <summary>
    /// The page's port over a scripted Brego and Cognito and the login's row:
    /// by default Brego offers the link, redeems it, approves the device and
    /// mints a session; a code of <see cref="Code"/> approves a device that
    /// was not.
    /// </summary>
    private sealed class Harness
    {
        public Harness(LocalDbTestDatabase database, FakeBregoMailReader? mail = null)
        {
            var clock = Clock;
            using var scope = database.CreateAsyncScope();
            Store = new EfIntegrationLoginStore(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                new EphemeralDataProtectionProvider(),
                clock);
            Link = new BregoAccountLink(
                new ScriptedClientFactory(new CazanaGuideValuationProviderTests.Answering(AnswerAsync)),
                Store,
                clock,
                Logger,
                mail is null ? null : new BregoMailSignIn(mail, clock));
        }

        /// <summary>The clock a poll for Brego's e-mail steps instead of waiting.</summary>
        public SteppingClock Clock { get; } = new(BregoGuideValuationProviderTests.Now);

        public EfIntegrationLoginStore Store { get; }

        public BregoAccountLink Link { get; }

        public CapturingLogger Logger { get; } = new();

        public List<CazanaGuideValuationProviderTests.RecordedRequest> Requests { get; } = [];

        public DateTimeOffset Now
        {
            get => Clock.Now;
            set => Clock.Now = value;
        }

        public bool DeviceApproved { get; set; } = true;

        public Func<HttpRequestMessage, string?, HttpResponseMessage?>? Override { get; init; }

        private async Task<HttpResponseMessage> AnswerAsync(HttpRequestMessage request)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            Requests.Add(new(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                body));
            return Override?.Invoke(request, body) ?? Answer(request, body ?? "");
        }

        private HttpResponseMessage Answer(HttpRequestMessage request, string body)
        {
            if (HasTarget(request, CustomAuthTarget) && body.Contains("CUSTOM_AUTH", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, CustomChallengeJson);
            }

            if (HasTarget(request, RespondTarget) && body.Contains("\"action\":\"request\"", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, LinkSentJson);
            }

            if (HasTarget(request, RespondTarget) && body.Contains("\"action\":\"redeem\"", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, LinkTokensJson);
            }

            var path = request.RequestUri!.AbsolutePath;
            if (path == ChallengePath)
            {
                return Json(
                    HttpStatusCode.OK,
                    DeviceApproved
                        ? DeviceApprovedJson
                        : """{"status":"challenge_required","destination":"di****@c***.co.uk"}""");
            }

            if (path == VerifyPath)
            {
                if (!body.Contains($"\"code\":\"{Code}\"", StringComparison.Ordinal))
                {
                    return Json(HttpStatusCode.BadRequest, """{"message":"Invalid code"}""");
                }

                DeviceApproved = true;
                return Json(HttpStatusCode.OK, "{}");
            }

            return DefaultAnswer(request);
        }
    }

    private sealed class CapturingLogger : ILogger<BregoAccountLink>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
