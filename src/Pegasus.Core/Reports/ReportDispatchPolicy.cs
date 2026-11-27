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

    /// <summary>
    /// The names of the contacts the Case chose and the rules name, by id,
    /// for the rule labels; a contact not here is labelled by its id.
    /// </summary>
    public IReadOnlyDictionary<Guid, string> ContactNames { get; init; } = new Dictionary<Guid, string>();

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

    /// <summary>The files the actions that applied attach or offer, after the last one that named each.</summary>
    public ReportSendingAttachments Attach { get; init; } = ReportSendingAttachments.None;
}

/// <summary>
/// The one owner of how a report dispatch is planned (Report Sending SOP v8,
/// rule R12): who the report goes to, what must go with it, what holds or
/// stops it, and what staff do after sending. Which mailbox it leaves from is
/// <see cref="ResolveCaseMailSender"/>'s. Pure: it reads the facts, never a
/// clock, a store or a mailbox. It asks staff nothing: a rule reads what the
/// Case holds, and a fact the Case has not set does not meet an "is"
/// condition (operator, 12 November 2026; R1023, R1024) and does meet an "is
/// not" one (operator, 25 November 2026; R1269). Nothing happens on a send
/// that no action says: the every-Case actions apply, then each applying
/// rule's, in order.
/// </summary>
public static class ReportDispatchPolicy
{
    public const string GarageFiguresTask = "Send the figures to the garage.";
    public const string RateCardTask = "Check the report's estimate against the Connexus Repair Rate Card.";
    public const string NoFiledEstimateWarning = "No recognised estimate is filed on this Case.";
    public const string NoSeparateFeeNoteWarning =
        "This Principal expects the fee note as a separate document and none is confirmed.";

