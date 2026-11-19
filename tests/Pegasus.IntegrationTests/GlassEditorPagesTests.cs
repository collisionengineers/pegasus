using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The readers of Glass's editor pages, the addresses the editor client asks
/// for and the browser's form encoding, against pages cut from real browser
/// captures (<c>GlassEditorPages/manifest.json</c>; every key and token in
/// them is made up), and the form reader's rules on small pages of its own.
/// </summary>
public sealed class GlassEditorPagesTests
{
    private static readonly byte[] HighBytes = [0x80, 0x41, 0xA3, 0x81, 0x9F];

    private static readonly int[] Equipment821Shape = [2, 8, 0, 0];

    private static readonly string[] CalculationTrades = ["Body", "Mechanical", "Auxiliary work", "Paint"];

    private static readonly int[] Equipment193Shape = [2, 4, 9, 9];

    private static readonly string[] SpecialGroupCodes =
        ["DS-AUXWORK-702", "SS-BULPS", "SS-FREEPAINT", "SS-FREEPART", "SS-SPECMET", "SS-SPECPLA", "SS-TYRES"];

    private static readonly string[] SubmittedFields =
    [
        "t1=",
        "h=a \"b\" <c>",
        "c1=on",
        "r=2",
        "ro=kept",
        "s1=b",
        "s2=First one",
        "s3=y",
        "multi=line one\nline two",
        "go=Go",
        "ta=hello & bye",
        "upper=V",
        "after=1",
    ];

    [Fact]
    public void TheEstimateListsItsPositionsAndCodesWithoutTheLeadingBlank()
    {
        var page = GlassEditorFixture.Page("selected");

        var estimate = GlassEditorPages.Selected(GlassEditorFixture.Text(page.GetProperty("file").GetString()!));

        Assert.True(estimate.Found);
        Assert.Equal(GlassEditorFixture.Strings(page, "positions"), estimate.Positions);
        Assert.Equal(GlassEditorFixture.Strings(page, "codes"), estimate.Codes);
    }

    [Fact]
    public void APageWithoutTheListIsNotAnEstimate() =>
        Assert.False(GlassEditorPages.Selected("<html><body>Please wait</body></html>").Found);

    /// <summary>
    /// The estimate's page holds Glass's running totals beside the
    /// positions, each with Glass's own label, in its order (R1206).
    /// </summary>
    [Fact]
    public void TheEstimateHoldsGlasssRunningTotalsWithItsOwnLabels()
    {
        var page = GlassEditorFixture.Page("selected");

        var estimate = GlassEditorPages.Selected(GlassEditorFixture.Text(page.GetProperty("file").GetString()!));

        Assert.NotNull(estimate.Totals);
        Assert.Equal(ExpectedTotals(page), estimate.Totals.Select(total => (total.Label, total.Amount)));
        Assert.Equal(9594.14m, Assert.Single(estimate.Totals, total => total.Label == "Amount due (£)").Amount);
    }

    /// <summary>An empty estimate lists no position and the amount due alone, as Glass's gives it.</summary>
    [Fact]
    public void AnEmptyEstimateListsTheAmountDueAlone()
    {
        var page = GlassEditorFixture.Page("selectedEmpty");

        var estimate = GlassEditorPages.Selected(GlassEditorFixture.Text(page.GetProperty("file").GetString()!));

        Assert.True(estimate.Found);
        Assert.Empty(estimate.Positions);
        var due = Assert.Single(estimate.Totals!);
        Assert.Equal("Amount due (£)", due.Label);
        Assert.Equal(0m, due.Amount);
    }

    /// <summary>No total is guessed: lists that are missing, differ in length or hold a figure that is not a number read as none.</summary>
    [Theory]
    [InlineData("<script>\nvar Partids = [ '' ];\n</script>")]
    [InlineData("<script>\nvar CalcToolTipTexts = [ 'Parts ', 'Body ' ];\nvar CalcToolTipNums = [ '1.00' ];\n</script>")]
    [InlineData("<script>\nvar CalcToolTipTexts = [ 'Parts ' ];\nvar CalcToolTipNums = [ 'n/a' ];\n</script>")]
    public void TotalsThatCannotBeReadAreNone(string html) => Assert.Null(GlassEditorPages.Totals(html));

