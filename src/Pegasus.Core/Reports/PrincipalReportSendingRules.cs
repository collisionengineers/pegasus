using System.Net.Mail;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Pegasus.Core.Assessment;

namespace Pegasus.Core.Reports;

/// <summary>What one condition of a report sending rule reads.</summary>
/// <remarks>
/// <see cref="ClaimSource"/> and <see cref="Repairer"/> compare the Case's
/// chosen contact (its organisation id) with the condition's ids.
/// <see cref="Outcome"/> compares the Case's recorded assessment outcome code.
/// <see cref="SenderNot"/> holds when the originating instruction's effective
/// sender is none of the addresses. <see cref="ImagesFrom"/> and
/// <see cref="Mentions"/> have no Case fact of their own, so they stay
/// undecided until staff answer them on the delivery form.
/// </remarks>
public enum ReportSendingConditionKind
{
    ClaimSource,
    Repairer,
    ImagesFrom,
    Mentions,
    SenderNot,
    Outcome
}

public enum ReportSendingRuleMatch
{
    All,
    Any
}

/// <summary>One condition; several values are alternatives (any one satisfies it).</summary>
public sealed record ReportSendingCondition(
    ReportSendingConditionKind Kind,
    IReadOnlyList<string> Values);

/// <summary>What a rule does when its conditions hold.</summary>
public sealed record ReportSendingActions(
    IReadOnlyList<string> CcAdd,
    IReadOnlyList<string> CcRemove,
    string? Remind = null,
    string? Hold = null,
    string? Stop = null)
{
    public static ReportSendingActions None { get; } = new([], []);

    [JsonIgnore]
    public bool IsEmpty =>
        CcAdd.Count == 0 && CcRemove.Count == 0 && Remind is null && Hold is null && Stop is null;
}

public sealed record ReportSendingRule(
    ReportSendingRuleMatch Match,
    IReadOnlyList<ReportSendingCondition> If,
    ReportSendingActions Then);

/// <summary>
/// What goes with the report. <see cref="FeeNoteSeparate"/> false means the
/// fee note is the report's final pages. <see cref="VehicleImagesDocument"/>
/// and <see cref="FigureBreakdown"/> name the generation's images pack and
/// Repair Spec companions, required before the delivery is prepared.
/// <see cref="Estimate"/> and <see cref="Audatex"/> attach the Case's
/// recognised filed estimate documents. <see cref="ReportImages"/> false is
/// a warning that this Principal's report should carry no vehicle images.
/// </summary>
public sealed record ReportSendingAttachments(
    bool FeeNoteSeparate = true,
    bool Estimate = false,
    bool Audatex = false,
    bool ReportImages = true,
    bool VehicleImagesDocument = false,
    bool FigureBreakdown = false)
{
    public static ReportSendingAttachments Default { get; } = new();
}

/// <summary>
/// How the attached report is named for this Principal, with the tokens
/// <c>{ref}</c>, <c>{reg}</c> and <c>{outcome}</c>: <see cref="First"/> on a
/// first send, <see cref="Resend"/> on every later one.
/// </summary>
public sealed record ReportAttachmentNamePattern(string First, string Resend)
{
    public static IReadOnlyList<string> Tokens { get; } = ["ref", "reg", "outcome"];
}

