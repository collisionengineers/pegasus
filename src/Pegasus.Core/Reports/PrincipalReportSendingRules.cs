using System.Net.Mail;
using System.Text.Json.Serialization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Reports;

/// <summary>What one condition of a report sending rule reads.</summary>
/// <remarks>
/// <see cref="ClaimSource"/> and <see cref="Repairer"/> compare the Case's
/// chosen contact (its organisation id) with the condition's ids.
/// <see cref="Outcome"/> compares the Case's recorded assessment outcome code.
/// <see cref="Sender"/> compares the originating instruction's effective
/// sender with the addresses. Every fact is one the Case holds: staff are
/// asked nothing (operator, 12 November 2026; R1023, R1024), and a condition
/// on a fact the Case has not set is not met, while its "is not" is
/// (operator, 25 November 2026; R1269). "Instruction mentions" is retired
/// (R1075, R1268).
/// <see cref="CaseType"/> compares the Case's type (<see cref="Cases.CaseType"/>:
/// Inspection, Audit or InspectionAndAudit; operator, 13 October 2026).
/// Inspection and Audit name the report being sent, so on an Inspection +
/// Audit Case they match its Inspection and its Audit report; InspectionAndAudit
/// matches either (<see cref="ReportDispatchFacts.ReportWork"/>).
/// <see cref="RefPrefix"/> holds when the report's reference starts with one
/// of its values and a dot (<c>a.PCH26118</c> has the prefix <c>a</c>).
/// <see cref="PreviousJob"/> takes no value and holds when another Case of the
/// same Principal with the same vehicle registration already has a sent
/// report (<see cref="ReportDispatchFacts.PreviousJob"/>; Report Sending SOP
/// v8, 15 October 2026).
/// The enum is stored by name wherever it is written.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ReportSendingFact>))]
public enum ReportSendingFact
{
    ClaimSource,
    Repairer,
    Outcome,
    CaseType,
    Sender,
    RefPrefix,
    PreviousJob
}

/// <summary>
/// One condition: the Case's <paramref name="Fact"/> is one of
/// <paramref name="Values"/> (any one satisfies it), or with
/// <paramref name="Not"/> it is none of them, which an unset fact is.
/// </summary>
public sealed record ReportSendingCondition(
    ReportSendingFact Fact,
    IReadOnlyList<string> Values,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Not = false);

/// <summary>What one action of a report sending rule does; stored by name wherever it is written.</summary>
/// <remarks>
/// <see cref="SendTo"/> replaces the To list (a later one wins) and
/// <see cref="SendToOnly"/> leaves out every Cc. <see cref="ReplyAll"/> keeps
/// the instruction's own Cc list. <see cref="Cc"/> copies its addresses and
/// <see cref="NeverCc"/> keeps its addresses out wherever they came from.
/// <see cref="Attach"/> and <see cref="DoNotAttach"/> switch one file on or
/// off for the send (a later one wins). <see cref="Hold"/> is text staff tick
/// as done before Send, <see cref="Stop"/> refuses the send unless staff give
/// a reason (the first one met is kept), and <see cref="Task"/> is what staff
/// owe after sending, created when Report sent is recorded (R1270).
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ReportSendingActionKind>))]
public enum ReportSendingActionKind
{
    SendTo,
    SendToOnly,
    ReplyAll,
    Cc,
    NeverCc,
    Attach,
    DoNotAttach,
    Hold,
    Stop,
    Task
}

/// <summary>
/// What an attach action names. Every report ends with its fee note;
/// <see cref="FeeNoteSeparate"/> attaches the separate fee note beside it when
/// one is generated. <see cref="VehicleImagesDocument"/> and
/// <see cref="FigureBreakdown"/> name the generation's images pack and Repair
/// Spec companions, required before the report is sent. <see cref="Audatex"/>
/// attaches the Case's recognised filed estimates in the Audatex format and
/// <see cref="Estimate"/> the recognised ones in any other format.
/// <see cref="OfferExternalEstimates"/> and <see cref="OfferRepairInvoices"/>
/// list the Case's confirmed External estimates and Repair invoices in the
/// Send report dialog, unticked (operator, 17 November 2026; R1151).
/// Stored by name wherever it is written.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReportSendingFile>))]
public enum ReportSendingFile
{
    FeeNoteSeparate,
    Estimate,
    Audatex,
    VehicleImagesDocument,
    FigureBreakdown,
    OfferExternalEstimates,
    OfferRepairInvoices
}

