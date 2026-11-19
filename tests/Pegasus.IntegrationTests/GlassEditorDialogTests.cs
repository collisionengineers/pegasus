using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The editor's dialog posts, built from dialog pages cut from real browser
/// captures and compared byte for byte with what the browser posted
/// (<c>GlassEditorPages/manifest.json</c>), and the answers that came back:
/// the page that tells the editor to redraw, or the "Mandatory parts:" page
/// and its own confirm post. The manifest's <c>operations</c> are Repair and
/// Uninstall and install chosen in four captured dialogs (R1172), and every
/// other operation of the left door and the left mirror, with Part and with
/// Part and Paint, two of the rear quarter's and the right reinforcement's
/// Renew (R1206), each compared with the post the dialog's own script sent
/// when driven offline.
/// </summary>
public sealed class GlassEditorDialogTests
{
    public static TheoryData<string> OperationNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var item in GlassEditorFixture.Manifest.GetProperty("operations").EnumerateArray())
            {
                names.Add(item.GetProperty("name").GetString()!);
            }

            return names;
        }
    }

    public static TheoryData<string> TypedNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var item in GlassEditorFixture.Manifest.GetProperty("typed").EnumerateArray())
            {
                names.Add(item.GetProperty("name").GetString()!);
            }

            return names;
        }
    }

    public static TheoryData<string> DialogNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var dialog in GlassEditorFixture.Dialogs)
            {
                names.Add(dialog.GetProperty("name").GetString()!);
            }

            return names;
        }
    }

    [Theory]
    [MemberData(nameof(DialogNames))]
    public void EachDialogIsPostedExactlyAsTheBrowserPostedIt(string name)
    {
        var dialog = GlassEditorFixture.Dialog(name);

        var build = Build(dialog);

        Assert.Null(build.Error);
        Assert.Equal(GlassEditorFixture.Strings(dialog, "problems"), build.Problems);
        Assert.Equal(GlassEditorFixture.Strings(dialog, "notes"), build.Notes);
        Assert.Equal(dialog.GetProperty("action").GetString(), build.Action);
        Assert.Equal(dialog.GetProperty("fields").GetInt32(), build.Fields.Count);
        Assert.Equal(
            GlassEditorFixture.Text(dialog.GetProperty("post").GetString()!),
            GlassEditorText.EncodeForm(build.Fields));
    }

    [Theory]
    [MemberData(nameof(DialogNames))]
    public void EachDialogIsSummarisedAsTheBuilderSummarisedIt(string name)
    {
        var dialog = GlassEditorFixture.Dialog(name);

        var summary = Build(dialog).Summary;

        AssertSummary(dialog, summary);
    }

    /// <summary>
    /// Repair (with its question before a part with no figures answered OK)
    /// and Uninstall and install, each built from the dialog page alone as
    /// its ChangeRepart and CheckSubmit would, and posted byte for byte as
    /// the dialog's own script posted it.
    /// </summary>
    [Theory]
    [MemberData(nameof(OperationNames))]
    public void EachOperationIsPostedExactlyAsTheDialogsOwnScriptPostedIt(string name)
    {
        var item = Operation(name);

        var build = BuildOperation(item, Confirmed(item));

        Assert.Null(build.Error);
        Assert.Equal(GlassEditorFixture.Strings(item, "problems"), build.Problems);
        Assert.Equal(GlassEditorFixture.Strings(item, "notes"), build.Notes);
        Assert.Equal(item.GetProperty("action").GetString(), build.Action);
        Assert.Equal(item.GetProperty("fields").GetInt32(), build.Fields.Count);
        Assert.Equal(
            GlassEditorFixture.Text(item.GetProperty("post").GetString()!),
            GlassEditorText.EncodeForm(build.Fields));
        AssertSummary(item, build.Summary);
    }

    /// <summary>
    /// The question the dialog's script asks before it sends a part whose
    /// price and hours are both zero is a problem until it is answered, in
    /// the script's own words; the answer changes nothing in the post. An
    /// operation with a figure is not asked about.
    /// </summary>
    [Theory]
    [MemberData(nameof(OperationNames))]
    public void APartWithNoFiguresIsNotPostedUntilTheQuestionIsAnswered(string name)
    {
        var item = Operation(name);

        var unanswered = BuildOperation(item, confirm: false);
        var answered = BuildOperation(item, confirm: true);

        if (!item.TryGetProperty("asked", out var asked))
        {
            Assert.Empty(unanswered.Problems);
            return;
        }

        Assert.Equal(GlassEditorFixture.Strings(item, "unanswered"), unanswered.Problems);
        Assert.Contains(asked.GetString()!, Assert.Single(unanswered.Problems), StringComparison.Ordinal);
        Assert.Empty(answered.Problems);
        Assert.Equal(unanswered.Fields, answered.Fields);
    }

    [Fact]
    public void OnAPaintablePartsDialogOnlyTheOperationMayBeChanged()
    {
        var page = GlassEditorFixture.Text("door_left_operations.dialog.html");

        var typed = GlassEditorDialogs.PartPost(page, "BSS", new GlassEditorChange(Operation: "AE", Hours: "0.30"));

        Assert.Contains("This dialog is built with no change but the operation.", typed.Problems);
    }

    [Theory]
    [InlineData("DD", "Choosing DD reloads the dialog.")]
    [InlineData("AN", "Choosing AN opens the old-for-new dialog.")]
    [InlineData("XX", "The dialog does not offer the operation XX.")]
    public void AnOperationThatReloadsOrOpensAnotherDialogIsAProblem(string operation, string problem)
    {
        var page = GlassEditorFixture.Text("door_left_operations.dialog.html");

        var build = GlassEditorDialogs.PartPost(page, "BSS", new GlassEditorChange(Operation: operation));

        Assert.Contains(problem, build.Problems);
    }

    [Theory]
    [MemberData(nameof(DialogNames))]
    public void EachAnswerIsReadAsTheBrowserSawIt(string name)
    {
        var dialog = GlassEditorFixture.Dialog(name);
        var kind = dialog.GetProperty("answerKind").GetString() == "confirm"
            ? GlassEditorAnswerKind.Confirm
            : GlassEditorAnswerKind.Done;
        List<string> ticks = dialog.TryGetProperty("ticks", out _) ? GlassEditorFixture.Strings(dialog, "ticks") : [];

        var answer = GlassEditorDialogs.AfterPost(GlassEditorFixture.Text(dialog.GetProperty("answer").GetString()!), ticks);

        Assert.Equal(kind, answer.Kind);
        if (kind != GlassEditorAnswerKind.Confirm)
        {
            return;
        }

        Assert.Equal("Mandatory parts:", answer.Title);
        Assert.Equal(dialog.GetProperty("confirmAction").GetString(), answer.Action);
        Assert.Equal(
            GlassEditorFixture.Text(dialog.GetProperty("confirm").GetString()!),
            GlassEditorText.EncodeForm(answer.Fields));
        Assert.Equal(
            dialog.GetProperty("listed").EnumerateArray().Select(listed => new GlassEditorListed(
                listed.GetProperty("code").GetString()!,
                listed.GetProperty("ticked").GetBoolean(),
                listed.GetProperty("optional").GetBoolean(),
                listed.GetProperty("text").GetString()!)),
            answer.Listed);
        Assert.Equal(
            GlassEditorAnswerKind.Done,
            GlassEditorDialogs.AfterPost(GlassEditorFixture.Text(dialog.GetProperty("confirmAnswer").GetString()!)).Kind);
    }

    [Fact]
    public void MoreHoursThanGlasssOwnWithoutAReasonIsAProblem()
    {
        var page = GlassEditorFixture.Text("special_hours.dialog.html");

        var build = GlassEditorDialogs.PlainPost(page, new GlassEditorChange(Hours: "0.80"));

        var problem = Assert.Single(build.Problems);
        Assert.Equal("More hours than Glass's own (0.50): the dialog asks for a reason.", problem);
    }

    [Fact]
    public void MoreHoursThanGlasssOwnWithAReasonIsPostedWithTheReason()
    {
        var page = GlassEditorFixture.Text("special_hours.dialog.html");

        var build = GlassEditorDialogs.PlainPost(
            page, new GlassEditorChange(Hours: "0.80", Reason: "Heavy soiling\nunder the arches"));

        Assert.Empty(build.Problems);
        Assert.Equal("0.80", Field(build, "Time"));
        Assert.Equal("Heavy soiling|under the arches", Field(build, "Reason"));
        Assert.Equal("0.80", build.Typed["Time"]);
        Assert.Equal("0.80", build.Summary!.Hours);
    }

    [Fact]
    public void FewerHoursThanGlasssOwnNeedNoReason()
    {
        var build = GlassEditorDialogs.PlainPost(
            GlassEditorFixture.Text("special_hours.dialog.html"), new GlassEditorChange(Hours: "0.30"));

        Assert.Empty(build.Problems);
        Assert.Equal("0.30", Field(build, "Time"));
        Assert.Equal(string.Empty, Field(build, "Reason"));
    }

    [Fact]
    public void ATypedPriceChangesOnlyThePrice()
    {
        var page = GlassEditorFixture.Text("special_fixed_price.dialog.html");
        var served = GlassEditorDialogs.PlainPost(page);

        var typed = GlassEditorDialogs.PlainPost(page, new GlassEditorChange(Price: "150.00"));

        Assert.Empty(typed.Problems);
        Assert.Equal("150.00", typed.Typed["Price"]);
        Assert.Equal(served.Fields.Select(pair => pair.Key), typed.Fields.Select(pair => pair.Key));
        var changed = Assert.Single(served.Fields.Zip(typed.Fields), pair => pair.First.Value != pair.Second.Value);
        Assert.Equal("Price", changed.Second.Key);
        Assert.Equal("176.96", changed.First.Value);
        Assert.Equal("150.00", changed.Second.Value);
    }

    [Theory]
    [InlineData("special_fixed_price.dialog.html", "XX")]
    [InlineData("cleaner_renew.dialog.html", "-")]
    public void AnOperationTheDialogDoesNotOfferIsAProblem(string file, string operation)
    {
        var build = GlassEditorDialogs.PlainPost(GlassEditorFixture.Text(file), new GlassEditorChange(Operation: operation));

        Assert.Contains("The dialog does not offer the operation " + operation + ".", build.Problems);
    }

    [Fact]
    public void APaintablePartsDialogIsNotBuiltAsAPlainOne()
    {
        var build = GlassEditorDialogs.PlainPost(GlassEditorFixture.Text("bumper_part.dialog.html"));

        Assert.Equal("The form has no submit button named BS.", build.Error);
    }

    [Fact]
    public void AButtonTheDialogDoesNotHaveIsAnError()
    {
        var build = GlassEditorDialogs.PartPost(GlassEditorFixture.Text("bumper_part.dialog.html"), "BS");

        Assert.Equal("The form has no submit button named BS.", build.Error);
        Assert.Empty(build.Fields);
    }

    /// <summary>
    /// An operation a dialog offers that no captured dialog's own script was
    /// seen to post is refused (R1206): the bonnet's NL.
    /// </summary>
    [Fact]
    public void AnOperationNotJudgedAgainstAScriptIsAProblem()
    {
        var build = GlassEditorDialogs.PartPost(
            GlassEditorFixture.Text("bonnet_operations.dialog.html"), "BSS", new GlassEditorChange(Operation: "NL"));

        Assert.Contains("Choosing NL has not been judged against a dialog's own script.", build.Problems);
    }

    /// <summary>Choosing the operation the dialog already serves changes nothing, as in the editor.</summary>
    [Fact]
    public void ChoosingTheServedOperationAgainChangesNothing()
    {
        var page = GlassEditorFixture.Text("door_left_operations.dialog.html");

        var again = GlassEditorDialogs.PartPost(page, "BSS", new GlassEditorChange(Operation: "ER"));

        Assert.Equal(GlassEditorDialogs.PartPost(page, "BSS").Fields, again.Fields);
        Assert.Empty(again.Problems);
    }

    /// <summary>
    /// The answer to a post with "Both" ticked names the other side's dialog
    /// as the list's BothNext opens it (judged offline: drive-both.mjs); an
    /// answer with no such call names none.
    /// </summary>
    [Fact]
    public void TheAnswerToAPostWithBothNamesTheOtherSidesDialog()
    {
        var next = GlassEditorDialogs.AfterPost(GlassEditorFixture.Text("reinforcement_left_both.answer.html")).Next;

        Assert.Equal(new GlassEditorNext("1783|ER|1786|AE|1792", "R", string.Empty), next);
        Assert.Null(GlassEditorDialogs.AfterPost(GlassEditorFixture.Text("reinforcement_right.answer.html")).Next);
    }

    /// <summary>
    /// A preview reads a part's dialog as a form (R1206): each operation with
    /// Glass's own text and the figures its script serves for the side,
    /// the fields with Glass's own
    /// labels, and its one button; with no inputs it says nothing of a send.
    /// Renew is offered but not choosable: the return cannot read it back.
    /// </summary>
    [Fact]
    public void APreviewReadsAPlainDialogAsAForm()
    {
        var (preview, build) = GlassEditorDialogs.Preview(GlassEditorFixture.Text("mirror_left_operations.dialog.html"));

        Assert.NotNull(preview);
        Assert.NotNull(build);
        Assert.Equal("part", preview.Kind);
        Assert.Equal("L", preview.Side);
        Assert.Null(preview.Paint);
        Assert.Null(preview.Sends);
        var refit = Assert.Single(preview.Operations, item => item.Code == "AE");
        Assert.Equal(0.60m, refit.Hours);
        Assert.True(refit.Changeable);
        var renew = Assert.Single(preview.Operations, item => item.Code == "AN");
        Assert.False(renew.Changeable);
        Assert.Equal(GlassAutomationPositionPolicy.OperationNotReadBackCode, renew.RefusedCode);
        Assert.DoesNotContain(preview.Operations, item => item.Code == "-");
        Assert.Equal("Repair type", Assert.Single(preview.Fields, field => field.Name == "Repart").Label);
        Assert.Contains(preview.Buttons, button => button.Value == "BS");
    }

    /// <summary>
    /// For the inputs given, a preview says what the add would send and what
    /// it would meet: Renew's figures as the old-for-new window writes them
    /// back on the right-hand reinforcement, and Both's other side.
    /// </summary>
    [Fact]
    public void APreviewSaysWhatAnAddWouldSendAndAsk()
    {
        var (renew, _) = GlassEditorDialogs.Preview(
            GlassEditorFixture.Text("reinforcement_right_operations.dialog.html"), null, new GlassEditorChange(Operation: "AN"));
        var (both, _) = GlassEditorDialogs.Preview(
            GlassEditorFixture.Text("reinforcement_left_both.dialog.html"), null, new GlassEditorChange(Both: true));

        Assert.NotNull(renew?.Sends);
        Assert.Equal("AN", renew.Sends.Operation);
        Assert.Equal(38.68m, renew.Sends.Price);
        Assert.Equal(0.00m, renew.Sends.Hours);
        Assert.Null(renew.Sends.Code);
        Assert.NotNull(both?.Sends);
        Assert.True(both.Sends.BothSides);
        Assert.Contains(both.Sends.Questions, question => question is { Kind: "other_side", Answered: true });
    }

    /// <summary>
    /// The person's typed input in each dialog of the operator's walk of 19
    /// November 2026 (R1206): built from the dialog page alone with that
    /// input, and posted byte for byte as the browser posted it.
    /// </summary>
    [Theory]
    [MemberData(nameof(TypedNames))]
    public void EachTypedInputIsPostedExactlyAsTheBrowserPostedIt(string name)
    {
        var item = Typed(name);

        var build = BuildTyped(item);

        Assert.Null(build.Error);
        Assert.Equal(GlassEditorFixture.Strings(item, "problems"), build.Problems);
        Assert.Equal(GlassEditorFixture.Strings(item, "notes"), build.Notes);
        Assert.Null(build.RefusalCode);
        Assert.Equal(item.GetProperty("action").GetString(), build.Action);
        Assert.Equal(item.GetProperty("fields").GetInt32(), build.Fields.Count);
        Assert.Equal(
            GlassEditorFixture.Text(item.GetProperty("post").GetString()!),
            GlassEditorText.EncodeForm(build.Fields));
        AssertSummary(item, build.Summary);
    }

    /// <summary>
    /// The editor's answer to each typed post of the walk: the page that
    /// tells the editor to redraw, or the "Mandatory parts:" page with its
    /// parts, and its confirm post where the browser's can be rebuilt.
    /// </summary>
    [Theory]
    [MemberData(nameof(TypedNames))]
    public void EachTypedPostsAnswerIsReadAsTheBrowserSawIt(string name)
    {
        var item = Typed(name);
        List<string> ticks = item.TryGetProperty("ticks", out _) ? GlassEditorFixture.Strings(item, "ticks") : [];

        var answer = GlassEditorDialogs.AfterPost(GlassEditorFixture.Text(item.GetProperty("answer").GetString()!), ticks);

        if (item.GetProperty("answerKind").GetString() != "confirm")
        {
            Assert.Equal(GlassEditorAnswerKind.Done, answer.Kind);
            return;
        }

        Assert.Equal(GlassEditorAnswerKind.Confirm, answer.Kind);
        Assert.Equal(item.GetProperty("confirmAction").GetString(), answer.Action);
        Assert.Equal(
            item.GetProperty("listed").EnumerateArray().Select(listed => listed.GetProperty("code").GetString()),
            answer.Listed.Select(listed => listed.Code));
        if (item.TryGetProperty("confirm", out var confirm))
        {
            Assert.Equal(GlassEditorFixture.Text(confirm.GetString()!), GlassEditorText.EncodeForm(answer.Fields));
            Assert.Equal(
                GlassEditorAnswerKind.Done,
                GlassEditorDialogs.AfterPost(GlassEditorFixture.Text(item.GetProperty("confirmAnswer").GetString()!)).Kind);
        }
    }

    /// <summary>
    /// The reason window's own Submit trims the spaces at either end of the
    /// reason (19 November 2026, reason.php); hours above Glass's own with no
    /// reason are refused with their code, and so is a reason the dialog
    /// does not ask for, which is never sent (R1206).
    /// </summary>
    [Fact]
    public void AReasonIsTrimmedAndSentOnlyWhenTheDialogAsksForIt()
    {
        var page = GlassEditorFixture.Text("special_hours.dialog.html");

        var trimmed = GlassEditorDialogs.PlainPost(page, new GlassEditorChange(Hours: "0.80", Reason: "  Seized bolts  "));
        var unreasoned = GlassEditorDialogs.PlainPost(page, new GlassEditorChange(Hours: "0.80"));
        var unasked = GlassEditorDialogs.PlainPost(page, new GlassEditorChange(Hours: "0.30", Reason: "Seized bolts"));

        Assert.Empty(trimmed.Problems);
        Assert.Equal("Seized bolts", Field(trimmed, "Reason"));
        Assert.Equal(GlassAutomationPositionPolicy.ReasonRequiredCode, unreasoned.RefusalCode);
        Assert.Equal("A reason is given, but the dialog asks for none.", Assert.Single(unasked.Problems));
        Assert.Equal(GlassAutomationPositionPolicy.ReasonNotAskedCode, unasked.RefusalCode);
        Assert.Equal(string.Empty, Field(unasked, "Reason"));
    }

    /// <summary>
    /// Overlap hours above the hours sent are refused with their code: the
    /// return's reader refuses a whole export with such a position.
    /// </summary>
    [Fact]
    public void OverlapHoursAboveTheHoursAreRefused()
    {
        var page = GlassEditorFixture.Text("door_glass_left_hours.dialog.html");

        var above = GlassEditorDialogs.PlainPost(page, new GlassEditorChange(Hours: "0.50", OverlapHours: "0.60"));
        var within = GlassEditorDialogs.PlainPost(page, new GlassEditorChange(Hours: "0.50", OverlapHours: "0.50"));

        Assert.Equal(GlassAutomationPositionPolicy.OverlapAboveHoursCode, above.RefusalCode);
        Assert.Empty(within.Problems);
        Assert.Equal("0.50", Field(within, "Ctime"));
        Assert.Equal("0.00", within.Summary!.ServedOverlapHours);
        Assert.Equal("0.90", within.Summary.ServedHours);
        Assert.Equal("0.50", within.Summary.Hours);
    }

    /// <summary>
    /// A preview says which inputs change the typed boxes of a dialog of a
    /// part that cannot be painted, and above which hours Glass's asks for a
    /// reason; a reason given answers that question (R1206).
    /// </summary>
    [Fact]
    public void APreviewGivesTheTypedBoxesAndTheReasonLimit()
    {
        var page = GlassEditorFixture.Text("windscreen_overlap.dialog.html");

        var (preview, _) = GlassEditorDialogs.Preview(page, null, new GlassEditorChange(Hours: "2.80", Reason: "Corroded frame"));

        Assert.NotNull(preview);
        var hours = Assert.Single(preview.Fields, field => field.Name == "Time");
        Assert.Equal("inputs.hours", hours.Input);
        Assert.True(hours.Changeable);
        Assert.Equal(1.80m, hours.ReasonAbove);
        Assert.Equal("inputs.overlapHours", Assert.Single(preview.Fields, field => field.Name == "Ctime").Input);
        Assert.Equal("inputs.price", Assert.Single(preview.Fields, field => field.Name == "Price").Input);
        Assert.NotNull(preview.Sends);
        Assert.Equal(2.80m, preview.Sends.Hours);
        Assert.Null(preview.Sends.Code);
        Assert.True(Assert.Single(preview.Sends.Questions, question => question.Kind == "reason").Answered);
    }

    private static JsonElement Typed(string name) =>
        GlassEditorFixture.Manifest.GetProperty("typed").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);

    private static GlassEditorBuild BuildTyped(JsonElement item)
    {
        var page = GlassEditorFixture.Text(item.GetProperty("page").GetString()!);
        var change = item.GetProperty("change");
        string? Given(string name) => change.TryGetProperty(name, out var value) ? value.GetString() : null;
        var typed = new GlassEditorChange(
            Price: Given("price"), Hours: Given("hours"), Reason: Given("reason"), OverlapHours: Given("overlapHours"));
        return item.GetProperty("dialog").GetString() == "comp.php"
            ? GlassEditorDialogs.PartPost(page, item.GetProperty("button").GetString()!, typed)
            : GlassEditorDialogs.PlainPost(page, typed);
    }

    private static JsonElement Operation(string name) =>
        GlassEditorFixture.Manifest.GetProperty("operations").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);

    private static bool Confirmed(JsonElement item) =>
        item.GetProperty("change").TryGetProperty("confirm", out var confirm) && confirm.GetBoolean();

    private static GlassEditorBuild BuildOperation(JsonElement item, bool confirm)
    {
        var page = GlassEditorFixture.Text(item.GetProperty("page").GetString()!);
        var change = new GlassEditorChange(
            Operation: item.GetProperty("change").GetProperty("operation").GetString(),
            ConfirmNoFigures: confirm);
        return item.GetProperty("dialog").GetString() == "comp.php"
            ? GlassEditorDialogs.PartPost(page, item.GetProperty("button").GetString()!, change)
            : GlassEditorDialogs.PlainPost(page, change);
    }

    private static void AssertSummary(JsonElement expected, GlassEditorSummary? summary)
    {
        Assert.NotNull(summary);
        foreach (var value in expected.GetProperty("summary").EnumerateObject())
        {
            var wanted = value.Value.ValueKind switch
            {
                JsonValueKind.String => value.Value.GetString(),
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                _ => null,
            };
            Assert.Equal($"{value.Name}={wanted}", $"{value.Name}={Summarised(summary, value.Name)}");
        }
    }

    private static GlassEditorBuild Build(JsonElement dialog)
    {
        var page = GlassEditorFixture.Text(dialog.GetProperty("page").GetString()!);
        if (dialog.GetProperty("dialog").GetString() == "comp.php")
        {
            return GlassEditorDialogs.PartPost(page, dialog.GetProperty("button").GetString()!);
        }

        var change = dialog.GetProperty("change");
        return GlassEditorDialogs.PlainPost(page, new GlassEditorChange(
            Operation: change.TryGetProperty("operation", out var operation) ? operation.GetString() : null,
            Both: change.TryGetProperty("both", out var both) && both.GetBoolean()));
    }

    private static string? Field(GlassEditorBuild build, string name) =>
        build.Fields.Where(pair => pair.Key == name).Select(pair => pair.Value).FirstOrDefault();

    private static string? Summarised(GlassEditorSummary summary, string name) => name switch
    {
        "position" => summary.Position,
        "partCode" => summary.PartCode,
        "description" => summary.Description,
        "special" => summary.Special switch { true => "True", false => "False", null => null },
        "operation" => summary.Operation,
        "offered" => summary.Offered,
        "price" => summary.Price,
        "hours" => summary.Hours,
        "partNumber" => summary.PartNumber,
        "trade" => summary.Trade,
        "area" => summary.Area,
        "paintType" => summary.PaintType,
        "paintLevel" => summary.PaintLevel,
        "paintPrice" => summary.PaintPrice,
        "paintHours" => summary.PaintHours,
        "side" => summary.Side,
        "sidesOffered" => summary.SidesOffered,
        "rowPair" => summary.RowPair,
        _ => throw new InvalidOperationException("The manifest names a summary value the builder does not have: " + name),
    };
}
