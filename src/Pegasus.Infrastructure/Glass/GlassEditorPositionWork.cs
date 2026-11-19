using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Glass;

/// <summary>
/// What a position act's work left: the act as it now stands, the editor
/// session after it, and the estimate as read after it, when one was read.
/// </summary>
internal sealed record GlassEditorPositionDone(
    GlassAutomationAct Act, GlassEditorSession Opened, GlassEditorEstimate? After);

/// <summary>
/// A part's equipment question as an add met it (R1206): the question, and
/// once an answer chose them, the row pair, the value stored for the vehicle
/// (null when the answer stores none), the values it changes and the part
/// list's figures for the part (price and Replace hours).
/// </summary>
internal sealed record GlassEditorEquipmentChoice(
    GlassEquipmentQuestion Question,
    string? Rows = null,
    string? Store = null,
    IReadOnlyList<GlassEquipmentChange>? Changes = null,
    GlassEstimateBuildFigures? Listed = null);

/// <summary>
/// What one read of an open estimate left: what was read (the estimate,
/// one position's figures or the calculation), or the <c>glass.*</c> code
/// when Glass's did not answer, and the editor session after it.
/// </summary>
internal sealed record GlassEditorReadDone(
    GlassEditorEstimate? Estimate,
    GlassPositionFigures? Position,
    GlassCalculation? Calculation,
    string? Code,
    GlassEditorSession Opened)
{
    /// <summary>The read as the Automation's tools answer it, with the session as the read left it.</summary>
    public GlassAutomationEstimate For(GlassRepairEstimateSession session) =>
        new(session, Estimate?.Positions, Estimate?.Codes)
        {
            Totals = Estimate?.Totals,
            Position = Position,
            Calculation = Calculation,
            Code = Code,
        };
}

/// <summary>
/// The work on an open estimate that reads Glass's editor's lists, previews
/// a dialog, adds a position or removes one: shared by the Automation's own
/// sessions (<c>GlassRepairEstimateGateway</c>) and its help in a member of
/// staff's session (<see cref="GlassSessionHelp"/>), so both choose rows and
/// post dialogs by the one rule (R1168, R1169, R1172, R1206). The rebuild of
/// an outside estimate shares <see cref="ChoosePartAsync"/> alone, and its
/// lines keep that method's own rule.
/// </summary>
/// <remarks>
/// It holds a logger and nothing else: no session store, no login, no
/// import, and it never saves or exits the editor. What an act leaves is
/// handed to the caller's own record before the post and once it has an
/// outcome, so each kind of session keeps it in its own way.
/// </remarks>
internal sealed partial class GlassEditorPositionWork(ILogger logger)
{
    /// <summary>
    /// The special list's group a build chooses special operations from:
    /// "Free positions parts", whose rows are the operator's own list of
    /// special operations, coded <c>AC</c> and seven digits (captures of 18
    /// November 2026). The list is asked for with this group alone.
    /// </summary>
    internal const string SpecialOperationGroup = "SS-FREEPART";

    internal const string SpecialOperationPrefix = "AC";

    /// <summary>
    /// The group list the part list is asked for with: the editor's own, as a
    /// browser asked for it on 7 September 2026 and a program asked for it on
    /// another vehicle on 18 November 2026, both answered with the whole list.
    /// </summary>
    internal const string PartListGroups =
        "DS2000019|DS2000008|DS2000005|DS2000018|DS2000003|DS2000002|DS2000001|DS2000007|DS2000004|DS2000006"
        + "|DS2000013|DS2000014|DS2000011|DS2000009|DS2000010|DS2000012|DS2000015|DS2000017|DS2000016|DS2000021|DS2000022";

    /// <summary>The part dialog's Part button: the part as served, not painted.</summary>
    private const string PartButton = "BSS";

    /// <summary>The part dialog's Paint button: the part's paint on its own (R1206).</summary>
    private const string PaintButton = "BLL";

    /// <summary>The part dialog's Part and Paint button (R1206).</summary>
    private const string PartAndPaintButton = "BSL";

    private const string BothSides = "both";

    internal async Task StageAsync(GlassRepairEstimateSession session, string stage, Func<Task> action)
    {
        await StageAsync(session, stage, async () => { await action(); return true; });
    }

