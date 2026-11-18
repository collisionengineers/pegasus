using System.Text.Json;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The editor's dialog posts, built from dialog pages cut from real browser
/// captures and compared byte for byte with what the browser posted
/// (<c>GlassEditorPages/manifest.json</c>), and the answers that came back:
/// the page that tells the editor to redraw, or the "Mandatory parts:" page
/// and its own confirm post.
/// </summary>
public sealed class GlassEditorDialogTests
{
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

        Assert.NotNull(summary);
        foreach (var value in dialog.GetProperty("summary").EnumerateObject())
        {
            var expected = value.Value.ValueKind switch
            {
                JsonValueKind.String => value.Value.GetString(),
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                _ => null,
            };
            Assert.Equal($"{value.Name}={expected}", $"{value.Name}={Summarised(summary, value.Name)}");
        }
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