/// <summary>
/// One action. <paramref name="Addresses"/> belongs to Send to, Cc and Never
/// cc; <paramref name="File"/> to Attach and Do not attach;
/// <paramref name="Text"/> to Hold, Stop and Task. Send to only and Reply all
/// carry nothing. What an action does not carry is not written.
/// </summary>
public sealed record ReportSendingAction(
    ReportSendingActionKind Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Addresses = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReportSendingFile? File = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Text = null);

/// <summary>
/// One built rule: when any group of <paramref name="If"/> has all its
/// conditions met (groups are joined by "or", a group's conditions by
/// "and"), its <paramref name="Then"/> actions apply after the every-Case
/// ones and those of the rules before it.
/// </summary>
public sealed record ReportSendingRule(
    IReadOnlyList<IReadOnlyList<ReportSendingCondition>> If,
    IReadOnlyList<ReportSendingAction> Then);

/// <summary>
/// The files a report send attaches or offers, as the dispatch plan resolves
/// them from the actions that applied. Nothing is attached unless an action
/// says so.
/// </summary>
public sealed record ReportSendingAttachments(
    bool FeeNoteSeparate = false,
    bool Estimate = false,
    bool Audatex = false,
    bool VehicleImagesDocument = false,
    bool FigureBreakdown = false,
    bool OfferExternalEstimates = false,
    bool OfferRepairInvoices = false)
{
    public static ReportSendingAttachments None { get; } = new();

    /// <summary>These flags with <paramref name="file"/> switched to <paramref name="on"/>.</summary>
    public ReportSendingAttachments With(ReportSendingFile file, bool on) => file switch
    {
        ReportSendingFile.FeeNoteSeparate => this with { FeeNoteSeparate = on },
        ReportSendingFile.Estimate => this with { Estimate = on },
        ReportSendingFile.Audatex => this with { Audatex = on },
        ReportSendingFile.VehicleImagesDocument => this with { VehicleImagesDocument = on },
        ReportSendingFile.FigureBreakdown => this with { FigureBreakdown = on },
        ReportSendingFile.OfferExternalEstimates => this with { OfferExternalEstimates = on },
        ReportSendingFile.OfferRepairInvoices => this with { OfferRepairInvoices = on },
        _ => this
    };
}

public enum ReportSendingRulesRule
{
    InvalidAddress,
    AddressInCcAndNeverCc,
    SendToOnlyNeedsAddresses,
    TextRequired,
    UnknownContact,
    UnknownOutcome,
    UnknownCaseType,
    InvalidRefPrefix,
    EmptyRule,
    EmptyCondition,
    EmptyActions
}

/// <summary>
/// Why rules were refused. <see cref="RuleIndex"/> is null for the every-Case
/// list and the rule's index otherwise; <see cref="Field"/> names the fact or
/// the action kind.
/// </summary>
public sealed class ReportSendingRulesException(
    ReportSendingRulesRule rule,
    string field,
    int? ruleIndex = null)
    : ArgumentException("The report sending rules are not valid.")
{
    public ReportSendingRulesRule Rule { get; } = rule;
    public string Field { get; } = field;
    public int? RuleIndex { get; } = ruleIndex;
}

