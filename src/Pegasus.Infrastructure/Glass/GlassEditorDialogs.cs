using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// What a person may change on a <c>part.php</c> dialog before pressing OK:
/// another operation the dialog offers, a typed price, hours or (a special
/// operation only) description, the reason the dialog asks for when the hours
/// are above Glass's own, and the "Both" sides tick. Nothing set is the
/// dialog as served.
/// </summary>
internal sealed record GlassEditorChange(
    string? Operation = null,
    string? Price = null,
    string? Hours = null,
    string? Description = null,
    string? Reason = null,
    bool Both = false);

/// <summary>
/// What a built dialog post is for, as the dialog states it. A value is null
/// where the dialog gave none; <see cref="Special"/> is set only for a
/// <c>part.php</c> dialog, and the paint and row values only for a
/// <c>comp.php</c> one.
/// </summary>
internal sealed record GlassEditorSummary
{
    public string? Position { get; init; }

    public string? PartCode { get; init; }

    public string? Description { get; init; }

    public bool? Special { get; init; }

    public string? Operation { get; init; }

    public string? Offered { get; init; }

    public string? Price { get; init; }

    public string? Hours { get; init; }

    public string? PartNumber { get; init; }

    public string? Trade { get; init; }

    public string? Area { get; init; }

    public string? PaintType { get; init; }

    public string? PaintLevel { get; init; }

    public string? PaintPrice { get; init; }

    public string? PaintHours { get; init; }

    public string? Side { get; init; }

    public string? SidesOffered { get; init; }

    public string? RowPair { get; init; }
}

/// <summary>
/// A dialog post built from the dialog page, or why none could be built.
/// <see cref="Error"/> is a page that is not the dialog expected; a
/// <see cref="Problems"/> entry is a dialog that must not be posted;
/// <see cref="Notes"/> are differences that do not stop the post.
/// </summary>
internal sealed record GlassEditorBuild(
    string? Error,
    string? Action,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    IReadOnlyList<string> Problems,
    IReadOnlyList<string> Notes,
    IReadOnlyDictionary<string, string> Typed,
    GlassEditorSummary? Summary)
{
    public static GlassEditorBuild Failed(string error) =>
        new(error, null, [], [], [], new Dictionary<string, string>(StringComparer.Ordinal), null);
}

/// <summary>How the editor answered a dialog's post.</summary>
internal enum GlassEditorAnswerKind
{
    /// <summary>The page that tells the editor to redraw: the post was taken.</summary>
    Done,

    /// <summary>The "Mandatory parts:" page, whose Continue posts its own list back.</summary>
    Confirm,

    /// <summary>Any other page.</summary>
    Unknown,
}

/// <summary>One part the "Mandatory parts:" page lists, by its code (such as <c>1017810.ER.</c>).</summary>
internal sealed record GlassEditorListed(string Code, bool Ticked, bool Optional, string Text);

/// <summary>
/// The editor's answer to a dialog's post. For <see cref="GlassEditorAnswerKind.Confirm"/>
/// it carries the confirm post (its action and its one field) and the parts listed.
/// </summary>
internal sealed record GlassEditorAnswer(
    GlassEditorAnswerKind Kind,
    string Title,
    string? PageTitle,
    string? Action,
    IReadOnlyList<KeyValuePair<string, string>> Fields,
    IReadOnlyList<GlassEditorListed> Listed);

/// <summary>
/// The editor's dialogs, built as a browser builds them. Each dialog's own
/// load script alters its form before a person sees it; these do what those
/// scripts do, in their order, from the same page, and then take the fields
/// a browser posts. Where the page gives a second source for a value, the two
/// are compared, and a difference is a problem: the caller does not post a
/// dialog that has one. The messages name the page's own values only.
/// </summary>
internal static partial class GlassEditorDialogs
{
    private static readonly string[] ServedNames =
        ["Place", "Stateid", "RemarkP", "RemarkTxt", "RemarkTxtTime", "RemarkTxtLack", "Zuschlag_d", "Klarlack_d"];

    private static readonly (string Name, string Table)[] PaintSelects = [("Verf", "Verfids"), ("Meth", "Methids")];

