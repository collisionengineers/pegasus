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
/// (ADR-0084): Cognito <c>USER_SRP_AUTH</c>, device-approval challenge, BFF
/// session cookie creation and reuse, VRM lookup, point-in-time valuation
/// (<c>retail.high</c> and <c>trade.high</c>), empty-VIN fill, report handle,
/// month-to-date mapping, whole-mile conversion, and stage-named failure codes.
/// </summary>
public sealed class BregoGuideValuationProviderTests
{
    internal const string Email = "digital@collisionengineers.test";
    internal const string Password = "synthetic-brego-password";
    internal const string OrganizationId = "876db4e2-68f3-49a2-a824-cc2e44076a57";
    internal const string Slug = "collision-engineers";
    internal const string PoolId = "eu-west-2_ukSKsqAOY";
    internal const string PoolName = "ukSKsqAOY";
    internal const string ClientId = "2fi40vh0cav05pikjkh04nd9e5";
    internal const string UserIdForSrp = "e6e222b4-a071-7002-cd41-d8b4a27b9eef";
    internal const string AccessToken = "synthetic-cognito-access-token";
    internal const string IdToken = "synthetic-cognito-id-token";
    internal const string RefreshToken = "synthetic-cognito-refresh-token";
    internal const string BregoSessionCookie = "synthetic-brego-session-cookie";
    internal const string DerivativeId = "d8d2bc32-6912-403e-9a43-59d6d0779c11";
    internal const string Vin = "SB1ZB3AE40E045642";

    internal static readonly DateTimeOffset Now = new(2031, 4, 15, 10, 0, 0, TimeSpan.Zero);
    internal static readonly DateOnly April = new(2031, 4, 1);

    private const string SaltHex = "3f7a9c12e4b081652390abcd1234ef01";
    private static readonly string SecretBlockBase64 = Convert.ToBase64String("synthetic-secret-block-bytes"u8);

    // Compute a valid server public ephemeral key B = (k * g^x + g^b) mod N for the test credentials.
    private static readonly BigInteger ServerPrivateB = new(1234567890123456789UL);
    private static readonly BigInteger SaltInt = new(Convert.FromHexString(SaltHex), isUnsigned: true, isBigEndian: true);
    private static readonly BigInteger ServerPublicB = ComputeServerPublicB();

    private static BigInteger ComputeServerPublicB()
    {
        var userPassHash = SHA256.HashData(Encoding.UTF8.GetBytes($"{PoolName}{UserIdForSrp}:{Password}"));
        var saltBytes = BregoClient.PadBytes(SaltInt);
        var xInput = new byte[saltBytes.Length + userPassHash.Length];
        Buffer.BlockCopy(saltBytes, 0, xInput, 0, saltBytes.Length);
        Buffer.BlockCopy(userPassHash, 0, xInput, saltBytes.Length, userPassHash.Length);
        var x = new BigInteger(SHA256.HashData(xInput), isUnsigned: true, isBigEndian: true);
        var verifier = BigInteger.ModPow(BregoClient.G, x, BregoClient.N);
        var gb = BigInteger.ModPow(BregoClient.G, ServerPrivateB, BregoClient.N);
        return ((BregoClient.K * verifier) + gb) % BregoClient.N;
    }

    internal static readonly string AuthorizerJson = $$$"""
        [
          {
            "organizationId": "{{{OrganizationId}}}",
            "name": "Collision Engineers",
            "slug": "{{{Slug}}}",
            "authorizer": "{{{PoolId}}}",
            "authorizerId": "{{{ClientId}}}"
          }
        ]
        """;

    internal static readonly string InitiateAuthJson = $$$"""
        {
          "ChallengeName": "PASSWORD_VERIFIER",
          "ChallengeParameters": {
            "USER_ID_FOR_SRP": "{{{UserIdForSrp}}}",
            "USERNAME": "{{{UserIdForSrp}}}",
            "SALT": "{{{SaltHex}}}",
            "SRP_B": "{{{BregoClient.ToUnsignedHexLower(ServerPublicB)}}}",
            "SECRET_BLOCK": "{{{SecretBlockBase64}}}"
          }
        }
        """;

    internal static readonly string RespondToAuthChallengeJson = $$$"""
        {
          "AuthenticationResult": {
            "AccessToken": "{{{AccessToken}}}",
            "IdToken": "{{{IdToken}}}",
            "RefreshToken": "{{{RefreshToken}}}",
            "ExpiresIn": 86400
          }
        }
        """;