/// <summary>
/// A Principal's report sending rules (Report Sending SOP v8, 15 October 2026,
/// built as rules since 25 November 2026; R1267): the actions every Case
/// takes, then the rules whose conditions the Case meets, in order. A send
/// does nothing no action says. Every Principal has them; one the SOP does
/// not name has <see cref="Default"/>.
/// </summary>
/// <remarks>
/// What the report is called is the Principal's report names
/// (<see cref="PrincipalReportNames"/>, R740), and the mailbox a send starts
/// on and whether the report carries vehicle images are the Principal's own
/// settings, not sending rules.
/// </remarks>
public sealed record PrincipalReportSendingRules(
    [property: JsonRequired] IReadOnlyList<ReportSendingAction> EveryCase,
    [property: JsonRequired] IReadOnlyList<ReportSendingRule> Rules)
{
    /// <summary>The SOP's defaults as actions: reply all, with the fee note separate.</summary>
    public static PrincipalReportSendingRules Default { get; } = new(
        [
            new(ReportSendingActionKind.ReplyAll),
            new(ReportSendingActionKind.Attach, File: ReportSendingFile.FeeNoteSeparate)
        ],
        []);

    public bool Equals(PrincipalReportSendingRules? other) =>
        other is not null && string.Equals(Canonical(), other.Canonical(), StringComparison.Ordinal);

    public override int GetHashCode() => Canonical().GetHashCode(StringComparison.Ordinal);

    /// <summary>Whether a condition of some rule reads <paramref name="fact"/>.</summary>
    public bool Names(ReportSendingFact fact) =>
        Rules.Any(rule => rule.If.Any(group => group.Any(condition => condition.Fact == fact)));

    /// <summary>Whether an Attach action anywhere in the rules names <paramref name="file"/>.</summary>
    public bool MayAttach(ReportSendingFile file) =>
        EveryCase.Concat(Rules.SelectMany(rule => rule.Then))
            .Any(action => action.Kind == ReportSendingActionKind.Attach && action.File == file);

    /// <summary>
    /// One deterministic text for the whole value: equality, hashing and the
    /// delivery fingerprint all read it. Addresses are compared without case.
    /// </summary>
    public string Canonical()
    {
        static string List(IEnumerable<string> values) => string.Join("\u001a", values);
        static string Action(ReportSendingAction action) => string.Join(
            "\u001b",
            action.Kind.ToString(),
            List((action.Addresses ?? []).Select(address => address.ToUpperInvariant())),
            action.File?.ToString() ?? "",
            action.Text ?? "");
        static string Actions(IEnumerable<ReportSendingAction> actions) =>
            string.Join("\u001c", actions.Select(Action));
        static string Condition(ReportSendingCondition condition) =>
            $"{condition.Fact}:{(condition.Not ? "not" : "is")}:{List(condition.Values)}";
        var rules = Rules.Select(rule => string.Join(
            "\u001d",
            string.Join("\u001c", rule.If.Select(group => string.Join("\u001b", group.Select(Condition)))),
            Actions(rule.Then)));
        return string.Join("\u001f", Actions(EveryCase), string.Join("\u001e", rules));
    }

    /// <summary>
    /// The one shape stored rules take: every action carries only what its
    /// kind uses, addresses trimmed, valid and unique without case, texts
    /// trimmed and present; every condition's values valid for its fact
    /// (none for Previous job); every rule with a condition and an action.
    /// The every-Case list may be empty.
    /// </summary>
    public static PrincipalReportSendingRules Normalize(PrincipalReportSendingRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var everyCase = NormalizeActions(rules.EveryCase, null);
        CheckActions(everyCase, null, everyCaseSendsTo: false);
        var everyCaseSendsTo = everyCase.Any(action => action.Kind == ReportSendingActionKind.SendTo);
        var ruleList = (rules.Rules ?? [])
            .Select((rule, index) => NormalizeRule(rule, index, everyCaseSendsTo))
            .ToArray();
        return new(everyCase, ruleList);
    }

    private static ReportSendingRule NormalizeRule(ReportSendingRule rule, int index, bool everyCaseSendsTo)
    {
        ArgumentNullException.ThrowIfNull(rule);
        IReadOnlyList<ReportSendingCondition>[] groups =
        [
            .. (rule.If ?? [])
                .Where(group => group is { Count: > 0 })
                .Select(group => (IReadOnlyList<ReportSendingCondition>)group
                    .Select(condition => NormalizeCondition(condition, index))
                    .ToArray())
        ];
        if (groups.Length == 0)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyRule, nameof(ReportSendingRule.If), index);
        }
        var actions = NormalizeActions(rule.Then, index);
        if (actions.Length == 0)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyActions, nameof(ReportSendingRule.Then), index);
        }
        CheckActions(actions, index, everyCaseSendsTo);
        return new(groups, actions);
    }

    private static ReportSendingAction[] NormalizeActions(IReadOnlyList<ReportSendingAction>? actions, int? index) =>
        [.. (actions ?? []).Select(action => NormalizeAction(action, index))];

    private static ReportSendingAction NormalizeAction(ReportSendingAction action, int? index)
    {
        ArgumentNullException.ThrowIfNull(action);
        var field = action.Kind.ToString();
        switch (action.Kind)
        {
            case ReportSendingActionKind.SendTo:
            case ReportSendingActionKind.Cc:
            case ReportSendingActionKind.NeverCc:
                var addresses = AddressList(action.Addresses, field, index);
                return addresses.Length == 0
                    ? throw new ReportSendingRulesException(ReportSendingRulesRule.InvalidAddress, field, index)
                    : new(action.Kind, Addresses: addresses);
            case ReportSendingActionKind.Attach:
            case ReportSendingActionKind.DoNotAttach:
                return action.File is { } file && Enum.IsDefined(file)
                    ? new(action.Kind, File: file)
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyActions, field, index);
            case ReportSendingActionKind.Hold:
            case ReportSendingActionKind.Stop:
            case ReportSendingActionKind.Task:
                return new(action.Kind, Text: RequiredText(action.Text, field, index));
            case ReportSendingActionKind.SendToOnly:
            case ReportSendingActionKind.ReplyAll:
                return new(action.Kind);
            default:
                throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyActions, field, index);
        }
    }

    /// <summary>
    /// The two checks across one list of actions: Send to only needs a Send
    /// to in the same list or, for a rule, in the every-Case list; and no
    /// address is both copied and never copied in the same list.
    /// </summary>
    private static void CheckActions(IReadOnlyList<ReportSendingAction> actions, int? index, bool everyCaseSendsTo)
    {
        if (actions.Any(action => action.Kind == ReportSendingActionKind.SendToOnly)
            && !everyCaseSendsTo
            && !actions.Any(action => action.Kind == ReportSendingActionKind.SendTo))
        {
            throw new ReportSendingRulesException(
                ReportSendingRulesRule.SendToOnlyNeedsAddresses, nameof(ReportSendingActionKind.SendTo), index);
        }
        IEnumerable<string> AddressesOf(ReportSendingActionKind kind) =>
            actions.Where(action => action.Kind == kind).SelectMany(action => action.Addresses ?? []);
        if (AddressesOf(ReportSendingActionKind.Cc)
            .Intersect(AddressesOf(ReportSendingActionKind.NeverCc), StringComparer.OrdinalIgnoreCase)
            .Any())
        {
            throw new ReportSendingRulesException(
                ReportSendingRulesRule.AddressInCcAndNeverCc, nameof(ReportSendingActionKind.NeverCc), index);
        }
    }

    private static ReportSendingCondition NormalizeCondition(ReportSendingCondition condition, int index)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var field = condition.Fact.ToString();
        if (!Enum.IsDefined(condition.Fact))
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyCondition, field, index);
        }
        if (condition.Fact == ReportSendingFact.PreviousJob)
        {
            return new(condition.Fact, [], condition.Not);
        }
        var values = (condition.Values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        if (values.Length == 0)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyCondition, field, index);
        }
        IReadOnlyList<string> normalized = condition.Fact switch
        {
            ReportSendingFact.ClaimSource or ReportSendingFact.Repairer =>
                values.Select(value => Guid.TryParse(value, out var id) && id != Guid.Empty
                    ? id.ToString("D")
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.UnknownContact, field, index))
                    .Distinct(StringComparer.Ordinal).ToArray(),
            ReportSendingFact.Outcome =>
                values.Select(value => OutcomeCodes.Contains(value, StringComparer.Ordinal)
                    ? value
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.UnknownOutcome, field, index))
                    .Distinct(StringComparer.Ordinal).ToArray(),
            ReportSendingFact.CaseType =>
                values.Select(value => CaseTypeValues.Contains(value, StringComparer.Ordinal)
                    ? value
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.UnknownCaseType, field, index))
                    .Distinct(StringComparer.Ordinal).ToArray(),
            ReportSendingFact.Sender => AddressList(values, field, index),
            _ =>
                values.Select(value => value.All(char.IsAsciiLetter)
                    ? value
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.InvalidRefPrefix, field, index))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
        return new(condition.Fact, normalized, condition.Not);
    }

    private static IReadOnlyList<string> OutcomeCodes =>
        AssessmentVocabulary.Definitions[AssessmentVocabulary.Outcome].Codes!;

    /// <summary>The Case types a rule can name: every type that sends a report, so not Triage.</summary>
    public static IReadOnlyList<string> CaseTypeValues { get; } =
        [.. Enum.GetValues<CaseType>().Where(type => type != Cases.CaseType.Triage).Select(type => type.ToString())];

    /// <summary>Whether <paramref name="value"/> is one address a rule or a Send from can hold.</summary>
    public static bool IsAddress(string? value) =>
        !string.IsNullOrWhiteSpace(value) && MailAddress.TryCreate(value.Trim(), out _);

    private static string Address(string value, string field, int? ruleIndex)
    {
        var trimmed = value.Trim();
        return IsAddress(trimmed)
            ? trimmed
            : throw new ReportSendingRulesException(ReportSendingRulesRule.InvalidAddress, field, ruleIndex);
    }

    private static string[] AddressList(IEnumerable<string>? values, string field, int? ruleIndex) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Address(value, field, ruleIndex))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string RequiredText(string? value, string field, int? ruleIndex)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.TextRequired, field, ruleIndex);
        }
        return trimmed;
    }
}
