using System.Globalization;
using System.Text;
using Pegasus.Core;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The single place a persisted code becomes words an operator reads.
/// </summary>
/// <remarks>
/// Raw <c>enum.ToString()</c>, snake_case event codes and PascalCase compounds
/// never reach markup: "NotReady", "ReportSent", "case_created" and
/// "InspectionAndAudit" are all things the codebase calls itself, not things
/// the business calls anything.
///
/// Two of these maps are settled business vocabulary and must not drift:
/// <see cref="CaseStage"/> carries the case lifecycle stage names, and the
/// distinct meanings of Audit, Triage and Unidentified are reserved.
/// Everything else falls through to <see cref="Humanise"/>, which turns an
/// unknown code into a readable sentence rather than printing it verbatim —
/// event codes in particular are composed at several call sites, so a fixed map
/// would silently go stale.
/// </remarks>
public static partial class OperatorLabels
{
    public const string MarkAsOriginalReport = "Mark as original report";

    /// <summary>
    /// The Case requirement an Audit lists first until its original report is
    /// filed (FRD-01): the Case page's aside and the Cases quick detail name it.
    /// </summary>
    public const string OriginalReportMissing = "Original report missing";

    /// <summary>
    /// The known principal on a pre-case record — an Image Intake or a Triage.
    /// One pair owns the concept for both surfaces: `Not known` is the absent
    /// principal and is never reused as a generic empty label elsewhere.
    /// </summary>
    public const string Principal = "Principal";

    public const string PrincipalNotKnown = "Not known";

    /// <summary>
    /// The Triage's own permanent reference, distinct from the originating
    /// principal claim number.
    /// </summary>
    public const string TriageReference = "Triage reference";

    public static string UnidentifiedReason(UnidentifiedReasonCode reason) => reason switch
    {
        UnidentifiedReasonCode.UnreadableOrCorruptContent => "Unreadable or corrupt content",
        UnidentifiedReasonCode.UnsupportedContent => "Unsupported content",
        UnidentifiedReasonCode.NoUsableIdentification => "No usable identification",
        UnidentifiedReasonCode.ConflictingIdentification => "Conflicting identification",
        UnidentifiedReasonCode.TechnicalProcessingFailure => "Technical processing failure",
        _ => Humanise(reason.ToString())
    };

    /// <summary>
    /// The reason label for an Unidentified reason code however the projection
    /// carried it — the Work Centre's needs-attention rows hold Core enum
    /// names as strings. Same parse-then-delegate shape as
    /// <see cref="CaseStage(string?)"/>.
    /// </summary>
    public static string UnidentifiedReason(string? reason) =>
        Enum.TryParse<UnidentifiedReasonCode>(reason, ignoreCase: true, out var parsed)
            ? UnidentifiedReason(parsed)
            : Humanise(reason);

    public static string UnidentifiedState(UnidentifiedState state) => state switch
    {
        Pegasus.Core.Intake.Unidentified.UnidentifiedState.Open => "Unidentified",
        Pegasus.Core.Intake.Unidentified.UnidentifiedState.Resolved => "Resolved Unidentified",
        _ => Humanise(state.ToString())
    };

    /// <summary>
    /// What an Unidentified item's retained material is, for the Material
    /// column, the row/detail "what is going on" text and the Automation list
    /// filter (<c>UnidentifiedMcpTools.ListAsync</c>). Supersedes the old
    /// origin-kind label ("Intake receipt"), which named the internal record
    /// rather than the material and used the banned word "intake".
    /// </summary>
    public static string UnidentifiedMediaKind(Pegasus.Core.Intake.Unidentified.UnidentifiedMediaKind kind) => kind switch
    {
        Pegasus.Core.Intake.Unidentified.UnidentifiedMediaKind.Image => "Image",
        Pegasus.Core.Intake.Unidentified.UnidentifiedMediaKind.Email => "E-mail",
        Pegasus.Core.Intake.Unidentified.UnidentifiedMediaKind.Document => "Document",
        _ => Humanise(kind.ToString())
    };

    /// <summary>
    /// The media-kind label for a persisted kind string, however the
    /// projection carried it — the Work Centre's needs-attention rows hold
    /// Core enum names as strings. Same parse-then-delegate shape as
    /// <see cref="CaseStage(string?)"/>; keeps the "E-mail" wording on the
    /// one list rather than letting a bare <see cref="Humanise"/> spell it
    /// "Email".
    /// </summary>
    public static string UnidentifiedMediaKind(string? kind) =>
        Enum.TryParse<Pegasus.Core.Intake.Unidentified.UnidentifiedMediaKind>(kind, ignoreCase: true, out var parsed)
            ? UnidentifiedMediaKind(parsed)
            : Humanise(kind);

    /// <summary>
    /// The operator-meaningful handle for a received e-mail: its subject and
    /// sender, or "(No subject)" when the subject could not be read. The one
    /// formatting rule for both the Cases queue's Unidentified rows
    /// (<c>Cases.IndexModel</c>) and the Unidentified detail page
    /// (<c>Unidentified.DetailsModel.Handle</c>), which read the same
    /// subject/sender from two different shapes.
    /// </summary>
    public static string EmailHandle(string? subject, string? sender) => (subject, sender) switch
    {
        ({ } presentSubject, { } presentSender) => $"{presentSubject} — from {presentSender}",
        ({ } presentSubject, null) => presentSubject,
        (null, { } presentSender) => $"(No subject) — from {presentSender}",
        _ => "(No subject)"
    };

    /// <summary>
    /// The confirmation surface's association report, worded by provenance:
    /// a staff decision is never described as automation's doing, and what
    /// it says happened automatically really did.
    /// </summary>
    public static string AssociatedWithCase(string? caseReference, bool byStaffDecision) =>
        (byStaffDecision, caseReference) switch
        {
            (true, null) => "This was added to a case.",
            (true, { } staffLinked) => $"This was added to case {staffLinked}.",
            (false, null) => "This was automatically associated with a case.",
            (false, { } matched) => $"This was automatically associated with case {matched}."
        };

    /// <summary>
    /// The one-press Attach form on the Cases page has no suggestions list, and
    /// a second press of an attach that already worked also reaches here, so the
    /// notice names both possibilities.
    /// </summary>
    public const string AttachOnePressNotFound =
        "No single Case matched that reference, or this image was already added. Check the reference and the Cases list.";

    /// <summary>
    /// What the Search preview's accident circumstances say when the read
    /// that fetches them for a selected row fails (operator, 16 October 2026);
    /// a failed read never reads "Not recorded".
    /// </summary>
    public const string CircumstancesUnavailable = "Circumstances unavailable.";

    /// <summary>
    /// The case lifecycle stage as the operator reads it (operator decision D3): a
    /// display mapping only. <see cref="CaseLifecycleState.ReportPreparation"/>
    /// reads "With Engineer" and <see cref="CaseLifecycleState.ReportSent"/>
    /// reads "Report sent". The Core enum is untouched. A Case with an open
    /// query reads "Query" through <see cref="CaseStage(CaseLifecycleState, bool)"/>.
    /// </summary>
    public static string CaseStage(CaseLifecycleState state) => state switch
    {
        CaseLifecycleState.NotReady => "Not ready",
        CaseLifecycleState.Held => "Held",
        CaseLifecycleState.Review => "Review",
        CaseLifecycleState.ReportPreparation => "With Engineer",
        CaseLifecycleState.ReportSent => "Report sent",
        _ when CaseLifecycleRules.IsTerminal(state) => ClosedStage,
        _ => Humanise(state.ToString())
    };