    /// <summary>
    /// A part with paint: its operation, and the price, hours and part
    /// number Glass's holds for that operation, then its paint position's
    /// price and labour time, as the calculation report shows them for the
    /// same bonnet (697.10 and 2.60).
    /// </summary>
    [Fact]
    public void APartsFiguresAreReadFromItsRecordWithItsPaint()
    {
        var figures = Figures("position");

        Assert.Equal("1060000", figures.PartCode);
        Assert.Equal("Bonnet", figures.Description);
        Assert.Equal("ER", figures.Operation);
        Assert.Equal(412.12m, figures.Price);
        Assert.Equal(0.80m, figures.Hours);
        Assert.Null(figures.Side);
        Assert.Equal("41007290942", figures.PartNumber);
        Assert.NotNull(figures.Paint);
        Assert.Equal("L350899.20.Lxx1640.Lxx1640", figures.Paint.PositionId);
        Assert.Equal("Bonnet", figures.Paint.Description);
        Assert.Equal(697.10m, figures.Paint.Price);
        Assert.Equal(2.60m, figures.Paint.LabourTime);
    }

    [Fact]
    public void ALeftHandPartIsReadOnItsSideWithNoPaint()
    {
        var figures = Figures("positionLeft");

        Assert.Equal("L", figures.Side);
        Assert.Equal("1012401", figures.PartCode);
        Assert.Equal("Front Bumper Side Reinforcement", figures.Description);
        Assert.Equal(38.68m, figures.Price);
        Assert.Equal(2.20m, figures.Hours);
        Assert.Equal("51117266193", figures.PartNumber);
        Assert.Null(figures.Paint);
    }

    /// <summary>A paint position on its own has no operation, price or hours of its own, only its paint.</summary>
    [Fact]
    public void APaintPositionOnItsOwnIsReadAsItsPaint()
    {
        var figures = Figures("positionPaint");

        Assert.Null(figures.Operation);
        Assert.Null(figures.Price);
        Assert.Null(figures.Hours);
        Assert.Equal("Tailgate (with Window Frame)", figures.Description);
        Assert.NotNull(figures.Paint);
        Assert.Equal(342.40m, figures.Paint.Price);
        Assert.Equal(1.50m, figures.Paint.LabourTime);
    }

    /// <summary>
    /// A special operation, read from the record its own dialog carries
    /// (no capture holds mcode.php for one): its fixed price for its
    /// operation, the hours it gives (none), and no part number or side.
    /// </summary>
    [Fact]
    public void ASpecialOperationIsReadWithItsOwnFiguresAndNoPartNumber()
    {
        var figures = Figures("positionSpecial");

        Assert.Equal(".Assessment Damage Appraisal Charge", figures.Description);
        Assert.Equal("ZK", figures.Operation);
        Assert.Equal(176.96m, figures.Price);
        Assert.Equal(0m, figures.Hours);
        Assert.Null(figures.PartNumber);
        Assert.Null(figures.Side);
        Assert.Null(figures.Paint);
    }

    /// <summary>An answer that is not the record of the position asked for is not read.</summary>
    [Fact]
    public void AnAnswerForAnotherPositionOrNotARecordIsNotRead()
    {
        var text = GlassEditorFixture.Text(GlassEditorFixture.Page("position").GetProperty("file").GetString()!);

        Assert.Null(GlassEditorPages.PositionFigures(text, "C1.2.C3.4"));
        Assert.Null(GlassEditorPages.PositionFigures("<html>not json</html>", "C1.2.C3.4"));
        Assert.Null(GlassEditorPages.PositionFigures("{\"mcode\":\"C1.2.C3.4\",\"part\":[\"C1.2.C3.4|64\"]}", "C1.2.C3.4"));
    }

