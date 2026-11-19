using System.Net;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

// The Automation's catalogue search and position acts in its own Glass's
// session (design 07 sections 3.3, 3.4 and 7, slices C2 and C3; operator,
// 18 November 2026, R1168, R1169, R1182, R1185; 19 November 2026, R1206:
// preview, paint buttons, any served operation, Both and optional parts),
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
    /// The BMW's roof panel asks no question and the panoramic roof it is
    /// hidden by is not on the car, so the editor shows it and a click opens
    /// its dialog (R1206): the Automation's add asks for the pair that click
    /// asks for, after reading the maps of the part's two heads. No roof
    /// dialog is captured, so the scripted editor serves the door's, whose
    /// part code refuses it before anything is posted. The preview asks for
    /// the same pair.
    /// </summary>
    [Fact]
    public async Task AHeadItsEquipmentShowsIsAddedWithoutAQuestion()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        harness.Mva.Enqueue("GET /ere/aco/comp.php", EditorPage("door_left_part.dialog.html", null));

        var step = await PositionAsync(harness, Add(harness, session, "mcp:roof-1",
            GlassAutomationPositionKind.Part, "525", GlassAutomationPartOperation.Part));
        harness.Mva.Enqueue("GET /ere/aco/comp.php", EditorPage("door_left_part.dialog.html", null));
        var preview = await harness.Gateway.PreviewAutomationPositionAsync(
            Preview(harness, step.Session, "525", GlassAutomationPartOperation.Part, null), CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassFailure.EditorRefused, step.Act.Code);
        Assert.Null(step.Act.Equipment);
        Assert.NotNull(preview);
        Assert.NotEqual(GlassEstimateBuildPolicy.EquipmentCode, preview.Code);
        var asked = Asked(harness, "GET", "comp.php");
        Assert.Equal(2, asked.Count);
        Assert.All(asked, query => Assert.Contains("mcode=525|526", query, StringComparison.Ordinal));
        Assert.Contains(Asked(harness, "GET", "service.php"), query => query.Contains("rowid=2697&reqcritmap3=1", StringComparison.Ordinal));
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// On a car with the panoramic roof the roof panel is hidden, as the
    /// editor hides it: the add and the preview are refused with
    /// glass.build.equipment_hidden by the list alone, with no question and
    /// nothing more asked (R1206).
    /// </summary>
    [Fact]
    public async Task AHeadItsEquipmentHidesIsRefusedWithItsOwnCode()
    {
        var harness = Harness.Create();
        harness.Mva.Respond("GET /ere/aco/list.php", _ => Editor(BuildPartList().Replace(
            NoPanoramicRoof, PanoramicRoof, StringComparison.Ordinal)));
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:roof-1",
            GlassAutomationPositionKind.Part, "525", GlassAutomationPartOperation.Part));
        var preview = await harness.Gateway.PreviewAutomationPositionAsync(
            Preview(harness, step.Session, "525", GlassAutomationPartOperation.Part, null), CancellationToken.None);

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.EquipmentHiddenCode, step.Act.Code);
        Assert.Null(step.Act.Equipment);
        Assert.NotNull(preview);
        Assert.Equal(GlassAutomationPositionPolicy.EquipmentHiddenCode, preview.Code);
        Assert.Null(preview.Equipment);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/service.php"));
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/comp.php"));
    }

    /// <summary>
    /// Repair on a part whose row asks Glass's equipment question is refused
    /// with a code of its own, never the bare question code (R1172, R1206).
    /// </summary>
    [Fact]
    public async Task ARepairOnAPartThatAsksIsRefusedWithItsOwnCode()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:lining-repair",
            GlassAutomationPositionKind.Part, "0", GlassAutomationPartOperation.Repair));

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.EquipmentPartOnlyCode, step.Act.Code);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/service.php"));
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
    /// Paint on its own and Part and Paint are sent (R1206): the button
    /// pressed on the dialog of a part that can be painted, the paint section
    /// as Glass's serves it, and the act recorded with Glass's own figures.
    /// </summary>
    [Theory]
    [InlineData(GlassAutomationPartOperation.Paint, "&BLL=Paint&")]
    [InlineData(GlassAutomationPartOperation.PartAndPaint, "&BSL=Part+and+Paint&")]
    public async Task PaintAndPartAndPaintArePostedWithTheirButtons(GlassAutomationPartOperation operation, string button)
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:paint-1",
            GlassAutomationPositionKind.Part, "771", operation));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        Assert.Null(step.Act.Code);
        Assert.Equal(new GlassEstimateBuildFigures(545.24m, 2.90m), step.Act.Served);
        var posted = Assert.Single(DialogPosts(harness));
        Assert.Contains(button, posted, StringComparison.Ordinal);
        Assert.Contains("&Repart=ER&", posted, StringComparison.Ordinal);
    }

    /// <summary>
    /// A paint button on a part that cannot be painted (part.php, the
    /// reinforcement) is refused before its dialog is asked for.
    /// </summary>
    [Fact]
    public async Task PaintOnAPartThatCannotBePaintedIsRefusedAndNothingIsSent()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:paint-2",
            GlassAutomationPositionKind.Part, "193", GlassAutomationPartOperation.Paint));

        Assert.Equal(GlassAutomationActOutcome.Refused, step.Act.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.NotPaintableCode, step.Act.Code);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/part.php"));
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// An operation the dialog offers and the return reads back is chosen by
    /// Glass's own code (R1206) and posted exactly as the dialog's own script
    /// posted it offline (manifest operations): Uninstall and install with
    /// its own hours; Sealing, served with no figures, only with the OK
    /// given; Repair with Part and Paint, answered OK by itself (R1188).
    /// Every input is kept on the act.
    /// </summary>
    [Fact]
    public async Task AnOperationTheDialogOffersIsPostedAsItsScriptPostsIt()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var adjust = await PositionAsync(harness, Add(harness, session, "mcp:op-ae",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(Operation: "AE")));
        var unanswered = await PositionAsync(harness, Add(harness, adjust.Session, "mcp:op-ad-1",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(Operation: "AD")));
        var answered = await PositionAsync(harness, Add(harness, unanswered.Session, "mcp:op-ad-2",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(Operation: "AD", ConfirmNoFigures: true)));
        var repaired = await PositionAsync(harness, Add(harness, answered.Session, "mcp:op-i",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(Operation: "I", Button: GlassAutomationButton.PartAndPaint)));

        Assert.Equal(GlassAutomationActOutcome.Added, adjust.Act.Outcome);
        Assert.Equal(new GlassEstimateBuildFigures(null, 0.40m), adjust.Act.Served);
        Assert.Equal("AE", adjust.Act.Inputs?.Operation);
        Assert.Equal(GlassAutomationActOutcome.Refused, unanswered.Act.Outcome);
        Assert.Equal(GlassFailure.EditorRefused, unanswered.Act.Code);
        Assert.Equal(GlassAutomationActOutcome.Added, answered.Act.Outcome);
        Assert.True(answered.Act.Inputs?.ConfirmNoFigures);
        Assert.Equal(GlassAutomationActOutcome.Added, repaired.Act.Outcome);
        Assert.Equal(GlassAutomationButton.PartAndPaint, repaired.Act.Inputs?.Button);
        List<string?> judged =
        [
            GlassEditorFixture.Text("door_left_refit.post.txt"),
            GlassEditorFixture.Text("door_left_ad.post.txt"),
            GlassEditorFixture.Text("door_left_repair_part_and_paint.post.txt"),
        ];
        Assert.Equal(judged, DialogPosts(harness));
    }

    /// <summary>
    /// Every operation the dialog offers goes through (R1206): the return
    /// reads every repair kind Glass's exports (issue 2155). Dismantle and
    /// assemble, with the question before no figures answered, and
    /// Adjustments are each posted as the door's own script posted them.
    /// </summary>
    [Theory]
    [InlineData("Z", "door_left_z.post.txt")]
    [InlineData("E", "door_left_e.post.txt")]
    public async Task EveryOperationTheDialogOffersIsPostedAsItsScriptPostsIt(string operation, string post)
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:op-" + operation,
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(Operation: operation, ConfirmNoFigures: true)));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        Assert.Equal(GlassEditorFixture.Text(post), Assert.Single(DialogPosts(harness)));
    }

    /// <summary>
    /// The optional parts named are ticked on the "Mandatory parts:" page, as
    /// the browser ticked them (bumper and door captures), and the act lists
    /// each part as it was confirmed.
    /// </summary>
    [Fact]
    public async Task TheOptionalPartsNamedAreTickedOnTheMandatoryPartsPage()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:ticks-1",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(OptionalParts: TrimSupportKit)));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        var confirm = harness.Mva.Requests.Single(request => request.Method == "POST"
            && request.Query.Contains("BX=1", StringComparison.Ordinal));
        Assert.Equal(GlassEditorFixture.Text("door_left_part.confirm.txt"), confirm.Body);
        var kit = Assert.Single(step.Act.Listed, listed => listed.Code == "8099801.ER.");
        Assert.True(kit.Ticked);
        Assert.True(kit.Optional);
        Assert.False(Assert.Single(step.Act.Listed, listed => listed.Code == "2029301.ER.").Ticked);
    }

    /// <summary>
    /// "Both" (R1206): the left reinforcement's dialog posted with Both
    /// ticked, as the browser posted it; its answer sends the list to the
    /// right one, whose own dialog is then asked for and posted as served,
    /// as the browser posted it; each side's code is recorded before its
    /// post, and the act holds both sides' positions.
    /// </summary>
    [Fact]
    public async Task BothSidesPostsTheOtherSidesOwnDialogAfterTheFirst()
    {
        var harness = Harness.Create();
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => Editor(Reinforced(harness.Mva.Count("POST /ere/aco/compproc.php"))));
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:both-1",
            GlassAutomationPositionKind.Part, "193", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(BothSides: true)));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        Assert.Null(step.Act.Code);
        Assert.Equal("C1012401", step.Act.ExpectedCode);
        Assert.Equal("C1012402", step.Act.OtherSideCode);
        List<string> bothSides = [LeftReinforcement, RightReinforcement];
        Assert.Equal(bothSides, step.Act.PositionIds);
        List<string?> judged =
        [
            GlassEditorFixture.Text("reinforcement_left_both.post.txt"),
            GlassEditorFixture.Text("reinforcement_right.post.txt"),
        ];
        Assert.Equal(judged, DialogPosts(harness));
        List<string> dialogs = [ReinforcementLeft, ReinforcementRight];
        Assert.Equal(
            dialogs,
            Asked(harness, "GET", "part.php").Select(query => query.Split('&').Single(part => part.StartsWith("mcode=", StringComparison.Ordinal))[6..]));
    }

    /// <summary>
    /// Typed figures (R1206): hours above Glass's own with no reason are
    /// refused with their code before anything is posted, and Pegasus writes
    /// none; with the Automation's own reason they are posted with it. A
    /// typed price and overlap hours are posted as the canonical text. Each
    /// act keeps the inputs beside the figures Glass's served and those sent.
    /// </summary>
    [Fact]
    public async Task TypedFiguresArePostedWithTheAutomationsOwnReason()
    {
        var harness = Harness.Create();
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => Editor(Reinforced(harness.Mva.Count("POST /ere/aco/compproc.php"))));
        var session = await AutomationStartAsync(harness);

        var unreasoned = await PositionAsync(harness, Add(harness, session, "mcp:typed-1",
            GlassAutomationPositionKind.SpecialOperation, "AC0000075", inputs: new GlassAutomationInputs(Hours: 0.8m)));

        Assert.Equal(GlassAutomationActOutcome.Refused, unreasoned.Act.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.ReasonRequiredCode, unreasoned.Act.Code);
        Assert.Equal(0.8m, unreasoned.Act.Inputs?.Hours);
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));

        var reasoned = await PositionAsync(harness, Add(harness, unreasoned.Session, "mcp:typed-2",
            GlassAutomationPositionKind.SpecialOperation, "AC0000075",
            inputs: new GlassAutomationInputs(Hours: 0.8m, Reason: "Seized bolts")));

        Assert.Equal(GlassAutomationActOutcome.Added, reasoned.Act.Outcome);
        var post = Assert.Single(DialogPosts(harness));
        Assert.Contains("&Time=0.80&", post, StringComparison.Ordinal);
        Assert.Contains("&Reason=Seized+bolts&", post, StringComparison.Ordinal);
        Assert.Equal(new GlassAutomationFigures(0m, 0.50m, 0.00m), reasoned.Act.DialogServed);
        Assert.Equal(new GlassAutomationFigures(0m, 0.80m, 0.00m), reasoned.Act.Sent);
        Assert.Equal("Seized bolts", reasoned.Act.Inputs?.Reason);

        var priced = await PositionAsync(harness, Add(harness, reasoned.Session, "mcp:typed-3",
            GlassAutomationPositionKind.SpecialOperation, "AC0000073", inputs: new GlassAutomationInputs(Price: 150m)));

        Assert.Equal(GlassAutomationActOutcome.Added, priced.Act.Outcome);
        Assert.Contains("&Price=150.00&", DialogPosts(harness)[^1], StringComparison.Ordinal);
        Assert.Equal(150.00m, priced.Act.Sent?.Price);

        var overlapped = await PositionAsync(harness, Add(harness, priced.Session, "mcp:typed-4",
            GlassAutomationPositionKind.Part, "193", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(OverlapHours: 0.4m)));

        Assert.Equal(GlassAutomationActOutcome.Added, overlapped.Act.Outcome);
        Assert.Contains("&Ctime=0.40&", DialogPosts(harness)[^1], StringComparison.Ordinal);
        Assert.Equal(new GlassAutomationFigures(38.68m, 2.20m, 0.40m), overlapped.Act.Sent);
        Assert.Equal(3, DialogPosts(harness).Count);
    }

    /// <summary>
    /// Typed figures on the dialog of a part that can be painted (R1206) are
    /// posted as typed: the left front door's price and hours.
    /// </summary>
    [Fact]
    public async Task TypedFiguresOnAPaintablePartArePosted()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var step = await PositionAsync(harness, Add(harness, session, "mcp:typed-5",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(Hours: 2m, Price: 500m)));

        Assert.Equal(GlassAutomationActOutcome.Added, step.Act.Outcome);
        var post = Assert.Single(DialogPosts(harness));
        Assert.Contains("&Price=500.00&", post, StringComparison.Ordinal);
        Assert.Contains("&Time=2.00&", post, StringComparison.Ordinal);
        Assert.Equal(2.90m, step.Act.DialogServed?.Hours);
        Assert.Equal(2.00m, step.Act.Sent?.Hours);
    }

    /// <summary>
    /// The dialog's own fields (R1206): a special operation's description,
    /// labour type, part number and labour description, posted as typed;
    /// the left reinforcement's side ticked to the right, its figures and
    /// part number then read for that side, as the dialog's own script does.
    /// </summary>
    [Fact]
    public async Task TheDialogsOwnFieldsArePostedAsAPersonGivesThem()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var special = await PositionAsync(harness, Add(harness, session, "mcp:fields-1",
            GlassAutomationPositionKind.SpecialOperation, "AC0000073",
            inputs: new GlassAutomationInputs(
                Description: "Storage charge", Trade: "M", PartNumber: "PN-1", LabourDescription: "Collected from site")));
        var right = await PositionAsync(harness, Add(harness, special.Session, "mcp:fields-2",
            GlassAutomationPositionKind.Part, "193", GlassAutomationPartOperation.Part,
            inputs: new GlassAutomationInputs(Place: "R")));

        Assert.Equal(GlassAutomationActOutcome.Added, special.Act.Outcome);
        var posts = DialogPosts(harness);
        Assert.Contains("Descr=Storage+charge&", posts[0], StringComparison.Ordinal);
        Assert.Contains("&Craft=M&", posts[0], StringComparison.Ordinal);
        Assert.Contains("&EtnrTxt=PN-1&", posts[0], StringComparison.Ordinal);
        Assert.Contains("&Wdesc=Collected+from+site&", posts[0], StringComparison.Ordinal);
        Assert.Equal(GlassAutomationActOutcome.Added, right.Act.Outcome);
        Assert.Contains("&Place_R=", posts[1], StringComparison.Ordinal);
        Assert.Contains("&Place=R&", posts[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Place_L=", posts[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// A preview (R1206) reads the part list, the row's map and its dialog,
    /// posts nothing and records no act. It answers the dialog as a form,
    /// with Glass's own labels and texts, and for the inputs given what an
    /// add would send and the question it would meet.
    /// </summary>
    [Fact]
    public async Task APreviewAnswersTheDialogAsAFormAndPostsNothing()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        var requests = harness.Mva.Requests.Count;

        var preview = await harness.Gateway.PreviewAutomationPositionAsync(
            Preview(harness, session, "771", GlassAutomationPartOperation.Part, new GlassAutomationInputs(Operation: "AD")),
            CancellationToken.None);

        Assert.NotNull(preview);
        Assert.Null(preview.Code);
        var dialog = preview.Dialog;
        Assert.NotNull(dialog);
        Assert.Equal("part_paintable", dialog.Kind);
        Assert.Equal("Front Door Shell", dialog.Description);
        Assert.Equal("L", dialog.Side);
        var replace = Assert.Single(dialog.Operations, item => item.Code == "ER");
        Assert.Equal("RP (Replace)", replace.Text);
        Assert.Equal(545.24m, replace.Price);
        Assert.Equal(2.90m, replace.Hours);
        Assert.True(replace.Served);
        Assert.True(Assert.Single(dialog.Operations, item => item.Code == "AD").Changeable);
        Assert.True(Assert.Single(dialog.Operations, item => item.Code == "Z").Changeable);
        Assert.False(Assert.Single(dialog.Operations, item => item.Code == "AN").Changeable);
        var repairType = Assert.Single(dialog.Fields, field => field.Name == "Repart");
        Assert.Equal("Repair type", repairType.Label);
        Assert.True(repairType.Changeable);
        Assert.False(Assert.Single(dialog.Fields, field => field.Name == "Price").Changeable);
        Assert.NotNull(dialog.Paint);
        Assert.Equal("Front Door, Complete K", dialog.Paint.Area?.Text);
        Assert.Contains(dialog.Buttons, button => button is { Value: "BSL", Text: "Part and Paint" });
        var sends = dialog.Sends;
        Assert.NotNull(sends);
        Assert.Equal("AD", sends.Operation);
        Assert.Equal(GlassFailure.EditorRefused, sends.Code);
        var question = Assert.Single(sends.Questions, item => item.Kind == "no_figures");
        Assert.False(question.Answered);
        Assert.StartsWith("You have not entered a price or a Labour time.", question.Text, StringComparison.Ordinal);
        Assert.Contains(sends.Questions, item => item.Kind == "mandatory_parts");
        Assert.Equal(requests + 3, harness.Mva.Requests.Count);
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(session.Version + 1, preview.Session.Version);
        Assert.Null(harness.Store.Material(session.Id).Session.Automation);

        var unread = await harness.Gateway.PreviewAutomationPositionAsync(
            Preview(harness, preview.Session, "771", GlassAutomationPartOperation.Part, new GlassAutomationInputs(Operation: "AN")),
            CancellationToken.None);
        Assert.Equal(GlassFailure.EditorRefused, unread?.Dialog?.Sends?.Code);
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>
    /// A preview of a special operation reads its list and its dialog; one
    /// of a position on the estimate reads the estimate, whose groups say
    /// which dialog the editor opens for it (R1206), and that dialog; a
    /// part whose row asks its equipment question with no answer answers the
    /// code and no dialog. Nothing is posted.
    /// </summary>
    [Fact]
    public async Task APreviewOfASpecialOperationOrAPositionAsksOnlyForItsDialog()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => Editor(CapturedWithGroups));
        var requests = harness.Mva.Requests.Count;

        var special = await harness.Gateway.PreviewAutomationPositionAsync(
            new GlassAutomationPositionRequest(harness.Automation, session.Id, 0, "mcp:preview-2", GlassAutomationActAction.Preview)
            {
                Kind = GlassAutomationPositionKind.SpecialOperation,
                Row = "AC0000073",
            },
            CancellationToken.None);
        Assert.NotNull(special?.Dialog);
        Assert.Equal("special_operation", special.Dialog.Kind);
        Assert.All(special.Dialog.Operations, item => Assert.False(item.Changeable));
        Assert.Equal(requests + 2, harness.Mva.Requests.Count);

        harness.Mva.Enqueue("GET /ere/aco/part.php", EditorPage("mirror_left.dialog.html", null));
        var position = await harness.Gateway.PreviewAutomationPositionAsync(
            new GlassAutomationPositionRequest(harness.Automation, session.Id, 0, "mcp:preview-3", GlassAutomationActAction.Preview)
            {
                PositionId = CapturedPosition,
            },
            CancellationToken.None);
        Assert.NotNull(position?.Dialog);
        Assert.Equal("part", position.Dialog.Kind);
        Assert.Null(position.Dialog.Sends);
        Assert.Contains("partid=" + CapturedPosition, Asked(harness, "GET", "part.php")[^1], StringComparison.Ordinal);
        Assert.Equal(requests + 4, harness.Mva.Requests.Count);

        var unanswered = await harness.Gateway.PreviewAutomationPositionAsync(
            Preview(harness, session, "0", GlassAutomationPartOperation.Part, null), CancellationToken.None);
        Assert.NotNull(unanswered);
        Assert.Null(unanswered.Dialog);
        Assert.Equal(GlassEstimateBuildPolicy.EquipmentCode, unanswered.Code);
        // The bare code always comes with the question (R1206).
        Assert.NotNull(unanswered.Equipment);
        Assert.Equal(LiningOptions, unanswered.Equipment.Options.Select(option => option.Code));
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/comp.php"));
    }

    /// <summary>The captured position on an estimate whose groups put it in a group of parts with no paint (made up).</summary>
    private static readonly string CapturedWithGroups =
        "<script>\nvar Partids = [ '', '" + CapturedPosition + "' ];\nvar CheckIds = [ 'C2027021' ];\nvar Groups = [ "
        + string.Join(", ", Enumerable.Repeat("'C'", 28)) + " ];\n</script>\n";

    /// <summary>The windscreen on the estimate of the operator's walk of 19 November 2026 (#776).</summary>
    private const string Windscreen = "C421260.10.C1100400.2205";

    /// <summary>The bonnet with its paint on the same estimate.</summary>
    private const string Bonnet = "C421280.12.C1060000.2102";

    /// <summary>The walk's estimate, its dialogs for the windscreen and the bonnet and the posts' answers, as the editor served them.</summary>
    private static void ScriptWalk(ScriptedGlass glass)
    {
        glass.Respond("GET /ere/aco/selected.php", _ => EditorPage("selected_before_change.html", null));
        glass.Respond("GET /ere/aco/part.php", _ => EditorPage("windscreen_change_hours.dialog.html", null));
        glass.Respond("GET /ere/aco/comp.php", _ => EditorPage("bonnet_change_paint.dialog.html", null));
        glass.Respond("POST /ere/aco/compproc.php", request =>
        {
            var walked = request.Query.Contains("partid=" + Bonnet, StringComparison.Ordinal) ? "bonnet_change_paint" : "windscreen_change_hours";
            return EditorPage(walked + (request.Query.Contains("BX=1", StringComparison.Ordinal) ? ".confirm_answer.html" : ".answer.html"), null);
        });
        // The bonnet's record read after its change (#890); the windscreen's was answered empty, as #946 was: it is not read.
        glass.Respond("GET /ere/aco/mcode.php", request => request.Query.Contains("partid=" + Bonnet, StringComparison.Ordinal)
            ? new Reply(HttpStatusCode.OK, GlassEditorFixture.Text("position_bonnet_changed.json"), ContentType: "application/json")
            : Editor(string.Empty));
    }

    /// <summary>The requests of a change of a position with no paint: the estimate, its dialog, the post, the confirm, the estimate, the record.</summary>
    private static readonly string[] ChangeRequests =
    [
        "GET /ere/aco/selected.php",
        "GET /ere/aco/part.php",
        "POST /ere/aco/compproc.php",
        "POST /ere/aco/compproc.php",
        "GET /ere/aco/selected.php",
        "GET /ere/aco/mcode.php",
    ];

    /// <summary>
    /// A change of a position already on the estimate (R1206), the walk's
    /// windscreen: its hours typed above Glass's own with no reason are
    /// refused before anything is posted; with the Automation's reason its
    /// own dialog, the one the editor's list opens for it, is posted as the
    /// browser posted it, and its "Mandatory parts:" page confirmed as the
    /// browser confirmed it. The position keeps its id; an empty record read
    /// afterwards is not read. A position the estimate does not hold, and
    /// a paint button on a part with no paint, are refused with only the
    /// estimate read.
    /// </summary>
    [Fact]
    public async Task APositionOnTheEstimateIsChangedThroughItsOwnDialog()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        ScriptWalk(harness.Mva);

        var unreasoned = await PositionAsync(harness, Change(harness, session, "mcp:change-1", Windscreen,
            new GlassAutomationInputs(Hours: 2.8m)));

        Assert.Equal(GlassAutomationActOutcome.Refused, unreasoned.Act.Outcome);
        Assert.Equal(GlassAutomationPositionPolicy.ReasonRequiredCode, unreasoned.Act.Code);
        Assert.Null(unreasoned.Act.ExpectedCode);
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
        var before = harness.Mva.Requests.Count;

        var changed = await PositionAsync(harness, Change(harness, unreasoned.Session, "mcp:change-2", Windscreen,
            new GlassAutomationInputs(Hours: 2.8m, Reason: "test")));

        Assert.Equal(GlassAutomationActOutcome.Changed, changed.Act.Outcome);
        Assert.Null(changed.Act.Code);
        Assert.Equal(GlassAutomationActAction.Change, changed.Act.Action);
        Assert.Equal(Windscreen, changed.Act.PositionId);
        Assert.Equal("C1100400", changed.Act.ExpectedCode);
        Assert.Equal(1.80m, changed.Act.DialogServed?.Hours);
        Assert.Equal(2.80m, changed.Act.Sent?.Hours);
        Assert.Null(changed.Act.Position);
        Assert.Equal(5, changed.Act.Listed.Count);
        Assert.Equal(
            ChangeRequests,
            harness.Mva.Requests.Skip(before).Select(request => request.Method + " " + request.Path));
        Assert.Contains("cml=C&partid=" + Windscreen + "&dialogCodePage", Asked(harness, "GET", "part.php")[^1], StringComparison.Ordinal);
        var posts = harness.Mva.Requests.Where(request => request.Method == "POST").ToList();
        Assert.Equal(GlassEditorFixture.Text("windscreen_change_hours.post.txt"), posts[0].Body);
        Assert.Equal(GlassEditorFixture.Text("windscreen_change_hours.confirm.txt"), posts[1].Body);
        Assert.Contains("partid=" + Windscreen, Uri.UnescapeDataString(posts[0].Query), StringComparison.Ordinal);
        var stored = GlassAutomationPositionPolicy.Find(harness.Store.Material(session.Id).Session.Automation, "mcp:change-2");
        Assert.Equal(GlassAutomationActOutcome.Changed, stored?.Outcome);
        var requests = harness.Mva.Requests.Count;

        var unheld = await PositionAsync(harness, Change(harness, changed.Session, "mcp:change-3", CapturedPosition,
            new GlassAutomationInputs(Price: 10m)));
        var painted = await PositionAsync(harness, Change(harness, unheld.Session, "mcp:change-4", Windscreen,
            new GlassAutomationInputs(Button: GlassAutomationButton.Paint)));

        Assert.Equal(GlassAutomationPositionPolicy.RowUnknownCode, unheld.Act.Code);
        Assert.Equal(GlassAutomationPositionPolicy.NotPaintableCode, painted.Act.Code);
        Assert.Equal(GlassAutomationActOutcome.Refused, painted.Act.Outcome);
        // Only the estimate was read for each.
        Assert.Equal(requests + 2, harness.Mva.Requests.Count);
    }

    /// <summary>
    /// The paint of a position on the estimate (R1206), the walk's bonnet:
    /// its own dialog is comp.php with Part and Paint the one button shown,
    /// posted with 2 Coat and level II as the browser posted it (#867), and
    /// confirmed as the browser confirmed it (#873). The act keeps the paint
    /// served and sent, and the bonnet's record read afterwards.
    /// </summary>
    [Fact]
    public async Task APositionsPaintIsChangedAsTheBrowserChangedIt()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);
        ScriptWalk(harness.Mva);

        var changed = await PositionAsync(harness, Change(harness, session, "mcp:paint-1", Bonnet,
            new GlassAutomationInputs(Paint: new GlassAutomationPaint(Coats: "2", Level: "2"))));

        Assert.Equal(GlassAutomationActOutcome.Changed, changed.Act.Outcome);
        var posts = harness.Mva.Requests.Where(request => request.Method == "POST").ToList();
        Assert.Equal(GlassEditorFixture.Text("bonnet_change_paint.post.txt"), posts[0].Body);
        Assert.Equal(GlassEditorFixture.Text("bonnet_change_paint.confirm.txt"), posts[1].Body);
        Assert.Contains("partid=" + Bonnet + "&cml=C&dialogCodePage", Asked(harness, "GET", "comp.php")[^1], StringComparison.Ordinal);
        Assert.Equal("3", changed.Act.DialogServed?.PaintCoats);
        Assert.Equal(4.10m, changed.Act.DialogServed?.PaintHours);
        Assert.Equal("2", changed.Act.Sent?.PaintCoats);
        Assert.Equal("2", changed.Act.Sent?.PaintLevel);
        Assert.Equal(393.90m, changed.Act.Sent?.PaintPrice);
        Assert.Equal(1.40m, changed.Act.Sent?.PaintHours);
        Assert.Equal(Bonnet, changed.Act.Position?.PositionId);
    }

    /// <summary>
    /// Paint chosen with an add (R1206): the left door pressed with Part and
    /// Paint at level I with Surcharge ticked, both sent as its own script
    /// sets them; paint on a part that cannot be painted is refused and
    /// nothing is sent.
    /// </summary>
    [Fact]
    public async Task PaintIsChosenWithAnAddOnAPartThatCanBePainted()
    {
        var harness = Harness.Create();
        var session = await AutomationStartAsync(harness);

        var painted = await PositionAsync(harness, Add(harness, session, "mcp:paint-2",
            GlassAutomationPositionKind.Part, "771", GlassAutomationPartOperation.PartAndPaint,
            inputs: new GlassAutomationInputs(Paint: new GlassAutomationPaint(Level: "1", Surcharge: true))));
        var plain = await PositionAsync(harness, Add(harness, painted.Session, "mcp:paint-3",
            GlassAutomationPositionKind.Part, "193", GlassAutomationPartOperation.PartAndPaint,
            inputs: new GlassAutomationInputs(Paint: new GlassAutomationPaint(Level: "1"))));

        Assert.Equal(GlassAutomationActOutcome.Added, painted.Act.Outcome);
        var post = Assert.Single(DialogPosts(harness));
        Assert.Contains("&Lackstufe=1&", post, StringComparison.Ordinal);
        Assert.Contains("&Zuschlag=1&", post, StringComparison.Ordinal);
        Assert.Contains("&BSL=Part+and+Paint&", post, StringComparison.Ordinal);
        Assert.Equal("1", painted.Act.Sent?.PaintLevel);
        Assert.True(painted.Act.Sent?.Surcharge);
        Assert.Equal(GlassAutomationPositionPolicy.NotPaintableCode, plain.Act.Code);
        Assert.Single(DialogPosts(harness));
    }

    private static GlassAutomationPositionRequest Change(
        Harness harness, GlassRepairEstimateSession session, string operationKey, string positionId,
        GlassAutomationInputs inputs) =>
        new(harness.Automation, session.Id, session.Version, operationKey, GlassAutomationActAction.Change)
        {
            PositionId = positionId,
            Inputs = inputs,
        };

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
    /// makes with read positions.
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

        var read = await harness.Gateway.ReadAutomationEstimateAsync(
            harness.Automation, session.Id, GlassAutomationRead.Positions, CancellationToken.None);

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
        IReadOnlyList<string>? equipment = null,
        GlassAutomationInputs? inputs = null) =>
        new(harness.Automation, session.Id, session.Version, operationKey, GlassAutomationActAction.Add)
        {
            Kind = kind,
            Row = row,
            Operation = operation,
            Side = side,
            Inputs = inputs ?? (equipment is null ? null : new GlassAutomationInputs(equipment)),
        };

    private static GlassAutomationPositionRequest Preview(
        Harness harness, GlassRepairEstimateSession session, string row, GlassAutomationPartOperation operation,
        GlassAutomationInputs? inputs) =>
        new(harness.Automation, session.Id, 0, "mcp:preview-1", GlassAutomationActAction.Preview)
        {
            Kind = GlassAutomationPositionKind.Part,
            Row = row,
            Operation = operation,
            Inputs = inputs,
        };

    /// <summary>The bodies of the dialog posts, in order: every post but a "Mandatory parts:" confirm.</summary>
    private static List<string?> DialogPosts(Harness harness) =>
        harness.Mva.Requests
            .Where(request => request.Method == "POST" && request.Path == "/ere/aco/compproc.php"
                && !request.Query.Contains("BX=", StringComparison.Ordinal))
            .Select(request => request.Body)
            .ToList();

    /// <summary>The left reinforcement's position as the estimate lists it once posted; made up.</summary>
    private const string LeftReinforcement = "C350990.36.C1012401.1768";

    /// <summary>The right reinforcement's position as the estimate lists it once posted; made up.</summary>
    private const string RightReinforcement = "C350991.37.C1012402.1769";

    /// <summary>The door's optional trim support kit, as its "Mandatory parts:" page lists it.</summary>
    private static readonly string[] TrimSupportKit = ["8099801.ER."];

    /// <summary>The estimate after none, one or both of the reinforcement's posts.</summary>
    private static string Reinforced(int posts) => posts switch
    {
        0 => NoPositions,
        1 => "<script>\nvar Partids = [ '', '" + LeftReinforcement + "' ];\nvar CheckIds = [ 'C1012401' ];\n</script>\n",
        _ => "<script>\nvar Partids = [ '', '" + LeftReinforcement + "', '" + RightReinforcement
            + "' ];\nvar CheckIds = [ 'C1012401', 'C1012402' ];\n</script>\n",
    };

    private static GlassAutomationPositionRequest Remove(
        Harness harness, GlassRepairEstimateSession session, string operationKey, string positionId) =>
        new(harness.Automation, session.Id, session.Version, operationKey, GlassAutomationActAction.Remove)
        {
            PositionId = positionId,
        };

    /// <summary>
    /// A position is removed from the dialog the editor's own list opens for
    /// it (R1206): the captured bonnet, a part with its paint, from comp.php.
    /// </summary>
    [Fact]
    public async Task APartWithItsPaintIsRemovedFromItsOwnDialog()
    {
        const string CapturedBonnet = "C350898.20.C1060000.2464";
        var harness = Harness.Create();
        var captured = GlassEditorFixture.Text("selected.html");
        harness.Mva.Respond("GET /ere/aco/selected.php", _ => Editor(
            harness.Mva.Count("GET /ere/aco/compproc.php") == 0
                ? captured
                : captured.Replace("'" + CapturedBonnet + "', ", string.Empty, StringComparison.Ordinal)));
        harness.Mva.Enqueue("GET /ere/aco/comp.php", EditorPage("bonnet_change_paint.dialog.html", null));
        harness.Mva.Enqueue("GET /ere/aco/compproc.php", EditorPage("removed.answer.html", null));
        var session = await AutomationStartAsync(harness);

        var removed = await PositionAsync(harness, Remove(harness, session, "mcp:remove-bonnet", CapturedBonnet));

        Assert.Equal(GlassAutomationActOutcome.Removed, removed.Act.Outcome);
        Assert.Contains("partid=" + CapturedBonnet + "&cml=C", Asked(harness, "GET", "comp.php")[^1], StringComparison.Ordinal);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/part.php"));
    }

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
