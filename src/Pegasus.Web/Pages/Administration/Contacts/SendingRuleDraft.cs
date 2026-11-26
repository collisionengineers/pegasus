using System.Globalization;
using Pegasus.Core.Reports;

namespace Pegasus.Web.Pages.Administration.Contacts;

/// <summary>
/// One condition row of the rule dialog as drawn or posted: the fact, is or
/// is not, the chosen contacts, outcomes or Case types, and the typed text of
/// a Sender or Reference prefix box as it was typed.
/// </summary>
public sealed record SendingConditionDraft(ReportSendingFact Fact, bool Not, IReadOnlyList<string> Choices, string Text);

/// <summary>
/// One action row of the rule dialog as drawn or posted: the action, the
/// typed addresses or text as they were typed, and the file it names.
/// </summary>
public sealed record SendingActionDraft(ReportSendingActionKind Kind, string Text, ReportSendingFile? File);

/// <summary>
/// The rule (or the On every Case list) a Sending rules dialog edits, as the
/// form posts it: <see cref="If"/> is groups joined by "or", a group's
/// conditions joined by "and", and <see cref="Then"/> the actions. The
/// dialog's rows post as <c>Cond.{group}.{row}.*</c> and
/// <c>Action.{row}.*</c>; every row carries all its value controls, and
/// only the one that fits the row's fact or action is read. Nothing is
/// checked here: <see cref="PrincipalReportSendingRules.Normalize"/> refuses
/// what is wrong when the whole rules value is saved.
/// </summary>
public sealed record SendingRuleDraft(
    IReadOnlyList<IReadOnlyList<SendingConditionDraft>> If,
    IReadOnlyList<SendingActionDraft> Then)
{
    public const string ConditionPrefix = "Cond";
    public const string ActionPrefix = "Action";

    // Several addresses or prefixes in one box (the box's hint: "Separate several with commas.").
    private static readonly char[] AddressSeparators = [',', ';', ' ', '\r', '\n'];
    private static readonly char[] TextSeparators = [',', ';', '\r', '\n'];

    /// <summary>What And and Or add, and the condition a new rule starts with.</summary>
    public static SendingConditionDraft BlankCondition { get; } = new(ReportSendingFact.ClaimSource, false, [], string.Empty);

    /// <summary>What Add action adds.</summary>
    public static SendingActionDraft BlankAction { get; } = new(ReportSendingActionKind.Task, string.Empty, null);

    /// <summary>A new rule: one Claim Source condition and one Cc action, both empty.</summary>
    public static SendingRuleDraft NewRule { get; } = new(
        [[BlankCondition]],
        [new SendingActionDraft(ReportSendingActionKind.Cc, string.Empty, null)]);

    /// <summary>Whether <paramref name="fact"/>'s value is typed in a text box.</summary>
    public static bool IsTyped(ReportSendingFact fact) => fact is ReportSendingFact.Sender or ReportSendingFact.RefPrefix;

    /// <summary>Whether <paramref name="kind"/>'s value is a list of addresses.</summary>
    public static bool TakesAddresses(ReportSendingActionKind kind) =>
        kind is ReportSendingActionKind.SendTo or ReportSendingActionKind.Cc or ReportSendingActionKind.NeverCc;

    /// <summary>Whether <paramref name="kind"/>'s value is one file.</summary>
    public static bool TakesFile(ReportSendingActionKind kind) =>
        kind is ReportSendingActionKind.Attach or ReportSendingActionKind.DoNotAttach;

    /// <summary>Whether <paramref name="kind"/>'s value is a text.</summary>
    public static bool TakesText(ReportSendingActionKind kind) =>
        kind is ReportSendingActionKind.Hold or ReportSendingActionKind.Stop or ReportSendingActionKind.Task;

    public static SendingRuleDraft From(ReportSendingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return new(
            [.. rule.If.Select(group => (IReadOnlyList<SendingConditionDraft>)[.. group.Select(From)])],
            From(rule.Then));
    }

    /// <summary>The On every Case list as the dialog draws it: actions only.</summary>
    public static SendingRuleDraft FromActions(IReadOnlyList<ReportSendingAction> actions) => new([], From(actions));

    private static SendingConditionDraft From(ReportSendingCondition condition) =>
        IsTyped(condition.Fact)
            ? new(condition.Fact, condition.Not, [], string.Join(", ", condition.Values))
            : new(condition.Fact, condition.Not, condition.Values, string.Empty);

    private static IReadOnlyList<SendingActionDraft> From(IReadOnlyList<ReportSendingAction> actions) =>
        [.. actions.Select(action => new SendingActionDraft(
            action.Kind,
            TakesAddresses(action.Kind) ? string.Join(", ", action.Addresses ?? []) : action.Text ?? string.Empty,
            action.File))];

    /// <summary>The rule this draft stands for, as typed.</summary>
    public ReportSendingRule ToRule() =>
        new([.. If.Select(group => (IReadOnlyList<ReportSendingCondition>)[.. group.Select(ToCondition)])], ToActions());

    /// <summary>The actions this draft stands for, as typed.</summary>
    public IReadOnlyList<ReportSendingAction> ToActions() => [.. Then.Select(ToAction)];

    private static ReportSendingCondition ToCondition(SendingConditionDraft condition) =>
        new(
            condition.Fact,
            condition.Fact switch
            {
                ReportSendingFact.Sender => Split(condition.Text, AddressSeparators),
                ReportSendingFact.RefPrefix => Split(condition.Text, TextSeparators),
                ReportSendingFact.PreviousJob => [],
                _ => condition.Choices
            },
            condition.Not);

    private static ReportSendingAction ToAction(SendingActionDraft action) =>
        TakesAddresses(action.Kind) ? new(action.Kind, Addresses: Split(action.Text, AddressSeparators))
        : TakesFile(action.Kind) ? new(action.Kind, File: action.File)
        : TakesText(action.Kind) ? new(action.Kind, Text: action.Text)
        : new(action.Kind);

    private static string[] Split(string? value, char[] separators) =>
        (value ?? string.Empty).Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// The draft a dialog form posted. Rows are read in the order of their
    /// numbers, so a row added in place under a higher number than the rest
    /// takes its place at the end of its group or list.
    /// </summary>
    public static SendingRuleDraft Read(IFormCollection form)
    {
        ArgumentNullException.ThrowIfNull(form);
        string[] Many(string name) => [.. form[name].Select(value => value ?? string.Empty).Where(value => value.Length > 0)];
        string One(string name) => form[name].ToString();

        var conditions = new List<(int Group, int Row, SendingConditionDraft Condition)>();
        var actions = new List<(int Row, SendingActionDraft Action)>();
        foreach (var key in form.Keys)
        {
            var parts = key.Split('.');
            if (parts.Length == 4
                && parts[0] == ConditionPrefix
                && parts[3] == "Fact"
                && TryIndex(parts[1], out var group)
                && TryIndex(parts[2], out var row)
                && Enum.TryParse<ReportSendingFact>(One(key), out var fact)
                && Enum.IsDefined(fact))
            {
                var prefix = $"{ConditionPrefix}.{parts[1]}.{parts[2]}";
                IReadOnlyList<string> choices = fact switch
                {
                    ReportSendingFact.ClaimSource => Many($"{prefix}.ClaimSource"),
                    ReportSendingFact.Repairer => Many($"{prefix}.Repairer"),
                    ReportSendingFact.Outcome => Many($"{prefix}.Outcome"),
                    ReportSendingFact.CaseType => Many($"{prefix}.CaseType"),
                    _ => []
                };
                conditions.Add((group, row, new(
                    fact,
                    One($"{prefix}.Not") == "true",
                    choices,
                    IsTyped(fact) ? One($"{prefix}.Text") : string.Empty)));
            }
            else if (parts.Length == 3
                && parts[0] == ActionPrefix
                && parts[2] == "Kind"
                && TryIndex(parts[1], out var actionRow)
                && Enum.TryParse<ReportSendingActionKind>(One(key), out var kind)
                && Enum.IsDefined(kind))
            {
                var prefix = $"{ActionPrefix}.{parts[1]}";
                ReportSendingFile? file = TakesFile(kind)
                    && Enum.TryParse<ReportSendingFile>(One($"{prefix}.File"), out var named)
                    && Enum.IsDefined(named)
                        ? named
                        : null;
                actions.Add((actionRow, new(
                    kind,
                    TakesAddresses(kind) ? One($"{prefix}.Addresses") : TakesText(kind) ? One($"{prefix}.Text") : string.Empty,
                    file)));
            }
        }

        return new(
            [.. conditions
                .OrderBy(item => item.Group)
                .ThenBy(item => item.Row)
                .GroupBy(item => item.Group)
                .Select(group => (IReadOnlyList<SendingConditionDraft>)[.. group.Select(item => item.Condition)])],
            [.. actions.OrderBy(item => item.Row).Select(item => item.Action)]);
    }

    /// <summary>
    /// The draft with one row added or removed, as the dialog's And, Or, Add
    /// action and remove buttons post it without script: <c>and:{group}</c>,
    /// <c>or</c>, <c>action</c>, <c>remove-condition:{group}:{row}</c> and
    /// <c>remove-action:{row}</c>, numbered as drawn. A group left with no
    /// condition goes. Anything else leaves the draft as it is.
    /// </summary>
    public SendingRuleDraft Change(string? change)
    {
        var parts = (change ?? string.Empty).Split(':');
        var groups = If.Select(group => group.ToList()).ToList();
        var actions = Then.ToList();
        switch (parts[0])
        {
            case "and" when parts.Length == 2 && TryIndex(parts[1], out var group) && group < groups.Count:
                groups[group].Add(BlankCondition);
                break;
            case "or" when parts.Length == 1:
                groups.Add([BlankCondition]);
                break;
            case "action" when parts.Length == 1:
                actions.Add(BlankAction);
                break;
            case "remove-condition" when parts.Length == 3
                && TryIndex(parts[1], out var group)
                && TryIndex(parts[2], out var row)
                && group < groups.Count
                && row < groups[group].Count:
                groups[group].RemoveAt(row);
                if (groups[group].Count == 0)
                {
                    groups.RemoveAt(group);
                }
                break;
            case "remove-action" when parts.Length == 2 && TryIndex(parts[1], out var row) && row < actions.Count:
                actions.RemoveAt(row);
                break;
            default:
                return this;
        }

        return new([.. groups.Select(group => (IReadOnlyList<SendingConditionDraft>)group)], actions);
    }

    private static bool TryIndex(string value, out int index) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out index) && index < 10_000;
}

/// <summary>The dialog a Sending rules address opens.</summary>
public enum SendingDialog
{
    None,
    Rule,
    EveryCase,
    Remove
}
