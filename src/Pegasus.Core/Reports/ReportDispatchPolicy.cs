using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Reports;

/// <summary>
/// The Case's retained instruction e-mail as the report send needs it: the
/// message a report replies to, the approved mailbox that holds it, and who
/// sent and copied it. <paramref name="SenderAddress"/> is its transport
/// sender; for a Collision Engineers staff forward that is the forwarding
/// mailbox, and for one held in instructions@ <paramref name="Cc"/> is the
/// original message's own Cc list less that mailbox (operator, 3 November
/// 2026; R651, R652, R653).
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
    string? Subject,
    string? SenderAddress);

/// <summary>
/// One of the Case's recognised filed estimate documents as an attachment,
/// with the format it was read in (<see cref="Provider"/>, such as "Audatex"),
/// named by its occurrence on the Case, which the Send report dialog opens
/// and renames it by (R1176).
/// </summary>
public sealed record FiledEstimateAttachment(StaffMailAttachment Attachment, string? Provider, Guid OccurrenceId = default);

/// <summary>
/// One of the Case's files a Principal's Offer ticks may offer with the report
/// (operator, 17 November 2026; R1151): a confirmed, current, kept External
/// estimate or Repair invoice that Pegasus did not generate, named by its
/// occurrence on the Case, with its type and the attachment it sends as.
/// </summary>
public sealed record OfferedCaseFile(Guid OccurrenceId, DocumentSemanticRole Type, StaffMailAttachment Attachment);

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

    /// <summary>The Case's External estimates and Repair invoices the Principal's Offer ticks may offer (R1151).</summary>
    public IReadOnlyList<OfferedCaseFile> OfferableFiles { get; init; } = [];

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
    /// instruction text, and then no such condition holds. It is read for the
    /// plan and never stored with a send.
    /// </summary>
    public string? InstructionText { get; init; }

    /// <summary>The Case's type, which "Case type" conditions compare.</summary>
    public CaseType? CaseType { get; init; }

    /// <summary>What the Principal's reports are called (R740); not part of <see cref="Fingerprint"/>, since no rule reads it.</summary>
    public PrincipalReportNames ReportNames { get; init; } = PrincipalReportNames.None;

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

    /// <summary>
    /// One value for everything the rules read. A send carries the value its
    /// form was drawn from, so it never follows rules or Case facts staff did
    /// not see.
    /// </summary>
    public string Fingerprint => ReportDispatchPolicy.Fingerprint(this);
}

public sealed record ReportDispatchRecipient(string Address, string Source);

public sealed record ReportDispatchRemoval(string Address, string Reason);

/// <summary>
/// What a report send records with its operation: the holds staff ticked as
/// done, a Stop they overrode and why, and what they still owe after sending
/// (which becomes Case tasks when Report sent is recorded).
/// </summary>
public sealed record ReportDispatchRecord(
    IReadOnlyList<string> Holds,
    string? StopOverridden,
    string? StopOverrideReason,
    IReadOnlyList<string> AfterSendTasks);

/// <summary>
/// The one plan a report send follows. <see cref="Holds"/> are ticked off
/// before Send; <see cref="Stop"/> refuses the send unless casework staff give
/// a reason. <see cref="Excluded"/> is every address the rules keep out,
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
    IReadOnlyList<FiledEstimateAttachment> FiledEstimates,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Holds,
    string? Stop,
    IReadOnlyList<string> AfterSendTasks)
{
    /// <summary>"morning" or "afternoon", for the covering message.</summary>
    public string Greeting { get; init; } = string.Empty;

    /// <summary>
    /// The Case's files the Send report dialog offers unticked, in the
    /// Case's file order: those of a type the Principal's Offer ticks name,
    /// less any <see cref="FiledEstimates"/> already attaches (R1151, R1158).
    /// </summary>
    public IReadOnlyList<OfferedCaseFile> OfferedFiles { get; init; } = [];
}

