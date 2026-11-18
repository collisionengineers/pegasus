using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The editor's main-frame address as the Pegasus helper hands it over
/// (R1170): the shape the editor's own frame has, built by the editor
/// client's own address builder, is read to its return token and editor
/// session; anything else is refused. Every key and token is made up.
/// </summary>
public sealed class GlassHelpFrameTests
{
    internal const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    internal const string Estimator = "https://ere.test/ere/aco/";

    internal static readonly GlassRepairEstimateOptions Options = GlassRepairEstimateOptions.Create(key => key switch
    {
        "Glass:MarketValueAssessorBaseUri" => "https://mva.test/",
        "Glass:EstimatorBaseUri" => "https://ere.test/",
        "Glass:CallbackBaseUri" => "https://pegasus.test/",
        "Glass:RepairProfileId" => "4063",
        _ => null,
    });

    /// <summary>The launch page's own name for where the editor came from, ending in the caller.</summary>
    internal static string Url0(string caller) => "../acolib/aco_call_xml.php?caller=" + caller;

    /// <summary>The main frame's address exactly as the editor client builds it (<c>GlassEditorAddresses.Frame</c>).</summary>
    internal static string FrameAddress(string url0, string key = GlassEditorFixture.Key, string estimator = Estimator) =>
        new GlassEditorAddresses(
            new GlassEditorSession(estimator, url0, key, GlassEditorFixture.NoCookies), TimeProvider.System).Frame;

    internal static string OwnFrame(string key = GlassEditorFixture.Key) =>
        FrameAddress(Url0(Options.CallbackFor(Token).AbsoluteUri), key);

    [Fact]
    public void TheEditorsOwnFrameAddressIsReadToItsTokenAndSession()
    {
        var address = OwnFrame();

        var frame = GlassEditorPages.HelpFrame(address, Options);

        Assert.NotNull(frame);
        Assert.Equal(Token, frame.Token);
        Assert.Equal(Estimator, frame.Session.BaseAddress);
        Assert.Equal(GlassEditorFixture.Key, frame.Session.Key);
        Assert.Equal(Url0(Options.CallbackFor(Token).AbsoluteUri), frame.Session.Url0);
        Assert.Empty(frame.Session.Cookies);
        // The session read builds the very address it was read from.
        Assert.Equal(address, new GlassEditorAddresses(frame.Session, TimeProvider.System).Frame);
        Assert.DoesNotContain(Token, frame.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(GlassEditorFixture.Key, frame.Session.ToString(), StringComparison.Ordinal);
    }

    public static TheoryData<string, string?> Refused()
    {
        var url0 = Url0(Options.CallbackFor(Token).AbsoluteUri);
        var own = FrameAddress(url0);
        return new()
        {
            { "nothing", null },
            { "not an address", "acofr.php?ksid=ABC" },
            { "another host", FrameAddress(url0, estimator: "https://other.test/ere/aco/") },
            { "plain http", FrameAddress(url0, estimator: "http://ere.test/ere/aco/") },
            { "another port", FrameAddress(url0, estimator: "https://ere.test:8443/ere/aco/") },
            { "another path", FrameAddress(url0, estimator: "https://ere.test/ere/other/") },
            { "another page", own.Replace("acofr.php", "selected.php", StringComparison.Ordinal) },
            { "no key", own.Replace("ksid=" + GlassEditorFixture.Key + "&", string.Empty, StringComparison.Ordinal) },
            { "the key not first", own.Replace("?ksid=", "?noacd=1&ksid=", StringComparison.Ordinal) },
            { "a key that is not alphanumeric", FrameAddress(url0, key: "TESTKEY-0001") },
            { "no url0", own[..own.IndexOf("&url0=", StringComparison.Ordinal)] },
            { "no caller", FrameAddress("../acolib/aco_call_xml.php?WorkTime=1") },
            { "a caller on another host", FrameAddress(Url0("https://elsewhere.test/Integrations/Glass/Callback/" + Token)) },
            { "a caller over http", FrameAddress(Url0("http://pegasus.test/Integrations/Glass/Callback/" + Token)) },
            { "a caller on another path", FrameAddress(Url0("https://pegasus.test/Integrations/Glass/Opening/" + Token)) },
            { "a token one short", FrameAddress(Url0("https://pegasus.test/Integrations/Glass/Callback/" + Token[..63])) },
            { "a token one long", FrameAddress(Url0("https://pegasus.test/Integrations/Glass/Callback/" + Token + "0")) },
            { "an upper-case token", FrameAddress(Url0("https://pegasus.test/Integrations/Glass/Callback/" + Token.ToUpperInvariant())) },
            { "a token that is not hex", FrameAddress(Url0("https://pegasus.test/Integrations/Glass/Callback/" + Token[..63] + "g")) },
            { "a caller with a query", FrameAddress(Url0(Options.CallbackFor(Token).AbsoluteUri + "?x=1")) },
        };
    }

    [Theory]
    [MemberData(nameof(Refused))]
    public void AnythingElseIsRefused(string why, string? address)
    {
        Assert.NotEmpty(why);
        Assert.Null(GlassEditorPages.HelpFrame(address, Options));
    }

    /// <summary>
    /// <c>url0</c> is read back exactly as <c>GlassEditorAddresses.Escape</c>
    /// wrote it: a plus stays a plus, and characters above 255 come back from
    /// their <c>%u</c> form.
    /// </summary>
    [Theory]
    [InlineData("../acolib/aco_call_xml.php?WorkTime=1788529734&caller=")]
    [InlineData("../acolib/aco_call_xml.php?note=a b+c%d~e'f\"g<h>&caller=")]
    [InlineData("../acolib/aco_call_xml.php?name=Zoë€&caller=")]
    public void Url0IsUnescapedAsTheEditorEscapesIt(string before)
    {
        var url0 = before + Options.CallbackFor(Token).AbsoluteUri;

        var frame = GlassEditorPages.HelpFrame(FrameAddress(url0), Options);

        Assert.NotNull(frame);
        Assert.Equal(url0, frame.Session.Url0);
        Assert.Equal(url0, GlassEditorAddresses.Unescape(GlassEditorAddresses.Escape(url0)));
    }

    [Theory]
    [InlineData("a+b", "a+b")]
    [InlineData("100%", "100%")]
    [InlineData("%zz%u12%4", "%zz%u12%4")]
    [InlineData("%41%u20AC%e9", "A€é")]
    public void UnescapeLeavesWhatIsNotAnEscape(string text, string expected) =>
        Assert.Equal(expected, GlassEditorAddresses.Unescape(text));
}

/// <summary>
/// The keys the helper hands over, held in memory (R1170): a newer key for a
/// session replaces the older, a poll keeps it, 30 seconds with no poll drops
/// it, and so does the session ending.
/// </summary>
public sealed class GlassHelpKeysTests
{
    private const string FirstKey = "TESTKEYD1000000000000000000001";

