using Pegasus.Infrastructure.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The inert text of an HTML-only message body: a sender's style sheet, script
/// and comments are not message text, and block tags still separate lines.
/// </summary>
public sealed class HtmlMailTextTests
{
    [Fact]
    public void HeadStyleScriptAndCommentsAreNotMessageText()
    {
        const string html =
            "<html><head><meta charset=\"utf-8\"><style type=\"text/css\">\n"
            + "  html {\n    font-family: Gotham, Calibri, Arial, sans-serif;\n  }\n"
            + "  .no-dec a { text-decoration: none; }\n"
            + "</style></head><body>\n"
            + "<STYLE>.late { color: #6e6e6e; }</STYLE>\n"
            + "<!-- [if mso]> conditional <![endif] -->\n"
            + "<script>var tracked = true;</script>\n"
            + "<p>Please find attached a summary of the instructions.</p>\n"
            + "</body></html>";

        var text = HtmlMailText.ToText(html);

        Assert.Equal("Please find attached a summary of the instructions.", text);
    }

    [Fact]
    public void BlockTagsSeparateLinesAndEntitiesAreDecoded()
    {
        var text = HtmlMailText.ToText(
            "<div>Smith &amp; Sons</div><p>Line one<br>Line two</p><span>inline</span>");

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("Smith & Sons|Line one|Line two|inline", string.Join('|', lines));
    }

    [Fact]
    public void AHeaderElementIsContentNotTheDocumentHead()
    {
        var text = HtmlMailText.ToText("<header>Our reference 30320-01</header><p>Body</p>");

        Assert.Contains("Our reference 30320-01", text, StringComparison.Ordinal);
        Assert.Contains("Body", text, StringComparison.Ordinal);
    }
}
