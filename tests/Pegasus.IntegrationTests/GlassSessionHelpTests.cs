using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Automation's help in an Engineer's open Glass's session (design 08
/// sections 5 and 6, slice D2; R1170, R1182, R1185, R1206: paint, Both, preview) against the scripted
/// editor of <see cref="GlassProviderFixture"/>, on a key held as the helper
/// hands it over: an add reads the estimate before and after and tells its
/// own position from those the Engineer added meanwhile; a lost answer is not
/// confirmed and still asks the window to show its list again; a session
/// another help act holds, one with help off, one with no window and one no
/// longer Active are refused before anything is sent, the last forgetting its
/// key; any position may be removed. Every key and token is made up.
/// </summary>
public sealed class GlassSessionHelpTests
{
    /// <summary>A position of the captured estimate the Automation did not add (R1185).</summary>
    private const string EngineersPosition = "C350926.27.C2027021.4646";

    /// <summary>The position the special operation AC0000073 puts on the captured estimate.</summary>
    private const string ChargePosition = "A350880.1.A73.A73";

    private const string NoPositions = "<script>\nvar Partids = [ '' ];\nvar CheckIds = [  ];\n</script>\n";

    private static readonly ActionActor Automation = ActionActor.Automation("automation-grant-help");

    /// <summary>The door's optional trim support kit, as its "Mandatory parts:" page lists it.</summary>
    private static readonly string[] TrimSupportKit = ["8099801.ER."];

    /// <summary>The two reinforcement positions the "Both" add puts on the estimate; made up.</summary>
    private static readonly string[] BothReinforcements = ["C1.1.C1012401.1", "C2.2.C1012402.2"];

    /// <summary>The load balancer's cookies as a window's helper hands them over; made up.</summary>
    private static readonly Dictionary<string, string> WindowsCookies = new()
    {
        ["AWSALB"] = "madeUpWindowOne",
        ["AWSALBCORS"] = "madeUpWindowTwo",
    };

