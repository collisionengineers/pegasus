using System.Globalization;
using System.Text;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Tasks;
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
/// never reach markup: "NotReady", "PostReportComplete", "case_created" and
/// "InspectionAndAudit" are all things the codebase calls itself, not things
/// the business calls anything.
///
/// Two of these maps are settled business vocabulary and must not drift:
/// <see cref="CaseStage"/> carries the case lifecycle stage names, and the
/// distinct meanings of Audit, Triage, Unidentified and Blocked are reserved.
/// Everything else falls through to <see cref="Humanise"/>, which turns an
/// unknown code into a readable sentence rather than printing it verbatim —
/// event codes in particular are composed at several call sites, so a fixed map
/// would silently go stale.
/// </remarks>
public static class OperatorLabels
{
    /// <summary>
    /// The known principal on a pre-case record — an Image Intake or a Triage.
    /// One pair owns the concept for both surfaces: `Not known` is the absent
    /// principal and is never reused as a generic empty label elsewhere.
    /// </summary>
    public const string Principal = "Principal";

    public const string PrincipalNotKnown = "Not known";

    /// <summary>
    /// The Triage's own permanent reference, distinct from the originating
    /// provider claim number.
    /// </summary>
    public const string TriageReference = "Triage reference";

    public static string AttachmentSearchability(bool isSearchable) =>
        isSearchable ? "Searchable content" : "Content unavailable for search";

