using System.Net;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The editor client against a scripted editor that answers from the pages
/// cut from real browser captures: what it asks for, in what order, with
/// which cookies and referrers, and what it refuses to do.
/// </summary>
public sealed class GlassEditorClientTests
{
    private const string EmptyEstimate = "<script>\nvar Partids = [ '' ];\nvar CheckIds = [  ];\n</script>\n";

    private static readonly string[] LoadBalancerCookies =
    [
        "AWSALB=one; Path=/",
        "AWSALBCORS=two; Path=/; SameSite=None; Secure",
    ];

    private static readonly string[] WholeSession =
    [
        "GET /ere/acolib/aco_call_xml.php",
        "GET /ere/aco/aco.php",
        "GET /ere/aco/aco2.php",
        "GET /ere/aco/selected.php",
        "GET /ere/aco/part.php",
        "POST /ere/aco/compproc.php",
        "GET /ere/aco/selected.php",
        "GET /ere/aco/service.php",
        "GET /ere/aco/close.php",
        "GET /ere/acolib/aco_call_xml.php",
    ];

    private static readonly string[] Removal =
    [
        "GET /ere/aco/part.php",
        "GET /ere/aco/compproc.php",
        "GET /ere/aco/selected.php",
    ];

    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeMilliseconds(1794979937000);

    private static string LaunchAddress =>
        GlassEditorFixture.Page("opening").GetProperty("launchAddress").GetString()!;