    /// <summary>
    /// The calculation report's summary, each trade's line by Glass's own
    /// headers, then its totals; and every figure agrees with the running
    /// totals the estimate's page held just before the report was asked for
    /// (18 November 2026): the trades' labour costs, the material as parts
    /// and paint material together, the total, the VAT and the amount due.
    /// </summary>
    [Fact]
    public void TheCalculationIsTheSummaryOfGlasssReportAndAgreesWithTheTotals()
    {
        var calculation = GlassEditorPages.Calculation(
            GlassEditorFixture.Text(GlassEditorFixture.Page("report").GetProperty("file").GetString()!));
        var totals = GlassEditorPages.Selected(GlassEditorFixture.Text(
            GlassEditorFixture.Page("selectedAtReport").GetProperty("file").GetString()!)).Totals!
            .ToDictionary(total => total.Label, total => total.Amount, StringComparer.Ordinal);

        Assert.NotNull(calculation);
        Assert.Equal(CalculationTrades, calculation.Trades.Select(trade => trade.Trade));
        var body = calculation.Trades[0];
        Assert.Equal(80.00m, body.LabourRate);
        Assert.Equal(24.40m, body.LabourTime);
        Assert.Equal(1952.00m, body.LabourCosts);
        Assert.Equal(3878.42m, body.MaterialCosts);
        Assert.All(calculation.Trades, trade => Assert.Equal(totals[trade.Trade], trade.LabourCosts));
        Assert.Equal(45.90m, calculation.TotalLabourTime);
        Assert.Equal(3672.00m, calculation.TotalLabourCosts);
        Assert.Equal(totals["Parts"] + totals["Paint material"], calculation.TotalMaterial);
        Assert.Equal(totals["Total (£)"], calculation.RepairCostsExclVat);
        Assert.Equal("VAT (20.00 %)", calculation.VatLabel);
        Assert.Equal(totals["VAT (20.00 %)"], calculation.Vat);
        Assert.Equal(totals["Amount due (£)"], calculation.RepairCostsInclVat);
    }

    [Fact]
    public void APageWithNoSummaryIsNotACalculation() =>
        Assert.Null(GlassEditorPages.Calculation("<html><body><table><tr><td>Please wait</td></tr></table></body></html>"));

    private static GlassPositionFigures Figures(string name)
    {
        var page = GlassEditorFixture.Page(name);
        var figures = GlassEditorPages.PositionFigures(
            GlassEditorFixture.Text(page.GetProperty("file").GetString()!), page.GetProperty("id").GetString()!);
        Assert.NotNull(figures);
        Assert.Equal(page.GetProperty("id").GetString(), figures.PositionId);
        return figures;
    }

