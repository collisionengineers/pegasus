using System.Net;
using System.Text;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Vehicle;

namespace Pegasus.IntegrationTests;

public sealed class ProductionVehicleLookupTests
{
    [Fact]
    public async Task SuccessfulDvlaAndDvsaResponsesProduceCurrentEvidenceWithProvenance()
    {
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.True(request.Headers.Contains("x-api-key"));
                return Json(HttpStatusCode.OK, """{"make":"FORD","model":"FOCUS","yearOfManufacture":2020,"engineCapacity":999,"fuelType":"PETROL"}""");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.True(request.Headers.Contains("X-API-Key"));
            return Json(HttpStatusCode.OK, """[{"motTests":[{"completedDate":"2026-01-02","testResult":"PASSED","expiryDate":"2027-01-01","odometerValue":"12000","odometerUnit":"mi"}]}]""");
        });

        var result = await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);

        Assert.Equal(VehicleLookupOutcome.Current, result.Outcome);
        Assert.Equal("FORD", result.Vehicle?.Make);
        Assert.Equal(12000, Assert.Single(result.MotTests).Mileage);
        Assert.Equal("ves-1.2+mot-history-v1", result.ProviderVersion);
        Assert.Equal(64, result.ResponseIdentity.Length);
    }

    [Fact]
    public async Task TheMotHistoryApisRealDateShapeIsRead()
    {
        // ENG-010, verbatim from a live DVSA call for DP07EFB. The API
        // writes completedDate as a full instant, not a date. Every fixture
        // here used a date-only string, so the tests passed while
        // production silently discarded every MOT test for every vehicle
        // and no mileage was ever derived. The odometer is in KM for this
        // vehicle, which the unit mapping must carry rather than assume.
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                return Json(HttpStatusCode.OK, """{"make":"TOYOTA","model":"ALPHARD","yearOfManufacture":2007}""");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            return Json(HttpStatusCode.OK, """{"make":"TOYOTA","model":"ALPHARD","registration":"DP07EFB","motTests":[{"completedDate":"2026-05-14T13:11:22.000Z","testResult":"PASSED","expiryDate":"2027-05-13","odometerValue":"113068","odometerUnit":"KM"},{"completedDate":"2025-05-14T15:38:02.000Z","testResult":"PASSED","odometerValue":"102742","odometerUnit":"KM"}]}""");
        });

        var result = await adapter.LookupAsync(new VehicleLookupRequest("DP07EFB"), CancellationToken.None);

        Assert.Equal(VehicleLookupOutcome.Current, result.Outcome);
        Assert.Equal(2, result.MotTests.Count);
        var latest = result.MotTests[0];
        Assert.Equal(new DateOnly(2026, 5, 14), latest.TestDate);
        Assert.Equal(113068, latest.Mileage);
        Assert.Equal(VehicleMileageUnit.Kilometres, latest.MileageUnit);
    }

    [Fact]
    public async Task MotTestsThatAllFailToReadAreAFailureRatherThanSilence()
    {
        // A vehicle with no MOT history and a vehicle whose history we
        // cannot read both yield no mileage, so the two were
        // indistinguishable in production. They must not be.
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                return Json(HttpStatusCode.OK, """{"make":"FORD","model":"FOCUS","yearOfManufacture":2020}""");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            return Json(HttpStatusCode.OK, """[{"motTests":[{"completedDate":"not a date at all","testResult":"PASSED"}]}]""");
        });

        var result = await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);

        Assert.Empty(result.MotTests);
        Assert.Equal("dvsa_unreadable_tests", result.Failure?.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.NotFound, VehicleLookupOutcome.NotFound, null)]
    [InlineData(HttpStatusCode.BadRequest, HttpStatusCode.NotFound, VehicleLookupOutcome.Failed, "dvla_invalid")]
    [InlineData(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, VehicleLookupOutcome.Failed, "dvla_denied")]
    [InlineData(HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable, VehicleLookupOutcome.Throttled, "dvla_throttled")]
    [InlineData(HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, VehicleLookupOutcome.Unavailable, "dvla_unavailable")]
    public async Task ProviderFailuresMapToTypedOutcomes(
        HttpStatusCode dvlaStatus,
        HttpStatusCode dvsaStatus,
        VehicleLookupOutcome expected,
        string? expectedFailureCode)
    {
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                return Json(dvlaStatus, "{}");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            return Json(dvsaStatus, "{}");
        });

        var result = await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expectedFailureCode, result.Failure?.Code);
        Assert.Null(result.Vehicle);
        Assert.Empty(result.MotTests);
    }

    [Fact]
    public async Task OneProviderNotFoundPreservesTheOtherEvidenceAsPartial()
    {
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                return Json(HttpStatusCode.OK, """{"make":"FORD"}""");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        });

        var result = await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);

        Assert.Equal(VehicleLookupOutcome.Partial, result.Outcome);
        Assert.Equal("FORD", result.Vehicle?.Make);
        Assert.Equal("dvsa_not_found", result.Failure?.Code);
        Assert.False(result.Failure!.Retryable);
    }

    [Fact]
    public async Task CachedProviderEvidenceIsExplicitlyStaleWithSourceAge()
    {
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                var response = Json(HttpStatusCode.OK, """{"make":"FORD"}""");
                response.Headers.Age = TimeSpan.FromMinutes(30);
                return response;
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            return Json(HttpStatusCode.OK, """[{"motTests":[]}]""");
        });

        var result = await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);

        Assert.Equal(VehicleLookupOutcome.Stale, result.Outcome);
        Assert.Equal(TimeSpan.FromMinutes(30), result.SourceAge);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task DvsaOauthTokenIsCachedAcrossLookups()
    {
        var tokenCalls = 0;
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                return Json(HttpStatusCode.NotFound, "{}");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                tokenCalls++;
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        });

        await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);
        await adapter.LookupAsync(new VehicleLookupRequest("XY12ZZZ"), CancellationToken.None);

        Assert.Equal(1, tokenCalls);
    }

    [Fact]
    public async Task ShortLivedDvsaOauthTokenIsRefreshedBeforeReuse()
    {
        var tokenCalls = 0;
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                return Json(HttpStatusCode.NotFound, "{}");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                tokenCalls++;
                return Json(HttpStatusCode.OK, $"{{\"access_token\":\"token-{tokenCalls}\",\"expires_in\":30}}");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        });

        await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);
        await adapter.LookupAsync(new VehicleLookupRequest("XY12ZZZ"), CancellationToken.None);

        Assert.Equal(2, tokenCalls);
    }

    [Fact]
    public async Task MalformedProviderPayloadIsTypedAndContainsNoInventedEvidence()
    {
        using var adapter = Create(request =>
        {
            if (request.RequestUri!.Host == "driver-vehicle-licensing.api.gov.uk")
            {
                return Json(HttpStatusCode.OK, "not-json");
            }
            if (request.RequestUri.Host == "login.microsoftonline.com")
            {
                return Json(HttpStatusCode.OK, """{"access_token":"dvsa-token","expires_in":3600}""");
            }
            return Json(HttpStatusCode.NotFound, "{}");
        });

        var result = await adapter.LookupAsync(new VehicleLookupRequest("AB12CDE"), CancellationToken.None);

        Assert.Equal(VehicleLookupOutcome.Failed, result.Outcome);
        Assert.Equal("dvla_malformed", result.Failure?.Code);
        Assert.Null(result.Vehicle);
        Assert.Empty(result.MotTests);
    }

    private static DvlaDvsaProductionAdapter Create(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var values = new Dictionary<string, string?>
        {
            ["Dvla:BaseUri"] = "https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/",
            ["Dvla:ApiKey"] = "dvla-key",
            ["Dvsa:BaseUri"] = "https://history.mot.api.gov.uk/v1/trade/vehicles/registration/",
            ["Dvsa:TokenUri"] = "https://login.microsoftonline.com/tenant/oauth2/v2.0/token",
            ["Dvsa:ClientId"] = "client",
            ["Dvsa:ClientSecret"] = "secret",
            ["Dvsa:ApiKey"] = "dvsa-key",
            ["Dvsa:Scope"] = "https://tapi.dvsa.gov.uk/.default"
        };
        return new(
            DvlaDvsaProductionOptions.Create(values),
            new HttpClient(new DelegateHandler(handler)),
            TimeProvider.System);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
