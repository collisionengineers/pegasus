using System.Globalization;
using System.Text;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;

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
/// distinct meanings of Audit, Triage, Needs sorting and Blocked are reserved.
/// Everything else falls through to <see cref="Humanise"/>, which turns an
/// unknown code into a readable sentence rather than printing it verbatim —
/// event codes in particular are composed at several call sites, so a fixed map
/// would silently go stale.
/// </remarks>
public static class OperatorLabels
{
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

    public static string CustodyState(DocumentCustodyStatus status) => status switch
    {
        DocumentCustodyStatus.Pending => "Storing",
        DocumentCustodyStatus.Confirmed => "Stored",
        DocumentCustodyStatus.Failed => "Storage failed",
        _ => Humanise(status.ToString())
    };

    public static string UploadLinkState(BoxFileRequestStatus status) => status switch
    {
        BoxFileRequestStatus.Pending => "Being created",
        BoxFileRequestStatus.Active => "Active",
        BoxFileRequestStatus.Unavailable => "Unavailable",
        BoxFileRequestStatus.Deactivated => "Withdrawn",
        BoxFileRequestStatus.Failed => "Failed",
        BoxFileRequestStatus.Unknown => "Unknown",
        _ => Humanise(status.ToString())
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
    /// Every other date surface in the product renders Europe/London. A column
    /// headed "(UTC)" and a `ToLocalTime()` against the server clock were the
    /// two places that did not, so the same instant read differently depending
    /// on which screen you were on.
    /// </remarks>
    public static string OfficeTime(DateTimeOffset value)
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

        return TimeZoneInfo.ConvertTime(value, office)
            .ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
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
}
