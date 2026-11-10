using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.PhotoCapture;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The photo link text (FRD-28 The photo link text; R918 to R921): Send and
/// the Resend composer offer Send by text only where a text provider is
/// configured, a text goes only to a UK mobile, the link it carries exists
/// before it is sent, and the link's history says "Sent by text · name".
/// A recording provider stands in for Azure Communication Services.
/// </summary>
public sealed partial class PhotoCaptureWebTests
{
    private const string TextMobile = "07700 900123";
    private const string TextMobileE164 = "+447700900123";

    /// <summary>
    /// Send offers Send by text and draws the text message only where a text
    /// provider is configured (R918); with none, neither is drawn.
    /// </summary>
    [Fact]
    public async Task SendOffersSendByTextOnlyWhereATextProviderIsConfigured()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var button = $"<span>{OperatorLabels.PhotoCapture.SendByText}</span>";

        using (var unconfigured = IntakeWebDriver.CreateClient(baseFactory))
        {
            var html = await IntakeWebDriver.GetHtmlAsync(unconfigured, send);
            Assert.DoesNotContain(button, html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"TextBody\"", html, StringComparison.Ordinal);
        }

        using var factory = WithTexts(baseFactory, new RecordingPhotoLinkTextSend());
        using var client = IntakeWebDriver.CreateClient(factory);
        var offered = await IntakeWebDriver.GetHtmlAsync(client, send);
        Assert.Contains(button, offered, StringComparison.Ordinal);
        var text = TextareaValue(offered, "TextBody");
        Assert.Contains("Collision Engineers need a few photos of AB12 CDE", text, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Send by text issues the link for a text, sends one text to the mobile
    /// in E.164 carrying the link just made, says "Link sent to … by text",
    /// and the link's history reads "Sent by text · name" (R918, R921).
    /// </summary>
    [Fact]
    public async Task SendByTextIssuesTheLinkAndTextsIt()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        var texts = new RecordingPhotoLinkTextSend();
        using var factory = WithTexts(baseFactory, texts);
        using var client = IntakeWebDriver.CreateClient(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var opened = await IntakeWebDriver.GetHtmlAsync(client, send);
        var standard = TextareaValue(opened, "TextBody");
        var before = await CaptureIdsAsync(baseFactory, caseId);

        using var sent = await PostStaffFormAsync(client, send, TextSendForm(InputValue(opened, "OperationKey"), TextMobile, standard));

        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var page = await sent.Content.ReadAsStringAsync();
        Assert.Contains($"<span>{OperatorLabels.PhotoCapture.LinkTextedTo("Sam Taylor")}</span>", page, StringComparison.Ordinal);
        var url = WebUtility.HtmlDecode(Regex.Match(page, "<code id=\"photo-link-issued-url\" class=\"photo-link-url\">(?<url>[^<]+)</code>").Groups["url"].Value);
        Assert.Contains("/c/", url, StringComparison.Ordinal);
        var text = Assert.Single(texts.Texts);
        Assert.Equal(TextMobileE164, text.To);
        Assert.Contains(url, text.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, text.Message, StringComparison.Ordinal);

        var captureId = Assert.Single((await CaptureIdsAsync(baseFactory, caseId)).Except(before));
        Assert.Contains(PhotoCaptureCodes.ToCode(PhotoLinkTokenPurpose.Text), await TokenPurposesAsync(baseFactory, captureId));
        var name = await StaffNameAsync(baseFactory, DevelopmentOfflineIdentity.AdministratorId);
        var details = await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{captureId:D}");
        Assert.Contains(
            HtmlEncoder.Default.Encode($"{OperatorLabels.PhotoCapture.SentByText} · {name}"),
            details,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A number that is not a UK mobile is refused beside Mobile in the
    /// operator's words (R918): no link is issued and nothing is texted.
    /// </summary>
    [Fact]
    public async Task SendByTextToANumberThatIsNotAUkMobileIsRefused()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        var texts = new RecordingPhotoLinkTextSend();
        using var factory = WithTexts(baseFactory, texts);
        using var client = IntakeWebDriver.CreateClient(factory);
        var send = $"/PhotoCapture/Send/{caseId:D}";
        var opened = await IntakeWebDriver.GetHtmlAsync(client, send);
        var before = await CaptureIdsAsync(baseFactory, caseId);

        using var refused = await PostStaffFormAsync(
            client, send, TextSendForm(NewOperationKey(), "020 7946 0123", TextareaValue(opened, "TextBody")));

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains(SendPhotoLinkText.NotAUkMobile, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(texts.Texts);
        Assert.Equal(before, await CaptureIdsAsync(baseFactory, caseId));
    }

    /// <summary>
    /// The Resend composer (R915) sends by text to the mobile in To and
    /// returns to where it was asked from with "Link sent to … by text";
    /// the link's history reads "Sent by text · name" (R918, R921).
    /// </summary>
    [Fact]
    public async Task TheComposerSendsByTextAndReturnsWithTheNotice()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(baseFactory);
        await SeedCaseRegistrationAsync(baseFactory, caseId, "AB12 CDE");
        var link = await IssueLinkToAsync(baseFactory, caseId, "Sam Taylor", "sam.taylor@example.com");
        var texts = new RecordingPhotoLinkTextSend();
        using var factory = WithTexts(baseFactory, texts);
        using var client = IntakeWebDriver.CreateClient(factory);
        var listPath = $"/PhotoCapture?tab={OperatorLabels.PhotoCapture.LinksWeSentKey}";
        var composer = $"/PhotoCapture/{link.Capture.Id:D}/Resend";

        var html = await IntakeWebDriver.GetHtmlAsync(client, $"{composer}?returnUrl={Uri.EscapeDataString(listPath)}");

        Assert.Contains($"<span>{OperatorLabels.PhotoCapture.SendByText}</span>", html, StringComparison.Ordinal);
        var standard = TextareaValue(html, "TextBody");
        Assert.Contains(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, standard, StringComparison.Ordinal);

        using (var sent = await PostStaffFormAsync(client, composer, new Dictionary<string, string>
        {
            ["OperationKey"] = InputValue(html, "OperationKey"),
            ["ReturnUrl"] = listPath,
            ["Mobile"] = TextMobile,
            ["TextBody"] = standard,
            ["StandardTextBody"] = standard,
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.ResendModel.ByText
        }))
        {
            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
            Assert.Equal(listPath, sent.Headers.Location?.OriginalString);
        }

        var text = Assert.Single(texts.Texts);
        Assert.Equal(TextMobileE164, text.To);
        Assert.Contains("/c/", text.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(OperatorLabels.PhotoCapture.PhotoLinkPlaceholder, text.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"<span>{OperatorLabels.PhotoCapture.LinkTextedTo("Sam Taylor")}</span>",
            await IntakeWebDriver.GetHtmlAsync(client, listPath),
            StringComparison.Ordinal);
        Assert.Contains(PhotoCaptureCodes.ToCode(PhotoLinkTokenPurpose.Text), await TokenPurposesAsync(baseFactory, link.Capture.Id));
        var name = await StaffNameAsync(baseFactory, DevelopmentOfflineIdentity.AdministratorId);
        Assert.Contains(
            HtmlEncoder.Default.Encode($"{OperatorLabels.PhotoCapture.SentByText} · {name}"),
            await IntakeWebDriver.GetHtmlAsync(client, $"/PhotoCapture/{link.Capture.Id:D}"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A text act is written to the link's history once per operation key:
    /// the same key again writes nothing and answers false, which is what
    /// keeps a reposted form from texting twice (R921).
    /// </summary>
    [Fact]
    public async Task ATextActIsWrittenOncePerOperationKey()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var key = NewOperationKey() + ":text";

        bool first;
        bool replayed;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>();
            first = await store.RecordTextAsync(
                link.Capture.Id, Issuer, key, PhotoLinkActs.TextSubmitted, new(TextMobileE164),
                IntakeWebApplicationFactory.FixedUtcNow, CancellationToken.None);
            replayed = await store.RecordTextAsync(
                link.Capture.Id, Issuer, key, PhotoLinkActs.TextSubmitted, new(TextMobileE164),
                IntakeWebApplicationFactory.FixedUtcNow, CancellationToken.None);
        }

        Assert.True(first);
        Assert.False(replayed);
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var written = Assert.Single(await db.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == EfPhotoCaptureStore.HistoryAggregateType && item.CorrelationId == key)
            .ToArrayAsync());
        Assert.Equal(PhotoLinkActs.TextSubmitted, written.EventKind);
        Assert.Equal(link.Capture.Id.ToString("D"), written.AggregateId);
        using var after = JsonDocument.Parse(written.AfterJson ?? "{}");
        Assert.Equal(TextMobileE164, after.RootElement.GetProperty("to").GetString());
    }

    /// <summary>The composed host with the recording text provider in place of the unavailable one.</summary>
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> WithTexts(
        IntakeWebApplicationFactory factory,
        RecordingPhotoLinkTextSend texts) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            CaseWebTestSupport.Substitute<IPhotoLinkTextSend>(services, texts)));

    /// <summary>Send's form as Send by text posts it, for Sam Taylor at the mobile given.</summary>
    private static Dictionary<string, string> TextSendForm(string operationKey, string mobile, string textBody) => new()
    {
        ["OperationKey"] = operationKey,
        ["CustomerName"] = "Sam Taylor",
        ["Mobile"] = mobile,
        ["Mode"] = nameof(PhotoCaptureMode.Guided),
        ["Days"] = "7",
        ["LiveCameraOnly"] = "true",
        ["OriginalsInBackground"] = "true",
        ["TextBody"] = textBody,
        ["StandardTextBody"] = textBody,
        ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ByText
    };

    /// <summary>The purposes of the tokens issued for the capture, by their stored codes.</summary>
    private static async Task<string[]> TokenPurposesAsync(IntakeWebApplicationFactory factory, Guid captureId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        return await db.PhotoCaptureTokens.AsNoTracking()
            .Where(value => value.CaptureId == captureId)
            .Select(value => value.Purpose)
            .ToArrayAsync();
    }

    private sealed record RecordedPhotoLinkText(string To, string Message);

    /// <summary>A configured text provider that records each text and accepts it at once.</summary>
    private sealed class RecordingPhotoLinkTextSend : IPhotoLinkTextSend
    {
        private readonly List<RecordedPhotoLinkText> texts = [];

        public bool Available => true;

        public IReadOnlyList<RecordedPhotoLinkText> Texts
        {
            get
            {
                lock (texts)
                {
                    return [.. texts];
                }
            }
        }

        public Task<PhotoLinkTextOutcome> SendAsync(string recipient, string message, CancellationToken cancellationToken)
        {
            lock (texts)
            {
                texts.Add(new(recipient, message));
                return Task.FromResult(PhotoLinkTextOutcome.Sent($"message-{texts.Count}"));
            }
        }
    }
}