    /// <summary>The stage word of an open Case that holds an open query (operator, 11 October 2026).</summary>
    public const string QueryStage = "Query";

    /// <summary>The stage word of a Case in any closed disposition (operator, 15 October 2026).</summary>
    public const string ClosedStage = "Closed";

    /// <summary>
    /// The stage as every screen, queue and filter reads it: Query while the
    /// Case holds an open query, otherwise the state's own word.
    /// </summary>
    public static string CaseStage(CaseLifecycleState state, bool hasOpenQuery) =>
        CaseQueryRules.IsQueryStage(state, hasOpenQuery) ? QueryStage : CaseStage(state);

    /// <summary>
    /// One choice of the Case stage filter: the states it finds and, where the
    /// stage is decided by the open-query mark rather than the state alone,
    /// which mark.
    /// </summary>
    public sealed record CaseStageOption(
        string Key,
        string Label,
        IReadOnlyList<CaseLifecycleState> States,
        bool? HasOpenQuery = null);

    /// <summary>
    /// The stages a Case can be filtered by, in workflow order: one per stage
    /// word. Report sent is a Case with no open query; Query is any open Case
    /// with one; Closed is every closed disposition.
    /// </summary>
    public static IReadOnlyList<CaseStageOption> CaseStageOptions { get; } =
    [
        new(nameof(CaseLifecycleState.NotReady), CaseStage(CaseLifecycleState.NotReady), [CaseLifecycleState.NotReady]),
        new(nameof(CaseLifecycleState.Review), CaseStage(CaseLifecycleState.Review), [CaseLifecycleState.Review]),
        new(nameof(CaseLifecycleState.ReportPreparation), CaseStage(CaseLifecycleState.ReportPreparation), [CaseLifecycleState.ReportPreparation]),
        new(nameof(CaseLifecycleState.ReportSent), CaseStage(CaseLifecycleState.ReportSent), [CaseLifecycleState.ReportSent], HasOpenQuery: false),
        new(
            QueryStage,
            QueryStage,
            Enum.GetValues<CaseLifecycleState>().Where(state => !CaseLifecycleRules.IsTerminal(state)).ToArray(),
            HasOpenQuery: true),
        new(nameof(CaseLifecycleState.Held), CaseStage(CaseLifecycleState.Held), [CaseLifecycleState.Held]),
        new(ClosedStage, ClosedStage, Enum.GetValues<CaseLifecycleState>().Where(CaseLifecycleRules.IsTerminal).ToArray())
    ];