    private static readonly string[] EnabledRemarks = ["RemarkTxt", "RemarkTxtTime"];

    private static readonly char[] ClassSeparators = [' ', '\t', '\n', '\f'];

    /// <summary>
    /// The part dialog (<c>comp.php</c>) of a part that can be painted: the
    /// fields a browser posts when <paramref name="button"/> (<c>BSS</c> Part,
    /// <c>BLL</c> Paint, <c>BSL</c> Part and Paint) is pressed with nothing
    /// changed. The load script's steps, in order: SetZKBoxes (the two paint
    /// tick boxes and their hidden twins, by paint state), SetPlaceBoxes (the
    /// side tick boxes from the served side), SetRepartValues (price and time
    /// for the operation and side), SetRepartRemarks (the remarks and the
    /// part number) and SetLackartValues (paint price and time).
    /// </summary>
    public static GlassEditorBuild PartPost(string html, string button)
    {
        ArgumentNullException.ThrowIfNull(html);
        var form = GlassEditorForm.Read(html, "Part");
        if (form is null)
        {
            return GlassEditorBuild.Failed("The page has no form named Part.");
        }

        var submitter = form.Controls.FirstOrDefault(control => control.Type == "submit" && control.Name == button);
        if (submitter is null)
        {
            return GlassEditorBuild.Failed("The form has no submit button named " + button + ".");
        }

        var problems = new List<string>();
        var notes = new List<string>();
        var served = ServedNames.ToDictionary(name => name, name => form.Field(name)?.Value, StringComparer.Ordinal);

        // SetZKBoxes
        var state = Number(served["Stateid"]);
        var stateIndex = IsInteger(state) && state is >= 0 and <= int.MaxValue ? (int)state : -1;
        var zStates = GlassEditorPages.SplitArray(html, "Zstates");
        var kStates = GlassEditorPages.SplitArray(html, "Kstates");
        var zDims = GlassEditorPages.SplitArray(html, "Zdimmes");
        var kDims = GlassEditorPages.SplitArray(html, "Kdimmes");
        if (zStates is not null && kStates is not null && zDims is not null && kDims is not null && IsInteger(state))
        {
            foreach (var (name, states, dims) in new[] { ("Zuschlag", zStates, zDims), ("Klarlack", kStates, kDims) })
            {
                if (form.Field(name) is { } box)
                {
                    box.Checked = Truthy(NumberAt(states, stateIndex));
                    box.Disabled = Truthy(NumberAt(dims, stateIndex));
                }
                if (form.Field(name + "_d") is { } twin && At(dims, stateIndex) is { } dim)
                {
                    twin.Value = dim;
                }
            }
        }
        else
        {
            problems.Add("The paint option tables (Zstates, Kstates, Zdimmes, Kdimmes) or the paint state are missing.");
        }

        // SetPlaceBoxes: a right-hand place reads the second of each pair of lines.
        var place = served["Place"] ?? " ";
        var placeIndex = PlaceBoxes(form, place);
        var places = GlassEditorPages.SplitArray(html, "PosPlaces");
        if (places is null || !places.Contains(place))
        {
            problems.Add("The served side \"" + place + "\" is not one the dialog offers (" + Offered(places) + ").");
        }

        // setBothState: a page that ticks "Both" itself is adding two sides at once.
        var both = BothDescriptor().Match(html);
        if (both.Success && both.Groups[1].Value.StartsWith('1'))
        {
            problems.Add("The dialog ticks \"Both\" sides itself.");
        }

        var data = form.Submit(submitter);
        var record = Record(data);
        var operation = data.Get("Repart");
        var gross = (At(record, 52) ?? string.Empty).Split('|');
        var paint = (At(record, 86) ?? string.Empty).Split('|');
        if (record.Length < 90)
        {
            problems.Add("The part record has " + Count(record.Length) + " lines; the builder reads up to line 87.");
        }
        if (record.Length != 90)
        {
            notes.Add("The part record has " + Count(record.Length) + " lines; both captured ones have 90.");
        }
        if (!SameAt(record, 4, operation))
        {
            problems.Add("The selected operation (" + Show(operation) + ") is not the record's (" + ShowAt(record, 4) + ").");
        }

        // SetRepartValues, SetRepartRemarks, SetLackartValues
        var remarkPrice = GlassEditorPages.StringArray(html, "RemarkPrice");
        var remarkMaterial = GlassEditorPages.StringArray(html, "RemarkMaterial");
        var lackRemark = GlassEditorPages.StringArray(html, "LackRemark");
        var areaIndex = form.Field("Bereich") is { IsSelect: true, SelectedIndex: >= 0 } area ? area.SelectedIndex : 0;
        if (!OemSelectionLocked().IsMatch(html))
        {
            problems.Add("The dialog offers a choice of part make (OemSelDis is not true).");
        }
        if (remarkPrice is null || remarkMaterial is null || lackRemark is null)
        {
            problems.Add("The remark lists (RemarkPrice, RemarkMaterial, LackRemark) are missing.");
        }

        var priceRemark = remarkPrice is null ? string.Empty : At(remarkPrice, placeIndex) ?? string.Empty;
        var materialRemark = remarkMaterial is null ? string.Empty : At(remarkMaterial, placeIndex) ?? string.Empty;
        var price = GlassEditorPages.Lookup(At(record, 6 + placeIndex), operation);
        var time = GlassEditorPages.Lookup(At(record, 8 + placeIndex), operation);
        var partNumber = (At(record, 13 + placeIndex) ?? string.Empty).Split('|')[0];
        var paintPrice = At(paint, 0);
        var paintTime = At(paint, 1);
        (string Name, string? Value)[] filled =
        [
            ("Price", price),
            ("Time", time),
            ("RemarkP", priceRemark),
            ("RemarkTxt", Remarks(priceRemark, materialRemark)),
            ("RemarkTxtTime", GlassEditorPages.Lookup(At(record, 19 + placeIndex), operation) ?? string.Empty),
            ("RemarkTxtLack", lackRemark is null ? string.Empty : At(lackRemark, areaIndex) ?? string.Empty),
            ("EtnrTxt", partNumber),
            ("LPrice", paintPrice),
            ("LTime", paintTime),
        ];
        if (!SameAt(gross, placeIndex, price) || !SameAt(gross, 2 + placeIndex, time))
        {
            problems.Add("Price and time by operation do not match the record's gross figures (" + ShowAt(record, 52) + ").");
        }
        foreach (var (name, value) in filled)
        {
            if (!data.Has(name))
            {
                problems.Add("The form has no field " + name + ".");
                continue;
            }
            if (value is null)
            {
                problems.Add("No value for " + name + " in the part record.");
                continue;
            }
            if (served.TryGetValue(name, out var was) && !string.IsNullOrEmpty(was) && was != value)
            {
                notes.Add(name + " is served as \"" + was + "\" and set by the script to \"" + value + "\".");
            }

            data.Set(name, value);
        }

        // SetLackStufe picks the paint level for the operation and material; the served selection
        // must already be that one. SetLackartValues reads paint price and time for that level;
        // they must be the pair the record carries.
        var levelMap = GlassEditorPages.SplitArray(html, "ArtLackMap");
        var material = Material().Match(html);
        var at = -1;
        for (var index = 0; levelMap is not null && index < levelMap.Length; index += 3)
        {
            if (levelMap[index] == operation)
            {
                at = index;
                break;
            }
        }
        if (levelMap is not null && levelMap.Length > 2 && at >= 0)
        {
            var wanted = At(levelMap, at + (material.Success && material.Groups[1].Value == "K" ? 2 : 1));
            List<string> levels = form.Field("Lackstufe") is { IsSelect: true } level
                ? level.Options.Select(option => option.Value).ToList()
                : [];
            if (wanted is not null && levels.Contains(wanted) && wanted != data.Get("Lackstufe"))
            {
                problems.Add("The script would select paint level " + wanted + "; the page serves " + Show(data.Get("Lackstufe")) + ".");
            }

            var levelKey = StateLetter(state) + Show(data.Get("Lackstufe"));
            var prices = PaintPrices().Match(html);
            var times = PaintTimes().Match(html);
            if (!prices.Success || !times.Success)
            {
                notes.Add("The paint price lists could not be read to check the record's pair.");
            }
            else if (areaIndex == 0)
            {
                var listedPrice = GlassEditorPages.Lookup(prices.Groups[1].Value, levelKey);
                var listedTime = GlassEditorPages.Lookup(times.Groups[1].Value, levelKey);
                if (!Same(listedPrice, paintPrice) || !Same(listedTime, paintTime))
                {
                    problems.Add("Paint price and time for " + levelKey + " (" + Show(listedPrice) + ", " + Show(listedTime)
                        + ") are not the record's pair (" + ShowAt(record, 86) + ").");
                }
            }
        }

        // FillVerfSelect, FillMethSelect: both selects are disabled when the paint state's entry
        // is below 3, and a disabled field is not posted. An enabled one is reported, not guessed.
        foreach (var (name, table) in PaintSelects)
        {
            if (!data.Has(name))
            {
                continue;
            }

            var ids = GlassEditorPages.SplitArray(html, table);
            var entry = ids is null ? double.NaN : NumberAt(ids, stateIndex);
            if (entry < 3)
            {
                data.Delete(name);
            }
            else
            {
                problems.Add("The select " + name + " would be enabled here (" + table + "[" + ShowNumber(state) + "] = "
                    + (ids is null ? "absent" : ShowAt(ids, stateIndex)) + ").");
            }
        }
        if (!PaintQuantityOff().IsMatch(html))
        {
            problems.Add("The dialog asks for a paint quantity.");
        }

        return new(null, form.Action, [.. data.Entries], problems, notes,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new GlassEditorSummary
            {
                Position = At(record, 0),
                PartCode = At(record, 1),
                Description = (At(record, 3) ?? string.Empty).Split('|')[0],
                Operation = operation,
                Offered = At(record, 5),
                Price = price,
                Hours = time,
                PartNumber = partNumber,
                Trade = data.Get("Craft"),
                Area = data.Get("Bereich"),
                PaintType = data.Get("Lackart"),
                PaintLevel = data.Get("Lackstufe"),
                PaintPrice = paintPrice,
                PaintHours = paintTime,
                Side = place,
                SidesOffered = places is null ? null : string.Join('|', places),
                RowPair = At(record, 87),
            });
    }