    internal const string DeviceApprovedJson = """{"status":"approved"}""";

    internal static readonly string SessionJson = $$$"""
        {
          "sessionId": "b0d2c68e-7394-49bb-aed4-921fd7ae8435",
          "organizationId": "{{{OrganizationId}}}"
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
                "POST https://cognito-idp.eu-west-2.amazonaws.com/",
                "POST https://www.brego.ai/login/api/brego/users/me/device-approval/challenge",
                "POST https://www.brego.ai/login/api/brego/v1/sessions",
                "GET https://collision-engineers.brego.ai/platform/api/brego/vehicles/vrm/ab12cde?countryCode=gb&expand=sources&expand=metadata",
                "GET https://collision-engineers.brego.ai/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations?countryCode=gb&dates=2031-04-15&period=month&futureMonthlyMileage=650&standardOptionsValue=0&fullOptionsValue=0&significantOptionsValue=0&excludeTax=false&excludeUserHistory=false&mileage=33000&color=Silver&year=2024&previousOwners=2&registrationDate=2024-01-04",
            ],
            harness.Requests.Select(request => $"{request.Method} {request.Uri}").ToArray());

        Assert.Contains(
            "AWSCognitoIdentityProviderService.InitiateAuth",
            harness.Requests[1].Headers["X-Amz-Target"]);
        Assert.Contains(
            "AWSCognitoIdentityProviderService.RespondToAuthChallenge",
            harness.Requests[2].Headers["X-Amz-Target"]);

        using var verifyDoc = JsonDocument.Parse(harness.Requests[2].Body!);
        var challengeResponses = verifyDoc.RootElement.GetProperty("ChallengeResponses");
        Assert.Equal(UserIdForSrp, challengeResponses.GetProperty("USERNAME").GetString());
        Assert.Equal("Tue Apr 15 10:00:00 UTC 2031", challengeResponses.GetProperty("TIMESTAMP").GetString());
        Assert.NotEmpty(challengeResponses.GetProperty("PASSWORD_CLAIM_SIGNATURE").GetString()!);

        Assert.Contains(AccessToken, harness.Requests[3].Headers["X-Brego-Bootstrap-Access-Token"]);
        Assert.Contains(AccessToken, harness.Requests[4].Headers["X-Brego-Bootstrap-Access-Token"]);

        foreach (var platformCall in harness.Requests.Skip(5))
        {
            var cookie = Assert.Single(platformCall.Headers["Cookie"]);
            Assert.Contains($"brego-session={BregoSessionCookie}", cookie, StringComparison.Ordinal);
            Assert.Contains($"CognitoIdentityServiceProvider.{ClientId}.{UserIdForSrp}.accessToken={AccessToken}", cookie, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            harness.Requests,
            request => request.Uri.AbsoluteUri.Contains(Password, StringComparison.Ordinal)
                || request.Uri.AbsoluteUri.Contains(AccessToken, StringComparison.Ordinal)
                || request.Uri.AbsoluteUri.Contains(BregoSessionCookie, StringComparison.Ordinal));
        Assert.Empty(harness.Logger.Messages);
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
            UserIdForSrp,
            Password,
            a,
            aPub,
            ServerPublicB,
            SaltInt,
            secretBlock,
            timestamp);
        var second = BregoClient.ComputePasswordClaimSignature(
            PoolName,
            UserIdForSrp,
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
        Assert.Equal(7, harness.Requests.Count);

        // Second call reuses the session without calling login/Cognito again.
        await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(9, harness.Requests.Count);

        // Third call receives 401 on the reused session, refreshes the session once, and succeeds.
        failFirstReusedLookup = true;
        var quote = await harness.Provider.GetAsync(Request("AB12CDE", April), default);
        Assert.Equal(14250m, quote.RetailValue);
        Assert.Equal(17, harness.Requests.Count);
    }

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
    [InlineData(400, "/", """{"__type":"NotAuthorizedException","message":"Incorrect username or password."}""", "brego.valuation.forbidden stage=cognito_init status=400 error=NotAuthorizedException; Incorrect username or password.")]
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
    [InlineData("cognito_init", "/", 2)]
    [InlineData("device_approval", "/login/api/brego/users/me/device-approval/challenge", 4)]
    [InlineData("session", "/login/api/brego/v1/sessions", 5)]
    [InlineData("lookup", "/platform/api/brego/vehicles/vrm/ab12cde", 6)]
    [InlineData("valuation", "/platform/api/brego/vehicles/vin/sb1zb3ae40e045642/valuations", 7)]
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

    [Theory]
    [InlineData("Brego:ValuationAccount:Email")]
    [InlineData("Brego:ValuationAccount:Password")]
    public async Task AnUnresolvedAccountSettingIsUnavailableAndNamesTheKeyNotItsValue(string unresolvedKey)
    {
        var reference = "@Microsoft.KeyVault(SecretUri=https://vault.test/secrets/brego-valuation/1)";
        var harness = Harness.Create(
            account: () => BregoValuationAccount.Create(key => key == unresolvedKey ? reference : "configured"));

        await AssertUnavailableAsync(
            harness,
            $"brego.valuation.configuration {unresolvedKey} is an unresolved Key Vault reference");
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public void TheValuationAccountRefusesBlankControlAndUnresolvedValuesAndNeverPrintsItsPassword()
    {
        static Func<string, string?> Read(string? email, string? password) =>
            key => key == "Brego:ValuationAccount:Email" ? email : password;

        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(Read(" ", "secret")));
        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(Read(Email, null)));
        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(Read(Email, "sec\nret")));
        Assert.Throws<InvalidOperationException>(() => BregoValuationAccount.Create(
            Read("@Microsoft.KeyVault(SecretUri=https://example.vault.azure.net/secrets/brego-valuation-email)", "secret")));

        var account = BregoValuationAccount.Create(Read($" {Email} ", " spaced secret "));
        Assert.Equal(Email, account.Email);
        Assert.Equal(" spaced secret ", account.Password);
        Assert.DoesNotContain("secret", account.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AddBregoGuideValuationComposesOneProviderAndReadsTheAccountOnlyWhenValuing()
    {
        var reads = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddBregoGuideValuation(_ =>
        {
            reads++;
            return BregoValuationAccount.Create(static _ => null);
        });
        using var provider = services.BuildServiceProvider();

        var source = Assert.Single(provider.GetServices<IGuideValuationProvider>());

        Assert.IsType<BregoGuideValuationProvider>(source);
        Assert.Equal(ValuationSource.Brego, source.Source);
        Assert.Equal(0, reads);
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

    internal static HttpResponseMessage DefaultAnswer(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path == "/login/api/getAuthorizer")
        {
            return Json(HttpStatusCode.OK, AuthorizerJson);
        }

        if (request.RequestUri.Host.StartsWith("cognito-idp.", StringComparison.Ordinal))
        {
            var target = request.Headers.TryGetValues("X-Amz-Target", out var targets) ? targets.FirstOrDefault() : null;
            return target == "AWSCognitoIdentityProviderService.InitiateAuth"
                ? Json(HttpStatusCode.OK, InitiateAuthJson)
                : Json(HttpStatusCode.OK, RespondToAuthChallengeJson);
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

    private static async Task AssertUnavailableAsync(Harness harness, string logged)
    {
        var refused = await Assert.ThrowsAsync<GuideValuationProviderUnavailableException>(() =>
            harness.Provider.GetAsync(Request("AB12CDE", April), default));

        Assert.NotNull(refused.InnerException);
        Assert.Contains(harness.Logger.Messages, message => message.Contains(logged, StringComparison.Ordinal));
        Assert.DoesNotContain(harness.Logger.Messages, message =>
            message.Contains(Password, StringComparison.Ordinal)
            || message.Contains(AccessToken, StringComparison.Ordinal)
            || message.Contains(BregoSessionCookie, StringComparison.Ordinal));
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
            Func<BregoValuationAccount>? account = null)
        {
            var requests = new List<CazanaGuideValuationProviderTests.RecordedRequest>();
            var logger = new CapturingLogger();
            return new(
                new BregoGuideValuationProvider(
                    new ScriptedClientFactory(new CazanaGuideValuationProviderTests.Answering(async request =>
                    {
                        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                        requests.Add(new(
                            request.Method,
                            request.RequestUri!,
                            request.Headers.Authorization?.ToString(),
                            request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                            body));
                        if (overrideRequest?.Invoke(request) is { } overridden)
                        {
                            return overridden;
                        }

                        return DefaultAnswer(request);
                    })),
                    account ?? (() => BregoValuationAccount.Create(
                        key => key == "Brego:ValuationAccount:Email" ? Email : Password)),
                    new CazanaGuideValuationProviderTests.FixedClock(Now),
                    logger),
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
}
