using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Brego;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Brego as a guide valuation source through the Brego valuation account
/// (ADR-0084): Cognito token renewal from the stored refresh token
/// (<c>GetTokensFromRefreshToken</c>), sign-in with a saved password
/// (<c>USER_SRP_AUTH</c>, R1290) when there is none or it is refused,
/// device-approval challenge, BFF
/// session cookie creation and reuse, VRM lookup, point-in-time valuation
/// (<c>retail.high</c> and <c>trade.high</c>), empty-VIN fill, report handle,
/// month-to-date mapping, whole-mile conversion, and stage-named failure codes.
/// </summary>
public sealed class BregoGuideValuationProviderTests
{
    internal const string Email = "digital@collisionengineers.test";
    internal const string OrganizationId = "876db4e2-68f3-49a2-a824-cc2e44076a57";
    internal const string Slug = "collision-engineers";
    internal const string PoolId = "eu-west-2_ukSKsqAOY";
    internal const string ClientId = "2fi40vh0cav05pikjkh04nd9e5";
    internal const string CognitoUsername = "e6e222b4-a071-7002-cd41-d8b4a27b9eef";
    internal const string RefreshTarget = "AWSCognitoIdentityProviderService.GetTokensFromRefreshToken";
    internal const string InitiateAuthTarget = "AWSCognitoIdentityProviderService.InitiateAuth";
    internal const string RespondTarget = "AWSCognitoIdentityProviderService.RespondToAuthChallenge";
    internal const string Password = "synthetic-brego-password";
    internal const string PoolName = "ukSKsqAOY";
    internal const string PasswordRefreshToken = "synthetic-password-refresh-token";
    internal const string AccessToken = "synthetic-cognito-access-token";
    internal static readonly string IdToken = SyntheticIdToken(
        $$"""{"cognito:username":"{{CognitoUsername}}","organizationId":"{{OrganizationId}}","slug":"{{Slug}}","email":"{{Email}}"}""");
    internal const string RefreshToken = "synthetic-cognito-refresh-token";
    internal const string RotatedRefreshToken = "synthetic-rotated-refresh-token";
    internal const string BregoSessionCookie = "synthetic-brego-session-cookie";
    internal const string PersistedCookie = "persisted-one-year-cookie";
    internal const string DerivativeId = "d8d2bc32-6912-403e-9a43-59d6d0779c11";
    internal const string Vin = "SB1ZB3AE40E045642";

    internal static readonly DateTimeOffset Now = new(2031, 4, 15, 10, 0, 0, TimeSpan.Zero);
    internal static readonly DateOnly April = new(2031, 4, 1);

    // As Brego answers from November 2026: the tenant is no longer named here.
    internal static readonly string AuthorizerJson = $$$"""
        [
          {
            "authorizer": "{{{PoolId}}}",
            "authorizerId": "{{{ClientId}}}",
            "allowPasskeyLogin": true
          }
        ]
        """;

    /// <summary>An unsigned token in a JWT's three-part shape, carrying the given claims.</summary>
    internal static string SyntheticIdToken(string claimsJson) =>
        "synthetic-header."
        + Convert.ToBase64String(Encoding.UTF8.GetBytes(claimsJson)).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        + ".synthetic-signature";

