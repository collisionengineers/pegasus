using System.Text;
using MimeKit;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// An e-mail shown as it was sent: a sanitised, self-contained document of the
/// message the display reader shows, holding the sender's own words and not
/// the messages quoted beneath them.
/// </summary>
public sealed class MailAsSentRendererTests
{
    // The 1x1 PNG signature and header bytes; the renderer does not read the
    // picture, it only carries it.
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    // What a sanitised document must not hold of the dangerous-content fixture.
    private static readonly string[] DroppedMarkup =
    [
        "<script", "alert", "<iframe", "frame text", "<form", "form value", "Press", "http-equiv",
        "refresh", "evil.example", "onclick", "expression", "<title", "Window title", "noscript text"
    ];

    private static readonly MimeKitMailAsSentRenderer Renderer = new();

    [Fact]
    public void PlainTextOnlyMessageHasNoRendering()
    {
        var message = new MimeMessage
        {
            Subject = "Plain",
            Body = new TextPart("plain") { Text = "Only text." }
        };

        Assert.Null(Renderer.Render(Bytes(message), isStaffForward: false));
    }

    [Fact]
    public void UnreadableBytesHaveNoRendering()
    {
        Assert.Null(Renderer.Render(ReadOnlyMemory<byte>.Empty, isStaffForward: false));
        Assert.Null(Renderer.Render(Encoding.ASCII.GetBytes("\0\u0001\u0002 not a message"), isStaffForward: false));
    }