    /// <summary>The source of a recipient an every-Case action adds.</summary>
    public const string EveryCaseSource = "On every Case";

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
            facts.Outcome?.Trim() ?? "");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>
    /// Whether <paramref name="rule"/> applies to the Case: some group of its
    /// conditions has every condition met.
    /// </summary>
    public static bool Evaluate(ReportSendingRule rule, ReportDispatchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(facts);
        return rule.If.Any(group => group.All(condition => Holds(condition, facts)));
    }

    /// <summary>
    /// Whether one condition is met: "is" when the Case holds the fact and it
    /// is among the values, "is not" otherwise, an unset fact included (R1269).
    /// </summary>
    public static bool Holds(ReportSendingCondition condition, ReportDispatchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(facts);
        return Is(condition, facts) != condition.Not;
    }

    /// <summary>The plan for <paramref name="facts"/>.</summary>
    public static ReportDispatchPlan Plan(ReportDispatchFacts facts, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(facts.Rules);
        var rules = facts.Rules;

        // Nothing is decided until an action says so: the every-Case actions
        // first, then each applying rule's, in rule order.
        var applied = new List<(IReadOnlyList<ReportSendingAction> Actions, Func<string> Source)>
        {
            (rules.EveryCase, () => EveryCaseSource)
        };
        for (var index = 0; index < rules.Rules.Count; index++)
        {
            var rule = rules.Rules[index];
            if (Evaluate(rule, facts))
            {
                var ruleIndex = index;
                applied.Add((rule.Then, () => RuleLabel(ruleIndex, rule, facts)));
            }
        }

        List<ReportDispatchRecipient>? sendTo = null;
        var sendToOnly = false;
        var replyAll = false;
        var ccAdded = new List<ReportDispatchRecipient>();
        var excluded = new List<ReportDispatchRemoval>();
        var attach = ReportSendingAttachments.None;
        var holds = new List<string>();
        string? stop = null;
        var tasks = new List<string>();
        foreach (var (actions, source) in applied)
        {
            foreach (var action in actions)
            {
                switch (action.Kind)
                {
                    case ReportSendingActionKind.SendTo:
                        // A later Send to replaces the To list.
                        sendTo = [];
                        foreach (var address in action.Addresses ?? [])
                        {
                            Add(sendTo, address, source());
                        }
                        break;
                    case ReportSendingActionKind.SendToOnly:
                        sendToOnly = true;
                        break;
                    case ReportSendingActionKind.ReplyAll:
                        replyAll = true;
                        break;
                    case ReportSendingActionKind.Cc:
                        foreach (var address in action.Addresses ?? [])
                        {
                            Add(ccAdded, address, source());
                        }
                        break;
                    case ReportSendingActionKind.NeverCc:
                        foreach (var address in action.Addresses ?? [])
                        {
                            Exclude(excluded, address, ReferenceEquals(actions, rules.EveryCase) ? "never cc" : source());
                        }
                        break;
                    case ReportSendingActionKind.Attach when action.File is { } file:
                        attach = attach.With(file, true);
                        break;
                    case ReportSendingActionKind.DoNotAttach when action.File is { } file:
                        attach = attach.With(file, false);
                        break;
                    case ReportSendingActionKind.Hold when action.Text is { } text:
                        holds.Add(text);
                        break;
                    case ReportSendingActionKind.Stop when action.Text is { } text:
                        stop ??= text;
                        break;
                    case ReportSendingActionKind.Task when action.Text is { } text:
                        tasks.Add(text);
                        break;
                }
            }
        }

        var to = new List<ReportDispatchRecipient>();
        if (sendTo is { Count: > 0 })
        {
            to.AddRange(sendTo);
        }
        else
        {
            Add(to, facts.Sender, "Original sender");
        }

        var cc = new List<ReportDispatchRecipient>();
        if (!sendToOnly)
        {
            if (replyAll && facts.Instruction is { } instruction)
            {
                foreach (var address in instruction.Cc)
                {
                    Add(cc, address, "Instruction Cc");
                }
            }
            foreach (var recipient in ccAdded)
            {
                Add(cc, recipient.Address, recipient.Source);
            }
        }

        // Removals win. Excluded is every address the rules keep out, so one
        // staff type in later is taken out too; Removed is what left the
        // copies made above.
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

        // The Audatex file attaches the estimates filed in that format; the
        // Estimate file attaches the recognised ones in any other.
        FiledEstimateAttachment[] filed =
        [
            .. facts.FiledEstimates
                .Where(estimate => IsAudatex(estimate) ? attach.Audatex : attach.Estimate)
        ];

        // The Offer files list the Case's External estimates and Repair
        // invoices for staff to tick (R1151); a file the Estimate or Audatex
        // file already attaches is not offered a second time (R1158).
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
            OfferedFiles = offered,
            Attach = attach
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

    private static bool IsAudatex(FiledEstimateAttachment estimate) =>
        string.Equals(estimate.Provider, EstimateFormats.AudatexProvider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the Case's fact is among the condition's values. It reads only
    /// what the Case holds, so a Case with no Claim Source, Repairer, outcome,
    /// type or sender is not among any (R1024).
    /// </summary>
    private static bool Is(ReportSendingCondition condition, ReportDispatchFacts facts) =>
        condition.Fact switch
        {
            ReportSendingFact.ClaimSource => facts.ClaimSourceId is { } claimSource
                && Contains(condition.Values, claimSource.ToString("D")),
            ReportSendingFact.Repairer => facts.RepairerId is { } repairer
                && Contains(condition.Values, repairer.ToString("D")),
            ReportSendingFact.Outcome => !string.IsNullOrWhiteSpace(facts.Outcome)
                && Contains(condition.Values, facts.Outcome.Trim()),
            ReportSendingFact.CaseType => CaseTypeHolds(condition.Values, facts),
            ReportSendingFact.Sender => !string.IsNullOrWhiteSpace(facts.Sender)
                && Contains(condition.Values, facts.Sender!.Trim()),
            ReportSendingFact.RefPrefix => condition.Values.Any(prefix =>
                facts.CaseReference.StartsWith($"{prefix}.", StringComparison.OrdinalIgnoreCase)),
            ReportSendingFact.PreviousJob => facts.PreviousJob,
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

    /// <summary>
    /// "Rule {n}: {its conditions}", a group's conditions joined by ", " and
    /// the groups by " or "; a Claim Source or Repairer it is met by is the
    /// Case's own, and an "is not" condition reads "{fact} not {values}".
    /// </summary>
    private static string RuleLabel(int index, ReportSendingRule rule, ReportDispatchFacts facts)
    {
        string Contacts(IReadOnlyList<string> values) => string.Join(" or ", values.Select(value =>
            Guid.TryParse(value, out var id) && facts.ContactNames.TryGetValue(id, out var name) ? name : value));
        string Words(ReportSendingCondition condition)
        {
            var not = condition.Not ? " not" : "";
            return condition.Fact switch
            {
                ReportSendingFact.ClaimSource => condition.Not
                    ? $"Claim Source not {Contacts(condition.Values)}"
                    : $"Claim Source {facts.ClaimSourceName}".Trim(),
                ReportSendingFact.Repairer => condition.Not
                    ? $"Repairer not {Contacts(condition.Values)}"
                    : $"Repairer {facts.RepairerName}".Trim(),
                ReportSendingFact.Outcome => $"Outcome{not} {string.Join(" or ", condition.Values.Select(value => OutcomeWords(value)))}",
                ReportSendingFact.Sender => $"Sender{not} {string.Join(" or ", condition.Values)}",
                ReportSendingFact.CaseType => $"Case type{not} {string.Join(" or ", condition.Values.Select(CaseTypeWords))}",
                ReportSendingFact.RefPrefix => $"Reference prefix{not} {string.Join(" or ", condition.Values)}",
                _ => condition.Not ? "No previous job" : "Previous job"
            };
        }
        var groups = rule.If.Select(group => string.Join(", ", group.Select(Words)));
        return $"Rule {index + 1}: {string.Join(" or ", groups)}";
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