    [Fact]
    public async Task OpeningReadsTheKeyAndSendsBackTheCookiesItWasGiven()
    {
        using var editor = new ScriptedEditor();
        ScriptOpening(editor);
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, EmptyEstimate));
        var client = Client(editor);

        var session = await client.OpenAsync(LaunchAddress, default);
        var read = await client.ReadEstimateAsync(session, default);

        Assert.Equal(GlassEditorFixture.Key, session.Key);
        Assert.Equal(GlassEditorFixture.Base, session.BaseAddress);
        Assert.Equal("one", session.Cookies["AWSALB"]);
        Assert.Equal("two", session.Cookies["AWSALBCORS"]);
        Assert.Null(editor.Requests[0].Cookie);
        Assert.Equal("https://pegasus.test/", editor.Requests[0].Referer);
        Assert.Equal(LaunchAddress, editor.Requests[1].Referer);
        Assert.All(editor.Requests.Skip(1), request => Assert.Equal("AWSALB=one; AWSALBCORS=two", request.Cookie));
        Assert.All(editor.Requests, request => Assert.Equal("repairestimate.autovistagroup.com", request.Host));
        Assert.True(read.Value.Found);
        Assert.Empty(read.Value.Positions);
        Assert.DoesNotContain(GlassEditorFixture.Key, session.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("one", session.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWholeSessionAsksForWhatTheBrowserAsksForAndAnswersTheWayHome()
    {
        using var editor = new ScriptedEditor();
        ScriptOpening(editor);
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, EmptyEstimate));
        editor.Enqueue("GET part.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("special_fixed_price.dialog.html")));
        editor.Enqueue("POST compproc.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("special_fixed_price.answer.html")));
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("selected.html")));
        editor.Enqueue("GET service.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("save.answer.html")));
        editor.Enqueue("GET close.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("close.html")));
        editor.Enqueue("GET aco_call_xml.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("return.html")));
        var client = Client(editor);

        var session = await client.OpenAsync(LaunchAddress, default);
        var read = await client.ReadEstimateAsync(session, default);
        var added = await client.AddAsync(
            read.Session, read.Value, new GlassEditorAddRequest(GlassEditorDialog.SpecialOperation, "AC0000073", "73"), default);
        var home = await client.SaveAndExitAsync(added.Session, default);

        Assert.Equal(WholeSession, editor.Requests.Select(request => request.Method + " " + request.Path));
        Assert.Equal(
            GlassEditorFixture.Page("closing").GetProperty("home").GetProperty("address").GetString(),
            home);
        Assert.Equal(GlassEditorFixture.Text("special_fixed_price.post.txt"), editor.Requests[5].Body);
        Assert.Equal("?ksid=" + GlassEditorFixture.Key + "&mcode=AC0000073&cml=A&dialogCodePage=iso-8859-1&dummy=1794979937000",
            editor.Requests[4].Query);
        Assert.Equal(GlassEditorFixture.Base + "part.php" + editor.Requests[4].Query, editor.Requests[5].Referer);
        Assert.Equal(".Assessment Damage Appraisal Charge", added.Value.Summary.Description);
        Assert.Empty(added.Value.Listed);
        Assert.Equal(GlassEditorFixture.Strings(GlassEditorFixture.Page("selected"), "positions"), added.Value.Added);
        Assert.Equal(
            GlassEditorFixture.Page("closing").GetProperty("backAddress").GetString(),
            "https://" + editor.Requests[9].Host + editor.Requests[9].Path + editor.Requests[9].Query);
    }

    /// <summary>
    /// Typed hours above Glass's own with no reason (R1206): the dialog would
    /// ask for one, so nothing is posted, with the code that tells the
    /// Automation to give its reason. Only typed hours meet this; a rebuild
    /// types none.
    /// </summary>
    [Fact]
    public async Task TypedHoursThatNeedAReasonAreNeverPostedWithoutOne()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET part.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("special_hours.dialog.html")));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.AddAsync(
            GlassEditorFixture.Session(),
            NoPositions(),
            new GlassEditorAddRequest(GlassEditorDialog.SpecialOperation, "AC0000075", "75", Change: new GlassEditorChange(Hours: "0.80")),
            default));

        Assert.Equal(GlassAutomationPositionPolicy.ReasonRequiredCode, refused.FailureCode);
        Assert.False(refused.OutcomeUnknown);
        Assert.Contains("the dialog asks for a reason", refused.Detail, StringComparison.Ordinal);
        var only = Assert.Single(editor.Requests);
        Assert.Equal("GET", only.Method);
    }

    /// <summary>Any other problem the builder finds refuses the dialog with the editor's own code, and nothing is posted.</summary>
    [Fact]
    public async Task ADialogWithAProblemIsNeverPosted()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET part.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("special_hours.dialog.html")));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.AddAsync(
            GlassEditorFixture.Session(),
            NoPositions(),
            new GlassEditorAddRequest(GlassEditorDialog.SpecialOperation, "AC0000075", "75", Change: new GlassEditorChange(Operation: "XX")),
            default));

        Assert.Equal(GlassFailure.EditorRefused, refused.FailureCode);
        Assert.False(refused.OutcomeUnknown);
        Assert.Contains("The dialog does not offer the operation XX.", refused.Detail, StringComparison.Ordinal);
        var only = Assert.Single(editor.Requests);
        Assert.Equal("GET", only.Method);
    }

    [Fact]
    public async Task ADialogForAnotherPartIsNeverPosted()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET comp.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("door_left_part.dialog.html")));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.AddAsync(
            GlassEditorFixture.Session(),
            NoPositions(),
            new GlassEditorAddRequest(GlassEditorDialog.PaintablePart, "771|772", "1060000", Button: "BSS"),
            default));

        Assert.Equal(GlassFailure.EditorRefused, refused.FailureCode);
        Assert.Single(editor.Requests);
    }

    /// <summary>A dialog that serves another side than the line's is refused before anything is posted.</summary>
    [Fact]
    public async Task ADialogForTheOtherSideIsNeverPosted()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET comp.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("door_left_part.dialog.html")));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.AddAsync(
            GlassEditorFixture.Session(),
            NoPositions(),
            new GlassEditorAddRequest(GlassEditorDialog.PaintablePart, "771|772", "2020002", Button: "BSS", ExpectedSide: "R"),
            default));

        Assert.Equal(GlassFailure.EditorRefused, refused.FailureCode);
        Assert.False(refused.OutcomeUnknown);
        Assert.Equal("The dialog serves side L, not R.", refused.Detail);
        Assert.Single(editor.Requests);
    }

    /// <summary>A part with no side is refused when its dialog serves one.</summary>
    [Fact]
    public async Task ADialogWithASideIsNeverPostedForAPartWithNone()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET comp.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("door_left_part.dialog.html")));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.AddAsync(
            GlassEditorFixture.Session(),
            NoPositions(),
            new GlassEditorAddRequest(GlassEditorDialog.PaintablePart, "771|772", "2020001", Button: "BSS", ExpectedSide: string.Empty),
            default));

        Assert.Equal(GlassFailure.EditorRefused, refused.FailureCode);
        Assert.Equal("The dialog serves side L, not none.", refused.Detail);
        Assert.Single(editor.Requests);
    }

    /// <summary>
    /// A right-hand dialog carries the left code first and the right code
    /// second; a right-hand part is checked against the second.
    /// </summary>
    [Fact]
    public async Task ARightHandPartIsCheckedAgainstTheRightHandCode()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET part.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("reinforcement_right.dialog.html")));
        editor.Enqueue("GET part.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("reinforcement_right.dialog.html")));
        editor.Enqueue("POST compproc.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("reinforcement_right.answer.html")));
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("selected.html")));
        var client = Client(editor);
        const string Rows = "1783|ER|1786|AE|1792";

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.AddAsync(
            GlassEditorFixture.Session(), NoPositions(),
            new GlassEditorAddRequest(GlassEditorDialog.Part, Rows, "1012401", ExpectedSide: "R"), default));
        var added = await client.AddAsync(
            GlassEditorFixture.Session(), NoPositions(),
            new GlassEditorAddRequest(GlassEditorDialog.Part, Rows, "1012402", ExpectedSide: "R"), default);

        Assert.Equal(GlassFailure.EditorRefused, refused.FailureCode);
        Assert.Equal("The dialog carries part code 1012402, not 1012401.", refused.Detail);
        Assert.Equal("1012402", added.Value.Summary.RightPartCode);
        Assert.Equal(GlassEditorFixture.Text("reinforcement_right.post.txt"), editor.Requests[2].Body);
    }

    /// <summary>
    /// An equipment answer is stored at the address the browser asked for
    /// (R1206), and its answer must be the page that tells the list to read
    /// the criteria again; any other page may still have stored it.
    /// </summary>
    [Fact]
    public async Task AnEquipmentAnswerIsStoredAtTheAddressTheBrowserAskedFor()
    {
        using var editor = new ScriptedEditor();
        var page = GlassEditorFixture.Page("globcrit");
        editor.Enqueue("GET service.php", new(HttpStatusCode.OK, GlassEditorFixture.Text(page.GetProperty("file").GetString()!)));
        editor.Enqueue("GET service.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("save.answer.html")));
        var client = new GlassEditorClient(
            new HttpClient(editor, disposeHandler: false), GlassEditorFixture.Options(),
            new GlassEditorFixture.FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(1794979937123)), TimeSpan.Zero);

        var stored = await client.StoreEquipmentAsync(
            GlassEditorFixture.Session(), page.GetProperty("answers").GetString()!, default);
        var unread = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.StoreEquipmentAsync(
            GlassEditorFixture.Session(), page.GetProperty("answers").GetString()!, default));

        Assert.True(stored.Value);
        var first = editor.Requests[0];
        Assert.Equal(page.GetProperty("address").GetString(), "https://" + first.Host + first.Path + first.Query);
        Assert.Equal(GlassEditorFixture.Base + "acofr.php", first.Referer?.Split('?')[0]);
        Assert.Equal(GlassFailure.EditorAnswer, unread.FailureCode);
        Assert.True(unread.OutcomeUnknown);
        Assert.DoesNotContain(GlassEditorFixture.Key, unread.Detail ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// A part whose rows an equipment answer chose (R1206) is refused before
    /// its post when the dialog's record names another row pair, or the
    /// dialog carries another part number; the captured lining dialog is
    /// for <c>0|5</c> and number 6F0807221DGRU.
    /// </summary>
    [Theory]
    [InlineData("0|3", "6F0807221DGRU", "The dialog's record names other rows.")]
    [InlineData("0|5", "6F0807221XXX", "The dialog carries another part number.")]
    public async Task AnAnsweredPartsDialogForAnotherVariantIsNeverPosted(string rows, string number, string detail)
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET comp.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("bumper_part.dialog.html")));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.AddAsync(
            GlassEditorFixture.Session(),
            NoPositions(),
            new GlassEditorAddRequest(
                GlassEditorDialog.PaintablePart, rows, "1010800", Button: "BSS", ExpectedSide: string.Empty,
                CheckRows: true, ExpectedPartNumber: number),
            default));

        Assert.Equal(GlassFailure.EditorRefused, refused.FailureCode);
        Assert.Equal(detail, refused.Detail);
        Assert.Single(editor.Requests);
    }

    /// <summary>The part list is asked for at the address the browser asked for and read row by row.</summary>
    [Fact]
    public async Task ThePartListIsReadFromTheAddressTheBrowserAskedFor()
    {
        using var editor = new ScriptedEditor();
        var page = GlassEditorFixture.Page("partList");
        editor.Enqueue("GET list.php", new(HttpStatusCode.OK, GlassEditorFixture.Text(page.GetProperty("file").GetString()!)));
        var client = Client(editor);

        var read = await client.ReadPartListAsync(GlassEditorFixture.Session(), page.GetProperty("groups").GetString()!, default);

        Assert.Equal(page.GetProperty("rows").GetInt32(), read.Value.Rows.Count);
        Assert.Equal(170, read.Value.Criteria.Count);
        Assert.Equal("Criteria affects other parts!", read.Value.ReferencedNote);
        var only = Assert.Single(editor.Requests);
        Assert.Equal(page.GetProperty("address").GetString(), "https://" + only.Host + only.Path + Uri.UnescapeDataString(only.Query));
        Assert.Equal(GlassEditorFixture.Base + "acofr.php", only.Referer?.Split('?')[0]);
    }

    [Fact]
    public async Task APartWithMandatoryPartsIsConfirmedWithTheTicksAskedFor()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET comp.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("door_left_part.dialog.html")));
        editor.Enqueue("POST compproc.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("door_left_part.answer.html")));
        editor.Enqueue("POST compproc.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("door_left_part.confirm_answer.html")));
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("selected.html")));
        var client = Client(editor);
        var dialog = GlassEditorFixture.Dialog("door_left_part");

        var added = await client.AddAsync(
            GlassEditorFixture.Session(),
            NoPositions(),
            new GlassEditorAddRequest(
                GlassEditorDialog.PaintablePart, "771|772", "2020001", Button: "BSS",
                Ticks: GlassEditorFixture.Strings(dialog, "ticks")),
            default);

        Assert.Equal(GlassEditorFixture.Text("door_left_part.post.txt"), editor.Requests[1].Body);
        Assert.Equal(GlassEditorFixture.Text("door_left_part.confirm.txt"), editor.Requests[2].Body);
        Assert.Equal(
            "?ksid=" + GlassEditorFixture.Key + "&cml=C&BX=1&mcode=771|772", Uri.UnescapeDataString(editor.Requests[2].Query));
        Assert.Equal(3, added.Value.Listed.Count);
    }

    [Fact]
    public async Task AnotherHostIsNeverAsked()
    {
        using var editor = new ScriptedEditor();
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.OpenAsync(
            "https://elsewhere.test/ere/acolib/aco_call_xml.php?WorkTime=1", default));

        Assert.Equal(GlassFailure.EditorHost, refused.FailureCode);
        Assert.Empty(editor.Requests);
    }

    [Fact]
    public async Task ARedirectIsAFailureAndIsNotFollowed()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET aco_call_xml.php", new(
            HttpStatusCode.Found, string.Empty, Location: "https://repairestimate.autovistagroup.com/ere/aco/aco.php"));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.OpenAsync(LaunchAddress, default));

        Assert.Equal(GlassFailure.EditorLaunch, refused.FailureCode);
        Assert.Contains("redirect", refused.Detail, StringComparison.Ordinal);
        Assert.Single(editor.Requests);
    }

    [Fact]
    public async Task ABlockedLaunchIsRefused()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET aco_call_xml.php", new(
            HttpStatusCode.OK, "<script>document.location.replace( 'blocked.html?caller=x' );</script>"));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() => client.OpenAsync(LaunchAddress, default));

        Assert.Equal(GlassFailure.EditorBlocked, refused.FailureCode);
        Assert.Single(editor.Requests);
    }

    [Fact]
    public async Task RemovalAsksForThePositionsDialogFirst()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET part.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("mirror_left.dialog.html")));
        editor.Enqueue("GET compproc.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("removed.answer.html")));
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("selected.html")));
        var client = Client(editor);
        var id = GlassEditorFixture.Page("removed").GetProperty("id").GetString()!;

        var after = await client.RemoveAsync(GlassEditorFixture.Session(), id, default);

        Assert.Equal(Removal, editor.Requests.Select(request => request.Method + " " + request.Path));
        Assert.Equal(
            "?ksid=" + GlassEditorFixture.Key + "&cml=C&partid=" + id + "&dialogCodePage=iso-8859-1&dummy=1794979937000",
            editor.Requests[0].Query);
        Assert.Equal(GlassEditorFixture.Base + "part.php" + editor.Requests[0].Query, editor.Requests[1].Referer);
        Assert.Equal("?ksid=" + GlassEditorFixture.Key + "&cml=C&partid=" + id + "&dummy=1794979937000", editor.Requests[1].Query);
        Assert.DoesNotContain(id, after.Value.Positions);
    }

    [Fact]
    public async Task ARemovalTheEstimateStillListsIsAFailure()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET part.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("mirror_left.dialog.html")));
        editor.Enqueue("GET compproc.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("removed.answer.html")));
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("selected.html")));
        var client = Client(editor);

        var refused = await Assert.ThrowsAsync<GlassMvaStageException>(() =>
            client.RemoveAsync(GlassEditorFixture.Session(), "C350926.27.C2027021.4646", default));

        Assert.Equal(GlassFailure.EditorAnswer, refused.FailureCode);
    }

    /// <summary>
    /// A Glass's server that does not hold the session answers 200 with a
    /// page that has none of the editor's lists (first live session, 19
    /// November 2026). The failure names the page asked for and the answer's
    /// length, which is all the host log needs to tell such an answer from
    /// the editor's own, and nothing of the answer itself.
    /// </summary>
    [Fact]
    public async Task AnAnswerWithNoneOfTheEditorsListsSaysItsPageAndLength()
    {
        const string NoSession = "<html><body>no such session</body></html>";
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, NoSession));
        editor.Enqueue("GET list_1G.php", new(HttpStatusCode.OK, NoSession));
        var client = Client(editor);

        var read = await Assert.ThrowsAsync<GlassMvaStageException>(() =>
            client.ReadEstimateAsync(GlassEditorFixture.Session(), default));
        var listed = await Assert.ThrowsAsync<GlassMvaStageException>(() =>
            client.ReadSpecialOperationsAsync(GlassEditorFixture.Session(), "DS-AUXWORK-702", default));

        Assert.Equal(GlassFailure.EditorAnswer, read.FailureCode);
        Assert.Equal("page=selected length=41", read.Detail);
        Assert.Equal(GlassFailure.EditorAnswer, listed.FailureCode);
        Assert.Equal("page=special-list length=41", listed.Detail);
    }

    /// <summary>
    /// The pace holds across clients (design 07 section 6): a client that
    /// continues an editor another client used is given that client's last
    /// request time, so its first request still waits out the pause, and it
    /// reports its own afterwards to be kept for the next.
    /// </summary>
    [Fact]
    public async Task APaceCarriedFromAnotherClientHoldsItsFirstRequest()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET selected.php", new(HttpStatusCode.OK, EmptyEstimate));
        var clock = new GlassEditorFixture.FixedClock(Now);
        var client = new GlassEditorClient(
            new HttpClient(editor, disposeHandler: false), GlassEditorFixture.Options(), clock, TimeSpan.FromSeconds(1))
        {
            LastRequestAtUtc = Now,
        };

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var read = await client.ReadEstimateAsync(GlassEditorFixture.Session(), default);

        Assert.True(read.Value.Found);
        Assert.True(
            System.Diagnostics.Stopwatch.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(900),
            "The first request must wait out the pause the previous client left.");
        Assert.Equal(Now, client.LastRequestAtUtc);
        Assert.Null(Client(editor).LastRequestAtUtc);
    }

    /// <summary>
    /// The calculation is asked for as the editor's top bar asks for it, one
    /// request; a position id that is not as Glass's lists them is refused
    /// before anything is sent, so nothing can be added to the address.
    /// </summary>
    [Fact]
    public async Task TheCalculationIsAskedForAsTheEditorAsksAndAnOddPositionIdIsNeverSent()
    {
        using var editor = new ScriptedEditor();
        editor.Enqueue("GET report.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("report.html")));
        var client = Client(editor);

        var read = await client.ReadCalculationAsync(GlassEditorFixture.Session(), default);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadPositionAsync(GlassEditorFixture.Session(), "C1.2&cml=C", default));

        var asked = Assert.Single(editor.Requests);
        Assert.Equal("/ere/aco/report.php", asked.Path);
        Assert.Equal("?ksid=" + GlassEditorFixture.Key + "&cml=C&calcid=0&dummy=1794979937000", asked.Query);
        Assert.Equal(GlassEditorFixture.Page("opening").GetProperty("addresses").GetProperty("frame").GetString(), asked.Referer);
        Assert.Equal(13209.89m, read.Value.RepairCostsInclVat);
    }

    private static GlassEditorEstimate NoPositions() => new(true, Array.Empty<string>(), Array.Empty<string>());

    private static GlassEditorClient Client(ScriptedEditor editor) =>
        new(new HttpClient(editor, disposeHandler: false), GlassEditorFixture.Options(), new GlassEditorFixture.FixedClock(Now), TimeSpan.Zero);

    private static void ScriptOpening(ScriptedEditor editor)
    {
        editor.Enqueue("GET aco_call_xml.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("launch.html"), SetCookies: LoadBalancerCookies));
        editor.Enqueue("GET aco.php", new(HttpStatusCode.OK, "<html><body></body></html>"));
        editor.Enqueue("GET aco2.php", new(HttpStatusCode.OK, GlassEditorFixture.Text("aco2.html")));
    }
}

