using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// What a person may change on a <c>part.php</c> dialog before pressing OK:
/// another operation the dialog offers, a typed price, hours, overlap hours
/// (<c>Ctime</c>) or (a special operation only) description, the reason the
/// dialog asks for when the hours are above Glass's own, and the "Both"
/// sides tick. On a <c>comp.php</c> dialog only the operation may be
/// changed. <see cref="ConfirmNoFigures"/> is a person's OK to the question
/// the dialog asks before it sends a part whose price and hours are both
/// zero; the OK changes nothing in the post. Typed text is posted as given,
/// as the browser posts it (19 November 2026: <c>0.5</c>, <c>0.50</c>,
/// <c>2.80</c> and <c>1234</c> each reached the post unchanged). Nothing set
/// is the dialog as served.
/// </summary>
internal sealed record GlassEditorChange(
    string? Operation = null,
    string? Price = null,
    string? Hours = null,
    string? Description = null,
    string? Reason = null,
    bool Both = false,
    bool ConfirmNoFigures = false,
    string? OverlapHours = null);

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

    /// <summary>
    /// The right-hand part's code, the record's second code line: a part
    /// with sides carries the left code first and the right code second (the
    /// right-hand dialog of 18 November 2026 served 1012401 then 1012402);
    /// a part with no side carries the same code twice.
    /// </summary>
    public string? RightPartCode { get; init; }

    public string? Description { get; init; }

    public bool? Special { get; init; }

    public string? Operation { get; init; }

    public string? Offered { get; init; }

    public string? Price { get; init; }

    public string? Hours { get; init; }

    /// <summary>The price the dialog served for the operation sent, before anything was typed (R1206).</summary>
    public string? ServedPrice { get; init; }

    /// <summary>The hours the dialog served for the operation sent, before anything was typed (R1206).</summary>
    public string? ServedHours { get; init; }

    /// <summary>The overlap hours (<c>Ctime</c>) the post sends; null where the dialog has none.</summary>
    public string? OverlapHours { get; init; }

    /// <summary>The overlap hours the dialog served (R1206).</summary>
    public string? ServedOverlapHours { get; init; }

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
    /// <summary>
    /// The <c>glass.*</c> code of the one problem that is a typed input the
    /// dialog does not take (R1206): a reason it asks for and was not given,
    /// one it does not ask for, or overlap hours above the hours. Null
    /// otherwise, when the refusal is the editor's own.
    /// </summary>
    public string? RefusalCode { get; init; }

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
    IReadOnlyList<GlassEditorListed> Listed)
{
    /// <summary>
    /// After a post with "Both" ticked, the other side's dialog the editor's
    /// list opens next (its <c>BothNext</c>), as the answer names it; null
    /// when the answer names none.
    /// </summary>
    public GlassEditorNext? Next { get; init; }
}

