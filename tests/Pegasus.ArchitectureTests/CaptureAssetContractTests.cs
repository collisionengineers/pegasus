using System.Globalization;
using System.Text.RegularExpressions;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.Pages.Capture;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The photo capture app (FRD-28) is served under a Content Security Policy
/// that admits no inline script or style, so its assets and the pages that
/// embed them carry none: an inline handler, a style attribute or a
/// <c>javascript:</c> URL would be refused by the browser and break the app
/// silently. No browser automation exists to catch that (ADR-0050), so the
/// text is held to the rule here.
/// </summary>
public sealed partial class CaptureAssetContractTests
{
    private static readonly string[] AssetPaths =
    [
        "src/Pegasus.Web/wwwroot/capture/capture.js",
        "src/Pegasus.Web/wwwroot/capture/shell.html"
    ];

    private const string PagesFolder = "src/Pegasus.Web/Pages/Capture";
    private const string ScriptPath = "src/Pegasus.Web/wwwroot/capture/capture.js";
    private const string ShellPath = "src/Pegasus.Web/wwwroot/capture/shell.html";
    private const string ShellPartialPath = "src/Pegasus.Web/Pages/Capture/_CaptureShell.cshtml";

    [GeneratedRegex(@"\bconst ONE=(\d+)")]
    private static partial Regex SingleRequestConstant();

    [GeneratedRegex(@"\bconst ORIGMAX=(\d+)")]
    private static partial Regex OriginalConstant();

    [GeneratedRegex(@"\bconst PHOTOMAX=(\d+)")]
    private static partial Regex PhotoConstant();

    [GeneratedRegex(@"\bconst DOCMAX=(\d+)")]
    private static partial Regex DocumentConstant();

    [GeneratedRegex(@"\bconst VIDEOMAX=(\d+)")]
    private static partial Regex VideoConstant();

    [GeneratedRegex(@"<input type=""file""[^>]*>")]
    private static partial Regex FileInput();

    [GeneratedRegex(@"<input type=""file"" id=""fileVideo""[^>]*>")]
    private static partial Regex VideoInput();

    [GeneratedRegex(@"style=""", RegexOptions.IgnoreCase)]
    private static partial Regex StyleAttribute();

