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
/// The work on an open estimate that reads Glass's editor's lists, adds a
/// position or removes one: shared by the Automation's own sessions
/// (<c>GlassRepairEstimateGateway</c>) and its help in a member of staff's
/// session (<see cref="GlassSessionHelp"/>), so both choose rows and post
/// dialogs by the one rule (R1168, R1169, R1172, R1182).
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
    /// whatever price and hours it serves for it, none included (R1172).
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
    /// head row, the row an add takes. A side asked for keeps the parts of
    /// that side and leaves the special list unread, as a special operation
    /// has none. Nothing changes at Glass's. A list Glass's does not answer
    /// answers no rows and its code.
    /// </summary>
    internal async Task<(IReadOnlyList<GlassAutomationCatalogueRow> Rows, string? Code, GlassEditorSession Opened)> SearchAsync(
        GlassRepairEstimateSession session,
        GlassEditorClient editor,
        GlassEditorSession opened,
        string term,
        GlassAutomationPositionKind? kind,
        string? side,
        CancellationToken cancellationToken)
    {
        var rows = new List<GlassAutomationCatalogueRow>();
        try
        {
            if (kind != GlassAutomationPositionKind.Part && side is null)
            {
                var specials = await StageAsync(session, "EditorSpecialList", () => editor.ReadSpecialOperationsAsync(
                    opened, SpecialOperationGroup, cancellationToken));
                opened = specials.Session;
                rows.AddRange(SpecialOperationsOf(specials.Value)
                    .Where(row => Matches(term, row.Name, row.Code, null))
                    .Select(row => new GlassAutomationCatalogueRow(
                        row.Code, GlassAutomationPositionKind.SpecialOperation, row.Name.Trim(),
                        PartCode: null, PartNumber: null, Side: null, Paintable: false)));
            }
            if (kind != GlassAutomationPositionKind.SpecialOperation)
            {
                var parts = await StageAsync(session, "EditorPartList", () => editor.ReadPartListAsync(
                    opened, PartListGroups, cancellationToken));
                opened = parts.Session;
                rows.AddRange(parts.Value
                    .Where(row => row.IsHead
                        && (side is null || string.Equals(SideOf(row), side, StringComparison.Ordinal))
                        && Matches(term, row.Name, row.Code, row.Number))
                    .Select(row => new GlassAutomationCatalogueRow(
                        row.Id, GlassAutomationPositionKind.Part, row.Name.Trim(), row.Code,
                        string.IsNullOrWhiteSpace(row.Number) ? null : row.Number, SideOf(row), row.Paintable)));
            }
        }
        catch (Exception failure)
            when (failure is GlassMvaStageException || IsTransportFailure(failure, cancellationToken))
        {
            // Reading a list changes nothing: no rows, and the code.
            return (Array.Empty<GlassAutomationCatalogueRow>(), CodeOf(failure), opened);
        }

        return (rows, null, opened);
    }

    /// <summary>
    /// A claimed add's work (design 07 section 3.4): its row chosen as a
    /// build chooses one, from the open estimate's own lists, and refused
    /// with the reason and nothing sent when the row is not certain (R1169).
    /// Before the post the code its position will carry and the positions
    /// that carry it now are handed to <paramref name="record"/>, so a lost
    /// answer can be settled by the next read. The dialog is posted as
    /// Glass's serves it, nothing typed (R1168). The outcome, Glass's own
    /// figures and the new positions (every position on the estimate after
    /// the post that was not in <paramref name="before"/>) are handed to
    /// <paramref name="record"/> last, with the editor session as it stands.
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
                    request = new GlassEditorAddRequest(
                        GlassEditorDialog.SpecialOperation, special.Code, PartCodeOf(special.Code));
                }
            }
            else
            {
                var parts = await StageAsync(session, "EditorPartList", () => editor.ReadPartListAsync(
                    opened, PartListGroups, cancellationToken));
                opened = parts.Session;
                (request, reason, opened) = await ChooseAutomationPartAsync(
                    session, editor, opened, parts.Value, act, cancellationToken);
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
            };
            await record(act, opened);
            return new(act, opened, null);
        }

        // Before the post: the code the new position carries on the estimate
        // (A73 for AC0000073, C2020001 for part 2020001) and the positions
        // that carry it now, so a lost answer is settled by the next read.
        var expected = (adding.Dialog == GlassEditorDialog.SpecialOperation ? "A" : "C") + adding.ExpectedPartCode;
        act = act with
        {
            ExpectedCode = expected,
            Before = before.Positions
                .Where(id => string.Equals(GlassAutomationPositionPolicy.CodeOf(id), expected, StringComparison.Ordinal))
                .ToList(),
        };
        await record(act, opened);
        GlassEditorEstimate? after = null;
        try
        {
            var added = await StageAsync(session, "EditorAdd", () => editor.AddAsync(
                opened, before, adding, cancellationToken));
            opened = added.Session;
            after = added.Value.Estimate;
            act = act with
            {
                Outcome = GlassAutomationActOutcome.Added,
                Code = null,
                PositionIds = added.Value.Added,
                Description = added.Value.Summary.Description?.Trim(),
                Served = FiguresOf(added.Value.Summary),
                Listed = added.Value.Listed
                    .Select(listed => new GlassAutomationListedPart(listed.Code, listed.Text, listed.Ticked))
                    .ToList(),
            };
        }
        catch (GlassMvaStageException refused)
            when (refused.FailureCode is GlassFailure.EditorDialog or GlassFailure.EditorRefused)
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
    /// answered, found in the estimate's part list, decided by the build's
    /// own rule (<see cref="ChoosePartAsync"/>) for its code, its side and
    /// the act's operation. A row that is not a head of the list, a row
    /// serving both sides, and a side asked for that is not the row's own
    /// are refused before anything is asked.
    /// </summary>
    private async Task<(GlassEditorAddRequest? Request, string? Reason, GlassEditorSession Opened)> ChooseAutomationPartAsync(
        GlassRepairEstimateSession session, GlassEditorClient editor, GlassEditorSession opened,
        IReadOnlyList<GlassEditorPartRow> parts, GlassAutomationAct act, CancellationToken cancellationToken)
    {
        var row = parts.FirstOrDefault(item => item.IsHead && string.Equals(item.Id, act.Row, StringComparison.Ordinal));
        if (row is null)
        {
            return (null, GlassAutomationPositionPolicy.RowUnknownCode, opened);
        }
        if (row.Has('l') && row.Has('r'))
        {
            return (null, GlassAutomationPositionPolicy.BothSidesCode, opened);
        }
        if (act.Side is { } side && !string.Equals(side, row.Side, StringComparison.Ordinal))
        {
            return (null, GlassEstimateBuildPolicy.NoRowCode, opened);
        }

        var operation = act.Operation switch
        {
            GlassAutomationPartOperation.Repair => GlassEditorRows.Repair,
            GlassAutomationPartOperation.RemoveAndRefit => GlassEditorRows.RemoveAndRefit,
            _ => GlassEditorRows.Replace,
        };
        return await ChoosePartAsync(
            session, editor, opened, parts, GlassEstimateBuildKeyKind.PartCode, row.Code, row.Side, operation,
            cancellationToken);
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's session {SessionId} for case {CaseId} position act {Action} {Outcome} at {Code} {Detail}")]
    private static partial void LogPositionAct(
        ILogger logger, Guid sessionId, Guid caseId, GlassAutomationActAction action, GlassAutomationActOutcome outcome,
        string code, string detail);
}