    /// <summary>The stage option a filter value names, or null for none or an unknown key.</summary>
    public static CaseStageOption? FindCaseStageOption(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : CaseStageOptions.FirstOrDefault(option => string.Equals(option.Key, key.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// One named closure outcome, as the operator chooses it in the Close
    /// dialog: FRD-13's disposition names (operator, 16 October 2026). Every
    /// disposition reads <see cref="ClosedStage"/> as the Case's stage.
    /// </summary>
    public static string CaseClosure(CaseClosureOutcome outcome) => outcome switch
    {
        CaseClosureOutcome.PrincipalCancelled => "Principal cancelled",
        CaseClosureOutcome.CollisionEngineersRejected => "Collision Engineers rejected",
        CaseClosureOutcome.CreatedInError => "Created in error",
        CaseClosureOutcome.SourceEmailUnlinked => "Source email unlinked",
        _ => Humanise(outcome.ToString())
    };

    /// <summary>The Triage record's own lifecycle words (moved here from the Cases page).</summary>
    public static string TriageState(Pegasus.Core.Triage.TriageState state) => state switch
    {
        Pegasus.Core.Triage.TriageState.Open => "Open",
        Pegasus.Core.Triage.TriageState.AwaitingInformation => "Awaiting information",
        Pegasus.Core.Triage.TriageState.FindingRecorded => "Finding recorded",
        Pegasus.Core.Triage.TriageState.Completed => "Completed",
        Pegasus.Core.Triage.TriageState.Cancelled => "Cancelled",
        _ => throw new InvalidOperationException($"Unknown triage state '{(int)state}'.")
    };

    /// <summary>
    /// The Triage Case page's own words: each action's notice, the one Assign
    /// control and the reply the correspondence panel offers (26 September 2026).
    /// </summary>
    public static class Triage
    {
        public const string FindingRecorded = "Finding recorded.";
        public const string Completed = "Triage completed.";
        public static string AssignedTo(string name) => $"Assigned to {name}.";
        public const string Unassigned = "Unassigned.";
        public const string Cancelled = "Triage cancelled.";
        public const string Reopened = "Triage reopened.";
        public const string CaseLinked = "Case linked.";
        public const string CaseUnlinked = "Case unlinked.";
        public const string NoteAdded = "Note added.";
        public const string ResponseLinked = "Response evidence linked.";
        public const string ResponseUnlinked = "Response evidence unlinked.";
        public const string Changed = "This Triage record changed while you were working. Reload and try again.";
        public const string Expired = "Editing expired before this change was saved. Reload and try again.";
        public const string ChooseAssignee = "Choose who to assign.";

        public const string Assignee = "Assignee";
        public const string Assign = "Assign";
        public const string Reassign = "Reassign";
        public const string Unassign = "Unassign";
        public const string NoAssignee = "Unassigned";
        public static string You(string name) => $"{name} (you)";
        public const string Case = "Case";
        public const string NoCase = "None";
        public const string OpenCase = "Open the case";
        public const string Cancel = "Cancel";

        public const string RecordFinding = "Record finding";
        public const string Finding = "Finding";
        public const string RecordCorrection = "Record correction";
        public const string CompleteTriage = "Complete Triage";
        public const string CancelTriage = "Cancel Triage";
        public const string OpenFile = "Open file";
        public const string VehicleImages = "Vehicle images";
        public const string Notes = "Notes";
        public const string Reopen = "Reopen";

        public const string ReplyWithFinding = "Reply with finding";
        public const string SendChaser = "Send chaser";
        public const string SendReply = "Send reply";
        public const string NoReplyWhenCancelled = "A cancelled Triage has no reply to send.";
        public const string ReplyNeedsEmail = "A reply can only be sent for a Triage that came by e-mail.";

        public static string Sent(Pegasus.Core.Operations.StaffMailPurpose purpose) =>
            purpose == Pegasus.Core.Operations.StaffMailPurpose.TriageOutcomeReply ? "Reply sent." : "Chaser sent.";

        public static string SendStatus(
            Pegasus.Core.Operations.StaffMailPurpose purpose,
            Pegasus.Core.Operations.StaffMailState state) =>
            $"{(purpose == Pegasus.Core.Operations.StaffMailPurpose.TriageOutcomeReply ? "Reply" : "Chaser")} status: {StaffMail.State(state)}.";

        public static string LatestSendStatus(Pegasus.Core.Operations.StaffMailState state) =>
            $"Latest send: {StaffMail.State(state)}";
    }

    /// <summary>
    /// Administration › E-mail templates: the area, its list and its dialog
    /// (26 September 2026).
    /// </summary>
    public static class EmailTemplates
    {
        public const string Area = "E-mail templates";
        public const string ActionLogArea = "E-mail template";
        public const string Template = "Template";
        public const string LastChanged = "Last changed";
        public const string BuiltInWording = "Built-in wording";
        public const string Actions = "Actions";
        public const string Edit = "Edit";
        public const string Body = "Body";
        public const string Placeholders = "Placeholders";
        public const string Cancel = "Cancel";
        public const string Save = "Save";
        public const string Saved = "E-mail template saved.";
        public const string Stale = "The template changed after this page was loaded. Reload it and try again.";
        public const string OperationConflict = "The form was already used for a different change. Retry from the current page.";
        public const string Expired = "The form has expired. Retry the operation.";
        public const string BodyRequired = "Enter the template body.";

        public static string TooLong =>
            $"The template body can be at most {Pegasus.Core.Operations.EmailTemplates.MaximumBodyLength} characters.";

        public static string UnknownPlaceholder(string placeholder) =>
            $"{{{placeholder}}} is not a placeholder for this template.";

        public static string Name(EmailTemplatePurpose purpose) => purpose switch
        {
            EmailTemplatePurpose.TriageOutcomeReply => "Triage outcome reply",
            EmailTemplatePurpose.CaseReportDelivery => "Case report delivery",
            EmailTemplatePurpose.CaseChaser => "Case chaser",
            _ => throw new InvalidOperationException($"Unknown e-mail template purpose '{(int)purpose}'.")
        };
    }

    /// <summary>
    /// The state a staff "Link to Case" destination shows: a Case's stage, or
    /// a Triage Case's Triage state.
    /// </summary>
    public static string AssociationDestinationState(IntakeAssociationDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return destination.TriageState is { } triageState
            ? TriageState(triageState)
            : CaseStage(destination.State
                ?? throw new InvalidOperationException("A Case destination has no lifecycle state."));
    }

    /// <summary>
    /// A Not ready case's outstanding requirement as the operator reads it:
    /// the requirement and the action that resolves it. Both come from the
    /// case's recorded completeness facts, never from a sentence written here.
    /// </summary>
    public sealed record CaseRequirement(string Requirement, string Resolve);

    /// <summary>A missing completeness requirement as the Next action names it.</summary>
    public static string RequirementIncomplete(string requirement) => $"{requirement} incomplete";

    public static IReadOnlyList<CaseRequirement> CaseRequirements(bool instructionsMissing, bool imagesMissing)
    {
        var items = new List<CaseRequirement>(2);
        if (instructionsMissing)
        {
            items.Add(new("Instructions", "Receive the instruction"));
        }
        if (imagesMissing)
        {
            items.Add(new("Images", "Receive the vehicle images"));
        }
        return items;
    }

    /// <summary>
    /// Maps the current workflow-policy evaluation to the two operator actions.
    /// Raw completeness facts are evidence; the evaluation decides which facts
    /// are actually required under the persisted configuration.
    /// </summary>
    public static IReadOnlyList<CaseRequirement> CaseRequirements(
        IReadOnlyList<string> missingRequirements) =>
        CaseRequirements(
            missingRequirements.Contains("Instructions", StringComparer.Ordinal),
            missingRequirements.Contains("Images", StringComparer.Ordinal));

    public static string ProblemReportStatus(Pegasus.Core.Support.ProblemReportStatus status) => status switch
    {
        Pegasus.Core.Support.ProblemReportStatus.Sent => ProblemReports.Sent,
        Pegasus.Core.Support.ProblemReportStatus.Unknown => ProblemReports.DeliveryUnknown,
        _ => ProblemReports.NotSent
    };

    public static string ReleaseNoteStatus(Pegasus.Core.ReleaseNotes.ReleaseNoteStatus status) => status switch
    {
        Pegasus.Core.ReleaseNotes.ReleaseNoteStatus.Published => "Published",
        _ => "Draft"
    };

    /// <summary>
    /// A personal notification's cause in operator words. An AI draft reads by
    /// its kind, which the notification's route carries (the policy sets one
    /// route shape per kind: the Case's Estimate section, the message or the
    /// Case's correspondence, the Unidentified item); a route of another shape
    /// reads plainly as "AI draft ready". "Edited by" names the colleague only
    /// when the record carries a name; it carries a subject identifier, so it
    /// reads "Edited by a colleague".
    /// </summary>
    public static string NotificationCause(Pegasus.Core.Notifications.StaffNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return notification.Cause switch
        {
            Pegasus.Core.Notifications.StaffNotificationCause.AiDraftReady => AiDraftCause(notification.Route),
            Pegasus.Core.Notifications.StaffNotificationCause.CaseAssigned => "Assigned to you",
            Pegasus.Core.Notifications.StaffNotificationCause.EditedByOther => "Edited by a colleague",
            Pegasus.Core.Notifications.StaffNotificationCause.EmailReceived => "E-mail received",
            Pegasus.Core.Notifications.StaffNotificationCause.QueryReceived => "Query received",
            Pegasus.Core.Notifications.StaffNotificationCause.CancellationReceived => "Cancellation received",
            Pegasus.Core.Notifications.StaffNotificationCause.QueryAssigned => "Query assigned",
            _ => Humanise(notification.Cause.ToString())
        };
    }

    private static string AiDraftCause(string route)
    {
        if (route.StartsWith("/Unidentified/", StringComparison.OrdinalIgnoreCase))
        {
            return "Unidentified resolution ready";
        }

        if (route.StartsWith("/Inbox/", StringComparison.OrdinalIgnoreCase)
            || route.EndsWith("?section=correspondence", StringComparison.OrdinalIgnoreCase))
        {
            return "Query draft ready";
        }

        if (route.EndsWith("?section=estimate", StringComparison.OrdinalIgnoreCase))
        {
            return AiJobs.AiProposalReady;
        }

        return "AI draft ready";
    }

    /// <summary>
    /// How long ago an instant was, for the bell: "Just now", "12 min ago",
    /// "3 h ago", "Yesterday", "4 days ago", then the office date. The absolute
    /// time is on the element's title for anyone who needs it.
    /// </summary>
    public static string RelativeTime(DateTimeOffset value, DateTimeOffset now)
    {
        var elapsed = now - value;
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Just now";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalMinutes} min ago");
        }

        if (elapsed < TimeSpan.FromHours(24))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalHours} h ago");
        }

