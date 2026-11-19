using System.Text.Json;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The editor's dialog posts, built from dialog pages cut from real browser
/// captures and compared byte for byte with what the browser posted
/// (<c>GlassEditorPages/manifest.json</c>), and the answers that came back:
/// the page that tells the editor to redraw, or the "Mandatory parts:" page
/// and its own confirm post. The manifest's <c>operations</c> are Repair and
/// Uninstall and install chosen in four captured dialogs, each compared with
/// the post the dialog's own script sent when driven offline (R1172).
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
