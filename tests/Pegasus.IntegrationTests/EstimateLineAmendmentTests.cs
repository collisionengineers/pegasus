using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Web.Pages.Cases;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-047 B04: the Case estimate editor replaces the whole line collection
/// on save, so amendment attribution is decided line by line. A line that
/// came back exactly as it was loaded keeps the stamp it already carried; a
/// line whose editable values moved names the operator who moved them and the
/// server's time. These are the eight values the editor lets an operator
/// change, one test case each.
/// </summary>
public sealed class EstimateLineAmendmentTests
{
    private const string PriorActor = "engineer-before";
    private const string Actor = "engineer-now";

    private static readonly DateTimeOffset PriorAmendedAtUtc =
        new(2031, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static readonly DateTimeOffset SavedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    public static TheoryData<string, EstimateLineInput> ChangedLines => new()
    {
        { "operation", Saved() with { Type = "repair" } },
        { "description", Saved() with { Description = "Door skin, offside" } },
        { "part number", Saved() with { PartNumber = "P-9999" } },
        { "quantity", Saved() with { Quantity = 2 } },
        { "panel hours", Saved() with { WorkUnits = 3.5m } },
        { "paint hours", Saved() with { PaintWorkUnits = 1.25m } },
        { "materials", Saved() with { Materials = 19.99m } },
        { "unit amount", Saved() with { Price = 240.01m } },
    };

    [Fact]
    public void AnUnchangedLineKeepsTheAttributionItAlreadyCarried()
    {
        var (amendedBy, amendedAtUtc) =
            EstimateLineAmendment.Stamp(Saved(), Loaded(), Actor, SavedAtUtc);

        Assert.True(EstimateLineAmendment.IsUnchanged(Saved(), Loaded()));
        Assert.Equal(PriorActor, amendedBy);
        Assert.Equal(PriorAmendedAtUtc, amendedAtUtc);
    }

    /// <summary>
    /// The screen offers no finer operation than
    /// <see cref="EstimateOperation"/>, so an imported <c>paint_new</c> line
    /// the operator never touched is posted back as <c>paint_repair</c>. That
    /// is the editor's vocabulary, not an amendment.
    /// </summary>
    [Fact]
    public void ALineTypeTheEditorCannotExpressIsNotAnAmendment()
    {
        var loaded = Loaded() with { Type = "paint_new", PartNumber = null, Quantity = null };
        var saved = Saved() with { Type = "paint_repair", PartNumber = null, Quantity = null };

        var (amendedBy, amendedAtUtc) =
            EstimateLineAmendment.Stamp(saved, loaded, Actor, SavedAtUtc);

        Assert.Equal(PriorActor, amendedBy);
        Assert.Equal(PriorAmendedAtUtc, amendedAtUtc);
    }

    [Theory]
    [MemberData(nameof(ChangedLines))]
    public void AChangedFieldStampsThisActorAndTheServersTime(
        string field, EstimateLineInput saved)
    {
        var (amendedBy, amendedAtUtc) =
            EstimateLineAmendment.Stamp(saved, Loaded(), Actor, SavedAtUtc);

        Assert.False(
            EstimateLineAmendment.IsUnchanged(saved, Loaded()),
            $"A changed {field} is an amendment.");
        Assert.Equal(Actor, amendedBy);
        Assert.Equal(SavedAtUtc, amendedAtUtc);
    }

    /// <summary>
    /// A line the operator cleared moved just as much as one they retyped.
    /// </summary>
    [Fact]
    public void ClearingAnEditableValueIsAnAmendment()
    {
        var saved = Saved() with { Price = null };

        var (amendedBy, amendedAtUtc) =
            EstimateLineAmendment.Stamp(saved, Loaded(), Actor, SavedAtUtc);

        Assert.Equal(Actor, amendedBy);
        Assert.Equal(SavedAtUtc, amendedAtUtc);
    }

    /// <summary>
    /// A line the source document never stamped, and that the operator did
    /// not touch, still carries no attribution: an unchanged line is never
    /// given one it did not have.
    /// </summary>
    [Fact]
    public void AnUnchangedLineWithNoPriorAttributionGainsNone()
    {
        var loaded = Loaded() with { AmendedBy = null, AmendedAtUtc = null };

        var (amendedBy, amendedAtUtc) =
            EstimateLineAmendment.Stamp(Saved(), loaded, Actor, SavedAtUtc);

        Assert.Null(amendedBy);
        Assert.Null(amendedAtUtc);
    }

    /// <summary>The line as the editor posts it back, unchanged.</summary>
    private static EstimateLineInput Saved() => new(
        "new_part",
        GuideCode: null,
        "Door skin",
        WorkUnits: 2.5m,
        Price: 240.00m,
        Unpriced: false,
        "P-1234",
        Betterment: null,
        Status: null,
        EvidenceLabel: null,
        Justification: null,
        PaintWorkUnits: 1.5m,
        Quantity: 1,
        Materials: 12.50m);

    /// <summary>The same line as the save loaded it, already amended once.</summary>
    private static CaseEstimateLineRecord Loaded() => new(
        Guid.NewGuid(),
        1,
        "new_part",
        GuideCode: "283",
        "Door skin",
        WorkUnits: 2.5m,
        Price: 240.00m,
        Unpriced: false,
        "P-1234",
        Betterment: null,
        Status: "confirmed",
        EvidenceLabel: "official",
        Justification: null,
        ActorKind.Staff,
        "engineer-recorded",
        PriorAmendedAtUtc,
        ConfirmedBy: null,
        ConfirmedAtUtc: null,
        PaintWorkUnits: 1.5m,
        Quantity: 1,
        Materials: 12.50m,
        AmendedBy: PriorActor,
        AmendedAtUtc: PriorAmendedAtUtc);
}
