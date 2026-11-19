using System.Net;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

// The Automation's catalogue search and position acts in its own Glass's
// session (design 07 sections 3.3, 3.4 and 7, slices C2 and C3; operator,
// 18 November 2026, R1168, R1169, R1182, R1185; 19 November 2026, R1206),
// through the gateway against
// the scripted editor of GlassProviderFixture: a search changes nothing, an
// add is claimed before Glass's is asked and posted as the dialog serves it,
// a row that is not certain is refused and nothing is sent, a lost answer is
// settled by the next read, and the same key never sends twice.
public sealed partial class GlassRepairEstimateGatewayTests
{
    /// <summary>A position of the captured estimate that the Automation did not add (R1185: any position may be removed).</summary>
    private const string CapturedPosition = "C350926.27.C2027021.4646";

    /// <summary>The scripted editor's estimate with nothing on it.</summary>
    private const string NoPositions = "<script>\nvar Partids = [ '' ];\nvar CheckIds = [  ];\n</script>\n";

    /// <summary>
    /// A second head row for the left front door's code and side, another
    /// part (its price differs) on another drawing number, and its Replace
    /// time row: the list then holds two heads for one line that are not the
    /// same part.
    /// </summary>
    private static readonly string[] SecondDoorRows =
    [
        "900|pbclTIdLg|HTB3-202_809|701300250101|37|2020001||41009628759|600.00|ERAEI |||||||Front Door Shell|||||||||||KS|||||790|0|0.0.0.9",
        "901|tlTI|||||||2.90|ER|||||||||||||||||||||||792|0|0.0.0.9",
    ];

    /// <summary>The lining's three options (glasses7 #356, row 0).</summary>
    private static readonly string[] LiningOptions = ["12000", "24119", "90109"];

    /// <summary>An answer that ticks nothing.</summary>
    private static readonly string[] NothingTicked = [];

    /// <summary>
    /// An answered add's requests after the estimate's read (design 10
    /// section 2): the part list, the row's map, the stored answer, the
    /// dialog, its post and the "Mandatory parts:" confirm, the estimate.
    /// </summary>
    private static readonly string[] AnsweredAdd =
    [
        "GET /ere/aco/list.php",
        "GET /ere/aco/service.php",
        "GET /ere/aco/service.php",
        "GET /ere/aco/comp.php",
        "POST /ere/aco/compproc.php",
        "POST /ere/aco/compproc.php",
        "GET /ere/aco/selected.php",
    ];

    /// <summary>
    /// The search reads the special list's group and the part list, once
    /// each, and answers each row by the value an add takes: a special
    /// operation's code, a part's head row, with its side and whether it can
    /// be painted. A side asks only for parts, so the special list is not
    /// read. Nothing is posted, and the editor's pace and cookies are kept.
    /// </summary>
    [Fact]
    public async Task TheCatalogueSearchReadsBothListsAndAnswersRowsByWhatAnAddTakes()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var special = await SearchAsync(harness, session.Id, "appraisal", null, null);