    [GeneratedRegex(@"\son[a-z]+=""", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandler();

    [GeneratedRegex(@"javascript:", RegexOptions.IgnoreCase)]
    private static partial Regex JavaScriptUrl();

    [GeneratedRegex(@"<script\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTag();

    [Fact]
    public void TheCaptureAssetsCarryNoInlineStyleHandlerOrJavaScriptUrl()
    {
        var root = RepositoryRoot.Find();
        foreach (var relative in AssetPaths)
        {
            var path = Path.Combine(root, relative);
            Assert.True(File.Exists(path), $"{relative} is missing.");
            var text = File.ReadAllText(path);
            Assert.False(StyleAttribute().IsMatch(text), $"{relative} carries an inline style attribute.");
            Assert.False(InlineHandler().IsMatch(text), $"{relative} carries an inline event handler.");
            Assert.False(JavaScriptUrl().IsMatch(text), $"{relative} carries a javascript: URL.");
        }
    }

    /// <summary>
    /// The capture pages embed exactly two JSON blocks (the asset manifest and
    /// the link context) and load the app from its external file; no other
    /// script element, inline or not, may appear.
    /// </summary>
    [Fact]
    public void TheCapturePagesEmbedOnlyTheTwoJsonBlocksAndTheExternalScript()
    {
        var folder = Path.Combine(RepositoryRoot.Find(), PagesFolder);
        Assert.True(Directory.Exists(folder), $"{PagesFolder} is missing.");
        var pages = Directory.GetFiles(folder, "*.cshtml", SearchOption.AllDirectories);
        Assert.NotEmpty(pages);
        foreach (var page in pages)
        {
            var text = File.ReadAllText(page);
            Assert.False(StyleAttribute().IsMatch(text), $"{Path.GetFileName(page)} carries an inline style attribute.");
            Assert.False(InlineHandler().IsMatch(text), $"{Path.GetFileName(page)} carries an inline event handler.");
            foreach (Match script in ScriptTag().Matches(text))
            {
                var isJson = script.Value.Contains("type=\"application/json\"", StringComparison.OrdinalIgnoreCase);
                var isExternal = script.Value.Contains(" src=", StringComparison.OrdinalIgnoreCase);
                Assert.True(isJson || isExternal, $"{Path.GetFileName(page)} embeds a script that is neither a JSON block nor the external app: {script.Value}");
            }
        }
    }

    /// <summary>The app splits a file into parts of the size the API takes in one request.</summary>
    [Fact]
    public void TheAppsPartSizeIsTheApisSingleRequestBound()
    {
        var match = SingleRequestConstant().Match(Read(ScriptPath));

        Assert.True(match.Success, "capture.js no longer declares ONE.");
        Assert.Equal(CaptureLimits.SingleRequestBytes, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>The app sends no original larger than the API takes, so one over the bound is never sent only to be refused.</summary>
    [Fact]
    public void TheAppsOriginalBoundIsTheApisOriginalBound()
    {
        var match = OriginalConstant().Match(Read(ScriptPath));

        Assert.True(match.Success, "capture.js no longer declares ORIGMAX.");
        Assert.Equal(CaptureLimits.OriginalBytes, int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The app refuses a photo, document or video larger than the API takes
    /// before reading it to hash, so the bounds it holds are the API's.
    /// </summary>
    [Fact]
    public void TheAppsFileBoundsAreTheApisFileBounds()
    {
        var script = Read(ScriptPath);

        Assert.Equal(CaptureLimits.PhotoBytes, Bound(PhotoConstant(), script, "PHOTOMAX"));
        Assert.Equal(CaptureLimits.DocumentBytes, Bound(DocumentConstant(), script, "DOCMAX"));
        Assert.Equal(CaptureLimits.VideoBytes, Bound(VideoConstant(), script, "VIDEOMAX"));
    }

    /// <summary>A link that dies while the app is open says what the page an unavailable link gets says (R789).</summary>
    [Fact]
    public void TheAppsUnavailableScreenSaysTheUnavailablePagesTwoLines()
    {
        var script = Read(ScriptPath);

        Assert.Contains(CaptureCardView.Unavailable.Heading, script, StringComparison.Ordinal);
        Assert.Contains(CaptureCardView.Unavailable.Line, script, StringComparison.Ordinal);
    }

    /// <summary>
    /// The done page keeps the handover's sentence for a link sent from a Case
    /// or sent on to a customer, and says to enter the same registration where
    /// the capture was started from a standing or website link (R809).
    /// </summary>
    [Fact]
    public void TheDonePageHoldsBothForgotSomethingSentences()
    {
        var script = Read(ScriptPath);

        Assert.Contains("Open the same link again to send more photos for this vehicle.", script, StringComparison.Ordinal);
        Assert.Contains("Open the link again and enter the same registration to send more photos for this vehicle.", script, StringComparison.Ordinal);
    }

    /// <summary>
    /// Video can always come from the camera roll, so its input never forces
    /// the camera; Record sets the camera on it for that one press (R984).
    /// </summary>
    [Fact]
    public void TheVideoInputNeverForcesTheCamera()
    {
        foreach (var relative in new[] { ShellPath, ShellPartialPath })
        {
            var input = VideoInput().Match(Read(relative));
            Assert.True(input.Success, $"{relative} has no video input.");
            Assert.DoesNotContain("capture=", input.Value, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Until it has a video, the walk-around video's row offers Record, which
    /// opens the camera, and Choose, which leaves the phone's own picker
    /// (R984, R985).
    /// </summary>
    [Fact]
    public void TheVideoRowOffersRecordAndChoose()
    {
        var script = Read(ScriptPath);

        Assert.Contains("data-x=\"video\" data-record>${ICON.camera}Record</button>", script, StringComparison.Ordinal);
        Assert.Contains("data-x=\"video\">${ICON.image}Choose</button>", script, StringComparison.Ordinal);
        Assert.Contains(
            "if(record) fileVideo.setAttribute('capture','environment'); else fileVideo.removeAttribute('capture');",
            script,
            StringComparison.Ordinal);
    }

    /// <summary>A file the phone cannot read has its own sentence, never the one for no signal (R983).</summary>
    [Fact]
    public void TheAppSaysWhenThePhoneCannotReadAFile()
    {
        Assert.Contains(
            "unreadable: 'We couldn\u2019t read that file from your phone. Please add it again.'",
            Read(ScriptPath),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A file is read a request's worth at a time, so a video is never read
    /// whole: the browser's own digest, which takes the whole file in one
    /// buffer, is not used (R983).
    /// </summary>
    [Fact]
    public void TheAppNeverReadsAFileWhole()
    {
        var script = Read(ScriptPath);

        Assert.Contains("blob.slice(from,to).arrayBuffer()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("blob.arrayBuffer()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("crypto.subtle", script, StringComparison.Ordinal);
    }

    /// <summary>The Razor shell emits shell.html's markup, so the four file inputs are the same in both.</summary>
    [Fact]
    public void TheShellAndItsRazorCopyHaveTheSameFourFileInputs()
    {
        var shell = FileInput().Matches(Read(ShellPath)).Select(match => match.Value).ToArray();
        var partial = FileInput().Matches(Read(ShellPartialPath)).Select(match => match.Value).ToArray();

        Assert.Equal(4, shell.Length);
        Assert.Equal(shell, partial);
    }

    private static int Bound(Regex declaration, string script, string name)
    {
        var match = declaration.Match(script);
        Assert.True(match.Success, $"capture.js no longer declares {name}.");
        return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static string Read(string relative)
    {
        var path = Path.Combine(RepositoryRoot.Find(), relative);
        Assert.True(File.Exists(path), $"{relative} is missing.");
        return File.ReadAllText(path);
    }
}
