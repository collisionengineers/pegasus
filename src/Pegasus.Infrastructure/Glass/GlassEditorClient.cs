using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// One open session of Glass's editor, small enough for a caller to keep
/// between requests: the editor's address, its own name for where it was
/// opened from (<c>url0</c>), the session key and the cookies Glass's set.
/// The key, <c>url0</c> (which holds the return token) and the cookies are
/// secrets; <see cref="ToString"/> prints none of them.
/// </summary>
internal sealed record GlassEditorSession(
    string BaseAddress, string Url0, string Key, IReadOnlyDictionary<string, string> Cookies)
{
    public override string ToString() => $"GlassEditorSession {{ Cookies = {Cookies.Count} }}";
}

/// <summary>What an editor request answered, and the session as it stands after it.</summary>
internal sealed record GlassEditorReply<T>(T Value, GlassEditorSession Session);

/// <summary>Which of the editor's dialogs adds a position.</summary>
internal enum GlassEditorDialog
{
    /// <summary>A part that can be painted: <c>comp.php</c>, pressed with one of its buttons.</summary>
    PaintablePart,

    /// <summary>A part that cannot be painted: <c>part.php</c> for its rows.</summary>
    Part,

    /// <summary>A special operation: <c>part.php</c> for its code, in mode <c>A</c>.</summary>
    SpecialOperation,
}

/// <summary>
/// One position to add: the dialog, its rows (<c>753|754</c>) or code
/// (<c>AC0000073</c>), the part code the dialog must carry, the button to
/// press on a <c>comp.php</c> dialog (<c>BSS</c> Part, <c>BLL</c> Paint,
/// <c>BSL</c> Part and Paint), what to change on the dialog (on a
/// <c>comp.php</c> dialog only the operation and the OK to a part with no
/// figures),
/// which optional parts to tick if the editor lists mandatory parts, and the
/// side the dialog must serve: <c>L</c>, <c>R</c>, empty for a part with no
/// side, or null when the side is not checked (a special operation). A part
/// whose rows an equipment answer chose (R1206) also names the part number
/// the dialog must serve, and has its rows checked against the pair the
/// record's first line names (<c>C1010800_701910520101_2G_0_5_|64</c>).
/// </summary>
internal sealed record GlassEditorAddRequest(
    GlassEditorDialog Dialog,
    string Rows,
    string ExpectedPartCode,
    string? Button = null,
    GlassEditorChange? Change = null,
    IReadOnlyCollection<string>? Ticks = null,
    string? ExpectedSide = null,
    bool CheckRows = false,
    string? ExpectedPartNumber = null);

/// <summary>
/// What an add did: the dialog's summary and notes, the parts the
/// "Mandatory parts:" page listed (none when it did not appear), the estimate
/// read afterwards, and the positions on it that were not there before.
/// </summary>
internal sealed record GlassEditorAdded(
    GlassEditorSummary Summary,
    IReadOnlyList<string> Notes,
    IReadOnlyList<GlassEditorListed> Listed,
    GlassEditorEstimate Estimate,
    IReadOnlyList<string> Added)
{
    /// <summary>With "Both" ticked: the other side's dialog the editor's answer sends its list to next; null otherwise.</summary>
    public GlassEditorNext? Next { get; init; }
}