    // As Cognito answered GetTokensFromRefreshToken on 23 November 2026: the
    // pool does not rotate, so no RefreshToken comes back.
    internal static readonly string RefreshTokensJson = $$$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{{AccessToken}}}",
            "ExpiresIn": 86400,
            "IdToken": "{{{IdToken}}}",
            "TokenType": "Bearer"
          },
          "ChallengeParameters": {}
        }
        """;

    internal static readonly string RotatingRefreshTokensJson = $$$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{{AccessToken}}}",
            "ExpiresIn": 86400,
            "IdToken": "{{{IdToken}}}",
            "RefreshToken": "{{{RotatedRefreshToken}}}",
            "TokenType": "Bearer"
          },
          "ChallengeParameters": {}
        }
        """;

    internal const string DeviceApprovedJson = """{"status":"approved"}""";

    internal static readonly string SessionJson = $$$"""
        {
          "sessionId": "b0d2c68e-7394-49bb-aed4-921fd7ae8435",
          "organizationId": "{{{OrganizationId}}}"
        }
        """;

    private const string SaltHex = "3f7a9c12e4b081652390abcd1234ef01";
    private static readonly string SecretBlockBase64 = Convert.ToBase64String("synthetic-secret-block-bytes"u8);

    // A valid server public ephemeral key B = (k * g^x + g^b) mod N for the test password.
    private static readonly BigInteger ServerPrivateB = new(1234567890123456789UL);
    private static readonly BigInteger SaltInt = new(Convert.FromHexString(SaltHex), isUnsigned: true, isBigEndian: true);
    private static readonly BigInteger ServerPublicB = ComputeServerPublicB();

    private static BigInteger ComputeServerPublicB()
    {
        var userPassHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{PoolName}{CognitoUsername}:{Password}"));
        var saltBytes = BregoClient.PadBytes(SaltInt);
        var xInput = new byte[saltBytes.Length + userPassHash.Length];
        Buffer.BlockCopy(saltBytes, 0, xInput, 0, saltBytes.Length);
        Buffer.BlockCopy(userPassHash, 0, xInput, saltBytes.Length, userPassHash.Length);
        var x = new BigInteger(SHA256.HashData(xInput), isUnsigned: true, isBigEndian: true);
        var verifier = BigInteger.ModPow(BregoClient.G, x, BregoClient.N);
        var gb = BigInteger.ModPow(BregoClient.G, ServerPrivateB, BregoClient.N);
        return ((BregoClient.K * verifier) + gb) % BregoClient.N;
    }

    // As Cognito answered USER_SRP_AUTH before R1231.
    internal static readonly string SrpChallengeJson = $$$"""
        {
          "ChallengeName": "PASSWORD_VERIFIER",
          "ChallengeParameters": {
            "USER_ID_FOR_SRP": "{{{CognitoUsername}}}",
            "USERNAME": "{{{CognitoUsername}}}",
            "SALT": "{{{SaltHex}}}",
            "SRP_B": "{{{BregoClient.ToUnsignedHexLower(ServerPublicB)}}}",
            "SECRET_BLOCK": "{{{SecretBlockBase64}}}"
          }
        }
        """;

    internal const string MailToken = "synthetic-mailed-link-token-8841";
    internal const string MailRefreshToken = "synthetic-mailed-refresh-token-2207";
    internal const string MailCode = "604217";

    // The link as Brego e-mails it to the account, HTML-decoded (R1294).
    internal static readonly string MailedLink =
        $"https://www.brego.ai/login?client=collision-engineers#magic-link={MailToken}&email={Uri.EscapeDataString(Email)}";

    internal static readonly string MailCustomChallengeJson = $$$"""
        {"ChallengeName":"CUSTOM_CHALLENGE","Session":"synthetic-mail-session","ChallengeParameters":{"USERNAME":"{{{CognitoUsername}}}"}}
        """;

    internal const string MailLinkSentJson =
        """{"ChallengeName":"CUSTOM_CHALLENGE","Session":"synthetic-mail-session-2","ChallengeParameters":{"step":"MAGIC_LINK_SENT"}}""";

    internal static readonly string MailLinkTokensJson = $$$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{{AccessToken}}}",
            "IdToken": "{{{IdToken}}}",
            "RefreshToken": "{{{MailRefreshToken}}}",
            "ExpiresIn": 3600
          }
        }
        """;

    internal static readonly string PasswordTokensJson = $$$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{{AccessToken}}}",
            "IdToken": "{{{IdToken}}}",
            "RefreshToken": "{{{PasswordRefreshToken}}}",
            "ExpiresIn": 86400
          }
        }
        """;

    [Fact]
    public async Task TheFiguresVinAndReportComeFromBregoLookupAndPointInTimeValuation()
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request(" ab12 cde ", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(11890m, quote.TradeValue);
        Assert.Equal(April, quote.GuideMonth);
        Assert.Equal(33000, quote.Mileage);
        Assert.Equal(Vin, quote.Vin);
        Assert.StartsWith("brego-valuation:", quote.Report!.Identity, StringComparison.Ordinal);

        Assert.Equal(
            [
                "POST https://www.brego.ai/login/api/getAuthorizer",
                "POST https://cognito-idp.eu-west-2.amazonaws.com/",
                "POST https://www.brego.ai/login/api/brego/users/me/device-approval/challenge",
                "POST https://www.brego.ai/login/api/brego/v1/sessions",
                "GET https://collision-engineers.brego.ai/platform/api/brego/vehicles/vrm/ab12cde?countryCode=gb&expand=sources&expand=metadata",
                "GET https://collision-engineers.brego.ai/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations?countryCode=gb&dates=2031-04-15&period=month&futureMonthlyMileage=650&standardOptionsValue=0&fullOptionsValue=0&significantOptionsValue=0&excludeTax=false&excludeUserHistory=false&mileage=33000&color=Silver&year=2024&previousOwners=2&registrationDate=2024-01-04",
            ],
            harness.Requests.Select(request => $"{request.Method} {request.Uri}").ToArray());

        Assert.Contains(AccessToken, harness.Requests[2].Headers["X-Brego-Bootstrap-Access-Token"]);
        Assert.Contains(AccessToken, harness.Requests[3].Headers["X-Brego-Bootstrap-Access-Token"]);

        using var sessionDoc = JsonDocument.Parse(harness.Requests[3].Body!);
        Assert.Equal(Slug, sessionDoc.RootElement.GetProperty("client").GetString());
        Assert.Equal(IdToken, sessionDoc.RootElement.GetProperty("idToken").GetString());
        Assert.Equal(RefreshToken, sessionDoc.RootElement.GetProperty("refreshToken").GetString());

        foreach (var platformCall in harness.Requests.Skip(4))
        {
            var cookie = Assert.Single(platformCall.Headers["Cookie"]);
            Assert.Contains($"brego-session={BregoSessionCookie}", cookie, StringComparison.Ordinal);
            Assert.Contains($"CognitoIdentityServiceProvider.{ClientId}.LastAuthUser={CognitoUsername};", cookie, StringComparison.Ordinal);
            Assert.Contains($"CognitoIdentityServiceProvider.{ClientId}.{CognitoUsername}.accessToken={AccessToken}", cookie, StringComparison.Ordinal);
            Assert.Contains($"CognitoIdentityServiceProvider.{ClientId}.{CognitoUsername}.refreshToken={RefreshToken};", cookie, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            harness.Requests,
            request => request.Uri.AbsoluteUri.Contains(RefreshToken, StringComparison.Ordinal)
                || request.Uri.AbsoluteUri.Contains(AccessToken, StringComparison.Ordinal)
                || request.Uri.AbsoluteUri.Contains(BregoSessionCookie, StringComparison.Ordinal));
        Assert.Empty(harness.Logger.Messages);
    }

    [Fact]
    public async Task SignInRenewsTheCognitoTokensFromTheStoredRefreshTokenAndMakesNoPasswordCall()
    {
        var harness = Harness.Create();

        await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        var renewal = Assert.Single(
            harness.Requests,
            request => request.Uri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal));
        Assert.Equal(RefreshTarget, Assert.Single(renewal.Headers["X-Amz-Target"]));
        using var body = JsonDocument.Parse(renewal.Body!);
        Assert.Equal(ClientId, body.RootElement.GetProperty("ClientId").GetString());
        Assert.Equal(RefreshToken, body.RootElement.GetProperty("RefreshToken").GetString());
        Assert.Equal(2, body.RootElement.EnumerateObject().Count());
        Assert.DoesNotContain(
            harness.Requests,
            request => request.Body is { } text
                && (text.Contains("USER_SRP_AUTH", StringComparison.Ordinal)
                    || text.Contains("PASSWORD_VERIFIER", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("no store")]
    [InlineData("empty store")]
    [InlineData("cookie only")]
    public async Task WithNoStoredRefreshTokenTheValuationIsUnavailableAndBregoIsNotCalled(string row)
    {
        var harness = row switch
        {
            "no store" => Harness.Create(login: new InMemoryBregoLogin(Email, null, null), withoutStore: true),
            "empty store" => Harness.Create(login: new InMemoryBregoLogin(Email, null, null)),
            _ => Harness.Create(login: new InMemoryBregoLogin(Email, PersistedCookie, null)),
        };

        await AssertUnavailableAsync(harness, "brego.valuation.unreadable stage=sign_in refresh_token=absent");
        Assert.Empty(harness.Requests);
    }

    /// <summary>
    /// The session this host holds is still within its 24 hours, but the
    /// account was replaced and linked again since: the next press signs in
    /// with the new link instead of reusing it (R1288).
    /// </summary>
    [Fact]
    public async Task AStillValidSessionIsNotReusedOnceTheAccountGenerationMoves()
    {
        const string relinkedRefreshToken = "synthetic-relinked-refresh-token";
        var login = new InMemoryBregoLogin(Email, null, RefreshToken);
        var harness = Harness.Create(login: login);

        await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(6, harness.Requests.Count);

        login.Link(null, relinkedRefreshToken);
        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        var renewal = Assert.Single(
            harness.Requests.Skip(6),
            request => request.Uri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(renewal.Body!);
        Assert.Equal(relinkedRefreshToken, body.RootElement.GetProperty("RefreshToken").GetString());
    }

    /// <summary>
    /// A press that began before the account was replaced still finishes, but
    /// the sign-in it renewed belongs to the old account and is not stored
    /// over the new one.
    /// </summary>
    [Fact]
    public async Task ASignInRenewedForAReplacedAccountIsNotStoredOverIt()
    {
        var login = new InMemoryBregoLogin(Email, null, RefreshToken);
        var before = await login.ReadAsync(default);
        login.Replace("new-account@collisionengineers.test");
        var harness = Harness.Create(account: _ => Task.FromResult(before), login: login);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(0, login.Saves);
        Assert.Null(login.Cookie);
        Assert.Null(login.StoredRefreshToken);
    }

    [Fact]
    public async Task ARotatedRefreshTokenIsCarriedForwardAndStoredWithTheKeptCookie()
    {
        var store = new InMemoryBregoLogin(Email, PersistedCookie, RefreshToken);
        var harness = Harness.Create(
            request => IsTokenRenewal(request) ? Json(HttpStatusCode.OK, RotatingRefreshTokensJson) : null,
            login: store);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(1, store.Saves);
        Assert.Equal(RotatedRefreshToken, store.StoredRefreshToken);
        Assert.Equal(PersistedCookie, store.Cookie);
        Assert.DoesNotContain(harness.Requests, request => request.Uri.AbsolutePath == "/login/api/brego/v1/sessions");
        Assert.Contains(
            $"CognitoIdentityServiceProvider.{ClientId}.{CognitoUsername}.refreshToken={RotatedRefreshToken};",
            Assert.Single(harness.Requests[^1].Headers["Cookie"]),
            StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains(RotatedRefreshToken, StringComparison.Ordinal));
    }

    [Fact]
    public async Task APlatform401MintsANewSessionFromTheRefreshTokenAndStoresTheNewCookie()
    {
        var store = new InMemoryBregoLogin(Email, PersistedCookie, RefreshToken);
        var rejected = false;
        var harness = Harness.Create(
            request =>
            {
                if (!rejected && request.RequestUri!.AbsolutePath.Contains("/vehicles/vrm/", StringComparison.Ordinal))
                {
                    rejected = true;
                    return Json(HttpStatusCode.Unauthorized, """{"message":"Session expired"}""");
                }

                return null;
            },
            login: store);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(
            [
                "/login/api/getAuthorizer",
                "/",
                "/platform/api/brego/vehicles/vrm/ab12cde",
                "/login/api/getAuthorizer",
                "/",
                "/login/api/brego/users/me/device-approval/challenge",
                "/login/api/brego/v1/sessions",
                "/platform/api/brego/vehicles/vrm/ab12cde",
                "/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations",
            ],
            harness.Requests.Select(request => request.Uri.AbsolutePath).ToArray());
        var renewals = harness.Requests
            .Where(request => request.Uri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, renewals.Length);
        Assert.All(renewals, renewal =>
        {
            using var body = JsonDocument.Parse(renewal.Body!);
            Assert.Equal(RefreshToken, body.RootElement.GetProperty("RefreshToken").GetString());
        });
        Assert.Equal(1, store.Saves);
        Assert.Equal(BregoSessionCookie, store.Cookie);
        Assert.Equal(RefreshToken, store.StoredRefreshToken);
        Assert.Contains(
            $"brego-session={BregoSessionCookie}",
            Assert.Single(harness.Requests[^1].Headers["Cookie"]),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubsequentValuationsReuseTheActiveSessionAndReauthenticateOnceOn401()
    {
        var failFirstReusedLookup = false;
        var harness = Harness.Create(request =>
        {
            if (failFirstReusedLookup
                && request.RequestUri!.AbsolutePath.Contains("/vehicles/vrm/", StringComparison.Ordinal))
            {
                failFirstReusedLookup = false;
                return Json(HttpStatusCode.Unauthorized, """{"message":"Session expired"}""");
            }

            return null;
        });

        await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(6, harness.Requests.Count);

        // Second call reuses the session without calling login/Cognito again.
        await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(8, harness.Requests.Count);

        // Third call receives 401 on the reused session, refreshes the session once, and succeeds.
        failFirstReusedLookup = true;
        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(15, harness.Requests.Count);
    }

    [Fact]
    public async Task NextDayCognitoTokenRefreshReusesTheOneYearSessionCookieWithoutDeviceApproval()
    {
        var currentUtc = Now;
        var harness = Harness.Create(timeProvider: new DelegateClock(() => currentUtc));

        // Day 1: full sign-in (6 calls including device-approval/challenge and v1/sessions).
        await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(6, harness.Requests.Count);

        // Day 2 (25 hours later): 24h Cognito token expired, so getAuthorizer + Cognito renewal run,
        // but the 1-year brego-session cookie is retained without calling device-approval/challenge or v1/sessions.
        currentUtc = Now.AddHours(25);
        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(10, harness.Requests.Count);
        Assert.Equal(
            [
                "POST https://www.brego.ai/login/api/getAuthorizer",
                "POST https://cognito-idp.eu-west-2.amazonaws.com/",
                "GET https://collision-engineers.brego.ai/platform/api/brego/vehicles/vrm/ab12cde?countryCode=gb&expand=sources&expand=metadata",
                "GET https://collision-engineers.brego.ai/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations?countryCode=gb&dates=2031-04-16&period=month&futureMonthlyMileage=650&standardOptionsValue=0&fullOptionsValue=0&significantOptionsValue=0&excludeTax=false&excludeUserHistory=false&mileage=33000&color=Silver&year=2024&previousOwners=2&registrationDate=2024-01-04",
            ],
            harness.Requests.Skip(6).Select(request => $"{request.Method} {request.Uri}").ToArray());
    }

    [Fact]
    public async Task HostRestartLoadsPersistedSessionCookieWithoutDeviceApprovalAndReplacesItOn401()
    {
        var store = new InMemoryBregoLogin(Email, PersistedCookie, RefreshToken);
        var failPersistedCookieOnce = false;
        var harness = Harness.Create(
            request =>
            {
                if (failPersistedCookieOnce
                    && request.RequestUri!.AbsolutePath.Contains("/vehicles/vrm/", StringComparison.Ordinal))
                {
                    failPersistedCookieOnce = false;
                    return Json(HttpStatusCode.Unauthorized, """{"message":"Session expired"}""");
                }

                return null;
            },
            login: store);

        // First call after host restart uses the persisted brego-session cookie without device-approval or v1/sessions (4 calls total).
        var firstQuote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(14250m, firstQuote.RetailValue);
        Assert.Equal(4, harness.Requests.Count);
        Assert.DoesNotContain(harness.Requests, request => request.Uri.AbsolutePath.Contains("device-approval", StringComparison.Ordinal));
        Assert.Contains($"brego-session={PersistedCookie}", Assert.Single(harness.Requests[^1].Headers["Cookie"]), StringComparison.Ordinal);
        Assert.Equal(PersistedCookie, store.Cookie);
        Assert.Equal(0, store.Saves);

        // When Brego rejects the persisted cookie with 401 on a fresh host start, the provider mints a new session from the refresh token and updates the store.
        var restartedHarness = Harness.Create(
            request =>
            {
                if (failPersistedCookieOnce
                    && request.RequestUri!.AbsolutePath.Contains("/vehicles/vrm/", StringComparison.Ordinal))
                {
                    failPersistedCookieOnce = false;
                    return Json(HttpStatusCode.Unauthorized, """{"message":"Session expired"}""");
                }

                return null;
            },
            login: store);
        failPersistedCookieOnce = true;
        var secondQuote = await restartedHarness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(14250m, secondQuote.RetailValue);
        Assert.Equal(BregoSessionCookie, store.Cookie);
        Assert.Equal(RefreshToken, store.StoredRefreshToken);
        Assert.Contains(restartedHarness.Requests, request => request.Uri.AbsolutePath == "/login/api/brego/v1/sessions");
    }

    [Fact]
    public async Task ASignInStoredSinceThisHostSignedInIsUsedWithoutARestart()
    {
        const string relinkedRefreshToken = "synthetic-relinked-refresh-token";
        const string relinkedCookie = "synthetic-relinked-session-cookie";
        var currentUtc = Now;
        var store = new InMemoryBregoLogin(Email, null, RefreshToken);
        var harness = Harness.Create(timeProvider: new DelegateClock(() => currentUtc), login: store);

        await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(6, harness.Requests.Count);

        // The operator links the account again while this host runs.
        store.Link(relinkedCookie, relinkedRefreshToken);
        currentUtc = Now.AddHours(25);
        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        var later = harness.Requests.Skip(6).ToArray();
        Assert.Equal(4, later.Length);
        var renewal = Assert.Single(
            later,
            request => request.Uri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(renewal.Body!);
        Assert.Equal(relinkedRefreshToken, body.RootElement.GetProperty("RefreshToken").GetString());
        foreach (var platformCall in later.Where(request => request.Uri.Host.EndsWith(".brego.ai", StringComparison.Ordinal)
            && request.Uri.AbsolutePath.StartsWith("/platform/", StringComparison.Ordinal)))
        {
            var cookie = Assert.Single(platformCall.Headers["Cookie"]);
            Assert.Contains($"brego-session={relinkedCookie}", cookie, StringComparison.Ordinal);
            Assert.Contains($".{CognitoUsername}.refreshToken={relinkedRefreshToken};", cookie, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CognitoSrpPasswordClaimSignatureMatchesDeterministicVector()
    {
        var a = new BigInteger(9876543210987654321UL);
        var aPub = BigInteger.ModPow(BregoClient.G, a, BregoClient.N);
        var secretBlock = "deterministic-secret-block"u8.ToArray();
        const string timestamp = "Tue Apr 15 10:00:00 UTC 2031";

        var first = BregoClient.ComputePasswordClaimSignature(
            PoolName,
            CognitoUsername,
            Password,
            a,
            aPub,
            ServerPublicB,
            SaltInt,
            secretBlock,
            timestamp);
        var second = BregoClient.ComputePasswordClaimSignature(
            PoolName,
            CognitoUsername,
            Password,
            a,
            aPub,
            ServerPublicB,
            SaltInt,
            secretBlock,
            timestamp);

        Assert.Equal(first, second);
        Assert.Equal(44, first.Length);
    }

    /// <summary>
    /// R1290: with a password saved and no stored sign-in, the provider signs
    /// in with the password on the approved device, mints a session and
    /// stores the sign-in for the next press.
    /// </summary>
    [Fact]
    public async Task WithAPasswordAndNoStoredSignInThePasswordSignsInAndIsStored()
    {
        var login = new InMemoryBregoLogin(Email, null, null, Password);
        var harness = Harness.Create(login: login);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(
            [
                "/login/api/getAuthorizer",
                "/",
                "/",
                "/login/api/brego/users/me/device-approval/challenge",
                "/login/api/brego/v1/sessions",
                "/platform/api/brego/vehicles/vrm/ab12cde",
                "/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations",
            ],
            harness.Requests.Select(request => request.Uri.AbsolutePath).ToArray());
        Assert.Equal(InitiateAuthTarget, Assert.Single(harness.Requests[1].Headers["X-Amz-Target"]));
        Assert.Equal(RespondTarget, Assert.Single(harness.Requests[2].Headers["X-Amz-Target"]));
        using (var init = JsonDocument.Parse(harness.Requests[1].Body!))
        {
            Assert.Equal("USER_SRP_AUTH", init.RootElement.GetProperty("AuthFlow").GetString());
            Assert.Equal(Email, init.RootElement.GetProperty("AuthParameters").GetProperty("USERNAME").GetString());
        }

        using (var verify = JsonDocument.Parse(harness.Requests[2].Body!))
        {
            var responses = verify.RootElement.GetProperty("ChallengeResponses");
            Assert.Equal("PASSWORD_VERIFIER", verify.RootElement.GetProperty("ChallengeName").GetString());
            Assert.Equal(CognitoUsername, responses.GetProperty("USERNAME").GetString());
            Assert.Equal("Tue Apr 15 10:00:00 UTC 2031", responses.GetProperty("TIMESTAMP").GetString());
            Assert.NotEmpty(responses.GetProperty("PASSWORD_CLAIM_SIGNATURE").GetString()!);
        }

        Assert.DoesNotContain(harness.Requests, request => request.Body?.Contains(Password, StringComparison.Ordinal) == true);
        Assert.Equal(1, login.Saves);
        Assert.Equal(PasswordRefreshToken, login.StoredRefreshToken);
        Assert.Equal(BregoSessionCookie, login.Cookie);
        Assert.Empty(harness.Logger.Messages);
    }

    [Fact]
    public async Task ARefreshTokenCognitoRefusesFallsBackToTheSavedPassword()
    {
        var login = new InMemoryBregoLogin(Email, PersistedCookie, RefreshToken, Password);
        var harness = Harness.Create(
            request => IsTokenRenewal(request)
                ? Json(HttpStatusCode.BadRequest, """{"__type":"NotAuthorizedException","message":"Refresh Token has been revoked"}""")
                : null,
            login: login);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(
            [RefreshTarget, InitiateAuthTarget, RespondTarget],
            harness.Requests
                .Where(request => request.Uri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal))
                .Select(request => Assert.Single(request.Headers["X-Amz-Target"]))
                .ToArray());
        Assert.Contains(harness.Requests, request => request.Uri.AbsolutePath == "/login/api/brego/v1/sessions");
        Assert.Equal(1, login.Saves);
        Assert.Equal(PasswordRefreshToken, login.StoredRefreshToken);
        Assert.Equal(BregoSessionCookie, login.Cookie);
    }

    [Fact]
    public async Task WithoutAPasswordARefusedRefreshTokenIsUnavailableAndNoPasswordIsTried()
    {
        var harness = Harness.Create(request => IsTokenRenewal(request)
            ? Json(HttpStatusCode.BadRequest, """{"__type":"NotAuthorizedException","message":"Invalid Refresh Token"}""")
            : null);

        await AssertUnavailableAsync(
            harness,
            "brego.valuation.forbidden stage=cognito_refresh status=400 error=NotAuthorizedException; Invalid Refresh Token");
        Assert.DoesNotContain(harness.Requests, request => request.Headers.TryGetValue("X-Amz-Target", out var target)
            && target.Contains(InitiateAuthTarget));
    }

    [Fact]
    public async Task AWrongPasswordIsUnavailableAtStagePasswordAndStoresNothing()
    {
        var login = new InMemoryBregoLogin(Email, null, null, Password);
        var harness = Harness.Create(
            request => HasTarget(request, RespondTarget)
                ? Json(HttpStatusCode.BadRequest, """{"__type":"NotAuthorizedException","message":"Incorrect username or password."}""")
                : null,
            login: login);

        await AssertUnavailableAsync(
            harness,
            "brego.valuation.forbidden stage=password status=400 error=NotAuthorizedException; Incorrect username or password.");
        Assert.Equal(0, login.Saves);
        Assert.DoesNotContain(harness.Requests, request => request.Uri.AbsolutePath.Contains("device-approval", StringComparison.Ordinal));
    }

    [Fact]
    public async Task APasswordSignInOnADeviceBregoWantsACodeForIsUnavailableAndKeepsNothing()
    {
        var login = new InMemoryBregoLogin(Email, null, null, Password);
        var harness = Harness.Create(
            request => request.RequestUri!.AbsolutePath == "/login/api/brego/users/me/device-approval/challenge"
                ? Json(HttpStatusCode.OK, """{"status":"challenge_required","destination":"di****@c***.co.uk"}""")
                : null,
            login: login);

        await AssertUnavailableAsync(harness, "brego.valuation.device_approval stage=device_approval device=challenge_required");
        Assert.Equal(0, login.Saves);
        Assert.Null(login.StoredRefreshToken);
        Assert.DoesNotContain(harness.Requests, request => request.Uri.AbsolutePath == "/login/api/brego/v1/sessions");
    }

    /// <summary>
    /// R1295: a press whose refresh token Cognito refuses, on an account with
    /// no password, signs in again from Brego's e-mail once and values.
    /// </summary>
    [Fact]
    public async Task ARefusedRefreshTokenIsRepairedOnceFromBregosEmailAndTheValuationAnswers()
    {
        var login = new InMemoryBregoLogin(Email, PersistedCookie, RefreshToken);
        var mail = new FakeBregoMailReader { Link = MailedLink };
        var harness = Harness.Create(
            request => HasTarget(request, RefreshTarget)
                ? Json(HttpStatusCode.BadRequest, """{"__type":"NotAuthorizedException","message":"Refresh Token has been revoked"}""")
                : null,
            login: login,
            mail: mail);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        Assert.Single(harness.Requests, request => IsLinkRequest(request.Body));
        var redeemed = Assert.Single(
            harness.Requests,
            request => request.Body?.Contains("\"action\":\"redeem\"", StringComparison.Ordinal) == true);
        Assert.Contains(MailToken, redeemed.Body!, StringComparison.Ordinal);
        Assert.Equal(1, login.Restores);
        Assert.Equal(0, login.Saves);
        Assert.Equal(MailRefreshToken, login.StoredRefreshToken);
        Assert.Equal(BregoSessionCookie, login.Cookie);
        AssertNoMailSecretsLogged(harness);
    }

    [Fact]
    public async Task ADeviceBregoWantsACodeForIsApprovedWithTheCodeFromBregosEmail()
    {
        var login = new InMemoryBregoLogin(Email, null, RefreshToken);
        var mail = new FakeBregoMailReader { Code = MailCode };
        var approved = false;
        var harness = Harness.Create(
            request =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path == "/login/api/brego/users/me/device-approval/challenge")
                {
                    return Json(HttpStatusCode.OK, approved ? DeviceApprovedJson : """{"status":"challenge_required"}""");
                }

                if (path == "/login/api/brego/users/me/device-approval/verify")
                {
                    approved = true;
                    return Json(HttpStatusCode.OK, "{}");
                }

                return null;
            },
            login: login,
            mail: mail);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Equal(14250m, quote.RetailValue);
        var verified = Assert.Single(
            harness.Requests,
            request => request.Uri.AbsolutePath == "/login/api/brego/users/me/device-approval/verify");
        using (var body = JsonDocument.Parse(verified.Body!))
        {
            Assert.Equal(MailCode, body.RootElement.GetProperty("code").GetString());
        }

        Assert.DoesNotContain(harness.Requests, request => IsLinkRequest(request.Body));
        Assert.Equal(1, login.Restores);
        Assert.Equal(RefreshToken, login.StoredRefreshToken);
        Assert.Equal(BregoSessionCookie, login.Cookie);
        AssertNoMailSecretsLogged(harness);
    }

    [Fact]
    public async Task TwoPressesThatFindTheSignInGoneAskBregoForOneLink()
    {
        var login = new InMemoryBregoLogin(Email, null, null);
        var mail = new FakeBregoMailReader { Link = MailedLink };
        var harness = Harness.Create(login: login, mail: mail);

        var quotes = await Task.WhenAll(
            harness.Provider.GetAsync(Request("AB12CDE", April), default),
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.All(quotes, quote => Assert.Equal(14250m, quote.RetailValue));
        Assert.Single(harness.Requests, request => IsLinkRequest(request.Body));
        Assert.Equal(1, login.Restores);
    }

    [Theory]
    [InlineData("no sign-in")]
    [InlineData("refused")]
    [InlineData("code")]
    public async Task WhereTheMailboxCannotBeReadNothingIsAskedOfBregoAndTheStageIsAsBefore(string row)
    {
        var mail = new FakeBregoMailReader { Readable = false, Link = MailedLink, Code = MailCode };
        var harness = row switch
        {
            "no sign-in" => Harness.Create(login: new InMemoryBregoLogin(Email, null, null), mail: mail),
            "refused" => Harness.Create(
                request => HasTarget(request, RefreshTarget)
                    ? Json(HttpStatusCode.BadRequest, """{"__type":"NotAuthorizedException","message":"Invalid Refresh Token"}""")
                    : null,
                mail: mail),
            _ => Harness.Create(
                request => request.RequestUri!.AbsolutePath == "/login/api/brego/users/me/device-approval/challenge"
                    ? Json(HttpStatusCode.OK, """{"status":"challenge_required"}""")
                    : null,
                mail: mail),
        };

        await AssertUnavailableAsync(
            harness,
            row switch
            {
                "no sign-in" => "brego.valuation.unreadable stage=sign_in refresh_token=absent",
                "refused" => "brego.valuation.forbidden stage=cognito_refresh status=400 error=NotAuthorizedException; Invalid Refresh Token",
                _ => "brego.valuation.device_approval stage=device_approval device=challenge_required",
            });
        Assert.DoesNotContain(harness.Requests, request => IsLinkRequest(request.Body));
        Assert.DoesNotContain(harness.Requests, request => request.Uri.AbsolutePath.EndsWith("/verify", StringComparison.Ordinal));
        Assert.Equal(0, mail.LinkReads);
        Assert.Equal(0, mail.CodeReads);
    }

    [Fact]
    public async Task ALinkThatNeverArrivesIsUnavailableAtMailLinkAndKeepsNothing()
    {
        var login = new InMemoryBregoLogin(Email, null, null);
        var mail = new FakeBregoMailReader();
        var harness = Harness.Create(login: login, mail: mail);

        await AssertUnavailableAsync(harness, "brego.valuation.unreadable stage=mail_link mail=not_arrived");

        Assert.Single(harness.Requests, request => IsLinkRequest(request.Body));
        Assert.InRange(mail.LinkReads, 2, 16);
        Assert.Equal(0, login.Restores);
        Assert.Equal(0, login.Saves);
        Assert.Null(login.StoredRefreshToken);
    }

    private static bool IsLinkRequest(string? body) =>
        body?.Contains("\"action\":\"request\"", StringComparison.Ordinal) == true;

    private static void AssertNoMailSecretsLogged(Harness harness) =>
        Assert.DoesNotContain(harness.Logger.Messages, message =>
            message.Contains(MailToken, StringComparison.Ordinal)
            || message.Contains(MailCode, StringComparison.Ordinal)
            || message.Contains(MailRefreshToken, StringComparison.Ordinal)
            || message.Contains(RefreshToken, StringComparison.Ordinal)
            || message.Contains(AccessToken, StringComparison.Ordinal)
            || message.Contains(BregoSessionCookie, StringComparison.Ordinal));

    [Theory]
    [InlineData("2031-02", "2031-02-28", "2031-02")]
    [InlineData("2028-02", "2028-02-29", "2028-02")]
    [InlineData("2030-12", "2030-12-31", "2030-12")]
    [InlineData("2031-06", "2031-04-15", "2031-04")]
    public async Task AMonthIsValuedOnItsLastDayOrTodayAndNeverAhead(
        string month, string valuationDate, string answeredMonth)
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", Month(month)), default);

        Assert.Equal(Month(answeredMonth), quote.GuideMonth);
        Assert.Contains($"dates={valuationDate}", harness.Requests[^1].Uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AKilometreReadingIsSentInWholeMiles()
    {
        var harness = Harness.Create();

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April, 53108, "km"), default);

        Assert.Contains("mileage=33000", harness.Requests[^1].Uri.Query, StringComparison.Ordinal);
        Assert.Equal(53108, quote.Mileage);
    }

    [Theory]
    [InlineData("/platform/api/brego/vehicles/vrm/ab12cde", "lookup")]
    [InlineData("/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations", "valuation")]
    public async Task ARegistrationBregoHoldsNoDataForIsNotValuedRatherThanUnavailable(string path, string stage)
    {
        var harness = Harness.Create(request => request.RequestUri!.AbsolutePath == path
            ? Json(HttpStatusCode.NotFound, """{"message":"Vehicle not found"}""")
            : null);

        var refused = await Assert.ThrowsAsync<GuideValuationNotValuedException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.Equal(GuideValuationNotValuedReason.NoVehicleData, refused.Reason);
        Assert.Equal(ValuationSource.Brego, refused.ValuationSource);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains($"brego.valuation.not_found stage={stage} status=404 error=Vehicle not found", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(400, "/login/api/getAuthorizer", """{"message":"Invalid email"}""", "brego.valuation.rejected stage=authorizer status=400 error=Invalid email")]
    [InlineData(400, "/", """{"__type":"NotAuthorizedException","message":"Invalid Refresh Token"}""", "brego.valuation.forbidden stage=cognito_refresh status=400 error=NotAuthorizedException; Invalid Refresh Token")]
    [InlineData(401, "/login/api/brego/v1/sessions", """{"message":"Invalid token"}""", "brego.valuation.forbidden stage=session status=401 error=Invalid token")]
    [InlineData(429, "/platform/api/brego/vehicles/vrm/ab12cde", """{"message":"Rate limit exceeded"}""", "brego.valuation.throttled stage=lookup status=429 error=Rate limit exceeded")]
    [InlineData(500, "/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations", """{"message":"Internal server error"}""", "brego.valuation.provider_failed stage=valuation status=500 error=Internal server error")]
    public async Task EveryOtherRefusalIsUnavailableAndLoggedByItsCodeAndStage(
        int status, string failingPath, string body, string logged)
    {
        var harness = Harness.Create(request => request.RequestUri!.AbsolutePath == failingPath
            ? Json((HttpStatusCode)status, body)
            : null);

        await AssertUnavailableAsync(harness, logged);
    }

    [Theory]
    [InlineData("not a token", "synthetic-cognito-id-token")]
    [InlineData("no slug", """{"organizationId":"876db4e2-68f3-49a2-a824-cc2e44076a57"}""")]
    [InlineData("no organisation", """{"slug":"collision-engineers"}""")]
    [InlineData("slug is not a host label", """{"organizationId":"876db4e2-68f3-49a2-a824-cc2e44076a57","slug":"evil.example/"}""")]
    public async Task AnIdTokenThatDoesNotNameTheTenantIsUnavailableBeforeAnySessionCall(string row, string claims)
    {
        Assert.NotEmpty(row);
        var idToken = claims.StartsWith('{') ? SyntheticIdToken(claims) : claims;
        var harness = Harness.Create(request =>
            IsTokenRenewal(request)
                ? Json(HttpStatusCode.OK, RefreshTokensJson.Replace(IdToken, idToken, StringComparison.Ordinal))
                : null);

        await AssertUnavailableAsync(harness, "brego.valuation.unreadable stage=cognito_refresh tenant=absent");
        Assert.Equal(2, harness.Requests.Count);
    }

    [Theory]
    [InlineData("no username", "username=absent")]
    [InlineData("no tokens", "tokens=absent")]
    public async Task ARenewalWithoutTheTokensOrTheUsernameIsUnavailableBeforeAnySessionCall(string row, string detail)
    {
        var answer = row == "no tokens"
            ? """{"AuthenticationResult":{"ExpiresIn":86400,"TokenType":"Bearer"}}"""
            : RefreshTokensJson.Replace(
                IdToken,
                SyntheticIdToken($$"""{"organizationId":"{{OrganizationId}}","slug":"{{Slug}}"}"""),
                StringComparison.Ordinal);
        var harness = Harness.Create(request => IsTokenRenewal(request) ? Json(HttpStatusCode.OK, answer) : null);

        await AssertUnavailableAsync(harness, $"brego.valuation.unreadable stage=cognito_refresh {detail}");
        Assert.Equal(2, harness.Requests.Count);
    }

    [Fact]
    public async Task DeviceApprovalChallengeRequiredIsUnavailableAndNamedAsDeviceApproval()
    {
        var harness = Harness.Create(request =>
            request.RequestUri!.AbsolutePath == "/login/api/brego/users/me/device-approval/challenge"
                ? Json(HttpStatusCode.OK, """{"status":"challenge_required","destination":"di****@c***.co.uk"}""")
                : null);

        await AssertUnavailableAsync(harness, "brego.valuation.device_approval stage=device_approval device=challenge_required");
    }

    [Theory]
    [InlineData("trade missing", """{"items":[{"retail":{"high":14250},"trade":{}}]}""")]
    [InlineData("trade zero", """{"items":[{"retail":{"high":14250},"trade":{"high":0}}]}""")]
    [InlineData("retail string", """{"items":[{"retail":{"high":"14250"},"trade":{"high":11890}}]}""")]
    public async Task AnAnswerWithoutBothPositiveHighFiguresIsUnavailable(string row, string body)
    {
        Assert.NotEmpty(row);
        var harness = Harness.Create(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/valuations", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, body)
                : null);

        await AssertUnavailableAsync(harness, "brego.valuation.unreadable stage=valuation figures=absent");
    }

    [Theory]
    [InlineData("SB1ZB3AE4********")]
    [InlineData("SHORT")]
    [InlineData("")]
    public async Task AMaskedOrIncompleteVinIsNotCarriedOnTheQuote(string vin)
    {
        var harness = Harness.Create(request =>
            request.RequestUri!.AbsolutePath == "/platform/api/brego/vehicles/vrm/ab12cde"
                ? Json(HttpStatusCode.OK, LookupJson(vin))
                : null);

        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);

        Assert.Null(quote.Vin);
    }

    [Fact]
    public async Task AnAnswerThatIsNotJsonIsUnavailable()
    {
        var harness = Harness.Create(request =>
            request.RequestUri!.AbsolutePath.Contains("/vehicles/vrm/", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, "<html>maintenance</html>")
                : null);

        await AssertUnavailableAsync(harness, "brego.valuation.unreadable stage=lookup json=invalid");
    }

    [Theory]
    [InlineData("authorizer", "/login/api/getAuthorizer", 1)]
    [InlineData("cognito_refresh", "/", 2)]
    [InlineData("device_approval", "/login/api/brego/users/me/device-approval/challenge", 3)]
    [InlineData("session", "/login/api/brego/v1/sessions", 4)]
    [InlineData("lookup", "/platform/api/brego/vehicles/vrm/ab12cde", 5)]
    [InlineData("valuation", "/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations", 6)]
    public async Task ATransportFailureOrATimeoutIsUnavailableAtItsStageAndStopsTheCalls(
        string stage, string failingPath, int calls)
    {
        foreach (Exception failure in new Exception[]
        {
            new HttpRequestException("connection refused"),
            new TaskCanceledException("timed out"),
        })
        {
            var harness = Harness.Create(request =>
            {
                if (request.RequestUri!.AbsolutePath == failingPath)
                {
                    throw failure;
                }

                return null;
            });

            await AssertUnavailableAsync(
                harness, $"brego.valuation.transport stage={stage} transport={failure.GetType().Name}");

            Assert.Equal(calls, harness.Requests.Count);
            var logged = Assert.Single(harness.Logger.Exceptions);
            Assert.IsType<BregoFailure>(logged);
            Assert.IsType(failure.GetType(), logged.InnerException);
        }
    }

    [Fact]
    public async Task AnUnreadableMileageUnitIsUnavailableAndAsksNothing()
    {
        var harness = Harness.Create();

        await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April, 33000, "furlongs"), default));

        Assert.Empty(harness.Requests);
        Assert.Contains(harness.Logger.Messages, message =>
            message.Contains("brego.valuation.mileage_unit stage=mileage unit=unreadable", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALoginNotSetIsUnavailableAndNamesTheLoginNotAValue()
    {
        var harness = Harness.Create(account: _ => Task.FromResult(BregoValuationAccount.Create(null)));

        await AssertUnavailableAsync(
            harness,
            "brego.valuation.configuration The Brego valuation login is not set.");
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public void TheValuationAccountIsItsEmailAndStoredSignInAndRefusesAMissingBlankOrControlEmail()
    {
        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(null));
        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(
            TestIntegrationLogins.Brego(" ", RefreshToken, null)));
        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(
            TestIntegrationLogins.Brego("digital@\ncollisionengineers.test", RefreshToken, null)));
        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(
            TestIntegrationLogins.Cazana(Email, "secret")));

        var account = BregoValuationAccount.Create(
            TestIntegrationLogins.Brego($" {Email} ", $" {RefreshToken} ", " ", generation: 3));
        Assert.Equal(Email, account.Email);
        Assert.Equal(RefreshToken, account.RefreshToken);
        Assert.Null(account.SessionCookie);
        Assert.Equal(3, account.Generation);
        Assert.Equal(nameof(BregoValuationAccount), account.ToString());
    }

    [Fact]
    public void NeitherAStoredSignInNorASessionPrintsItsTokensOrCookie()
    {
        var stored = new BregoStoredSignIn(BregoSessionCookie, RefreshToken);
        var session = new BregoSession(
            Email,
            RefreshToken,
            Slug,
            OrganizationId,
            BregoSessionCookie,
            $"brego-session={BregoSessionCookie}",
            Now);

        Assert.Equal(nameof(BregoStoredSignIn), stored.ToString());
        Assert.Equal(nameof(BregoSession), session.ToString());
    }

    [Fact]
    public void AddBregoGuideValuationComposesOneProviderAndReadsTheLoginOnlyWhenValuing()
    {
        // The composition-only database does not exist, so a read at
        // resolution would fail this test (R1288).
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        TestIntegrationLogins.AddCompositionOnlyStore(services);
        services.AddBregoGuideValuation();
        using var provider = services.BuildServiceProvider();

        var source = Assert.Single(provider.GetServices<IGuideValuationProvider>());

        Assert.IsType<BregoGuideValuationProvider>(source);
        Assert.Equal(ValuationSource.Brego, source.Source);
        Assert.IsType<Pegasus.Infrastructure.Persistence.EfIntegrationLoginStore>(
            provider.GetRequiredService<IBregoSessionStore>());
        var link = Assert.IsType<BregoAccountLink>(provider.GetRequiredService<IBregoAccountLink>());
        Assert.False(link.ReadsSignInMail);
    }

    internal static string LookupJson(string vin = Vin) => $$$"""
        {
          "vrm": "AB12CDE",
          "vin": "{{{vin}}}",
          "derivativeId": "{{{DerivativeId}}}",
          "makeModelDerivative": "Toyota Corolla 1.8 VVT-h Icon Touring Sports 5dr",
          "manufacturer": "Toyota",
          "manufacturerUrl": "toyota",
          "range": "Corolla",
          "rangeUrl": "corolla",
          "year": 2024,
          "color": "Silver",
          "registrationDate": "2024-01-04T00:00:00.000Z",
          "previousOwnerChanges": [
            { "previousOwnerCount": 1 },
            { "previousOwnerCount": 2 }
          ],
          "metadata": [
            { "region": "national", "averageMileageMonthly": 650 }
          ]
        }
        """;

    internal const string ValuationJson = """
        {
          "currencyCode": "gbp",
          "mileageUnit": "mi",
          "items": [
            {
              "date": "2031-04-15",
              "mileage": 33000,
              "retail": { "low": 13100, "average": 13650, "high": 14250 },
              "trade": { "low": 10800, "average": 11350, "high": 11890 }
            }
          ]
        }
        """;

    internal static HttpResponseMessage DefaultAnswer(HttpRequestMessage request, string? body = null)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/login/api/getAuthorizer")
        {
            return Json(HttpStatusCode.OK, AuthorizerJson);
        }

        if (request.RequestUri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal))
        {
            // Token renewal and the two password exchanges are answered: any
            // other Cognito call is refused.
            if (IsTokenRenewal(request))
            {
                return Json(HttpStatusCode.OK, RefreshTokensJson);
            }

            if (HasTarget(request, InitiateAuthTarget))
            {
                return Json(
                    HttpStatusCode.OK,
                    body?.Contains("CUSTOM_AUTH", StringComparison.Ordinal) == true ? MailCustomChallengeJson : SrpChallengeJson);
            }

            if (HasTarget(request, RespondTarget) && body?.Contains("\"action\":\"request\"", StringComparison.Ordinal) == true)
            {
                return Json(HttpStatusCode.OK, MailLinkSentJson);
            }

            if (HasTarget(request, RespondTarget) && body?.Contains("\"action\":\"redeem\"", StringComparison.Ordinal) == true)
            {
                return Json(HttpStatusCode.OK, MailLinkTokensJson);
            }

            return HasTarget(request, RespondTarget)
                ? Json(HttpStatusCode.OK, PasswordTokensJson)
                : Json(HttpStatusCode.BadRequest, """{"__type":"InvalidParameterException","message":"Unexpected call"}""");
        }

        if (path == "/login/api/brego/users/me/device-approval/challenge")
        {
            return Json(HttpStatusCode.OK, DeviceApprovedJson);
        }

        if (path == "/login/api/brego/v1/sessions")
        {
            var response = Json(HttpStatusCode.OK, SessionJson);
            response.Headers.TryAddWithoutValidation("Set-Cookie", "brego-session=; Path=/; Max-Age=0");
            response.Headers.TryAddWithoutValidation("Set-Cookie", $"brego-session={BregoSessionCookie}; Path=/; HttpOnly; Secure");
            return response;
        }

        if (path.EndsWith("/valuations", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, ValuationJson);
        }

        if (path.StartsWith("/platform/api/brego/vehicles/vrm/", StringComparison.Ordinal))
        {
            return Json(HttpStatusCode.OK, LookupJson());
        }

        return Json(HttpStatusCode.NotFound, "{}");
    }

    internal static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static bool IsTokenRenewal(HttpRequestMessage request) => HasTarget(request, RefreshTarget);

    internal static bool HasTarget(HttpRequestMessage request, string target) =>
        request.RequestUri!.Host.StartsWith("cognito-idp.", StringComparison.Ordinal)
        && request.Headers.TryGetValues("X-Amz-Target", out var targets)
        && targets.Contains(target);

    private static async Task AssertUnavailableAsync(Harness harness, string logged)
    {
        var refused = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.NotNull(refused.InnerException);
        Assert.Contains(harness.Logger.Messages, message => message.Contains(logged, StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message =>
            message.Contains(RefreshToken, StringComparison.Ordinal)
            || message.Contains(PasswordRefreshToken, StringComparison.Ordinal)
            || message.Contains(Password, StringComparison.Ordinal)
            || message.Contains(AccessToken, StringComparison.Ordinal)
            || message.Contains(IdToken, StringComparison.Ordinal)
            || message.Contains(BregoSessionCookie, StringComparison.Ordinal)
            || message.Contains(PersistedCookie, StringComparison.Ordinal));
    }

    private static DateOnly Month(string month) =>
        DateOnly.ParseExact(month + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static GuideValuationRequest Request(
        string registration, DateOnly month, long mileage = 33000, string? unit = null) => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
        Guid.NewGuid(),
        registration,
        mileage,
        unit,
        month);

    internal sealed class Harness(
        BregoGuideValuationProvider provider,
        CapturingLogger logger,
        List<CazanaGuideValuationProviderTests.RecordedRequest> requests)
    {
        public BregoGuideValuationProvider Provider { get; } = provider;

        public CapturingLogger Logger { get; } = logger;

        public List<CazanaGuideValuationProviderTests.RecordedRequest> Requests { get; } = requests;

        public static Harness Create(
            Func<HttpRequestMessage, HttpResponseMessage?>? overrideRequest = null,
            Func<CancellationToken, Task<BregoValuationAccount>>? account = null,
            TimeProvider? timeProvider = null,
            InMemoryBregoLogin? login = null,
            bool withoutStore = false,
            FakeBregoMailReader? mail = null)
        {
            // By default the account is linked: its refresh token is stored
            // and no session cookie has been minted yet.
            login ??= new InMemoryBregoLogin(Email, null, RefreshToken);
            // A press that waits for Brego's e-mail polls on the clock, so a
            // harness that reads mail steps its clock instead of waiting.
            var clock = timeProvider
                ?? (mail is null ? (TimeProvider)new CazanaGuideValuationProviderTests.FixedClock(Now) : new SteppingClock(Now));
            var requests = new List<CazanaGuideValuationProviderTests.RecordedRequest>();
            var logger = new CapturingLogger();
            return new(
                new BregoGuideValuationProvider(
                    new ScriptedClientFactory(new CazanaGuideValuationProviderTests.Answering(async request =>
                    {
                        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                        lock (requests)
                        {
                            requests.Add(new(
                                request.Method,
                                request.RequestUri!,
                                request.Headers.Authorization?.ToString(),
                                request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                                body));
                        }

                        if (overrideRequest?.Invoke(request) is { } overridden)
                        {
                            return overridden;
                        }

                        return DefaultAnswer(request, body);
                    })),
                    account ?? (cancellationToken => login.ReadAsync(cancellationToken)),
                    clock,
                    logger,
                    withoutStore ? null : login)
                {
                    MailSignIn = mail is null ? null : new BregoMailSignIn(mail, clock)
                },
                logger,
                requests);
        }
    }

    internal sealed class CapturingLogger : ILogger<BregoGuideValuationProvider>
    {
        public List<string> Messages { get; } = [];

        public List<Exception> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (exception is not null)
            {
                Exceptions.Add(exception);
            }
        }
    }

    private sealed class DelegateClock(Func<DateTimeOffset> now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now();
    }

    /// <summary>
    /// The Brego login's row as the store keeps it (R1288): the account, its
    /// stored sign-in and its generation, read whole on each press and
    /// renewed in place only while the generation is unchanged.
    /// </summary>
    internal sealed class InMemoryBregoLogin(
        string email,
        string? cookie,
        string? refreshToken,
        string? password = null) : IBregoSessionStore
    {
        public string Email { get; private set; } = email;

        public string? Password { get; } = password;

        public string? Cookie { get; private set; } = cookie;

        public string? StoredRefreshToken { get; private set; } = refreshToken;

        public long Generation { get; private set; } = 1;

        public int Saves { get; private set; }

        /// <summary>An Administrator replaces the account: its sign-in is cleared.</summary>
        public void Replace(string email)
        {
            Email = email;
            Cookie = null;
            StoredRefreshToken = null;
            Generation++;
        }

        /// <summary>The account linked again: a new refresh token and cookie.</summary>
        public void Link(string? cookie, string refreshToken)
        {
            Cookie = cookie;
            StoredRefreshToken = refreshToken;
            Generation++;
        }

        public Task<BregoValuationAccount> ReadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(BregoValuationAccount.Create(
                TestIntegrationLogins.Brego(Email, StoredRefreshToken, Cookie, Generation, Password)));

        public int Restores { get; private set; }

        /// <summary>As the store does: under the next generation, as a link is.</summary>
        public Task<bool> TryStoreRestoredSignInAsync(long generation, BregoStoredSignIn signIn, CancellationToken cancellationToken)
        {
            if (generation != Generation)
            {
                return Task.FromResult(false);
            }

            Cookie = signIn.BregoSessionCookie;
            StoredRefreshToken = signIn.RefreshToken;
            Generation++;
            Restores++;
            return Task.FromResult(true);
        }

        public Task<bool> TrySaveAsync(long generation, BregoStoredSignIn signIn, CancellationToken cancellationToken)
        {
            if (generation != Generation)
            {
                return Task.FromResult(false);
            }

            Cookie = signIn.BregoSessionCookie;
            StoredRefreshToken = signIn.RefreshToken;
            Saves++;
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// A clock whose timers fire at once and move it on by their due time, so
    /// a poll that waits two seconds at a time takes no real time.
    /// </summary>
    internal sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        private readonly Lock guard = new();
        private DateTimeOffset now = start;

        public DateTimeOffset Now
        {
            get
            {
                lock (guard)
                {
                    return now;
                }
            }

            set
            {
                lock (guard)
                {
                    now = value;
                }
            }
        }

        public override DateTimeOffset GetUtcNow() => Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                lock (guard)
                {
                    now += dueTime;
                }

                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }

            return new FiredTimer();
        }

        private sealed class FiredTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => false;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// The account's mailbox as the sign-in mail reader sees it (R1294): the
    /// link and the code arrive on the given read, or never when unset.
    /// </summary>
    internal sealed class FakeBregoMailReader : IBregoSignInMailReader
    {
        public bool Readable { get; set; } = true;

        public string? Link { get; set; }

        public string? Code { get; set; }

        public int ArrivesOnRead { get; set; } = 2;

        public int CanReads { get; private set; }

        public int LinkReads { get; private set; }

        public int CodeReads { get; private set; }

        public List<DateTimeOffset> Since { get; } = [];

        public Task<bool> CanReadAsync(string mailboxAddress, CancellationToken cancellationToken)
        {
            CanReads++;
            return Task.FromResult(Readable);
        }

        public Task<string?> ReadLinkAsync(string mailboxAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
        {
            if (!Readable)
            {
                throw new BregoSignInMailUnreadableException();
            }

            Since.Add(sinceUtc);
            LinkReads++;
            return Task.FromResult(LinkReads >= ArrivesOnRead ? Link : null);
        }

        public Task<string?> ReadCodeAsync(string mailboxAddress, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
        {
            if (!Readable)
            {
                throw new BregoSignInMailUnreadableException();
            }

            Since.Add(sinceUtc);
            CodeReads++;
            return Task.FromResult(CodeReads >= ArrivesOnRead ? Code : null);
        }
    }
}
