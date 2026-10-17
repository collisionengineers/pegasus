using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Reports;

/// <summary>
/// The Case's retained instruction e-mail as the report send needs it: the
/// message a report replies to, the approved mailbox that holds it, and who
/// sent and copied it.
/// </summary>
public sealed record ReportInstructionMessage(
    Guid RetainedMessageId,
    Guid MailboxId,
    string MailboxAddress,
    string ImmutableMessageId,
    string? InternetMessageId,
    string? ConversationId,
    string? EffectiveSender,
    IReadOnlyList<string> Cc,
    string? Subject);

/// <summary>
/// One of the Case's recognised filed estimate documents as an attachment,
/// with the format it was read in (<see cref="Provider"/>, such as "Audatex").
/// </summary>
public sealed record FiledEstimateAttachment(StaffMailAttachment Attachment, string? Provider);

/// <summary>
/// Everything one report dispatch is planned from. The first group is read
/// from the Case, its Principal and its instruction; the init properties are
/// what the generation and the Case's files add.
/// </summary>
/// <param name="Rules">The Principal's report sending rules. Every Principal has them.</param>
/// <param name="Outcome">The recorded outcome code, such as <c>repairable</c>.</param>
public sealed record ReportDispatchFacts(
    string CaseReference,
    string? Registration,
    string? PrincipalName,
    PrincipalReportSendingRules Rules,
    ReportInstructionMessage? Instruction,
    Guid? ClaimSourceId = null,
    string? ClaimSourceName = null,
    Guid? RepairerId = null,
    string? RepairerName = null,
    string? Outcome = null)
{
    /// <summary>The Case's recognised filed estimate documents, each with its format.</summary>
    public IReadOnlyList<FiledEstimateAttachment> FiledEstimates { get; init; } = [];

    /// <summary>The kinds the viewed generation has Confirmed.</summary>
    public IReadOnlyList<CaseReportArtifactKind> ConfirmedArtifacts { get; init; } = [];

    public CaseReportSendHistory History { get; init; } = CaseReportSendHistory.None;

    /// <summary>
    /// The effective sender of the instruction that opened the Case when the
    /// Case holds no mailbox instruction to reply to (an uploaded e-mail), so
    /// the original sender is still addressed.
    /// </summary>
    public string? OriginalSender { get; init; }

    /// <summary>
    /// The instruction e-mail's subject and plain text, which "Instruction
    /// mentions" conditions are searched in. Null when the Case holds no
    /// instruction text. It is read for the plan and never stored with a send.
    /// </summary>
    public string? InstructionText { get; init; }

    /// <summary>The Case's type, which "Case type" conditions compare.</summary>
    public CaseType? CaseType { get; init; }

    /// <summary>
    /// The work whose report is being sent: the primary work's is the
    /// Inspection report and the Audit work's the Audit report. On an
    /// Inspection + Audit Case "Case type" conditions compare the report
    /// being sent; a send carrying both reports is the work it is sent from.
    /// </summary>
    public CaseWorkKind? ReportWork { get; init; }

    /// <summary>
    /// Whether another Case of the same Principal with the same vehicle
    /// registration already has a sent report, which "Previous job"
    /// conditions read (operator, 16 October 2026). The dispatch facts query
    /// looks it up when a rule names one.
    /// </summary>
    public bool PreviousJob { get; init; }

    /// <summary>The effective sender of the instruction, however the Case holds it.</summary>
    public string? Sender => Instruction?.EffectiveSender ?? OriginalSender;

    /// <summary>The names of the contacts the rules mention, for the questions staff are asked.</summary>
    public IReadOnlyDictionary<Guid, string> ContactNames { get; init; } = new Dictionary<Guid, string>();

    /// <summary>
    /// One value for everything that decides which questions the form asks and
    /// what its answers mean. A send carries the value its form was drawn
    /// from, so an answer is never applied to rules staff did not see.
    /// </summary>
    public string Fingerprint => ReportDispatchPolicy.Fingerprint(this);
}

/// <summary>Staff's answer to one rule condition the Case cannot answer itself.</summary>
public sealed record ReportRuleDecision(int RuleIndex, int ConditionIndex, bool Holds);

