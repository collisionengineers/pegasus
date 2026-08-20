using System.Globalization;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
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
    /// The operator-meaningful handle for a received e-mail: its subject and
    /// sender, or "(No subject)" when the subject could not be read. The one
    /// formatting rule for both the Unidentified queue row
    /// (<c>Triage.IndexModel.UnidentifiedHandle</c>) and its detail page
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

    public static string CaseStage(CaseLifecycleState state) => state switch
    {
        CaseLifecycleState.NotReady => "Not ready",
        CaseLifecycleState.Held => "Held",
        CaseLifecycleState.Review => "Review",
        CaseLifecycleState.ReportPreparation => "Report preparation",
        CaseLifecycleState.PostReport => "Post report",
        CaseLifecycleState.PostReportComplete => "Post-report complete",
        CaseLifecycleState.ProviderCancelled => "Provider cancelled",
        CaseLifecycleState.CollisionEngineersRejected => "Collision Engineers rejected",
        CaseLifecycleState.CreatedInError => "Created in error",
        _ => Humanise(state.ToString())
    };

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
    /// The application work view a classified message belongs in, from the
    /// Core operational-destination policy.
    /// </summary>
    /// <remarks>
    /// The abstention case reuses the exact "Needs sorting" wording this page
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
        InOffice(value).ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

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
        InOffice(value).ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// The time of day in the office's zone, for the two-line surfaces that
    /// print the date above it.
    /// </summary>
    public static string OfficeClock(DateTimeOffset value) =>
        InOffice(value).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// The one conversion. It falls back to UTC rather than throwing, because
    /// a missing zone database is an operational fault and a blank screen
    /// would be a worse answer than an hour's offset.
    /// </summary>
    private static DateTimeOffset InOffice(DateTimeOffset value)
    {
        TimeZoneInfo office;
        try
        {
            office = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            office = TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.ConvertTime(value, office);
    }

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
    /// Where a value came from, as the one word the provenance icon announces
    /// and the approved Lucide glyph that carries it.
    /// </summary>
    /// <remarks>
    /// The sprite is a checksummed asset of sixteen glyphs and the design
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
            CaseDataSourceKind.CaseAcceptance => ("Automatic", "icon-refresh-cw"),
            _ => ("Unknown", "icon-info")
        };
    }
}
