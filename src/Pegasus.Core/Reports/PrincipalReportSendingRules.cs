using System.Net.Mail;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Reports;

/// <summary>What one condition of a report sending rule reads.</summary>
/// <remarks>
/// <see cref="ClaimSource"/> and <see cref="Repairer"/> compare the Case's
/// chosen contact (its organisation id) with the condition's ids.
/// <see cref="Outcome"/> compares the Case's recorded assessment outcome code.
/// <see cref="SenderNot"/> holds when the originating instruction's effective
/// sender is none of the addresses. <see cref="Mentions"/> is searched for in
/// the instruction e-mail's subject and text, ignoring case and spaces, and is
/// asked only when the Case holds no instruction text. <see cref="ImagesFrom"/>
/// and <see cref="BodyshopMentions"/> have no Case fact of their own, so they
/// stay undecided until staff answer them on the delivery form.
/// <see cref="CaseType"/> compares the Case's type (<see cref="Cases.CaseType"/>:
/// Inspection, Audit or InspectionAndAudit; operator, 13 October 2026).
/// Inspection and Audit name the report being sent, so on an Inspection +
/// Audit Case they match its Inspection and its Audit report; InspectionAndAudit
/// matches either (<see cref="ReportDispatchFacts.ReportWork"/>).
/// <see cref="RefPrefix"/> holds when the report's reference starts with one
/// of its values and a dot (<c>a.PCH26118</c> has the prefix <c>a</c>).
/// <see cref="PreviousJob"/> takes no value and holds when another Case of the
/// same Principal with the same vehicle registration already has a sent
/// report (<see cref="ReportDispatchFacts.PreviousJob"/>). Neither is ever
/// asked (Report Sending SOP v8, 15 October 2026).
/// </remarks>
public enum ReportSendingConditionKind
{
    ClaimSource,
    Repairer,
    ImagesFrom,
    Mentions,
    SenderNot,
    Outcome,
    BodyshopMentions,
    CaseType,
    RefPrefix,
    PreviousJob
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
/// <param name="RateCard">Staff check the report's estimate against the rate card after sending.</param>
/// <param name="Attach">The attach flags this rule replaces for the send.</param>
public sealed record ReportSendingActions(
    IReadOnlyList<string> CcAdd,
    IReadOnlyList<string> CcRemove,
    string? Remind = null,
    string? Hold = null,
    string? Stop = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool RateCard = false,
    ReportSendingAttachOverride? Attach = null)
{
    public static ReportSendingActions None { get; } = new([], []);

    [JsonIgnore]
    public bool IsEmpty =>
        CcAdd.Count == 0 && CcRemove.Count == 0 && Remind is null && Hold is null && Stop is null
        && !RateCard && Attach is not { IsEmpty: false };
}

/// <summary>
/// A rule's attach override (Report Sending SOP v8, rule R14): each flag it
/// names replaces the Principal's for this send, and one it leaves null keeps
/// the Principal's. Report images is not among them, since the report is
/// generated before the rules are read.
/// </summary>
public sealed record ReportSendingAttachOverride(
    bool? FeeNoteSeparate = null,
    bool? Estimate = null,
    bool? Audatex = null,
    bool? VehicleImagesDocument = null,
    bool? FigureBreakdown = null)
{
    [JsonIgnore]
    public bool IsEmpty =>
        FeeNoteSeparate is null && Estimate is null && Audatex is null
        && VehicleImagesDocument is null && FigureBreakdown is null;

    /// <summary><paramref name="attach"/> with the flags this override names replaced.</summary>
    public ReportSendingAttachments Over(ReportSendingAttachments attach) => attach with
    {
        FeeNoteSeparate = FeeNoteSeparate ?? attach.FeeNoteSeparate,
        Estimate = Estimate ?? attach.Estimate,
        Audatex = Audatex ?? attach.Audatex,
        VehicleImagesDocument = VehicleImagesDocument ?? attach.VehicleImagesDocument,
        FigureBreakdown = FigureBreakdown ?? attach.FigureBreakdown
    };
}

public sealed record ReportSendingRule(
    ReportSendingRuleMatch Match,
    IReadOnlyList<ReportSendingCondition> If,
    ReportSendingActions Then);

/// <summary>
/// What goes with the report. Every report ends with its fee note;
/// <see cref="FeeNoteSeparate"/> means the separate fee note is attached beside
/// it when one is generated. <see cref="VehicleImagesDocument"/> and
/// <see cref="FigureBreakdown"/> name the generation's images pack and Repair
/// Spec companions, required before the report is sent. <see cref="Audatex"/>
/// attaches the Case's recognised filed estimates in the Audatex format and
/// <see cref="Estimate"/> the recognised ones in any other format.
/// <see cref="ReportImages"/> false means this Principal's report is generated
/// without vehicle images.
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
    UnknownContact,
    UnknownOutcome,
    UnknownCaseType,
    InvalidRefPrefix,
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
/// A Principal's report sending rules (Report Sending SOP v8, 15 October 2026):
/// who the report goes to and from, who is copied, what goes with it, what
/// holds or stops the delivery, and what staff must do after sending. Every
/// Principal has them; one the SOP does not name has <see cref="Default"/>.
/// </summary>
/// <param name="SendFromMailbox">The Outgoing mailbox the send form chooses for a new message when there is no instruction e-mail to reply from; staff may change it, and a reply always leaves from the mailbox the instruction arrived in.</param>
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
            rule.Then.Stop,
            rule.Then.RateCard,
            rule.Then.Attach));
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
    /// without case; texts trimmed and present where given; contact ids
    /// parseable; outcome codes known; reference prefixes letters only; every
    /// rule with a condition and an action, and an attach override only when
    /// it names a flag; name tokens known.
    /// </summary>
    public static PrincipalReportSendingRules Normalize(PrincipalReportSendingRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var sendFrom = string.IsNullOrWhiteSpace(rules.SendFromMailbox)
            ? null
            : Address(rules.SendFromMailbox, nameof(SendFromMailbox));
        var sendTo = AddressList(rules.SendTo, nameof(SendTo));
        var cc = AddressList(rules.Cc, nameof(Cc));
        var neverCc = AddressList(rules.NeverCc, nameof(NeverCc));
        if (rules.SendToOnly && sendTo.Length == 0)
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
            OptionalText(then.Stop, nameof(ReportSendingActions.Stop), index),
            then.RateCard,
            then.Attach is { IsEmpty: false } attach ? attach : null);
        if (actions.IsEmpty)
        {
            throw new ReportSendingRulesException(ReportSendingRulesRule.EmptyActions, nameof(ReportSendingRule.Then), index);
        }
        return new(rule.Match, conditions, actions);
    }

    private static ReportSendingCondition NormalizeCondition(ReportSendingCondition condition, int index)
    {
        if (condition.Kind == ReportSendingConditionKind.PreviousJob)
        {
            return new(condition.Kind, []);
        }
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
            ReportSendingConditionKind.CaseType =>
                values.Select(value => CaseTypeValues.Contains(value, StringComparer.Ordinal)
                    ? value
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.UnknownCaseType, field, index))
                    .Distinct(StringComparer.Ordinal).ToArray(),
            ReportSendingConditionKind.SenderNot => AddressList(values, field, index),
            ReportSendingConditionKind.RefPrefix =>
                values.Select(value => value.All(char.IsAsciiLetter)
                    ? value
                    : throw new ReportSendingRulesException(ReportSendingRulesRule.InvalidRefPrefix, field, index))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            _ => values.Select(value => RequiredText(value, field, index)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
        return new(condition.Kind, normalized);
    }

    private static IReadOnlyList<string> OutcomeCodes =>
        AssessmentVocabulary.Definitions[AssessmentVocabulary.Outcome].Codes!;

    /// <summary>The Case types a rule can name: every type that sends a report, so not Triage.</summary>
    public static IReadOnlyList<string> CaseTypeValues { get; } =
        [.. Enum.GetValues<CaseType>().Where(type => type != Cases.CaseType.Triage).Select(type => type.ToString())];

    private static string Address(string value, string field, int? ruleIndex = null)
    {
        var trimmed = value.Trim();
        return MailAddress.TryCreate(trimmed, out _)
            ? trimmed
            : throw new ReportSendingRulesException(ReportSendingRulesRule.InvalidAddress, field, ruleIndex);
    }

    private static string[] AddressList(IEnumerable<string>? values, string field, int? ruleIndex = null) =>
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