    private const string SecondKey = "TESTKEYD1000000000000000000002";

    private static readonly DateTimeOffset Start = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void ANewerKeyReplacesTheOlderAndAPollSaysItIsHeld()
    {
        var clock = new ManualClock(Start);
        var keys = new GlassHelpKeys(GlassHelpFrameTests.Options, clock);
        var sessionId = Guid.NewGuid();
        Assert.Equal(new GlassHelpPoll(false, 0), keys.Poll(sessionId, 0));

        keys.Hold(sessionId, Frame(keys, FirstKey));
        keys.Hold(sessionId, Frame(keys, SecondKey));

        Assert.Equal(SecondKey, keys.Session(sessionId)?.Key);
        Assert.Equal(new GlassHelpPoll(true, 0), keys.Poll(sessionId, 0));
        Assert.Null(keys.Session(Guid.NewGuid()));
    }

    [Fact]
    public void AKeyIsDroppedThirtySecondsAfterItsWindowWasLastHeardFrom()
    {
        var clock = new ManualClock(Start);
        var keys = new GlassHelpKeys(GlassHelpFrameTests.Options, clock);
        var sessionId = Guid.NewGuid();
        keys.Hold(sessionId, Frame(keys, FirstKey));

        clock.Now = Start + TimeSpan.FromSeconds(20);
        Assert.True(keys.Poll(sessionId, 0).Held);
        clock.Now = Start + TimeSpan.FromSeconds(50);
        Assert.True(keys.Poll(sessionId, 0).Held);

        clock.Now = Start + TimeSpan.FromSeconds(81);
        Assert.False(keys.Poll(sessionId, 0).Held);
        Assert.Null(keys.Session(sessionId));
    }

    [Fact]
    public void AKeyIsDroppedWhenItsSessionEnds()
    {
        var keys = new GlassHelpKeys(GlassHelpFrameTests.Options, new ManualClock(Start));
        var sessionId = Guid.NewGuid();
        keys.Hold(sessionId, Frame(keys, FirstKey));

        keys.Drop(sessionId);

        Assert.False(keys.Poll(sessionId, 0).Held);
    }

    private static GlassHelpFrame Frame(GlassHelpKeys keys, string key) =>
        keys.Read(GlassHelpFrameTests.OwnFrame(key)) ?? throw new InvalidOperationException("The frame was not read.");

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