/// <summary>
/// The other side's dialog an answer sends the editor's list to after a post
/// with "Both" ticked (<c>BothNext</c>): its rows, as the dialog is asked for
/// them (<c>1783|ER|1786|AE|1792</c>), the side it serves, and the extra
/// address parameters the answer passes (empty in every capture).
/// </summary>
internal sealed record GlassEditorNext(string Rows, string Side, string Parameters);

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
    /// <remarks>
    /// <paramref name="change"/> may name another operation the dialog
    /// offers, as a person chooses it in the list: the dialog's ChangeRepart
    /// then sets the price, time and remarks for that operation from the
    /// script's own lists (SetRepartValues, SetRepartRemarks), and the paint
    /// level the script's ArtLackMap names for it with that level's paint
    /// price and time (SetLackStufe, SetLackartValues); the part record and
    /// its gross figures are posted as served. Pressing Part or Part and
    /// Paint then runs CheckSubmit's two questions: price and hours both zero
    /// (answered only by <see cref="GlassEditorChange.ConfirmNoFigures"/>)
    /// and more hours than Glass's own (never answered here). Judged
    /// offline on 18 November 2026: each captured dialog's own script, run
    /// in a browser with Repair and with Uninstall and install chosen, sent
    /// these posts byte for byte.
    /// </remarks>
    public static GlassEditorBuild PartPost(string html, string button, GlassEditorChange? change = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        change ??= new GlassEditorChange();
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

        // Choosing the operation the dialog already serves changes nothing, as in the editor.
        if (change.Operation is { } chosenAgain && form.Field("Repart") is { IsSelect: true } servedList && servedList.Value == chosenAgain)
        {
            change = change with { Operation = null };
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

        // ChangeRepart: a person may choose another operation the dialog offers; nothing else.
        if (change.Price is not null || change.Hours is not null || change.Description is not null
            || change.Reason is not null || change.OverlapHours is not null || change.Both)
        {
            problems.Add("This dialog is built with no change but the operation.");
        }

        var changed = change.Operation is not null;
        if (change.Operation is { } chosen)
        {
            var select = form.Field("Repart");
            List<string> offered = select is { IsSelect: true }
                ? select.Options.Select(option => option.Value).ToList()
                : [];
            if (ChangeProblem(html, offered, chosen) is { } refused)
            {
                problems.Add(refused);
            }
            else
            {
                select!.Value = chosen;
            }
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
        if (!changed && !SameAt(record, 4, operation))
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
        var remarkTime = GlassEditorPages.Lookup(At(record, 19 + placeIndex), operation) ?? string.Empty;
        if (!changed && (!SameAt(gross, placeIndex, price) || !SameAt(gross, 2 + placeIndex, time)))
        {
            problems.Add("Price and time by operation do not match the record's gross figures (" + ShowAt(record, 52) + ").");
        }
        if (changed)
        {
            // ChangeRepart's SetRepartValues and SetRepartRemarks read the script's own lists;
            // the record carries the same figures, and a difference is a problem.
            var prices = At(GlassEditorPages.SplitPairs(html, "Prices"), placeIndex);
            var times = At(GlassEditorPages.SplitPairs(html, "Times"), placeIndex);
            var remarkTimes = At(GlassEditorPages.SplitPairs(html, "RemarkTimes"), placeIndex);
            if (prices is null || times is null || remarkTimes is null)
            {
                problems.Add("The script lists Prices, Times or RemarkTimes are missing.");
            }
            if (GlassEditorPages.Lookup(prices, operation) != price || GlassEditorPages.Lookup(times, operation) != time)
            {
                problems.Add("The script's price and time for " + Show(operation) + " are not the record's.");
            }

            remarkTime = GlassEditorPages.Lookup(remarkTimes, operation) ?? string.Empty;
            notes.Add("The operation was changed from " + ShowAt(record, 4) + " to " + Show(operation)
                + "; the record and the gross figures are posted as served.");
        }

        (string Name, string? Value)[] filled =
        [
            ("Price", price),
            ("Time", time),
            ("RemarkP", priceRemark),
            ("RemarkTxt", Remarks(priceRemark, materialRemark)),
            ("RemarkTxtTime", remarkTime),
            ("RemarkTxtLack", lackRemark is null ? string.Empty : At(lackRemark, areaIndex) ?? string.Empty),
            ("EtnrTxt", partNumber),
            ("LPrice", paintPrice),
            ("LTime", paintTime),
        ];
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
        if (changed && levelMap is not null && levelMap.Length > 2 && at >= 0)
        {
            // SetLackStufe for the chosen operation: the level the map names for the material,
            // if the level list (as FillLackstufeSelect built it at load) has it; then
            // SetLackartValues reads that level's paint price and time in the paint area.
            var mater = form.Field("Mater") is { } materialField ? materialField.Value
                : material.Success ? material.Groups[1].Value
                : null;
            if (mater is not ("B" or "K"))
            {
                problems.Add("The material code (" + Show(mater) + ") is neither B nor K.");
            }
            else if (PaintOfLevel(html, areaIndex, data.Get("Lackart"), At(levelMap, at + (mater == "K" ? 2 : 1)), state, problems)
                is { } level)
            {
                data.Set("Lackstufe", level.Level);
                data.Set("LPrice", level.Price);
                data.Set("LTime", level.Time);
                paintPrice = level.Price;
                paintTime = level.Time;
            }
        }
        else if (levelMap is not null && levelMap.Length > 2 && at >= 0)
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

        // CheckSubmit, for Part and for Part and Paint, on a changed operation.
        if (changed && button is "BSS" or "BSL")
        {
            ZeroPrompt(html, data, change, problems, notes);
            if (ReasonAsked(html, operation, placeIndex, data.Get("Time")) is { } reason)
            {
                problems.Add(reason);
            }
        }

        return new(null, form.Action, [.. data.Entries], problems, notes,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new GlassEditorSummary
            {
                Position = At(record, 0),
                PartCode = At(record, 1),
                RightPartCode = At(record, 2),
                Description = (At(record, 3) ?? string.Empty).Split('|')[0],
                Operation = operation,
                Offered = At(record, 5),
                Price = price,
                Hours = time,
                ServedPrice = price,
                ServedHours = time,
                OverlapHours = data.Get("Ctime"),
                ServedOverlapHours = form.Field("Ctime")?.Value,
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

        // Choosing the operation the dialog already serves changes nothing, as in the editor.
        if (change.Operation is { } chosenAgain && form.Field("Repart") is { IsSelect: true } servedList && servedList.Value == chosenAgain)
        {
            change = change with { Operation = null };
        }

        var problems = new List<string>();
        var notes = new List<string>();
        var servedOverlap = form.Field("Ctime")?.Value;

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
            // Renew (AN) goes through the old-for-new dialog, taken as served (seen once, 18
            // November 2026); any other operation must not reload the dialog.
            var refused = change.Operation == "AN"
                ? (offered.Contains("AN") ? null : "The dialog does not offer the operation AN.")
                : ChangeProblem(html, offered, change.Operation);
            if (select is null || refused is not null)
            {
                problems.Add(refused ?? "The dialog does not offer the operation " + change.Operation + ".");
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
        var price = GlassEditorPages.Lookup(Side("Prices"), operation);

        // "Renew" (AN) opens the old-for-new window with the side's gross price and hours
        // (ANBruttos, as OpenOldfrnewWindow reads them); taken as served, with no deduction, its OK
        // writes them back as the price and the hours. Judged offline on 19 November 2026 on six
        // captured part.php dialogs, among them a right-hand one whose gross hours are not its Times.
        if (change.Operation == "AN")
        {
            var oldForNew = (form.Field("ANBruttos")?.Value ?? string.Empty).Split('|');
            price = At(oldForNew, placeIndex);
            time = At(oldForNew, 2 + placeIndex);
            notes.Add("Renew goes through the old-for-new dialog, taken as served with no deduction.");
        }

        (string Name, string? Value)[] filled =
        [
            ("Price", price),
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

        // What a person may type: a price, hours, overlap hours (R1206) and (a special operation
        // only) its description. Outside work, where a dialog has it ticked, greys the overlap hours.
        var typed = new Dictionary<string, string>(StringComparer.Ordinal);
        string? coded = null;
        if (change.OverlapHours is not null && form.Field("fremdarbeit") is { Checked: true })
        {
            problems.Add("The dialog has outside work ticked, which greys the overlap hours.");
        }

        foreach (var (value, name) in new[]
            { (change.Price, "Price"), (change.Hours, "Time"), (change.OverlapHours, "Ctime"), (change.Description, "Descr") })
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

        // CheckSubmit: more hours than Glass's own, where Glass's has a time for the operation and
        // no reason is held yet, opens a window that asks for a reason and holds the post back
        // until one is given. The window's own Submit trims the spaces at either end; CheckSubmit
        // turns line breaks into bars. A reason the dialog does not ask for is not sent (R1206).
        var reasonSent = false;
        if (typed.TryGetValue("Time", out var typedTime))
        {
            var own = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "OrigTimes"), placeIndex), operation);
            var database = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "DatabaseTimes"), placeIndex), operation);
            var reason = change.Reason?.Trim(' ');
            if (own is null || database is null)
            {
                problems.Add("The dialog's own times for " + Show(operation) + " could not be read to check the typed hours.");
            }
            else if (Number(database) != 0 && Number(own) != 0 && Number(typedTime) > Number(own)
                && string.IsNullOrEmpty(data.Get("Reason")))
            {
                if (string.IsNullOrEmpty(reason))
                {
                    problems.Add("More hours than Glass's own (" + own + "): the dialog asks for a reason.");
                    coded = GlassAutomationPositionPolicy.ReasonRequiredCode;
                }
                else if (!data.Has("Reason"))
                {
                    problems.Add("The form has no field for the reason.");
                }
                else
                {
                    data.Set("Reason", ReasonLineBreaks().Replace(reason, "|"));
                    reasonSent = true;
                }
            }
        }
        if (change.Reason is not null && !reasonSent && coded is null)
        {
            problems.Add("A reason is given, but the dialog asks for none.");
            coded = GlassAutomationPositionPolicy.ReasonNotAskedCode;
        }
        if (typed.Count > 0 && Number(data.Get("Ctime")) > Number(data.Get("Time")))
        {
            // The return's reader refuses a whole export with more overlap time than time.
            problems.Add("Overlap hours (" + Show(data.Get("Ctime")) + ") are more than the hours (" + Show(data.Get("Time")) + ").");
            coded = GlassAutomationPositionPolicy.OverlapAboveHoursCode;
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
            if (!typed.ContainsKey("Time") && ReasonAsked(html, operation, placeIndex, data.Get("Time")) is { } reason)
            {
                problems.Add(reason);
            }
        }
        if (Number(data.Get("Price")) == 0 && Number(data.Get("Time")) == 0)
        {
            // A special operation (Descr) is never sent with both zero; a part's dialog asks.
            if (data.Has("Descr"))
            {
                problems.Add(ZeroAsked);
            }
            else
            {
                ZeroPrompt(html, data, change, problems, notes);
            }
        }

        return new(null, form.Action, [.. data.Entries], problems, notes, typed,
            new GlassEditorSummary
            {
                Position = At(record, 0),
                PartCode = At(record, 1),
                RightPartCode = At(record, 2),
                Description = data.Has("Descr") ? data.Get("Descr") : (At(record, 3) ?? string.Empty).Split('|')[0],
                Special = data.Has("Descr"),
                Operation = operation,
                Offered = string.Join('|', offered.Where(value => value != "-")),
                Price = data.Get("Price"),
                Hours = data.Get("Time"),
                ServedPrice = price,
                ServedHours = time,
                OverlapHours = data.Get("Ctime"),
                ServedOverlapHours = servedOverlap,
                PartNumber = Side("Etnr"),
                Trade = data.Get("Craft"),
                Side = place,
                SidesOffered = places is null ? null : string.Join('|', places),
            })
        {
            RefusalCode = problems.Count == 1 ? coded : null,
        };
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
                title, PageTitle(tokens), null, [], [])
            {
                Next = NextOf(html),
            };
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
            if (fields[0] == "L" && fields.Count > 3 && fields[3] == "1" && ticked.Contains(fields[1]))
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

    private const string ZeroAsked = "Price and hours are both zero; the dialog would ask before sending.";

    /// <summary>
    /// The answer's call to the list's <c>BothNext</c>, as the list runs it:
    /// the dialog it opens is the right-hand rows when the first side was
    /// left, else the left-hand ones, with the answer's <c>Params</c>. Null
    /// when the answer makes no such call.
    /// </summary>
    private static GlassEditorNext? NextOf(string html)
    {
        var call = BothNextCall().Match(html);
        if (!call.Success)
        {
            return null;
        }

        var first = call.Groups[3].Value;
        var parameters = BothNextParameters().Match(html);
        return new GlassEditorNext(
            first == "L" ? call.Groups[2].Value : call.Groups[1].Value,
            first == "L" ? "R" : "L",
            parameters.Success ? parameters.Groups[1].Value : string.Empty);
    }

    /// <summary>
    /// A dialog as a person sees it, read as a form, posting nothing (R1206):
    /// its kind, what it is for, each operation its list offers with the
    /// price, hours and Glass's own hours its script serves for the served
    /// side, each field a person sees with Glass's own label, its value as the
    /// load script leaves it and its choices, the paint section and the
    /// buttons. With <paramref name="button"/> or <paramref name="change"/>,
    /// also what the post built for them would send and what the dialog or
    /// the editor would ask on the way (<see cref="GlassDialogSend"/>), and
    /// that build, which the caller checks as an add checks it. Null when the
    /// page has no dialog form.
    /// </summary>
    public static (GlassDialogPreview? Preview, GlassEditorBuild? Build) Preview(
        string html, string? button = null, GlassEditorChange? change = null)
    {
        ArgumentNullException.ThrowIfNull(html);
        var form = GlassEditorForm.Read(html, "Part");
        if (form is null)
        {
            return (null, null);
        }

        var paintable = form.Named("Lackart").Any();
        var special = !paintable && form.Field("Descr") is not null;
        var served = paintable ? PartPost(html, "BSS") : PlainPost(html);
        var summary = served.Summary;
        var posted = served.Fields;
        var placeIndex = PlaceIndex(form.Field("Place")?.Value ?? " ");
        var tokens = GlassEditorForm.Tokens(html);

        // The operations: Glass's code and text, and the script's own figures for the served side.
        var select = form.Field("Repart");
        List<string> offered = select is { IsSelect: true } ? select.Options.Select(option => option.Value).ToList() : [];
        var prices = At(GlassEditorPages.SplitPairs(html, "Prices"), placeIndex);
        var times = At(GlassEditorPages.SplitPairs(html, "Times"), placeIndex);
        var own = At(GlassEditorPages.SplitPairs(html, "OrigTimes"), placeIndex);
        IReadOnlyList<GlassEditorOption> options = select is { IsSelect: true } ? select.Options : [];
        var operations = options
            .Where(option => option.Value != "-")
            .Select(option => new GlassDialogOperation(
                option.Value,
                GlassEditorForm.Collapse(option.Text).Trim(),
                Amount(GlassEditorPages.Lookup(prices, option.Value)),
                Amount(GlassEditorPages.Lookup(times, option.Value)),
                Amount(GlassEditorPages.Lookup(own, option.Value)),
                option.Value == select!.Value,
                !special && Choosable(html, offered, option.Value, paintable)
                    && GlassAutomationPositionPolicy.OperationRefusal(option.Value) is null)
            {
                // Offered and buildable, but the return cannot read it back (R1206; issue 2155).
                RefusedCode = !special && Choosable(html, offered, option.Value, paintable)
                    ? GlassAutomationPositionPolicy.OperationRefusal(option.Value)
                    : null,
            })
            .ToList();

        var both = BothDescriptor().Match(html);
        var descriptor = both.Success ? both.Groups[1].Value : null;
        var bothOffered = !paintable && !special && form.Field("Both") is not null
            && (descriptor is null || (CharAt(descriptor, 1) != '1' && CharAt(descriptor, 2) != '1'));
        var fields = new List<GlassDialogField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var control in form.Controls)
        {
            if (control.Name is not { Length: > 0 } name || control.Type == "hidden" || control.IsButton || !seen.Add(name))
            {
                continue;
            }

            var group = form.Named(name).Count() > 1;
            var sent = posted.Where(pair => pair.Key == name).Select(pair => pair.Value).FirstOrDefault();
            string? value;
            IReadOnlyList<GlassDialogChoice>? choices = null;
            if (control.Type is "checkbox" or "radio")
            {
                // A tick box's value is whether it is ticked; a group of radios answers the ticked one's value.
                var ticked = sent is not null || control.Checked;
                value = group
                    ? sent ?? form.Named(name).FirstOrDefault(member => member.Checked)?.Value
                    : ticked ? "true" : "false";
                if (group)
                {
                    choices = form.Named(name).Select(member => new GlassDialogChoice(member.Value, member.Value)).ToList();
                }
            }
            else
            {
                value = sent ?? control.Value;
                if (control.IsSelect)
                {
                    choices = control.Options
                        .Select(option => new GlassDialogChoice(option.Value, GlassEditorForm.Collapse(option.Text).Trim()))
                        .ToList();
                }
            }

            // The typed figures (R1206): on a dialog of a part that cannot be painted or of a
            // special operation, where the box may be typed in.
            var input = paintable || control.Disabled || control.ReadOnly ? null : TypedInput(name);
            var changeable = name switch
            {
                "Repart" => operations.Exists(operation => operation.Changeable && !operation.Served),
                "Both" => bothOffered,
                _ => input is not null,
            };
            fields.Add(new GlassDialogField(name, Label(tokens, name), value, choices, control.Disabled, changeable)
            {
                Input = input,
                ReasonAbove = input == "inputs.hours" ? ReasonLimit(html, select?.Value, placeIndex) : null,
            });
        }

        var buttons = form.Controls
            .Where(control => control.IsButton && control.Name is { Length: > 0 } && !control.Disabled)
            .Select(control => new GlassDialogChoice(control.Name!, GlassEditorForm.Collapse(control.Value).Trim()))
            .ToList();
        var paint = paintable && summary is not null
            ? new GlassDialogPaint(
                Choice(form.Field("Bereich"), summary.Area),
                Choice(form.Field("Lackart"), summary.PaintType),
                Choice(form.Field("Lackstufe"), summary.PaintLevel),
                Amount(summary.PaintPrice),
                Amount(summary.PaintHours))
            : null;
        var side = (summary?.Side ?? string.Empty).Trim();
        var preview = new GlassDialogPreview(
            special ? "special_operation" : paintable ? "part_paintable" : "part",
            summary?.Description?.Trim(),
            summary?.PartNumber is { } number && number.Trim().Length > 0 ? number.Trim() : null,
            side == "R" ? summary?.RightPartCode : summary?.PartCode,
            side.Length == 0 ? null : side,
            operations,
            fields,
            paint,
            buttons);
        if (button is null && change is null)
        {
            return (preview, served);
        }

        // What an add with these inputs would send, and each question on the way.
        var pressed = paintable ? button ?? "BSS" : "BS";
        var build = paintable ? PartPost(html, pressed, change) : PlainPost(html, change);
        var built = build.Summary;
        var questions = new List<GlassDialogQuestion>();
        if (!special && pressed != "BLL" && Number(built?.Price) == 0 && Number(built?.Hours) == 0)
        {
            var force = ForceNonZero().Match(html);
            var message = ZeroMessage().Match(html);
            var forced = force.Success && Number(force.Groups[1].Value) != 0;
            questions.Add(new GlassDialogQuestion(
                "no_figures",
                message.Success ? message.Groups[forced ? 1 : 2].Value : null,
                !forced && change?.ConfirmNoFigures == true));
        }
        if (pressed != "BLL"
            && ReasonAsked(html, built?.Operation, placeIndex, built?.Hours) is { } reason
            && reason.StartsWith("More hours", StringComparison.Ordinal))
        {
            questions.Add(new GlassDialogQuestion("reason", null, !string.IsNullOrWhiteSpace(change?.Reason)));
        }
        if (change?.Both == true)
        {
            questions.Add(new GlassDialogQuestion("other_side", null, true));
        }
        if (!special)
        {
            questions.Add(new GlassDialogQuestion("mandatory_parts", null, true));
        }

        var code = build.Error is not null ? GlassFailure.EditorDialog
            : build.Problems.Count > 0 ? build.RefusalCode ?? GlassFailure.EditorRefused
            : null;
        return (preview with
        {
            Sends = new GlassDialogSend(
                buttons.Find(item => item.Value == pressed)?.Text ?? pressed,
                built?.Operation,
                Amount(built?.Price),
                Amount(built?.Hours),
                paintable ? built?.PaintLevel : null,
                paintable ? Amount(built?.PaintPrice) : null,
                paintable ? Amount(built?.PaintHours) : null,
                change?.Both == true,
                questions,
                code)
            {
                OverlapHours = Amount(built?.OverlapHours),
            },
        }, build);
    }

    /// <summary>The member of the act's inputs that types in a dialog's box (R1206), or null for any other field.</summary>
    private static string? TypedInput(string name) => name switch
    {
        "Price" => "inputs.price",
        "Time" => "inputs.hours",
        "Ctime" => "inputs.overlapHours",
        _ => null,
    };

    /// <summary>
    /// Glass's own hours for the operation and side, above which CheckSubmit
    /// asks for a reason; null where it asks none (Glass's has no hours of its
    /// own for the operation) or its times cannot be read.
    /// </summary>
    private static decimal? ReasonLimit(string html, string? operation, int placeIndex)
    {
        var own = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "OrigTimes"), placeIndex), operation);
        var database = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "DatabaseTimes"), placeIndex), operation);
        return own is not null && database is not null && Number(database) != 0 && Number(own) != 0 ? Amount(own) : null;
    }

    /// <summary>
    /// Whether a person may choose the operation without the dialog reloading
    /// or opening a window this builder does not answer: what
    /// <see cref="ChangeProblem"/> allows, and on a <c>part.php</c> dialog
    /// Renew (<c>AN</c>), taken through the old-for-new window as served.
    /// </summary>
    private static bool Choosable(string html, List<string> offered, string operation, bool paintable) =>
        !paintable && operation == "AN"
            ? offered.Contains("AN")
            : ChangeProblem(html, offered, operation) is null;

    /// <summary>Which of each pair of lines a dialog reads for its served side: 1 for a right-hand side.</summary>
    private static int PlaceIndex(string place)
    {
        var lead = place.Length > 0 ? place[..1] : string.Empty;
        var side = lead is "V" or "H" ? (place.Length > 1 ? place.Substring(1, 1) : string.Empty) : lead;
        return side == "R" ? 1 : 0;
    }

    /// <summary>The selected value of a list, with Glass's own text for it.</summary>
    private static GlassDialogChoice? Choice(GlassEditorControl? select, string? value) =>
        value is null
            ? null
            : new GlassDialogChoice(
                value,
                select?.Options.FirstOrDefault(option => option.Value == value) is { } option
                    ? GlassEditorForm.Collapse(option.Text).Trim()
                    : value);

    /// <summary>Glass's own label for a field: the text of the dialog's <c>ID_</c> span for it, or null.</summary>
    private static string? Label(List<GlassEditorToken> tokens, string name)
    {
        var start = tokens.FindIndex(token => token.Kind == GlassEditorTokenKind.Start
            && token.Attribute("id") == "ID_" + name);
        if (start < 0)
        {
            return null;
        }

        var tag = tokens[start].Name;
        var depth = 1;
        var text = new StringBuilder();
        for (var index = start + 1; index < tokens.Count && depth > 0; index++)
        {
            var token = tokens[index];
            if (token.Kind == GlassEditorTokenKind.Text)
            {
                text.Append(token.Text);
            }
            else if (token.Name == tag)
            {
                depth += token.Kind == GlassEditorTokenKind.Start ? 1 : -1;
            }
        }

        var label = GlassEditorForm.Collapse(text.ToString().Replace(' ', ' ')).Trim();
        return label.Length == 0 ? null : label;
    }

    /// <summary>A figure as the dialog serves it, a zero kept; null when it gives none or none that reads as a number.</summary>
    private static decimal? Amount(string? served) =>
        decimal.TryParse(served, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// An operation a person may choose without the dialog reloading or
    /// opening another window, or why not: <c>DD</c> reloads the dialog,
    /// <c>AN</c> opens the old-for-new dialog, and an operation the script's
    /// SubClassMap marks <c>WS</c> reloads a <c>part.php</c> dialog.
    /// </summary>
    private static string? ChangeProblem(string html, List<string> offered, string operation)
    {
        if (!offered.Contains(operation) || operation == "-")
        {
            return "The dialog does not offer the operation " + operation + ".";
        }
        if (operation == "DD")
        {
            return "Choosing DD reloads the dialog.";
        }
        if (operation == "AN")
        {
            return "Choosing AN opens the old-for-new dialog.";
        }

        var classes = GlassEditorPages.SplitArray(html, "SubClassMap") ?? [];
        for (var index = 1; index < classes.Length; index += 2)
        {
            if (classes[index] == "WS" && classes[index - 1] == operation)
            {
                return "Choosing " + operation + " reloads the dialog.";
            }
        }

        return JudgedOperations.Contains(operation)
            ? null
            : "Choosing " + operation + " has not been judged against a dialog's own script.";
    }

    /// <summary>
    /// The operations a captured dialog's own script was seen to post when
    /// chosen, byte for byte as this builder posts them (19 November 2026,
    /// R1206: 13 captured dialogs driven offline, each operation they offer;
    /// Renew on <c>part.php</c> only). Any other is refused until it has been.
    /// </summary>
    private static readonly HashSet<string> JudgedOperations =
        new(StringComparer.Ordinal) { "AD", "AE", "AN", "E", "ET", "HO", "I", "IT", "KM", "KS", "P", "UN", "V", "Z" };

    /// <summary>
    /// CheckSubmit's question when price and hours are both zero. The page
    /// says whether a person may go on at all (<c>forceNonZero</c>) and what
    /// it asks (<c>messageString</c>); with no
    /// <see cref="GlassEditorChange.ConfirmNoFigures"/> the question is a
    /// problem, and with it a note: a person's OK sends the form as it
    /// stands.
    /// </summary>
    private static void ZeroPrompt(
        string html, GlassEditorFields data, GlassEditorChange change, List<string> problems, List<string> notes)
    {
        if (Number(data.Get("Price")) != 0 || Number(data.Get("Time")) != 0)
        {
            return;
        }

        var force = ForceNonZero().Match(html);
        var message = ZeroMessage().Match(html);
        if (!force.Success || !message.Success)
        {
            problems.Add(ZeroAsked);
        }
        else if (Number(force.Groups[1].Value) != 0)
        {
            problems.Add("Price and hours are both zero, and the dialog does not send without one: \""
                + message.Groups[1].Value + "\"");
        }
        else if (!change.ConfirmNoFigures)
        {
            problems.Add("Price and hours are both zero; the dialog asks: \"" + message.Groups[2].Value + "\"");
        }
        else
        {
            notes.Add("Price and hours are both zero; the dialog asked \"" + message.Groups[2].Value + "\" and was answered OK.");
        }
    }

    /// <summary>
    /// CheckSubmit's reason window: more hours than Glass's own for the
    /// operation, where Glass's has a time for it, are held back until a
    /// reason is given. Null when the dialog would not ask.
    /// </summary>
    private static string? ReasonAsked(string html, string? operation, int placeIndex, string? time)
    {
        var own = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "OrigTimes"), placeIndex), operation);
        var database = GlassEditorPages.Lookup(At(GlassEditorPages.SplitPairs(html, "DatabaseTimes"), placeIndex), operation);
        if (own is null || database is null)
        {
            return "The dialog's own times for " + Show(operation) + " could not be read to check the hours.";
        }

        return Number(database) != 0 && Number(own) != 0 && Number(time) > Number(own)
            ? "More hours than Glass's own (" + own + "): the dialog asks for a reason."
            : null;
    }

    /// <summary>
    /// SetLackStufe and SetLackartValues for a paint level the script's
    /// ArtLackMap names: the level, if the level list FillLackstufeSelect
    /// built at load (each level some price key of the selected coat has in
    /// the area) holds it, and that level's paint price and time in the
    /// area's first quantity. Null, and the selection left as it was, when
    /// the list does not hold it, as the script leaves it; null with a
    /// problem when the lists cannot be read or have no price for it.
    /// </summary>
    private static (string Level, string Price, string Time)? PaintOfLevel(
        string html, int areaIndex, string? coat, string? wanted, double state, List<string> problems)
    {
        var areaPrices = Area(NestedSplits(html, "LPrices"), areaIndex);
        var areaTimes = Area(NestedSplits(html, "LTimes"), areaIndex);
        var levelIds = GlassEditorPages.SplitArray(html, "Lackartids");
        var levelValues = GlassEditorPages.SplitArray(html, "LackStufeValues");
        if (areaPrices is not { Count: > 0 } || areaTimes is not { Count: > 0 } || levelIds is null || levelValues is null)
        {
            problems.Add("The paint lists (LPrices, LTimes, Lackartids, LackStufeValues) could not be read.");
            return null;
        }

        var keys = areaPrices[0].Where((_, index) => index % 2 == 0).ToList();
        var levels = levelValues
            .Where((value, index) => index % 2 == 0 && keys.Exists(key =>
                key.Length > 1 && At(levelIds, key[0] - 'a') == coat && key[1..2] == value))
            .ToList();
        if (wanted is null || !levels.Contains(wanted))
        {
            return null;
        }

        var priceKey = StateLetter(state) + wanted;
        var paintPrice = GlassEditorPages.Lookup(string.Join('|', areaPrices[0]), priceKey);
        var paintTime = GlassEditorPages.Lookup(string.Join('|', areaTimes[0]), priceKey);
        if (paintPrice is null || paintTime is null)
        {
            problems.Add("No paint price and time for " + priceKey + ".");
            return null;
        }

        return (wanted, paintPrice, paintTime);
    }

    private static List<string[]>? Area(List<List<string[]>>? areas, int index) =>
        areas is not null && index >= 0 && index < areas.Count ? areas[index] : null;

    /// <summary>
    /// A script's list of lists of bar-separated lists, one per paint area
    /// (<c>var LPrices = [["a|b".split( "|" )],["c|d".split( "|" )]];</c>),
    /// each split, read without running it. Null when the page has none.
    /// </summary>
    private static List<List<string[]>>? NestedSplits(string source, string name)
    {
        var start = Regex.Match(source, @"var\s+" + Regex.Escape(name) + @"\s*=\s*\[", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!start.Success)
        {
            return null;
        }

        var end = source.IndexOf("];", start.Index, StringComparison.Ordinal);
        if (end < 0)
        {
            return null;
        }

        var body = source[(start.Index + start.Length)..end];
        return AreaList().Matches(body)
            .Select(area => SplitEntry().Matches(area.Groups[1].Value).Select(entry => entry.Groups[1].Value.Split('|')).ToList())
            .ToList();
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

    [GeneratedRegex(@"\.BothNext\(\s*p\s*,\s*'([^']*)'\s*,\s*'([^']*)'\s*,\s*'([LR])'\s*,\s*Params\s*\)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex BothNextCall();

    [GeneratedRegex(@"var\s+Params\s*=\s*""([^""]*)""", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex BothNextParameters();

    [GeneratedRegex(@"^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex DecimalNumber();

    [GeneratedRegex(@"var\s+forceNonZero\s*=\s*([0-9]+)\s*;", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ForceNonZero();

    [GeneratedRegex(@"var\s+messageString\s*=\s*\(\s*forceNonZero\s*\)\s*\?\s*""([^""]*)""\s*:\s*""([^""]*)""\s*;", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ZeroMessage();

    [GeneratedRegex(@"\[((?:\s*""[^""]*""\.split\(\s*""\|""\s*\)\s*,?)+)\s*\]", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex AreaList();

    [GeneratedRegex(@"""([^""]*)""\.split", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex SplitEntry();
}