        var charge = Assert.Single(special.Rows);
        Assert.Equal("AC0000073", charge.Row);
        Assert.Equal(GlassAutomationPositionKind.SpecialOperation, charge.Kind);
        Assert.Equal(Charge, charge.Description);
        Assert.Null(special.Code);
        Assert.Equal(session.Version + 1, special.Session.Version);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/list_1G.php"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/list.php"));

        var doors = await SearchAsync(harness, session.Id, "door shell", GlassAutomationPositionKind.Part, null);

        Assert.Equal(2, doors.Rows.Count);
        Assert.Equal("771", doors.Rows[0].Row);
        Assert.Equal("L", doors.Rows[0].Side);
        Assert.Equal("2020001", doors.Rows[0].PartCode);
        Assert.Equal("41009628759", doors.Rows[0].PartNumber);
        Assert.True(doors.Rows[0].Paintable);
        Assert.Equal("790", doors.Rows[1].Row);
        Assert.Equal("R", doors.Rows[1].Side);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/list_1G.php"));

        var right = await SearchAsync(harness, session.Id, "2020002", null, "R");

        Assert.Equal("790", Assert.Single(right.Rows).Row);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/list_1G.php"));
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(GlassRepairEstimateSessionState.Active, right.Session.State);
    }

    /// <summary>A list Glass's does not answer answers no rows and its code; the session stays open.</summary>
    [Fact]
    public async Task ASearchWhoseListIsNotAnsweredAnswersItsCode()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        harness.Mva.Enqueue("GET /ere/aco/list.php", new Reply(HttpStatusCode.InternalServerError, string.Empty));

        var found = await SearchAsync(harness, session.Id, "door", GlassAutomationPositionKind.Part, null);

        Assert.Empty(found.Rows);
        Assert.Equal(GlassFailure.EditorRequest, found.Code);
        Assert.Equal(GlassRepairEstimateSessionState.Active, found.Session.State);
    }

    /// <summary>
    /// A special operation is claimed, its dialog posted as served and the
    /// outcome recorded with Glass's own figures (R1168). The same key again
    /// answers the recorded act and sends nothing.
    /// </summary>
    [Fact]
    public async Task ASpecialOperationIsAddedAsServedAndItsKeyNeverSendsTwice()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:add-1",
            GlassAutomationPositionKind.SpecialOperation, "AC0000073"));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        Assert.Null(step.Act.Code);
        Assert.Equal("A73", step.Act.ExpectedCode);
        Assert.Contains("A350880.1.A73.A73", step.Act.PositionIds);
        Assert.Equal(new GlassEstimateBuildFigures(176.96m, null), step.Act.Served);
        // Claimed, then the code recorded before the post, then the outcome.
        Assert.Equal(session.Version + 3, step.Session.Version);
        Assert.Equal(GlassRepairEstimateSessionState.Active, step.Session.State);
        var stored = Assert.Single(harness.Store.Material(session.Id).Session.Automation!.Acts);
        Assert.Equal(GlassAutomationActOutcome.Added, stored.Outcome);
        Assert.Equal(1, harness.Mva.Count("POST /ere/aco/compproc.php"));

        var requests = harness.Mva.Requests.Count;
        var replayed = await harness.Gateway.PrepareAutomationPositionAsync(
            Add(harness, session, "mcp:add-1", GlassAutomationPositionKind.SpecialOperation, "AC0000073"),
            CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.False(replayed.Owed);
        Assert.Equal(GlassAutomationActOutcome.Added, replayed.Act.Outcome);
        Assert.Equal(step.Session.Version, replayed.Session.Version);
        Assert.Equal(requests, harness.Mva.Requests.Count);
    }

    /// <summary>
    /// A part whose row is certain is added as Part: its equipment map read,
    /// its dialog posted with Glass's own price and hours, and the parts the
    /// "Mandatory parts:" page listed answered with it.
    /// </summary>
    [Fact]
    public async Task ACertainPartIsAddedAsServed()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:add-1",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        Assert.Equal("C2020001", step.Act.ExpectedCode);
        Assert.Equal(new GlassEstimateBuildFigures(545.24m, 2.90m), step.Act.Served);
        Assert.NotEmpty(step.Act.Listed);
        Assert.Contains("C350987.35.C2020001.4467", step.Act.PositionIds);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/list.php"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/comp.php"));
        Assert.Contains("mcode=771|772", Assert.Single(Asked(harness, "GET", "comp.php")), StringComparison.Ordinal);
    }

    /// <summary>
    /// Repair and remove-and-refit on a part whose row is certain for that
    /// operation (R1172, R1182): the same row pair, the operation chosen in
    /// the dialog, and the dialog posted exactly as its own script posts it,
    /// with whatever Glass's serves.
    /// </summary>
    [Fact]
    public async Task ARepairAndARefitAreSentWithWhateverGlassServes()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var repair = await PositionAsync(harness, Add(harness, session, "mcp:repair-1",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Repair));
        var refit = await PositionAsync(harness, Add(harness, repair.Session, "mcp:refit-1",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.RemoveAndRefit));

        Assert.Equal(GlassAutomationActOutcome.Added, repair.Act.Outcome);
        Assert.Equal(new GlassEstimateBuildFigures(null, null), repair.Act.Served);
        Assert.Equal(GlassAutomationActOutcome.Added, refit.Act.Outcome);
        Assert.Equal(new GlassEstimateBuildFigures(null, 0.40m), refit.Act.Served);
        var dialogPosts = harness.Mva.Requests
            .Where(request => request.Method == "POST" && request.Path == "/ere/aco/compproc.php"
                && !request.Query.Contains("BX=", StringComparison.Ordinal))
            .Select(request => request.Body)
            .ToList();
        List<string?> judged =
        [
            GlassEditorFixture.Text("door_left_repair.post.txt"),
            GlassEditorFixture.Text("door_left_refit.post.txt"),
        ];
        Assert.Equal(judged, dialogPosts);
    }

    /// <summary>
    /// A row that is not certain is refused with its reason and nothing is
    /// posted (R1169): a part whose rows wait on equipment (no dialog asked
    /// for), a side asked for that is not the row's own (nothing asked), a
    /// row neither list holds, and a dialog that serves the other side
    /// (asked for, refused before its post).
    /// </summary>
    [Fact]
    public async Task ARowThatIsNotCertainIsRefusedAndNothingIsSent()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var equipment = await PositionAsync(harness, Add(harness, session, "mcp:lining-1",
            GlassAutomationPositionKind.Part, "0", GlassAutomationPartOperation.Part));
        Assert.Equal(GlassAutomationActOutcome.Refused, equipment.Act.Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.EquipmentCode, equipment.Act.Code);
        // Given no answer, the add carries the question its map was read for (R1206).
        Assert.NotNull(equipment.Act.Equipment);
        Assert.Equal(LiningOptions, equipment.Act.Equipment.Options.Select(option => option.Code));
        Assert.False(equipment.Act.EquipmentStored);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/comp.php"));

        var side = await PositionAsync(harness, Add(harness, equipment.Session, "mcp:side-1",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part, side: "R"));
        Assert.Equal(GlassAutomationActOutcome.Refused, side.Act.Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NoRowCode, side.Act.Code);
        // Only the lining's map was read.
        Assert.Equal(1, Asked(harness, "GET", "service.php").Count(query => query.Contains("reqcritmap3=1", StringComparison.Ordinal)));
        Assert.Equal(0, Asked(harness, "GET", "service.php").Count(query => query.Contains("globcrit=", StringComparison.Ordinal)));

        var unknown = await PositionAsync(harness, Add(harness, side.Session, "mcp:unknown-1",
            GlassAutomationPositionKind.SpecialOperation, "AC9999999"));
        Assert.Equal(GlassAutomationActOutcome.Refused, unknown.Act.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.RowUnknownCode, unknown.Act.Code);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/part.php"));

        var wrongSide = await PositionAsync(harness, Add(harness, unknown.Session, "mcp:right-1",
            GlassAutomationPositionKind.Part, "790", GlassAutomationPartOperation.Part));
        Assert.Equal(GlassAutomationActOutcome.Refused, wrongSide.Act.Outcome);
        Assert.Equal(GlassFailure.EditorRefused, wrongSide.Act.Code);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/comp.php"));

        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(4, harness.Store.Material(session.Id).Session.Automation!.Acts.Count);
    }

    /// <summary>
    /// A part whose row asks Glass's equipment question is added as Part by
    /// the Automation's answer (R1206): the list and the row's map are read,
    /// the answer is stored for the vehicle as the editor stores it
    /// (<c>encodeURIComponent</c>-encoded) just before the dialog of the row
    /// pair it leads to is asked for, and the dialog is posted as served. The
    /// act keeps the answer, the pair and the values it changed.
    /// </summary>
    [Fact]
    public async Task AnAnsweredPartIsStoredThenAddedAsServed()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        var before = harness.Mva.Requests.Count;

        var step = await PositionAsync(harness, Add(harness, session, "mcp:lining-1",
            GlassAutomationPositionKind.Part, "0", GlassAutomationPartOperation.Part, equipment: LiningOptions));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        Assert.Equal("C1010800", step.Act.ExpectedCode);
        Assert.Equal("0|5", step.Act.RowPair);
        Assert.True(step.Act.EquipmentStored);
        Assert.Equal(LiningOptions, step.Act.Inputs?.Equipment);
        Assert.Equal(LiningOptions, step.Act.EquipmentChanged.Select(change => change.Code));
        Assert.All(step.Act.EquipmentChanged, change => Assert.True(change.Ticked));
        Assert.Null(step.Act.Equipment);
        Assert.Equal(new GlassEstimateBuildFigures(379.00m, 1.80m), step.Act.Served);
        Assert.Contains("C350955.31.C1010800.1580", step.Act.PositionIds);
        var asked = harness.Mva.Requests.Skip(before).Select(request => request.Method + " " + request.Path).ToList();
        Assert.Equal("GET /ere/aco/selected.php", asked[0]);
        Assert.Equal(AnsweredAdd, asked.Skip(1));
        var stored = Assert.Single(harness.Mva.Requests, request => request.Query.Contains("globcrit=", StringComparison.Ordinal));
        Assert.Contains(
            "&globcrit=12000%7C1%7C1%7C24119%7C1%7C1%7C90109%7C1%7C1&updatecrit2g=1&", stored.Query, StringComparison.Ordinal);
        Assert.Contains("rowid=0&reqcritmap3=1", Assert.Single(
            Asked(harness, "GET", "service.php"), query => query.Contains("reqcritmap3", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("mcode=0|5", Assert.Single(Asked(harness, "GET", "comp.php")), StringComparison.Ordinal);
        var recorded = Assert.Single(harness.Store.Material(session.Id).Session.Automation!.Acts);
        Assert.Equal("0|5", recorded.RowPair);
        Assert.True(recorded.EquipmentStored);
    }

    /// <summary>
    /// A dialog Glass's serves for another variant than the answer's rows
    /// is refused before its post (R1206): the answer that ticks nothing
    /// leads to <c>0|1</c>, and the scripted editor serves the dialog of
    /// <c>0|5</c>. The answer was stored first, and the act says so.
    /// </summary>
    [Fact]
    public async Task AnotherVariantsDialogIsRefusedAndTheActSaysTheAnswerWasStored()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:lining-1",
            GlassAutomationPositionKind.Part, "0", GlassAutomationPartOperation.Part, equipment: NothingTicked));

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassFailure.EditorRefused, step.Act.Code);
        Assert.Equal("0|1", step.Act.RowPair);
        Assert.True(step.Act.EquipmentStored);
        Assert.Empty(step.Act.EquipmentChanged);
        Assert.Contains("globcrit=12000|0|1|24119|0|1|90109|0|1&updatecrit2g=1", Assert.Single(
            Asked(harness, "GET", "service.php"), query => query.Contains("globcrit=", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Contains("mcode=0|1", Assert.Single(Asked(harness, "GET", "comp.php")), StringComparison.Ordinal);
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// An answer the dialog would not take, or one given for a row that asks
    /// nothing, is refused with its code and nothing is sent: no answer is
    /// stored and no dialog asked for (R1206). The lining's refusals carry
    /// its question.
    /// </summary>
    [Theory]
    [InlineData("0", "99999", GlassAutomationPositionPolicy.EquipmentUnknownCode, 1)]
    [InlineData("0", "12000,12000", GlassAutomationPositionPolicy.EquipmentRepeatedCode, 1)]
    [InlineData("771", "12000", GlassAutomationPositionPolicy.EquipmentNotAskedCode, 0)]
    public async Task AnAnswerTheDialogWouldNotTakeIsRefusedAndNothingIsSent(string row, string equipment, string code, int maps)
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:answer-1",
            GlassAutomationPositionKind.Part, row, GlassAutomationPartOperation.Part, equipment: equipment.Split(',')));

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(code, step.Act.Code);
        Assert.Equal(maps == 1, step.Act.Equipment is not null);
        Assert.False(step.Act.EquipmentStored);
        Assert.Equal(maps, Asked(harness, "GET", "service.php").Count(query => query.Contains("reqcritmap3=1", StringComparison.Ordinal)));
        Assert.Equal(0, Asked(harness, "GET", "service.php").Count(query => query.Contains("globcrit=", StringComparison.Ordinal)));
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/comp.php"));
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// The search answers each part whose block asks with its question,
    /// from the list alone; with a row it reads that row's map too and
    /// answers every answer that leads to a part (R1206). A row that is not
    /// a head of the list answers no rows and its code. Nothing is sent.
    /// </summary>
    [Fact]
    public async Task TheSearchCarriesTheQuestionAndARowAsksForItsAnswers()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var linings = await SearchAsync(harness, session.Id, "bumper lining", GlassAutomationPositionKind.Part, null);
        var doors = await SearchAsync(harness, session.Id, "door shell", GlassAutomationPositionKind.Part, null);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/service.php"));
        var lining = await harness.Gateway.SearchAutomationCatalogueAsync(
            new(harness.Automation, session.Id, string.Empty, null, null) { Row = "0" }, CancellationToken.None);
        var unknown = await harness.Gateway.SearchAutomationCatalogueAsync(
            new(harness.Automation, session.Id, string.Empty, null, null) { Row = "99999" }, CancellationToken.None);

        Assert.Equal(2, linings.Rows.Count);
        Assert.All(linings.Rows, row => Assert.Equal(LiningOptions, row.Equipment?.Options.Select(option => option.Code)));
        Assert.All(linings.Rows, row => Assert.Null(row.Equipment?.Answers));
        Assert.All(doors.Rows, row => Assert.Null(row.Equipment));
        Assert.NotNull(lining);
        var answered = Assert.Single(lining.Rows);
        Assert.Equal("0", answered.Row);
        Assert.Equal(8, answered.Equipment?.Answers?.Count);
        Assert.Contains(answered.Equipment!.Answers!, answer => answer.Rows == "0|5" && answer.Ticked.SequenceEqual(LiningOptions));
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/service.php"));
        Assert.NotNull(unknown);
        Assert.Empty(unknown.Rows);
        Assert.Equal(GlassAutomationPositionPolicy.RowUnknownCode, unknown.Code);
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// Two head rows for one line that are not the same part are ambiguous:
    /// refused, both maps read, and no dialog asked for.
    /// </summary>
    [Fact]
    public async Task TwoHeadsThatAreNotTheSamePartAreRefusedAsAmbiguous()
    {
        var harness = Harness.Create();
        harness.Mva.Respond("GET /ere/aco/list.php", _ => Editor(BuildPartList().Replace(
            "\n</script>",
            "\n" + string.Join('\n', SecondDoorRows.Select(row => "partlist.addPart(\"" + row + "\");")) + "\n</script>",
            StringComparison.Ordinal)));
        harness.Mva.Respond("GET /ere/aco/service.php", request => request.Query.Contains("rowid=", StringComparison.Ordinal)
            ? Editor("<script>\nvar aCritMap = [ [\"I\",\"2020001\"],[],[],[] ];\n</script>\n")
            : EditorPage("save.answer.html", null));
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:door-1",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part));

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.AmbiguousRowCode, step.Act.Code);
        Assert.Equal(2, Asked(harness, "GET", "service.php").Count(query => query.Contains("reqcritmap3=1", StringComparison.Ordinal)));
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/comp.php"));
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// Paint on its own and Part and Paint are not sent (R1182): the act is
    /// recorded refused with its reason when it is claimed, and Glass's is
    /// asked nothing.
    /// </summary>
    [Theory]
    [InlineData(GlassAutomationPartOperation.Paint)]
    [InlineData(GlassAutomationPartOperation.PartAndPaint)]
    public async Task PaintIsRefusedWhenItIsClaimed(GlassAutomationPartOperation operation)
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        var requests = harness.Mva.Requests.Count;

        var step = await harness.Gateway.PrepareAutomationPositionAsync(
            Add(harness, session, "mcp:paint-1", GlassAutomationPositionKind.Part, "771", operation),
            CancellationToken.None);

        Assert.NotNull(step);
        Assert.False(step.Owed);
        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NotBuiltCode, step.Act.Code);
        Assert.Equal(requests, harness.Mva.Requests.Count);
    }

    /// <summary>A dialog the editor refuses before anything is posted refuses the act with the editor's code.</summary>
    [Fact]
    public async Task ARefusedDialogRefusesTheAct()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        // The dialog served is another operation's: its part code is not the one asked for.
        harness.Mva.Enqueue("GET /ere/aco/part.php", EditorPage("special_hours.dialog.html", null));

        var step = await PositionAsync(harness, Add(harness, session, "mcp:add-1",
            GlassAutomationPositionKind.SpecialOperation, "AC0000073"));

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassFailure.EditorRefused, step.Act.Code);
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// A post whose answer is lost is not_confirmed; the next add's read of
    /// the estimate settles it: its code is on a position that was not there
    /// before its post, so it was added.
    /// </summary>
    [Fact]
    public async Task ALostAnswerIsSettledAsAddedByTheNextAdd()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        harness.Mva.Enqueue("POST /ere/aco/compproc.php", new Reply(HttpStatusCode.InternalServerError, string.Empty));

        var lost = await PositionAsync(harness, Add(harness, session, "mcp:add-1",
            GlassAutomationPositionKind.SpecialOperation, "AC0000073"));

        Assert.Equal(GlassAutomationActOutcome.NotConfirmed, lost.Act.Outcome);
        Assert.Equal(GlassFailure.EditorAnswer, lost.Act.Code);
        Assert.Equal("A73", lost.Act.ExpectedCode);

        var next = await PositionAsync(harness, Add(harness, lost.Session, "mcp:add-2",
            GlassAutomationPositionKind.SpecialOperation, "AC0000075"));

        Assert.Equal(GlassAutomationActOutcome.Added, next.Act.Outcome);
        var settled = GlassAutomationPositionPolicy.Find(next.Session.Automation, "mcp:add-1");
        Assert.NotNull(settled);
        Assert.Equal(GlassAutomationActOutcome.Added, settled.Outcome);
        Assert.Null(settled.Code);
        Assert.Equal("A350880.1.A73.A73", Assert.Single(settled.PositionIds));
    }

    /// <summary>
    /// A lost answer the estimate does not bear out is settled as refused,
    /// with its code, by the next read, here the one pegasus_glass_session_get
    /// makes with readEstimate.
    /// </summary>
    [Fact]
    public async Task ALostAnswerTheEstimateDoesNotBearOutIsSettledAsRefused()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => Editor(NoPositions));
        harness.Mva.Enqueue("POST /ere/aco/compproc.php", new Reply(HttpStatusCode.InternalServerError, string.Empty));
        var lost = await PositionAsync(harness, Add(harness, session, "mcp:add-1",
            GlassAutomationPositionKind.SpecialOperation, "AC0000073"));
        Assert.Equal(GlassAutomationActOutcome.NotConfirmed, lost.Act.Outcome);

        var read = await harness.Gateway.ReadAutomationEstimateAsync(harness.Automation, session.Id, CancellationToken.None);

        Assert.NotNull(read);
        var settled = GlassAutomationPositionPolicy.Find(read.Session.Automation, "mcp:add-1");
        Assert.NotNull(settled);
        Assert.Equal(GlassAutomationActOutcome.Refused, settled.Outcome);
        Assert.Equal(GlassFailure.EditorAnswer, settled.Code);
    }

    /// <summary>
    /// remove asks for the position's dialog first, then removes it, and the
    /// estimate read afterwards no longer lists it. Any position of the
    /// estimate may be removed, not only one the Automation added (R1185). A
    /// position the estimate does not hold is refused and nothing is sent.
    /// </summary>
    [Fact]
    public async Task APositionIsRemovedAfterItsDialogAndAnUnheldOneIsRefused()
    {
        var harness = Harness.Create();
        var captured = GlassEditorFixture.Text("selected.html");
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => Editor(
            harness.Mva.Count("GET /ere/aco/compproc.php") == 0
                ? captured
                : captured.Replace("'" + CapturedPosition + "', ", string.Empty, StringComparison.Ordinal)));
        harness.Mva.Enqueue("GET /ere/aco/part.php", EditorPage("mirror_left.dialog.html", null));
        harness.Mva.Enqueue("GET /ere/aco/compproc.php", EditorPage("removed.answer.html", null));
        var session = await AutomationStartAsync(harness);

        var removed = await PositionAsync(harness, Remove(harness, session, "mcp:remove-1", CapturedPosition));

        Assert.Equal(GlassAutomationActOutcome.Removed, removed.Act.Outcome);
        Assert.Null(removed.Act.Code);
        Assert.True(harness.Mva.IndexOf("/ere/aco/part.php") < harness.Mva.IndexOf("/ere/aco/compproc.php"));
        var requests = harness.Mva.Requests.Count;

        var unheld = await PositionAsync(harness, Remove(harness, removed.Session, "mcp:remove-2", CapturedPosition));

        Assert.Equal(GlassAutomationActOutcome.Refused, unheld.Act.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.RowUnknownCode, unheld.Act.Code);
        // Only the estimate was read.
        Assert.Equal(requests + 1, harness.Mva.Requests.Count);
    }

    /// <summary>
    /// A key an earlier, closed session of the Case recorded is answered
    /// from that session in a new one, at any version: the recorded outcome
    /// comes back and nothing is sent to Glass's again.
    /// </summary>
    [Fact]
    public async Task AKeyAClosedSessionRecordedIsAnsweredInTheNextAndNeverSendsTwice()
    {
        var harness = Harness.Create();
        var first = await AutomationStartAsync(harness);
        var added = await PositionAsync(harness, Add(harness, first, "mcp:add-1",
            GlassAutomationPositionKind.SpecialOperation, "AC0000073"));
        Assert.Equal(GlassAutomationActOutcome.Added, added.Act.Outcome);
        await harness.Gateway.CloseAsync(
            new(harness.Automation, first.Id, added.Session.Version, true, "Started again."), CancellationToken.None);
        var second = await AutomationStartAsync(harness, "mcp:start-2");
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(GlassRepairEstimateSessionState.Active, second.State);
        var requests = harness.Mva.Requests.Count;

        var replayed = await harness.Gateway.PrepareAutomationPositionAsync(
            Add(harness, second with { Version = second.Version + 5 }, "mcp:add-1",
                GlassAutomationPositionKind.SpecialOperation, "AC0000073"),
            CancellationToken.None);

        Assert.NotNull(replayed);
        Assert.False(replayed.Owed);
        Assert.Equal(first.Id, replayed.Session.Id);
        Assert.Equal(GlassAutomationActOutcome.Added, replayed.Act.Outcome);
        Assert.Equal(added.Act.PositionIds, replayed.Act.PositionIds);
        Assert.Equal(requests, harness.Mva.Requests.Count);
        Assert.Null(harness.Store.Material(second.Id).Session.Automation);
        Assert.Equal(1, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>A stale session version is refused before anything is claimed or asked.</summary>
    [Fact]
    public async Task AStaleSessionVersionIsRefused()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        var requests = harness.Mva.Requests.Count;

        var refused = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(() =>
            harness.Gateway.PrepareAutomationPositionAsync(
                Add(harness, session with { Version = session.Version + 7 }, "mcp:add-1",
                    GlassAutomationPositionKind.SpecialOperation, "AC0000073"),
                CancellationToken.None));

        Assert.Equal(GlassRepairEstimateSessionConflict.Version, refused.Conflict);
        Assert.Equal(requests, harness.Mva.Requests.Count);
        Assert.Null(harness.Store.Material(session.Id).Session.Automation);
    }

    /// <summary>
    /// Two acts claimed on the version one read are one act: the second is
    /// refused. A claim whose work never ran is settled by the next act's
    /// read: nothing was posted for it, so it is refused as interrupted.
    /// </summary>
    [Fact]
    public async Task AConcurrentActIsRefusedAndAnInterruptedClaimIsSettled()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        var first = await harness.Gateway.PrepareAutomationPositionAsync(
            Add(harness, session, "mcp:add-1", GlassAutomationPositionKind.SpecialOperation, "AC0000073"),
            CancellationToken.None);
        Assert.NotNull(first);
        Assert.True(first.Owed);

        var concurrent = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(() =>
            harness.Gateway.PrepareAutomationPositionAsync(
                Add(harness, session, "mcp:add-2", GlassAutomationPositionKind.SpecialOperation, "AC0000075"),
                CancellationToken.None));
        Assert.Equal(GlassRepairEstimateSessionConflict.Version, concurrent.Conflict);

        var next = await PositionAsync(harness, Add(harness, first.Session, "mcp:add-3",
            GlassAutomationPositionKind.SpecialOperation, "AC0000075"));

        Assert.Equal(GlassAutomationActOutcome.Added, next.Act.Outcome);
        var interrupted = GlassAutomationPositionPolicy.Find(next.Session.Automation, "mcp:add-1");
        Assert.NotNull(interrupted);
        Assert.Equal(GlassAutomationActOutcome.Refused, interrupted.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.InterruptedCode, interrupted.Code);
        Assert.Null(GlassAutomationPositionPolicy.Find(next.Session.Automation, "mcp:add-2"));
        Assert.Equal(1, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>A member of staff never searches or acts in the Automation's session.</summary>
    [Fact]
    public async Task OnlyTheAutomationSearchesAndActsInItsSession()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        var requests = harness.Mva.Requests.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Gateway.SearchAutomationCatalogueAsync(
            new(harness.Engineer, session.Id, "door", null, null), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Gateway.PrepareAutomationPositionAsync(
            Add(harness, session, "mcp:add-1", GlassAutomationPositionKind.SpecialOperation, "AC0000073")
                with { Actor = harness.Engineer },
            CancellationToken.None));
        Assert.Equal(requests, harness.Mva.Requests.Count);
    }

    private static async Task<GlassAutomationCatalogue> SearchAsync(
        Harness harness, Guid sessionId, string term, GlassAutomationPositionKind? kind, string? side)
    {
        var found = await harness.Gateway.SearchAutomationCatalogueAsync(
            new(harness.Automation, sessionId, term, kind, side), CancellationToken.None);
        Assert.NotNull(found);
        return found;
    }

    private static GlassAutomationPositionRequest Add(
        Harness harness,
        GlassRepairEstimateSession session,
        string operationKey,
        GlassAutomationPositionKind kind,
        string row,
        GlassAutomationPartOperation? operation = null,
        string? side = null,
        IReadOnlyList<string>? equipment = null) =>
        new(harness.Automation, session.Id, session.Version, operationKey, GlassAutomationActAction.Add)
        {
            Kind = kind,
            Row = row,
            Operation = operation,
            Side = side,
            Inputs = equipment is null ? null : new GlassAutomationInputs(equipment),
        };

    private static GlassAutomationPositionRequest Remove(
        Harness harness, GlassRepairEstimateSession session, string operationKey, string positionId) =>
        new(harness.Automation, session.Id, session.Version, operationKey, GlassAutomationActAction.Remove)
        {
            PositionId = positionId,
        };

    /// <summary>A position act's prepare half, then its provider work inline, as the tool's queue runs it.</summary>
    private static async Task<GlassAutomationPositionStep> PositionAsync(
        Harness harness, GlassAutomationPositionRequest request)
    {
        var step = await harness.Gateway.PrepareAutomationPositionAsync(request, CancellationToken.None);
        Assert.NotNull(step);
        return step.Owed
            ? await harness.Gateway.ContinueAutomationPositionAsync(
                new(harness.Automation, request.SessionId, request.OperationKey), CancellationToken.None)
            : step;
    }
}
