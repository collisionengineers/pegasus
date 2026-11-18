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
        Assert.Equal(GlassEstimateBuildPolicy.NotBuiltCode, build.Acts[1].ReasonCode);
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

    private static RepairSpecificationVersion Outside(params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.AudatexPdf, null, null, null),
        lines,
        "engineer", LineRecordedAtUtc,
        new("Repairer", 60m, null, 20m));

    private static CaseEstimateLineRecord OutsideLine(
        int position, string type, string description, decimal? workUnits = null, decimal? price = null) => new(
        Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
        null, null, ActorKind.Staff, "engineer", LineRecordedAtUtc);
}
