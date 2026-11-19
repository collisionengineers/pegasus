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

        var values = new List<string>();
        foreach (var head in item.GetProperty("heads").EnumerateArray())
        {
            var index = rows.ToList().FindIndex(row => row.IsHead && row.Id == head.GetString());
            var panelItem = GlassEditorPanel.ItemOf(rows, index);
            Assert.NotNull(panelItem);
            var (value, problem) = GlassEditorPanel.PartAdd(panelItem, operation, hours, zone, areas);
            Assert.Null(problem);
            values.Add(value!);
        }

        Assert.Equal(GlassEditorFixture.Strings(item, "commands"), GlassEditorPanel.Commands(values));
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
