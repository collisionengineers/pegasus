using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.IntegrationTests;

public sealed partial class PhotoCaptureWebTests
{
    /// <summary>
    /// The firm's form on a Quick link starts with the customer's name and
    /// accident date staff typed on the Send page, so the link's context
    /// carries both; a standing link's context, and that of a capture started
    /// on one, carries neither (operator, 10 November 2026; R853).
    /// </summary>
    [Fact]
    public async Task AQuickLinksContextCarriesTheCustomerAndAccidentDateAndAStandingLinksCarriesNeither()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        IssuedPhotoLink quick;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            quick = await scope.ServiceProvider.GetRequiredService<IssuePhotoLink>().ExecuteAsync(
                new IssuePhotoLinkRequest(
                    caseId,
                    Issuer,
                    CustomerName: "Sam Example",
                    Mobile: null,
                    Email: null,
                    IncidentDate: new DateOnly(2031, 4, 30),
                    PhotoCaptureMode.Quick,
                    PhotoCaptureQuickLinkFor.AccidentManagement,
                    LiveCameraOnly: false,
                    OriginalsInBackground: false,
                    CaptureLimits.DefaultLifetimeDays,
                    PhotoLinkTokenPurpose.Copy,
                    NewOperationKey()),
                CancellationToken.None);
        }

        var forFirm = await ContextAsync(client, quick.Token);
        Assert.Equal("Sam Example", forFirm.GetProperty("customerName").GetString());
        Assert.Equal("2031-04-30", forFirm.GetProperty("incidentDate").GetString());

        var link = await StandingCaptureDriver.CreateContactLinkAsync(factory);
        using (var standing = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Get, $"{Api}/context", StandingCaptureDriver.Standing(link)))
        {
            Assert.Equal(HttpStatusCode.OK, standing.StatusCode);
            var forStanding = await JsonAsync(standing);
            Assert.Equal(JsonValueKind.Null, forStanding.GetProperty("customerName").ValueKind);
            Assert.Equal(JsonValueKind.Null, forStanding.GetProperty("incidentDate").ValueKind);
        }

        var started = await StandingCaptureDriver.StartWithFormAsync(
            factory, client, link, new { registration = "ZZ99QQQ", customerName = "Sam Example", incidentDate = "2031-04-30" });
        using var startedHere = await StandingCaptureDriver.SendAsync(
            client, HttpMethod.Get, $"{Api}/context", StandingCaptureDriver.Capture(started.Token));
        Assert.Equal(HttpStatusCode.OK, startedHere.StatusCode);
        var forStarted = await JsonAsync(startedHere);
        Assert.Equal(JsonValueKind.Null, forStarted.GetProperty("customerName").ValueKind);
        Assert.Equal(JsonValueKind.Null, forStarted.GetProperty("incidentDate").ValueKind);
    }
}