    [Fact]
    public void DocumentIsCompleteAndOpensLinksElsewhere()
    {
        var html = Render("<p>Hello</p>");

        Assert.StartsWith("<!DOCTYPE html>", html, StringComparison.Ordinal);
        Assert.Contains("<meta charset=\"utf-8\">", html, StringComparison.Ordinal);
        Assert.Contains("<base target=\"_blank\">", html, StringComparison.Ordinal);
        Assert.EndsWith("</body></html>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptFramesFormsRefreshAndHandlersAreGone()
    {
        var html = Render(
            "<html><head><meta http-equiv=\"refresh\" content=\"0;url=https://evil.example/\">"
            + "<title>Window title</title><script>alert('head script')</script></head><body>"
            + "<p onclick=\"alert('clicked')\" style=\"color:red;width:expression(alert(1))\">Hello there</p>"
            + "<iframe src=\"https://evil.example/frame\">frame text</iframe>"
            + "<form action=\"https://evil.example/post\"><input name=\"q\" value=\"form value\">"
            + "<button>Press</button></form>"
            + "<noscript>noscript text</noscript>"
            + "</body></html>");

        Assert.Contains("<p style=\"color:red;\">Hello there</p>", html, StringComparison.Ordinal);
        foreach (var gone in DroppedMarkup)
        {
            Assert.DoesNotContain(gone, html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ScriptLinkLosesItsTargetButKeepsItsText()
    {
        var html = Render("<p><a href=\" jav&#x09;ascript:alert(1)\">Click me</a></p>");

        Assert.Contains("<a>Click me</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WebLinkOpensElsewhereWithoutReferrer()
    {
        var html = Render(
            "<p><a href=\"https://example.com/a?b=1&amp;c=2\" target=\"_self\" onclick=\"x()\">Site</a></p>");

        Assert.Contains(
            "<a href=\"https://example.com/a?b=1&amp;c=2\" target=\"_blank\" rel=\"noopener noreferrer\">Site</a>",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RemotePictureIsKept()
    {
        var html = Render("<p><img src=\"https://images.example/logo.png\" alt=\"Logo\"></p>");

        Assert.Contains("src=\"https://images.example/logo.png\"", html, StringComparison.Ordinal);
        Assert.Contains("alt=\"Logo\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CarriedPictureIsInlined()
    {
        var picture = new MimePart("image", "png")
        {
            ContentId = "logo@example.invalid",
            Content = new MimeContent(new MemoryStream(PngBytes)),
            ContentTransferEncoding = ContentEncoding.Base64
        };
        var message = HtmlMessage(
            "<p>Logo <img src=\"cid:logo@example.invalid\" alt=\"Logo\"></p>",
            picture);

        var html = RenderMessage(message);

        Assert.Contains(
            "src=\"data:image/png;base64," + Convert.ToBase64String(PngBytes) + "\"",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("cid:", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Each img that names a carried picture writes it again, so a message
    /// that names one picture many times is held to the document's budget of
    /// inlined bytes (16 MB) rather than growing without end.
    /// </summary>
    [Fact]
    public void RepeatedReferencesToACarriedPictureStopAtTheDocumentBudget()
    {
        var pictureBytes = new byte[1024 * 1024];
        PngBytes.CopyTo(pictureBytes, 0);
        var picture = new MimePart("image", "png")
        {
            ContentId = "big@example.invalid",
            Content = new MimeContent(new MemoryStream(pictureBytes)),
            ContentTransferEncoding = ContentEncoding.Base64
        };
        var body = new StringBuilder("<p>Repeated</p>");
        for (var index = 0; index < 40; index++)
        {
            body.Append("<img src=\"cid:big@example.invalid\">");
        }

        var html = RenderMessage(HtmlMessage(body.ToString(), picture));

        Assert.Contains("Repeated", html, StringComparison.Ordinal);
        var written = html.Split("data:image/png;base64,").Length - 1;
        Assert.Equal(16, written);
    }

    [Fact]
    public void CarriedNonPictureIsDropped()
    {
        var document = new MimePart("application", "pdf")
        {
            ContentId = "doc@example.invalid",
            Content = new MimeContent(new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4"))),
            ContentTransferEncoding = ContentEncoding.Base64
        };
        var message = HtmlMessage("<p>Document <img src=\"cid:doc@example.invalid\" alt=\"Doc\"></p>", document);

        var html = RenderMessage(message);

        Assert.Contains("Document", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data:", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SendersOwnDataPictureIsDropped()
    {
        var html = Render("<p>Text<img src=\"data:image/png;base64,AAAA\"></p>");

        Assert.Contains("<p>Text</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data:", html, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnStyleSheetIsKeptWithoutImports()
    {
        var html = Render(
            "<html><head><style>@import url(\"https://evil.example/x.css\"); .note { color: red; }</style>"
            + "</head><body><p class=\"note\">Styled</p></body></html>");

        Assert.Contains(".note { color: red; }", html, StringComparison.Ordinal);
        Assert.DoesNotContain("@import", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"note\">Styled</p>", html, StringComparison.Ordinal);
        Assert.True(
            html.IndexOf(".note", StringComparison.Ordinal) < html.IndexOf("</head>", StringComparison.Ordinal),
            "The style sheet belongs in the head.");
    }

    [Fact]
    public void CutsAtOutlookReplyHeader()
    {
        var html = Render(
            "<div><p>My own reply</p></div>"
            + "<hr style=\"display:inline-block;width:98%\">"
            + "<div id=\"divRplyFwdMsg\" dir=\"ltr\"><font><b>From:</b> Earlier Sender<br>"
            + "<b>Sent:</b> 1 November 2026 10:00<br><b>To:</b> Us<br><b>Subject:</b> Claim</font>"
            + "<div>&nbsp;</div></div>"
            + "<div><p>The quoted earlier text</p></div>");

        Assert.Contains("<body><div><p>My own reply</p></div></body>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Earlier Sender", html, StringComparison.Ordinal);
        Assert.DoesNotContain("quoted earlier text", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<hr", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CutsAtGmailQuote()
    {
        var html = Render(
            "<div dir=\"ltr\">Own gmail words</div><br>"
            + "<div class=\"gmail_quote\"><div dir=\"ltr\" class=\"gmail_attr\">"
            + "On Mon, 2 Nov 2026 at 10:00, Earlier &lt;e@example.invalid&gt; wrote:<br></div>"
            + "<blockquote class=\"gmail_quote\">Quoted gmail text</blockquote></div>");

        Assert.Contains("Own gmail words", html, StringComparison.Ordinal);
        Assert.DoesNotContain("wrote:", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Quoted gmail text", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CutsAtCitedBlockquote()
    {
        var html = Render(
            "<div>Own apple words</div>"
            + "<blockquote type=\"cite\"><div>Quoted apple text</div></blockquote>");

        Assert.Contains("Own apple words", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Quoted apple text", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<blockquote", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CutsAtUnmarkedOutlookHeader()
    {
        var html = Render(
            "<p>Own text words</p>"
            + "<p><b>From:</b> Earlier Sender<br><b>Sent:</b> 1 November 2026<br><b>To:</b> Desk<br>"
            + "<b>Cc:</b> Copy<br><b>Subject:</b> Claim</p>"
            + "<p>Quoted plain text</p>");

        Assert.Contains("<body><p>Own text words</p></body>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Earlier Sender", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Quoted plain text", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// A letter heading that opens with From and Date is the sender's own
    /// text, not a quoted message: only the four shapes the plain-text
    /// splitter reads cut the view (FRD-20).
    /// </summary>
    [Fact]
    public void LetterHeadingWithFromAndDateIsNotCut()
    {
        var html = Render(
            "<p><b>From:</b> ABC Claims<br><b>Date:</b> 25 November 2026<br>Our ref: AB123</p>"
            + "<p>Please inspect the vehicle.</p>");

        Assert.Contains("ABC Claims", html, StringComparison.Ordinal);
        Assert.Contains("Our ref: AB123", html, StringComparison.Ordinal);
        Assert.Contains("Please inspect the vehicle.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void WholeBodyWithoutQuotedMessage()
    {
        var html = Render("<p>First paragraph</p><p>Second paragraph</p>");

        Assert.Contains("<body><p>First paragraph</p><p>Second paragraph</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineStaffForwardShowsOnlyTheForwardedMessage()
    {
        var message = HtmlMessage(
            "<div><p>Please see below. Forwarder preamble</p><p>CE signature</p></div>"
            + "<hr style=\"display:inline-block;width:98%\">"
            + "<div id=\"divRplyFwdMsg\" dir=\"ltr\"><font><b>From:</b> Principal Sender<br>"
            + "<b>Sent:</b> 1 November 2026 10:00<br><b>To:</b> Desk<br><b>Subject:</b> New instruction</font>"
            + "<div>&nbsp;</div></div>"
            + "<div><p>Forwarded instruction words</p></div>"
            + "<hr style=\"display:inline-block;width:98%\">"
            + "<div id=\"divRplyFwdMsg\" dir=\"ltr\"><font><b>From:</b> Older Sender<br>"
            + "<b>Sent:</b> 31 October 2026 09:00<br><b>To:</b> Principal<br><b>Subject:</b> Older</font>"
            + "<div>&nbsp;</div></div>"
            + "<div><p>Older quoted message</p></div>");
        message.Subject = "FW: New instruction";

        var html = RenderMessage(message, isStaffForward: true);

        Assert.Contains("<div><p>Forwarded instruction words</p></div>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Forwarder preamble", html, StringComparison.Ordinal);
        Assert.DoesNotContain("CE signature", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Principal Sender", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Older quoted message", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AttachedOriginalStaffForwardShowsTheAttachedMessage()
    {
        var message = ForwardWithAttachedOriginal(new TextPart("html")
        {
            Text = "<p>Attached original words</p><div class=\"gmail_quote\">Older attached quote</div>"
        });

        var html = RenderMessage(message, isStaffForward: true);

        Assert.Contains("Attached original words", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Forwarder wrapper words", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Older attached quote", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayReaderSaysWhetherTheShownMessageHasHtml()
    {
        var htmlMessage = HtmlMessage("<p>Formatted</p>");
        var plainMessage = new MimeMessage
        {
            Subject = "Plain",
            Body = new TextPart("plain") { Text = "Only text." }
        };
        var plainOriginal = ForwardWithAttachedOriginal(new TextPart("plain") { Text = "Attached plain words" });

        Assert.True(LocalEmailDisplayReader.Read(htmlMessage).HasHtmlBody);
        Assert.False(LocalEmailDisplayReader.Read(plainMessage).HasHtmlBody);
        var forwarded = LocalEmailDisplayReader.Read(plainOriginal);
        Assert.Contains("Attached plain words", forwarded.Body, StringComparison.Ordinal);
        Assert.False(forwarded.HasHtmlBody);
    }

    private static string Render(string body) => RenderMessage(HtmlMessage(body));

    private static string RenderMessage(MimeMessage message, bool isStaffForward = false)
    {
        var rendering = Renderer.Render(Bytes(message), isStaffForward);
        Assert.NotNull(rendering);
        return rendering.Html;
    }

    /// <summary>An HTML message; with a related part, the HTML is the root of a multipart/related body.</summary>
    private static MimeMessage HtmlMessage(string body, MimePart? related = null)
    {
        var html = new TextPart("html") { Text = body };
        var message = new MimeMessage
        {
            Subject = "An instruction",
            Body = related is null ? html : (MimeEntity)new MultipartRelated { html, related }
        };
        message.From.Add(new MailboxAddress("Sender", "sender@example.invalid"));
        return message;
    }

    /// <summary>A staff forward whose HTML wrapper carries the original as an attached message.</summary>
    private static MimeMessage ForwardWithAttachedOriginal(MimeEntity originalBody)
    {
        var original = new MimeMessage { Subject = "Claim", Body = originalBody };
        original.From.Add(new MailboxAddress("Principal", "principal@example.invalid"));
        var message = new MimeMessage
        {
            Subject = "FW: Claim",
            Body = new Multipart("mixed")
            {
                new TextPart("html") { Text = "<p>Forwarder wrapper words</p>" },
                new MessagePart { Message = original }
            }
        };
        message.From.Add(new MailboxAddress("Staff", "staff@example.invalid"));
        return message;
    }

    private static byte[] Bytes(MimeMessage message)
    {
        using var output = new MemoryStream();
        message.WriteTo(output);
        return output.ToArray();
    }
}
