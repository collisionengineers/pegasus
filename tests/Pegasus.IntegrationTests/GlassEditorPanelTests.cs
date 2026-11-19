using System.Text.Json;
using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The quick repair panel (R1206): each command it sends, built from the
/// captured part list and paint list exactly as the panel's own script sent
/// it when driven offline, among them glasss2missing.har #536 and #537.
/// </summary>
public sealed class GlassEditorPanelTests
{
    public static TheoryData<string> PanelNames
    {
        get
        {
            var names = new TheoryData<string>();
            foreach (var item in GlassEditorFixture.Manifest.GetProperty("panel").EnumerateArray())
            {
                names.Add(item.GetProperty("name").GetString()!);
            }

            return names;
        }
    }

    [Theory]
    [MemberData(nameof(PanelNames))]
    public void EachPressIsSentAsThePanelsOwnScriptSentIt(string name)
    {
        var item = GlassEditorFixture.Manifest.GetProperty("panel").EnumerateArray()
            .Single(entry => entry.GetProperty("name").GetString() == name);
        var rows = GlassEditorPages.PartList(GlassEditorFixture.Text("panel_list.html"))!;
        var areas = GlassEditorPanel.ZoneLevels(GlassEditorFixture.Text("panel_paint.html"));
        var operation = item.GetProperty("operation").GetString()!;
        var hours = Text(item, "hours");
        var zone = Text(item, "zone");

        var estimate = EstimateOf(item);

        var values = new List<string>();
        var deletes = new List<string?>();
        foreach (var head in item.GetProperty("heads").EnumerateArray())
        {
            var index = rows.ToList().FindIndex(row => row.IsHead && row.Id == head.GetString());
            var panelItem = GlassEditorPanel.ItemOf(rows, index);
            Assert.NotNull(panelItem);
            var (value, problem) = GlassEditorPanel.PartAdd(panelItem, operation, hours, zone, areas);
            Assert.Null(problem);
            values.Add(value!);
            var (delete, pressed) = GlassEditorPanel.Deletes(panelItem, operation, estimate);
            Assert.Null(pressed);
            deletes.Add(delete);
        }

        Assert.Equal(GlassEditorFixture.Strings(item, "commands"), GlassEditorPanel.Commands(values, deletes));
    }

    /// <summary>
    /// A press on an operation the part already has on the estimate removes
    /// that position in the panel (it sent nothing to add, driven offline),
    /// so it is refused; the Automation removes a position by its id.
    /// </summary>
    [Fact]
    public void APressThatWouldRemoveThePartsOwnPositionIsRefused()
    {
        var rows = GlassEditorPages.PartList(GlassEditorFixture.Text("panel_list.html"))!;
        var skin = GlassEditorPanel.ItemOf(rows, rows.ToList().FindIndex(row => row.IsHead && row.Id == "171"))!;
        var estimate = new GlassEditorEstimate(true, OwnPositions, OwnCodes)
        {
            Records = new Dictionary<string, GlassEditorRecord>(StringComparer.Ordinal)
            {
                [OwnPosition] = new("2020811", "AE"),
            },
        };

        Assert.NotNull(GlassEditorPanel.Deletes(skin, GlassEditorPanel.RemoveAndRefit, estimate).Problem);
        var replaced = GlassEditorPanel.Deletes(skin, GlassEditorPanel.Replace, estimate);
        Assert.Equal(OwnPosition, replaced.Delete);
        Assert.Null(replaced.Problem);
    }

    private const string OwnPosition = "C1.1.C2020811.1869";

    private static readonly string[] OwnPositions = [OwnPosition];

    private static readonly string[] OwnCodes = ["C2020811"];

    private static GlassEditorEstimate EstimateOf(JsonElement item)
    {
        var records = new Dictionary<string, GlassEditorRecord>(StringComparer.Ordinal);
        if (item.TryGetProperty("estimate", out var held))
        {
            foreach (var position in held.EnumerateArray())
            {
                records[position.GetProperty("id").GetString()!] =
                    new(position.GetProperty("code").GetString()!, position.GetProperty("operation").GetString()!);
            }
        }

        return new GlassEditorEstimate(true, [.. records.Keys], []) { Records = records };
    }

    /// <summary>The panel's commands go out as the editor's AcoCommand sends them: a space as %20 and the command closed with =1.</summary>
    [Fact]
    public void ACommandIsAskedForAsTheEditorAsksForIt()
    {
        var at = new GlassEditorAddresses(GlassEditorFixture.Session(), TimeProvider.System);

        var address = at.Panel("partadd=171|172|I |1.5|III|80||&addpart2g");

        Assert.StartsWith(
            GlassEditorFixture.Base + "service.php?ksid=" + GlassEditorFixture.Key
                + "&html5svg=1&nationscodepage=iso-8859-1&partadd=171|172|I%20|1.5|III|80||&addpart2g=1&dummy=",
            address,
            StringComparison.Ordinal);
    }

    /// <summary>A button the panel greys, an area its paint box does not offer and hours with another button are not sent.</summary>
    [Fact]
    public void WhatThePanelWouldNotSendIsRefused()
    {
        var rows = GlassEditorPages.PartList(GlassEditorFixture.Text("panel_list.html"))!;
        var areas = GlassEditorPanel.ZoneLevels(GlassEditorFixture.Text("panel_paint.html"));
        var skin = GlassEditorPanel.ItemOf(rows, rows.ToList().FindIndex(row => row.IsHead && row.Id == "171"))!;
        var reinforcement = GlassEditorPanel.ItemOf(rows, rows.ToList().FindIndex(row => row.IsHead && row.Id == "19"))!;

        Assert.NotNull(GlassEditorPanel.PartAdd(skin, "ZZ", null, null, areas).Problem);
        Assert.NotNull(GlassEditorPanel.PartAdd(skin, GlassEditorPanel.RemoveAndRefit, null, "999", areas).Problem);
        Assert.NotNull(GlassEditorPanel.PartAdd(reinforcement, GlassEditorPanel.Replace, null, "20", areas).Problem);
        Assert.NotNull(GlassEditorPanel.PartAdd(skin, GlassEditorPanel.Replace, "1.0", null, areas).Problem);
        Assert.NotNull(GlassEditorPanel.PartAdd(skin, GlassEditorPanel.Replace, null, null, null).Problem);
        Assert.Equal("1.5", GlassEditorPanel.HoursText(1.5m));
        Assert.Null(GlassEditorPanel.HoursText(1.25m));
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
