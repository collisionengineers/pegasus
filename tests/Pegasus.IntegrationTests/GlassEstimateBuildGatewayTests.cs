using System.Net;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Glass;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

// A repair spec made outside Pegasus rebuilt in Glass's (operator, 18
// November 2026; R1168 to R1173), through the gateway against the scripted
// portal and the scripted editor of GlassProviderFixture: the launch, the
// special operations added as their dialogs serve them, Save and Exit, and
// the build's own return imported as a new Glass's spec.
public sealed partial class GlassRepairEstimateGatewayTests
{
    private const string Charge = ".Assessment Damage Appraisal Charge";

    private const string Cleaning = ".Pre repair cleaning charge";

    private static readonly DateTimeOffset LineRecordedAtUtc = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The rows whose equipment maps a build of the two doors reads.</summary>
    private static readonly string[] DoorMaps = ["rowid=771", "rowid=790"];

    /// <summary>The left door's map, read once for each of its two lines.</summary>
    private static readonly string[] DoorMapTwice = ["rowid=771", "rowid=771"];

    /// <summary>The row pairs whose dialogs a build of the two doors asks for.</summary>
    private static readonly string[] DoorDialogs = ["mcode=771|772", "mcode=790|791"];

    /// <summary>How many acts were done as each of the two posts was made.</summary>
    private static readonly int[] DoneAtEachPost = [0, 1];

    [Fact]
    public async Task ABuildAddsItsSpecialOperationsAsServedAndLandsAsANewGlassSpec()
    {
        var harness = Harness.Create();
        var outside = Outside(
            OutsideLine(1, "specialist_fixed", Charge, price: 150m),
            OutsideLine(2, "new_part", "Front Door Shell (L)", price: 942.95m),
            OutsideLine(3, "paint_prep", "Preparation"));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Null(session.FailureCode);
        var build = Assert.IsType<GlassEstimateBuild>(session.Build);
        Assert.Equal(outside.SpecificationId, build.SourceSpecificationId);
        Assert.Equal(harness.Import.EstimateId, build.BuiltSpecificationId);
        Assert.Null(build.StoppedAt);
        var charge = build.Acts[0];
        Assert.Equal(GlassEstimateBuildOutcome.Added, charge.Outcome);
        Assert.Equal(new GlassEstimateBuildFigures(176.96m, null), charge.Served);
        Assert.True(GlassEstimateBuildPolicy.PriceDiffers(charge));
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, build.Acts[1].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NoKeyCode, build.Acts[1].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.AddedByGlass, build.Acts[2].Outcome);

        // One posted position, one save, and the build's own return relayed
        // and imported once, as a new spec beside the outside one.
        Assert.Equal(1, harness.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/service.php"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        Assert.Equal(1, harness.Mva.Count("POST /ere/start-ere"));
        var import = Assert.Single(harness.Import.Requests);
        Assert.Null(import.EstimateId);
        Assert.Equal(VehicleId, import.GlassEstimate?.VehicleId);

        // Launching until the return is handed over: never Active while the
        // editor is the build's own.
        var states = harness.Store.History.Select(entry => entry.State).ToList();
        var active = states.IndexOf(GlassRepairEstimateSessionState.Active);
        Assert.True(active > 0);
        Assert.Equal(GlassRepairEstimateSessionState.Importing, states[active + 1]);
        Assert.All(states.Take(active), state => Assert.True(
            state is GlassRepairEstimateSessionState.Prepared or GlassRepairEstimateSessionState.Launching));
    }

    /// <summary>Progress is written as each act is reached, so the window can say how far it has gone.</summary>
    [Fact]
    public async Task EachActIsRecordedAsItIsReached()
    {
        var harness = Harness.Create();
        var outside = Outside(
            OutsideLine(1, "specialist_fixed", Charge, price: 176.96m),
            OutsideLine(2, "specialist_wu", Cleaning, workUnits: 0.5m));
        var seen = new List<int>();
        harness.Mva.OnServed("POST /ere/aco/compproc.php", _ =>
        {
            var current = harness.Store.Sessions.Values.Single().Session.Build!;
            seen.Add(current.Done);
        });

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Equal(DoneAtEachPost, seen);
        Assert.Equal(2, session.Build!.Done);
        Assert.Equal(2, session.Build.Total);
        Assert.False(GlassEstimateBuildPolicy.PriceDiffers(session.Build.Acts[0]));
        Assert.False(GlassEstimateBuildPolicy.HoursDiffers(session.Build.Acts[1]));
    }