    private static List<(string Label, decimal Amount)> ExpectedTotals(JsonElement page) =>
        page.GetProperty("totals").EnumerateArray()
            .Select(total => (
                total.GetProperty("label").GetString()!,
                decimal.Parse(total.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();

    [Fact]
    public void AnEquipmentMapIsReadAsTheNestedListsThePageHolds()
    {
        var first = GlassEditorPages.EquipmentMap(
            GlassEditorFixture.Text(GlassEditorFixture.Page("equipment821").GetProperty("file").GetString()!));
        var second = GlassEditorPages.EquipmentMap(
            GlassEditorFixture.Text(GlassEditorFixture.Page("equipment193").GetProperty("file").GetString()!));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(Equipment821Shape, first.Select(list => list.Count));
        Assert.Equal(Equipment193Shape, second.Select(list => list.Count));
        Assert.Equal("I|2027021", string.Join('|', first[0]));
        Assert.Equal("e|21115_21117_85142|824", first[1][3]);
        Assert.Equal("ER", second[2][0]);
        Assert.Equal(" |24114_24119|207", second[3][8]);
    }

    [Fact]
    public void TheSpecialListHasSevenGroupsAndItsFreePositionsParts()
    {
        var groups = GlassEditorPages.SpecialOperations(
            GlassEditorFixture.Text(GlassEditorFixture.Page("specialList").GetProperty("file").GetString()!));

        Assert.NotNull(groups);
        Assert.Equal(SpecialGroupCodes, groups.Select(group => group.Code));
        var parts = Assert.Single(groups, group => group.Code == "SS-FREEPART");
        Assert.Equal("Free positions parts", parts.Name);
        Assert.Equal(129, parts.Rows.Count);
        var charge = Assert.Single(parts.Rows, row => row[0] == "AC0000073");
        Assert.Equal(".Assessment Damage Appraisal Charge", charge[2]);
        Assert.Empty(Assert.Single(groups, group => group.Code == "SS-BULPS").Rows);
    }

    [Fact]
    public void TheRemovalAnswerTellsTheEditorToRedraw()
    {
        var answer = GlassEditorDialogs.AfterPost(
            GlassEditorFixture.Text(GlassEditorFixture.Page("removed").GetProperty("file").GetString()!));

        Assert.Equal(GlassEditorAnswerKind.Done, answer.Kind);
    }

    [Fact]
    public void OpeningAsksForTheAddressesTheBrowserAskedFor()
    {
        var opening = GlassEditorFixture.Page("opening");
        var addresses = opening.GetProperty("addresses");
        var launchAddress = opening.GetProperty("launchAddress").GetString()!;

        var launch = GlassEditorPages.LaunchPage(
            GlassEditorFixture.Text(opening.GetProperty("launch").GetString()!), launchAddress, opening.GetProperty("wt").GetInt64());
        var key = GlassEditorPages.SessionKey(GlassEditorFixture.Text(opening.GetProperty("aco2").GetString()!));

        Assert.NotNull(launch);
        Assert.False(launch.Blocked);
        Assert.Equal(GlassEditorFixture.Key, key);
        Assert.Equal("https://repairestimate.autovistagroup.com/ere/aco/", launch.Base);
        Assert.Equal(opening.GetProperty("url0").GetString(), launch.Url0);
        Assert.Equal(opening.GetProperty("caller").GetString(), launch.Caller);
        Assert.Equal(addresses.GetProperty("aco").GetString(), launch.Aco);
        Assert.Equal(addresses.GetProperty("aco2").GetString(), launch.Aco2);

        var at = new GlassEditorAddresses(
            new GlassEditorSession(launch.Base, launch.Url0, key!, GlassEditorFixture.NoCookies),
            new GlassEditorFixture.FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(opening.GetProperty("wt").GetInt64())));
        Assert.Equal(addresses.GetProperty("frame").GetString(), at.Frame);
        Assert.Equal(addresses.GetProperty("close").GetString(), at.Close);
        Assert.DoesNotContain(GlassEditorFixture.Key, launch.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("TESTTOKEN", launch.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ABlockedLaunchPageIsReadAsBlocked()
    {
        var launch = GlassEditorPages.LaunchPage(
            "<script>document.location.replace( 'blocked.html?caller=x' );</script>",
            "https://repairestimate.autovistagroup.com/ere/acolib/aco_call_xml.php?WorkTime=1",
            0);

        Assert.NotNull(launch);
        Assert.True(launch.Blocked);
    }

    [Fact]
    public void TheWorkingAddressesAreInTheFormsTheEditorAsksForThem()
    {
        var clock = new GlassEditorFixture.FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(1794979937123));
        var at = new GlassEditorAddresses(GlassEditorFixture.Session(), clock);
        const string Base = "https://repairestimate.autovistagroup.com/ere/aco/";
        const string Q = "ksid=Q0TESTKEY000000000000000000001";

        Assert.Equal(Base + "selected.php?" + Q + "&dummy=1794979937&html5svg=1&win7=1&actFrame3=1", at.Selected());
        Assert.Equal(Base + "mcode.php?" + Q + "&partid=C1.2.C3.4&mcode=", at.Position("C1.2.C3.4"));
        Assert.Equal(
            Base + GlassEditorFixture.Page("report").GetProperty("address").GetString() + "1794979937123",
            at.Calculation());
        Assert.Equal(
            Base + "service.php?" + Q + "&html5svg=1&nationscodepage=iso-8859-1&rowid=821&reqcritmap3=1&dummy=1794979937123",
            at.Equipment("821"));
        Assert.Equal(
            Base + "comp.php?" + Q + "&mcode=753|754&cml=C&dialogCodePage=iso-8859-1&dummy=1794979937123",
            at.Dialog("comp.php", "753|754", "C"));
        Assert.Equal(
            Base + "part.php?" + Q + "&cml=C&partid=C1.2.C3.4&dialogCodePage=iso-8859-1&dummy=1794979937123",
            at.OpenPosition("C1.2.C3.4"));
        Assert.Equal(Base + "compproc.php?" + Q + "&cml=C&partid=C1.2.C3.4&dummy=1794979937123", at.Remove("C1.2.C3.4"));
        Assert.Equal(
            Base + "service.php?" + Q + "&html5svg=1&nationscodepage=iso-8859-1&savexit=1&dummy=1794979937123",
            at.Save());
        Assert.Equal(
            Base + "list_1G.php?" + Q + "&cml=A&html5svg=1&tcode=SS-FREEPART&exclk=AC&ds=DS-AUXWORK-702|SS-FREEPART&win7=1",
            at.SpecialList("DS-AUXWORK-702|SS-FREEPART"));
    }

    /// <summary>
    /// The vehicle's criteria are the part list's <c>partlist.addCrit</c>
    /// lines (R1206): each one's code, marks, value, name, radio group and
    /// flag, and whether another position uses it; and the page's own note
    /// on an option another position uses. The list served after the
    /// answers of 7 September 2026 holds the values they stored.
    /// </summary>
    [Fact]
    public void TheVehiclesCriteriaAreReadFromThePartList()
    {
        var page = GlassEditorFixture.Text(GlassEditorFixture.Page("partList").GetProperty("file").GetString()!);
        var later = GlassEditorFixture.Text(GlassEditorFixture.Page("partListAfterAnswers").GetProperty("file").GetString()!);

        var criteria = GlassEditorPages.Criteria(page);
        var after = GlassEditorPages.Criteria(later);

        Assert.Equal(170, criteria.Count);
        var park = criteria["24119"];
        Assert.Equal("Front and Rear Park Assistant System", park.Name);
        Assert.True(park.Has('g'));
        Assert.False(park.IsSet);
        Assert.True(park.IsRadio);
        Assert.Equal("144", park.Group);
        Assert.False(park.AffectsOtherParts);
        Assert.False(criteria["12000"].IsRadio);
        Assert.True(criteria["740"].Has('f'));
        Assert.True(criteria["740"].IsSet);
        Assert.True(after["24119"].IsSet);
        Assert.True(after["24119"].AffectsOtherParts);
        Assert.True(after["85161"].IsSet);
        Assert.False(after["85161"].AffectsOtherParts);
        Assert.Equal("Criteria affects other parts!", GlassEditorPages.ReferencedNote(page));
        Assert.Null(GlassEditorPages.ReferencedNote(later));
        Assert.Empty(GlassEditorPages.Criteria("<html><body>Please wait</body></html>"));
    }

    /// <summary>
    /// An equipment answer is stored at the address the editor's list asked
    /// for it, the answer <c>encodeURIComponent</c>-encoded (glasses7 #519),
    /// and its answer is the page that tells the list to read the criteria
    /// again; the save's answer is not.
    /// </summary>
    [Fact]
    public void AnEquipmentAnswerIsStoredAtTheAddressTheEditorAskedForIt()
    {
        var page = GlassEditorFixture.Page("globcrit");
        var at = new GlassEditorAddresses(
            GlassEditorFixture.Session(),
            new GlassEditorFixture.FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(1794979937123)));

        Assert.Equal(page.GetProperty("address").GetString(), at.StoreEquipment(page.GetProperty("answers").GetString()!));
        Assert.True(GlassEditorPages.EquipmentStored(GlassEditorFixture.Text(page.GetProperty("file").GetString()!)));
        Assert.False(GlassEditorPages.EquipmentStored(GlassEditorFixture.Text("save.answer.html")));
    }

    /// <summary>
    /// A part dialog's record names the row pair it was asked for after
    /// <c>2G</c> in its first line, as every captured part dialog asked for
    /// by a pair does; the "both" request names its part row and 0.
    /// </summary>
    [Theory]
    [InlineData("C1010800_701910520101_2G_0_5_|64", "0|5", true)]
    [InlineData("C1010800_701910520101_2G_0_5_|64", "0|3", false)]
    [InlineData("C2027021_701300250101_2G_822_829_|64", "822|829", true)]
    [InlineData("C2020001_701300250101_2G_771_772_|64", "771|772", true)]
    [InlineData("C1012402_701300250203_2G_1783_0_|64", "1783|1786", false)]
    [InlineData(null, "0|5", false)]
    public void ADialogsRecordNamesTheRowsItWasAskedFor(string? position, string rows, bool named) =>
        Assert.Equal(named, GlassEditorClient.NamesRows(position, rows));

    /// <summary>The part list is asked for with the group list in its address, as the browser asked for it.</summary>
    [Fact]
    public void ThePartListIsAskedForAsTheBrowserAskedForIt()
    {
        var page = GlassEditorFixture.Page("partList");
        var at = new GlassEditorAddresses(
            GlassEditorFixture.Session(),
            new GlassEditorFixture.FixedClock(DateTimeOffset.FromUnixTimeMilliseconds(1794979937123)));

        Assert.Equal(GlassRepairEstimateGateway.PartListGroups, page.GetProperty("groups").GetString());
        Assert.Equal(page.GetProperty("address").GetString(), at.PartList(GlassRepairEstimateGateway.PartListGroups));
    }

    /// <summary>
    /// Each <c>partlist.addPart</c> line is one row, with the drawing, its
    /// number, the part code and the name filled down from the row above and
    /// a criteria of 0 read as none, as the list's own code holds them.
    /// </summary>
    [Fact]
    public void ThePartListIsReadRowByRowAsTheEditorHoldsIt()
    {
        var page = GlassEditorFixture.Page("partList");

        var list = GlassEditorPages.PartList(GlassEditorFixture.Text(page.GetProperty("file").GetString()!));

        Assert.NotNull(list);
        Assert.Equal(page.GetProperty("rows").GetInt32(), list.Count);
        var lining = list[0];
        Assert.Equal("0", lining.Id);
        Assert.True(lining.IsHead);
        Assert.True(lining.Paintable);
        Assert.Null(lining.Side);
        Assert.Equal("1010800", lining.Code);
        Assert.Equal("6F0807221DGRU", lining.Number);
        Assert.Equal("379.00", lining.Price);
        Assert.Equal("Front Bumper Lining", lining.Name);
        var firstTime = list[1];
        Assert.False(firstTime.IsPart);
        Assert.True(firstTime.Has('t'));
        Assert.Equal("ER", firstTime.Operations);
        Assert.Equal(string.Empty, firstTime.Criteria);
        Assert.Equal("1010800", firstTime.Code);
        Assert.Equal("701910520101", firstTime.Drawing);
        Assert.Equal("12000", list[2].Criteria);
        var variant = Assert.Single(list, row => row.Id == "275");
        Assert.True(variant.IsPart);
        Assert.False(variant.IsHead);
        Assert.Equal("L", variant.Side);
        Assert.Equal("2025851", variant.Code);
        Assert.Equal("800", variant.Criteria);
        Assert.True(Assert.Single(list, row => row.Id == "65").ShownByEquipment);
        Assert.False(lining.ShownByEquipment);
        Assert.Equal("R", Assert.Single(list, row => row.Id == "20").Side);
        Assert.Null(GlassEditorPages.PartList("<html><body>Please wait</body></html>"));
    }

    [Fact]
    public void SaveAndExitIsReadToTheWayHomeExactlyAsThePageGivesIt()
    {
        var closing = GlassEditorFixture.Page("closing");
        var home = closing.GetProperty("home");
        var close = GlassEditorFixture.Page("opening").GetProperty("addresses").GetProperty("close").GetString()!;

        Assert.True(GlassEditorPages.SavedAndClosing(GlassEditorFixture.Text(closing.GetProperty("save").GetString()!)));
        Assert.False(GlassEditorPages.SavedAndClosing("<html><body></body></html>"));
        Assert.Equal(
            closing.GetProperty("backAddress").GetString(),
            GlassEditorPages.ClosePage(GlassEditorFixture.Text(closing.GetProperty("close").GetString()!), close));

        var outcome = GlassEditorPages.ReturnPage(GlassEditorFixture.Text(closing.GetProperty("back").GetString()!));

        Assert.NotNull(outcome);
        Assert.Equal(home.GetProperty("address").GetString(), outcome.Address);
        Assert.Equal(home.GetProperty("origin").GetString(), outcome.Origin);
        Assert.Equal(home.GetProperty("path").GetString(), outcome.Path);
        Assert.Equal(home.GetProperty("total").GetString(), outcome.Total);
        Assert.Equal(home.GetProperty("doSave").GetString(), outcome.DoSave);
        Assert.Equal(home.GetProperty("hasFile").GetBoolean(), outcome.HasFile);
        Assert.DoesNotContain("TESTTOKEN", outcome.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a b", "a+b")]
    [InlineData("*-._~|", "*-._%7E%7C")]
    [InlineData("£ 5", "%A3+5")]
    [InlineData("line\nbreak", "line%0D%0Abreak")]
    [InlineData("line\r\nbreak\rend", "line%0D%0Abreak%0D%0Aend")]
    [InlineData("50%&=?", "50%25%26%3D%3F")]
    public void AValueIsEncodedAsTheBrowserEncodesIt(string value, string expected) =>
        Assert.Equal(expected, GlassEditorText.EncodeValue(value));

    [Fact]
    public void ACharacterOutsideWindows1252IsSentAsItsCharacterReference()
    {
        Assert.Equal("%80", GlassEditorText.EncodeValue(char.ConvertFromUtf32(0x20AC)));
        Assert.Equal("%26%2326085%3B", GlassEditorText.EncodeValue(char.ConvertFromUtf32(0x65E5)));
        Assert.Equal(
            "Price=1%2C5&Reason=one%0D%0Atwo",
            GlassEditorText.EncodeForm([KeyValuePair.Create("Price", "1,5"), KeyValuePair.Create("Reason", "one\ntwo")]));
    }

    [Fact]
    public void AnswersAreReadAsWindows1252()
    {
        var text = GlassEditorText.Decode(HighBytes);

        Assert.Equal(char.ConvertFromUtf32(0x20AC) + "A£\u0081" + char.ConvertFromUtf32(0x0178), text);
        Assert.Equal(text, GlassEditorText.Decode(GlassEditorText.Encode(text)));
    }

    [Fact]
    public void TheFormReaderSubmitsWhatABrowserSubmits()
    {
        const string Page = """
            <html><body>
            <input type="text" name="outside" value="not in the form">
            <form name="F" action="go.php?a=1&amp;b=2&cml=C">
            <input type="text" name="t1">
            <input type=hidden name=h value='a "b" &lt;c&gt;'>
            <input type="checkbox" name="c1" checked>
            <input type="checkbox" name="c2" value="x">
            <input type="radio" name="r" value="1"><input type="radio" name="r" value="2" CHECKED>
            <input type="text" name="d" value="no" disabled>
            <input type="text" name="ro" value="kept" readonly>
            <select name="s1"><option value="a">A<option value="b" selected>B</select>
            <select name="s2"><option>  First
              one  <option>Second</select>
            <select name="s3"><option value="x" disabled>X<option value="y">Y</select>
            <input type="hidden" name="multi" value="line one
            line two">
            <input type="submit" name="go" value="Go"><input type="submit" name="other" value="Other">
            <input type="button" name="b" value="B">
            <textarea name="ta">
            hello &amp; bye</textarea>
            <input NAME="upper" VALUE="V">
            <!-- <input type="text" name="commented" value="1"> -->
            <script>var x = '<input name="scripted" value="1">';</script>
            <input type="hidden" name="after" value="1">
            </form>
            <input type="hidden" name="later" value="1">
            </body></html>
            """;

        var form = GlassEditorForm.Read(Page, "F");

        Assert.NotNull(form);
        Assert.Equal("go.php?a=1&b=2&cml=C", form.Action);
        var fields = form.Submit(form.Field("go"));
        Assert.Equal(SubmittedFields, fields.Entries.Select(pair => pair.Key + "=" + pair.Value));
        Assert.True(form.Field("ro")!.ReadOnly);
        Assert.Equal(1, form.Field("s1")!.SelectedIndex);
        Assert.Null(form.Field("r"));
        Assert.Null(GlassEditorForm.Read(Page, "Other"));
    }

    [Fact]
    public void ABuilderMayChangeControlsBeforeTheFieldsAreTaken()
    {
        const string Page = """
            <form name="Part" action="x.php">
            <input name="Place_L" type="checkbox" disabled>
            <input name="Both" type="checkbox">
            <select name="Repart"><option value="ER" selected>RP<option value="-">--<option value="AN">RN</select>
            <input type="text" name="RemarkTxt" value="" disabled>
            <input type="submit" name="BS" value="OK">
            </form>
            """;
        var form = GlassEditorForm.Read(Page, "Part")!;

        form.Field("Place_L")!.Checked = true;
        form.Field("Both")!.Checked = true;
        form.Field("Repart")!.Value = "AN";
        form.Field("RemarkTxt")!.Disabled = false;
        var fields = form.Submit(form.Field("BS"));

        Assert.Equal("Both=on&Repart=AN&RemarkTxt=&BS=OK", GlassEditorText.EncodeForm(fields.Entries));
        fields.Set("Repart", "ER");
        fields.Set("Added", "1");
        fields.Delete("Both");
        Assert.Equal("Repart=ER&RemarkTxt=&BS=OK&Added=1", GlassEditorText.EncodeForm(fields.Entries));
    }
}

/// <summary>The editor pages embedded in this assembly and their manifest.</summary>
internal static class GlassEditorFixture
{
    public const string Key = "Q0TESTKEY000000000000000000001";

    public const string Base = "https://repairestimate.autovistagroup.com/ere/aco/";

    public static readonly ReadOnlyDictionary<string, string> NoCookies =
        new Dictionary<string, string>(StringComparer.Ordinal).AsReadOnly();

    private static readonly Lazy<JsonElement> ManifestRoot = new(() =>
    {
        using var document = JsonDocument.Parse(Text("manifest.json"));
        return document.RootElement.Clone();
    });

    public static JsonElement Manifest => ManifestRoot.Value;

    /// <summary>One embedded page or answer, as text.</summary>
    public static string Text(string file)
    {
        using var stream = typeof(GlassEditorFixture).Assembly.GetManifestResourceStream("GlassEditorPages/" + file)
            ?? throw new InvalidOperationException("The test assembly holds no editor page " + file + ".");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static JsonElement.ArrayEnumerator Dialogs => Manifest.GetProperty("dialogs").EnumerateArray();

    public static JsonElement Dialog(string name) =>
        Dialogs.Single(dialog => dialog.GetProperty("name").GetString() == name);

    public static JsonElement Page(string name) => Manifest.GetProperty("pages").GetProperty(name);

    public static List<string> Strings(JsonElement element, string name) =>
        element.GetProperty(name).EnumerateArray().Select(item => item.GetString()!).ToList();

    /// <summary>A session as the opening fixture opens it, with no cookies.</summary>
    public static GlassEditorSession Session() =>
        new(Base, Page("opening").GetProperty("url0").GetString()!, Key, NoCookies);

    /// <summary>The adapter's options with the editor's real host and the fixture's return origin.</summary>
    public static GlassRepairEstimateOptions Options() =>
        new(
            new Uri("https://mva.test/"),
            new Uri("https://repairestimate.autovistagroup.com/"),
            new Uri("https://pegasus.test/"),
            "101",
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(60),
            GlassEstimateXmlParser.MaximumDocumentBytes);

    public sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
