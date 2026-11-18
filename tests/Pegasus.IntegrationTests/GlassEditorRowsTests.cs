using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Which row of Glass's part list a part line is placed by (R1172), on blocks
/// cut whole from the part list a browser was served on 7 September 2026
/// (<c>GlassEditorPages/part_list.html</c>). Every expected value is what the
/// rule's JavaScript reference decided on the whole list, where the editor's
/// own list code, run offline with the row's side, gave the same row pair
/// with no equipment question open for every line the rule calls certain.
/// For Repair and remove-and-refit the reference was judged the same way,
/// and the editor's own choice of the operation's time rows agreed too.
/// </summary>
public sealed class GlassEditorRowsTests
{
    private const string Replace = GlassEditorRows.Replace;

    private const string Repair = GlassEditorRows.Repair;

    private const string Refit = GlassEditorRows.RemoveAndRefit;

    private const string Equipment = GlassEstimateBuildPolicy.EquipmentCode;

    private const string NoRow = GlassEstimateBuildPolicy.NoRowCode;

    private const string Ambiguous = GlassEstimateBuildPolicy.AmbiguousRowCode;

    /// <summary>The heads of the left side support, one on each of the front's two drawings.</summary>
    private static readonly string[] TwoDrawings = ["17", "864"];

    /// <summary>Row 17's map with a variant that needs equipment 12100 in its part group.</summary>
    private static readonly IReadOnlyList<IReadOnlyList<string>> SupportWithAVariant =
    [
        new List<string> { "I", "1016101" },
        new List<string> { "e|0|17", "e|12100|99" },
        new List<string>(),
        new List<string>(),
    ];

    /// <summary>Row 17's map naming its Replace time row as needing no equipment.</summary>
    private static readonly IReadOnlyList<IReadOnlyList<string>> SupportWithItsTimeRow =
    [
        new List<string> { "I", "1016101" },
        new List<string>(),
        new List<string> { "ER", "e|0|18" },
        new List<string>(),
    ];

    /// <summary>A map that is not row 17's: another part code.</summary>
    private static readonly IReadOnlyList<IReadOnlyList<string>> AnotherRowsMap =
    [
        new List<string> { "I", "2020001" },
        new List<string>(),
        new List<string>(),
        new List<string>(),
    ];

    /// <summary>Row 17's map whose remove-and-refit group has a variant that needs equipment 12100.</summary>
    private static readonly IReadOnlyList<IReadOnlyList<string>> SupportWithARefitVariant =
    [
        new List<string> { "I", "1016101" },
        new List<string>(),
        new List<string>(),
        new List<string> { "AE", "e|0|19", "e|12100|99" },
    ];

    /// <summary>Row 17's map naming its Replace and its remove-and-refit time rows as needing no equipment.</summary>
    private static readonly IReadOnlyList<IReadOnlyList<string>> SupportWithBothTimeRows =
    [
        new List<string> { "I", "1016101" },
        new List<string>(),
        new List<string> { "ER", "e|0|18" },
        new List<string> { "AE", "e|0|19" },
    ];

    /// <summary>
    /// Four blocks made for these tests in the list's own row format, each
    /// needing no equipment: a part that offers no Repair (100); one with two
    /// remove-and-refit time rows (110); one with a Repair time row of its
    /// own (120); and a part that offers only remove-and-refit (130), as the
    /// list's "Strip &amp; Set-Up for Paint" rows do.
    /// </summary>
    private static readonly string[] MadeUpRows =
    [
        "100|pbcd||D1|1|9000100||NUM100|10.00|ERAE|||||||Panel A|||",
        "101|t|||||||0.50|ER||||||||||",
        "102|t|||||||0.40|AE||||||||||",
        "110|pbcd||D1|2|9000110||NUM110|20.00|ERAEI |||||||Panel B|||",
        "111|t|||||||0.60|ER||||||||||",
        "112|t|||||||0.30|AE||||||||||",
        "113|t|||||||0.35|AE||||||||||",
        "120|pbcd||D1|3|9000120||NUM120|30.00|ERAEI |||||||Panel C|||",
        "121|t|||||||0.70|ER||||||||||",
        "122|t|||||||0.50|AE||||||||||",
        "123|t|||||||1.10|I ||||||||||",
        "130|pbcd||D1|4|9000130||KET|0.00|AE|||||||Strip|||",
        "131|t|||||||0.90|ER||||||||||",
        "132|t|||||||0.90|AE||||||||||",
    ];

    private static readonly Lazy<IReadOnlyList<GlassEditorPartRow>> MadeUp = new(() =>
        GlassEditorPages.PartList("<script>\n" + string.Join('\n', MadeUpRows.Select(row => "partlist.addPart(\"" + row + "\");")) + "\n</script>\n")
        ?? throw new InvalidOperationException("The made-up part list holds no row."));

    private static readonly Lazy<IReadOnlyList<GlassEditorPartRow>> Fixture = new(() =>
        GlassEditorPages.PartList(GlassEditorFixture.Text(
            GlassEditorFixture.Page("partList").GetProperty("file").GetString()!))
        ?? throw new InvalidOperationException("The part list fixture holds no row."));

