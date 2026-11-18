using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// How a repair spec made outside Pegasus becomes a build in Glass's
/// (operator, 18 November 2026; R1168, R1169, R1172): one act per line in
/// the spec's order, special operations tried and chosen by exact name, new
/// parts tried by Glass's part code and side or by part number, Glass's own
/// operations not sent, and every other line left off as not built in this
/// version.
/// </summary>
public sealed class GlassEstimateBuildPolicyTests
{
    private static readonly DateTimeOffset RecordedAtUtc = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);

    private static readonly GlassSpecialOperation Charge = new("AC0000073", ".Assessment Damage Appraisal Charge");

    private static readonly GlassSpecialOperation Cleaning = new("AC0000075", ".Pre repair cleaning charge");

    private static readonly int[] InSpecOrder = [1, 2, 3];

    [Fact]
    public void EachLineIsOneActInTheSpecsOrder()
    {
        var build = GlassEstimateBuildPolicy.Plan(Spec(
            RepairSpecificationSourceRoute.AudatexPdf,
            Line(3, "new_part", "Front Door Shell (L)", partNumber: "41007408961", price: 942.95m),
            Line(1, "specialist_fixed", ".Sundries", price: 20m),
            Line(2, "specialist_wu", ".Pre repair cleaning charge ", workUnits: 0.5m)));

        Assert.NotNull(build);
        Assert.Equal(InSpecOrder, build.Acts.Select(act => act.SourcePositions.Single()));
        Assert.Equal(3, build.Total);
        Assert.Equal(0, build.Done);
        Assert.True(build.Running);
        Assert.False(build.Placed);
    }

    [Fact]
    public void ASpecialistLineIsAnActToTryByItsName()
    {
        var act = OnlyAct(Line(1, "specialist_wu", "  .Pre repair cleaning charge ", workUnits: 0.5m));

        Assert.Equal(GlassEstimateBuildKind.SpecialOperation, act.Kind);
        Assert.True(act.Sends);
        Assert.Equal(GlassEstimateBuildOutcome.Pending, act.Outcome);
        Assert.Equal(".Pre repair cleaning charge", act.Key);
        Assert.Null(act.ReasonCode);
        Assert.Equal(new GlassEstimateBuildFigures(null, 0.5m), act.Source);
    }

    [Theory]
    [InlineData("paint_prep", GlassEstimateBuildKind.PaintPreparation)]
    [InlineData("check_labour", GlassEstimateBuildKind.IncludedOperation)]
    public void GlassOwnOperationsAreNotSent(string type, GlassEstimateBuildKind kind)
    {
        var act = OnlyAct(Line(1, type, "Remove trim", justification: IncludedOperations.Note(null)));

        Assert.Equal(kind, act.Kind);
        Assert.False(act.Sends);
        Assert.Equal(GlassEstimateBuildOutcome.AddedByGlass, act.Outcome);
        Assert.Null(act.ReasonCode);
    }

    [Theory]
    [InlineData("paint_new", GlassEstimateBuildKind.Paint)]
    [InlineData("paint_repair", GlassEstimateBuildKind.Paint)]
    [InlineData("paint_blend", GlassEstimateBuildKind.Paint)]
    [InlineData("repair", GlassEstimateBuildKind.Repair)]
    [InlineData("rnr", GlassEstimateBuildKind.RemoveAndRefit)]
    [InlineData("check_labour", GlassEstimateBuildKind.Operation)]
    public void EveryOtherLineIsLeftOffAsNotBuiltInThisVersion(string type, GlassEstimateBuildKind kind)
    {
        var build = GlassEstimateBuildPolicy.Plan(Spec(
            RepairSpecificationSourceRoute.Glasses,
            Line(1, "specialist_fixed", ".Sundries", price: 20m),
            Line(2, type, "Front Wing (R)", guideCode: "3030081", workUnits: 1.2m)));

        var act = Assert.Single(build!.Acts, candidate => candidate.SourcePositions.Single() == 2);
        Assert.Equal(kind, act.Kind);
        Assert.False(act.Sends);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, act.Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NotBuiltCode, act.ReasonCode);
    }

    /// <summary>The key each line is found by in Glass's, and the side its description names (design table).</summary>
    [Fact]
    public void APartIsKeyedByItsGlassCodeOrItsAudatexPartNumberAndASideIsRead()
    {
        var fromGlass = OnlyAct(
            Line(1, "new_part", "Front Door Shell (L)", guideCode: "2020001", partNumber: "41007408961"),
            RepairSpecificationSourceRoute.Glasses);
        var fromAudatex = OnlyAct(
            Line(1, "new_part", "Front Door Shell (R)", guideCode: "6F0807", partNumber: "41007408961"),
            RepairSpecificationSourceRoute.AudatexPdf);
        var typedIn = OnlyAct(
            Line(1, "new_part", "Front Door Shell (Both)", guideCode: "2020001", partNumber: "41007408961"),
            RepairSpecificationSourceRoute.Manual);

        Assert.Equal("2020001", fromGlass.Key);
        Assert.Equal(GlassEstimateBuildKeyKind.PartCode, fromGlass.KeyKind);
        Assert.Equal("L", fromGlass.Side);
        Assert.Equal("41007408961", fromAudatex.Key);
        Assert.Equal(GlassEstimateBuildKeyKind.PartNumber, fromAudatex.KeyKind);
        Assert.Equal("R", fromAudatex.Side);
        Assert.Null(typedIn.Key);
        Assert.Equal(GlassEstimateBuildKeyKind.None, typedIn.KeyKind);
        Assert.Null(typedIn.Side);
    }

    /// <summary>A new part with a key is an act to try: its row is chosen at Glass's (R1172).</summary>
    [Theory]
    [InlineData(RepairSpecificationSourceRoute.Glasses)]
    [InlineData(RepairSpecificationSourceRoute.AudatexPdf)]
    public void ANewPartWithAKeyIsAnActToTry(RepairSpecificationSourceRoute route)
    {
        var act = OnlyAct(
            Line(1, "new_part", "Front Door Shell (L)", guideCode: "2020001", partNumber: "41007408961", price: 942.95m),
            route);

        Assert.Equal(GlassEstimateBuildKind.Part, act.Kind);
        Assert.True(act.Sends);
        Assert.Equal(GlassEstimateBuildOutcome.Pending, act.Outcome);
        Assert.Null(act.ReasonCode);
        Assert.Equal(new GlassEstimateBuildFigures(942.95m, null), act.Source);
    }

    /// <summary>A new part with nothing to find it by in Glass's is left off with that reason.</summary>
    [Theory]
    [InlineData(RepairSpecificationSourceRoute.Manual, "2020001", "41007408961")]
    [InlineData(RepairSpecificationSourceRoute.AiDraft, "2020001", "41007408961")]
    [InlineData(RepairSpecificationSourceRoute.Json, "2020001", "41007408961")]
    [InlineData(RepairSpecificationSourceRoute.Glasses, null, "41007408961")]
    [InlineData(RepairSpecificationSourceRoute.AudatexPdf, "2020001", "  ")]
    public void ANewPartWithNothingToFindItByIsLeftOff(
        RepairSpecificationSourceRoute route, string? guideCode, string? partNumber)
    {
        var act = OnlyAct(Line(1, "new_part", "Front Door Shell (L)", guideCode: guideCode, partNumber: partNumber), route);

        Assert.False(act.Sends);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, act.Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.NoKeyCode, act.ReasonCode);
        Assert.Null(act.Key);
        Assert.Equal(GlassEstimateBuildKeyKind.None, act.KeyKind);
    }

    /// <summary>
    /// A paint line is left off even beside its part's line (R1172): a
    /// Glass's export's paint row carries a paint code (1640 for the bonnet),
    /// not the part's code (1060000), so nothing ties the two.
    /// </summary>
    [Fact]
    public void APaintLineIsLeftOffBesideItsPart()
    {
        var build = GlassEstimateBuildPolicy.Plan(Spec(
            RepairSpecificationSourceRoute.Glasses,
            Line(1, "new_part", "Bonnet", guideCode: "1060000", price: 361.74m),
            Line(2, "paint_new", "Bonnet", guideCode: "1640", workUnits: 2.8m)))!;

        Assert.True(build.Acts[0].Sends);
        Assert.Equal(GlassEstimateBuildKind.Paint, build.Acts[1].Kind);
        Assert.False(build.Acts[1].Sends);
        Assert.Equal(GlassEstimateBuildPolicy.NotBuiltCode, build.Acts[1].ReasonCode);
        Assert.Equal(1, build.Total);
    }

    [Fact]
    public void ASpecWithNoLinesHasNothingToBuild()
    {
        Assert.Null(GlassEstimateBuildPolicy.Plan(Spec(RepairSpecificationSourceRoute.Manual)));
    }

    /// <summary>A spec with nothing to send is not taken into the editor: it stops before it starts.</summary>
    [Fact]
    public void ASpecWithNothingToSendStopsBeforeItStarts()
    {
        var build = GlassEstimateBuildPolicy.Plan(Spec(
            RepairSpecificationSourceRoute.AudatexPdf,
            Line(1, "new_part", "Bonnet", price: 310m),
            Line(2, "paint_new", "Bonnet", workUnits: 2.8m)));

        Assert.NotNull(build);
        Assert.Equal(0, build.Total);
        Assert.Equal(GlassEstimateBuildPolicy.NothingPlacedCode, build.StoppedAt);
        Assert.False(build.Running);
    }

    [Fact]
    public void ASpecialOperationIsTheOneRowWithExactlyThatName()
    {
        var (row, reason) = GlassEstimateBuildPolicy.ChooseSpecialOperation(
            [Charge, Cleaning], ".Pre repair cleaning charge");

        Assert.Equal(Cleaning, row);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("Pre repair cleaning charge")]
    [InlineData(".pre repair cleaning charge")]
    [InlineData("")]
    [InlineData(null)]
    public void ANameNoRowHasExactlyIsLeftOff(string? name)
    {
        var (row, reason) = GlassEstimateBuildPolicy.ChooseSpecialOperation([Charge, Cleaning], name);

        Assert.Null(row);
        Assert.Equal(GlassEstimateBuildPolicy.NoRowCode, reason);
    }

    [Fact]
    public void ANameTwoRowsHaveIsLeftOffAsAmbiguous()
    {
        var (row, reason) = GlassEstimateBuildPolicy.ChooseSpecialOperation(
            [Cleaning, Cleaning with { Code = "AC0000175" }], Cleaning.Name);

        Assert.Null(row);
        Assert.Equal(GlassEstimateBuildPolicy.AmbiguousRowCode, reason);
    }

    [Fact]
    public void ALostAnswerLeavesEveryActNotYetTriedOff()
    {
        var build = GlassEstimateBuildPolicy.Plan(Spec(
            RepairSpecificationSourceRoute.Manual,
            Line(1, "specialist_fixed", Charge.Name, price: 150m),
            Line(2, "specialist_wu", Cleaning.Name, workUnits: 0.5m),
            Line(3, "paint_prep", "Preparation")))!;

        var lost = GlassEstimateBuildPolicy.Record(build, 0, build.Acts[0] with
        {
            Outcome = GlassEstimateBuildOutcome.NotConfirmed,
            ReasonCode = "glass.editor.answer",
        });
        var stopped = GlassEstimateBuildPolicy.LeaveOffPending(lost, GlassEstimateBuildPolicy.StoppedCode);

        Assert.Equal(GlassEstimateBuildOutcome.NotConfirmed, stopped.Acts[0].Outcome);
        Assert.Equal(GlassEstimateBuildOutcome.LeftOff, stopped.Acts[1].Outcome);
        Assert.Equal(GlassEstimateBuildPolicy.StoppedCode, stopped.Acts[1].ReasonCode);
        Assert.Equal(GlassEstimateBuildOutcome.AddedByGlass, stopped.Acts[2].Outcome);
        Assert.Equal(stopped.Total, stopped.Done);
        Assert.True(stopped.Placed);
    }

    /// <summary>A rebuilt line carries Glass's own figures; the list says where they differ (R1168).</summary>
    [Fact]
    public void TheListSaysWhereGlasssFiguresDiffer()
    {
        var act = OnlyAct(Line(1, "specialist_fixed", Charge.Name, price: 150m));

        var differs = act with { Served = new GlassEstimateBuildFigures(176.96m, null) };
        var same = act with { Served = new GlassEstimateBuildFigures(150m, null) };

        Assert.True(GlassEstimateBuildPolicy.PriceDiffers(differs));
        Assert.False(GlassEstimateBuildPolicy.HoursDiffers(differs));
        Assert.False(GlassEstimateBuildPolicy.PriceDiffers(same));
        Assert.False(GlassEstimateBuildPolicy.PriceDiffers(act));
    }

    private static GlassEstimateBuildAct OnlyAct(
        CaseEstimateLineRecord line, RepairSpecificationSourceRoute route = RepairSpecificationSourceRoute.Manual) =>
        Assert.Single(GlassEstimateBuildPolicy.Plan(Spec(route, line))!.Acts);

    internal static RepairSpecificationVersion Spec(
        RepairSpecificationSourceRoute route, params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), Guid.NewGuid(), 1, RepairSpecificationState.Draft,
        new(route, null, null, null),
        lines,
        "engineer", RecordedAtUtc,
        new("Repairer", 60m, null, 20m));

    internal static CaseEstimateLineRecord Line(
        int position,
        string type,
        string description,
        string? guideCode = null,
        string? partNumber = null,
        decimal? workUnits = null,
        decimal? price = null,
        string? justification = null) => new(
        Guid.NewGuid(), position, type, guideCode, description, workUnits, price, false, partNumber, null,
        null, justification, ActorKind.Staff, "engineer", RecordedAtUtc);
}
