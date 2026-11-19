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

    public static TheoryData<string> InputNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var item in GlassEditorFixture.Manifest.GetProperty("inputs").EnumerateArray())
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
    public void APaintablePartsDialogHasNoDescriptionAndNoBoth()
    {
        var page = GlassEditorFixture.Text("door_left_operations.dialog.html");

        var both = GlassEditorDialogs.PartPost(page, "BSS", new GlassEditorChange(Both: true));

        Assert.Contains("This dialog is built with no description and no Both.", both.Problems);
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
            dialog.GetProperty("listed").EnumerateArray().Select(listed => (
                listed.GetProperty("code").GetString()!,
                listed.GetProperty("ticked").GetBoolean(),
                listed.GetProperty("optional").GetBoolean(),
                listed.GetProperty("text").GetString()!)),
            answer.Listed.Select(listed => (listed.Code, listed.Ticked, listed.Optional, listed.Text)));
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
    /// Renew is choosable: the return reads every repair kind (R1206).
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
        Assert.True(Assert.Single(preview.Operations, item => item.Code == "AN").Changeable);
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

    private static readonly string[] PaintOnlyGroups = ["", "L", "CL"];

    private static readonly string[] Cartridge = ["1102450.ER."];

    private static readonly string[] SecondCartridge = ["1102450.ER.#2"];

    private static readonly string[] CartridgeBoxes = ["1102450.ER.#1", "1102450.ER.#2"];

    /// <summary>
    /// A code the "Mandatory parts:" page lists twice (the windscreen's
    /// Additional Cartridge, #717) is two boxes, each with its own id (R1206):
    /// the second id ticks the second box alone, as the browser ticked it
    /// (#723), and the bare code ticks the first optional one.
    /// </summary>
    [Fact]
    public void ACodeListedTwiceIsTickedOneBoxAtATime()
    {
        var page = GlassEditorFixture.Text("windscreen_overlap.answer.html");

        var second = GlassEditorDialogs.AfterPost(page, SecondCartridge);
        var first = GlassEditorDialogs.AfterPost(page, Cartridge);

        var boxes = second.Listed.Where(listed => listed.Code == "1102450.ER.").ToList();
        Assert.Equal(CartridgeBoxes, boxes.Select(listed => listed.Id));
        Assert.False(boxes[0].Chosen);
        Assert.True(boxes[1].Chosen);
        Assert.Equal(GlassEditorFixture.Text("windscreen_overlap.confirm.txt"), GlassEditorText.EncodeForm(second.Fields));
        var firstBoxes = first.Listed.Where(listed => listed.Code == "1102450.ER.").ToList();
        Assert.True(firstBoxes[0].Chosen);
        Assert.False(firstBoxes[1].Chosen);
        Assert.Equal("1102410.ER.#1", first.Listed[1].Id);
    }

    /// <summary>
    /// The editor's own rule for which dialog opens a position on the
    /// estimate (selected.php's LoadGraph, 19 November 2026; R1206): a part
    /// with no paint opens part.php, a part with its paint and its paint
    /// position comp.php, a paint position of its own lack.php, and a group
    /// the list does not hold none.
    /// </summary>
    [Fact]
    public void TheEditorsOwnRuleChoosesAPositionsDialog()
    {
        var estimate = GlassEditorPages.Selected(GlassEditorFixture.Text("selected_before_change.html"));

        Assert.NotNull(estimate.Groups);
        Assert.Equal("part", GlassEditorPages.DialogPage("C421260.10.C1100400.2205", estimate.Groups));
        Assert.Equal("comp", GlassEditorPages.DialogPage("C421280.12.C1060000.2102", estimate.Groups));
        Assert.Equal("comp", GlassEditorPages.DialogPage("L421281.12.Lxx1640.Lxx1640", estimate.Groups));
        Assert.Null(GlassEditorPages.DialogPage("C1.5.C1.1", estimate.Groups));
        Assert.Null(GlassEditorPages.DialogPage("C1.99.C1.1", estimate.Groups));
        Assert.Equal("lack", GlassEditorPages.DialogPage("L1.1.Lxx1.Lxx1", PaintOnlyGroups));
        Assert.Equal("lack", GlassEditorPages.DialogPage("L1.2.Lxx1.Lxx1", PaintOnlyGroups));
        Assert.Equal("part", GlassEditorPages.DialogPage("S1.2.S1.1", PaintOnlyGroups));
        Assert.Equal("comp", GlassEditorPages.DialogPage("C1.2.C1.1", PaintOnlyGroups));
    }

    /// <summary>
    /// A new part's comp.php dialog shows all three buttons, so none is
    /// pressed by itself; the reopened bonnet's shows Part and Paint alone
    /// (#848), and a hidden button is not pressed (R1206).
    /// </summary>
    [Fact]
    public void ADialogShowingEveryButtonHasNoneToPressByItselfAndAHiddenOneIsRefused()
    {
        var reopened = GlassEditorFixture.Text("bonnet_change_paint.dialog.html");

        var hidden = GlassEditorDialogs.PartPost(reopened, "BSS", new GlassEditorChange(Paint: new GlassEditorPaint(Coats: "2")));

        Assert.Null(GlassEditorDialogs.VisibleButton(GlassEditorFixture.Text("bumper_part.dialog.html")));
        Assert.Equal("BSL", GlassEditorDialogs.VisibleButton(reopened));
        Assert.Contains("The dialog hides the button BSS.", hidden.Problems);
    }

    /// <summary>
    /// The paint choices a dialog's own script would not take are problems
    /// (R1206): Surcharge where the paint type fixes it, a level the type does
    /// not offer. A plastic part's level goes through (R1206). Unticking
    /// Surcharge where it may be changed is posted without it.
    /// </summary>
    [Fact]
    public void PaintChoicesTheDialogWouldNotTakeAreRefused()
    {
        var page = GlassEditorFixture.Text("bonnet_paint_choices.dialog.html");
        var plastic = page.Replace("var Mater = \"B\";", "var Mater = \"K\";", StringComparison.Ordinal);

        var fixedSurcharge = GlassEditorDialogs.PartPost(page, "BSL", new GlassEditorChange(Paint: new GlassEditorPaint(Coats: "1", Surcharge: true)));
        var unoffered = GlassEditorDialogs.PartPost(page, "BSL", new GlassEditorChange(Paint: new GlassEditorPaint(Level: "Z")));
        var unread = GlassEditorDialogs.PartPost(plastic, "BSL", new GlassEditorChange(Paint: new GlassEditorPaint(Level: "3")));
        var unticked = GlassEditorDialogs.PartPost(page, "BSL", new GlassEditorChange(Paint: new GlassEditorPaint(Coats: "3", Surcharge: false)));

        Assert.Contains("The dialog fixes Surcharge here.", fixedSurcharge.Problems);
        Assert.Contains("The paint type 2 does not offer the paint level Z.", unoffered.Problems);
        Assert.Empty(unread.Problems);
        Assert.Contains(unread.Fields, pair => pair.Key == "Lackstufe" && pair.Value == "3");
        Assert.Empty(unticked.Problems);
        Assert.DoesNotContain(unticked.Fields, pair => pair.Key == "Zuschlag");
        Assert.Contains(unticked.Fields, pair => pair.Key == "Klarlack" && pair.Value == "1");
        Assert.False(unticked.Summary!.Surcharge);
        Assert.Equal("2", unticked.Summary.ServedPaintType);
        Assert.Equal("3", unticked.Summary.PaintType);
    }

    /// <summary>
    /// A preview of a part that can be painted gives each paint type and
    /// level it offers with Glass's figures for them and the input that
    /// changes each paint box; the reopened bonnet's says its figures are the
    /// position's own (R1206).
    /// </summary>
    [Fact]
    public void APreviewGivesThePaintChoices()
    {
        var (preview, _) = GlassEditorDialogs.Preview(
            GlassEditorFixture.Text("bonnet_paint_choices.dialog.html"), "BSL",
            new GlassEditorChange(Paint: new GlassEditorPaint(Coats: "3", Level: "3")));
        var (reopened, _) = GlassEditorDialogs.Preview(GlassEditorFixture.Text("bonnet_change_paint.dialog.html"));

        Assert.NotNull(preview?.Paint);
        var threeCoat = Assert.Single(preview.Paint.Choices, choice => choice is { Coats: "3", Level: "3" });
        Assert.Equal(691.15m, threeCoat.Price);
        Assert.Equal(3.60m, threeCoat.Hours);
        Assert.False(preview.Paint.PositionsOwn);
        Assert.Equal("inputs.paint.coats", Assert.Single(preview.Fields, field => field.Name == "Lackart").Input);
        Assert.Equal("inputs.paint.hours", Assert.Single(preview.Fields, field => field.Name == "LTime").Input);
        Assert.NotNull(preview.Sends);
        Assert.Equal("3", preview.Sends.PaintCoats);
        Assert.Equal("3", preview.Sends.PaintLevel);
        Assert.Equal(691.15m, preview.Sends.PaintPrice);
        Assert.Null(preview.Sends.Code);
        Assert.True(reopened?.Paint?.PositionsOwn);
    }

    /// <summary>
    /// Every input a person gives a position's dialog (R1206): each built
    /// from the captured dialog page alone and posted byte for byte as the
    /// dialog's own script posted it when driven offline with that input.
    /// </summary>
    [Theory]
    [MemberData(nameof(InputNames))]
    public void EachInputIsPostedExactlyAsTheDialogsOwnScriptPostedIt(string name)
    {
        var item = GlassEditorFixture.Manifest.GetProperty("inputs").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == name);

        var build = BuildTyped(item);

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
    /// Renew's old-for-new window is asked for as the dialog's own
    /// OpenOldfrnewWindow asks for it (R1206): the walk's door (#916) with its
    /// paint, and the roof panel cleaner (n #731) without.
    /// </summary>
    [Fact]
    public void RenewsWindowIsAskedForWithTheDialogsOwnFigures()
    {
        Assert.Equal(
            "&cml=C&nprice=568.86&discount=0.00&disctype=%&ntime=3.50&discountt=0.00&disctypet=%&npricep=437.30&ntimep=1.80&discountp=0.00",
            GlassEditorDialogs.OldForNewParameters(GlassEditorFixture.Text("door_shell.dialog.html"), "BSS"));
        Assert.Equal(
            "&cml=C&nprice=11.44&discount=0.00&disctype=%&ntime=0.00&discountt=0.00&disctypet=%",
            GlassEditorDialogs.OldForNewParameters(GlassEditorFixture.Text("cleaner_renew.dialog.html"), null));
    }

    /// <summary>
    /// Paintless dent repair reloads the dialog (R1206): its ChangeRepart
    /// submits the form as the load script left it, with DD and the labour
    /// type D, to the address the script names, exactly as the bonnet's own
    /// script sent it when driven offline (#745).
    /// </summary>
    [Fact]
    public void AnOperationThatReloadsTheDialogSubmitsItAsItsScriptDoes()
    {
        var page = GlassEditorFixture.Text("bonnet_paint_choices.dialog.html");

        var reload = GlassEditorDialogs.ReloadPost(page, "DD");

        Assert.True(GlassEditorDialogs.Reloads(page, "DD"));
        Assert.False(GlassEditorDialogs.Reloads(page, "AE"));
        Assert.Null(reload.Error);
        Assert.Empty(reload.Problems);
        Assert.Equal("comp.php?ksid=" + GlassEditorFixture.Key + "&cml=C&mcode=477|478&frl=1", reload.Action);
        Assert.Equal(GlassEditorFixture.Text("bonnet_dd_reload.post.txt"), GlassEditorText.EncodeForm(reload.Fields));
    }

    /// <summary>
    /// An operation the script holds no figures for (the bonnet's NL): its
    /// own script alerts, CheckSubmit writes 0.00 in each box and asks before
    /// sending no figures; answered OK, the post is the script's own (#745).
    /// </summary>
    [Fact]
    public void AnOperationWithNoFiguresIsPostedWithZeroesOnceTheQuestionIsAnswered()
    {
        var page = GlassEditorFixture.Text("bonnet_paint_choices.dialog.html");

        var unanswered = GlassEditorDialogs.PartPost(page, "BSS", new GlassEditorChange(Operation: "NL"));
        var answered = GlassEditorDialogs.PartPost(page, "BSS", new GlassEditorChange(Operation: "NL", ConfirmNoFigures: true));

        Assert.NotEmpty(unanswered.Problems);
        Assert.Empty(answered.Problems);
        Assert.Equal(GlassEditorFixture.Text("c3_bonnet_nl.post.txt"), GlassEditorText.EncodeForm(answered.Fields));
    }

    private static JsonElement Typed(string name) =>
        GlassEditorFixture.Manifest.GetProperty("typed").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == name);

    private static GlassEditorBuild BuildTyped(JsonElement item)
    {
        var page = GlassEditorFixture.Text(item.GetProperty("page").GetString()!);
        var typed = ChangeOf(item.GetProperty("change"));
        return item.GetProperty("dialog").GetString() == "comp.php"
            ? GlassEditorDialogs.PartPost(page, item.GetProperty("button").GetString()!, typed)
            : GlassEditorDialogs.PlainPost(page, typed);
    }

    /// <summary>A manifest entry's change, as a person gave it in the dialog.</summary>
    private static GlassEditorChange ChangeOf(JsonElement change)
    {
        static string? Text(JsonElement from, string name) => from.TryGetProperty(name, out var value) ? value.GetString() : null;
        static bool? Tick(JsonElement from, string name) => from.TryGetProperty(name, out var value) ? value.GetBoolean() : null;
        GlassEditorPaint? paint = null;
        if (change.TryGetProperty("paint", out var chosen))
        {
            paint = new GlassEditorPaint(
                Text(chosen, "coats"), Text(chosen, "level"), Tick(chosen, "surcharge"), Text(chosen, "hours"),
                Text(chosen, "area"), Text(chosen, "procedure"), Text(chosen, "method"), Tick(chosen, "colouredClear"),
                Tick(chosen, "scratchResistant"), Text(chosen, "price"), Text(chosen, "reason"));
        }

        GlassEditorOldForNew? window = null;
        if (change.TryGetProperty("oldForNew", out var answered))
        {
            window = new GlassEditorOldForNew(
                GlassEditorFixture.Text(Text(answered, "window")!), Text(answered, "priceDeduction"), Text(answered, "priceKind"),
                Text(answered, "hoursDeduction"), Text(answered, "hoursKind"), Text(answered, "paintDeduction"));
        }

        return new GlassEditorChange(
            Operation: Text(change, "operation"),
            Price: Text(change, "price"),
            Hours: Text(change, "hours"),
            Description: Text(change, "description"),
            Reason: Text(change, "reason"),
            ConfirmNoFigures: Tick(change, "confirm") == true,
            OverlapHours: Text(change, "overlapHours"),
            Paint: paint,
            Place: Text(change, "place"),
            Trade: Text(change, "trade"),
            PartNumber: Text(change, "partNumber"),
            LabourDescription: Text(change, "labourDescription"),
            OutsideWork: Tick(change, "outsideWork") == true,
            OverlapFull: Tick(change, "overlapFull") == true,
            OldForNew: window);
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