/// <summary>One scripted editor answer, its body sent as windows-1252 as the editor sends it.</summary>
internal sealed record EditorReply(
    HttpStatusCode Status, string Body, string? Location = null, IReadOnlyList<string>? SetCookies = null);

/// <summary>What one request to the scripted editor carried.</summary>
internal sealed record EditorRequest(
    string Method, string Host, string Path, string Query, string? Body, string? Cookie, string? Referer);

/// <summary>
/// The scripted editor. Answers are queued by method and page name
/// (<c>GET selected.php</c>) and served in order; a request nothing was
/// queued for is answered 404.
/// </summary>
internal sealed class ScriptedEditor : HttpMessageHandler
{
    private readonly Dictionary<string, Queue<EditorReply>> replies = new(StringComparer.Ordinal);

    public List<EditorRequest> Requests { get; } = [];

    public void Enqueue(string route, EditorReply reply)
    {
        if (!replies.TryGetValue(route, out var queue))
        {
            queue = new Queue<EditorReply>();
            replies[route] = queue;
        }

        queue.Enqueue(reply);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var path = uri.AbsolutePath;
        Requests.Add(new(
            request.Method.Method,
            uri.Host,
            path,
            uri.Query,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            Header(request, "Cookie"),
            Header(request, "Referer")));

        var page = path[(path.LastIndexOf('/') + 1)..];
        var route = request.Method.Method + " " + page;
        if (!replies.TryGetValue(route, out var queue) || queue.Count == 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var reply = queue.Dequeue();
        var response = new HttpResponseMessage(reply.Status)
        {
            Content = new ByteArrayContent(GlassEditorText.Encode(reply.Body)),
        };
        if (reply.Location is not null)
        {
            response.Headers.Location = new Uri(reply.Location);
        }
        foreach (var cookie in reply.SetCookies ?? [])
        {
            response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
        }

        return response;
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(", ", values) : null;
}