    private static IReadOnlyList<GlassEditorPartRow> Parts => Fixture.Value;

    public static TheoryData<string, string?, string?, string?> ByPartCodeAndSide() => new()
    {
        // Certain: one plain part row and one plain Replace time row, on two drawings alike.
        { "1016101", "L", "17|18", null },
        { "1016102", "R", "20|21", null },
        { "1011502", "R", "11|12", null },
        { "1011100", null, "8|9", null },
        // Its Replace time rows need equipment (12000, 24119, 90109).
        { "1010800", null, null, Equipment },
        // Variant part rows by equipment.
        { "3080600", null, null, Equipment },
        { "2025851", "L", null, Equipment },
        { "2025852", "R", null, Equipment },
        // Shown on its drawing only with equipment 24119.
        { "1019011", "L", null, Equipment },
        // Offers no Replace.
        { "7033020", null, null, NoRow },
        // A part with sides is not found by a line that names none, nor by the other side.
        { "1016101", null, null, NoRow },
        { "1016101", "R", null, NoRow },
    };

    public static TheoryData<string, string?, string?> ByPartNumber() => new()
    {
        { "6F0807248B", "8|9", null },
        { "6F0807183A", "17|18", null },
        // A variant row's number: only an equipment answer chooses it.
        { "5G0837205NGRU", null, Equipment },
        // Two different parts (6092100 and 6098100) carry the same number.
        { "03F919501B", null, Ambiguous },
        { "NOSUCHNUMBER", null, NoRow },
    };

    /// <summary>
    /// Repair and remove-and-refit lines are placed by the pair Replace would
    /// be (the editor asks for a part's dialog by one pair whatever the
    /// operation), only where Replace's row is certain (R1172).
    /// </summary>
    public static TheoryData<string, string?, string, string?, string?> ByOperation() => new()
    {
        { "1016101", "L", Refit, "17|18", null },
        { "1016102", "R", Repair, "20|21", null },
        { "1011502", "R", Refit, "11|12", null },
        { "1011100", null, Repair, "8|9", null },
        { "1011100", null, Refit, "8|9", null },
        { "6092100", null, Refit, "3246|3247", null },
        { "1010800", null, Repair, null, Equipment },
        { "2025851", "L", Refit, null, Equipment },
        { "1019011", "L", Repair, null, Equipment },
        { "7033020", null, Refit, null, NoRow },
        { "1016101", null, Repair, null, NoRow },
    };

    /// <summary>The made-up blocks: each operation's own conditions.</summary>
    public static TheoryData<string, string, string?, string?, string?> OnMadeUpBlocks() => new()
    {
        // Offers no Repair: only Replace and remove-and-refit are placed.
        { "100", Repair, null, NoRow, null },
        { "100", Refit, "100|101", null, "NUM100|10.00|0.40" },
        { "100", Replace, "100|101", null, "NUM100|10.00|0.50" },
        // Two remove-and-refit time rows: the dialog's choice, which Pegasus cannot see.
        { "110", Refit, null, Ambiguous, null },
        { "110", Repair, "110|111", null, "NUM110|20.00|" },
        // A Repair time row of its own is the Repair hours.
        { "120", Repair, "120|121", null, "NUM120|30.00|1.10" },
        { "120", Refit, "120|121", null, "NUM120|30.00|0.50" },
        // Offers no Replace: its pair would be another time row.
        { "130", Refit, null, NoRow, null },
        { "130", Repair, null, NoRow, null },
    };

    [Theory]
    [MemberData(nameof(ByPartCodeAndSide))]
    public void APartIsPlacedByCodeAndSideOnlyWhenItsRowIsCertain(
        string code, string? side, string? rows, string? reason)
    {
        var choice = Resolve(GlassEstimateBuildKeyKind.PartCode, code, side);

        Assert.Equal(rows, choice.Rows);
        Assert.Equal(reason, choice.ReasonCode);
    }

    [Theory]
    [MemberData(nameof(ByPartNumber))]
    public void APartIsPlacedByItsNumberOnlyWhenItsRowIsCertain(string number, string? rows, string? reason)
    {
        var choice = Resolve(GlassEstimateBuildKeyKind.PartNumber, number, null);

        Assert.Equal(rows, choice.Rows);
        Assert.Equal(reason, choice.ReasonCode);
    }

    [Theory]
    [MemberData(nameof(ByOperation))]
    public void ARepairOrRefitIsPlacedByReplacesRowsOnlyWhenTheyAreCertain(
        string code, string? side, string operation, string? rows, string? reason)
    {
        var choice = Resolve(GlassEstimateBuildKeyKind.PartCode, code, side, operation);

        Assert.Equal(rows, choice.Rows);
        Assert.Equal(reason, choice.ReasonCode);
    }