    public static string UnidentifiedReason(UnidentifiedReasonCode reason) => reason switch
    {
        UnidentifiedReasonCode.UnreadableOrCorruptContent => "Unreadable or corrupt content",
        UnidentifiedReasonCode.UnsupportedContent => "Unsupported content",
        UnidentifiedReasonCode.NoUsableIdentification => "No usable identification",
        UnidentifiedReasonCode.ConflictingIdentification => "Conflicting identification",
        UnidentifiedReasonCode.AmbiguousOwnershipOrDestination => "Ambiguous ownership or destination",
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
    /// What an Unidentified item's retained material is, for the Queues
    /// page's Images/E-mails filter and the row/detail "what is going on"
    /// text. Supersedes the old origin-kind label ("Intake receipt"), which
    /// named the internal record rather than the material and used the
    /// banned word "intake".
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
    /// The case lifecycle stage as the operator reads it (EPIC-011 D3): a
    /// display mapping only. <see cref="CaseLifecycleState.ReportPreparation"/>
    /// and <see cref="CaseLifecycleState.PostReport"/> both read "With
    /// Engineer", <see cref="CaseLifecycleState.PostReportComplete"/> reads
    /// "Complete", and every other terminal outcome reads "Closed · outcome".
    /// The Core enum is untouched.
    /// </summary>
    public static string CaseStage(CaseLifecycleState state) => state switch
    {
        CaseLifecycleState.NotReady => "Not ready",
        CaseLifecycleState.Held => "Held",
        CaseLifecycleState.Review => "Review",
        CaseLifecycleState.ReportPreparation or CaseLifecycleState.PostReport => "With Engineer",
        CaseLifecycleState.PostReportComplete => "Complete",
        CaseLifecycleState.ProviderCancelled => "Closed · Provider cancelled",
        CaseLifecycleState.CollisionEngineersRejected => "Closed · Collision Engineers rejected",
        CaseLifecycleState.CreatedInError => "Closed · Created in error",
        CaseLifecycleState.SourceEmailUnlinked => "Closed · E-mail unlinked",
        _ => Humanise(state.ToString())
    };

    /// <summary>The Triage record's own lifecycle words (moved here from the Cases page by CASE-025).</summary>
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
    /// What the retained-instruction analysis concluded, in the operator's own
    /// words. The enum name is never printed: "NoProfile" tells a member of
    /// staff nothing about what to do next.
    /// </summary>
    public static string RetainedInstructionAnalysisOutcome(
        Pegasus.Core.Intake.RetainedInstructionAnalysisOutcome outcome) => outcome switch
    {
        Pegasus.Core.Intake.RetainedInstructionAnalysisOutcome.Analyzed =>
            "Read from the document",
        Pegasus.Core.Intake.RetainedInstructionAnalysisOutcome.NoProfile =>
            "No provider document was recognised",
        Pegasus.Core.Intake.RetainedInstructionAnalysisOutcome.Ambiguous =>
            "More than one provider document was recognised",
        Pegasus.Core.Intake.RetainedInstructionAnalysisOutcome.SourceUnavailable =>
            "The retained file could not be read",
        Pegasus.Core.Intake.RetainedInstructionAnalysisOutcome.Conflict =>
            "The receipt changed before the analysis was recorded",
        _ => throw new InvalidOperationException(
            $"Unknown retained instruction analysis outcome '{(int)outcome}'.")
    };

    /// <summary>
    /// What one recorded field candidate is worth to a member of staff. The
    /// vocabulary is shared with the case-side source candidates, so a field
    /// never reads two different ways on two screens.
    /// </summary>
    public static string SourceCandidateDisposition(
        Pegasus.Core.Intake.SourceCandidateDisposition disposition) => disposition switch
    {
        Pegasus.Core.Intake.SourceCandidateDisposition.Usable => "Usable",
        Pegasus.Core.Intake.SourceCandidateDisposition.Missing => "Not stated in the document",
        Pegasus.Core.Intake.SourceCandidateDisposition.Ambiguous => "Ambiguous",
        Pegasus.Core.Intake.SourceCandidateDisposition.Conflicting => "Conflicting statements",
        _ => throw new InvalidOperationException(
            $"Unknown source candidate disposition '{(int)disposition}'.")
    };

    /// <summary>
    /// A Not ready case's outstanding requirement as the operator reads it:
    /// the requirement and the action that resolves it. Both come from the
    /// case's recorded completeness facts, never from a sentence written here.
    /// </summary>
    public sealed record CaseRequirement(string Requirement, string Resolve);

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

    /// <summary>The primary navigation and the shell's section labels — one list.</summary>
    public static class Nav
    {
        public const string Work = "Work";
        public const string Manage = "Manage";
        public const string WorkCentre = "Work Centre";
        public const string Inbox = "Inbox";
        public const string Upload = "Upload";
        public const string Cases = "Cases";
        public const string Search = "Search";
        public const string Operations = "Operations";
        public const string Administration = "Administration";
    }

    /// <summary>The administration areas (§1.12) — one list.</summary>
    public static class Admin
    {
        public const string Accounts = "Staff accounts & roles";
        public const string Principals = "Principals";
        public const string Configuration = "Workflow configuration";
        public const string Mail = "Mail settings";
        public const string Automation = "Automation & AI";

        // C08 shell administration areas start
        public const string ActionLogs = "Action logs";
        public const string AiJobs = "AI jobs";
        public const string Reports = "Reports";
        public const string Health = "Service health";
        public const string ValuationPresets = "Valuation presets";
        public const string ClaimSources = "Claim sources";
        // C08 shell administration areas end
    }

    /// <summary>The freshness words the shell and every page header share.</summary>
    public static class Freshness
    {
        public const string Current = "Current";
        public const string Never = "Never updated";

        public static string Label(string? status) => status switch
        {
            "loading" => "Refreshing",
            "stale" => "Stale",
            "partial" => "Partial",
            "unavailable" => "Unavailable",
            "failed" => "Failed",
            _ => Current
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
        Pegasus.Core.Operations.NeedsAttentionKind.Case => "Case",
        Pegasus.Core.Operations.NeedsAttentionKind.HeldDecision => "Held decision",
        Pegasus.Core.Operations.NeedsAttentionKind.Mail => "Mail",
        Pegasus.Core.Operations.NeedsAttentionKind.Triage => "Triage",
        Pegasus.Core.Operations.NeedsAttentionKind.ExternalWork => "External work",
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
        Pegasus.Core.Operations.NeedsAttentionPriority.High => "High",
        Pegasus.Core.Operations.NeedsAttentionPriority.Today => "Today",
        Pegasus.Core.Operations.NeedsAttentionPriority.Normal => "Normal",
        _ => Humanise(priority.ToString())
    };

    /// <summary>The chip tone for a work-item priority word.</summary>
    public static string NeedsAttentionPriorityTone(NeedsAttentionPriority priority) => priority switch
    {
        Pegasus.Core.Operations.NeedsAttentionPriority.Overdue
            or Pegasus.Core.Operations.NeedsAttentionPriority.High => "red",
        Pegasus.Core.Operations.NeedsAttentionPriority.Today => "amber",
        _ => "neutral"
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
    /// The application work view a classified message belongs in, from the
    /// Core operational-destination policy.
    /// </summary>
    /// <remarks>
    /// The abstention case reuses the exact "Unidentified" wording this page
    /// already shows for the unmatched Queue and Filed-to states
    /// (<see cref="Pegasus.Web.Pages.Mail.MessageModel.QueueLabel"/> and
    /// <see cref="Pegasus.Web.Pages.Mail.MessageModel.OutcomeLabel(IntakeDecision)"/>)
    /// rather than introducing a second operator-visible spelling of the same
    /// fail-closed state.
    /// </remarks>
    public static string MailOperationalDestinationLabel(MailOperationalDestination destination) => destination switch
    {
        MailOperationalDestination.ReceivingWork => "Receiving work",
        MailOperationalDestination.Queries => "Queries",
        MailOperationalDestination.DetailedClassification => "Detailed classification",
        MailOperationalDestination.Other => "Other",
        MailOperationalDestination.Triage => "Triage",
        MailOperationalDestination.Unidentified => "Unidentified",
        _ => Humanise(destination.ToString())
    };

    /// <summary>
    /// Where a repair specification's lines came from (ENG-002). The
    /// unresolved legacy route is the fallback: rows recorded before the
    /// product tracked a source at all.
    /// </summary>
    public static string RepairSpecificationRoute(RepairSpecificationSourceRoute route) => route switch
    {
        RepairSpecificationSourceRoute.Manual => "entered by hand",
        RepairSpecificationSourceRoute.Glasses => "imported from Glass's",
        RepairSpecificationSourceRoute.AudatexPdf => "imported from Audatex",
        RepairSpecificationSourceRoute.ApprovedAiProposal => "from an approved AI proposal",
        RepairSpecificationSourceRoute.Json => "imported from a JSON estimate",
        RepairSpecificationSourceRoute.AiDraft => "drafted by AI",
        _ => "recorded before source tracking"
    };

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
        DocumentSemanticRole.Other => "Other",
        _ => Humanise(role.ToString())
    };

    public static string DocumentOrigin(DocumentSource source) => source switch
    {
        DocumentSource.Intake => "E-mail",
        DocumentSource.StaffUpload => "Staff upload",
        DocumentSource.RequestUpload => "Upload link",
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

    public static string CustodyState(DocumentCustodyStatus status) => status switch
    {
        DocumentCustodyStatus.Pending => "Storing",
        DocumentCustodyStatus.Confirmed => "Stored",
        DocumentCustodyStatus.Failed => "Storage failed",
        _ => Humanise(status.ToString())
    };

    // CASE-032 start
    public static string ImageCustodyState(ImageCustodyState state) => state switch
    {
        Pegasus.Core.ImageIntake.ImageCustodyState.Pending => "Storing",
        Pegasus.Core.ImageIntake.ImageCustodyState.Confirmed => "Stored",
        Pegasus.Core.ImageIntake.ImageCustodyState.Merged => "Merged",
        Pegasus.Core.ImageIntake.ImageCustodyState.Failed => "Storage failed",
        _ => Humanise(state.ToString())
    };
    // CASE-032 end

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
    /// The state of an in-house upload request, as the operator reads it.
    /// </summary>
    /// <remarks>
    /// This describes the request Pegasus issues itself, distinct from the
    /// document custody states above; the enums share member names but no
    /// members, so one label method cannot serve both.
    /// </remarks>
    public static string UploadRequestState(RequestUploadStatus status) => status switch
    {
        RequestUploadStatus.Pending => "Being created",
        RequestUploadStatus.Active => "Active",
        RequestUploadStatus.Expired => "Expired",
        RequestUploadStatus.Exhausted => "No uploads left",
        RequestUploadStatus.Revoked => "Withdrawn",
        RequestUploadStatus.Failed => "Failed",
        _ => Humanise(status.ToString())
    };

    /// <summary>
    /// The state of one Operations-listed request operation, as the operator
    /// reads it on the Operations workspace.
    /// </summary>
    /// <remarks>
    /// The Operations projection covers both upload links and external work
    /// under one state vocabulary; <see cref="UploadRequestState"/> stays the
    /// map for the request surface itself.
    /// </remarks>
    public static string RequestOperationState(RequestOperationState state) => state switch
    {
        Pegasus.Core.Operations.RequestOperationState.Pending => "Pending",
        Pegasus.Core.Operations.RequestOperationState.Active => "Active",
        Pegasus.Core.Operations.RequestOperationState.Expired => "Expired",
        Pegasus.Core.Operations.RequestOperationState.Exhausted => "Exhausted",
        Pegasus.Core.Operations.RequestOperationState.Revoked => "Revoked",
        Pegasus.Core.Operations.RequestOperationState.Failed => "Failed",
        Pegasus.Core.Operations.RequestOperationState.Completed => "Completed",
        Pegasus.Core.Operations.RequestOperationState.UnknownExternal => "Unknown external",
        _ => Humanise(state.ToString())
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
        ServiceHealthArea.Custody => "Custody",
        ServiceHealthArea.Eva => "EVA",
        ServiceHealthArea.Ai => "AI",
        ServiceHealthArea.Automation => "Automation",
        _ => Humanise(area.ToString())
    };

    /// <summary>The Service health row's state, as the operator reads it.</summary>
    public static string ServiceHealthStateName(ServiceHealthState state) => state switch
    {
        ServiceHealthState.Current => "Current",
        ServiceHealthState.Partial => "Partial",
        ServiceHealthState.Failed => "Failed",
        ServiceHealthState.Running => "Running",
        ServiceHealthState.Configured => "Configured",
        ServiceHealthState.ReviewRequired => "Review required",
        _ => Humanise(state.ToString())
    };

    /// <summary>The external thing a service's recorded evidence depends on.</summary>
    public static string ServiceHealthDependencyName(ServiceHealthDependency dependency) => dependency switch
    {
        ServiceHealthDependency.MicrosoftGraph => "Microsoft Graph",
        ServiceHealthDependency.Worker => "Worker",
        ServiceHealthDependency.Box => "Box",
        ServiceHealthDependency.EvaApi => "EVA API",
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
    /// "External work", "EVA submissions", "AI jobs" — is already the
    /// operator's own word and passes through, as do external-work kind codes
    /// via <see cref="Humanise"/>.
    /// </remarks>
    public static string ServiceHealthServiceName(string? service) => service switch
    {
        ServiceHealthPolicy.IntakeDispatchService => "Receiving dispatch",
        ServiceHealthPolicy.AutomationService => "Automation clients",
        null or "" => "Unknown",
        _ => service.Contains('_') ? Humanise(service) : service
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
        "message_too_large" => "The message was too large to process",
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
        IntakeDecision.BlockedIntake =>
            "This item was blocked, with the reason recorded. It cannot become a case until it is corrected on the received item.",
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
        "engineer_finding_recorded" => "Engineer finding recorded",
        "report_evidence_auto_linked" => "Sent report linked automatically",
        "standalone_audit_evidence_confirmed" => "Audit evidence confirmed",
        "audit_custody_confirmed" => "Audit evidence stored",
        "audit_custody_failed" => "Audit evidence storage failed",
        "case_document_removed" => "File removed",
        "custody_confirmed" => "Document stored",
        "custody_failed" => "Document storage failed",
        "provider_inspection_mode_applied" => "Inspection mode taken from the principal",
        "triage_response_linked" => "Reply linked",
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
    /// The time of day in the office's zone, for the two-line surfaces that
    /// print the date above it.
    /// </summary>
    public static string OfficeClock(DateTimeOffset value) =>
        LondonCalendar.TimeAt(value).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// A file size the operator can act on. Bytes are an implementation detail
    /// and a KB branch lets a 10 MB limit render as "10240 KB", so MB with one
    /// decimal is the only form — and only where the size matters at all.
    /// </summary>
    public static string FileSize(long bytes)
    {
        var megabytes = bytes / 1024d / 1024d;
        return megabytes < 0.1d
            ? "under 0.1 MB"
            : string.Create(CultureInfo.InvariantCulture, $"{megabytes:0.0} MB");
    }

    /// <summary>
    /// The approved-mailbox allowlist's read-only route scope, as read on
    /// /Administration/Mailboxes. Explicit because the mechanical
    /// <see cref="Humanise"/> fallback would render <c>InboundIntake</c> as
    /// "Inbound intake", which carries the banned "intake" word.
    /// </summary>
    public static string RouteScope(ApprovedMailboxRouteScope routeScope) => routeScope switch
    {
        ApprovedMailboxRouteScope.InboundIntake => "New instructions and Triage mail (Inbox)",
        ApprovedMailboxRouteScope.SentEvidence => "Exact report and Triage evidence (Sent Items)",
        _ => Humanise(routeScope.ToString())
    };

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
    /// Turns a persisted code into a sentence: <c>case_returned_to_review</c>
    /// becomes "Case returned to review", <c>PostReportComplete</c> becomes
    /// "Post report complete".
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
    /// Where a value came from, as the one word the provenance icon announces
    /// and the approved Lucide glyph that carries it.
    /// </summary>
    /// <remarks>
    /// The sprite is a checksummed asset of seventeen glyphs and the design
    /// authority records that none was added, removed or redrawn, so two of the
    /// seven words share a glyph with a neighbour and lean on the tooltip to
    /// tell them apart.
    ///
    /// "AI" has no persisted distinction from a plain document read: both are
    /// IntakeEvidence. It is derived from the reader identity already carried on
    /// the source label, and falls back to Extracted rather than guessing.
    /// </remarks>
    /// <summary>
    /// The supplied/external/estimated classification a mileage figure carries. The
    /// binding rule sits in Core (<see cref="VehicleMileageEvidenceClassification"/>):
    /// a derived estimate is never presented as supplied.
    /// </summary>
    public static string MileageEvidence(VehicleMileageEvidenceClass value) => value switch
    {
        VehicleMileageEvidenceClass.Supplied => "Supplied",
        VehicleMileageEvidenceClass.External => "External",
        VehicleMileageEvidenceClass.Estimated => "Estimated",
        _ => Humanise(value.ToString())
    };

    /// <summary>
    /// The unit word a mileage figure carries ("12,345 miles").
    /// </summary>
    public static string MileageUnit(VehicleMileageUnit value) => value switch
    {
        VehicleMileageUnit.Miles => "miles",
        VehicleMileageUnit.Kilometres => "km",
        _ => Humanise(value.ToString())
    };

    /// <summary>
    /// The operator word for how material arrived. One owner for the channel
    /// vocabulary; the string overload accepts the persisted channel code.
    /// </summary>
    public static string SourceChannel(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "Manual upload",
        IntakeSourceChannel.Mailbox => "E-mail",
        IntakeSourceChannel.Automation => "Automation",
        IntakeSourceChannel.ProviderApi => ProviderSubmissionApi.Source,
        _ => throw new InvalidOperationException(
            $"Unknown intake source channel value '{(int)channel}'.")
    };

    /// <inheritdoc cref="SourceChannel(IntakeSourceChannel)" />
    public static string SourceChannel(string? code) => code switch
    {
        "manual_upload" => "Manual upload",
        "mailbox" => "E-mail",
        "automation" => "Automation",
        "provider_api" => ProviderSubmissionApi.Source,
        _ => Humanise(code)
    };

    public static (string Word, string Icon) Provenance(CaseDataSource? source)
    {
        var isAiReader = source is not null
            && source.Kind == CaseDataSourceKind.IntakeEvidence
            && (source.Label.Contains("ai", StringComparison.OrdinalIgnoreCase)
                || source.PolicyKey.Contains("ai", StringComparison.OrdinalIgnoreCase));

        return source?.Kind switch
        {
            null => ("Unknown", "icon-info"),
            CaseDataSourceKind.StaffCorrection => ("Staff", "icon-user"),
            CaseDataSourceKind.IntakeEvidence when isAiReader => ("AI", "icon-filter"),
            CaseDataSourceKind.IntakeEvidence => ("Extracted", "icon-file-text"),
            CaseDataSourceKind.MailRoute => ("E-mail", "icon-arrow-right"),
            CaseDataSourceKind.VehicleLookup => ("Lookup", "icon-search"),
            CaseDataSourceKind.ProviderSetting => ("Principal", "icon-shield"),
            CaseDataSourceKind.ProviderApi => (
                ProviderSubmissionApi.Source,
                ProviderSubmissionApi.ProvenanceIcon),
            CaseDataSourceKind.CaseAcceptance => ("Automatic", "icon-refresh-cw"),
            _ => ("Unknown", "icon-info")
        };
    }

    /// <summary>
    /// A mail classification in operator words: the settled family label, with
    /// the subtype appended after a separator dot ("New instruction ·
    /// Inspection"). Other categories carry the operator's own name verbatim.
    /// </summary>
    public static string MailClassification(Pegasus.Core.Intake.MailCategory category)
    {
        if (category.IsOther)
        {
            return category.OtherName!;
        }

        var family = category.ReceivedFamily is { } received
            ? received switch
            {
                Pegasus.Core.Intake.ReceivedMailFamily.General => "General",
                Pegasus.Core.Intake.ReceivedMailFamily.Billing => "Billing",
                Pegasus.Core.Intake.ReceivedMailFamily.NewInstructionReceived => "New instruction",
                Pegasus.Core.Intake.ReceivedMailFamily.NonClientRelated => "Not client related",
                Pegasus.Core.Intake.ReceivedMailFamily.InProgressCases => "In-progress case",
                Pegasus.Core.Intake.ReceivedMailFamily.PostReportEmails => "Post-report",
                Pegasus.Core.Intake.ReceivedMailFamily.PreInstructionEmails => "Pre-instruction",
                Pegasus.Core.Intake.ReceivedMailFamily.InternalCc => "Internal CC",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            }
            : category.SentFamily switch
            {
                Pegasus.Core.Intake.SentMailFamily.ReportSent => "Report sent",
                Pegasus.Core.Intake.SentMailFamily.CaseRejected => "Case rejected",
                Pegasus.Core.Intake.SentMailFamily.QuerySent => "Query sent",
                Pegasus.Core.Intake.SentMailFamily.AdditionalImageRequest => "Additional image request",
                _ => throw new ArgumentOutOfRangeException(nameof(category))
            };
        var prefixed = category.Direction == Pegasus.Core.Intake.MailDirection.Sent
            ? $"Sent · {family}"
            : family;
        return category.Subtype is { } subtype
            ? $"{prefixed} · {HumanizeSlug(subtype)}"
            : prefixed;
    }

    /// <summary>
    /// The resolve dialog's destination words. The four contract wordings
    /// (EPIC-011 §1.6) cover the kinds a staff resolution completes directly;
    /// Triage and Blocked intake remain real Core destinations and keep their
    /// settled names. The prototype's "Create Case from accepted instruction"
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
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.BlockedIntake =>
            "Blocked intake",
        Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.ExternalReference =>
            "Close with reason",
        _ => Humanise(kind.ToString())
    };

    /// <summary>
    /// The AI job ledger's words on the Operations AI Job List (PLAT-049).
    /// </summary>
    /// <remarks>
    /// The kind and state wordings are FRD-11 &#167; AI Job List's own; the Core
    /// enum names are the writer's spelling of them. This is the only map of
    /// either in the Web layer &#8212; nothing named these states before.
    ///
    /// <c>Shared/_StatusChip</c> already owns tones for Completed, Failed,
    /// Cancelled and settled terminal labels. <see cref="StateToneOverride"/>
    /// supplies only the three AI-specific labels that partial does not know.
    /// </remarks>
    public static class AiJobs
    {
        public const string PanelTitle = "AI Job List";
        public const string SendUnidentified = "Send Unidentified to AI";
        public const string CompleteJob = "Complete job";
        public const string Cancel = "Cancel";
        public const string ReviewEstimate = "Review estimate";
        public const string OpenQuery = "Open query";
        public const string Review = "Review";

        /// <summary>
        /// A queue pass names no record: its Core subject reference is the
        /// internal token <c>unidentified-queue</c>, which no operator reads.
        /// </summary>
        public const string QueueRecord = "Unidentified queue";

        public static string Kind(Pegasus.Core.AiWork.AiJobKind kind) => kind switch
        {
            Pegasus.Core.AiWork.AiJobKind.Estimate => "Estimate",
            Pegasus.Core.AiWork.AiJobKind.UnidentifiedResolution => "Unidentified resolution",
            Pegasus.Core.AiWork.AiJobKind.QueryResponse => "Query response",
            Pegasus.Core.AiWork.AiJobKind.UnidentifiedQueuePass => "Unidentified-queue pass",
            Pegasus.Core.AiWork.AiJobKind.MarketResearch => "Market research",
            _ => Humanise(kind.ToString())
        };

        public static string State(Pegasus.Core.AiWork.AiJobState state) => state switch
        {
            Pegasus.Core.AiWork.AiJobState.Queued => "Queued",
            Pegasus.Core.AiWork.AiJobState.Taken => "Taken",
            Pegasus.Core.AiWork.AiJobState.DraftReady => "Draft ready",
            Pegasus.Core.AiWork.AiJobState.Completed => "Completed",
            Pegasus.Core.AiWork.AiJobState.Failed => "Failed",
            Pegasus.Core.AiWork.AiJobState.Cancelled => "Cancelled",
            Pegasus.Core.AiWork.AiJobState.Expired => "Expired",
            _ => Humanise(state.ToString())
        };

        /// <summary>
        /// The explicit chip tone only for AI state labels not owned by
        /// Shared/_StatusChip. A null lets the shared partial apply its single
        /// tone vocabulary.
        /// </summary>
        public static string? StateToneOverride(Pegasus.Core.AiWork.AiJobState state) => state switch
        {
            Pegasus.Core.AiWork.AiJobState.Queued or Pegasus.Core.AiWork.AiJobState.DraftReady => "amber",
            Pegasus.Core.AiWork.AiJobState.Taken => "navy",
            _ => null
        };

        /// <summary>The panel meta.</summary>
        public static string Count(int jobs) => jobs == 1
            ? "1 job"
            : string.Create(CultureInfo.InvariantCulture, $"{jobs} jobs");
    }

    // PLAT-069: Operations partial-data notices.
    public static class OperationsNotices
    {
        public const string PartialData = "Partial data";
    }

    /// <summary>
    /// The recorded EVA facts available to the Operations panel (PLAT-049).
    /// </summary>
    public static class EvaHandoffs
    {
        public const string PanelTitle = "EVA handoffs";
        public const string PendingWork = "Pending work";
        public const string LatestActivity = "Latest activity";
        public const string Failures = "Failures";
        public const string Failure = "Failure";
        public const string Submitted = "Submitted";
        public const string Failed = "Failed";
    }

    /// <summary>The Workflow configuration administration surface — one list.</summary>
    public static class WorkflowConfiguration
    {
        public static string Meta(int policyVersion) => $"Version {policyVersion}";
    }

    /// <summary>The provider-submission API's operator vocabulary — one list.</summary>
    public static class ProviderSubmissionApi
    {
        public const string Source = "Provider API";
        public const string ProvenanceIcon = "icon-link";
    }

    private static string HumanizeSlug(string slug)
    {
        var words = slug.Replace('-', ' ').Replace('_', ' ');
        return words.Length == 0 ? words : char.ToUpperInvariant(words[0]) + words[1..];
    }

    /// <summary>
    /// Staff correspondence's own state (S12, C08): the operator sees only
    /// whether it is on its way, delivered, or needs attention — the internal
    /// attempt-stage vocabulary (draft creation, attaching, sending) is
    /// writer detail, not a distinction the operator acts on differently.
    /// "Unknown" is the one state that ever offers Reconcile rather than a
    /// resend: a resend from an unknown outcome could double-send a message
    /// that already reached Outlook.
    /// </summary>
    public static class StaffMail
    {
        public const string Reconcile = "Reconcile";

        public static string State(Pegasus.Core.Operations.StaffMailState state) => state switch
        {
            Pegasus.Core.Operations.StaffMailState.Sent => "Sent",
            Pegasus.Core.Operations.StaffMailState.Failed => "Failed",
            Pegasus.Core.Operations.StaffMailState.Cancelled => "Cancelled",
            Pegasus.Core.Operations.StaffMailState.Unknown => "Unknown",
            _ => "Submitted"
        };
    }

    /// <summary>The Mail settings area labels and status values — one list.</summary>
    public static class MailSettings
    {
        public const string Description = "Approved mailboxes and mail categories";
        public const string ApprovedMailboxes = "Approved mailboxes";
        public const string MailCategories = "Mail categories";
        public const string Mailbox = "Mailbox";
        public const string Scope = "Scope";
        public const string LastUpdate = "Last update";
        public const string State = "State";
        public const string Activated = "Activated";
        public const string Subscription = "Subscription";
        public const string ReviewFoldersRefresh = "Review folders / Refresh";
        public const string Category = "Category";
        public const string Review = "Review";
        public const string ReviewFolders = "Review folders";
        public const string AddMailbox = "Add mailbox";
        public const string AddCategory = "Add category";
        public const string SaveMailbox = "Save mailbox";
        public const string SaveCategory = "Save category";
        public const string Refresh = "Refresh";
        public const string ApprovedAddress = "Approved address";
        public const string RouteScope = "Route scope";
        public const string DisplayName = "Display name";
        public const string Reason = "Reason";
        public const string NoApprovedMailboxes = "No approved mailboxes";
        public const string NoMailCategories = "No mail categories";
        public const string NotActivated = "Not activated";
        public const string NoSubscription = "None.";
        public const string Configured = "Configured";
        public const string NotConfigured = "Not configured";

        public static string Meta(int mailboxCount, int categoryCount) =>
            $"{mailboxCount} approved {(mailboxCount == 1 ? "mailbox" : "mailboxes")} · " +
            $"{categoryCount} mail {(categoryCount == 1 ? "category" : "categories")}";

        /// <summary>
        /// Both state vocabularies are the enum names themselves, so they
        /// delegate to <see cref="Humanise"/> rather than restating a second
        /// copy of the same two words.
        /// </summary>
        public static string MailboxState(ApprovedMailboxState state) =>
            Humanise(state.ToString());

        public static string CategoryState(ApprovedOutlookCategoryState state) =>
            Humanise(state.ToString());

        public static string FolderState(bool configured) =>
            configured ? Configured : NotConfigured;

        /// <summary>
        /// The folder disclosure's own control label: how many of the logical
        /// folders this mailbox has bound, without expanding the list.
        /// </summary>
        public static string ReviewFoldersProgress(int configured, int total) =>
            $"{ReviewFolders} ({configured} of {total})";

        public static string PollStatus(
            ApprovedMailbox mailbox,
            ApprovedMailboxPollStatus? status)
        {
            if (status is null)
            {
                return mailbox.State == ApprovedMailboxState.Approved
                    && mailbox.RouteScopes.Contains(ApprovedMailboxRouteScope.InboundIntake)
                        ? "Not yet polled."
                        : "Not polled.";
            }

            var completed = status.LastCompletedAtUtc is { } lastCompletedAtUtc
                ? $"Last completed {OfficeTime(lastCompletedAtUtc)}."
                : "No completed poll yet.";
            var due = $" Next due {OfficeTime(status.DueAtUtc)}.";
            var failure = status.LastFailureCode switch
            {
                null => string.Empty,
                "mailbox_access_denied" =>
                    " The tenant has not granted this application access to this mailbox.",
                "mailbox_not_approved" =>
                    " The last attempt stopped because this mailbox was no longer approved.",
                var code => $" Last failure: {Humanise(code)}."
            };
            return $"{completed}{due}{failure}";
        }

        public static string SubscriptionStatus(ApprovedMailboxSubscription? subscription)
        {
            if (subscription is null)
            {
                return NoSubscription;
            }

            var state = $"{Humanise(subscription.LifecycleState.ToString())}.";
            var expires = $" Expires {OfficeTime(subscription.ExpiresAtUtc)}.";
            var failure = subscription.LastMaintenanceFailureCode is { } code
                ? $" Last failure: {Humanise(code)}."
                : string.Empty;
            return $"{state}{expires}{failure}";
        }
    }

    /// <summary>
    /// The consolidated "Staff accounts &amp; roles" administration area
    /// (EPIC-011 §1.12) — one list. The area's own name lives in
    /// <see cref="Admin.Accounts"/>; the three <see cref="StaffRole"/> names
    /// are already the settled operator words and go through
    /// <see cref="Humanise(string?)"/> rather than being spelled a second
    /// time here.
    /// </summary>
    public static class StaffAccounts
    {
        public const string Enabled = "Enabled";
        public const string Disabled = "Disabled";
        public const string PasswordChangeRequired = "Password change required";

        /// <summary>
        /// The chip shown where Core reports an outstanding access review
        /// (<c>StaffAccessReviewProjection.ReviewIsOutstanding</c>). "Due"
        /// is the word <c>_StatusChip</c> already tones amber.
        /// </summary>
        public const string ReviewDue = "Due";

        public static string State(bool isEnabled) => isEnabled ? Enabled : Disabled;

        public const string PasswordChangeComplete = "Password change complete";
        public const string Disable = "Disable";
        public const string Review = "Review";
        public const string Reason = "Reason";
        public const string Confirm = "Confirm";
        public const string DisableConsequence =
            "Disabling revokes existing browser sessions; the account is retained permanently.";
        public const string SignOffEngineer = "Sign-off Engineer";
        public const string Yes = "Yes";
        public const string No = "No";
        public const string PrintedName = "Printed name";
        public const string Qualifications = "Qualifications";
        public const string SignatureImage = "Signature image";
        public const string OnFile = "On file";
        public const string NotOnFile = "Not on file";
        public const string UploadSignature = "Upload signature";
        public const string ReplaceSignature = "Replace signature";
        public const string DefaultSignOffEngineer = "Default sign-off Engineer";
        public const string Settings = "Settings";
        public const string Save = "Save";
        public const string Cancel = "Cancel";
        public const string CloseDialog = "Close dialog";
        public const string SignatureMissing = "Signature missing";
        public const string QualificationsMissing = "Yes · qualifications missing";
        public const string Default = "Yes · default";
        public const string NotEligible = "Yes · not eligible";
        public const string PrintedNameRequired =
            "Enter the printed name for the Sign-off Engineer.";
        public const string SignatureInvalid =
            "Select a PNG signature image no larger than 1 MiB.";
        public const string EngineerRoleRequired =
            "Only an Engineer account can be a Sign-off Engineer.";
        public const string DefaultRequiresEligible =
            "The default Sign-off Engineer must be eligible to sign off.";
        public const string SignOffUpdated = "Sign-off Engineer settings updated.";

        public static string SignOffState(StaffAccountSummary account)
        {
            if (!account.Roles.Contains(Pegasus.Core.Identity.StaffRole.Engineer))
            {
                return "—";
            }

            if (!account.SignOff.IsSignOffEngineer)
            {
                return No;
            }

            if (!account.SignOff.HasSignature)
            {
                return SignatureMissing;
            }

            if (account.SignOff.Qualifications is null)
            {
                return QualificationsMissing;
            }

            if (account.SignOff.IsDefault)
            {
                // Role, sign-off flag, and signature presence are already
                // confirmed by the earlier branches; only enabled state
                // remains to determine eligibility here.
                return account.IsEnabled ? Default : NotEligible;
            }

            return Yes;
        }
    }

    /// <summary>
    /// The Automation &amp; AI administration area's words (EPIC-011 §1.12) —
    /// one list. <see cref="Admin.Automation"/> above is the area's name in the
    /// rail; these are the two panels inside it.
    /// </summary>
    public static class AutomationAdmin
    {
        public const string AutomationPanel = "Automation";
        public const string AiSettingsPanel = "AI settings";
        public const string Enabled = "Enabled";
        public const string Stopped = "Stopped";
        public const string RegisteredClients = "Registered clients";
        public const string ActiveJobs = "Active jobs";
        public const string FailedJobs = "Failed jobs";
        public const string Stop = "Stop automation";
        public const string Start = "Start automation";

        /// <summary>
        /// The one consequence sentence on the kill switch, from the design
        /// authority's necessary-copy allowance for a destructive action.
        /// </summary>
        public const string StopConsequence =
            "In-flight work remains visible and no result is discarded.";

        public const string ChannelToken = "Channel token";
        public const string ChannelTokenEntered = "Entered from Administration";
        public const string ChannelTokenStandard = "Standard setting";
        public const string ChannelTokenChanged = "Changed";
        public const string ChannelAddress = "Channel address";
        public const string Timeout = "Timeout in seconds";
        public const string NewChannelToken = "New channel token";
        public const string SendToAiEnabled = "Reviewed AI proposals enabled";
        public const string Save = "Save AI settings";
        public const string RemoveChannelToken = "Remove the channel token";
        public const string Reason = "Reason";

        /// <summary>The state word for a switch an administrator holds.</summary>
        public static string SwitchState(bool enabled) => enabled ? Enabled : Stopped;

        public const string ClientIdentifier = "Client identifier";
        public const string GrantedScopes = "Granted scopes";
    }

    /// <summary>The retained post-report query's AI job words (AUTO-014).</summary>
    public static class QueryResponseJobs
    {
        public const string Source = "Post-report";
        /// <summary>
        /// Deliberately not "Send query to AI", which would match the shape of
        /// "Send Unidentified to AI" and "Send to Claude". Those two send a
        /// record to be worked; this one queues a ledger row for a draft reply
        /// and sends nothing. The sibling shape would misdescribe the action,
        /// so the wording differs on purpose rather than by oversight.
        /// </summary>
        public const string Create = "Draft reply with AI";
        public const string Created = "AI reply job created.";
        public const string AutomationStopped = "Automation stopped";
        public const string AvailableInPostReportWork = "Available in post-report work";
        public const string CaseUnavailable = "Case unavailable";
        public const string InvalidSource =
            "This message is not a linked post-report message.";
    }

    /// <summary>
    /// The Case workspace's Vehicle, Inspection address and Case Files
    /// sections (EPIC-011 §1.8) — one list. Appended by CASE-027 inside its
    /// own nested class; no member above is reordered or edited.
    /// </summary>
    public static class CaseWorkspace
    {
        // CASE-039: Engineer notes
        public const string EngineerNotesSectionTitle = "Engineer notes";
        public const string AddEngineerNote = "Add note";
        public const string AddEngineerNoteTitle = "Add Engineer note";
        public const string EngineerNoteField = "Note";
        public const string EngineerNoteAdded = "The Engineer note was added.";

        public static string EngineerNoteCount(int count) =>
            count == 1 ? "1 note" : $"{count} notes";
        // end CASE-039

        // CASE-009: read-only query correspondence table.
        public const string Received = "Received";
        public const string Sender = "Sender";
        public const string Subject = "Subject";
        public const string Classification = "Classification";
        public const string OpenMessage = "Open message";

        public const string VehicleFactsPanel = "Vehicle";
        public const string VehicleChecksPanel = "Vehicle checks";
        public const string RefreshDvla = "Refresh DVLA";
        public const string RefreshDvsaMot = "Refresh DVSA/MOT";
        public const string RunExperianCheck = "Run Experian check";

        /// <summary>
        /// Why the Experian control is drawn disabled (EPIC-011 D7/D22,
        /// ENG-001). Always supplied: <c>.gated::after</c> renders
        /// <c>attr(data-condition)</c> unguarded, so a <c>.gated</c> span
        /// without one paints an empty pill (PLAT-061).
        /// </summary>
        public const string ExperianSeamCondition = "Experian is not connected";

        public const string VehicleChecksHistory = "Recorded checks";
        public const string AcceptSuggestion = "Accept";
        public const string CorrectSuggestion = "Correct";
        public const string InspectionAddressPanel = "Inspection address";
        public const string ProviderDefaultInspectionAddress = "Provider default";
        // CASE-041: Inspect-at choices and storage-location labels.
        public const string InspectAt = "Inspect at";
        public const string Source = "Source";
        public const string ImageBasedAssessment = "Image Based Assessment";
        public const string ClaimantAddress = "Claimant address";
        public const string RepairerLocation = "Repairer location";
        public const string StorageLocation = "Storage location";
        public const string PreviousAddress = "Previous";
        public const string ManualEntry = "Manual entry";
        public const string NotRecorded = "Not recorded";
        public const string NotRecordedSuffix = " · not recorded";
        // End CASE-041.
        public const string FilesPanel = "Files";
        public const string UploadRequestsPanel = "Public upload requests";
        public const string InstructionPhotographs = "Instruction photographs";
        public const string VehicleImages = "Vehicle images";
        public const string AddEvidence = "Add evidence";
        public const string OpenOperations = "Open Operations";
        public const string Preview = "Preview";
        public const string SaveAs = "Save as";
        public const string ThirdPartyVehicle = "Third-party vehicle";

        /// <summary>
        /// Why a refresh control is disabled: the lookup searches on the
        /// case's registration, and this case has none recorded. State, not a
        /// seam — the control enables as soon as a registration is recorded.
        /// </summary>
        public const string NoRegistrationCondition = "No registration recorded";

        /// <summary>One section of the Case record, as the jump-nav names it.</summary>
        public sealed record CaseSection(string Key, string Label, string Icon);

        /// <summary>
        /// The eleven Case record sections in their fixed order (D30,
        /// FRD-12 §Case workspace). One list: the page model's accepted
        /// <c>?section=</c> vocabulary, the jump-nav, the section hosts and
        /// their headings all read it, so no second section list exists in
        /// Razor, CSS or script.
        /// </summary>
        public static readonly IReadOnlyList<CaseSection> Sections =
        [
            new("overview", "Overview", "icon-layout-dashboard"),
            new("engineer-notes", EngineerNotesSectionTitle, "icon-pencil"),
            new("inspection", "Inspection", "icon-map-pin"),
            new("vehicle", "Vehicle", "icon-car"),
            new("damage", "Damage", "icon-alert-triangle"),
            new("valuation", "Valuation", "icon-file-text"),
            new("estimate", "Estimate", "icon-list"),
            new("settlement", "Settlement", "icon-check-circle"),
            new("report", "Report", "icon-file"),
            new("files", "Files", "icon-folder"),
            new("notes", "Notes", "icon-history")
        ];

        /// <summary>The section a <c>?section=</c> value the record does not own selects.</summary>
        public const string DefaultSectionKey = "overview";

        public const string SectionNav = "Case sections";

        // The identity ribbon the frame itself renders (D29, D31).
        public const string RibbonReference = "Case/PO";
        public const string RibbonRegistration = "Registration";
        public const string RibbonClaimant = "Claimant";
        public const string RibbonPrincipal = "Principal";
        public const string RibbonState = "State";
        public const string RibbonEngineer = "Engineer";

        /// <summary>
        /// What the record prints where a value it would show is not held.
        /// </summary>
        public const string AbsentValue = "Not recorded";

        // CASE-040: Sign-off Engineer / Send to EVA labels
        public const string SignOffEngineer = "Sign-off Engineer";
        public const string Unassigned = "Unassigned";
        public const string ReasonForAction = "Reason for action";
        public const string AssignEngineer = "Assign Engineer";
        public const string SetSignOffEngineer = "Set Sign-off Engineer";
        public const string SendToEva = "Send to EVA";
        public const string EvaHandoff = "EVA handoff";
        public const string DownloadZip = "Download ZIP";
        public const string SendViaApi = "Send via API";
        public const string EvaApiNotEnabled =
            "EVA API submission is not enabled for this principal.";
        // end CASE-040

        // ENG-034: the Engineer sections moved from the retired Assessment
        // page. Keep this block together so the parallel Case lanes can merge
        // their own vocabulary without interleaving it.
        public static class EngineerSections
        {
            public const string Damage = "Damage";
            public const string ImpactLocation = "Impact location";
            public const string ImpactSeverity = "Impact severity";
            public const string IncidentNarrative = "Incident narrative";
            public const string Estimate = "Estimate";
            public const string Estimates = "Estimates";
            public const string NoEstimatesRecorded = "No estimates recorded";
            public const string NewEstimate = "New estimate";
            public const string Current = "Current";
            public const string Recorded = "recorded";
            public const string DeleteEstimate = "Delete estimate";
            public const string Duplicate = "Duplicate";
            public const string UseEstimate = "Use estimate";
            public const string SaveEstimate = "Save estimate";
            public const string AddLine = "Add line";
            public const string EstimateName = "Estimate name";
            public const string Source = "Source";
            public const string RepairDays = "Repair days";
            public const string LabourRate = "Labour rate";
            public const string LabourRatePerHour = "Labour rate (\u00a3/h)";
            public const string PaintLabourRate = "Paint labour rate";
            public const string PaintLabourRatePerHour = "Paint labour rate (\u00a3/h)";
            public const string PaintMaterials = "Paint materials";
            public const string PaintMaterialsPounds = "Paint materials (\u00a3)";
            public const string OtherCosts = "Other costs";
            public const string OtherCostsPounds = "Other costs (\u00a3)";
            public const string Vat = "VAT";
            public const string VatPercent = "VAT %";
            public const string EstimateNotes = "Estimate notes";
            public const string PartsAndOperations = "Parts and operations";
            public const string Operation = "Operation";
            public const string Description = "Description";
            public const string PartNumber = "Part number";
            public const string Quantity = "Quantity";
            public const string QuantityShort = "Qty";
            public const string LabourHours = "Labour hours";
            public const string LabourHoursShort = "Labour h";
            public const string PaintHours = "Paint hours";
            public const string PaintHoursShort = "Paint h";
            public const string PartAmount = "Part amount";
            public const string PartPounds = "Part \u00a3";
            public const string Action = "Action";
            public const string Notes = "Notes";
            public const string NoneRecorded = "None recorded";
            public const string ImportEstimate = "Import estimate";
            public const string AudatexPdf = "Audatex PDF";
            public const string JsonEstimate = "JSON estimate";
            public const string Other = "Other";
            public const string EstimateDropzone = "Drag an estimate here, or choose it";
            public const string ChooseFile = "Choose a file";
            public const string Reason = "Reason";
            public const string Cancel = "Cancel";
            public const string CloseDialog = "Close dialog";
            public const string Replace = "Replace";
            public const string Repair = "Repair";
            public const string RemoveAndRefit = "R&I";
            public const string PaintOperation = "Paint";

            public const string Settlement = "Settlement";
            public const string Outcome = "Outcome";
            public const string SalvageCategory = "Salvage category";
            public const string SalvageValue = "Salvage value";
            public const string RecoveryCharge = "Recovery charge";
            public const string StorageCharge = "Storage charge";
            public const string RepairerVatRegistered = "Repairer VAT registered";

            public const string Report = "Report";
            public const string EngineersComments = "Engineer's comments";
            public const string HistoryCheck = "History check";
            public const string AgreedFee = "Agreed fee";
            public const string FeeDescription = "Fee description";
            public const string StatementOfTruth = "Statement of truth";
            public const string GenerateReportDraft = "Generate report draft";
            public const string PreviewReportDraft = "Preview report draft";
            public const string ReportDraftNotReady = "Report draft not ready";

            public const string SendToClaude = "Send to Claude";
            public const string Direction = "Direction";
            public const string TargetEstimate = "Target Estimate";
            public const string CaseValuation = "Case Valuation";
            public const string TargetAmount = "Target amount";

            public const string Parts = "Parts";
            public const string Labour = "Labour";
            public const string Paint = "Paint";
            public const string Subtotal = "Subtotal";
            public const string Total = "Total";
            public const string Line = "Line";
            public const string Type = "Type";
            public const string Code = "Code";
            public const string WorkUnits = "Work units";
            public const string Price = "Price";
            public const string Betterment = "Betterment";
            public const string ToBeConfirmed = "To be confirmed";

            public const string ReadOnlyOnceComplete = "Read-only once Complete";
            public const string EngineerOnlyImport = "Only an Engineer can import an estimate";
            public const string SendingToAiDisabled = "Sending to AI is disabled by an Administrator";
            public const string ConfirmedEngineerValueRequired = "A confirmed Engineer's Value is required";
            public const string NotAvailableForCase = "Not available for this case";
            public const string NotReady = "Not ready";

            public static string LineField(string label, int line) => $"{label}, line {line}";

            public static string RemoveLine(int line) => $"Remove line {line}";

            public static string DeleteEstimatePrompt(string name) =>
                $"Delete {name} and its lines from this case?";

            public static string SpecificationLinesCaption(string kind) =>
                $"The {kind} specification's ordered lines, exactly as recorded.";
        }
        // ENG-034 end.
    }

    /// The Upload surfaces' own words (EPIC-011 §1.10) — one list. The
    /// accepted-files line is built from <see cref="IntakeEnvelopeLimits"/>
    /// rather than transcribed from the prototype, whose "25 MB each · 10
    /// files" is fixture data and not this product's limits.
    /// </summary>
    public static class Upload
    {
        public const string Dropzone = "Drag files here or choose files";
        public const string Choose = "Choose files";
        public const string Submit = "Upload";
        public const string Clear = "Clear";
        public const string Another = "Upload another file";
        public const string Refresh = "Refresh";

        /// <summary>The public request page's single-file wording.</summary>
        public const string RequestEyebrow = "Secure file request";
        public const string RequestTitle = "Upload a file";
        public const string RequestDropzone = "Drag a file here or choose one";
        public const string RequestChoose = "Choose file";
        public const string RequestSubmit = "Submit file";

        /// <summary>The request's own size limit, which is set per request.</summary>
        public static string RequestLimit(string maximumFileSize) =>
            string.Create(CultureInfo.InvariantCulture, $"Up to {maximumFileSize}.");

        /// <summary>The accepted types and the real envelope limits, as drawn.</summary>
        public static string AcceptedFiles(long maximumFileBytes, int maximumFileCount) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"EML, MSG, PDF, DOC, DOCX, JPG or PNG · up to {FileSize(maximumFileBytes)} each · {maximumFileCount} files");
    }
}