public sealed record ReportDispatchRecipient(string Address, string Source);

public sealed record ReportDispatchRemoval(string Address, string Reason);

public sealed record ReportDispatchQuestion(int RuleIndex, int ConditionIndex, string Question);

/// <summary>
/// What a report send records with its operation: the answers staff gave, the
/// holds they ticked as done, a Stop they overrode and why, and what they
/// still owe after sending (which becomes Case tasks when Report sent is
/// recorded).
/// </summary>
public sealed record ReportDispatchRecord(
    IReadOnlyList<ReportRuleDecision> Decisions,
    IReadOnlyList<string> Holds,
    string? StopOverridden,
    string? StopOverrideReason,
    IReadOnlyList<string> AfterSendTasks);

/// <summary>
/// The one plan a report send follows. <see cref="Holds"/> are ticked off
/// before Send; <see cref="Stop"/> refuses the send unless casework staff give
/// a reason. <see cref="StopPossible"/> says an unanswered question could
/// still stop it. <see cref="Excluded"/> is every address the rules keep out,
/// whether or not the plan had added it; <see cref="Removed"/> is the ones it
/// took out of the recipients it made.
/// </summary>
public sealed record ReportDispatchPlan(
    IReadOnlyList<ReportDispatchRecipient> To,
    IReadOnlyList<ReportDispatchRecipient> Cc,
    IReadOnlyList<ReportDispatchRemoval> Removed,
    IReadOnlyList<ReportDispatchRemoval> Excluded,
    string Subject,
    IReadOnlyList<CaseReportArtifactKind> RequiredCompanions,
    IReadOnlyList<CaseReportArtifactKind> MissingCompanions,
    IReadOnlyList<StaffMailAttachment> FiledEstimates,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Holds,
    string? Stop,
    bool StopPossible,
    IReadOnlyList<ReportDispatchQuestion> Undecided,
    IReadOnlyList<string> AfterSendTasks)
{
    /// <summary>"morning" or "afternoon", for the covering message.</summary>
    public string Greeting { get; init; } = string.Empty;

    /// <summary>
    /// The holds of rules an unanswered question could still make true. The
    /// form offers each with its tick beside the questions, since the same
    /// click answers and sends.
    /// </summary>
    public IReadOnlyList<string> PossibleHolds { get; init; } = [];
}

/// <summary>
/// The one owner of how a report dispatch is planned (Report Sending SOP v8,
/// rule R12): who the report goes to, what must go with it, what holds or
/// stops it, and what staff do after sending. Which mailbox it leaves from is
/// <see cref="ResolveCaseMailSender"/>'s. Pure: it reads the facts and staff's
/// decisions, never a clock, a store or a mailbox.
/// </summary>
public static class ReportDispatchPolicy
{
    public const string GarageFiguresTask = "Send the figures to the garage.";
    public const string RateCardTask = "Check the report's estimate against the Connexus Repair Rate Card.";
    public const string NoFiledEstimateWarning = "No recognised estimate is filed on this Case.";
    public const string NoSeparateFeeNoteWarning =
        "This Principal expects the fee note as a separate document and none is confirmed.";

    /// <summary>"morning" before 12:00 Europe/London, else "afternoon".</summary>
    public static string Greeting(DateTimeOffset nowUtc) =>
        LondonCalendar.LocalAt(nowUtc).Hour < 12 ? "morning" : "afternoon";