    /// <summary>One request, or a few, to Glass's, timed and logged by the session's id, Case and version.</summary>
    internal async Task<T> StageAsync<T>(GlassRepairEstimateSession session, string stage, Func<Task<T>> action)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = "Succeeded";
        try { return await action(); }
        catch (Exception failure)
        {
            outcome = failure is GlassMvaStageException providerFailure
                ? providerFailure.FailureCode : failure is OperationCanceledException ? "Cancelled" : "Failed";
            if (failure is GlassMvaStageException { Detail: { } detail })
            {
                // A search or a read answers the caller its code alone; the
                // detail (a page's name and length, a status) is said here.
                LogStageDetail(logger, session.Id, session.CaseId, stage, detail);
            }

            throw;
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                LogStage(logger, session.Id, session.CaseId, session.Version, stage, outcome, elapsedMs);
            }
        }
    }

    /// <summary>
    /// The dialog a part's line is added by, chosen by <see cref="GlassEditorRows"/>
    /// for the line's operation from the part list and each found row's
    /// equipment map (R1172); or why the line is left off. Reading a map
    /// changes nothing at Glass's, so a map that cannot be read leaves only
    /// this line off. The dialog must serve the part code and the side the
    /// line was found by before anything is posted. A new part is posted as
    /// served; any other operation is chosen in the dialog, which then sends
    /// whatever price and hours it serves for it, none included (R1172):
    /// its question before sending no figures is answered OK (R1188).
    /// A build's line and the Automation's add both choose their rows here,
    /// by what the part is found by: its key and side.
    /// </summary>
    internal async Task<(GlassEditorAddRequest? Request, string? Reason, GlassEditorSession Opened)> ChoosePartAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        IReadOnlyList<GlassEditorPartRow> parts,
        GlassEstimateBuildKeyKind keyKind,
        string? key,
        string? side,
        string operation,
        CancellationToken cancellationToken)
    {
        var (heads, unfound) = GlassEditorRows.Find(parts, keyKind, key, side);
        if (unfound is not null)
        {
            return (null, unfound, opened);
        }

        var decided = new List<GlassEditorRowChoice>();
        foreach (var head in heads)
        {
            if (GlassEditorRows.ListRefusal(parts, head, operation) is { } refused)
            {
                decided.Add(GlassEditorRowChoice.Refused(refused));
                break;
            }

            try
            {
                var map = await StageAsync(session, "EditorEquipment", () => editor.ReadEquipmentMapAsync(
                    opened, parts[head].Id, cancellationToken));
                opened = map.Session;
                var choice = GlassEditorRows.Decide(parts, head, map.Value, operation);
                decided.Add(choice);
                if (choice.Rows is null)
                {
                    break;
                }
            }
            catch (Exception failure)
                when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
            {
                return (null, CodeOf(failure), opened);
            }
        }

        var chosen = GlassEditorRows.Choose(decided);
        if (chosen is not { Head: { } row, Rows: { } rows })
        {
            return (null, chosen.ReasonCode ?? GlassEstimateBuildPolicy.NoRowCode, opened);
        }

        var request = new GlassEditorAddRequest(
            row.Paintable ? GlassEditorDialog.PaintablePart : GlassEditorDialog.Part,
            rows,
            keyKind == GlassEstimateBuildKeyKind.PartCode && key is { } code ? code : row.Code,
            Button: row.Paintable ? PartButton : null,
            Change: operation == GlassEditorRows.Replace ? null : new GlassEditorChange(Operation: operation, ConfirmNoFigures: true),
            ExpectedSide: side ?? row.Side ?? string.Empty);
        return (request, null, opened);
    }

    /// <summary>
    /// The catalogue search of an open estimate (design 07 section 3.3): the
    /// special list's "Free positions parts" group, the part list, or both,
    /// asked for once each and matched against each row's description, part
    /// code or part number, ignoring case. A part is answered by its list's
    /// head row, the row an add takes, with the equipment question its block
    /// asks, when it asks one (R1206), built from the list alone. A side
    /// asked for keeps the parts of that side and leaves the special list
    /// unread, as a special operation has none. With <paramref name="row"/>
    /// the part list alone is read and that one head row answered, with
    /// every answer to its question that leads to a part, which reads the
    /// row's equipment map as well. Nothing changes at Glass's. A list or a
    /// map Glass's does not answer answers no rows and its code; a row that
    /// is not a head of the part list answers no rows and
    /// <see cref="GlassAutomationPositionPolicy.RowUnknownCode"/>.
    /// </summary>
    internal async Task<(IReadOnlyList<GlassAutomationCatalogueRow> Rows, string? Code, GlassEditorSession Opened)> SearchAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        string term,
        GlassAutomationPositionKind? kind,
        string? side,
        string? row,
        CancellationToken cancellationToken)
    {
        var rows = new List<GlassAutomationCatalogueRow>();
        try
        {
            if (row is not null)
            {
                var list = await StageAsync(session, "EditorPartList", () => editor.ReadPartListAsync(
                    opened, PartListGroups, cancellationToken));
                opened = list.Session;
                var head = IndexOfHead(list.Value.Rows, row);
                if (head < 0)
                {
                    return (Array.Empty<GlassAutomationCatalogueRow>(), GlassAutomationPositionPolicy.RowUnknownCode, opened);
                }

                var found = CatalogueRowOf(list.Value.Rows[head]);
                if (GlassEditorRows.Question(list.Value, head) is { } question)
                {
                    var map = await StageAsync(session, "EditorEquipment", () => editor.ReadEquipmentMapAsync(
                        opened, list.Value.Rows[head].Id, cancellationToken));
                    opened = map.Session;
                    if (!GlassEditorRows.IsMapOf(map.Value, list.Value.Rows[head]))
                    {
                        return (Array.Empty<GlassAutomationCatalogueRow>(), GlassFailure.EditorAnswer, opened);
                    }

                    found = found with { Equipment = GlassEditorRows.WithAnswers(list.Value, head, question, map.Value) };
                }

                rows.Add(found);
                return (rows, null, opened);
            }

            if (kind != GlassAutomationPositionKind.Part && side is null)
            {
                var specials = await StageAsync(session, "EditorSpecialList", () => editor.ReadSpecialOperationsAsync(
                    opened, SpecialOperationGroup, cancellationToken));
                opened = specials.Session;
                rows.AddRange(SpecialOperationsOf(specials.Value)
                    .Where(item => Matches(term, item.Name, item.Code, null))
                    .Select(item => new GlassAutomationCatalogueRow(
                        item.Code, GlassAutomationPositionKind.SpecialOperation, item.Name.Trim(),
                        PartCode: null, PartNumber: null, Side: null, Paintable: false)));
            }
            if (kind != GlassAutomationPositionKind.SpecialOperation)
            {
                var parts = await StageAsync(session, "EditorPartList", () => editor.ReadPartListAsync(
                    opened, PartListGroups, cancellationToken));
                opened = parts.Session;
                var list = parts.Value;
                for (var index = 0; index < list.Rows.Count; index++)
                {
                    var part = list.Rows[index];
                    if (part.IsHead
                        && (side is null || string.Equals(SideOf(part), side, StringComparison.Ordinal))
                        && Matches(term, part.Name, part.Code, part.Number))
                    {
                        rows.Add(CatalogueRowOf(part) with { Equipment = GlassEditorRows.Question(list, index)?.Asked });
                    }
                }
            }
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            // Reading a list or a map changes nothing: no rows, and the code.
            return (Array.Empty<GlassAutomationCatalogueRow>(), CodeOf(failure), opened);
        }

        return (rows, null, opened);
    }

    /// <summary>A part's head row as the search answers it.</summary>
    private static GlassAutomationCatalogueRow CatalogueRowOf(GlassEditorPartRow row) =>
        new(row.Id, GlassAutomationPositionKind.Part, row.Name.Trim(), row.Code,
            string.IsNullOrWhiteSpace(row.Number) ? null : row.Number, SideOf(row), row.Paintable);

    /// <summary>The index of the head row with this id, or -1.</summary>
    private static int IndexOfHead(IReadOnlyList<GlassEditorPartRow> rows, string? id)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].IsHead && string.Equals(rows[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// One read of an open estimate (R1206), one request to Glass's: the
    /// positions and Glass's running totals (<c>selected.php</c>, stage
    /// <c>EditorRead</c>), one position's figures (<c>mcode.php</c>, stage
    /// <c>EditorPosition</c>) or Glass's calculation (<c>report.php</c>,
    /// stage <c>EditorCalculation</c>). Reading changes nothing at Glass's:
    /// a read Glass's did not answer answers nothing read and its code, and
    /// the editor session as it was.
    /// </summary>
    internal async Task<GlassEditorReadDone> ReadAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        GlassAutomationRead read,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        try
        {
            switch (read.Kind)
            {
                case GlassAutomationReadKind.Position:
                    var positionId = read.PositionId
                        ?? throw new ArgumentException("A position read names its position.", nameof(read));
                    var position = await StageAsync(session, "EditorPosition", () => editor.ReadPositionAsync(
                        opened, positionId, cancellationToken));
                    return new(null, position.Value, null, null, position.Session);
                case GlassAutomationReadKind.Calculation:
                    var calculation = await StageAsync(session, "EditorCalculation", () => editor.ReadCalculationAsync(
                        opened, cancellationToken));
                    return new(null, null, calculation.Value, null, calculation.Session);
                default:
                    var estimate = await StageAsync(session, "EditorRead", () => editor.ReadEstimateAsync(
                        opened, cancellationToken));
                    return new(estimate.Value, null, null, null, estimate.Session);
            }
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return new(null, null, null, CodeOf(failure), opened);
        }
    }

    /// <summary>
    /// A claimed add's work (design 07 section 3.4): its row chosen as a
    /// build chooses one, from the open estimate's own lists, and refused
    /// with the reason and nothing sent when the row is not certain (R1169).
    /// A part whose block asks Glass's equipment question is added as Part
    /// only by the Automation's answer (R1206): its rows are the ones the
    /// editor's own rule gives for it, and an add with no answer, or one the
    /// dialog would not take or that leads to no part, is refused with the
    /// question and nothing sent. Before the post the code its position will
    /// carry, the positions that carry it now and the answer's rows and
    /// changes are handed to <paramref name="record"/>, so a lost answer can
    /// be settled by the next read. The answer is then stored for the vehicle
    /// as the editor stores it, just before the dialog is asked for. The
    /// dialog is posted as Glass's serves it with the button, operation,
    /// answers and typed figures the Automation gave it (<see cref="Chosen"/>,
    /// R1206), and nothing Pegasus types itself (R1168); the figures served
    /// and sent are handed to <paramref name="record"/> just before the post;
    /// with "Both" the other side's own dialog follows
    /// (<see cref="OtherSideAsync"/>). The
    /// outcome, Glass's own figures and the new positions (every position on
    /// the estimate after the post that was not in <paramref name="before"/>)
    /// are handed to <paramref name="record"/> last, with the editor session
    /// as it stands.
    /// </summary>
    internal async Task<GlassEditorPositionDone> AddPositionAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        GlassAutomationAct act,
        GlassEditorEstimate before,
        Func<GlassAutomationAct, GlassEditorSession, Task> record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(record);
        GlassEditorAddRequest? request = null;
        string? reason = null;
        GlassEditorEquipmentChoice? equipment = null;
        GlassEditorPartList? partList = null;
        var row = act.Row ?? string.Empty;
        try
        {
            if (act.Kind == GlassAutomationPositionKind.SpecialOperation)
            {
                var specials = await StageAsync(session, "EditorSpecialList", () => editor.ReadSpecialOperationsAsync(
                    opened, SpecialOperationGroup, cancellationToken));
                opened = specials.Session;
                var special = SpecialOperationsOf(specials.Value)
                    .Find(item => string.Equals(item.Code, row, StringComparison.Ordinal));
                if (special is null)
                {
                    reason = GlassAutomationPositionPolicy.RowUnknownCode;
                }
                else
                {
                    // A special operation takes the Automation's typed figures and reason (R1206).
                    request = new GlassEditorAddRequest(
                        GlassEditorDialog.SpecialOperation, special.Code, PartCodeOf(special.Code),
                        Change: WithTyped(null, act.Inputs));
                }
            }
            else
            {
                var parts = await StageAsync(session, "EditorPartList", () => editor.ReadPartListAsync(
                    opened, PartListGroups, cancellationToken));
                opened = parts.Session;
                partList = parts.Value;
                (request, reason, opened, equipment) = await ChooseAutomationPartAsync(
                    session, editor, opened, parts.Value, act, cancellationToken);
                if (request is not null)
                {
                    // The button, the operation and the answers the Automation gave the dialog (R1206).
                    var (choices, refusedBy) = Chosen(request, act);
                    request = refusedBy is null ? choices : null;
                    reason = refusedBy ?? reason;
                    equipment = refusedBy is null ? equipment : null;
                }
            }
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            // Reading a list changes nothing: the act is refused with the code.
            request = null;
            reason = CodeOf(failure);
        }

        if (request is not { } adding)
        {
            act = act with
            {
                Outcome = GlassAutomationActOutcome.Refused,
                Code = reason ?? GlassEstimateBuildPolicy.NoRowCode,
                Equipment = equipment?.Question,
            };
            await record(act, opened);
            return new(act, opened, null);
        }

        // Before the post: the code the new position carries on the estimate
        // (A73 for AC0000073, C2020001 for part 2020001) and the positions
        // that carry it now, so a lost answer is settled by the next read;
        // and an equipment answer's rows, the values it changes and that it
        // is about to be stored (R1206).
        var expected = (adding.Dialog == GlassEditorDialog.SpecialOperation ? "A" : "C") + adding.ExpectedPartCode;
        act = act with
        {
            ExpectedCode = expected,
            Before = before.Positions
                .Where(id => string.Equals(GlassAutomationPositionPolicy.CodeOf(id), expected, StringComparison.Ordinal))
                .ToList(),
            RowPair = equipment?.Rows,
            EquipmentChanged = equipment?.Changes ?? [],
            EquipmentStored = equipment?.Store is not null,
        };
        await record(act, opened);
        if (equipment?.Store is { } store)
        {
            try
            {
                var stored = await StageAsync(session, "EditorEquipmentAnswer", () => editor.StoreEquipmentAsync(
                    opened, store, cancellationToken));
                opened = stored.Session;
            }
            catch (Exception failure)
                when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
            {
                // Nothing was posted, but the answer may be stored: the act says so.
                var refused = AsFailure(failure);
                LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                    GlassAutomationActOutcome.Refused, refused.FailureCode, refused.Detail ?? string.Empty);
                act = act with { Outcome = GlassAutomationActOutcome.Refused, Code = refused.FailureCode };
                await record(act, opened);
                return new(act, opened, null);
            }
        }

        // Just before the post: the figures the dialog served and those it is about to send (R1206).
        async Task PostingAsync(GlassEditorSummary summary)
        {
            act = act with { DialogServed = ServedOf(summary), Sent = SentOf(summary) };
            await record(act, opened);
        }

        GlassEditorEstimate? after = null;
        try
        {
            var added = await StageAsync(session, "EditorAdd", () => editor.AddAsync(
                opened, before, adding, PostingAsync, cancellationToken));
            opened = added.Session;
            after = added.Value.Estimate;
            act = act with
            {
                Outcome = GlassAutomationActOutcome.Added,
                Code = null,
                PositionIds = added.Value.Added,
                Description = added.Value.Summary.Description?.Trim(),
                Served = FiguresOf(added.Value.Summary),
                DialogServed = ServedOf(added.Value.Summary),
                Sent = SentOf(added.Value.Summary),
                Listed = ListedOf(added.Value.Listed, adding.Ticks),
            };
            if (equipment?.Listed is { } fromList && act.Served is { } served
                && (fromList.Price != served.Price || fromList.Hours != served.Hours))
            {
                // Noted, not refused, until a live walk shows they always agree (design 10 section 2).
                LogServedFigures(logger, session.Id, session.CaseId, equipment.Rows ?? string.Empty,
                    fromList.Price, fromList.Hours, served.Price, served.Hours);
            }
            if (act.Inputs?.BothSides == true)
            {
                (act, opened, after) = await OtherSideAsync(
                    session, editor, opened, act, adding, added.Value, partList, record, cancellationToken);
            }
        }
        catch (GlassMvaStageException refused) when (RefusedBeforePost(refused.FailureCode))
        {
            // Refused before anything was posted.
            LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                GlassAutomationActOutcome.Refused, refused.FailureCode, refused.Detail ?? string.Empty);
            act = act with { Outcome = GlassAutomationActOutcome.Refused, Code = refused.FailureCode };
        }
        catch (Exception lost)
            when (lost is GlassMvaStageException || IsTransportFailure(lost, cancellationToken))
        {
            // Posted, or perhaps posted: the next read of the estimate settles it.
            var failure = AsFailure(lost);
            LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                GlassAutomationActOutcome.NotConfirmed, failure.FailureCode, failure.Detail ?? string.Empty);
            act = act with { Outcome = GlassAutomationActOutcome.NotConfirmed, Code = failure.FailureCode };
        }

        await record(act, opened);
        return new(act, opened, after);
    }

    /// <summary>
    /// The rows an add of a part is placed by: the head row the search
    /// answered, found in the estimate's part list. A row that is not a head
    /// of the list, a row serving both sides, and a side asked for that is
    /// not the row's own are refused before anything is asked. A head whose
    /// block asks no equipment question is decided by the build's own rule
    /// (<see cref="ChoosePartAsync"/>) for its code, its side and the act's
    /// operation, and an answer given for it is refused. A head whose block
    /// asks one is added as Part only by the Automation's answer (R1206):
    /// refused by the list alone when it is a wheel, offers no Replace or is
    /// hidden for the vehicle; otherwise its equipment map is read, and an
    /// add with no answer, or with one the dialog would not take or that
    /// leads to no part, is refused with the question. Repair and
    /// remove-and-refit on it are refused as before (R1172).
    /// </summary>
    private async Task<(GlassEditorAddRequest? Request, string? Reason, GlassEditorSession Opened, GlassEditorEquipmentChoice? Equipment)> ChooseAutomationPartAsync(
        GlassRepairEstimateSession session, GlassEditorClient editor, GlassEditorSession opened,
        GlassEditorPartList parts, GlassAutomationAct act, CancellationToken cancellationToken)
    {
        var head = IndexOfHead(parts.Rows, act.Row);
        if (head < 0)
        {
            return (null, GlassAutomationPositionPolicy.RowUnknownCode, opened, null);
        }

        var row = parts.Rows[head];
        if (row.Has('l') && row.Has('r'))
        {
            return (null, GlassAutomationPositionPolicy.BothSidesCode, opened, null);
        }
        if (act.Side is { } side && !string.Equals(side, row.Side, StringComparison.Ordinal))
        {
            return (null, GlassEstimateBuildPolicy.NoRowCode, opened, null);
        }

        var operation = act.Operation switch
        {
            GlassAutomationPartOperation.Repair => GlassEditorRows.Repair,
            GlassAutomationPartOperation.RemoveAndRefit => GlassEditorRows.RemoveAndRefit,
            _ => GlassEditorRows.Replace,
        };
        var answer = act.Inputs?.Equipment;
        var question = GlassEditorRows.Question(parts, head);
        if (question is null && answer is not null)
        {
            return (null, GlassAutomationPositionPolicy.EquipmentNotAskedCode, opened, null);
        }
        if (question is null || operation != GlassEditorRows.Replace)
        {
            var (certain, why, chosen) = await ChoosePartAsync(
                session, editor, opened, parts.Rows, GlassEstimateBuildKeyKind.PartCode, row.Code, row.Side, operation,
                cancellationToken);
            return (certain, why, chosen, null);
        }
        if (GlassEditorRows.AskedRefusal(parts, head) is { } refused)
        {
            return (null, refused, opened, null);
        }

        IReadOnlyList<IReadOnlyList<string>> map;
        try
        {
            var read = await StageAsync(session, "EditorEquipment", () => editor.ReadEquipmentMapAsync(
                opened, row.Id, cancellationToken));
            opened = read.Session;
            map = read.Value;
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            return (null, CodeOf(failure), opened, null);
        }

        var asked = new GlassEditorEquipmentChoice(question.Asked);
        if (!GlassEditorRows.IsMapOf(map, row))
        {
            return (null, GlassFailure.EditorAnswer, opened, null);
        }
        if (answer is null)
        {
            return (null, GlassEstimateBuildPolicy.EquipmentCode, opened, asked);
        }

        var answered = GlassEditorRows.Answer(parts, head, question, map, answer);
        if (answered is not { Part: { } part, Time: { } time, Rows: { } rows })
        {
            return (null, answered.ReasonCode ?? GlassAutomationPositionPolicy.EquipmentNoPartCode, opened, asked);
        }

        // The dialog is the variant row's: comp.php when it can be painted.
        var request = new GlassEditorAddRequest(
            part.Paintable ? GlassEditorDialog.PaintablePart : GlassEditorDialog.Part,
            rows,
            row.Code,
            Button: part.Paintable ? PartButton : null,
            ExpectedSide: row.Side ?? string.Empty,
            CheckRows: true,
            ExpectedPartNumber: part.Number);
        return (request, null, opened, asked with
        {
            Rows = rows,
            Store = answered.Store,
            Changes = answered.Changes,
            Listed = new(Amount(part.Price), Amount(time.Price)),
        });
    }

    /// <summary>
    /// The dialog an Automation add posts, with what it gave the dialog
    /// (R1206), and the code that refuses it before anything is asked for, or
    /// null. The button: <c>inputs.button</c>, else the act's operation (Part
    /// and Paint, Paint, or Part); a part that cannot be painted has none, so
    /// any but Part is refused with <see cref="GlassAutomationPositionPolicy.NotPaintableCode"/>.
    /// The operation: <c>inputs.operation</c>, else Repair or remove-and-refit
    /// for those operations, else the dialog's own. The question before a
    /// part with no figures: <c>inputs.confirmNoFigures</c>, else answered OK
    /// for Repair alone (R1188). "Both": only on a <c>part.php</c> dialog,
    /// else refused as not built. An operation the return cannot read back is
    /// refused first (<see cref="GlassAutomationPositionPolicy.OperationRefusal"/>).
    /// The optional parts to tick. The typed figures and reason (R1206), on
    /// a <c>part.php</c> dialog only; on one that can be painted they are
    /// refused as not built, having no capture. A rebuild never
    /// comes here: its lines keep <see cref="ChoosePartAsync"/>'s own rule.
    /// The request is answered with the choices even when refused, so a
    /// preview still shows the dialog.
    /// </summary>
    private static (GlassEditorAddRequest Request, string? Refused) Chosen(GlassEditorAddRequest request, GlassAutomationAct act)
    {
        var inputs = act.Inputs;
        var button = inputs?.Button ?? act.Operation switch
        {
            GlassAutomationPartOperation.PartAndPaint => GlassAutomationButton.PartAndPaint,
            GlassAutomationPartOperation.Paint => GlassAutomationButton.Paint,
            _ => GlassAutomationButton.Part,
        };
        var operation = inputs?.Operation?.Trim() ?? act.Operation switch
        {
            GlassAutomationPartOperation.Repair => GlassEditorRows.Repair,
            GlassAutomationPartOperation.RemoveAndRefit => GlassEditorRows.RemoveAndRefit,
            _ => null,
        };
        var confirm = inputs?.ConfirmNoFigures ?? operation == GlassEditorRows.Repair;
        var paintable = request.Dialog == GlassEditorDialog.PaintablePart;
        var both = inputs?.BothSides == true && !paintable;
        var chosen = request with
        {
            Button = !paintable ? null
                : button switch
                {
                    GlassAutomationButton.PartAndPaint => PartAndPaintButton,
                    GlassAutomationButton.Paint => PaintButton,
                    _ => PartButton,
                },
            Change = WithPaint(
                WithTyped(
                    operation is null && !confirm && !both
                        ? null
                        : new GlassEditorChange(Operation: operation, Both: both, ConfirmNoFigures: confirm),
                    paintable ? null : inputs),
                paintable ? inputs : null),
            Ticks = inputs?.OptionalParts,
        };
        // Typed figures on the dialog of a part that can be painted have no capture (R1206).
        var refused = GlassAutomationPositionPolicy.OperationRefusal(operation) is { } unread ? unread
            : !paintable && (button != GlassAutomationButton.Part || inputs?.Paint is not null) ? GlassAutomationPositionPolicy.NotPaintableCode
            : paintable && inputs?.BothSides == true ? GlassEstimateBuildPolicy.NotBuiltCode
            : paintable && inputs?.Typed == true ? GlassEstimateBuildPolicy.NotBuiltCode
            : null;
        return (chosen, refused);
    }

    /// <summary>
    /// The parts a "Mandatory parts:" page listed, each ticked as it was
    /// confirmed: by Glass's itself, or as an optional part the add named.
    /// </summary>
    private static List<GlassAutomationListedPart> ListedOf(
        IReadOnlyList<GlassEditorListed> listed, IReadOnlyCollection<string>? ticks) =>
        listed
            .Select(item => new GlassAutomationListedPart(
                item.Code, item.Text, item.Ticked || (item.Optional && ticks is not null && ticks.Contains(item.Code)))
            {
                Optional = item.Optional,
            })
            .ToList();

    /// <summary>
    /// "Both" (R1206): after the first side's post, the other side's own
    /// dialog, as the editor's list opens it when the answer calls its
    /// <c>BothNext</c>: its rows from the answer, a <c>part.php</c> dialog
    /// (the list's row for them must be a part that cannot be painted), the
    /// other side and the code the first dialog names for it. It is posted as
    /// served, its "Both" ticked and greyed by the editor so not sent, as the
    /// browser posted it on 18 November 2026. Before its post the code its
    /// position carries is recorded, so a lost answer is settled by the next
    /// read. An answer that names no other side, extra parameters, a row that
    /// is not such a part, or a dialog refused before its post leave the
    /// first side added and the act's code
    /// <see cref="GlassAutomationPositionPolicy.OtherSideNotAddedCode"/>.
    /// </summary>
    private async Task<(GlassAutomationAct Act, GlassEditorSession Opened, GlassEditorEstimate After)> OtherSideAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        GlassAutomationAct act,
        GlassEditorAddRequest first,
        GlassEditorAdded added,
        GlassEditorPartList? parts,
        Func<GlassAutomationAct, GlassEditorSession, Task> record,
        CancellationToken cancellationToken)
    {
        var after = added.Estimate;
        var servedSide = (added.Summary.Side ?? string.Empty).Trim();
        var otherCode = servedSide == "R" ? added.Summary.PartCode : added.Summary.RightPartCode;
        var next = added.Next;
        var head = next?.Rows.Split('|')[0];
        var row = parts?.Rows.FirstOrDefault(item => item.IsPart && string.Equals(item.Id, head, StringComparison.Ordinal));
        if (next is null || next.Parameters.Length > 0 || next.Side == servedSide || string.IsNullOrEmpty(otherCode)
            || row is null || row.Paintable)
        {
            LogPositionAct(logger, session.Id, session.CaseId, act.Action, act.Outcome,
                GlassAutomationPositionPolicy.OtherSideNotAddedCode, next is null ? "answer=no-other-side" : "other-side=not-a-part-dialog");
            return (act with { Code = GlassAutomationPositionPolicy.OtherSideNotAddedCode }, opened, after);
        }

        var expected = "C" + otherCode;
        act = act with
        {
            OtherSideCode = expected,
            Before = act.Before
                .Concat(after.Positions.Where(id => string.Equals(GlassAutomationPositionPolicy.CodeOf(id), expected, StringComparison.Ordinal)))
                .ToList(),
        };
        await record(act, opened);
        try
        {
            var second = await StageAsync(session, "EditorAddOtherSide", () => editor.AddAsync(
                opened, after,
                new GlassEditorAddRequest(GlassEditorDialog.Part, next.Rows, otherCode, Ticks: first.Ticks, ExpectedSide: next.Side),
                cancellationToken));
            act = act with
            {
                PositionIds = act.PositionIds.Concat(second.Value.Added).Distinct(StringComparer.Ordinal).ToList(),
                Listed = act.Listed.Concat(ListedOf(second.Value.Listed, first.Ticks)).ToList(),
            };
            return (act, second.Session, second.Value.Estimate);
        }
        catch (GlassMvaStageException refused)
            when (refused.FailureCode is GlassFailure.EditorDialog or GlassFailure.EditorRefused)
        {
            // The other side's dialog was refused before anything was posted for it.
            LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                act.Outcome, refused.FailureCode, refused.Detail ?? string.Empty);
            return (act with { Code = GlassAutomationPositionPolicy.OtherSideNotAddedCode }, opened, after);
        }
        catch (Exception lost)
            when (lost is GlassMvaStageException || IsTransportFailure(lost, cancellationToken))
        {
            // Posted, or perhaps posted: the next read of the estimate settles both sides.
            var failure = AsFailure(lost);
            LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                GlassAutomationActOutcome.NotConfirmed, failure.FailureCode, failure.Detail ?? string.Empty);
            return (act with { Outcome = GlassAutomationActOutcome.NotConfirmed, Code = failure.FailureCode }, opened, after);
        }
    }

    /// <summary>
    /// A preview (R1206): the dialog an add of <paramref name="draft"/>'s row
    /// would post, chosen exactly as the add chooses it from the estimate's
    /// own lists and with the same choices, but with no equipment answer
    /// stored and nothing posted; or a position on the estimate's dialog, as
    /// a remove opens it. Answers the dialog as a form, or the code when no
    /// dialog was read: a row the lists do not hold or that is not certain,
    /// an equipment question with no answer, or a page Glass's did not
    /// answer. A choice the add would refuse (a part that cannot be painted,
    /// "Both" on a part that can) still shows the dialog, with the refusal as
    /// <see cref="GlassDialogSend.Code"/>. Requests: a part, the part list,
    /// its equipment map and the dialog; a special operation, the special
    /// list and the dialog; a position, the estimate (whose groups say which
    /// dialog the editor opens for it, R1206) and its dialog, with what a
    /// change with the inputs would send.
    /// </summary>
    internal async Task<(GlassDialogPreview? Dialog, string? Code, GlassEditorSession Opened)> PreviewAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        GlassAutomationAct draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        try
        {
            if (draft.Kind is null)
            {
                // A position on the estimate: its dialog as the editor's list opens it, with the
                // change the inputs would make (R1206).
                var positionId = draft.PositionId
                    ?? throw new ArgumentException("A preview of a position names it.", nameof(draft));
                var read = await StageAsync(session, "EditorRead", () => editor.ReadEstimateAsync(opened, cancellationToken));
                opened = read.Session;
                var (found, unshown) = PageOf(read.Value, positionId);
                if (found is null)
                {
                    return (null, unshown, opened);
                }

                string page = found;
                var (change, button, refusedChange) = ChangeOf(draft.Inputs, page);
                var shown = await StageAsync(session, "EditorPreview", () => editor.PreviewPositionAsync(
                    opened, positionId, page, button, draft.Inputs is null ? null : change, cancellationToken));
                var shownDialog = refusedChange is not null && shown.Value.Sends is { } changeSends
                    ? shown.Value with { Sends = changeSends with { Code = refusedChange } }
                    : shown.Value;
                return (shownDialog, null, shown.Session);
            }

            GlassEditorAddRequest? request = null;
            string? reason = null;
            string? refusedBy = null;
            var row = draft.Row ?? string.Empty;
            if (draft.Kind == GlassAutomationPositionKind.SpecialOperation)
            {
                var specials = await StageAsync(session, "EditorSpecialList", () => editor.ReadSpecialOperationsAsync(
                    opened, SpecialOperationGroup, cancellationToken));
                opened = specials.Session;
                var special = SpecialOperationsOf(specials.Value)
                    .Find(item => string.Equals(item.Code, row, StringComparison.Ordinal));
                request = special is null
                    ? null
                    : new GlassEditorAddRequest(
                        GlassEditorDialog.SpecialOperation, special.Code, PartCodeOf(special.Code),
                        Change: WithTyped(null, draft.Inputs));
                reason = special is null ? GlassAutomationPositionPolicy.RowUnknownCode : null;
            }
            else
            {
                var parts = await StageAsync(session, "EditorPartList", () => editor.ReadPartListAsync(
                    opened, PartListGroups, cancellationToken));
                opened = parts.Session;
                (request, reason, opened, _) = await ChooseAutomationPartAsync(
                    session, editor, opened, parts.Value, draft, cancellationToken);
                if (request is not null)
                {
                    (request, refusedBy) = Chosen(request, draft);
                }
            }

            if (request is null)
            {
                return (null, reason ?? GlassEstimateBuildPolicy.NoRowCode, opened);
            }

            var previewed = await StageAsync(session, "EditorPreview", () => editor.PreviewAsync(
                opened, request, cancellationToken));
            var dialog = refusedBy is not null && previewed.Value.Sends is { } sends
                ? previewed.Value with { Sends = sends with { Code = refusedBy } }
                : previewed.Value;
            return (dialog, null, previewed.Session);
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            // Reading a list, a map or a dialog changes nothing: no dialog, and the code.
            return (null, CodeOf(failure), opened);
        }
    }

    /// <summary>
    /// The dialog page of a position on the estimate, by the editor's own
    /// rule (<see cref="GlassEditorPages.DialogPage"/>), or why there is none
    /// to change: a position the estimate does not hold
    /// (<see cref="GlassAutomationPositionPolicy.RowUnknownCode"/>), a group
    /// the list does not hold (<see cref="GlassFailure.EditorAnswer"/>), or a
    /// paint position's own dialog (<c>lack.php</c>), not built
    /// (<see cref="GlassEstimateBuildPolicy.NotBuiltCode"/>: the return's
    /// reader does not know its free paint rows).
    /// </summary>
    private static (string? Page, string? Code) PageOf(GlassEditorEstimate estimate, string positionId)
    {
        if (!estimate.Positions.Contains(positionId, StringComparer.Ordinal))
        {
            return (null, GlassAutomationPositionPolicy.RowUnknownCode);
        }

        return GlassEditorPages.DialogPage(positionId, estimate.Groups) switch
        {
            null => (null, GlassFailure.EditorAnswer),
            "lack" => (null, GlassEstimateBuildPolicy.NotBuiltCode),
            { } page => (page, null),
        };
    }

    /// <summary>
    /// What a change gives a position's dialog (R1206): the typed figures and
    /// reason on a <c>part.php</c> dialog, the OK to no figures, the button on
    /// a <c>comp.php</c> one (null: the one the page shows); and the code that
    /// refuses it before anything is posted: typed figures on a part with its
    /// paint have no capture, and a paint button has no place on <c>part.php</c>.
    /// </summary>
    private static (GlassEditorChange Change, string? Button, string? Refused) ChangeOf(GlassAutomationInputs? inputs, string page)
    {
        var paintable = page == "comp";
        var change = WithPaint(
            WithTyped(new GlassEditorChange(ConfirmNoFigures: inputs?.ConfirmNoFigures == true), paintable ? null : inputs),
            paintable ? inputs : null)!;
        var button = !paintable ? null
            : inputs?.Button switch
            {
                GlassAutomationButton.Part => PartButton,
                GlassAutomationButton.Paint => PaintButton,
                GlassAutomationButton.PartAndPaint => PartAndPaintButton,
                _ => null,
            };
        var refused = paintable && inputs?.Typed == true ? GlassEstimateBuildPolicy.NotBuiltCode
            : !paintable && (inputs?.Button is not null and not GlassAutomationButton.Part || inputs?.Paint is not null)
                ? GlassAutomationPositionPolicy.NotPaintableCode
            : null;
        return (change, button, refused);
    }

    /// <summary>
    /// A claimed change's work (R1206): it must name a position the estimate
    /// holds, any position of it (as a remove may, R1185), or it is refused
    /// and nothing is sent. Its dialog is the one the editor's list opens for
    /// it (<see cref="PageOf"/>), built with the inputs (<see cref="ChangeOf"/>);
    /// just before the post the code it carries and the figures served and
    /// sent are handed to <paramref name="record"/>, so a lost answer is not
    /// confirmed. The "Mandatory parts:" page is confirmed as an add's is, the
    /// estimate read back and the position's own record read. The outcome is
    /// handed to <paramref name="record"/> with the editor session as it stands.
    /// </summary>
    internal async Task<GlassEditorPositionDone> ChangePositionAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        GlassAutomationAct act,
        GlassEditorEstimate before,
        Func<GlassAutomationAct, GlassEditorSession, Task> record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(record);
        var positionId = act.PositionId ?? string.Empty;
        var (found, unfound) = PageOf(before, positionId);
        if (found is null)
        {
            act = act with { Outcome = GlassAutomationActOutcome.Refused, Code = unfound };
            await record(act, opened);
            return new(act, opened, null);
        }

        string page = found;
        var (change, button, refusedBy) = ChangeOf(act.Inputs, page);
        if (refusedBy is not null)
        {
            act = act with { Outcome = GlassAutomationActOutcome.Refused, Code = refusedBy };
            await record(act, opened);
            return new(act, opened, null);
        }

        // Just before the post: the code the position carries, and the figures served and sent.
        async Task PostingAsync(GlassEditorSummary summary)
        {
            act = act with
            {
                ExpectedCode = GlassAutomationPositionPolicy.CodeOf(positionId),
                DialogServed = ServedOf(summary),
                Sent = SentOf(summary),
            };
            await record(act, opened);
        }

        GlassEditorEstimate? after = null;
        try
        {
            var changed = await StageAsync(session, "EditorChange", () => editor.ChangeAsync(
                opened, positionId, page, button, change, act.Inputs?.OptionalParts, PostingAsync, cancellationToken));
            opened = changed.Session;
            after = changed.Value.Estimate;
            act = act with
            {
                Outcome = GlassAutomationActOutcome.Changed,
                Code = null,
                Description = changed.Value.Summary.Description?.Trim(),
                Served = FiguresOf(changed.Value.Summary),
                DialogServed = ServedOf(changed.Value.Summary),
                Sent = SentOf(changed.Value.Summary),
                Listed = ListedOf(changed.Value.Listed, act.Inputs?.OptionalParts),
                Position = changed.Value.Position,
            };
        }
        catch (GlassMvaStageException refused) when (RefusedBeforePost(refused.FailureCode))
        {
            // Refused before anything was posted.
            LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                GlassAutomationActOutcome.Refused, refused.FailureCode, refused.Detail ?? string.Empty);
            act = act with { Outcome = GlassAutomationActOutcome.Refused, Code = refused.FailureCode };
        }
        catch (Exception lost)
            when (lost is GlassMvaStageException || IsTransportFailure(lost, cancellationToken))
        {
            // Posted, or perhaps posted: the position's own read settles it. Before the post
            // began (no code recorded), nothing was sent and the change is refused.
            var failure = AsFailure(lost);
            var outcome = act.ExpectedCode is null ? GlassAutomationActOutcome.Refused : GlassAutomationActOutcome.NotConfirmed;
            LogPositionAct(logger, session.Id, session.CaseId, act.Action, outcome, failure.FailureCode, failure.Detail ?? string.Empty);
            act = act with { Outcome = outcome, Code = failure.FailureCode };
        }

        await record(act, opened);
        return new(act, opened, after);
    }

    /// <summary>
    /// A claimed remove's work: it must name a position the estimate holds
    /// (R1185: any position of the open estimate), or it is refused and
    /// nothing is sent. The position's dialog, then the removal, then the
    /// estimate read back. The outcome is handed to <paramref name="record"/>
    /// with the editor session as it stands.
    /// </summary>
    internal async Task<GlassEditorPositionDone> RemovePositionAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        GlassAutomationAct act,
        GlassEditorEstimate before,
        Func<GlassAutomationAct, GlassEditorSession, Task> record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(act);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(record);
        var positionId = act.PositionId ?? string.Empty;
        if (!before.Positions.Contains(positionId, StringComparer.Ordinal))
        {
            act = act with
            {
                Outcome = GlassAutomationActOutcome.Refused,
                Code = GlassAutomationPositionPolicy.RowUnknownCode,
            };
            await record(act, opened);
            return new(act, opened, null);
        }

        GlassEditorEstimate? after = null;
        try
        {
            var removed = await StageAsync(session, "EditorRemove", () => editor.RemoveAsync(
                opened, positionId, cancellationToken));
            opened = removed.Session;
            after = removed.Value;
            act = act with { Outcome = GlassAutomationActOutcome.Removed, Code = null };
        }
        catch (GlassMvaStageException refused) when (refused.FailureCode == GlassFailure.EditorDialog)
        {
            // The position's dialog was not the one expected: nothing was removed.
            LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                GlassAutomationActOutcome.Refused, refused.FailureCode, refused.Detail ?? string.Empty);
            act = act with { Outcome = GlassAutomationActOutcome.Refused, Code = refused.FailureCode };
        }
        catch (Exception lost)
            when (lost is GlassMvaStageException || IsTransportFailure(lost, cancellationToken))
        {
            // Removed, or perhaps removed: the next read of the estimate settles it.
            var failure = AsFailure(lost);
            LogPositionAct(logger, session.Id, session.CaseId, act.Action,
                GlassAutomationActOutcome.NotConfirmed, failure.FailureCode, failure.Detail ?? string.Empty);
            act = act with { Outcome = GlassAutomationActOutcome.NotConfirmed, Code = failure.FailureCode };
        }

        await record(act, opened);
        return new(act, opened, after);
    }

    /// <summary>The special operations of the build's group: each row's code and name.</summary>
    internal static List<GlassSpecialOperation> SpecialOperationsOf(IReadOnlyList<GlassEditorSpecialGroup> groups) =>
        groups
            .Where(group => string.Equals(group.Code, SpecialOperationGroup, StringComparison.Ordinal))
            .SelectMany(group => group.Rows)
            .Where(row => row.Count > 2 && row[0].StartsWith(SpecialOperationPrefix, StringComparison.Ordinal))
            .Select(row => new GlassSpecialOperation(row[0], row[2]))
            .ToList();

    /// <summary>
    /// The part code a special operation's dialog carries: its row code
    /// without the prefix and the leading zeros (<c>AC0000073</c> is 73).
    /// </summary>
    internal static string PartCodeOf(string code) => code[SpecialOperationPrefix.Length..].TrimStart('0');

    /// <summary>What the dialog served, a zero read as none so it compares with a line that has none.</summary>
    internal static GlassEstimateBuildFigures FiguresOf(GlassEditorSummary summary) =>
        new(Amount(summary.Price), Amount(summary.Hours));

    /// <summary>The figures the dialog served for the operation it sends, before anything typed (R1206); a zero kept.</summary>
    internal static GlassAutomationFigures ServedOf(GlassEditorSummary summary) =>
        new(Figure(summary.ServedPrice ?? summary.Price), Figure(summary.ServedHours ?? summary.Hours), Figure(summary.ServedOverlapHours))
        {
            PaintCoats = summary.ServedPaintType,
            PaintLevel = summary.ServedPaintLevel,
            PaintPrice = Figure(summary.ServedPaintPrice),
            PaintHours = Figure(summary.ServedPaintHours),
            Surcharge = summary.ServedSurcharge,
        };

    /// <summary>The figures the post sends (R1206); a zero kept.</summary>
    internal static GlassAutomationFigures SentOf(GlassEditorSummary summary) =>
        new(Figure(summary.Price), Figure(summary.Hours), Figure(summary.OverlapHours))
        {
            PaintCoats = summary.PaintType,
            PaintLevel = summary.PaintLevel,
            PaintPrice = Figure(summary.PaintPrice),
            PaintHours = Figure(summary.PaintHours),
            Surcharge = summary.Surcharge,
        };

    private static decimal? Figure(string? text) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// A stage code that says the dialog was refused before anything was
    /// posted: the editor's own refusal, or a typed input the dialog does
    /// not take (R1206).
    /// </summary>
    internal static bool RefusedBeforePost(string code) =>
        code is GlassFailure.EditorDialog or GlassFailure.EditorRefused
            or GlassAutomationPositionPolicy.ReasonRequiredCode
            or GlassAutomationPositionPolicy.ReasonNotAskedCode
            or GlassAutomationPositionPolicy.OverlapAboveHoursCode
            or GlassAutomationPositionPolicy.PaintNotReadBackCode;

    /// <summary>
    /// The change with the act's typed figures and reason set as the dialog
    /// takes them (R1206): each figure as <see cref="GlassAutomationPositionPolicy.TypedText"/>
    /// gives it, the reason as given. The change as it was when none is typed.
    /// </summary>
    private static GlassEditorChange? WithTyped(GlassEditorChange? change, GlassAutomationInputs? inputs) =>
        inputs is null || !inputs.Typed
            ? change
            : (change ?? new GlassEditorChange()) with
            {
                Price = TypedOf(inputs.Price),
                Hours = TypedOf(inputs.Hours),
                OverlapHours = TypedOf(inputs.OverlapHours),
                Reason = inputs.Reason,
            };

    private static string? TypedOf(decimal? value) =>
        value is { } given ? GlassAutomationPositionPolicy.TypedText(given) : null;

    /// <summary>The change with the act's paint choices (R1206), paint hours as typed text; the change as it was when none is given.</summary>
    private static GlassEditorChange? WithPaint(GlassEditorChange? change, GlassAutomationInputs? inputs) =>
        inputs?.Paint is not { } paint
            ? change
            : (change ?? new GlassEditorChange()) with
            {
                Paint = new GlassEditorPaint(paint.Coats?.Trim(), paint.Level?.Trim(), paint.Surcharge, TypedOf(paint.Hours)),
            };

    private static decimal? Amount(string? served) =>
        decimal.TryParse(served, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value != 0m
            ? value
            : null;

    /// <summary>A stage's own code, or the transport's when nothing answered.</summary>
    internal static string CodeOf(Exception failure) =>
        failure is GlassMvaStageException stage ? stage.FailureCode : GlassFailure.TransportFailed;

    /// <summary>A failure of the request itself, not of a cancellation the caller asked for.</summary>
    internal static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && exception is HttpRequestException or TaskCanceledException or TimeoutException;

    /// <summary>
    /// A stage's own refusal as it was thrown; a transport failure after the
    /// provider may have acted becomes the outcome-unknown transport code.
    /// </summary>
    internal static GlassMvaStageException AsFailure(Exception exception) =>
        exception as GlassMvaStageException
            ?? new(GlassFailure.TransportUnknown, outcomeUnknown: true);

    /// <summary>Whether a row's description, part code or part number holds the term, ignoring case.</summary>
    private static bool Matches(string term, string? description, string? code, string? number) =>
        (description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (code?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (number?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>A part row's side as the search answers it: <c>L</c>, <c>R</c>, <c>both</c>, or null for none.</summary>
    private static string? SideOf(GlassEditorPartRow row) =>
        row.Has('l') && row.Has('r') ? BothSides : row.Side;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Glass's attempt {SessionId}/{Version} case {CaseId} stage {Stage} outcome {Outcome} in {ElapsedMs} ms")]
    private static partial void LogStage(ILogger logger, Guid sessionId, Guid caseId, long version,
        string stage, string outcome, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Glass's attempt {SessionId} case {CaseId} stage {Stage} was answered {Detail}")]
    private static partial void LogStageDetail(ILogger logger, Guid sessionId, Guid caseId, string stage, string detail);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Glass's session {SessionId} for case {CaseId} rows {Rows} served price {ServedPrice} hours {ServedHours}, the part list gives {ListedPrice} and {ListedHours}")]
    private static partial void LogServedFigures(
        ILogger logger, Guid sessionId, Guid caseId, string rows, decimal? listedPrice, decimal? listedHours,
        decimal? servedPrice, decimal? servedHours);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's session {SessionId} for case {CaseId} position act {Action} {Outcome} at {Code} {Detail}")]
    private static partial void LogPositionAct(
        ILogger logger, Guid sessionId, Guid caseId, GlassAutomationActAction action, GlassAutomationActOutcome outcome,
        string code, string detail);
}