/// <summary>
/// Drives Glass's editor from the server with the same requests the editor's
/// own pages send: opens a session from the launch address, reads the
/// estimate and its lists, adds a position by fetching its dialog and posting
/// the dialog's form exactly as a browser would, confirms the "Mandatory
/// parts:" page, removes a position, and saves and exits.
///
/// <para>
/// <b>Like a browser, and no further.</b> It keeps its own cookies from the
/// answers it is given, sends a browser's headers and the page a browser
/// would name as the referrer, asks one thing at a time with a pause between
/// requests, never follows a redirect and never asks any host but the
/// configured estimator. A dialog with any problem is refused before it is
/// posted.
/// </para>
///
/// <para>
/// <b>Nothing secret is said.</b> No failure carries the session key, a
/// cookie, the launch address or the way home; a refusal's detail is the
/// dialog builder's own description of the page.
/// </para>
/// </summary>
internal sealed class GlassEditorClient(
    HttpClient httpClient,
    GlassRepairEstimateOptions options,
    TimeProvider timeProvider,
    TimeSpan? pause = null)
{
    /// <summary>The pause between two requests when none is given.</summary>
    public static readonly TimeSpan DefaultPause = TimeSpan.FromSeconds(1);

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36";

    private const string Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    private const string AcceptLanguage = "en-GB,en;q=0.9";

    private readonly TimeSpan pace = pause ?? DefaultPause;

    private DateTimeOffset? last;

    /// <summary>
    /// When this client last sent a request, or null before its first. A
    /// client that continues an editor another client opened is given that
    /// client's, so its first request still waits out the pause (design 07
    /// section 6), and its own is read back afterwards to be kept.
    /// </summary>
    public DateTimeOffset? LastRequestAtUtc
    {
        get => last;
        init => last = value;
    }

    /// <summary>
    /// Opens the editor from the launch address Pegasus's own estimate start
    /// produced: the launch page, then <c>aco.php</c>, then <c>aco2.php</c>,
    /// which carries the session key. The launch page must name this host's
    /// own return address.
    /// </summary>
    public async Task<GlassEditorSession> OpenAsync(string launchAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launchAddress);
        var jar = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Uri.TryCreate(launchAddress, UriKind.Absolute, out var launchUri))
        {
            throw new GlassMvaStageException(GlassFailure.EditorLaunch, detail: "address=unreadable");
        }
        if (options.IsEstimator(launchUri) && !launchUri.AbsolutePath.EndsWith("/acolib/aco_call_xml.php", StringComparison.Ordinal))
        {
            throw new GlassMvaStageException(GlassFailure.EditorLaunch, detail: "address=not-the-launch-page");
        }

        var launch = await GetAsync(launchAddress, options.CallbackBaseUri.AbsoluteUri, jar, GlassFailure.EditorLaunch, false, cancellationToken);
        var open = GlassMvaClient.Matched(
            () => GlassEditorPages.LaunchPage(launch, launchAddress, timeProvider.GetUtcNow().ToUnixTimeMilliseconds()),
            GlassFailure.EditorLaunch);
        if (open is null)
        {
            throw new GlassMvaStageException(GlassFailure.EditorLaunch, detail: "page=unread");
        }
        if (open.Blocked)
        {
            throw new GlassMvaStageException(GlassFailure.EditorBlocked);
        }
        if (open.Caller is null || !open.Caller.StartsWith(ReturnPrefix().AbsoluteUri, StringComparison.Ordinal))
        {
            throw new GlassMvaStageException(GlassFailure.EditorLaunch, detail: "caller=not-this-host");
        }

        await GetAsync(open.Aco, launchAddress, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var second = await GetAsync(open.Aco2, open.Aco, jar, GlassFailure.EditorKey, false, cancellationToken);
        var key = GlassMvaClient.Matched(() => GlassEditorPages.SessionKey(second), GlassFailure.EditorKey)
            ?? throw new GlassMvaStageException(GlassFailure.EditorKey);
        return new(open.Base, open.Url0, key, Snapshot(jar));
    }

    /// <summary>The estimate's positions and codes, and Glass's running totals from the same page.</summary>
    public async Task<GlassEditorReply<GlassEditorEstimate>> ReadEstimateAsync(
        GlassEditorSession session, CancellationToken cancellationToken)
    {
        var (jar, at) = Begin(session);
        var estimate = await SelectedAsync(at, jar, cancellationToken);
        return new(estimate, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// One position's figures from its record, as <c>mcode.php</c> answers
    /// it. The id goes into the address as it is, so only an id as Glass's
    /// lists them (letters, digits and dots) is asked for.
    /// </summary>
    public async Task<GlassEditorReply<GlassPositionFigures>> ReadPositionAsync(
        GlassEditorSession session, string positionId, CancellationToken cancellationToken)
    {
        if (!GlassAutomationRead.IsPositionId(positionId))
        {
            throw new ArgumentException("A position id is letters, digits and dots.", nameof(positionId));
        }

        var (jar, at) = Begin(session);
        var text = await GetAsync(at.Position(positionId), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var position = GlassMvaClient.Matched(() => GlassEditorPages.PositionFigures(text, positionId), GlassFailure.EditorAnswer)
            ?? throw Unread("position", text);
        return new(position, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>Glass's calculation, as <c>report.php?calcid=0</c> states it in its summary.</summary>
    public async Task<GlassEditorReply<GlassCalculation>> ReadCalculationAsync(
        GlassEditorSession session, CancellationToken cancellationToken)
    {
        var (jar, at) = Begin(session);
        var text = await GetAsync(at.Calculation(), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var calculation = GlassMvaClient.Matched(() => GlassEditorPages.Calculation(text), GlassFailure.EditorAnswer)
            ?? throw Unread("calculation", text);
        return new(calculation, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// The special-operation list, asked for as the editor's graph page first
    /// asks for it, with the editor's group list (<c>DS-AUXWORK-702|SS-FREEPAINT|…</c>).
    /// </summary>
    public async Task<GlassEditorReply<IReadOnlyList<GlassEditorSpecialGroup>>> ReadSpecialOperationsAsync(
        GlassEditorSession session, string groups, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(groups);
        var (jar, at) = Begin(session);
        var text = await GetAsync(at.SpecialList(groups), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var list = GlassMvaClient.Matched(() => GlassEditorPages.SpecialOperations(text), GlassFailure.EditorAnswer)
            ?? throw Unread("special-list", text);
        return new(list, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// The vehicle's part list (<c>list.php</c>), asked for with the editor's
    /// group list as the graph page asks for it, read row by row, with the
    /// vehicle's equipment criteria the same page carries.
    /// </summary>
    public async Task<GlassEditorReply<GlassEditorPartList>> ReadPartListAsync(
        GlassEditorSession session, string groups, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(groups);
        var (jar, at) = Begin(session);
        var text = await GetAsync(at.PartList(groups), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var rows = GlassMvaClient.Matched(() => GlassEditorPages.PartList(text), GlassFailure.EditorAnswer)
            ?? throw Unread("part-list", text);
        var list = new GlassEditorPartList(
            rows,
            GlassMvaClient.Matched(() => GlassEditorPages.Criteria(text), GlassFailure.EditorAnswer),
            GlassMvaClient.Matched(() => GlassEditorPages.ReferencedNote(text), GlassFailure.EditorAnswer));
        return new(list, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// Stores an equipment answer for the vehicle as the editor's list does
    /// just before it asks for the part's dialog (R1206). Glass's keeps it
    /// for the vehicle. The answer must be the service page that tells the
    /// list to read the criteria again; any other, or none, may still have
    /// stored it.
    /// </summary>
    public async Task<GlassEditorReply<bool>> StoreEquipmentAsync(
        GlassEditorSession session, string answer, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(answer);
        var (jar, at) = Begin(session);
        var text = await GetAsync(at.StoreEquipment(answer), at.Frame, jar, GlassFailure.EditorRequest, true, cancellationToken);
        if (!GlassMvaClient.Matched(() => GlassEditorPages.EquipmentStored(text), GlassFailure.EditorAnswer, outcomeUnknown: true))
        {
            throw new GlassMvaStageException(
                GlassFailure.EditorAnswer, outcomeUnknown: true, detail: $"page=equipment-answer length={text.Length}");
        }

        return new(true, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>One part-list row's equipment map, as the nested lists the page holds.</summary>
    public async Task<GlassEditorReply<IReadOnlyList<IReadOnlyList<string>>>> ReadEquipmentMapAsync(
        GlassEditorSession session, string row, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(row);
        var (jar, at) = Begin(session);
        var text = await GetAsync(at.Equipment(row), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var map = GlassMvaClient.Matched(() => GlassEditorPages.EquipmentMap(text), GlassFailure.EditorAnswer)
            ?? throw Unread("equipment", text);
        return new(map, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// Adds one position: fetches its dialog, builds the post a browser would
    /// send, refuses it without posting when the builder found any problem or
    /// the dialog is for another part, posts it, confirms the "Mandatory
    /// parts:" page with the ticks asked for when it appears, and reads the
    /// estimate back. <paramref name="before"/> is the estimate as last read,
    /// against which the new positions are found.
    /// </summary>
    public async Task<GlassEditorReply<GlassEditorAdded>> AddAsync(
        GlassEditorSession session, GlassEditorEstimate before, GlassEditorAddRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Rows);
        var (page, mode) = request.Dialog switch
        {
            GlassEditorDialog.PaintablePart => ("comp.php", "C"),
            GlassEditorDialog.Part => ("part.php", "C"),
            GlassEditorDialog.SpecialOperation => ("part.php", "A"),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "The dialog is not one the editor has."),
        };
        if (request.Dialog == GlassEditorDialog.PaintablePart && string.IsNullOrEmpty(request.Button))
        {
            throw new ArgumentException("A comp.php dialog is pressed with a button.", nameof(request));
        }

        var (jar, at) = Begin(session);
        var dialogAddress = at.Dialog(page, request.Rows, mode);
        var dialog = await GetAsync(dialogAddress, at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var build = GlassMvaClient.Matched(
            () => request.Dialog == GlassEditorDialog.PaintablePart
                ? GlassEditorDialogs.PartPost(dialog, request.Button!, request.Change)
                : GlassEditorDialogs.PlainPost(dialog, request.Change),
            GlassFailure.EditorDialog);
        if (Refusal(build, request) is { } refusal)
        {
            throw refusal;
        }

        // From the post on, a lost or unexpected answer may still have added the position.
        var postAddress = session.BaseAddress + build.Action;
        var posted = await PostAsync(postAddress, GlassEditorText.EncodeForm(build.Fields), dialogAddress, jar, cancellationToken);
        var answer = GlassMvaClient.Matched(
            () => GlassEditorDialogs.AfterPost(posted, request.Ticks), GlassFailure.EditorAnswer, outcomeUnknown: true);
        IReadOnlyList<GlassEditorListed> listed = [];
        if (answer.Kind == GlassEditorAnswerKind.Confirm)
        {
            listed = answer.Listed;
            var confirmAction = answer.Action
                ?? throw new GlassMvaStageException(GlassFailure.EditorAnswer, outcomeUnknown: true, detail: "kind=confirm action=absent");
            var confirmed = await PostAsync(
                session.BaseAddress + confirmAction, GlassEditorText.EncodeForm(answer.Fields), postAddress, jar, cancellationToken);
            answer = GlassMvaClient.Matched(
                () => GlassEditorDialogs.AfterPost(confirmed), GlassFailure.EditorAnswer, outcomeUnknown: true);
        }
        if (answer.Kind != GlassEditorAnswerKind.Done)
        {
            throw new GlassMvaStageException(GlassFailure.EditorAnswer, outcomeUnknown: true, detail: $"kind={answer.Kind}");
        }

        var after = await SelectedAsync(at, jar, cancellationToken);
        var added = after.Positions.Where(id => !before.Positions.Contains(id)).ToList();
        return new(
            new GlassEditorAdded(build.Summary!, build.Notes, listed, after, added) { Next = answer.Next },
            session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// Why a built dialog is not posted, as the add's stage failure, or null:
    /// a page that is not the dialog, any problem the builder found, a side,
    /// a part code, rows or a part number other than the request's, or no
    /// form action.
    /// </summary>
    private static GlassMvaStageException? Refusal(GlassEditorBuild build, GlassEditorAddRequest request)
    {
        if (build.Error is not null)
        {
            return new GlassMvaStageException(GlassFailure.EditorDialog, detail: build.Error);
        }
        if (build.Problems.Count > 0)
        {
            return new GlassMvaStageException(GlassFailure.EditorRefused, detail: string.Join(' ', build.Problems));
        }
        if (build.Summary is null)
        {
            return new GlassMvaStageException(GlassFailure.EditorRefused, detail: "The dialog carries no part code.");
        }

        // The side first: a right-hand part's code is the record's second code line.
        var servedSide = (build.Summary.Side ?? string.Empty).Trim();
        if (request.ExpectedSide is { } expectedSide && servedSide != expectedSide.Trim())
        {
            return new GlassMvaStageException(
                GlassFailure.EditorRefused,
                detail: "The dialog serves side " + (servedSide.Length == 0 ? "none" : servedSide) + ", not "
                    + (expectedSide.Trim().Length == 0 ? "none" : expectedSide.Trim()) + ".");
        }

        var servedCode = servedSide == "R" ? build.Summary.RightPartCode : build.Summary.PartCode;
        if (servedCode != request.ExpectedPartCode)
        {
            return new GlassMvaStageException(
                GlassFailure.EditorRefused,
                detail: "The dialog carries part code " + (servedCode ?? "none") + ", not " + request.ExpectedPartCode + ".");
        }
        if (request.CheckRows && !NamesRows(build.Summary.Position, request.Rows))
        {
            return new GlassMvaStageException(GlassFailure.EditorRefused, detail: "The dialog's record names other rows.");
        }
        if (request.ExpectedPartNumber is { } number
            && !string.Equals((build.Summary.PartNumber ?? string.Empty).Trim(), number.Trim(), StringComparison.Ordinal))
        {
            return new GlassMvaStageException(GlassFailure.EditorRefused, detail: "The dialog carries another part number.");
        }

        return build.Action is null
            ? new GlassMvaStageException(GlassFailure.EditorDialog, detail: "The form has no action.")
            : null;
    }

    /// <summary>
    /// A preview (R1206): the dialog an add of the request would post, asked
    /// for as the add asks for it and read as a form, and for the request's
    /// button and change what the add would send. Nothing is posted: Cancel
    /// in the editor sends no request. Where the add would refuse the built
    /// dialog before its post, the preview's <see cref="GlassDialogSend.Code"/>
    /// says so with the add's code.
    /// </summary>
    public async Task<GlassEditorReply<GlassDialogPreview>> PreviewAsync(
        GlassEditorSession session, GlassEditorAddRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Rows);
        var (page, mode) = request.Dialog switch
        {
            GlassEditorDialog.PaintablePart => ("comp.php", "C"),
            GlassEditorDialog.Part => ("part.php", "C"),
            GlassEditorDialog.SpecialOperation => ("part.php", "A"),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "The dialog is not one the editor has."),
        };
        var (jar, at) = Begin(session);
        var dialog = await GetAsync(at.Dialog(page, request.Rows, mode), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var (preview, build) = GlassMvaClient.Matched(
            () => GlassEditorDialogs.Preview(
                dialog, request.Dialog == GlassEditorDialog.PaintablePart ? request.Button ?? "BSS" : null,
                request.Change ?? new GlassEditorChange()),
            GlassFailure.EditorDialog);
        if (preview is null || build is null)
        {
            throw new GlassMvaStageException(GlassFailure.EditorDialog, detail: "The page has no form named Part.");
        }

        var code = Refusal(build, request)?.FailureCode;
        if (code is not null && preview.Sends is { Code: null } sends)
        {
            preview = preview with { Sends = sends with { Code = code } };
        }

        return new(preview, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// A preview of a position already on the estimate (R1206): its dialog,
    /// asked for as a remove asks for it, read as a form. Nothing is posted.
    /// </summary>
    public async Task<GlassEditorReply<GlassDialogPreview>> PreviewPositionAsync(
        GlassEditorSession session, string positionId, CancellationToken cancellationToken)
    {
        if (!GlassAutomationRead.IsPositionId(positionId))
        {
            throw new ArgumentException("A position id is letters, digits and dots.", nameof(positionId));
        }

        var (jar, at) = Begin(session);
        var shown = await GetAsync(at.OpenPosition(positionId), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var preview = GlassMvaClient.Matched(() => GlassEditorDialogs.Preview(shown).Preview, GlassFailure.EditorDialog)
            ?? throw new GlassMvaStageException(GlassFailure.EditorDialog, detail: "The page has no form named Part.");
        return new(preview, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// Removes one position as the editor does: its dialog first, then the
    /// removal, then the estimate is read back and must no longer list it.
    /// </summary>
    public async Task<GlassEditorReply<GlassEditorEstimate>> RemoveAsync(
        GlassEditorSession session, string positionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(positionId);
        var (jar, at) = Begin(session);
        var openAddress = at.OpenPosition(positionId);
        var shown = await GetAsync(openAddress, at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var dialog = GlassMvaClient.Matched(() => GlassEditorDialogs.PlainPost(shown), GlassFailure.EditorDialog);
        if (dialog.Error is not null)
        {
            throw new GlassMvaStageException(GlassFailure.EditorDialog, detail: dialog.Error);
        }

        var removed = await GetAsync(at.Remove(positionId), openAddress, jar, GlassFailure.EditorAnswer, true, cancellationToken);
        var answer = GlassMvaClient.Matched(
            () => GlassEditorDialogs.AfterPost(removed), GlassFailure.EditorAnswer, outcomeUnknown: true);
        if (answer.Kind != GlassEditorAnswerKind.Done)
        {
            throw new GlassMvaStageException(GlassFailure.EditorAnswer, outcomeUnknown: true, detail: $"kind={answer.Kind}");
        }

        var after = await SelectedAsync(at, jar, cancellationToken);
        if (after.Positions.Contains(positionId))
        {
            throw new GlassMvaStageException(GlassFailure.EditorAnswer, detail: "The position is still on the estimate.");
        }

        return new(after, session with { Cookies = Snapshot(jar) });
    }

    /// <summary>
    /// Save and Exit: the save request, whose answer must tell the editor to
    /// close; the close page, which names the launch page on the way out; and
    /// that page, which names the way home. Answers the way home exactly as
    /// the page gives it, once it is proved to be this host's own return
    /// address and to report a save.
    /// </summary>
    public async Task<string> SaveAndExitAsync(GlassEditorSession session, CancellationToken cancellationToken)
    {
        var (jar, at) = Begin(session);
        var saved = await GetAsync(at.Save(), at.Frame, jar, GlassFailure.EditorSave, true, cancellationToken);
        if (!GlassMvaClient.Matched(() => GlassEditorPages.SavedAndClosing(saved), GlassFailure.EditorSave, outcomeUnknown: true))
        {
            throw new GlassMvaStageException(GlassFailure.EditorSave, outcomeUnknown: true, detail: "page=save");
        }

        var close = at.Close;
        var closed = await GetAsync(close, at.Frame, jar, GlassFailure.EditorReturn, true, cancellationToken);
        var back = GlassMvaClient.Matched(() => GlassEditorPages.ClosePage(closed, close), GlassFailure.EditorReturn, outcomeUnknown: true)
            ?? throw new GlassMvaStageException(GlassFailure.EditorReturn, outcomeUnknown: true, detail: "page=close");
        var returned = await GetAsync(back, close, jar, GlassFailure.EditorReturn, false, cancellationToken);
        var outcome = GlassMvaClient.Matched(() => GlassEditorPages.ReturnPage(returned), GlassFailure.EditorReturn)
            ?? throw new GlassMvaStageException(GlassFailure.EditorReturn, detail: "page=return");
        var home = ReturnPrefix();
        if (outcome.Origin != home.GetLeftPart(UriPartial.Authority)
            || !outcome.Path.StartsWith(home.AbsolutePath, StringComparison.Ordinal))
        {
            throw new GlassMvaStageException(GlassFailure.EditorReturn, detail: "address=not-this-host");
        }
        if (outcome.DoSave != "1")
        {
            throw new GlassMvaStageException(GlassFailure.EditorSave, detail: "dosave=" + (outcome.DoSave is "0" ? "0" : "other"));
        }

        return outcome.Address;
    }

    /// <summary>
    /// Whether a part record's first line names the row pair asked for: the
    /// two fields after <c>2G</c> in <c>C1010800_701910520101_2G_0_5_|64</c>,
    /// as all five captured part dialogs asked for by a pair carry them.
    /// </summary>
    internal static bool NamesRows(string? position, string rows)
    {
        var pair = rows.Split('|');
        var fields = (position ?? string.Empty).Split('|')[0].Split('_');
        var at = Array.IndexOf(fields, "2G");
        return pair.Length == 2 && at >= 0 && at + 2 < fields.Length
            && fields[at + 1] == pair[0] && fields[at + 2] == pair[1];
    }

    /// <summary>Pegasus's own return address, up to its one-use token.</summary>
    private Uri ReturnPrefix() => new(options.CallbackBaseUri, GlassRepairEstimateOptions.CallbackPath);

    private (Dictionary<string, string> Jar, GlassEditorAddresses At) Begin(GlassEditorSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return (new Dictionary<string, string>(session.Cookies, StringComparer.Ordinal), new GlassEditorAddresses(session, timeProvider));
    }

    private async Task<GlassEditorEstimate> SelectedAsync(
        GlassEditorAddresses at, Dictionary<string, string> jar, CancellationToken cancellationToken)
    {
        var text = await GetAsync(at.Selected(), at.Frame, jar, GlassFailure.EditorRequest, false, cancellationToken);
        var estimate = GlassMvaClient.Matched(() => GlassEditorPages.Selected(text), GlassFailure.EditorAnswer);
        return estimate.Found
            ? estimate
            : throw Unread("selected", text);
    }

    /// <summary>
    /// An answer that is not the page asked for, said by the page's name and
    /// the answer's length alone. A Glass's server that does not hold the
    /// session answers a short page with none of the editor's lists, where
    /// the lists of the one that holds it run to tens of thousands of
    /// characters.
    /// </summary>
    private static GlassMvaStageException Unread(string page, string text) =>
        new(GlassFailure.EditorAnswer, detail: $"page={page} length={text.Length}");

    /// <summary>The cookies as they stand, in the order Glass's set them, as a copy no later answer changes.</summary>
    private static ReadOnlyDictionary<string, string> Snapshot(Dictionary<string, string> jar) =>
        new Dictionary<string, string>(jar, StringComparer.Ordinal).AsReadOnly();

    private Task<string> GetAsync(
        string address, string referer, Dictionary<string, string> jar, string failureCode, bool outcomeUnknown,
        CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Get, address, null, referer, jar, failureCode, outcomeUnknown, cancellationToken);

    /// <summary>A dialog's post: a lost or unexpected answer may still have changed the estimate.</summary>
    private Task<string> PostAsync(
        string address, string body, string referer, Dictionary<string, string> jar, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, address, body, referer, jar, GlassFailure.EditorAnswer, true, cancellationToken);

    /// <summary>
    /// One request, after the pause since the last one. Another host is
    /// refused before anything is sent; a redirect is a failure and is not
    /// followed; any answer but 200 is a failure; the answer is read as
    /// windows-1252 within the same bound as the portal's pages.
    /// </summary>
    private async Task<string> SendAsync(
        HttpMethod method, string address, string? body, string referer, Dictionary<string, string> jar,
        string failureCode, bool outcomeUnknown, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || !options.IsEstimator(uri))
        {
            throw new GlassMvaStageException(GlassFailure.EditorHost);
        }

        if (last is { } previous && previous + pace - timeProvider.GetUtcNow() is var wait && wait > TimeSpan.Zero)
        {
            await Task.Delay(wait, timeProvider, cancellationToken);
        }

        using var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", Accept);
        request.Headers.TryAddWithoutValidation("Accept-Language", AcceptLanguage);
        if (jar.Count > 0)
        {
            request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", jar.Select(pair => pair.Key + "=" + pair.Value)));
        }

        request.Headers.TryAddWithoutValidation("Referer", referer);
        if (body is not null)
        {
            request.Content = new ByteArrayContent(Encoding.ASCII.GetBytes(body));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        }

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            Keep(response, jar);
            var status = (int)response.StatusCode;
            if (status is >= 300 and < 400 || response.Headers.Location is not null)
            {
                throw new GlassMvaStageException(failureCode, outcomeUnknown, $"status={status} redirect=not-followed");
            }
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new GlassMvaStageException(failureCode, outcomeUnknown, $"status={status}");
            }

            var bytes = await GlassMvaClient.ReadAsync(
                response, GlassMvaClient.MaximumTextBytes, failureCode, outcomeUnknown, cancellationToken);
            return GlassEditorText.Decode(bytes);
        }
        finally
        {
            last = timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// Keeps each cookie an answer sets, by name, as a browser's jar keeps it
    /// for a page on this one host: only its name and value are sent back.
    /// </summary>
    private static void Keep(HttpResponseMessage response, Dictionary<string, string> jar)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var lines))
        {
            return;
        }

        foreach (var line in lines)
        {
            var pair = line.Split(';', 2)[0];
            var at = pair.IndexOf('=', StringComparison.Ordinal);
            if (at > 0)
            {
                jar[pair[..at].Trim()] = pair[(at + 1)..];
            }
        }
    }
}