        var days = LondonCalendar.DateAt(now).DayNumber - LondonCalendar.DateAt(value).DayNumber;
        return days switch
        {
            <= 1 => "Yesterday",
            < 7 => string.Create(CultureInfo.InvariantCulture, $"{days} days ago"),
            _ => OfficeDate(value)
        };
    }

    /// <summary>A staff role name as the operator reads it.</summary>
    public static string StaffRole(string? roleName) => roleName switch
    {
        StaffRoleNames.Administrator => "Administrator",
        StaffRoleNames.Engineer => "Engineer",
        StaffRoleNames.User => "User",
        _ => Humanise(roleName)
    };

    /// <summary>
    /// The avatar initials for a display name: the first letter of the first
    /// two words, or the first two letters of a single word.
    /// </summary>
    public static string Initials(string? name)
    {
        var words = (name ?? string.Empty)
            .Split([' ', '.', '_', '-', '@'], StringSplitOptions.RemoveEmptyEntries);
        var letters = words.Length switch
        {
            0 => "?",
            1 => words[0].Length > 1 ? words[0][..2] : words[0],
            _ => string.Concat(words[0][0], words[1][0])
        };
        return letters.ToUpperInvariant();
    }

    /// <summary>A policy duration in the operator's words ("2 hours").</summary>
    public static string Duration(TimeSpan value)
    {
        if (value.TotalHours >= 1 && value.TotalHours == Math.Floor(value.TotalHours))
        {
            var hours = (int)value.TotalHours;
            return hours == 1 ? "1 hour" : $"{hours} hours";
        }

        var minutes = (int)Math.Round(value.TotalMinutes);
        return minutes == 1 ? "1 minute" : $"{minutes} minutes";
    }

    /// <summary>
    /// A reported average turnaround in the operator's words, in whole days
    /// once it reaches one; otherwise the same words as <see cref="Duration"/>.
    /// <paramref name="absent"/> stands for a period with no such transitions.
    /// </summary>
    public static string ReportTurnaround(TimeSpan? value, string absent)
    {
        if (value is not { } duration)
        {
            return absent;
        }

        if (duration.TotalDays >= 1)
        {
            var days = (int)Math.Round(duration.TotalDays, MidpointRounding.AwayFromZero);
            return days == 1 ? "1 day" : $"{days} days";
        }

        return Duration(duration);
    }

    /// <summary>The stage name for a persisted stage string, however stored.</summary>
    public static string CaseStage(string? state) =>
        Enum.TryParse<CaseLifecycleState>(state, ignoreCase: true, out var parsed)
            ? CaseStage(parsed)
            : Humanise(state);

    public static string CaseTypeName(CaseType type) => type switch
    {
        CaseType.Inspection => "Inspection",
        CaseType.Audit => "Audit",
        CaseType.InspectionAndAudit => "Inspection and audit",
        CaseType.Triage => "Triage",
        _ => Humanise(type.ToString())
    };

    public static string CaseTypeName(string? type) =>
        Enum.TryParse<CaseType>(type, ignoreCase: true, out var parsed)
            ? CaseTypeName(parsed)
            : Humanise(type);

    /// <summary>
    /// The chase schedule's own state, which is not the case stage: a case in
    /// Review can still be waiting on a scheduled chase.
    /// </summary>
    public static string ChaseState(CaseDueWorkState state) => state switch
    {
        CaseDueWorkState.Scheduled => "Chase due",
        CaseDueWorkState.Held => "Chasing paused",
        CaseDueWorkState.Stopped => "Chasing stopped",
        _ => Humanise(state.ToString())
    };

    /// <summary>
    /// The chase-state label for a persisted state string, however the
    /// projection carried it — the Work Centre's needs-attention rows hold
    /// Core enum names as strings. Same parse-then-delegate shape as
    /// <see cref="CaseStage(string?)"/>.
    /// </summary>
    public static string ChaseState(string? state) =>
        Enum.TryParse<CaseDueWorkState>(state, ignoreCase: true, out var parsed)
            ? ChaseState(parsed)
            : Humanise(state);

    /// <summary>
    /// The Work Centre's work-item kinds (FRD-12 § Work Centre): the row's
    /// "kind · reference" lead-in and the selected-work eyebrow both read
    /// from this one list.
    /// </summary>
    public static string NeedsAttentionKind(NeedsAttentionKind kind) => kind switch
    {
        Pegasus.Core.Operations.NeedsAttentionKind.CaseChase => "Case",
        Pegasus.Core.Operations.NeedsAttentionKind.HeldDecision => "Held decision",
        Pegasus.Core.Operations.NeedsAttentionKind.Unidentified => "Unidentified",
        Pegasus.Core.Operations.NeedsAttentionKind.Triage => "Triage",
        Pegasus.Core.Operations.NeedsAttentionKind.AiDraft => "AI draft",
        Pegasus.Core.Operations.NeedsAttentionKind.ReviewCase => "Review Case",
        Pegasus.Core.Operations.NeedsAttentionKind.UnassignedEngineer => "Assign Engineer",
        Pegasus.Core.Operations.NeedsAttentionKind.VehicleImagesPaired => WorkCentre.VehicleImagesPaired,
        Pegasus.Core.Operations.NeedsAttentionKind.OpenTasks => WorkCentre.OpenTasks,
        Pegasus.Core.Operations.NeedsAttentionKind.WithEngineer => WorkCentre.WithEngineer,
        _ => Humanise(kind.ToString())
    };

    /// <summary>
    /// The Work Centre's work-item priority chip: declaration order is the
    /// list order, and the tone is the chip treatment <c>_StatusChip</c>
    /// renders for that word (red for failed-or-overdue, amber for
    /// in-the-day, neutral for the rest).
    /// </summary>
    public static string NeedsAttentionPriority(NeedsAttentionPriority priority) => priority switch
    {
        Pegasus.Core.Operations.NeedsAttentionPriority.Overdue => "Overdue",
        Pegasus.Core.Operations.NeedsAttentionPriority.Today => "Today",
        Pegasus.Core.Operations.NeedsAttentionPriority.Normal => "Normal",
        _ => Humanise(priority.ToString())
    };

    /// <summary>
    /// The Image-initiated Case side of chase visibility
    /// (<see cref="ImageIntakeChaseSchedule"/>): a derived due/not-due read
    /// with no held/stopped state, reusing the exact "Chase due" wording
    /// <see cref="ChaseState"/> already uses for the Case side rather than a
    /// second spelling of the same fact.
    /// </summary>
    public static string ImageChaseState(bool chaseDue) => chaseDue ? "Chase due" : "Not yet due";

    /// <summary>
    /// An estimate line's operation type, in the same words the line-type
    /// choices offer. An unlisted code prints verbatim rather than being
    /// humanised, because the persisted vocabulary is closed
    /// (<see cref="EstimateLineCodes"/>) and an unknown value is a fault the
    /// operator should be able to read back exactly.
    /// </summary>
    public static string EstimateLineType(string type) => type switch
    {
        "rnr" => "Remove and refit",
        "repair" => "Repair",
        "new_part" => "New part",
        "check_labour" => "Check",
        "paint_new" => "Paint — new part",
        "paint_repair" => "Paint — repair",
        "paint_blend" => "Paint — blend",
        "paint_prep" => "Paint — preparation",
        "specialist_fixed" => "Specialist, fixed price",
        "specialist_wu" => "Specialist, by work units",
        _ => type
    };

    public static string DocumentRole(DocumentSemanticRole role) => role switch
    {
        DocumentSemanticRole.OriginalSource => "Original source",
        DocumentSemanticRole.Instruction => "Instruction",
        DocumentSemanticRole.Image => "Image",
        DocumentSemanticRole.Correspondence => "Correspondence",
        DocumentSemanticRole.EngineerReport => "Engineer report",
        DocumentSemanticRole.AuditReport => "Audit report",
        DocumentSemanticRole.Estimate => "Estimate",
        DocumentSemanticRole.ValuationEvidence => "Valuation Evidence",
        DocumentSemanticRole.Other => "Other",
        _ => Humanise(role.ToString())
    };

    public static string DocumentOrigin(DocumentSource source) => source switch
    {
        DocumentSource.Intake => "E-mail",
        DocumentSource.StaffUpload => "Staff upload",
        DocumentSource.ExternalCorrespondence => "Correspondence",
        DocumentSource.Generated => "Generated",
        DocumentSource.Automation => "Automatic",
        _ => Humanise(source.ToString())
    };

    /// <summary>
    /// The Image-initiated Case lifecycle state, in the operator's words.
    /// "Awaiting definitive instruction" is the established term for the open
    /// state (see the Image intake glossary entry in CONTEXT.md); the other
    /// two are the permanent outcomes the state can settle into.
    /// </summary>
    public static string ImageIntakeLifecycleState(ImageInitiatedCaseState state) => state switch
    {
        ImageInitiatedCaseState.AwaitingInstruction => "Awaiting definitive instruction",
        ImageInitiatedCaseState.MergedIntoInstructionCase => "Merged into Instruction-initiated Case",
        ImageInitiatedCaseState.StaffClosed => "Staff-closed",
        _ => Humanise(state.ToString())
    };

    /// <summary>
    /// The same state label where it continues a sentence ("None — awaiting
    /// definitive instruction"). Only the first character drops case, so
    /// "Instruction-initiated Case" survives intact.
    /// </summary>
    public static string ImageIntakeLifecycleStateContinuation(ImageInitiatedCaseState state)
    {
        var label = ImageIntakeLifecycleState(state);
        return string.Concat(char.ToLowerInvariant(label[0]).ToString(), label.AsSpan(1));
    }

    /// <summary>
    /// Why an Image-initiated Case is not linked automatically. The wording
    /// says who decides, not how the policy counts.
    /// </summary>
    public static string ImageIntakeWithheldReason(ImageIntakeAutomationWithheld reason) => reason switch
    {
        ImageIntakeAutomationWithheld.PrincipalDisagrees =>
            "Not linked automatically. No eligible Case with this registration belongs to the recorded Principal, so staff decide.",
        ImageIntakeAutomationWithheld.RegistrationAmbiguous =>
            "Not linked automatically. More than one eligible Case could match this registration, so staff decide.",
        _ => Humanise(reason.ToString())
    };

    public static string CustodyState(DocumentCustodyStatus status) => status switch
    {
        DocumentCustodyStatus.Pending => "Storing",
        DocumentCustodyStatus.Confirmed => "Stored",
        DocumentCustodyStatus.Failed => "Storage failed",
        _ => Humanise(status.ToString())
    };

    /// <summary>An image's custody state, in the operator's words.</summary>
    public static string ImageCustodyState(ImageCustodyState state) => state switch
    {
        Pegasus.Core.ImageIntake.ImageCustodyState.Pending => "Storing",
        Pegasus.Core.ImageIntake.ImageCustodyState.Confirmed => "Stored",
        Pegasus.Core.ImageIntake.ImageCustodyState.Merged => "Merged",
        Pegasus.Core.ImageIntake.ImageCustodyState.Failed => "Storage failed",
        _ => Humanise(state.ToString())
    };

    /// <summary>
    /// The case's Box folder state, in the operator's words, for the cases
    /// where there is no live folder to open. A confirmed folder with a remote
    /// identity is a link the page renders directly; every other state resolves
    /// to plain text here so a dead or empty link is never shown.
    /// </summary>
    public static string CustodyFolderState(CaseCustodyState state) => state switch
    {
        CaseCustodyState.Pending => "Box case folder: preparing",
        _ => "Box case folder: unavailable"
    };

    /// <summary>
    /// The Audit's <c>a.</c> Box folder state, mirroring
    /// <see cref="CustodyFolderState"/> for the cases where there is no
    /// confirmed folder to name.
    /// </summary>
    public static string AuditCustodyFolderState(CaseCustodyState state) => state switch
    {
        CaseCustodyState.Pending => "Box audit folder: preparing",
        _ => "Box audit folder: unavailable"
    };

    /// <summary>
    /// The Service health table's area grouping, in the operator's language.
    /// </summary>
    /// <remarks>
    /// The Core enum name for the queued receiving pipeline is internal
    /// vocabulary; the office word for that work is "Receiving".
    /// </remarks>
    public static string ServiceHealthAreaName(ServiceHealthArea area) => area switch
    {
        ServiceHealthArea.Mail => "Mail",
        ServiceHealthArea.Intake => "Receiving",
        ServiceHealthArea.Custody => "Box",
        ServiceHealthArea.Ai => "AI",
        ServiceHealthArea.Automation => "Automation",
        _ => Humanise(area.ToString())
    };

    /// <summary>The Service health row's state, as the operator reads it.</summary>
    public static string ServiceHealthStateName(ServiceHealthState state) => state switch
    {
        ServiceHealthState.Current or ServiceHealthState.Running => "Working",
        ServiceHealthState.Partial or ServiceHealthState.Failed or ServiceHealthState.ReviewRequired =>
            "Needs attention",
        ServiceHealthState.Configured => "No recorded activity",
        _ => "Unknown"
    };

    /// <summary>The external thing a service's recorded evidence depends on.</summary>
    public static string ServiceHealthDependencyName(ServiceHealthDependency dependency) => dependency switch
    {
        ServiceHealthDependency.MicrosoftGraph => "Microsoft Graph",
        ServiceHealthDependency.Worker => "Worker",
        ServiceHealthDependency.Box => "Box",
        ServiceHealthDependency.AiConnector => "AI",
        ServiceHealthDependency.AutomationClient => "Automation client",
        _ => Humanise(dependency.ToString())
    };

    /// <summary>
    /// One Service health row's service name, in the operator's language.
    /// </summary>
    /// <remarks>
    /// Two Core service names contain words banned from operator-facing copy
    /// ("Intake dispatch", "Automation ingress"); they are renamed here and
    /// only here. Everything else — mailbox addresses, "Sent evidence",
    /// "External work", "AI jobs" — is already the
    /// operator's own word and passes through. Only a Custody row that names
    /// an external-work kind code goes through <see cref="Humanise"/>; the row's
    /// Area decides that, not the characters in the name.
    /// </remarks>
    public static string ServiceHealthServiceName(ServiceHealthArea area, string? service) => service switch
    {
        ServiceHealthPolicy.IntakeDispatchService => "Receiving dispatch",
        ServiceHealthPolicy.AutomationService => "Automation clients",
        null or "" => "Unknown",
        ServiceHealthPolicy.ExternalWorkService => ServiceHealthPolicy.ExternalWorkService,
        _ => area == ServiceHealthArea.Custody ? Humanise(service) : service
    };

    /// <summary>
    /// Why an intake failed, in the operator's language.
    /// </summary>
    /// <remarks>
    /// The persisted failure code is what distinguishes one terminal outcome
    /// from another, and the operator has to be able to tell them apart —
    /// "it failed" is not an answer they can act on. What they do not need is
    /// the code itself: <c>unreadable_docx</c> is the writer's name for the
    /// fact, not the reader's. So the distinction stays and the spelling goes.
    /// </remarks>
    public static string IntakeFailure(string? failureCode) => failureCode switch
    {
        "unreadable_docx" => "The Word document could not be read",
        "unreadable_pdf" => "The PDF could not be read",
        "image_decode_failure" => "The image could not be read",
        "email_read_failure" => "The e-mail could not be read",
        "source_read_failure" or "source_reader_failure" =>
            "The file could not be read",
        "empty_message" => "The message was empty",
        "docx_limit_exceeded" =>
            "The Word document is larger than the processing limit allows",
        "intake_limit_exceeded" =>
            "The file is larger or more deeply nested than the processing limit allows",
        "unsupported_file_type" => "That file type is not supported",
        "deferred_file_type" => "That file type is not supported yet",
        "unsupported_source" => "That source is not supported",
        "artifact_retention_failure" or "not_run_retention_failure" =>
            "The original file could not be retained",
        "artifact_read_failure" => "The retained file could not be read back",
        "artifact_integrity_failure" or "staged_artifact_integrity_failure"
            or "integrity_failure" =>
            "The retained file did not match what was received",
        "persistence_failure" => "The result could not be saved",
        "invalid_intake_data" => "The file's contents were not valid",
        "source_identity_conflict" =>
            "The same receipt token was already used for a different file",
        "processing_lease_expired" => "Processing timed out and was not completed",
        "queue_poisoned" => "Processing was attempted repeatedly without completing",
        "intake_processing_failure" or "technical_failure"
            or "unexpected_intake_processing_failure" =>
            "Processing failed for a technical reason",
        null or "" => "Processing failed",
        _ => Humanise(failureCode)
    };

    /// <summary>
    /// Why a received item is not, and cannot become, a case — the one
    /// wording for this, shared by the case-creation screen and the upload
    /// confirmation surface so the same fact is never phrased twice.
    /// </summary>
    public static string IntakeCannotBecomeCaseReason(IntakeDecision decision) => decision switch
    {
        IntakeDecision.ImageIntakeRegistered =>
            "This item was registered as vehicle images. Image material never becomes a case on its own.",
        IntakeDecision.Unsupported =>
            "This file could not be read, so there is nothing to create a case from.",
        _ =>
            "This file failed while it was being processed, so there is nothing to create a case from."
    };

    /// <summary>
    /// A case history event in plain language.
    /// </summary>
    /// <remarks>
    /// Only the events whose natural phrasing differs from a mechanical
    /// expansion are listed; everything else is genuinely readable once the
    /// underscores are gone, and listing it would be a map to maintain for no
    /// gain.
    /// </remarks>
    public static string HistoryEvent(string? eventType) => eventType switch
    {
        "operator_note" => "Note",
        "case_workspace_saved" => "Case data saved",
        "case_accepted" => "Case created",
        "case_created_as_replacement" => "Created as a replacement case",
        "intake_case_association_seeded" => "Linked to the e-mail that started it",
        "intake_case_linked_automatic" => "E-mail linked automatically",
        "intake_receipt_recorded" => "E-mail received",
        "intake_receipt_reevaluated" => "E-mail reprocessed",
        "image_intake_registered" => "Vehicle images registered",
        "image_intake_registration_reasserted" => "Vehicle images re-registered",
        "merged_into_instruction_case" => "Merged into Instruction-initiated Case",
        "staff_closed" => "Staff-closed",
        "image_initiated_case_merged" => "Image-initiated Case merged in",
        "report_evidence_auto_linked" => "Sent report linked automatically",
        "standalone_audit_evidence_confirmed" => "Audit evidence confirmed",
        "audit_custody_confirmed" => "Audit evidence stored",
        "audit_custody_failed" => "Audit evidence storage failed",
        "case_document_removed" => "File removed",
        "case_document_type_set" => "Document type changed",
        "custody_confirmed" => "Document stored",
        "custody_failed" => "Document storage failed",
        "principal_inspection_mode_applied" => "Inspection mode taken from the principal",
        "principal_guidance_applied" => "Principal guidance applied",
        "case_guidance_applied" => "Guidance applied",
        "claim_source_guidance_applied" => "Claim source guidance applied",
        "triage_response_linked" => "Reply linked",
        "case_report_draft_previewed" => "Report draft viewed",
        "case_report_artifact_downloaded" => "Report downloaded",
        "estimate_scaled" => "Repair spec scaled",
        "estimate_scaling_removed" => "Scaling removed",
        "estimate_restored" => "Repair spec restored",
        "case_query_logged" => "Query logged",
        "case_query_received" => "Query received",
        "case_query_withdrawn" => "Query withdrawn",
        "case_query_resolved" => "Query marked done",
        "case_query_reopened" => "Query reopened",
        "case_query_assigned" => "Query assigned",
        _ => Humanise(eventType)
    };

    /// <summary>
    /// A date and time in the office's zone.
    /// </summary>
    /// <remarks>
    /// Every operator date surface renders Europe/London through this method.
    /// The alternative that used to be spread across the product was
    /// <c>ToLocalTime()</c>, which resolves against the server clock: on a
    /// developer workstation that happens to be Europe/London and looks
    /// correct, and on the deployed Linux container it is UTC. Through British
    /// Summer Time that made every one of those screens an hour early, with
    /// nothing on the page to say which zone it meant.
    /// </remarks>
    public static string OfficeTime(DateTimeOffset value) =>
        LondonCalendar.LocalAt(value).ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// A date and time in the office's zone, or <paramref name="absent"/> when
    /// there is no instant to show.
    /// </summary>
    public static string OfficeTime(DateTimeOffset? value, string absent) =>
        value is { } present ? OfficeTime(present) : absent;

    /// <summary>
    /// A date in the office's zone, for surfaces where the time of day is not
    /// part of what the operator is deciding.
    /// </summary>
    public static string OfficeDate(DateTimeOffset value) =>
        LondonCalendar.DateAt(value).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// The date a due instant falls due. A Due by date's instant is the
    /// midnight that ends it (<see cref="Pegasus.Core.Tasks.CaseDuePolicy.DueAt"/>),
    /// so the day named is the one just before the instant, as the Work
    /// Centre's due text reads it; a chase time names its own day.
    /// </summary>
    public static string DueDate(DateTimeOffset due) => OfficeDate(due.AddTicks(-1));

    /// <summary>
    /// The time of day in the office's zone, for the two-line surfaces that
    /// print the date above it.
    /// </summary>
    public static string OfficeClock(DateTimeOffset value) =>
        LondonCalendar.TimeAt(value).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// A file size the operator can act on. Bytes are an implementation detail
    /// and a KB branch lets a 10 MB limit render as "10240 KB", so MB with one
    /// decimal is the only form — and only where the size matters at all. A
    /// megabyte is 1,000,000 bytes, the unit the upload limits are declared
    /// in (FRD-18), so 100,000,000 bytes reads "100.0 MB".
    /// </summary>
    public static string FileSize(long bytes)
    {
        var megabytes = bytes / 1_000_000d;
        return megabytes < 0.1d
            ? "under 0.1 MB"
            : string.Create(CultureInfo.InvariantCulture, $"{megabytes:0.0} MB");
    }

    /// <summary>What one approved mailbox is used for, as read on /Administration/Mailboxes.</summary>
    public static string RouteScope(ApprovedMailboxRouteScope routeScope) => Humanise(routeScope.ToString());

    /// <summary>
    /// A stored chase reason for display. Maps the pre-release-15 wording
    /// (which used a banned word) without a data migration; anything else is
    /// already operator text.
    /// </summary>
    public static string ChaseReason(string? reason) =>
        reason == "Accepted intake is incomplete" ? "Details are incomplete" : reason ?? string.Empty;

    /// <summary>The operator words for a recorded inspection mode.</summary>
    public static string InspectionMode(CaseInspectionMode value) => value switch
    {
        CaseInspectionMode.PhysicalAddress => "Physical address",
        CaseInspectionMode.ImageBasedAssessment => "Image Based Assessment",
        _ => Humanise(value.ToString())
    };

    /// <summary>
    /// The tone a status chip takes for a state label. One table: the
    /// <c>_StatusChip</c> partial reads it when it renders, and the Inbox
    /// preview's JSON carries it so a chip repainted by script takes the
    /// colour the server would give it. Tones follow docs/design/README.md:
    /// amber is incomplete or pending, navy is Review and other in-flight
    /// states, red is blocked, failed or denied, green is confirmed completion
    /// only, and neutral covers absent, loading, current and settled-terminal
    /// states.
    /// </summary>
    public static string StatusTone(string? state)
    {
        // Normalising only `_` and `-` meant a PascalCase compound reaching the
        // chip — "NotReady", "ReportPreparation" — matched no key and fell
        // through to neutral grey, losing both the wording and the
        // amber/navy/green tone contract. Callers should pass an operator
        // label, but the chip must not silently mis-tone a state when one
        // does not.
        var key = Humanise(string.IsNullOrWhiteSpace(state) ? "Unknown" : state.Trim()).ToLowerInvariant();
        key = new string(key
            .Where(character => char.IsLetterOrDigit(character) || character == ' ')
            .ToArray());
        key = string.Join(' ', key.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return key switch
        {
            // Case lifecycle (D3 display labels)
            "not ready" => "amber",
            "review" => "navy",
            "with engineer" => "navy",
            "held" => "amber",
            "completed" => "green",
            "cancelled" or "principal cancellation" or "archived" => "neutral",
            "created in error" => "neutral",
            "reopened" or "open" => "navy",

            // Intake and Triage
            "needs sorting" or "unidentified" or "no case found" => "amber",
            "document text required" or "unsupported" => "amber",
            "awaiting information" => "amber",
            "finding recorded" => "navy",
            "registration missing" => "amber",

            // Query freshness
            "current" => "neutral",
            "loading" or "refreshing" => "neutral",
            "stale" => "amber",
            "partial" or "limit reached" => "amber",
            "unavailable" => "red",
            "failed" or "technical failure" or "error" => "red",

            // Access and mutation
            "denied" or "unauthenticated" => "red",
            "disabled" => "neutral",
            "enabled" => "navy",
            "conflict" or "stale version" => "red",
            "lease lost" => "red",
            "lease held" or "editing" => "navy",
            "lease expired" => "amber",
            "locked" => "neutral",

            // Access review
            "due no review recorded" or "due" => "amber",
            "recorded" => "neutral",

            // Chase state (Case-side CaseDueWorkState and the Image-initiated
            // Case's derived due/not-due read share this vocabulary)
            "chase due" => "amber",
            "chasing paused" => "amber",
            "chasing stopped" => "neutral",
            "not yet due" => "neutral",

            // External and document custody
            "pending" => "amber",
            "retry" => "amber",
            "success" => "green",
            "unknown" => "neutral",

            // Service health and request operations
            "running" => "blue",
            "working" => "green",
            "review required" => "amber",
            "needs attention" or "not set" => "amber",
            "active" => "navy",

            // Tone by meaning (v28 P2, 18 September 2026): green for an operation
            // or outcome that succeeded, red for one that did not, amber for
            // waiting on someone or something, navy for in hand, neutral for
            // settled. One table owns this; pages pass the label, never a tone.
            "case created" or "ready for case allocation" or "linked" or "linked to case" or "email linked" or "reply linked"
                or "vehicle images" or "triage"
                or "vehicle images registered" or "sent" or "report sent" or "delivered" or "saved" or "stored" or "document stored"
                or "approved" or "accepted" or "applied" or "resolved" or "done" or "registered" or "connected"
                or "configured" or "succeeded" or "passed" or "roadworthy" => "green",
            "could not be read" or "case not created" or "rejected" or "refused" or "overdue"
                or "unroadworthy" or "failure" => "red",
            // A Case with an open query (operator, 11 October 2026): its own tone.
            "query" => "purple",
            "creating case" or "not yet processed" or "draft" or "password change required"
                or "overridden" or "today" => "amber",
            "repairable" => "blue",
            "dismissed" or "staff closed" or "no recorded activity" => "neutral",
            _ when key.StartsWith("awaiting ", StringComparison.Ordinal) || key.EndsWith(" preparing", StringComparison.Ordinal) => "amber",
            _ when key.EndsWith(" failed", StringComparison.Ordinal) || key.StartsWith("failed ", StringComparison.Ordinal) => "red",

            // Every other state, including a closed Case's "Closed", is
            // settled or absent and reads neutral.
            _ => "neutral"
        };
    }

    /// <summary>
    /// Turns a persisted code into a sentence: <c>case_returned_to_review</c>
    /// becomes "Case returned to review", <c>ReportPreparation</c> becomes
    /// "Report preparation".
    /// </summary>
    public static string Humanise(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "Unknown";
        }

        var spaced = new StringBuilder(code.Length + 8);
        for (var index = 0; index < code.Length; index++)
        {
            var character = code[index];
            if (character is '_' or '-' or '.')
            {
                spaced.Append(' ');
                continue;
            }

            if (char.IsUpper(character) && index > 0 && !char.IsUpper(code[index - 1]))
            {
                spaced.Append(' ');
            }

            spaced.Append(character);
        }

        var words = spaced
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "Unknown";
        }

        var sentence = string.Join(' ', words).ToLowerInvariant();
        return char.ToUpperInvariant(sentence[0]) + sentence[1..];
    }

    /// <summary>
    /// The Automation activity view's Subject column, resolved from the raw
    /// subject id recorded on an Automation action or a denied automation
    /// request (<see cref="Pegasus.Core.Identity.AutomationActivityRecord"/>).
    /// There is exactly one Automation client per deployment (ADR-0011): a
    /// subject matching its configured client id is that client; anything else
    /// that is shaped like a GUID cannot be resolved to an identity and is never
    /// shown raw. A non-GUID subject (for example "anonymous", written for a
    /// request that carried no client identity at all) is already an honest
    /// label and passes through unchanged.
    /// </summary>
    public static string AutomationActorLabel(string subjectId, string? configuredClientId) =>
        configuredClientId is { Length: > 0 } && string.Equals(subjectId, configuredClientId, StringComparison.Ordinal)
            ? Pegasus.Web.Mcp.AutomationMcp.ClientDisplayName
            : Guid.TryParse(subjectId, out _)
                ? "Unknown automation client"
                : subjectId;

    /// <summary>
    /// The Action logs Area column. The recorded aggregate type is a storage
    /// key, so the handful an operator actually meets read as plain words and
    /// anything else falls back to the shared humanising rule rather than
    /// being hidden.
    /// </summary>
    public static string ActionLogArea(string area) => area switch
    {
        "ai_job" => "AI job",
        "automation_mcp" => "Automation",
        "email_template" => EmailTemplates.ActionLogArea,
        "staff_account" => "Staff account",
        "Security" => "Security",
        "Case" => "Case",
        _ => Humanise(area)
    };

    /// <summary>The chip an Action logs row's Actor carries when the Automation client did the work.</summary>
    public const string ActionLogAiActor = "AI";

    /// <summary>
    /// An Action logs row's Action: the stored event code as words, with AI
    /// and API as the initialisms they are (v41 A13).
    /// </summary>
    public static string ActionLogAction(string operation) =>
        string.Join(' ', Humanise(operation).Split(' ').Select(word => word.ToLowerInvariant() switch
        {
            "ai" => "AI",
            "api" => "API",
            _ => word
        }));

    /// <summary>An Action logs row's Result, written with a capital whatever its stored spelling (v41 A13).</summary>
    public static string ActionLogResult(string result) =>
        string.IsNullOrEmpty(result) ? result : char.ToUpperInvariant(result[0]) + result[1..];

    /// <summary>
    /// What a security event written before the acting principal was recorded
    /// can honestly be called in the Actor column. The row names the account it
    /// was about, never who acted, so it is labelled by what the event is —
    /// which is what the operator can act on — instead of asserting an unknown
    /// user. New rows carry their actor and never reach this.
    /// </summary>
    public static string SecurityEventActorLabel(string securityEventType) => securityEventType switch
    {
        nameof(SecurityEventType.SignIn) => "Sign-in",
        nameof(SecurityEventType.PasswordChanged) => "Password change",
        nameof(SecurityEventType.Token) => "Token request",
        nameof(SecurityEventType.Client) => "Automation client",
        nameof(SecurityEventType.RateLimited) => "Rate limit",
        nameof(SecurityEventType.SecurityStampChanged) => "Account security",
        nameof(SecurityEventType.SecurityConfigurationChanged) => "Security settings",
        _ => "Security"
    };

    /// <summary>
    /// What the application itself is called when it is the actor. The Worker's
    /// own actions are Pegasus acting, and naming the product is what an
    /// operator recognises where the persisted kind says "SystemWorker".
    /// </summary>
    public const string SystemActorLabel = "Pegasus";

    /// <summary>
    /// The Action logs Actor type filter. One option per actor class an
    /// operator distinguishes, each carrying the recorded
    /// <see cref="ActorKind"/> name as its value so the filter needs no second
    /// vocabulary. The Principal actor is deliberately absent: it is an
    /// attribution on an external caller, not a class of work an operator
    /// reviews here.
    /// </summary>
    public static IReadOnlyList<(string Value, string Label)> ActionLogActorTypes { get; } =
    [
        (nameof(ActorKind.Staff), "Staff"),
        (nameof(ActorKind.Automation), "AI (automation)"),
        (nameof(ActorKind.SystemWorker), "Pegasus (system)")
    ];

    /// <summary>
    /// The operator word for how material arrived. One owner for the channel
    /// vocabulary; the string overload accepts the persisted channel code.
    /// </summary>
    public static string SourceChannel(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "Manual upload",
        IntakeSourceChannel.Mailbox => "E-mail",
        IntakeSourceChannel.Automation => "Automation",
        IntakeSourceChannel.PrincipalApi => PrincipalSubmissionApi.Source,
        _ => throw new InvalidOperationException(
            $"Unknown intake source channel value '{(int)channel}'.")
    };

    /// <inheritdoc cref="SourceChannel(IntakeSourceChannel)" />
    public static string SourceChannel(string? code) => code switch
    {
        "manual_upload" => "Manual upload",
        "mailbox" => "E-mail",
        "automation" => "Automation",
        "principal_api" => PrincipalSubmissionApi.Source,
        _ => Humanise(code)
    };

    /// <summary>
    /// Where a Case datum came from, as the one word its source tag carries and
    /// the tag's tone (<c>src-tag--{Tone}</c>; empty is the neutral tag). Null
    /// for a value staff typed or corrected, and for no source: those carry no
    /// tag (operator, 23 September 2026).
    /// </summary>
    public static SourceTagWord? SourceTag(CaseDataSource? source) => source?.Kind switch
    {
        null or CaseDataSourceKind.StaffCorrection => null,
        CaseDataSourceKind.IntakeEvidence => SourceTagWord.Extracted,
        CaseDataSourceKind.MailRoute => SourceTagWord.Email,
        CaseDataSourceKind.VehicleLookup => SourceTagWord.Lookup,
        CaseDataSourceKind.PrincipalSetting => SourceTagWord.Principal,
        CaseDataSourceKind.PrincipalApi => SourceTagWord.PrincipalApi,
        CaseDataSourceKind.CaseAcceptance => SourceTagWord.Automatic,
        _ => null
    };

    /// <summary>
    /// The source tag of a recorded assessment value: the vehicle lookup's
    /// writes read Lookup, an Original report cell filled from the filed report
    /// and a VIN filled from a capture document Extracted, the agreed fee a new Case took from its Principal Principal,
    /// the findings a linked Triage filled Triage, any other Automation
    /// actor's AI, a Pegasus worker's Automatic; a staff
    /// value carries none.
    /// </summary>
    public static SourceTagWord? SourceTag(AssessmentFieldValue? field) => field is null ? null : field.RecordedByKind switch
    {
        ActorKind.Automation when field.RecordedBy == VehicleLookupFillPolicy.RecorderId => SourceTagWord.Lookup,
        ActorKind.Automation when field.RecordedBy == OriginalReportPrefillPolicy.RecorderId => SourceTagWord.Extracted,
        ActorKind.Automation when field.RecordedBy == Pegasus.Core.Intake.ThirdPartyReports.VehicleCaptureFill.RecorderId => SourceTagWord.Extracted,
        ActorKind.Automation when field.RecordedBy == PrincipalDefaultFeePolicy.RecorderId => SourceTagWord.Principal,
        ActorKind.Automation when field.RecordedBy == Pegasus.Core.Triage.TriageFindingFill.RecorderId => SourceTagWord.Triage,
        ActorKind.Automation => SourceTagWord.Ai,
        ActorKind.SystemWorker => SourceTagWord.Automatic,
        _ => null
    };

    /// <summary>One source tag: its word and its tone class suffix.</summary>
    public sealed record SourceTagWord(string Word, string Tone)
    {
        public static readonly SourceTagWord Extracted = new("Extracted", string.Empty);
        public static readonly SourceTagWord Email = new("E-mail", string.Empty);
        public static readonly SourceTagWord Lookup = new("Lookup", "lookup");
        public static readonly SourceTagWord Principal = new("Principal", string.Empty);
        public static readonly SourceTagWord PrincipalApi = new(PrincipalSubmissionApi.Source, string.Empty);
        public static readonly SourceTagWord Automatic = new("Automatic", string.Empty);
        public static readonly SourceTagWord Triage = new("Triage", string.Empty);
        public static readonly SourceTagWord Ai = new("AI", "ai");

        public string CssClass => Tone.Length == 0 ? "src-tag" : "src-tag src-tag--" + Tone;

        /// <summary>The tag as markup — the one place a source tag's span is written.</summary>
        public Microsoft.AspNetCore.Html.IHtmlContent Render()
        {
            var span = new Microsoft.AspNetCore.Mvc.Rendering.TagBuilder("span");
            span.Attributes["class"] = CssClass;
            span.Attributes["data-provenance-word"] = Word;
            span.InnerHtml.Append(Word);
            return span;
        }
    }

    /// <summary>
    /// A mail category in operator words (operator, 12 October 2026).
    /// </summary>
    public static string MailClassification(Pegasus.Core.Intake.MailCategory category) => category switch
    {
        Pegasus.Core.Intake.MailCategory.NewInstruction => "New instruction",
        Pegasus.Core.Intake.MailCategory.TriageRequest => "Triage request",
        Pegasus.Core.Intake.MailCategory.Cancellation => "Cancellation",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    /// <summary>
    /// The resolve dialog's destination words. The contract wordings
    /// cover the kinds a staff resolution completes directly. The prototype's "Create Case from accepted instruction"
    /// has no destination kind behind it — creating the case is the origin
    /// receipt's action — so it is not a select option.
    /// </summary>
    public static string UnidentifiedResolutionTarget(
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind kind) => kind switch
    {
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.InstructionCase =>
            "Add to existing Case",
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.ImageIntake =>
            "Register Image-initiated Case",
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.Triage =>
            "Link to Triage",
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.ExternalReference =>
            "External reference",
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.Closed =>
            "Close with reason",
        _ => Humanise(kind.ToString())
    };

    // Phase 6 Lane B (Inbox, Upload, Search, Operations, Administration) start

    // Phase 6 Lane B end
}