    [Theory]
    [MemberData(nameof(OnMadeUpBlocks))]
    public void EachOperationHasAtMostOneTimeRowAndItsHeadOffersItAndReplace(
        string head, string operation, string? rows, string? reason, string? same)
    {
        var list = MadeUp.Value;
        var at = Index(list, head);

        var choice = GlassEditorRows.Decide(list, at, NoVariants(list[at]), operation);

        Assert.Equal(rows, choice.Rows);
        Assert.Equal(reason, choice.ReasonCode);
        Assert.Equal(same, choice.Same);
    }

    [Fact]
    public void ARefitVariantInTheMapLeavesOffRepairAndRefitButNotReplace()
    {
        var support = Index("17");

        Assert.Equal(Equipment, GlassEditorRows.Decide(Parts, support, SupportWithARefitVariant, Refit).ReasonCode);
        Assert.Equal(Equipment, GlassEditorRows.Decide(Parts, support, SupportWithARefitVariant, Repair).ReasonCode);
        Assert.Equal("17|18", GlassEditorRows.Decide(Parts, support, SupportWithARefitVariant).Rows);
    }

    [Fact]
    public void TheMapsRefitEntryNamesTheRefitHours()
    {
        var choice = GlassEditorRows.Decide(Parts, Index("17"), SupportWithBothTimeRows, Refit);

        Assert.Equal("17|18", choice.Rows);
        Assert.Equal("6F0807183A|12.55|0.90", choice.Same);
    }

    /// <summary>Glass's lists one part on two drawings: the first in the list's order is placed.</summary>
    [Fact]
    public void APartOnTwoDrawingsIsPlacedByTheFirst()
    {
        var (heads, reason) = GlassEditorRows.Find(Parts, GlassEstimateBuildKeyKind.PartCode, "1016101", "L");

        Assert.Null(reason);
        Assert.Equal(TwoDrawings, heads.Select(head => Parts[head].Id));
        var choice = Resolve(GlassEstimateBuildKeyKind.PartCode, "1016101", "L");
        Assert.Equal("17", choice.Head?.Id);
        Assert.Equal("L", choice.Head?.Side);
        Assert.False(choice.Head?.Paintable);
    }

    /// <summary>The captured map of a handle with variants, read on the same vehicle, needs equipment too.</summary>
    [Fact]
    public void TheCapturedMapOfAPartWithVariantsWaitsOnEquipment()
    {
        var map = GlassEditorPages.EquipmentMap(GlassEditorFixture.Text(
            GlassEditorFixture.Page("equipment274").GetProperty("file").GetString()!));
        var handle = Index("274");

        Assert.NotNull(map);
        Assert.Equal(Equipment, GlassEditorRows.ListRefusal(Parts, handle));
        Assert.Equal(Equipment, GlassEditorRows.Decide(Parts, handle, map).ReasonCode);
    }

    [Fact]
    public void AMapWithAVariantLeavesOffAPartTheListAloneWouldPlace()
    {
        var support = Index("17");

        Assert.Null(GlassEditorRows.ListRefusal(Parts, support));
        Assert.Equal(Equipment, GlassEditorRows.Decide(Parts, support, SupportWithAVariant).ReasonCode);
    }

    [Fact]
    public void TheMapsReplaceEntryNamesTheTimeRow()
    {
        var choice = GlassEditorRows.Decide(Parts, Index("17"), SupportWithItsTimeRow);

        Assert.Equal("17|18", choice.Rows);
        Assert.Null(choice.ReasonCode);
    }

    [Fact]
    public void AMapOfAnotherRowChoosesNothing()
    {
        var choice = GlassEditorRows.Decide(Parts, Index("17"), AnotherRowsMap);

        Assert.Null(choice.Rows);
        Assert.Equal(GlassFailure.EditorAnswer, choice.ReasonCode);
    }

    /// <summary>
    /// The rule's whole decision for one line, as the build makes it: the
    /// heads the line is found by, each with the map Glass's serves for a row
    /// with no variants (the shape the program read for row 753 on 18
    /// November 2026), and the choice among them.
    /// </summary>
    private static GlassEditorRowChoice Resolve(
        GlassEstimateBuildKeyKind kind, string key, string? side, string operation = Replace)
    {
        var (heads, reason) = GlassEditorRows.Find(Parts, kind, key, side);
        if (reason is not null)
        {
            return GlassEditorRowChoice.Refused(reason);
        }

        var decided = new List<GlassEditorRowChoice>();
        foreach (var head in heads)
        {
            var choice = GlassEditorRows.Decide(Parts, head, NoVariants(Parts[head]), operation);
            decided.Add(choice);
            if (choice.Rows is null)
            {
                break;
            }
        }

        return GlassEditorRows.Choose(decided);
    }

    private static List<IReadOnlyList<string>> NoVariants(GlassEditorPartRow row) =>
        [new List<string> { "I", row.Code }, new List<string>(), new List<string>(), new List<string>()];

    private static int Index(string id) => Index(Parts, id);

    private static int Index(IReadOnlyList<GlassEditorPartRow> list, string id)
    {
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index].Id == id)
            {
                return index;
            }
        }

        throw new InvalidOperationException("The part list has no row " + id + ".");
    }
}