    [Fact]
    public async Task AnAddReadsBeforeAndAfterAndTellsItsOwnPositionFromTheEngineers()
    {
        var help = Help.Create();
        using var window = help.Window();

        var outcome = await help.Sessions.AddAsync(Add(help, "mcp:help-add-1"), help.Session, CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Added, outcome.Act.Outcome);
        Assert.Equal(ChargePosition, Assert.Single(outcome.Act.PositionIds));
        Assert.Equal(new GlassEstimateBuildFigures(176.96m, null), outcome.Act.Served);
        // Every other position on the estimate came from the Engineer's window meanwhile.
        Assert.Contains(EngineersPosition, outcome.AlsoNew);
        Assert.DoesNotContain(ChargePosition, outcome.AlsoNew);
        Assert.NotNull(outcome.Positions);
        Assert.Contains(ChargePosition, outcome.Positions);
        Assert.True(outcome.ShownInWindow);
        // The estimate was read before the post and after it.
        Assert.Equal("/ere/aco/selected.php", help.Glass.Requests[0].Path);
        Assert.Equal("/ere/aco/selected.php", help.Glass.Requests[^1].Path);
        Assert.Equal(1, help.Glass.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(1, help.Keys.Poll(help.Session.Id, 0).Refresh);
        var recorded = Assert.Single(help.Store.Acts);
        Assert.Equal("mcp:help-add-1", recorded.OperationKey);
        Assert.Equal(GlassAutomationActOutcome.Added, recorded.Outcome);
        // Nothing of Glass's save or exit was asked for.
        Assert.Equal(0, help.Glass.Count("GET /ere/aco/close.php"));
        Assert.DoesNotContain(help.Glass.Requests, request => request.Query.Contains("DoSave", StringComparison.Ordinal));
    }

    /// <summary>The BMW mirror's answer of 18 November 2026: automatic dimming only (r18 #872).</summary>
    private static readonly string[] Dimming = ["21115"];

    /// <summary>
    /// An answered part's requests in the Engineer's session (R1206): the
    /// estimate, the part list, the mirror's map, the stored answer, the
    /// variant's dialog (part.php, as row 822 cannot be painted), its post,
    /// the estimate.
    /// </summary>
    private static readonly string[] AnsweredMirror =
    [
        "GET /ere/aco/selected.php",
        "GET /ere/aco/list.php",
        "GET /ere/aco/service.php",
        "GET /ere/aco/service.php",
        "GET /ere/aco/part.php",
        "POST /ere/aco/compproc.php",
        "GET /ere/aco/selected.php",
    ];

    /// <summary>
    /// In an Engineer's session with help on, the Automation answers a
    /// part's equipment question as in its own (R1206): the BMW mirror's
    /// answer leads through its map's part group to the variant row 822,
    /// is stored as the editor stored it on 18 November 2026, and the
    /// variant's dialog is posted as served. The history row keeps the
    /// answer, the pair and the value it changed.
    /// </summary>
    [Fact]
    public async Task AnAnsweredPartIsAddedInTheEngineersSession()
    {
        var help = Help.Create();
        using var window = help.Window();

        var outcome = await help.Sessions.AddAsync(
            new GlassAutomationPositionRequest(Automation, help.Session.Id, help.Session.Version, "mcp:help-mirror-1",
                GlassAutomationActAction.Add)
            {
                Kind = GlassAutomationPositionKind.Part,
                Row = "821",
                Operation = GlassAutomationPartOperation.Part,
                Inputs = new GlassAutomationInputs(Dimming),
            },
            help.Session,
            CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Added, outcome.Act.Outcome);
        Assert.Equal(EngineersPosition, Assert.Single(outcome.Act.PositionIds));
        Assert.Equal("822|829", outcome.Act.RowPair);
        Assert.True(outcome.Act.EquipmentStored);
        Assert.Equal(new GlassEquipmentChange("21115", true), Assert.Single(outcome.Act.EquipmentChanged));
        Assert.Equal(new GlassEstimateBuildFigures(383.48m, 0.80m), outcome.Act.Served);
        Assert.Equal(AnsweredMirror, help.Glass.Requests.Select(request => request.Method + " " + request.Path));
        Assert.Contains(
            "&globcrit=" + Uri.EscapeDataString(GlassEditorFixture.Page("equipment821").GetProperty("answers").GetString()!) + "&",
            help.Glass.Requests[3].Query,
            StringComparison.Ordinal);
        Assert.Contains("rowid=821&reqcritmap3=1", help.Glass.Requests[2].Query, StringComparison.Ordinal);
        Assert.Contains("mcode=822|829", Uri.UnescapeDataString(help.Glass.Requests[4].Query), StringComparison.Ordinal);
        var recorded = Assert.Single(help.Store.Acts);
        Assert.Equal(Dimming, recorded.Inputs?.Equipment);
        Assert.Equal("822|829", recorded.RowPair);
        Assert.True(recorded.EquipmentStored);
    }

    /// <summary>A part whose row asks, added with no answer, is refused with its question and nothing is sent (R1206).</summary>
    [Fact]
    public async Task APartWithNoAnswerIsRefusedWithItsQuestionInTheEngineersSession()
    {
        var help = Help.Create();

        var outcome = await help.Sessions.AddAsync(
            new GlassAutomationPositionRequest(Automation, help.Session.Id, help.Session.Version, "mcp:help-mirror-2",
                GlassAutomationActAction.Add)
            {
                Kind = GlassAutomationPositionKind.Part,
                Row = "821",
                Operation = GlassAutomationPartOperation.Part,
            },
            help.Session,
            CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Refused, outcome.Act.Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.EquipmentCode, outcome.Act.Code);
        Assert.NotNull(outcome.Act.Equipment);
        Assert.Equal(
            "Outside Mirrors Automatic Dimming",
            Assert.Single(outcome.Act.Equipment.Options, option => option.Code == "21115").Name);
        Assert.Equal("160", Assert.Single(outcome.Act.Equipment.Options, option => option.Code == "85142").Group);
        Assert.Equal(0, help.Glass.Count("GET /ere/aco/part.php"));
        Assert.Equal(0, help.Glass.Count("POST /ere/aco/compproc.php"));
        Assert.DoesNotContain(help.Glass.Requests, request => request.Query.Contains("globcrit=", StringComparison.Ordinal));
        Assert.False(outcome.ShownInWindow);
    }

    [Fact]
    public async Task ALostAnswerIsNotConfirmedAndStillAsksTheWindowToShowItsList()
    {
        var help = Help.Create();
        help.Glass.Respond("GET /ere/aco/selected.php", _ => Editor(NoPositions));
        help.Glass.Enqueue("POST /ere/aco/compproc.php", new Reply(HttpStatusCode.InternalServerError, string.Empty));

        var outcome = await help.Sessions.AddAsync(Add(help, "mcp:help-add-lost"), help.Session, CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.NotConfirmed, outcome.Act.Outcome);
        Assert.NotNull(outcome.Act.Code);
        Assert.Equal("A73", outcome.Act.ExpectedCode);
        Assert.Empty(outcome.AlsoNew);
        // No window answered within the wait, so it is not said to show it.
        Assert.False(outcome.ShownInWindow);
        Assert.Equal(1, help.Keys.Poll(help.Session.Id, 0).Refresh);
        Assert.Equal(GlassAutomationActOutcome.NotConfirmed, Assert.Single(help.Store.Acts).Outcome);
    }

    [Fact]
    public async Task AnAddWhoseCodeIsOnNoNewPositionIsNotConfirmed()
    {
        var help = Help.Create();
        var captured = GlassEditorFixture.Text("selected.html");
        // Before the post the estimate already holds the charge; after it,
        // only the Engineer's own new position has come.
        var withoutEngineers = captured.Replace("'" + EngineersPosition + "', ", string.Empty, StringComparison.Ordinal);
        help.Glass.Respond("GET /ere/aco/selected.php", _ => Editor(
            help.Glass.Count("POST /ere/aco/compproc.php") == 0 ? withoutEngineers : captured));
        using var window = help.Window();

        var outcome = await help.Sessions.AddAsync(Add(help, "mcp:help-add-2"), help.Session, CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.NotConfirmed, outcome.Act.Outcome);
        Assert.Equal(GlassFailure.EditorAnswer, outcome.Act.Code);
        Assert.Empty(outcome.Act.PositionIds);
        Assert.Equal(EngineersPosition, Assert.Single(outcome.AlsoNew));
    }

    [Fact]
    public async Task APositionTheEngineerAddedIsRemoved()
    {
        var help = Help.Create();
        var captured = GlassEditorFixture.Text("selected.html");
        help.Glass.Respond("GET /ere/aco/selected.php", _ => Editor(
            help.Glass.Count("GET /ere/aco/compproc.php") == 0
                ? captured
                : captured.Replace("'" + EngineersPosition + "', ", string.Empty, StringComparison.Ordinal)));
        help.Glass.Enqueue("GET /ere/aco/part.php", EditorPage("mirror_left.dialog.html", null));
        help.Glass.Enqueue("GET /ere/aco/compproc.php", EditorPage("removed.answer.html", null));
        using var window = help.Window();

        var outcome = await help.Sessions.RemoveAsync(
            new GlassAutomationPositionRequest(Automation, help.Session.Id, help.Session.Version, "mcp:help-remove-1",
                GlassAutomationActAction.Remove)
            {
                PositionId = EngineersPosition,
            },
            help.Session,
            CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Removed, outcome.Act.Outcome);
        Assert.NotNull(outcome.Positions);
        Assert.DoesNotContain(EngineersPosition, outcome.Positions);
        Assert.Empty(outcome.AlsoNew);
        Assert.True(outcome.ShownInWindow);
        Assert.Equal(GlassAutomationActOutcome.Removed, Assert.Single(help.Store.Acts).Outcome);
    }

    [Fact]
    public async Task ASessionAnotherHelpActHoldsIsBusyAndNothingIsSent()
    {
        var help = Help.Create();
        using var held = help.Keys.TryEnter(help.Session.Id);
        Assert.NotNull(held);

        var refused = await Assert.ThrowsAsync<GlassSessionHelpRefusalException>(() =>
            help.Sessions.AddAsync(Add(help, "mcp:help-add-busy"), help.Session, CancellationToken.None));

        Assert.Equal(GlassSessionHelpPolicy.BusyCode, refused.Code);
        Assert.Empty(help.Glass.Requests);
        Assert.Empty(help.Store.Acts);
    }

    [Fact]
    public async Task ASessionNoLongerActiveIsRefusedAndItsKeyForgotten()
    {
        var help = Help.Create();
        var saved = help.Session with { State = GlassRepairEstimateSessionState.Importing };

        var refused = await Assert.ThrowsAsync<GlassSessionHelpRefusalException>(() =>
            help.Sessions.ReadAsync(Automation, saved, GlassAutomationRead.Positions, CancellationToken.None));

        Assert.Equal(GlassSessionHelpPolicy.NotActiveCode, refused.Code);
        Assert.False(help.Keys.IsHeld(help.Session.Id));
        Assert.Empty(help.Glass.Requests);
    }

    [Fact]
    public async Task HelpOffAndNoWindowAreRefusedBeforeAnythingIsSent()
    {
        var help = Help.Create();

        var off = await Assert.ThrowsAsync<GlassSessionHelpRefusalException>(() =>
            help.Sessions.ReadAsync(Automation, help.Session with { HelpOnAtUtc = null }, GlassAutomationRead.Positions, CancellationToken.None));
        help.Keys.Drop(help.Session.Id);
        var unconnected = await Assert.ThrowsAsync<GlassSessionHelpRefusalException>(() =>
            help.Sessions.ReadAsync(Automation, help.Session, GlassAutomationRead.Positions, CancellationToken.None));

        Assert.Equal(GlassSessionHelpPolicy.OffCode, off.Code);
        Assert.Equal(GlassSessionHelpPolicy.NoWindowCode, unconnected.Code);
        Assert.Empty(help.Glass.Requests);
    }

    /// <summary>
    /// Part and Paint is added in the Engineer's session as in the
    /// Automation's own (R1206): the button pressed, the inputs kept on the
    /// act's history row beside Glass's own figures.
    /// </summary>
    [Fact]
    public async Task PartAndPaintIsAddedInTheEngineersSessionWithItsInputsRecorded()
    {
        var help = Help.Create();

        var outcome = await help.Sessions.AddAsync(
            new GlassAutomationPositionRequest(Automation, help.Session.Id, help.Session.Version, "mcp:help-paint",
                GlassAutomationActAction.Add)
            {
                Kind = GlassAutomationPositionKind.Part,
                Row = "771",
                Operation = GlassAutomationPartOperation.Part,
                Inputs = new GlassAutomationInputs(Button: GlassAutomationButton.PartAndPaint, OptionalParts: TrimSupportKit),
            },
            help.Session,
            CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Added, outcome.Act.Outcome);
        var posted = help.Glass.Requests.First(request => request.Method == "POST").Body;
        Assert.Contains("&BSL=Part+and+Paint&", posted, StringComparison.Ordinal);
        var recorded = Assert.Single(help.Store.Acts);
        Assert.Equal(GlassAutomationButton.PartAndPaint, recorded.Inputs?.Button);
        Assert.Equal(TrimSupportKit, recorded.Inputs?.OptionalParts);
        Assert.Equal(new GlassEstimateBuildFigures(545.24m, 2.90m), recorded.Served);
        Assert.True(Assert.Single(recorded.Listed, listed => listed.Code == "8099801.ER.").Ticked);
    }

    /// <summary>
    /// "Both" in the Engineer's session: the left side's post, then the
    /// right side's own dialog and post; both positions are the act's own,
    /// told by their two codes from those the Engineer added meanwhile.
    /// </summary>
    [Fact]
    public async Task BothSidesInTheEngineersSessionAreTheActsOwnPositions()
    {
        var help = Help.Create();
        help.Glass.Respond("GET /ere/aco/selected.php", _ => Editor(help.Glass.Count("POST /ere/aco/compproc.php") switch
        {
            0 => NoPositions,
            1 => "<script>\nvar Partids = [ '', 'C1.1.C1012401.1', '" + EngineersPosition + "' ];\nvar CheckIds = [ 'C1012401', 'C2027021' ];\n</script>\n",
            _ => "<script>\nvar Partids = [ '', 'C1.1.C1012401.1', '" + EngineersPosition + "', 'C2.2.C1012402.2' ];\nvar CheckIds = [ 'C1012401', 'C2027021', 'C1012402' ];\n</script>\n",
        }));
        using var window = help.Window();

        var outcome = await help.Sessions.AddAsync(
            new GlassAutomationPositionRequest(Automation, help.Session.Id, help.Session.Version, "mcp:help-both",
                GlassAutomationActAction.Add)
            {
                Kind = GlassAutomationPositionKind.Part,
                Row = "193",
                Operation = GlassAutomationPartOperation.Part,
                Inputs = new GlassAutomationInputs(BothSides: true),
            },
            help.Session,
            CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Added, outcome.Act.Outcome);
        Assert.Equal(BothReinforcements, outcome.Act.PositionIds);
        Assert.Equal(EngineersPosition, Assert.Single(outcome.AlsoNew));
        Assert.Equal(2, help.Glass.Count("GET /ere/aco/part.php"));
        Assert.Equal(2, help.Glass.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// A preview in the Engineer's session (R1206) reads the lists and the
    /// dialog on the held key, posts nothing, records no history row and
    /// does not ask the window to redraw.
    /// </summary>
    [Fact]
    public async Task APreviewInTheEngineersSessionPostsNothingAndRecordsNothing()
    {
        var help = Help.Create();

        var preview = await help.Sessions.PreviewAsync(
            new GlassAutomationPositionRequest(Automation, help.Session.Id, 0, "mcp:help-preview",
                GlassAutomationActAction.Preview)
            {
                Kind = GlassAutomationPositionKind.Part,
                Row = "193",
                Operation = GlassAutomationPartOperation.Part,
                Inputs = new GlassAutomationInputs(BothSides: true),
            },
            help.Session,
            CancellationToken.None);

        Assert.Null(preview.Code);
        Assert.NotNull(preview.Dialog);
        Assert.Equal("part", preview.Dialog.Kind);
        Assert.True(Assert.Single(preview.Dialog.Fields, field => field.Name == "Both").Changeable);
        Assert.NotNull(preview.Dialog.Sends);
        Assert.True(preview.Dialog.Sends.BothSides);
        Assert.Null(preview.Dialog.Sends.Code);
        Assert.Contains(preview.Dialog.Sends.Questions, question => question.Kind == "other_side");
        Assert.Equal(3, help.Glass.Requests.Count);
        Assert.Equal(0, help.Glass.Count("POST /ere/aco/compproc.php"));
        Assert.Empty(help.Store.Acts);
        Assert.Equal(0, help.Keys.Poll(help.Session.Id, 0).Refresh);
    }

    [Fact]
    public async Task TheSearchReadsTheListsOnTheHeldKeyAndKeepsItsCookies()
    {
        var help = Help.Create();
        help.Glass.Enqueue("GET /ere/aco/list_1G.php", new Reply(
            HttpStatusCode.OK,
            GlassEditorFixture.Text("special_list.html"),
            SetCookie: "AWSALB=made-up-balancer; path=/",
            Bytes: GlassEditorText.Encode(GlassEditorFixture.Text("special_list.html"))));

        var found = await help.Sessions.SearchAsync(
            new GlassAutomationCatalogueQuery(Automation, help.Session.Id, "appraisal", GlassAutomationPositionKind.SpecialOperation, null),
            help.Session,
            CancellationToken.None);

        Assert.Equal("AC0000073", Assert.Single(found.Rows).Row);
        Assert.Null(found.Code);
        Assert.Equal("made-up-balancer", help.Keys.Session(help.Session.Id)?.Cookies["AWSALB"]);
        Assert.Equal(0, help.Glass.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// The editor's session lives on one Glass's server and the load
    /// balancer's cookies name it (first live session, 19 November 2026: 7
    /// of 8 requests sent on the key alone were answered by a server that
    /// did not hold it). The helper hands the window's cookies over with the
    /// key, so the first request Pegasus sends carries them.
    /// </summary>
    [Fact]
    public async Task TheFirstRequestCarriesTheLoadBalancersCookiesTheWindowHandedOver()
    {
        var help = Help.Create(WindowsCookies);

        var read = await help.Sessions.ReadAsync(Automation, help.Session, GlassAutomationRead.Positions, CancellationToken.None);

        Assert.NotNull(read.PositionIds);
        Assert.Equal("AWSALB=madeUpWindowOne; AWSALBCORS=madeUpWindowTwo", Assert.Single(help.Glass.Requests).Cookie);
    }

    /// <summary>
    /// A server that does not hold the session answers a page with none of
    /// the editor's lists and sets its own load balancer cookie. The search
    /// answers its code and no rows, and the cookies the window handed over
    /// stay as they were: the next request still goes to the window's server.
    /// </summary>
    [Fact]
    public async Task AnAnswerWithNoListsLeavesTheWindowsCookiesAsTheyWere()
    {
        var help = Help.Create(WindowsCookies);
        help.Glass.Enqueue("GET /ere/aco/list_1G.php", new Reply(
            HttpStatusCode.OK, "<html><body>no such session</body></html>", SetCookie: "AWSALB=madeUpOtherServer; path=/"));

        var found = await help.Sessions.SearchAsync(
            new GlassAutomationCatalogueQuery(Automation, help.Session.Id, "appraisal", GlassAutomationPositionKind.SpecialOperation, null),
            help.Session,
            CancellationToken.None);
        var read = await help.Sessions.ReadAsync(Automation, help.Session, GlassAutomationRead.Positions, CancellationToken.None);

        Assert.Empty(found.Rows);
        Assert.Equal(GlassFailure.EditorAnswer, found.Code);
        Assert.NotNull(read.PositionIds);
        Assert.All(
            help.Glass.Requests,
            request => Assert.Equal("AWSALB=madeUpWindowOne; AWSALBCORS=madeUpWindowTwo", request.Cookie));
    }

    /// <summary>
    /// In an Engineer's session the read is the Automation's own (R1206):
    /// the positions with Glass's running totals, one position's figures and
    /// the calculation, each with one request on the held key and nothing
    /// posted. A position Glass's does not answer for answers its code and
    /// nothing read.
    /// </summary>
    [Fact]
    public async Task TheReadAnswersTotalsAPositionAndTheCalculationAndAFailedReadItsCode()
    {
        var help = Help.Create();

        var positions = await help.Sessions.ReadAsync(Automation, help.Session, GlassAutomationRead.Positions, CancellationToken.None);
        var position = await help.Sessions.ReadAsync(
            Automation, help.Session, GlassAutomationRead.Position("C350898.20.C1060000.2464"), CancellationToken.None);
        var calculation = await help.Sessions.ReadAsync(Automation, help.Session, GlassAutomationRead.Calculation, CancellationToken.None);
        var unknown = await help.Sessions.ReadAsync(
            Automation, help.Session, GlassAutomationRead.Position("C1.2.C3.4"), CancellationToken.None);

        Assert.NotNull(positions.PositionIds);
        Assert.Equal("Amount due (£)", Assert.Single(positions.Totals!).Label);
        Assert.Null(positions.Code);
        Assert.Equal(412.12m, position.Position?.Price);
        Assert.Equal(697.10m, position.Position?.Paint?.Price);
        Assert.Equal(13209.89m, calculation.Calculation?.RepairCostsInclVat);
        Assert.Null(unknown.Position);
        Assert.Null(unknown.PositionIds);
        Assert.Equal(GlassFailure.EditorRequest, unknown.Code);
        Assert.Equal(4, help.Glass.Requests.Count);
        Assert.Equal(0, help.Glass.Count("POST /ere/aco/compproc.php"));
        Assert.Empty(help.Store.Acts);
    }

    private static GlassAutomationPositionRequest Add(Help help, string key) =>
        new(Automation, help.Session.Id, help.Session.Version, key, GlassAutomationActAction.Add)
        {
            Kind = GlassAutomationPositionKind.SpecialOperation,
            Row = "AC0000073",
        };

    /// <summary>The help, its scripted editor, its keys holding one made-up key, and the Engineer's session.</summary>
    private sealed class Help
    {
        private Help(ScriptedGlass glass, GlassHelpKeys keys, RecordingHelpStore store, GlassSessionHelp sessions,
            GlassRepairEstimateSession session)
        {
            Glass = glass;
            Keys = keys;
            Store = store;
            Sessions = sessions;
            Session = session;
        }

        public ScriptedGlass Glass { get; }

        public GlassHelpKeys Keys { get; }

        public RecordingHelpStore Store { get; }

        public GlassSessionHelp Sessions { get; }

        public GlassRepairEstimateSession Session { get; }

        public static Help Create(IReadOnlyDictionary<string, string>? windowsCookies = null)
        {
            var glass = new ScriptedGlass();
            ScriptEditor(glass);
            // The editor's own pace is a browser's; here it waits for nothing.
            var options = GlassHelpFrameTests.Options with { EditorPause = TimeSpan.Zero };
            var keys = new GlassHelpKeys(options, TimeProvider.System);
            var session = new GlassRepairEstimateSession(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "engineer-account-key",
                GlassRepairEstimateSessionState.Active, 4, "launch-help-1",
                new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero), VehicleId, EreId, null)
            {
                HelpOnAtUtc = new DateTimeOffset(2031, 5, 6, 10, 35, 0, TimeSpan.Zero),
            };
            keys.Hold(session.Id, keys.Read(GlassHelpFrameTests.OwnFrame(), windowsCookies)
                ?? throw new InvalidOperationException("The frame was not read."));
            var store = new RecordingHelpStore();
            var sessions = new GlassSessionHelp(
                keys, new ScriptedClientFactory(glass), options, store, TimeProvider.System,
                NullLogger<GlassSessionHelp>.Instance);
            return new(glass, keys, store, sessions, session);
        }

        /// <summary>
        /// The Engineer's window as the helper polls it: each poll reports
        /// the last ask to show the list that the previous answer carried.
        /// </summary>
        public IDisposable Window()
        {
            var stop = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                long seen = 0;
                while (!stop.IsCancellationRequested)
                {
                    seen = Keys.Poll(Session.Id, seen).Refresh;
                    try
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(20), stop.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            });
            return new Closer(stop);
        }

        private sealed class Closer(CancellationTokenSource stop) : IDisposable
        {
            public void Dispose()
            {
                stop.Cancel();
                stop.Dispose();
            }
        }
    }

    /// <summary>The help store's history rows, kept in memory.</summary>
    private sealed class RecordingHelpStore : IGlassSessionHelpStore
    {
        private readonly List<GlassAutomationAct> acts = [];

        public IReadOnlyList<GlassAutomationAct> Acts
        {
            get
            {
                lock (acts)
                {
                    return acts.ToList();
                }
            }
        }

        public Task<bool> SetHelpAsync(Guid sessionId, ActionActor actor, bool turnOn, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task RecordHelpActAsync(
            Guid sessionId, Guid caseId, ActionActor actor, GlassAutomationAct act, CancellationToken cancellationToken)
        {
            lock (acts)
            {
                acts.Add(act);
            }

            return Task.CompletedTask;
        }

        public Task<GlassSessionHelpRecordedAct?> FindHelpActAsync(
            Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult<GlassSessionHelpRecordedAct?>(null);
    }
}