    /// <summary>
    /// The dialog of a part that cannot be painted, and of a special operation
    /// (<c>part.php</c>). Its load script sets the side tick boxes, then the
    /// price, time and remarks for the operation. It has one submit button
    /// (OK) and no paint fields. With no change this is the post a browser
    /// sends when OK is pressed on the dialog as served.
    /// </summary>
    public static GlassEditorBuild PlainPost(string html, GlassEditorChange? change = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        change ??= new GlassEditorChange();
        var form = GlassEditorForm.Read(html, "Part");
        if (form is null)
        {
            return GlassEditorBuild.Failed("The page has no form named Part.");
        }

        var submitter = form.Controls.FirstOrDefault(control => control.Type == "submit" && control.Name == "BS");
        if (submitter is null)
        {
            return GlassEditorBuild.Failed("The form has no submit button named BS.");
        }
        if (form.Named("Lackart").Any())
        {
            return GlassEditorBuild.Failed("This dialog has paint fields; it is a comp.php dialog.");
        }

        var problems = new List<string>();
        var notes = new List<string>();

        // SetPlaceBoxes
        var place = form.Field("Place")?.Value ?? " ";
        var placeIndex = PlaceBoxes(form, place);
        var places = GlassEditorPages.SplitArray(html, "PosPlaces");
        if (places is null || !(places.Contains(place) || place.Trim().Length == 0))
        {
            problems.Add("The served side \"" + place + "\" is not one the dialog offers (" + Offered(places) + ").");
        }

        var both = BothDescriptor().Match(html);
        if (both.Success && both.Groups[1].Value.StartsWith('1'))
        {
            problems.Add("The dialog ticks \"Both\" sides itself.");
        }

        // A person may tick "Both": the editor then opens the other side's dialog after this one.
        if (change.Both)
        {
            var box = form.Field("Both");
            var descriptor = both.Success ? both.Groups[1].Value : null;
            if (box is null || (descriptor is not null && (CharAt(descriptor, 1) == '1' || CharAt(descriptor, 2) == '1')))
            {
                problems.Add("The dialog does not offer \"Both\" sides.");
            }
            else
            {
                box.Checked = true;
            }
        }

        // The operation: the served selection, or another one the select offers.
        var select = form.Field("Repart");
        List<string> offered = select is { IsSelect: true }
            ? select.Options.Select(option => option.Value).ToList()
            : [];
        if (change.Operation is not null)
        {
            if (select is null || !offered.Contains(change.Operation) || change.Operation == "-")
            {
                problems.Add("The dialog does not offer the operation " + change.Operation + ".");
            }
            else
            {
                select.Value = change.Operation;
            }
        }

        var operation = select?.Value;

        // SetRepartValues and SetRepartRemarks read the script's own lists, one per side.
        (string Name, IReadOnlyList<string>? List)[] lists =
        [
            ("Prices", GlassEditorPages.SplitPairs(html, "Prices")),
            ("Times", GlassEditorPages.SplitPairs(html, "Times")),
            ("RemarkTimes", GlassEditorPages.SplitPairs(html, "RemarkTimes")),
            ("Etnr", GlassEditorPages.StringArray(html, "Etnr")),
            ("RemarkPrice", GlassEditorPages.StringArray(html, "RemarkPrice")),
            ("RemarkMaterial", GlassEditorPages.StringArray(html, "RemarkMaterial")),
        ];
        foreach (var (name, list) in lists)
        {
            if (list is null || list.Count < placeIndex + 1)
            {
                problems.Add("The script list " + name + " is missing.");
            }
        }
        if (!OemSelectionLocked().IsMatch(html))
        {
            problems.Add("The dialog offers a choice of part make (OemSelDis is not true).");
        }

        string Side(string name) =>
            Array.Find(lists, entry => entry.Name == name).List is { } list ? At(list, placeIndex) ?? string.Empty : string.Empty;

        var priceRemark = Side("RemarkPrice");
        var time = GlassEditorPages.Lookup(Side("Times"), operation);

        // "Renew" (AN) opens the old-for-new dialog; accepted as served, it writes the hours with
        // two decimals.
        if (change.Operation == "AN" && time is not null && !double.IsNaN(Number(time)))
        {
            time = Number(time).ToString("F2", CultureInfo.InvariantCulture);
            notes.Add("Renew goes through the old-for-new dialog, taken as served with no deduction.");
        }

        (string Name, string? Value)[] filled =
        [
            ("Price", GlassEditorPages.Lookup(Side("Prices"), operation)),
            ("Time", time),
            ("RemarkP", priceRemark),
            ("RemarkTxt", Remarks(priceRemark, Side("RemarkMaterial"))),
            ("RemarkTxtTime", GlassEditorPages.Lookup(Side("RemarkTimes"), operation) ?? string.Empty),
            ("EtnrTxt", Side("Etnr")),
        ];

        // The two remark boxes are served disabled and every captured post carries them.
        foreach (var name in EnabledRemarks)
        {
            if (form.Field(name) is { } remark)
            {
                remark.Disabled = false;
            }
        }

        var data = form.Submit(submitter);
        foreach (var (name, value) in filled)
        {
            if (!data.Has(name))
            {
                problems.Add("The form has no field " + name + ".");
                continue;
            }
            if (value is null)
            {
                problems.Add("No value for " + name + " for the operation " + Show(operation) + ".");
                continue;
            }

            data.Set(name, value);
        }

        // What a person may type: a price, hours, and (a special operation only) its description.
        var typed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (value, name) in new[] { (change.Price, "Price"), (change.Hours, "Time"), (change.Description, "Descr") })
        {
            if (value is null)
            {
                continue;
            }
            if (!data.Has(name))
            {
                problems.Add("The dialog has no field " + name + " to change.");
                continue;
            }
            if (form.Field(name) is { } box && (box.ReadOnly || box.Disabled))
            {
                problems.Add("The dialog does not let " + name + " be typed.");
                continue;
            }

            data.Set(name, value);
            typed[name] = value;
        }