    /// <summary>The subject a report leaves with: a reply keeps "Re:" the instruction's subject, a new message is "{REG} Report".</summary>
    public static string Subject(ReportDispatchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Instruction is { } instruction && !string.IsNullOrWhiteSpace(instruction.Subject))
        {
            var subject = instruction.Subject.Trim();
            return subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ? subject : $"Re: {subject}";
        }
        var registration = string.IsNullOrWhiteSpace(facts.Registration)
            ? facts.CaseReference
            : facts.Registration.Trim();
        return $"{registration} Report";
    }

    /// <summary>The value <see cref="ReportDispatchFacts.Fingerprint"/> carries.</summary>
    public static string Fingerprint(ReportDispatchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(facts.Rules);
        var material = string.Join(
            "\u001f",
            facts.Rules.Canonical(),
            facts.Instruction?.RetainedMessageId.ToString("N") ?? "",
            facts.Sender?.Trim().ToUpperInvariant() ?? "",
            facts.ClaimSourceId?.ToString("N") ?? "",
            facts.RepairerId?.ToString("N") ?? "",
            facts.Outcome?.Trim() ?? "",
            facts.InstructionText is null ? "" : Compact(facts.InstructionText));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>
    /// Whether the rule at <paramref name="ruleIndex"/> holds: true, false, or
    /// null when the Case cannot say and staff have not answered. All is false
    /// if any condition is false, else null if any is null; Any is true if any
    /// is true, else null if any is null.
    /// </summary>
    public static bool? Evaluate(
        int ruleIndex, ReportSendingRule rule, ReportDispatchFacts facts, IReadOnlyList<ReportRuleDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(decisions);
        var results = rule.If
            .Select((condition, conditionIndex) => Condition(ruleIndex, conditionIndex, condition, facts, decisions))
            .ToArray();
        if (rule.Match == ReportSendingRuleMatch.All)
        {
            return results.Any(result => result == false)
                ? false
                : results.Any(result => result is null) ? null : true;
        }
        return results.Any(result => result == true)
            ? true
            : results.Any(result => result is null) ? null : false;
    }

    /// <summary>The plan for <paramref name="facts"/>, given staff's answers to the questions it asks.</summary>
    public static ReportDispatchPlan Plan(
        ReportDispatchFacts facts, IReadOnlyList<ReportRuleDecision>? decisions, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(facts.Rules);
        decisions ??= [];
        var rules = facts.Rules;
        var ruleList = rules.Rules;
        var outcomes = ruleList
            .Select((rule, index) => Evaluate(index, rule, facts, decisions))
            .ToArray();
        var applied = Enumerable.Range(0, ruleList.Count).Where(index => outcomes[index] == true).ToArray();

        var undecided = new List<ReportDispatchQuestion>();
        for (var index = 0; index < ruleList.Count; index++)
        {
            if (outcomes[index] is not null)
            {
                continue;
            }
            for (var conditionIndex = 0; conditionIndex < ruleList[index].If.Count; conditionIndex++)
            {
                var condition = ruleList[index].If[conditionIndex];
                if (Condition(index, conditionIndex, condition, facts, decisions) is null)
                {
                    undecided.Add(new(index, conditionIndex, Question(condition, facts)));
                }
            }
        }

        var to = new List<ReportDispatchRecipient>();
        if (rules.SendTo.Count > 0)
        {
            foreach (var address in rules.SendTo)
            {
                Add(to, address, "Principal To");
            }
        }
        else
        {
            Add(to, facts.Sender, "Original sender");
        }

        var cc = new List<ReportDispatchRecipient>();
        if (!rules.SendToOnly)
        {
            if (rules.ReplyAll && facts.Instruction is { } instruction)
            {
                foreach (var address in instruction.Cc)
                {
                    Add(cc, address, "Instruction Cc");
                }
            }
            foreach (var address in rules.Cc)
            {
                Add(cc, address, "Principal Cc");
            }
            foreach (var index in applied)
            {
                foreach (var address in ruleList[index].Then.CcAdd)
                {
                    Add(cc, address, RuleLabel(index, ruleList[index], facts));
                }
            }
        }

        // Removals win. Excluded is every address the rules keep out, so one
        // staff type in later is taken out too; Removed is what left the
        // copies made above.
        var excluded = new List<ReportDispatchRemoval>();
        foreach (var address in rules.NeverCc)
        {
            Exclude(excluded, address, "never cc");
        }
        foreach (var index in applied)
        {
            foreach (var address in ruleList[index].Then.CcRemove)
            {
                Exclude(excluded, address, RuleLabel(index, ruleList[index], facts));
            }
        }
        var removed = excluded
            .Where(removal => cc.Concat(to).Any(recipient => Same(recipient.Address, removal.Address)))
            .ToArray();
        to = [.. to.Where(recipient => !excluded.Any(removal => Same(removal.Address, recipient.Address)))];
        cc =
        [
            .. cc.Where(candidate =>
                !excluded.Any(removal => Same(removal.Address, candidate.Address))
                && !to.Any(recipient => Same(recipient.Address, candidate.Address)))
        ];

        var stop = applied.Select(index => ruleList[index].Then.Stop).FirstOrDefault(text => text is not null);
        var stopPossible = Enumerable.Range(0, ruleList.Count)
            .Any(index => outcomes[index] is null && ruleList[index].Then.Stop is not null);

        var holds = new List<string>();
        if (rules.Hold is { } principalHold)
        {
            holds.Add(principalHold);
        }
        holds.AddRange(applied.Select(index => ruleList[index].Then.Hold).OfType<string>());

        // A rule's Rate card is the reminder to check the report's estimate
        // against the card, after that rule's own reminder.
        var tasks = new List<string>();
        tasks.AddRange(rules.Reminders);
        foreach (var index in applied)
        {
            if (ruleList[index].Then.Remind is { } remind)
            {
                tasks.Add(remind);
            }
            if (ruleList[index].Then.RateCard)
            {
                tasks.Add(RateCardTask);
            }
        }
        if (rules.GarageFigures && string.Equals(facts.Outcome, "repairable", StringComparison.Ordinal))
        {
            tasks.Add(GarageFiguresTask);
        }

        // Each applied rule's attach override replaces the flags it names, in
        // rule order, so a later rule wins. The report itself was generated
        // from the Principal's own Report images.
        var attach = applied
            .Select(index => ruleList[index].Then.Attach)
            .OfType<ReportSendingAttachOverride>()
            .Aggregate(rules.Attach, (current, named) => named.Over(current));

        var required = new List<CaseReportArtifactKind>();
        if (attach.VehicleImagesDocument)
        {
            required.Add(CaseReportArtifactKind.ImagePack);
        }
        if (attach.FigureBreakdown)
        {
            required.Add(CaseReportArtifactKind.RepairSpecification);
        }
        var missing = required.Where(kind => !facts.ConfirmedArtifacts.Contains(kind)).ToArray();

        // The Audatex tick attaches the estimates filed in that format; the
        // Estimate tick attaches the recognised ones in any other.
        StaffMailAttachment[] filed =
        [
            .. facts.FiledEstimates
                .Where(estimate => IsAudatex(estimate) ? attach.Audatex : attach.Estimate)
                .Select(estimate => estimate.Attachment)
        ];

        var warnings = new List<string>();
        if ((attach.Estimate || attach.Audatex) && filed.Length == 0)
        {
            warnings.Add(NoFiledEstimateWarning);
        }
        if (attach.FeeNoteSeparate
            && !facts.ConfirmedArtifacts.Contains(CaseReportArtifactKind.FeeNote))
        {
            warnings.Add(NoSeparateFeeNoteWarning);
        }

        return new(
            to,
            cc,
            removed,
            excluded,
            Subject(facts),
            required,
            missing,
            filed,
            warnings,
            [.. holds.Distinct(StringComparer.Ordinal)],
            stop,
            stopPossible,
            undecided,
            [.. tasks.Distinct(StringComparer.Ordinal)])
        {
            Greeting = Greeting(nowUtc),
            PossibleHolds =
            [
                .. Enumerable.Range(0, ruleList.Count)
                    .Where(index => outcomes[index] is null)
                    .Select(index => ruleList[index].Then.Hold)
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .Except(holds, StringComparer.Ordinal)
            ]
        };
    }

    /// <summary>
    /// What staff submitted, made to follow the plan their answers produced:
    /// a copy that an answered rule added is added to Cc (the form was drawn
    /// before the answer, so it could not show it), and every excluded
    /// address is taken out of To and Cc wherever it came from. The second
    /// value is everything that was removed, for the form to say so.
    /// </summary>
    /// <param name="seeded">The plan the form was drawn from, with no answers.</param>
    public static (ReportRecipientReview Review, IReadOnlyList<ReportDispatchRemoval> Removed) Reconcile(
        ReportDispatchPlan plan, ReportDispatchPlan seeded, ReportRecipientReview review)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(seeded);
        ArgumentNullException.ThrowIfNull(review);
        var cc = (review.Cc ?? []).ToList();
        foreach (var copy in plan.Cc)
        {
            if (!seeded.Cc.Any(shown => Same(shown.Address, copy.Address))
                && !cc.Any(address => Same(address?.Trim() ?? "", copy.Address)))
            {
                cc.Add(copy.Address);
            }
        }

        var removed = plan.Removed.ToList();
        bool IsExcluded(string? address)
        {
            var trimmed = address?.Trim() ?? "";
            var removal = plan.Excluded.FirstOrDefault(candidate => Same(candidate.Address, trimmed));
            if (removal is null)
            {
                return false;
            }
            if (!removed.Any(item => Same(item.Address, removal.Address)))
            {
                removed.Add(removal);
            }
            return true;
        }

        var to = (review.To ?? []).Where(address => !IsExcluded(address)).ToArray();
        var copies = cc.Where(address => !IsExcluded(address)).ToArray();
        return (new(to, copies), removed);
    }

    /// <summary>A Case type value as staff read it, as the Cases list names it: "Inspection and audit".</summary>
    public static string CaseTypeWords(string value) =>
        value == nameof(Cases.CaseType.InspectionAndAudit) ? "Inspection and audit" : value;

    /// <summary>The outcome code as staff read it: "total_loss" is "Total loss".</summary>
    public static string? OutcomeWords(string? outcome)
    {
        if (string.IsNullOrWhiteSpace(outcome))
        {
            return null;
        }
        var code = outcome.Trim();
        if (!code.Contains('_') && code.Length <= 3)
        {
            return code;
        }
        var spaced = code.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    /// <summary>
    /// Whether <paramref name="text"/> mentions any of <paramref name="values"/>,
    /// ignoring case and spaces ("Car Claims" is "CarClaims").
    /// </summary>
    public static bool Mentions(string text, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);
        var haystack = Compact(text);
        return values
            .Select(Compact)
            .Any(needle => needle.Length > 0 && haystack.Contains(needle, StringComparison.Ordinal));
    }

    private static string Compact(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

    private static bool IsAudatex(FiledEstimateAttachment estimate) =>
        string.Equals(estimate.Provider, EstimateFormats.AudatexProvider, StringComparison.OrdinalIgnoreCase);

    private static bool? Condition(
        int ruleIndex,
        int conditionIndex,
        ReportSendingCondition condition,
        ReportDispatchFacts facts,
        IReadOnlyList<ReportRuleDecision> decisions)
    {
        bool? fact = condition.Kind switch
        {
            ReportSendingConditionKind.ClaimSource => facts.ClaimSourceId is { } claimSource
                ? Contains(condition.Values, claimSource.ToString("D"))
                : null,
            ReportSendingConditionKind.Repairer => facts.RepairerId is { } repairer
                ? Contains(condition.Values, repairer.ToString("D"))
                : null,
            ReportSendingConditionKind.Outcome => string.IsNullOrWhiteSpace(facts.Outcome)
                ? null
                : Contains(condition.Values, facts.Outcome.Trim()),
            ReportSendingConditionKind.CaseType => CaseTypeHolds(condition.Values, facts),
            ReportSendingConditionKind.SenderNot => string.IsNullOrWhiteSpace(facts.Sender)
                ? null
                : !Contains(condition.Values, facts.Sender!.Trim()),
            ReportSendingConditionKind.Mentions => string.IsNullOrWhiteSpace(facts.InstructionText)
                ? null
                : Mentions(facts.InstructionText, condition.Values),
            ReportSendingConditionKind.RefPrefix => condition.Values.Any(prefix =>
                facts.CaseReference.StartsWith($"{prefix}.", StringComparison.OrdinalIgnoreCase)),
            ReportSendingConditionKind.PreviousJob => facts.PreviousJob,
            _ => null
        };
        if (fact is not null)
        {
            return fact;
        }
        return decisions
            .Where(decision => decision.RuleIndex == ruleIndex && decision.ConditionIndex == conditionIndex)
            .Select(decision => (bool?)decision.Holds)
            .FirstOrDefault();
    }

    /// <summary>
    /// A "Case type" condition (operator, 13 October 2026): InspectionAndAudit
    /// holds for every report of an Inspection + Audit Case, while Inspection
    /// and Audit hold for the report being sent, which on that Case is the
    /// work it is sent from. Null when the Case's type, or on an
    /// Inspection + Audit Case the report's work, is not known.
    /// </summary>
    private static bool? CaseTypeHolds(IReadOnlyList<string> values, ReportDispatchFacts facts)
    {
        if (facts.CaseType is not { } caseType)
        {
            return null;
        }
        if (Contains(values, caseType.ToString()))
        {
            return true;
        }
        if (caseType != Cases.CaseType.InspectionAndAudit)
        {
            return false;
        }
        return facts.ReportWork is { } work
            ? Contains(
                values,
                CaseWorkPolicy.IsAuditReport(caseType, work)
                    ? nameof(Cases.CaseType.Audit)
                    : nameof(Cases.CaseType.Inspection))
            : null;
    }

    private static bool Contains(IReadOnlyList<string> values, string value) =>
        values.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string Question(ReportSendingCondition condition, ReportDispatchFacts facts)
    {
        var values = condition.Values;
        string Names() => string.Join(" or ", values.Select(value =>
            Guid.TryParse(value, out var id) && facts.ContactNames.TryGetValue(id, out var name)
                ? name
                : "that contact"));
        string Texts() => string.Join(" or ", values);
        return condition.Kind switch
        {
            ReportSendingConditionKind.ClaimSource => $"Is the Claim Source {Names()}?",
            ReportSendingConditionKind.Repairer => $"Is the Repairer {Names()}?",
            ReportSendingConditionKind.Outcome => $"Is the outcome {string.Join(" or ", values.Select(value => OutcomeWords(value)))}?",
            ReportSendingConditionKind.SenderNot => $"Was the instruction sent by someone other than {Texts()}?",
            ReportSendingConditionKind.ImagesFrom => $"Are the images from {Texts()}?",
            ReportSendingConditionKind.BodyshopMentions => $"Do the bodyshop details mention {Texts()}?",
            ReportSendingConditionKind.CaseType => $"Is the Case type {string.Join(" or ", values.Select(CaseTypeWords))}?",
            _ => $"Does the instruction mention {string.Join(" or ", values.Select(value => $"\"{value}\""))}?"
        };
    }

    private static string RuleLabel(int index, ReportSendingRule rule, ReportDispatchFacts facts)
    {
        var parts = rule.If.Select(condition => condition.Kind switch
        {
            ReportSendingConditionKind.ClaimSource => $"Claim Source {facts.ClaimSourceName}".Trim(),
            ReportSendingConditionKind.Repairer => $"Repairer {facts.RepairerName}".Trim(),
            ReportSendingConditionKind.Outcome => $"Outcome {string.Join(" or ", condition.Values.Select(value => OutcomeWords(value)))}",
            ReportSendingConditionKind.SenderNot => $"Sender not {string.Join(" or ", condition.Values)}",
            ReportSendingConditionKind.ImagesFrom => $"Images from {string.Join(" or ", condition.Values)}",
            ReportSendingConditionKind.BodyshopMentions => $"Bodyshop mentions {string.Join(" or ", condition.Values)}",
            ReportSendingConditionKind.CaseType => $"Case type {string.Join(" or ", condition.Values.Select(CaseTypeWords))}",
            ReportSendingConditionKind.RefPrefix => $"Reference prefix {string.Join(" or ", condition.Values)}",
            ReportSendingConditionKind.PreviousJob => "Previous job",
            _ => $"Mentions {string.Join(" or ", condition.Values)}"
        });
        return $"Rule {index + 1}: {string.Join(", ", parts)}";
    }

    private static string? Clean(string? address) =>
        !string.IsNullOrWhiteSpace(address) && MailAddress.TryCreate(address.Trim(), out _)
            ? address.Trim()
            : null;

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void Add(List<ReportDispatchRecipient> list, string? address, string source)
    {
        if (Clean(address) is { } clean && !list.Any(item => Same(item.Address, clean)))
        {
            list.Add(new(clean, source));
        }
    }

    private static void Exclude(List<ReportDispatchRemoval> list, string address, string reason)
    {
        var trimmed = address.Trim();
        if (trimmed.Length > 0 && !list.Any(item => Same(item.Address, trimmed)))
        {
            list.Add(new(trimmed, reason));
        }
    }
}