public enum ReportSendingRulesRule
{
    InvalidAddress,
    AddressInCcAndNeverCc,
    SendToOnlyNeedsAddresses,
    TextRequired,
    TextTooLong,
    UnknownContact,
    UnknownOutcome,
    EmptyRule,
    EmptyCondition,
    EmptyActions,
    UnknownNameToken
}

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
/// A Principal's report sending rules (Report Sending SOP v5, 2 October 2026):
/// who the report goes to and from, who is copied, what goes with it, what
/// holds or stops the delivery, and what staff must do after sending. Null on
/// a Principal means the SOP has no entry, and delivery keeps its plain
/// behaviour (original sender plus the configured extra addresses).
/// </summary>
/// <param name="SendFromMailbox">The approved mailbox address a new message leaves from when the Case has no retained instruction e-mail; a reply always leaves from the mailbox that holds the instruction.</param>
/// <param name="SendTo">Fixed To addresses, which replace the original sender. Empty means the original instruction sender.</param>
/// <param name="SendToOnly">Only <paramref name="SendTo"/>, with no Cc at all.</param>
/// <param name="ReplyAll">Keep the instruction's own Cc addresses.</param>
/// <param name="Cc">Always copied.</param>
/// <param name="NeverCc">Always removed, even from the instruction's Cc list; removals win.</param>
/// <param name="GarageFigures">On a repairable outcome, staff send the figures to the garage after sending.</param>
/// <param name="Hold">Text staff must tick as done before Send.</param>
/// <param name="Reminders">After-sending tasks created when Report sent is recorded.</param>
public sealed record PrincipalReportSendingRules(
    string? SendFromMailbox,
    IReadOnlyList<string> SendTo,
    bool SendToOnly,
    bool ReplyAll,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> NeverCc,
    ReportSendingAttachments Attach,
    bool GarageFigures,
    string? Hold,
    IReadOnlyList<string> Reminders,
    IReadOnlyList<ReportSendingRule> Rules,
    ReportAttachmentNamePattern? AttachmentName)
{
    public const int MaximumTextLength = 500;

    /// <summary>The SOP's defaults: reply to the original sender, keep their Cc list, separate fee note.</summary>
    public static PrincipalReportSendingRules Default { get; } = new(
        null, [], false, true, [], [], ReportSendingAttachments.Default, false, null, [], [], null);

    public bool Equals(PrincipalReportSendingRules? other) =>
        other is not null && string.Equals(Canonical(), other.Canonical(), StringComparison.Ordinal);

    public override int GetHashCode() => Canonical().GetHashCode(StringComparison.Ordinal);

    /// <summary>
    /// One deterministic text for the whole value: equality, hashing and the
    /// delivery fingerprint all read it. Addresses are compared without case.
    /// </summary>
    public string Canonical()
    {
        static string List(IEnumerable<string> values) => string.Join("\u001e", values);
        static string Addresses(IEnumerable<string> values) => List(values.Select(v => v.ToUpperInvariant()));
        var rules = Rules.Select(rule => string.Join("\u001d",
            rule.Match,
            string.Join("\u001c", rule.If.Select(c => $"{c.Kind}:{List(c.Values)}")),
            Addresses(rule.Then.CcAdd),
            Addresses(rule.Then.CcRemove),
            rule.Then.Remind,
            rule.Then.Hold,
            rule.Then.Stop));
        return string.Join("\u001f",
            SendFromMailbox?.ToUpperInvariant(),
            Addresses(SendTo),
            SendToOnly,
            ReplyAll,
            Addresses(Cc),
            Addresses(NeverCc),
            Attach,
            GarageFigures,
            Hold,
            List(Reminders),
            string.Join("\u001b", rules),
            AttachmentName?.First,
            AttachmentName?.Resend);
    }

    /// <summary>
    /// The one shape stored rules take: addresses trimmed, valid and unique
    /// without case; texts trimmed, present where given and within
    /// <see cref="MaximumTextLength"/>; contact ids parseable; outcome codes
    /// known; every rule with a condition and an action; name tokens known.
    /// </summary>
    public static PrincipalReportSendingRules Normalize(PrincipalReportSendingRules? rules)
    {
        rules ??= Default;
        var sendFrom = string.IsNullOrWhiteSpace(rules.SendFromMailbox)
            ? null
            : Address(rules.SendFromMailbox, nameof(SendFromMailbox));
        var sendTo = AddressList(rules.SendTo, nameof(SendTo));
        var cc = AddressList(rules.Cc, nameof(Cc));
        var neverCc = AddressList(rules.NeverCc, nameof(NeverCc));
        if (rules.SendToOnly && sendTo.Count == 0)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.SendToOnlyNeedsAddresses, nameof(SendTo));
        }
        if (cc.Intersect(neverCc, StringComparer.OrdinalIgnoreCase).Any())
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.AddressInCcAndNeverCc, nameof(NeverCc));
        }
        var hold = OptionalText(rules.Hold, nameof(Hold));
        var reminders = (rules.Reminders ?? [])
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => RequiredText(text, nameof(Reminders)))
            .ToArray();
        var ruleList = (rules.Rules ?? []).Select(NormalizeRule).ToArray();
        var name = rules.AttachmentName is { } pattern
            ? new ReportAttachmentNamePattern(
                NamePattern(pattern.First, nameof(ReportAttachmentNamePattern.First)),
                NamePattern(pattern.Resend, nameof(ReportAttachmentNamePattern.Resend)))
            : null;
        return new(
            sendFrom, sendTo, rules.SendToOnly, rules.ReplyAll, cc, neverCc,
            rules.Attach ?? ReportSendingAttachments.Default, rules.GarageFigures,
            hold, reminders, ruleList, name);
    }

    private static ReportSendingRule NormalizeRule(ReportSendingRule rule, int index)
    {
        if (rule.If is not { Count: > 0 })
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyRule, nameof(ReportSendingRule.If), index);
        }
        var conditions = rule.If.Select(condition => NormalizeCondition(condition, index)).ToArray();
        var then = rule.Then ?? ReportSendingActions.None;
        var actions = new ReportSendingActions(
            AddressList(then.CcAdd, nameof(ReportSendingActions.CcAdd), index),
            AddressList(then.CcRemove, nameof(ReportSendingActions.CcRemove), index),
            OptionalText(then.Remind, nameof(ReportSendingActions.Remind), index),
            OptionalText(then.Hold, nameof(ReportSendingActions.Hold), index),
            OptionalText(then.Stop, nameof(ReportSendingActions.Stop), index));
        if (actions.IsEmpty)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyActions, nameof(ReportSendingRule.Then), index);
        }
        return new(rule.Match, conditions, actions);
    }

    private static ReportSendingCondition NormalizeCondition(ReportSendingCondition condition, int index)
    {
        var field = condition.Kind.ToString();
        var values = (condition.Values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        if (values.Length == 0)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyCondition, field, index);
        }
        IReadOnlyList<string> normalized = condition.Kind switch
        {
            ReportSendingConditionKind.ClaimSource or ReportSendingConditionKind.Repairer =>
                values.Select(value => Guid.TryParse(value, out var id) && id != Guid.Empty
                    ? id.ToString("D")
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.UnknownContact, field, index))
                    .Distinct(StringComparer.Ordinal).ToArray(),
            ReportSendingConditionKind.Outcome =>
                values.Select(value => OutcomeCodes.Contains(value, StringComparer.Ordinal)
                    ? value
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.UnknownOutcome, field, index))
                    .Distinct(StringComparer.Ordinal).ToArray(),
            ReportSendingConditionKind.SenderNot => AddressList(values, field, index),
            _ => values.Select(value => RequiredText(value, field, index)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
        return new(condition.Kind, normalized);
    }

    private static IReadOnlyList<string> OutcomeCodes =>
        AssessmentVocabulary.Definitions[AssessmentVocabulary.Outcome].Codes!;

    private static string Address(string value, string field, int? ruleIndex = null)
    {
        var trimmed = value.Trim();
        return MailAddress.TryCreate(trimmed, out _)
            ? trimmed
            : throw new ReportSendingRulesException(ReportSendingRulesRule.InvalidAddress, field, ruleIndex);
    }

    private static IReadOnlyList<string> AddressList(IEnumerable<string>? values, string field, int? ruleIndex = null) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Address(value, field, ruleIndex))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? OptionalText(string? value, string field, int? ruleIndex = null) =>
        string.IsNullOrWhiteSpace(value) ? null : RequiredText(value, field, ruleIndex);

    private static string RequiredText(string? value, string field, int? ruleIndex = null)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.TextRequired, field, ruleIndex);
        }
        if (trimmed.Length > MaximumTextLength)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.TextTooLong, field, ruleIndex);
        }
        return trimmed;
    }

    private static string NamePattern(string? value, string field)
    {
        var trimmed = RequiredText(value, field);
        foreach (Match match in Regex.Matches(trimmed, @"\{([^{}]*)\}"))
        {
            if (!ReportAttachmentNamePattern.Tokens.Contains(match.Groups[1].Value, StringComparer.Ordinal))
            {
                throw new ReportSendingRulesException(ReportSendingRulesRule.UnknownNameToken, field);
            }
        }
        return trimmed;
    }
}
