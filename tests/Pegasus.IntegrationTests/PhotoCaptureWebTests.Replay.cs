using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Send and the Resend composer posted again under the same operation key (a
/// refresh, or Back and send) act once: no second capture, no second
/// token, no second message, and no error page. The link was shown to the
/// first post only, since only its digest is kept; a repeated text is refused
/// with "This text was already sent." (FRD-28 The photo link text), and any
/// other repeat goes to the link's page, or back to where the composer was
/// asked from.
/// </summary>
public sealed partial class PhotoCaptureWebTests
{
    [Fact]
    public async Task SendToCopyPostedAgainMakesNoSecondLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var form = new Dictionary<string, string>
        {
            ["OperationKey"] = InputValue(await IntakeWebDriver.GetHtmlAsync(client, send), "OperationKey"),
            ["Mode"] = nameof(PhotoCaptureMode.Guided),
            ["Days"] = "7",
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ToCopy
        };

        using (var first = await PostStaffFormAsync(client, send, form))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Matches("<code id=\"photo-link-issued-url\" class=\"photo-link-url\">[^<]*/c/[^<]+</code>", await first.Content.ReadAsStringAsync());
        }

        var captureId = Assert.Single(await CaptureIdsAsync(factory, caseId));
        using (var again = await PostStaffFormAsync(client, send, form))
        {
            Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
            Assert.Equal($"/PhotoCapture/{captureId:D}", again.Headers.Location?.OriginalString);
        }

        Assert.Equal(captureId, Assert.Single(await CaptureIdsAsync(factory, caseId)));
        Assert.Single(await TokenPurposesAsync(factory, captureId));
    }

    [Fact]
    public async Task SendByEmailPostedAgainSendsNothingMore()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        await SeedSendableMailboxAsync(baseFactory);
        var mail = new SentStaffMailSend();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            CaseWebTestSupport.Substitute<Pegasus.Core.Operations.IStaffMailSend>(services, mail)));
        using var client = IntakeWebDriver.CreateClient(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var html = await IntakeWebDriver.GetHtmlAsync(client, send);
        var from = Regex.Match(html, "<option value=\"(?<id>[^\"]+)\"[^>]*>photo-link-sender@collisionengineers.co.uk</option>");
        Assert.True(from.Success, "From does not offer the sending mailbox.");
        var body = TextareaValue(html, "Body");
        var form = new Dictionary<string, string>
        {
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["CustomerName"] = "Sam Taylor",
            ["Email"] = "sam.taylor@example.com",
            ["Mode"] = nameof(PhotoCaptureMode.Guided),
            ["Days"] = "7",
            ["LiveCameraOnly"] = "true",
            ["OriginalsInBackground"] = "true",
            ["FromMailboxId"] = from.Groups["id"].Value,
            ["Subject"] = InputValue(html, "Subject"),
            ["Body"] = body,
            ["StandardBody"] = body,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ByEmail
        };

        using (var first = await PostStaffFormAsync(client, send, form))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Contains(
                $"<span>{OperatorLabels.PhotoCapture.LinkSentTo("Sam Taylor")}</span>",
                await first.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        var captureId = Assert.Single(await CaptureIdsAsync(baseFactory, caseId));
        using (var again = await PostStaffFormAsync(client, send, form))
        {
            Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
            Assert.Equal($"/PhotoCapture/{captureId:D}", again.Headers.Location?.OriginalString);
        }

        Assert.Single(mail.Commands);
        Assert.Equal(captureId, Assert.Single(await CaptureIdsAsync(baseFactory, caseId)));
        Assert.Single(await TokenPurposesAsync(baseFactory, captureId));
    }

    [Fact]
    public async Task SendByTextPostedAgainTextsNothingMore()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        var texts = new RecordingPhotoLinkTextSend();
        using var factory = WithTexts(baseFactory, texts);
        using var client = IntakeWebDriver.CreateClient(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var opened = await IntakeWebDriver.GetHtmlAsync(client, send);
        var form = TextSendForm(InputValue(opened, "OperationKey"), TextMobile, TextareaValue(opened, "TextBody"));

        using (var first = await PostStaffFormAsync(client, send, form))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        using (var again = await PostStaffFormAsync(client, send, form))
        {
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            Assert.Contains(SendPhotoLinkText.AlreadySent, await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        Assert.Single(texts.Texts);
        var captureId = Assert.Single(await CaptureIdsAsync(baseFactory, caseId));
        Assert.Single(await TokenPurposesAsync(baseFactory, captureId));
    }

    [Fact]
    public async Task ResendPostedAgainMakesNoSecondTokenMessageOrText()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        await SeedSendableMailboxAsync(baseFactory);
        var link = await IssueLinkToAsync(baseFactory, caseId, "Sam Taylor", "sam.taylor@example.com");
        var mail = new SentStaffMailSend();
        var texts = new RecordingPhotoLinkTextSend();
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            CaseWebTestSupport.Substitute<Pegasus.Core.Operations.IStaffMailSend>(services, mail);
            CaseWebTestSupport.Substitute<IPhotoLinkTextSend>(services, texts);
        }));
        using var client = IntakeWebDriver.CreateClient(factory);
        var composer = $"/PhotoCapture/{link.Capture.Id:D}/Resend";
        var linkPage = $"/PhotoCapture/{link.Capture.Id:D}";
        var html = await IntakeWebDriver.GetHtmlAsync(client, composer);
        var from = Regex.Match(html, "<option value=\"(?<id>[^\"]+)\"[^>]*>photo-link-sender@collisionengineers.co.uk</option>");
        Assert.True(from.Success, "From does not offer the sending mailbox.");
        var body = TextareaValue(html, "Body");
        var text = TextareaValue(html, "TextBody");

        var copy = new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.ResendModel.ToCopy
        };
        var email = new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["FromMailboxId"] = from.Groups["id"].Value,
            ["Subject"] = InputValue(html, "Subject"),
            ["Body"] = body,
            ["StandardBody"] = body,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.ResendModel.ByEmail
        };
        var byText = new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["Mobile"] = TextMobile,
            ["TextBody"] = text,
            ["StandardTextBody"] = text,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.ResendModel.ByText
        };

        using (var first = await PostStaffFormAsync(client, composer, copy))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Contains("data-photo-link-issued", await first.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        using (var again = await PostStaffFormAsync(client, composer, copy))
        {
            Assert.Equal(HttpStatusCode.Redirect, again.StatusCode);
            Assert.Equal(linkPage, again.Headers.Location?.OriginalString);
        }

        for (var post = 0; post < 2; post++)
        {
            using var sent = await PostStaffFormAsync(client, composer, email);
            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
            Assert.Equal(linkPage, sent.Headers.Location?.OriginalString);
        }

        using (var first = await PostStaffFormAsync(client, composer, byText))
        {
            Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        }

        using (var again = await PostStaffFormAsync(client, composer, byText))
        {
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
            Assert.Contains(SendPhotoLinkText.AlreadySent, await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        Assert.Single(mail.Commands);
        Assert.Single(texts.Texts);
        // The first token, and one for each of the three forms.
        Assert.Equal(4, (await TokenPurposesAsync(baseFactory, link.Capture.Id)).Length);
    }

    /// <summary>
    /// Two posts of one Send form at once still issue one link: the second
    /// waits on the first's operation key and answers the capture it made.
    /// </summary>
    [Fact]
    public async Task TwoIssuesUnderOneOperationKeyAtOnceMakeOneLink()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var key = NewOperationKey();

        var issued = await Task.WhenAll(IssueUnderAsync(factory, caseId, key), IssueUnderAsync(factory, caseId, key));

        var captureId = Assert.Single(await CaptureIdsAsync(factory, caseId));
        Assert.All(issued, link => Assert.Equal(captureId, link.Capture.Id));
        Assert.Single(issued, link => !link.Replayed);
        Assert.Single(await TokenPurposesAsync(factory, captureId));
    }

    private static async Task<IssuedPhotoLink> IssueUnderAsync(IntakeWebApplicationFactory factory, Guid caseId, string operationKey)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IssuePhotoLink>().ExecuteAsync(
            new IssuePhotoLinkRequest(
                caseId,
                Issuer,
                CustomerName: null,
                Mobile: null,
                Email: null,
                IncidentDate: null,
                PhotoCaptureMode.Guided,
                QuickLinkFor: null,
                LiveCameraOnly: false,
                OriginalsInBackground: false,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy,
                operationKey),
            CancellationToken.None);
    }
}
