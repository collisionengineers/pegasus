using System.Text.Json;
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

    /// <summary>The cut list with its own criteria (7 September 2026, glasses7 #356).</summary>
    private static readonly Lazy<GlassEditorPartList> First = new(() => ListWith("part_list.html"));

    /// <summary>The cut list with the criteria served after the 08:06 answers (glasses7 #648).</summary>
    private static readonly Lazy<GlassEditorPartList> Later = new(() => ListWith("part_list_after_answers.html"));

    /// <summary>The editor's own outcomes for rows 0, 274 and 682 in both states (<c>equipment_answers.json</c>).</summary>
    private static readonly Lazy<JsonElement> EditorsOutcomes = new(() =>
    {
        using var document = JsonDocument.Parse(GlassEditorFixture.Text(
            GlassEditorFixture.Page("equipmentAnswers").GetProperty("file").GetString()!));
        return document.RootElement.Clone();
    });

    /// <summary>
    /// Criteria for the made-up equipment blocks, in the list's own format:
    /// Primed, Keyless Entry and Start and Front and Rear Park Assistant
    /// System as the SEAT's list gives them (the last set here), Fog Lamp and
    /// LED Fog Lamps as the BMW's are (one radio group), a build date the
    /// vehicle fixes, and nine made-up options with no group.
    /// </summary>
    private static readonly string[] MadeUpCriteria =
    [
        "800|pN|0|_|0|Primed|0|0|241|241|0|",
        "85161|pNIg|0|_|0|Keyless Entry and Start|0|0|142|142|0|",
        "24119|tNIgp|1|_|0|Front and Rear Park Assistant System|0|0|144|144|1|",
        "12000|tNIgp|0|_|0|Fog Lamp|0|0|220|180|1|",
        "12007|tNIgp|0|_|0|LED Fog Lamps|0|0|220|181|1|",
        "740|pNdf|1|_|0|from 03.2019|0|0|50|101|0|",
        "901|tNg|0|_|0|Option 1|0|0|0|0|0|",
        "902|tNg|0|_|0|Option 2|0|0|0|0|0|",
        "903|tNg|0|_|0|Option 3|0|0|0|0|0|",
        "904|tNg|0|_|0|Option 4|0|0|0|0|0|",
        "905|tNg|0|_|0|Option 5|0|0|0|0|0|",
        "906|tNg|0|_|0|Option 6|0|0|0|0|0|",
        "907|tNg|0|_|0|Option 7|0|0|0|0|0|",
        "908|tNg|0|_|0|Option 8|0|0|0|0|0|",
        "909|tNg|0|_|0|Option 9|0|0|0|0|0|",
    ];

    /// <summary>
    /// Blocks made for the equipment tests in the list's row format: a part
    /// whose every part row needs Primed, which Glass's does not keep, so it
    /// is greyed (300); Replace hours by two fog lamps of one radio group
    /// (310); a part shown on its drawing by the park assistant the vehicle
    /// has, whose own show field greys that option (320); one shown only with
    /// keyless entry, which the vehicle lacks (330); a part that asks
    /// nothing (340); and Replace hours by nine options, 512 answers (350).
    /// </summary>
    private static readonly string[] MadeUpEquipmentRows =
    [
        "300|pbcd||D3|1|9000300|800|NUM300|40.00|ERAEI |||||||Panel F|||",
        "301|p|||||800_85161|NUM301|45.00|ERAEI ||||||||||",
        "302|t|||||||0.50|ER||||||||||",
        "310|pbcd||D3|2|9000310||NUM310|50.00|ERAEI |||||||Panel G|||",
        "311|t|||||||0.60|ER||||||||||",
        "312|t|||||12000||0.70|ER||||||||||",
        "313|t|||||12007||0.80|ER||||||||||",
        "320|pbcd||D3|3|9000320||NUM320|60.00|ERAEI |24119||||||Panel H|||",
        "321|t|||||||0.90|ER||||||||||",
        "322|t|||||12000||1.00|ER||||||||||",
        "323|t|||||24119||1.10|ER||||||||||",
        "330|pbcd||D3|4|9000330||NUM330|70.00|ERAEI |85161||||||Panel I|||",
        "331|t|||||||1.20|ER||||||||||",
        "332|t|||||12000||1.30|ER||||||||||",
        "340|pbcd||D3|5|9000340||NUM340|80.00|ERAEI |||||||Panel J|||",
        "341|t|||||||1.40|ER||||||||||",
        "350|pbcd||D3|6|9000350||NUM350|90.00|ERAEI |||||||Panel K|||",
        "351|t|||||||1.50|ER||||||||||",
        "352|t|||||901||1.51|ER||||||||||",
        "353|t|||||902||1.52|ER||||||||||",
        "354|t|||||903||1.53|ER||||||||||",
        "355|t|||||904||1.54|ER||||||||||",
        "356|t|||||905||1.55|ER||||||||||",
        "357|t|||||906||1.56|ER||||||||||",
        "358|t|||||907||1.57|ER||||||||||",
        "359|t|||||908||1.58|ER||||||||||",
        "360|t|||||909||1.59|ER||||||||||",
    ];

    private static readonly Lazy<GlassEditorPartList> MadeUpEquipment = new(() =>
    {
        var page = "<script>\n"
            + string.Join('\n', MadeUpEquipmentRows.Select(row => "partlist.addPart(\"" + row + "\");")) + "\n"
            + string.Join('\n', MadeUpCriteria.Select(line => "partlist.addCrit(\"" + line + "\");")) + "\n</script>\n";
        return new GlassEditorPartList(
            GlassEditorPages.PartList(page) ?? throw new InvalidOperationException("The made-up list holds no row."),
            GlassEditorPages.Criteria(page),
            null);
    });

    /// <summary>The made-up blocks' maps, in the shape Glass's serves them.</summary>
    private static readonly Dictionary<string, IReadOnlyList<IReadOnlyList<string>>> MadeUpMaps = new(StringComparer.Ordinal)
    {
        ["300"] = [new List<string> { "I", "9000300" }, new List<string> { "e|0|300", "e|800_85161|301" }, new List<string>(), new List<string>()],
        ["310"] = [new List<string> { "I", "9000310" }, new List<string>(), new List<string> { "ER", "e|0|311", "e|12000|312", "e|12007|313" }, new List<string>()],
        ["320"] = [new List<string> { "I", "9000320" }, new List<string>(), new List<string> { "ER", "e|0|321", "e|12000|322", "e|24119|323" }, new List<string>()],
        ["350"] =
        [
            new List<string> { "I", "9000350" },
            new List<string>(),
            new List<string> { "ER", "e|0|351", "e|901|352", "e|902|353", "e|903|354", "e|904|355", "e|905|356", "e|906|357", "e|907|358", "e|908|359", "e|909|360" },
            new List<string>(),
        ],
    };

    private static readonly string[] FogLamp = ["12000"];

    private static readonly string[] NoTicks = [];

    private static readonly string[] ParkAndFog = ["12000", "24119"];

    private static readonly string[] AllThree = ["12000", "24119", "90109"];

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

    public static TheoryData<string, string> EditorsHeads() => new()
    {
        { "first", "0" },
        { "first", "274" },
        { "first", "682" },
        { "later", "0" },
        { "later", "274" },
        { "later", "682" },
    };

    /// <summary>
    /// The equipment question and every answer the editor's own list code
    /// gave for rows 0, 274 and 682 (R1206), with the vehicle's criteria as
    /// first served and after the answers of 08:06: the options, the fixed
    /// criteria, the greyed and ticked options, and for each answer the row
    /// pair and the value stored for the vehicle.
    /// </summary>
    [Theory]
    [MemberData(nameof(EditorsHeads))]
    public void TheQuestionAndEveryAnswerAreTheEditorsOwn(string state, string head)
    {
        var expected = EditorsOutcomes.Value.GetProperty(state).GetProperty(head);
        var list = state == "first" ? First.Value : Later.Value;
        var at = Index(head);

        var question = GlassEditorRows.Question(list, at);

        Assert.NotNull(question);
        Assert.Equal(Sorted(expected, "options"), question.Asked.Options.Select(option => option.Code).Order(StringComparer.Ordinal));
        Assert.Equal(Sorted(expected, "frozen"), question.Frozen.Order(StringComparer.Ordinal));
        Assert.Equal(
            Sorted(expected, "greyed"),
            question.Asked.Options.Where(option => option.Greyed).Select(option => option.Code).Order(StringComparer.Ordinal));
        Assert.Equal(
            Sorted(expected, "ticked"),
            question.Asked.Options.Where(option => option.Ticked).Select(option => option.Code).Order(StringComparer.Ordinal));
        var answers = expected.GetProperty("answers").EnumerateArray().ToList();
        Assert.NotEmpty(answers);
        foreach (var answer in answers)
        {
            var ticked = answer.GetProperty("ticked").GetString()!;
            var answered = GlassEditorRows.Answer(
                list, at, question, CapturedMap(head),
                ticked.Length == 0 ? NoTicks : ticked.Split('_'));

            Assert.Equal(answer.GetProperty("pair").GetString(), answered.Rows);
            Assert.Equal(answer.GetProperty("globcrit").GetString(), answered.Rows is null ? null : answered.Store);
        }
    }

    /// <summary>
    /// The values an answer changes are the options Glass's keeps whose new
    /// tick is not the vehicle's: as first served the lining's fog lamp is
    /// added; after the answers of 08:06 an empty answer unticks all three.
    /// </summary>
    [Fact]
    public void AnAnswerNamesTheStoredValuesItChanges()
    {
        var lining = Index("0");
        var first = GlassEditorRows.Question(First.Value, lining)!;
        var later = GlassEditorRows.Question(Later.Value, lining)!;

        var fog = GlassEditorRows.Answer(First.Value, lining, first, CapturedMap("0"), FogLamp);
        var none = GlassEditorRows.Answer(Later.Value, lining, later, CapturedMap("0"), NoTicks);

        Assert.Equal(new GlassEquipmentChange("12000", true), Assert.Single(fog.Changes));
        Assert.Equal(AllThree, none.Changes.Select(change => change.Code));
        Assert.All(none.Changes, change => Assert.False(change.Ticked));
        Assert.Equal("0|1", none.Rows);
        Assert.Equal("12000|0|1|24119|0|1|90109|0|1", none.Store);
    }

    /// <summary>
    /// An answer the dialog would not take, or that leads to no part, is
    /// refused with its own code before anything is sent (R1206): a code
    /// named twice, one that is no option (a fixed build date is none), a
    /// greyed option, two options of one radio group, and an answer whose
    /// part criteria no part row has.
    /// </summary>
    [Theory]
    [InlineData("0", "12000,12000", GlassAutomationPositionPolicy.EquipmentRepeatedCode)]
    [InlineData("0", "99999", GlassAutomationPositionPolicy.EquipmentUnknownCode)]
    [InlineData("682", "740", GlassAutomationPositionPolicy.EquipmentUnknownCode)]
    [InlineData("300", "800", GlassAutomationPositionPolicy.EquipmentGreyedCode)]
    [InlineData("310", "12000,12007", GlassAutomationPositionPolicy.EquipmentSameGroupCode)]
    [InlineData("300", "85161", GlassAutomationPositionPolicy.EquipmentNoPartCode)]
    public void AnAnswerTheDialogWouldNotTakeIsRefused(string head, string ticked, string code)
    {
        var madeUp = MadeUpMaps.TryGetValue(head, out var madeUpMap);
        var list = madeUp ? MadeUpEquipment.Value : First.Value;
        var at = Index(list.Rows, head);
        var question = GlassEditorRows.Question(list, at);
        Assert.NotNull(question);

        var answered = GlassEditorRows.Answer(
            list, at, question, madeUpMap ?? CapturedMap(head), ticked.Split(','));

        Assert.Null(answered.Rows);
        Assert.Null(answered.Store);
        Assert.Equal(code, answered.ReasonCode);
    }

    /// <summary>Another row's map chooses nothing for an answer.</summary>
    [Fact]
    public void AnAnswerOnAnotherRowsMapChoosesNothing()
    {
        var lining = Index("0");
        var question = GlassEditorRows.Question(First.Value, lining)!;

        var answered = GlassEditorRows.Answer(First.Value, lining, question, CapturedMap("274"), NoTicks);

        Assert.Null(answered.Rows);
        Assert.Equal(GlassFailure.EditorAnswer, answered.ReasonCode);
    }

    /// <summary>
    /// The greyed options are the dialog's: an option every part row needs
    /// that Glass's does not keep, and an option the head's own show field
    /// names. A head shown on its drawing by equipment the vehicle has is
    /// asked; one the vehicle's equipment hides is refused; a head whose
    /// block names no criterion asks nothing.
    /// </summary>
    [Fact]
    public void AShownHeadIsAskedAHiddenOneIsRefusedAndAPlainOneAsksNothing()
    {
        var list = MadeUpEquipment.Value;

        var primed = GlassEditorRows.Question(list, Index(list.Rows, "300"))!;
        var shown = GlassEditorRows.Question(list, Index(list.Rows, "320"))!;

        Assert.True(Assert.Single(primed.Asked.Options, option => option.Code == "800").Greyed);
        Assert.False(Assert.Single(primed.Asked.Options, option => option.Code == "85161").Greyed);
        Assert.Null(GlassEditorRows.AskedRefusal(list, Index(list.Rows, "320")));
        Assert.Equal(ParkAndFog, shown.Asked.Options.Select(option => option.Code));
        Assert.True(Assert.Single(shown.Asked.Options, option => option.Code == "24119").Greyed);
        Assert.True(Assert.Single(shown.Asked.Options, option => option.Code == "24119").Ticked);
        Assert.Equal("320|322", GlassEditorRows.Answer(list, Index(list.Rows, "320"), shown, MadeUpMaps["320"], FogLamp).Rows);
        Assert.Equal(Equipment, GlassEditorRows.AskedRefusal(list, Index(list.Rows, "330")));
        Assert.Null(GlassEditorRows.Question(list, Index(list.Rows, "340")));
        Assert.Null(GlassEditorRows.Question(First.Value, Index("17")));
    }

    /// <summary>
    /// The options are passed through as Glass's names them, its radio
    /// groups kept, and an option another position uses carries the page's
    /// own note.
    /// </summary>
    [Fact]
    public void TheOptionsAreGlasssOwn()
    {
        var question = GlassEditorRows.Question(Later.Value, Index("0"))!;

        var park = Assert.Single(question.Asked.Options, option => option.Code == "24119");
        Assert.Equal("Front and Rear Park Assistant System", park.Name);
        Assert.Equal("144", park.Group);
        Assert.Equal("Criteria affects other parts!", park.Note);
        Assert.Null(Assert.Single(question.Asked.Options, option => option.Code == "12000").Group);
        Assert.Null(Assert.Single(GlassEditorRows.Question(First.Value, Index("0"))!.Asked.Options, option => option.Code == "24119").Note);
    }

    /// <summary>
    /// A row asked for alone answers every allowed answer that leads to a
    /// part, with the part list's figures; above the cap it answers the
    /// map's own part and Replace hours entries instead.
    /// </summary>
    [Fact]
    public void ARowsAnswersAreListedUpToTheCap()
    {
        var lining = Index("0");
        var question = GlassEditorRows.Question(First.Value, lining)!;
        var many = MadeUpEquipment.Value;
        var panel = Index(many.Rows, "350");

        var listed = GlassEditorRows.WithAnswers(First.Value, lining, question, CapturedMap("0"));
        var capped = GlassEditorRows.WithAnswers(many, panel, GlassEditorRows.Question(many, panel)!, MadeUpMaps["350"]);

        Assert.NotNull(listed.Answers);
        Assert.Equal(8, listed.Answers.Count);
        var all = Assert.Single(listed.Answers, answer => answer.Ticked.SequenceEqual(AllThree));
        Assert.Equal(new GlassEquipmentAnswer(all.Ticked, "0|5", "6F0807221DGRU", 379.00m, 1.80m), all);
        Assert.Null(listed.MapParts);
        Assert.Null(capped.Answers);
        Assert.NotNull(capped.MapHours);
        Assert.Equal(10, capped.MapHours.Count);
        Assert.Equal(1.59m, capped.MapHours[^1].Hours);
        Assert.Equal("360", capped.MapHours[^1].Rows);
        Assert.NotNull(capped.MapParts);
        Assert.Empty(capped.MapParts);
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

    private static GlassEditorPartList ListWith(string criteriaFile) =>
        new(Parts,
            GlassEditorPages.Criteria(GlassEditorFixture.Text(criteriaFile)),
            GlassEditorPages.ReferencedNote(GlassEditorFixture.Text("part_list.html")));

    /// <summary>A row's map as Glass's served it (glasses7 #502, #577, #575).</summary>
    private static IReadOnlyList<IReadOnlyList<string>> CapturedMap(string head) =>
        GlassEditorPages.EquipmentMap(GlassEditorFixture.Text("equipment_" + head + ".html"))
            ?? throw new InvalidOperationException("The fixture has no map for row " + head + ".");

    private static IOrderedEnumerable<string> Sorted(JsonElement element, string name) =>
        element.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).Order(StringComparer.Ordinal);

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