    /// <summary>A line Glass's has no row for is left off and listed, and the build goes on (R1169).</summary>
    [Fact]
    public async Task ALineWithNoSpecialOperationOfItsNameIsLeftOffAndTheBuildGoesOn()
    {
        var harness = Harness.Create();
        var outside = Outside(
            OutsideLine(1, "specialist_fixed", "Valet", price: 40m),
            OutsideLine(2, "specialist_fixed", Charge, price: 176.96m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, session.Build!.Acts[0].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NoRowCode, session.Build.Acts[0].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.Added, session.Build.Acts[1].Outcome);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/part.php"));
    }

    /// <summary>A dialog the editor refuses before anything is posted leaves that line off; the next is still added.</summary>
    [Fact]
    public async Task ARefusedDialogIsLeftOffAndTheBuildGoesOn()
    {
        var harness = Harness.Create();
        // The first dialog asked for is another operation's: its part code
        // is not the one asked for, so nothing is posted for it.
        harness.Mva.Enqueue("GET /ere/aco/part.php", EditorPage("special_hours.dialog.html", null));
        var outside = Outside(
            OutsideLine(1, "specialist_fixed", Charge, price: 176.96m),
            OutsideLine(2, "specialist_wu", Cleaning, workUnits: 0.5m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, session.Build!.Acts[0].Outcome);
        Assert.Equal(GlassFailure.EditorRefused, session.Build.Acts[0].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.Added, session.Build.Acts[1].Outcome);
        Assert.Equal(1, harness.Mva.Count("POST /ere/aco/compproc.php"));
    }

    /// <summary>A post whose answer is lost stops the adding; what was placed is saved and imported.</summary>
    [Fact]
    public async Task ALostAnswerStopsTheAddingAndTheBuildSavesWhatItHas()
    {
        var harness = Harness.Create();
        harness.Mva.Enqueue("POST /ere/aco/compproc.php", new Reply(HttpStatusCode.InternalServerError, string.Empty));
        var outside = Outside(
            OutsideLine(1, "specialist_fixed", Charge, price: 176.96m),
            OutsideLine(2, "specialist_wu", Cleaning, workUnits: 0.5m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Equal(GlassEstimateBuildOutcome.NotConfirmed, session.Build!.Acts[0].Outcome);
        Assert.Equal(GlassFailure.EditorAnswer, session.Build.Acts[0].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, session.Build.Acts[1].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.StoppedCode, session.Build.Acts[1].ReasonCode);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/part.php"));
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/service.php"));
        Assert.Single(harness.Import.Requests);
    }

    /// <summary>
    /// An editor that cannot be opened has been sent nothing: the estimate is
    /// started again under the same callback and the member of staff gets an
    /// empty estimate, as a launch gives.
    /// </summary>
    [Fact]
    public async Task AnEditorThatDoesNotOpenGivesTheMemberOfStaffAnEmptyEstimate()
    {
        var harness = Harness.Create();
        harness.Mva.Enqueue("GET /ere/acolib/aco_call_xml.php", new Reply(HttpStatusCode.NotFound, string.Empty));
        var outside = Outside(OutsideLine(1, "specialist_fixed", Charge, price: 176.96m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Equal(GlassFailure.EditorLaunch, session.Build!.StoppedAt);
        Assert.Null(session.Build.BuiltSpecificationId);
        Assert.Equal(2, harness.Mva.Count("POST /ere/start-ere"));
        Assert.Equal(1, harness.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(0, harness.Mva.Count("POST /ere/aco/compproc.php"));
        var estimator = await harness.Gateway.GetEstimatorUrlAsync(harness.Engineer, session.Id, CancellationToken.None);
        Assert.NotNull(estimator);
        Assert.Empty(harness.Import.Requests);
    }

    /// <summary>Nothing placed is nothing saved: the member of staff gets an empty estimate.</summary>
    [Fact]
    public async Task ABuildThatPlacesNothingSavesNothingAndOpensAnEmptyEstimate()
    {
        var harness = Harness.Create();
        var outside = Outside(OutsideLine(1, "specialist_fixed", "Valet", price: 40m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Equal(GlassEstimateBuildPolicy.NothingPlacedCode, session.Build!.StoppedAt);
        Assert.Equal(GlassEstimateBuildPolicy.NoRowCode, session.Build.Acts[0].ReasonCode);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/service.php"));
        Assert.Equal(2, harness.Mva.Count("POST /ere/start-ere"));
        Assert.Empty(harness.Import.Requests);
    }

    /// <summary>A spec with nothing to send is launched as any spec is, without the editor.</summary>
    [Fact]
    public async Task ASpecWithNothingToSendIsLaunchedWithoutTheEditor()
    {
        var harness = Harness.Create();
        var outside = Outside(OutsideLine(1, "new_part", "Bonnet", price: 310m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Equal(GlassEstimateBuildPolicy.NothingPlacedCode, session.Build!.StoppedAt);
        Assert.Equal(0, harness.Mva.Count("GET /ere/acolib/aco_call_xml.php"));
        Assert.Equal(1, harness.Mva.Count("POST /ere/start-ere"));
    }

    /// <summary>A save the editor does not confirm leaves the outcome for a person: Unknown, holding the account.</summary>
    [Fact]
    public async Task ASaveThatFailsLeavesTheSessionUnknown()
    {
        var harness = Harness.Create();
        harness.Mva.Enqueue("GET /ere/aco/service.php", new Reply(HttpStatusCode.InternalServerError, string.Empty));
        var outside = Outside(OutsideLine(1, "specialist_fixed", Charge, price: 176.96m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Unknown, session.State);
        Assert.Equal(GlassFailure.EditorSave, session.FailureCode);
        Assert.Equal(GlassEstimateBuildOutcome.Added, session.Build!.Acts[0].Outcome);
        Assert.True(GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State));
        Assert.Empty(harness.Import.Requests);
    }

    /// <summary>A spec that belongs to a Glass's estimate reopens it, whatever was chosen.</summary>
    [Fact]
    public async Task ALinkedSpecIgnoresTheBuildAndReopensItsEstimate()
    {
        var harness = Harness.Create();
        var outside = Outside(OutsideLine(1, "specialist_fixed", Charge, price: 176.96m));
        harness.CaseAuthority.Links[outside.SpecificationId] = new GlassEstimateLink(
            VehicleId, EreId, NatCode, Placeholder: false, Registration, MileageMiles);
        // The portal shows a vehicle that holds an estimate with its profile locked.
        harness.Mva.Set("GET /index/vehicle-details-value/", new(HttpStatusCode.OK, VehicleDetail(locked: true)));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Null(session.Build);
        Assert.Equal(0, harness.Mva.Count("GET /index/create-new-vehicle"));
        Assert.Equal(0, harness.Mva.Count("GET /ere/acolib/aco_call_xml.php"));
    }

    /// <summary>
    /// A Glass's export's new parts (R1172): one whose row needs no equipment
    /// answer is found by its code and side, its equipment map read, and its
    /// dialog posted as served with Part, figures and all; one whose rows wait
    /// on equipment is left off with that reason and nothing is asked for it;
    /// one whose dialog serves the other side is refused before it is posted;
    /// a paint line is left off.
    /// </summary>
    [Fact]
    public async Task ABuildPlacesAPartWhoseRowNeedsNoEquipmentAnswerAndLeavesTheRestOff()
    {
        var harness = Harness.Create();
        var outside = OutsideFrom(
            RepairSpecificationSourceRoute.Glasses,
            OutsideLine(1, "new_part", "Front Door Shell (L)", workUnits: 3.7m, price: 942.95m, guideCode: "2020001"),
            OutsideLine(2, "paint_new", "Front Door Shell (L)", workUnits: 1.5m, guideCode: "2015"),
            OutsideLine(3, "new_part", "Front Bumper Lining", workUnits: 1.8m, price: 379m, guideCode: "1010800"),
            OutsideLine(4, "new_part", "Front Door Shell (R)", workUnits: 3.7m, price: 942.95m, guideCode: "2020002"),
            OutsideLine(5, "specialist_fixed", Charge, price: 176.96m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        var acts = session.Build!.Acts;
        Assert.Equal(GlassEstimateBuildOutcome.Added, acts[0].Outcome);
        Assert.Equal(new GlassEstimateBuildFigures(545.24m, 2.90m), acts[0].Served);
        Assert.True(GlassEstimateBuildPolicy.PriceDiffers(acts[0]));
        Assert.True(GlassEstimateBuildPolicy.HoursDiffers(acts[0]));
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, acts[1].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NotBuiltCode, acts[1].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, acts[2].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.EquipmentCode, acts[2].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, acts[3].Outcome);
        Assert.Equal(GlassFailure.EditorRefused, acts[3].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.Added, acts[4].Outcome);
        Assert.Equal(4, session.Build.Total);

        // The list once; the maps of the two doors only (the lining's rows
        // need equipment by the list alone); a dialog for each door; and
        // posts for the left door, its mandatory parts and the special
        // operation, none for the right door.
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/list.php"));
        Assert.Contains("&ds=" + GlassRepairEstimateGateway.PartListGroups + "&", Assert.Single(Asked(harness, "GET", "list.php")), StringComparison.Ordinal);
        Assert.Equal(
            DoorMaps,
            Asked(harness, "GET", "service.php").Where(query => query.Contains("reqcritmap3=1", StringComparison.Ordinal))
                .Select(query => query.Split('&').Single(part => part.StartsWith("rowid=", StringComparison.Ordinal))));
        Assert.Equal(
            DoorDialogs,
            Asked(harness, "GET", "comp.php").Select(query => query.Split('&').Single(part => part.StartsWith("mcode=", StringComparison.Ordinal))));
        var posts = Asked(harness, "POST", "compproc.php");
        Assert.Equal(3, posts.Count);
        Assert.DoesNotContain(posts, query => query.Contains("790", StringComparison.Ordinal));
        Assert.Single(harness.Import.Requests);
    }

    /// <summary>An Audatex sheet's new part is found by its part number, and its side is checked as served.</summary>
    [Fact]
    public async Task AnAudatexPartIsFoundByItsPartNumber()
    {
        var harness = Harness.Create();
        var outside = Outside(
            OutsideLine(1, "new_part", "Front Door Shell (L)", workUnits: 3.7m, price: 942.95m, partNumber: "41009628759"),
            OutsideLine(2, "new_part", "Front Door Outer Handle (L)", price: 66.30m, partNumber: "5G0837205NGRU"));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Equal(GlassEstimateBuildOutcome.Added, session.Build!.Acts[0].Outcome);
        Assert.Equal(GlassEstimateBuildKeyKind.PartNumber, session.Build.Acts[0].KeyKind);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, session.Build.Acts[1].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.EquipmentCode, session.Build.Acts[1].ReasonCode);
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/comp.php"));
    }

    /// <summary>A part list that cannot be read leaves each part off with the reason; the rest is still built.</summary>
    [Fact]
    public async Task APartListThatCannotBeReadLeavesThePartsOff()
    {
        var harness = Harness.Create();
        harness.Mva.Enqueue("GET /ere/aco/list.php", new Reply(HttpStatusCode.InternalServerError, string.Empty));
        var outside = OutsideFrom(
            RepairSpecificationSourceRoute.Glasses,
            OutsideLine(1, "new_part", "Front Door Shell (L)", price: 942.95m, guideCode: "2020001"),
            OutsideLine(2, "specialist_fixed", Charge, price: 176.96m));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, session.Build!.Acts[0].Outcome);
        Assert.Equal(GlassFailure.EditorRequest, session.Build.Acts[0].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.Added, session.Build.Acts[1].Outcome);
        Assert.Equal(0, harness.Mva.Count("GET /ere/aco/comp.php"));
    }

    /// <summary>
    /// A Glass's export's repair and remove-and-refit lines (R1172): each is
    /// found by its code and side as a part is, its dialog asked for by the
    /// same rows, Repair or Uninstall and install chosen in it, and posted
    /// exactly as the dialog's own script posted it, with whatever Glass's
    /// serves: no figures for the repair (the dialog's question answered OK)
    /// and its own hours for the refit. A refit whose rows wait on equipment
    /// is left off with that reason and nothing is asked for it, and a
    /// partial repair is left off as not built.
    /// </summary>
    [Fact]
    public async Task ABuildSendsRepairAndRefitWithWhateverGlassServesAndLeavesTheRestOff()
    {
        var harness = Harness.Create();
        var outside = OutsideFrom(
            RepairSpecificationSourceRoute.Glasses,
            OutsideLine(1, "repair", "Front Door Shell (L)", workUnits: 2.5m, guideCode: "2020001"),
            OutsideLine(2, "rnr", "Front Door Shell (L)", workUnits: 0.6m, guideCode: "2020001"),
            OutsideLine(3, "rnr", "Front Bumper Lining", workUnits: 0.8m, guideCode: "1010800"),
            OutsideLine(4, "repair", "Front Wing (L)", workUnits: 1.0m, guideCode: "3030081",
                justification: GlassEstimateBuildPolicy.PartialRepairNote));

        var session = await harness.BuildAsync(outside);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, session.State);
        var acts = session.Build!.Acts;
        Assert.Equal(GlassEstimateBuildKind.Repair, acts[0].Kind);
        Assert.Equal(GlassEstimateBuildOutcome.Added, acts[0].Outcome);
        Assert.Equal(new GlassEstimateBuildFigures(null, null), acts[0].Served);
        Assert.True(GlassEstimateBuildPolicy.HoursDiffers(acts[0]));
        Assert.Equal(GlassEstimateBuildKind.RemoveAndRefit, acts[1].Kind);
        Assert.Equal(GlassEstimateBuildOutcome.Added, acts[1].Outcome);
        Assert.Equal(new GlassEstimateBuildFigures(null, 0.40m), acts[1].Served);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, acts[2].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.EquipmentCode, acts[2].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, acts[3].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NotBuiltCode, acts[3].ReasonCode);
        Assert.Equal(2, session.Build.Total);

        // The list once, the door's map and dialog once for each of its two
        // lines, and the two dialog posts byte for byte as the dialog's own
        // script sent them, each followed by its "Mandatory parts:" post.
        Assert.Equal(1, harness.Mva.Count("GET /ere/aco/list.php"));
        Assert.Equal(
            DoorMapTwice,
            Asked(harness, "GET", "service.php").Where(query => query.Contains("reqcritmap3=1", StringComparison.Ordinal))
                .Select(query => query.Split('&').Single(part => part.StartsWith("rowid=", StringComparison.Ordinal))));
        Assert.Equal(2, harness.Mva.Count("GET /ere/aco/comp.php"));
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
        Assert.Equal(4, harness.Mva.Count("POST /ere/aco/compproc.php"));
        Assert.Single(harness.Import.Requests);
    }

    private static RepairSpecificationVersion Outside(params CaseEstimateLineRecord[] lines) =>
        OutsideFrom(RepairSpecificationSourceRoute.AudatexPdf, lines);

    private static RepairSpecificationVersion OutsideFrom(
        RepairSpecificationSourceRoute route, params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
        new(route, null, null, null),
        lines,
        "engineer", LineRecordedAtUtc,
        new("Repairer", 60m, null, 20m));

    private static CaseEstimateLineRecord OutsideLine(
        int position,
        string type,
        string description,
        decimal? workUnits = null,
        decimal? price = null,
        string? guideCode = null,
        string? partNumber = null,
        string? justification = null) => new(
        Guid.NewGuid(), position, type, guideCode, description, workUnits, price, false, partNumber, null,
        null, justification, ActorKind.Staff, "engineer", LineRecordedAtUtc);

    /// <summary>The scripted editor's requests to one page, with their queries unescaped.</summary>
    private static List<string> Asked(Harness harness, string method, string page) =>
        harness.Mva.Requests
            .Where(request => request.Method == method && request.Path == "/ere/aco/" + page)
            .Select(request => Uri.UnescapeDataString(request.Query))
            .ToList();
}