/// <summary>
/// The one owner of how a report dispatch is planned (Report Sending SOP v8,
/// rule R12): who the report goes to, what must go with it, what holds or
/// stops it, and what staff do after sending. Which mailbox it leaves from is
/// <see cref="ResolveCaseMailSender"/>'s. Pure: it reads the facts, never a
/// clock, a store or a mailbox. It asks staff nothing: a rule reads what the
/// Case holds, and a fact the Case has not set means its condition does not
/// hold (operator, 12 November 2026; R1023, R1024).
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

    /// <summary>
    /// The subject a report leaves with: an answer to the instruction keeps
    /// "Re:" its subject, without a staff forward's "FW:"
    /// (<see cref="StaffMailReplySubject"/>); a new message is "{REG} Report".
    /// </summary>
    public static string Subject(ReportDispatchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (facts.Instruction is { } instruction && !string.IsNullOrWhiteSpace(instruction.Subject))
        {
            return StaffMailReplySubject.For(instruction.Subject, instruction.SenderAddress, instruction.EffectiveSender);
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
            facts.InstructionText is null ? "" : NamedInText.Compact(facts.InstructionText));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>
    /// Whether <paramref name="rule"/> holds for the Case: with All, every
    /// condition holds; with Any, at least one does. A condition whose fact
    /// the Case has not set does not hold.
    /// </summary>
    public static bool Evaluate(ReportSendingRule rule, ReportDispatchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(facts);
        return rule.Match == ReportSendingRuleMatch.All
            ? rule.If.All(condition => Condition(condition, facts))
            : rule.If.Any(condition => Condition(condition, facts));
    }

    /// <summary>The plan for <paramref name="facts"/>.</summary>
    public static ReportDispatchPlan Plan(ReportDispatchFacts facts, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(facts.Rules);
        var rules = facts.Rules;
        var ruleList = rules.Rules;
        var applied = Enumerable.Range(0, ruleList.Count)
            .Where(index => Evaluate(ruleList[index], facts))
            .ToArray();

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
        FiledEstimateAttachment[] filed =
        [
            .. facts.FiledEstimates
                .Where(estimate => IsAudatex(estimate) ? attach.Audatex : attach.Estimate)
        ];

        // The Offer ticks list the Case's External estimates and Repair
        // invoices for staff to tick (R1151); a file the Estimate or Audatex
        // tick already attaches is not offered a second time (R1158).
        OfferedCaseFile[] offered =
        [
            .. facts.OfferableFiles
                .Where(file => file.Type switch
                {
                    DocumentSemanticRole.ExternalEstimate => attach.OfferExternalEstimates,
                    DocumentSemanticRole.RepairInvoice => attach.OfferRepairInvoices,
                    _ => false
                })
                .Where(file => !filed.Any(estimate => estimate.Attachment.VersionId == file.Attachment.VersionId))
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
            [.. tasks.Distinct(StringComparer.Ordinal)])
        {
            Greeting = Greeting(nowUtc),
            OfferedFiles = offered
        };
    }

    /// <summary>
    /// What staff submitted, made to follow the plan: every excluded address
    /// is taken out of To and Cc wherever it came from. The second value is
    /// everything that was removed, for the form to say so.
    /// </summary>
    public static (ReportRecipientReview Review, IReadOnlyList<ReportDispatchRemoval> Removed) Reconcile(
        ReportDispatchPlan plan, ReportRecipientReview review)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(review);

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
        var copies = (review.Cc ?? []).Where(address => !IsExcluded(address)).ToArray();
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
    /// ignoring case and spaces ("Car Claims" is "CarClaims"), by the one rule
    /// a new Case's contact fill also reads (<see cref="NamedInText"/>).
    /// </summary>
    public static bool Mentions(string text, IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(values);
        var haystack = NamedInText.Compact(text);
        return values.Any(value => NamedInText.Names(haystack, value));
    }

    private static bool IsAudatex(FiledEstimateAttachment estimate) =>
        string.Equals(estimate.Provider, EstimateFormats.AudatexProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether one condition holds. It reads only what the Case holds, so a
    /// Case with no Claim Source, Repairer, outcome, type, sender or
    /// instruction text meets no condition on that fact (R1024).
    /// </summary>
    private static bool Condition(ReportSendingCondition condition, ReportDispatchFacts facts) =>
        condition.Kind switch
        {
            ReportSendingConditionKind.ClaimSource => facts.ClaimSourceId is { } claimSource
                && Contains(condition.Values, claimSource.ToString("D")),
            ReportSendingConditionKind.Repairer => facts.RepairerId is { } repairer
                && Contains(condition.Values, repairer.ToString("D")),
            ReportSendingConditionKind.Outcome => !string.IsNullOrWhiteSpace(facts.Outcome)
                && Contains(condition.Values, facts.Outcome.Trim()),
            ReportSendingConditionKind.CaseType => CaseTypeHolds(condition.Values, facts),
            ReportSendingConditionKind.SenderNot => !string.IsNullOrWhiteSpace(facts.Sender)
                && !Contains(condition.Values, facts.Sender!.Trim()),
            ReportSendingConditionKind.Mentions => !string.IsNullOrWhiteSpace(facts.InstructionText)
                && Mentions(facts.InstructionText, condition.Values),
            ReportSendingConditionKind.RefPrefix => condition.Values.Any(prefix =>
                facts.CaseReference.StartsWith($"{prefix}.", StringComparison.OrdinalIgnoreCase)),
            ReportSendingConditionKind.PreviousJob => facts.PreviousJob,
            _ => false
        };

    /// <summary>
    /// A "Case type" condition (operator, 13 October 2026): InspectionAndAudit
    /// holds for every report of an Inspection + Audit Case, while Inspection
    /// and Audit hold for the report being sent, which on that Case is the
    /// work it is sent from. It does not hold when the Case's type, or on an
    /// Inspection + Audit Case the report's work, is not known.
    /// </summary>
    private static bool CaseTypeHolds(IReadOnlyList<string> values, ReportDispatchFacts facts)
    {
        if (facts.CaseType is not { } caseType)
        {
            return false;
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
            && Contains(
                values,
                CaseWorkPolicy.IsAuditReport(caseType, work)
                    ? nameof(Cases.CaseType.Audit)
                    : nameof(Cases.CaseType.Inspection));
    }

    private static bool Contains(IReadOnlyList<string> values, string value) =>
        values.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static string RuleLabel(int index, ReportSendingRule rule, ReportDispatchFacts facts)
    {
        var parts = rule.If.Select(condition => condition.Kind switch
        {
            ReportSendingConditionKind.ClaimSource => $"Claim Source {facts.ClaimSourceName}".Trim(),
            ReportSendingConditionKind.Repairer => $"Repairer {facts.RepairerName}".Trim(),
            ReportSendingConditionKind.Outcome => $"Outcome {string.Join(" or ", condition.Values.Select(value => OutcomeWords(value)))}",
            ReportSendingConditionKind.SenderNot => $"Sender not {string.Join(" or ", condition.Values)}",
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