        // CheckSubmit: more hours than Glass's own, where Glass's has a time for the operation,
        // opens a window that asks for a reason and holds the post back until one is given.
        if (typed.TryGetValue("Time", out var typedTime))
        {
            var own = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "OrigTimes"), placeIndex), operation);
            var database = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "DatabaseTimes"), placeIndex), operation);
            if (own is null || database is null)
            {
                problems.Add("The dialog's own times for " + Show(operation) + " could not be read to check the typed hours.");
            }
            else if (Number(database) != 0 && Number(own) != 0 && Number(typedTime) > Number(own))
            {
                if (string.IsNullOrEmpty(change.Reason))
                {
                    problems.Add("More hours than Glass's own (" + own + "): the dialog asks for a reason.");
                }
                else if (!data.Has("Reason"))
                {
                    problems.Add("The form has no field for the reason.");
                }
                else
                {
                    data.Set("Reason", ReasonLineBreaks().Replace(change.Reason, "|"));
                }
            }
        }

        var record = Record(data);
        var bruttos = data.Get("ANBruttos");
        var gross = (bruttos ?? string.Empty).Split('|');
        if (change.Operation is null)
        {
            if (!SameAt(record, 4, operation))
            {
                problems.Add("The selected operation (" + Show(operation) + ") is not the record's (" + ShowAt(record, 4) + ").");
            }
            if (Number(filled[0].Value) != NumberAt(gross, placeIndex) || Number(filled[1].Value) != NumberAt(gross, 2 + placeIndex))
            {
                // A browser posted a right-hand dialog whose gross hours were 0.00, so on the right
                // side this is reported and not refused.
                var text = "Price and time by operation do not match the served gross figures (" + Show(bruttos) + ").";
                (placeIndex == 1 ? notes : problems).Add(text);
            }
        }
        else
        {
            notes.Add("The operation was changed from " + ShowAt(record, 4) + " to " + Show(operation)
                + "; the gross figures and the record are posted as served.");
        }
        if (Number(data.Get("Price")) == 0 && Number(data.Get("Time")) == 0)
        {
            problems.Add("Price and hours are both zero; the dialog would ask before sending.");
        }

        return new(null, form.Action, [.. data.Entries], problems, notes, typed,
            new GlassEditorSummary
            {
                Position = At(record, 0),
                PartCode = At(record, 1),
                Description = data.Has("Descr") ? data.Get("Descr") : (At(record, 3) ?? string.Empty).Split('|')[0],
                Special = data.Has("Descr"),
                Operation = operation,
                Offered = string.Join('|', offered.Where(value => value != "-")),
                Price = data.Get("Price"),
                Hours = data.Get("Time"),
                PartNumber = Side("Etnr"),
                Trade = data.Get("Craft"),
                Side = place,
                SidesOffered = places is null ? null : string.Join('|', places),
            });
    }

    /// <summary>
    /// The answer to a dialog's post: the "Mandatory parts:" page, whose
    /// Continue button posts its own parameter list back, or the page that
    /// tells the editor to redraw. <paramref name="ticks"/> names optional
    /// listed parts to tick as well, by their code. The confirm post is built
    /// as the page's own clickCheckbox and clickButton build it: field 2 of a
    /// ticked listed part is 1, and field 3 of the pressed button is 1.
    /// </summary>
    public static GlassEditorAnswer AfterPost(string html, IReadOnlyCollection<string>? ticks = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        IReadOnlyCollection<string> ticked = ticks ?? [];
        var tokens = GlassEditorForm.Tokens(html);
        var title = (ElementText(tokens, "dialogtitle") ?? string.Empty).Trim();
        var parameters = GlassEditorPages.StringArray(html, "currentParameters");
        var form = GlassEditorForm.Read(html, "Part");
        if (parameters is null || form is null)
        {
            return new(
                Redraw().IsMatch(html) ? GlassEditorAnswerKind.Done : GlassEditorAnswerKind.Unknown,
                title, PageTitle(tokens), null, [], []);
        }

        var proceed = tokens.FirstOrDefault(token => token.Kind == GlassEditorTokenKind.Start
            && token.Name == "input"
            && string.Equals(token.Attribute("type"), "button", StringComparison.OrdinalIgnoreCase)
            && token.Attribute("externid") == "O");
        var id = proceed?.Attribute("id") ?? string.Empty;
        var position = Number(id.Length > 6 ? id[6..] : string.Empty);
        var at = IsInteger(position) && position is >= 0 and <= int.MaxValue ? (int)position : -1;
        if (proceed is null || string.IsNullOrEmpty(At(parameters, at)))
        {
            return new(GlassEditorAnswerKind.Unknown, title, null, null, [], []);
        }

        var chosen = parameters.Select(entry =>
        {
            var fields = entry.Split('|').ToList();
            if (fields[0] == "L" && fields.Count > 1 && ticked.Contains(fields[1]))
            {
                SetAt(fields, 2, "1");
            }
            return string.Join('|', fields);
        }).ToList();
        var descriptor = chosen[at].Split('|').ToList();
        SetAt(descriptor, 3, "1");
        chosen[at] = string.Join('|', descriptor);

        var listed = parameters
            .Where(entry => entry.StartsWith("L|", StringComparison.Ordinal))
            .Select(entry =>
            {
                var fields = entry.Split('|');
                return new GlassEditorListed(
                    fields[1],
                    At(fields, 2) == "1",
                    At(fields, 3) == "1",
                    WhiteSpace().Replace(At(fields, 4) ?? string.Empty, " ").Trim());
            })
            .ToList();
        return new(
            GlassEditorAnswerKind.Confirm, title, null, form.Action,
            [KeyValuePair.Create("Partx", string.Join('\n', chosen))], listed);
    }

    /// <summary>
    /// SetPlaceBoxes: ticks the side boxes the served side names and answers
    /// which of each pair of lines the dialog reads (1 for a right-hand side).
    /// </summary>
    private static int PlaceBoxes(GlassEditorForm form, string place)
    {
        var lead = place.Length > 0 ? place[..1] : string.Empty;
        var side = lead is "V" or "H" ? (place.Length > 1 ? place.Substring(1, 1) : string.Empty) : lead;
        foreach (var (name, on) in new[] { ("Place_V", lead == "V"), ("Place_H", lead == "H"), ("Place_L", side == "L"), ("Place_R", side == "R") })
        {
            if (form.Field(name) is { } box)
            {
                box.Checked = on;
            }
        }

        return side == "R" ? 1 : 0;
    }

    /// <summary>The part record the dialog carries in its hidden <c>Partx</c> field, one entry per line.</summary>
    private static string[] Record(GlassEditorFields data) =>
        (data.Get("Partx") ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string Remarks(string price, string material) =>
        string.Join("; ", new[] { price, material }.Where(text => text.Length > 0));

    private static string Offered(string[]? places) => places is null ? "absent" : string.Join('|', places);

    /// <summary>The text of the first element whose class list names <paramref name="className"/>.</summary>
    private static string? ElementText(List<GlassEditorToken> tokens, string className)
    {
        var start = tokens.FindIndex(token => token.Kind == GlassEditorTokenKind.Start
            && (token.Attribute("class") ?? string.Empty)
                .Split(ClassSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Contains(className));
        if (start < 0)
        {
            return null;
        }

        var name = tokens[start].Name;
        var depth = 1;
        var text = new StringBuilder();
        for (var index = start + 1; index < tokens.Count && depth > 0; index++)
        {
            var token = tokens[index];
            if (token.Kind == GlassEditorTokenKind.Text)
            {
                text.Append(token.Text);
            }
            else if (token.Name == name)
            {
                depth += token.Kind == GlassEditorTokenKind.Start ? 1 : -1;
            }
        }

        return text.ToString();
    }

    /// <summary>The page's title as a browser reports it, its white space collapsed.</summary>
    private static string PageTitle(List<GlassEditorToken> tokens)
    {
        var start = tokens.FindIndex(token => token.Kind == GlassEditorTokenKind.Start && token.Name == "title");
        return start >= 0 && start + 1 < tokens.Count && tokens[start + 1].Kind == GlassEditorTokenKind.Text
            ? GlassEditorForm.Collapse(tokens[start + 1].Text)
            : string.Empty;
    }

    private static string? At(IReadOnlyList<string>? list, int index) =>
        list is not null && index >= 0 && index < list.Count ? list[index] : null;

    private static void SetAt(List<string> list, int index, string value)
    {
        while (list.Count <= index)
        {
            list.Add(string.Empty);
        }

        list[index] = value;
    }

    private static char CharAt(string text, int index) => index < text.Length ? text[index] : '\0';

    /// <summary>Whether the list holds <paramref name="value"/> at the index; a missing entry is never equal.</summary>
    private static bool SameAt(string[] list, int index, string? value) => Same(At(list, index), value);

    private static bool Same(string? first, string? second) => first is not null && second is not null && first == second;

    private static string Show(string? value) => value ?? "null";

    private static string ShowAt(string[] list, int index) => At(list, index) ?? "undefined";

    private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

    private static string ShowNumber(double value) =>
        double.IsNaN(value) ? "NaN"
        : IsInteger(value) && Math.Abs(value) < 1e15 ? ((long)value).ToString(CultureInfo.InvariantCulture)
        : value.ToString("R", CultureInfo.InvariantCulture);

    private static bool IsInteger(double value) => double.IsFinite(value) && Math.Floor(value) == value;

    private static bool Truthy(double value) => value != 0 && !double.IsNaN(value);

    /// <summary>
    /// <c>String.fromCharCode(97 + state)</c>: the paint state as the letter
    /// its price list keys use (state 0 is <c>a</c>).
    /// </summary>
    private static string StateLetter(double state)
    {
        var code = 97 + state;
        if (!double.IsFinite(code) || Math.Abs(code) > 1e15)
        {
            return "\0";
        }

        var value = (long)Math.Truncate(code) % 65536;
        return new string((char)(value < 0 ? value + 65536 : value), 1);
    }

    /// <summary>
    /// JavaScript's <c>Number(text)</c> for the decimal text the editor's
    /// pages hold: null and blank are 0, anything that is not a decimal
    /// number is NaN.
    /// </summary>
    private static double Number(string? text)
    {
        if (text is null)
        {
            return 0;
        }

        var trimmed = text.Trim();
        return trimmed.Length == 0 ? 0
            : trimmed is "Infinity" or "+Infinity" ? double.PositiveInfinity
            : trimmed is "-Infinity" ? double.NegativeInfinity
            : DecimalNumber().IsMatch(trimmed) ? double.Parse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture)
            : double.NaN;
    }

    /// <summary><c>Number(list[index])</c>: a missing entry is NaN.</summary>
    private static double NumberAt(string[]? list, int index) =>
        At(list, index) is { } text ? Number(text) : double.NaN;

    [GeneratedRegex(@"var\s+bothDescriptor\s*=\s*""([^""]*)""", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex BothDescriptor();

    [GeneratedRegex(@"var\s+OemSelDis\s*=\s*true", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex OemSelectionLocked();

    [GeneratedRegex(@"var\s+Mater\s*=\s*""([^""]*)""", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Material();

    [GeneratedRegex(@"var\s+LPrices\s*=\s*\[\s*\[\s*""([^""]*)""", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PaintPrices();

    [GeneratedRegex(@"var\s+LTimes\s*=\s*\[\s*\[\s*""([^""]*)""", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PaintTimes();

    [GeneratedRegex(@"var\s+QuantityEnableLack\s*=\s*false", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PaintQuantityOff();

    [GeneratedRegex(@"frame_selected\.Actual\s*=\s*false", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Redraw();

    [GeneratedRegex(@"\r*\n", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ReasonLineBreaks();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex WhiteSpace();

    [GeneratedRegex(@"^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex DecimalNumber();
}
