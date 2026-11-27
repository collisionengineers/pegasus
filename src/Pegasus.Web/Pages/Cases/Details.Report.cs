using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Web.Presentation;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.AiWork;
using Pegasus.Core.Actors;
using Pegasus.Core.Address;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Glass;
using Pegasus.Infrastructure.Email;
using EstimateVatLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateVat;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;
using FrameLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame;
using Labels = Pegasus.Web.Presentation.OperatorLabels;
using EditorLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Settlement and Report sections' read helpers (v26 § Settlement,
/// § Report): how an assessment value prints, the figures strip, and the
/// report's title and content line.
/// Values only — every rule they read belongs to the vocabulary or the
/// report projection.
/// </summary>
public sealed partial class DetailsModel
{
    private static readonly CultureInfo Pounds = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>
    /// The recorded value a field's box and its control both show, reading and
    /// editing alike (operator, 23 September 2026): the value as recorded,
    /// with its source tag. A recorded value is the Case's value whoever
    /// recorded it (operator, 25 September 2026).
    /// </summary>
    public AssessmentFieldValue? ShownAssessment(string path) => Assessment?.Field(path);

    /// <summary>Whether the value shown at <paramref name="path"/> is absent.</summary>
    public bool AssessmentIsAbsent(string path) =>
        string.IsNullOrWhiteSpace(ShownAssessment(path)?.Value);

    /// <summary>
    /// The shown value as an operator reads it: money as pounds, a flag as
    /// Yes/No, a date as "14 Jul 2026", a code as words, else the text; the
    /// record's absent word when nothing is held.
    /// </summary>
    public string AssessmentDisplay(string path) =>
        DisplayAssessmentValue(path, ShownAssessment(path)?.Value);

    /// <summary>The source tag of the value shown at <paramref name="path"/>; none for a staff value.</summary>
    public OperatorLabels.SourceTagWord? AssessmentSourceTag(string path) =>
        OperatorLabels.SourceTag(ShownAssessment(path));

    /// <summary>
    /// The section of this Case a report blocker links to: the one that clears
    /// it when this Case shows it (Original report is a standalone Audit's
    /// only); null otherwise.
    /// </summary>
    public string? BlockerSectionKey(AssessmentReadinessItem item) =>
        CaseWorkspaceLabels.Report.BlockerSection(item) is { } key && (key != "original-report" || IsAuditCase)
            ? key
            : null;

    /// <summary>
    /// The control a repairer VAT blocker opens edit mode on (issue 898):
    /// the Repair Spec section is claimed on the Current spec and the control
    /// that clears the blocker takes focus. Null when the blocker names no
    /// control, or this viewer cannot open the Repair Spec for editing now,
    /// where the blocker jumps to the section instead. The claim is offered
    /// outside an edit session, so the section is not yet editable: the
    /// assessment access the section would edit under once claimed decides.
    /// </summary>
    public string? BlockerEditFocus(AssessmentReadinessItem? item) =>
        item is not null
        && CurrentSpecification is not null
        && SectionOffersEdit("estimate")
        && AssessmentCanOpen
        && !AssessmentIsReadOnly
            ? CaseWorkspaceLabels.Report.BlockerFocus(item)
            : null;

    /// <summary>
    /// Whether a blocker sends this viewer to Accounts: the Sign-off
    /// Engineer's name and signature are set there, by an Administrator
    /// (operator, 26 September 2026). For anyone else it links nowhere.
    /// Choosing the Sign-off Engineer is Case details' instead.
    /// </summary>
    public bool BlockerOpensAccounts(AssessmentReadinessItem item) =>
        item.Requirement == CaseReportReadiness.SignatoryRequirement
        && item != CaseReportReadiness.SignOffEngineerNotChosen
        && User.IsInRole(StaffRoleNames.Administrator);

    /// <summary>The same reading over any raw value the vocabulary defines at <paramref name="path"/>.</summary>
    public static string DisplayAssessmentValue(string path, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return OperatorLabels.CaseWorkspace.AbsentValue;
        }

        var definition = AssessmentVocabulary.Definitions.GetValueOrDefault(path);
        return definition?.Type switch
        {
            AssessmentFieldType.Money => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var money)
                ? FormatPounds(money)
                : raw,
            AssessmentFieldType.Flag => string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No",
            AssessmentFieldType.Date => DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                : raw,
            AssessmentFieldType.Enumerated => CodeWords(raw),
            _ => raw
        };
    }

    /// <summary>A vocabulary code as words: "total_loss" reads "Total loss"; "N/A" stays.</summary>
    public static string CodeWords(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.Contains('_') && code.Length <= 3)
        {
            return code;
        }
        var spaced = code.Replace('_', ' ');
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    public static string FormatPounds(decimal value) => "£" + value.ToString("N2", Pounds);

    /// <summary>The agreed fee as a figure, if one is recorded.</summary>
    public decimal? AgreedFeeFigure =>
        decimal.TryParse(Assessment?.Field(AssessmentVocabulary.AgreedFee)?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var fee)
            ? fee
            : null;

    /// <summary>
    /// The fee note the Fee tab reads (v28 P24): the agreed fee, the VAT the
    /// report charges on it and their total. The rate is the report
    /// contract's own, so the tab and the printed fee note agree.
    /// </summary>
    public (decimal Net, decimal Vat, decimal Total)? FeeNote
    {
        get
        {
            if (AgreedFeeFigure is not { } net)
            {
                return null;
            }
            var vat = decimal.Round(net * AssessmentReportContract.FeeVatRate, 2, MidpointRounding.AwayFromZero);
            return (net, vat, net + vat);
        }
    }

    /// <summary>
    /// The name the report would be attached under
    /// (v28 P23), read from the same policy and the same generation the send
    /// names it with, so the form states what pressing Send report will send.
    /// </summary>
    public string ReportDeliveryFileName => (CurrentReportGeneration is { } generation
        ? CaseReportDeliveryNaming.ReportName(
            generation.Snapshot,
            ReportSendHistory.SentCount,
            // The Principal's own name for this report, when it sets one (R740).
            DeliveryRecipientSuggestions?.ReportNames ?? PrincipalReportNames.None,
            DeliveryRecipientSuggestions?.CaseType,
            generation.WorkKind)
        : CaseReportDeliveryNaming.ReportName(
            Case?.Summary.Reference ?? "—",
            Case?.Summary.Registration,
            RecordedOutcome is { } outcome ? CodeWords(outcome) : null,
            ReportSendHistory.SentCount)) + CaseReportDeliveryNaming.Extension;

    /// <summary>
    /// The other work's current report, read once the Case has its Audit: the
    /// send form of either view offers it beside the viewed work's report, so
    /// one email can carry both (operator, 12 October 2026).
    /// </summary>
    public CaseReportGenerationRecord? OtherReportGeneration { get; private set; }

    /// <summary>The other work's sends, which name its report as the send does.</summary>
    public CaseReportSendHistory OtherReportSendHistory { get; private set; } = CaseReportSendHistory.None;

    /// <summary>
    /// Whether the other work's report can go in the same send, by the rule
    /// the viewed report's own send follows: a confirmed generation with its
    /// confirmed report.
    /// </summary>
    public bool OtherReportSendable =>
        OtherReportGeneration is { State: CaseReportGenerationState.Confirmed } other
        && other.Artifacts.Any(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport
            && artifact.Status == CaseReportArtifactStatus.Confirmed);

    /// <summary>The name the other work's report would be attached under, as the send names it.</summary>
    public string? OtherReportDeliveryFileName => OtherReportGeneration is { } other
        ? CaseReportDeliveryNaming.ReportName(
            other.Snapshot,
            OtherReportSendHistory.SentCount,
            DeliveryRecipientSuggestions?.ReportNames ?? PrincipalReportNames.None,
            DeliveryRecipientSuggestions?.CaseType,
            other.WorkKind) + CaseReportDeliveryNaming.Extension
        : null;

    /// <summary>
    /// Whether the viewed work's report can be sent: a confirmed generation
    /// with its confirmed report. A delivery may attach a subset of the
    /// generation's documents (v28 P22), so this needs no other artifact.
    /// </summary>
    public bool ReportSendable =>
        CurrentReportGeneration is { State: CaseReportGenerationState.Confirmed } generation
        && generation.Artifacts.Any(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport
            && artifact.Status == CaseReportArtifactStatus.Confirmed);

    /// <summary>
    /// Whether this page offers Send report: the Report section is in the
    /// viewer's edit session and the report is sendable (v48 items A and H;
    /// operator, 4 November 2026). The section head's Send report, the aside's
    /// Send report step and the dialog itself all read this one answer.
    /// </summary>
    public bool CanSendReport => SectionIsEditable("report") && ReportSendable;

    /// <summary>
    /// The subject the Send report dialog's Subject field starts on (v48 item
    /// C; operator, 4 November 2026): the one the send derives, read from the
    /// same policy, so staff edit what would otherwise be sent. Empty while
    /// the dialog is not offered.
    /// </summary>
    public string ReportDeliverySubject { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this response read what the Send report dialog draws (the
    /// Principal's plan, the senders, the subject and the message): the page
    /// and the section responses do; a commit's answer does not, and the
    /// script keeps the dialog it has across that swap.
    /// </summary>
    public bool ReportDeliveryRead { get; private set; }

    /// <summary>
    /// The Original report card's figures (v48 items I–L; operator,
    /// 4 November 2026): the Inspection report as generated, read from the
    /// newest Inspection generation's frozen snapshot on the Audit view of an
    /// Inspection + Audit Case; null where the card is not drawn or no
    /// Inspection report exists yet.
    /// </summary>
    public OriginalReportFigures? OriginalReport { get; private set; }

    /// <summary>Whether the aside draws the Original report card: the Audit view once the Audit exists.</summary>
    public bool DrawsOriginalReport => !IsInspectionView
        && Works is { HasAudit: true }
        // A Garage authorisation and invoice Audit has no original report (R1138).
        && CurrentSummary?.AuditWorkflow != AuditWorkflowKind.GarageAuthorisationAndInvoice;

    private void ApplyReportDeliverySubject()
    {
        ReportDeliveryRead = true;
        ReportDeliverySubject = CanSendReport && DeliveryRecipientSuggestions is { } facts
            ? CaseReportDeliveryPolicy.DerivedSubject(ReportSenders.ReplyTo, facts)
            : string.Empty;
    }

    /// <summary>
    /// The card's figures from the Inspection report's snapshot, with the
    /// guide the Engineer's Value came from, as the snapshot froze it (R666,
    /// R898). A typed value names no calculation row and gets no guide. A
    /// snapshot frozen before the guide was kept names it only when it was
    /// taken from a single guide; otherwise the brackets are left off. No
    /// read beyond the generation the page already holds.
    /// </summary>
    private OriginalReportFigures? ReadOriginalReport()
    {
        if (!DrawsOriginalReport || OtherReportGeneration is not { } inspection)
        {
            return null;
        }
        var snapshot = inspection.Snapshot;
        var source = snapshot.AppliedValuationId is null
            ? null
            : snapshot.EngineerValueGuide
                ?? (snapshot.Guides.ValuationGuides.Count == 1 ? snapshot.Guides.ValuationGuides[0] : null);
        var guide = source is { } named ? CaseWorkspaceLabels.Valuation.SourceLabel(named) : null;
        var totalLoss = snapshot.Report.Outcome == AssessmentReportOutcome.TotalLoss;
        return new(
            snapshot.Costs.Printed.Gross,
            totalLoss ? snapshot.Report.SalvageValue : null,
            snapshot.EngineerValue,
            guide,
            // The settlement the Total loss report printed: pre-accident value
            // less salvage (operator, 6 November 2026; R730).
            totalLoss && snapshot.Report.SalvageValue is not null
                ? snapshot.Report.Presentation().RecommendedSettlement
                : null);
    }

    /// <summary>
    /// The Case report delivery template rendered for this Case, for staff to
    /// review and edit before Send report (FRD-11): what the form's
    /// message box is pre-filled with. Empty while the form is not offered.
    /// What staff submit is sent, not this text.
    /// </summary>
    public string ReportDeliveryMessage { get; private set; } = string.Empty;

    private async Task<string> RenderReportDeliveryMessageAsync(
        ActionActor actor, CancellationToken cancellationToken)
    {
        if (CurrentReportGeneration is not { State: CaseReportGenerationState.Confirmed } generation
            || !SectionIsEditable("report")
            || !StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework))
        {
            return string.Empty;
        }

        var facts = new CaseReportDeliveryFacts(
            generation.Snapshot.CaseReference,
            Case?.Summary.Registration,
            RecordedOutcome is { } outcome ? CodeWords(outcome) : null,
            DeliveryRecipientSuggestions?.PrincipalName,
            ReportSendHistory,
            DeliveryDispatchPlan?.Greeting);
        return await renderEmailTemplate.ExecuteAsync(
            actor,
            EmailTemplatePurpose.CaseReportDelivery,
            facts.Values(),
            cancellationToken);
    }

    /// <summary>The recorded outcome code, else null.</summary>
    public string? RecordedOutcome => Assessment?.Field(AssessmentVocabulary.Outcome)?.Value;

    public bool IsTotalLoss => string.Equals(RecordedOutcome, "total_loss", StringComparison.Ordinal);

    public bool IsUnroadworthy => string.Equals(
        Assessment?.Field(AssessmentVocabulary.LegalStatus)?.Value, "unroadworthy", StringComparison.Ordinal);

    /// <summary>The recorded salvage value as a figure, if one is held.</summary>
    public decimal? SalvageValueFigure =>
        decimal.TryParse(Assessment?.Field(AssessmentVocabulary.SalvageValue)?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>
    /// The Current estimate's labour hours (panel and paint), for the figures
    /// strip: the figure the report prints as Labour Hours.
    /// </summary>
    public decimal? LabourHours => CurrentSpecification is { } estimate
        ? ReportRepairCosts.For(estimate).TotalLabourHours
        : null;

    /// <summary>The report's title as the preview card and the draft file name it: "Total Loss Report — MA59BDY".</summary>
    public string ReportTitle
    {
        get
        {
            var outcome = RecordedOutcome;
            var kind = outcome is null
                ? "Assessment"
                : string.Equals(outcome, "total_loss", StringComparison.Ordinal)
                    ? "Total Loss"
                    : CodeWords(outcome);
            return TitleOf(kind, Case?.Summary.Registration);
        }
    }

    private static string TitleOf(string kind, string? registration) =>
        string.IsNullOrWhiteSpace(registration) ? $"{kind} Report" : $"{kind} Report — {registration}";

    public AssessmentReportDraftPreparation? ReportDraftPreparation
    {
        get => reportDraftPreparation;
        private set
        {
            reportDraftPreparation = value;
            reportDraftReasons = null;
        }
    }

    private AssessmentReportDraftPreparation? reportDraftPreparation;

    private IReadOnlyList<AssessmentReadinessItem>? reportDraftReasons;

    /// <summary>
    /// The report's narrative blocks as the Report section offers them (v28
    /// P30), the ones taken off the report among them. Empty while the Case
    /// cannot yet be projected into a report: the readiness rail already
    /// states what is outstanding.
    /// </summary>
    public IReadOnlyList<ReportWordingBlock> ReportWording { get; private set; } = [];

    /// <summary>The addresses the delivery form offers beside its fields (v28 P21).</summary>
    public IReadOnlyList<ReportRecipientCandidate> ReportAddressBook { get; private set; } = [];

    /// <summary>
    /// Who the report leaves as: the Outgoing mailboxes staff choose from,
    /// starting on the default sender (R655).
    /// </summary>
    public CaseMailSenderOptions ReportSenders { get; private set; } = CaseMailSenderOptions.None;

    /// <summary>What the Case's report has already been sent (v28 P23).</summary>
    public CaseReportSendHistory ReportSendHistory { get; private set; } = CaseReportSendHistory.None;

    /// <summary>
    /// What each offered block says when nobody has written their own, so
    /// Recompose puts the composed sentence back without a round trip.
    /// </summary>
    public IReadOnlyDictionary<string, string> ReportWordingComposed { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The heading each offered block carries when the Engineer has not
    /// renamed it. With <see cref="ReportWordingComposed"/> it lets the page
    /// send a block it still shows as drawn as no change, so a section kept
    /// on screen while the Case moved on stores nothing the Engineer did not
    /// write (issue 2042).
    /// </summary>
    public IReadOnlyDictionary<string, string> ReportWordingHeadings { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The accepted statement of truth in its printed parts, from Core, shown
    /// read-only as Report wording's last block in every state the workspace
    /// loads (v45 D, I). Null only when the report inputs cannot be read.
    /// </summary>
    public ReportStatementParts? StatementParts { get; private set; }

    public string? ReportDraftCondition { get; private set; }

    public bool ReportDraftNotReady => ReportDraftPreparation is { CanGenerate: false };

    /// <summary>
    /// The report blockers in the order the page shows what clears them,
    /// ordered once per preparation.
    /// </summary>
    public IReadOnlyList<AssessmentReadinessItem> ReportDraftReasons =>
        reportDraftReasons ??=
            Pegasus.Web.Presentation.CaseWorkspaceLabels.Report.InPageOrder(ReportDraftPreparation?.Reasons ?? []);

    /// <summary>
    /// The Case's current generated report snapshot (B05): the newest
    /// generation that no later material change has superseded, with every
    /// artifact it was asked for. Null until the first generation.
    /// </summary>
    public CaseReportGenerationRecord? CurrentReportGeneration { get; private set; }

    private const string OpenReportKey = "CaseOpenReport";

    /// <summary>
    /// The report Generate report has just stored, carried to the one page
    /// that follows so that page opens it in the viewer. Null on every later
    /// read of the Case.
    /// </summary>
    public Guid? OpenReportArtifactId => TempData[OpenReportKey] switch
    {
        // TempData materializes Guid-shaped strings as Guid values.
        Guid artifactId => artifactId,
        string text when Guid.TryParse(text, out var artifactId) => artifactId,
        _ => null,
    };

    /// <summary>The dispatch facts of the shown generation: the Principal's rules read with the Case.</summary>
    public ReportDispatchFacts? DeliveryRecipientSuggestions { get; private set; }

    /// <summary>
    /// The one plan the delivery form follows, made once when the page loads
    /// with no answers; Send report plans again with staff's answers.
    /// </summary>
    public ReportDispatchPlan? DeliveryDispatchPlan { get; private set; }

    public string GenerateReportOperationKey { get; private set; } = NewOperationKey();

    public string GenerateFeeNoteOperationKey { get; private set; } = NewOperationKey();

    public string GeneratePrincipalInvoiceOperationKey { get; private set; } = NewOperationKey();

    public string GenerateRepairSpecOperationKey { get; private set; } = NewOperationKey();

    public string GenerateImagePackOperationKey { get; private set; } = NewOperationKey();

    public string SendReportOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// The operation keys of what a report request goes on to make: its fee
    /// note, the Principal's own invoice (R1147) and the companions the
    /// Principal's rules require (R1262). A missing key makes none of that kind.
    /// </summary>
    private sealed record FollowOnKeys(
        string? FeeNote,
        string? PrincipalInvoice,
        string? ImagePack,
        string? RepairSpec)
    {
        public static FollowOnKeys None { get; } = new(null, null, null, null);
    }

    /// <summary>
    /// The report's wording blocks for a Case whose report can be projected:
    /// the snapshot the blocks compose from, and every block the section
    /// offers. A Case that cannot yet be projected has neither.
    /// </summary>
    private (AssessmentReportSnapshot? Snapshot, IReadOnlyList<ReportWordingBlock> Offered) WordingOf(
        AssessmentReportProjectionInput projection) =>
        Pegasus.Web.Presentation.ReportWordingEdits.Offered(projection, clock.GetUtcNow());

    /// <summary>
    /// The working preview, exactly as the report would be generated: the
    /// report followed by its fee note.
    /// </summary>
    public Task<IActionResult> OnGetPreviewReportDraftAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        PreviewReportDraftAsync(id, CaseReportArtifactKind.AssessmentReport, cancellationToken);

    /// <summary>
    /// The included images as they would print (v28 P42): the same working
    /// snapshot the report preview renders, as the image pack alone. Nothing
    /// is persisted; a Case with no image in the report has none to show.
    /// </summary>
    public Task<IActionResult> OnGetPreviewImagePackAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        PreviewReportDraftAsync(id, CaseReportArtifactKind.ImagePack, cancellationToken);

    private async Task<IActionResult> PreviewReportDraftAsync(
        Guid id,
        CaseReportArtifactKind kind,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        GenerateCaseAssessmentReportDraftResult result;
        try
        {
            result = await HttpContext.RequestServices
                .GetRequiredService<GenerateCaseAssessmentReportDraft>()
                .ExecuteAsync(id, actor, kind, cancellationToken);
        }
        catch (ReportRenderRejectedException exception)
        {
            LogCaseCommandFailed(logger, id, "preview_report", exception);
            // An image pack of a Case whose report uses no image is refused
            // rather than rendered empty.
            return Request.Headers.ContainsKey("X-Pegasus-Document-Preview")
                ? EnhancedPreviewRefusal(exception.Message)
                : RedirectToReport(id);
        }
        switch (result.Outcome)
        {
            case GenerateCaseAssessmentReportDraftOutcome.NotFound:
                return NotFound();
            case GenerateCaseAssessmentReportDraftOutcome.NotReady:
                var detail = "The report draft is not ready. " + string.Join(
                    " ",
                    result.Reasons.Select(reason =>
                        $"{reason.Requirement}: {reason.WhyOutstanding}"));
                if (Request.Headers.ContainsKey("X-Pegasus-Document-Preview"))
                {
                    return EnhancedPreviewRefusal(detail);
                }
                // The Report section lists each blocker with its section.
                return RedirectToReport(id);
            default:
                // DOCS-014: an inline preview of the unretained working
                // draft is a view, never a completed download — recorded
                // only once the draft actually rendered, at most once per
                // Case, artifact kind, staff member and day.
                await reportGenerations.RecordDraftPreviewedAsync(
                    new(actor, id, kind, DateTimeOffset.UtcNow),
                    cancellationToken);
                return File(result.Draft!.Pdf, "application/pdf");
        }
    }

    /// <summary>
    /// B05's immutable generation: freezes the accepted snapshot inside the
    /// store's short transaction and renders through the registered
    /// renderer, one artifact per request. The draft handlers above stay for
    /// the labelled ungenerated working preview; this is the real report.
    /// The report always ends with its fee note, and the same button makes
    /// the separate fee note from the same frozen snapshot once the report is
    /// confirmed (operator, 7 October 2026). A report still being filed to
    /// Box makes its fee note on the next Generate report. After the fee note
    /// it makes the Principal's own invoice for a Principal whose setting asks
    /// for one, the same way (R1147), and then each companion document the
    /// Principal's rules require, so Send report has them (R1262).
    /// </summary>
    public Task<IActionResult> OnPostGenerateReportAsync(
        Guid id,
        string operationKey,
        string feeNoteOperationKey,
        string? principalInvoiceOperationKey,
        string? imagePackOperationKey,
        string? repairSpecOperationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        CancellationToken cancellationToken) =>
        GenerateArtifactAsync(
            id, operationKey, editLeaseToken, expectedCaseVersion,
            CaseReportArtifactKind.AssessmentReport,
            targetGenerationId: null, cancellationToken,
            new(feeNoteOperationKey, principalInvoiceOperationKey, imagePackOperationKey, repairSpecOperationKey));

    /// <summary>
    /// The two companion documents a delivery may attach (v28 P22). Each is a
    /// separate artifact of the generation that is already confirmed, so it
    /// carries that generation's own facts: the Repair Spec is the estimate
    /// the generation pinned, the images are the ones its report prints.
    /// </summary>
    public Task<IActionResult> OnPostGenerateRepairSpecAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        Guid targetGenerationId,
        CancellationToken cancellationToken) =>
        GenerateArtifactAsync(
            id, operationKey, editLeaseToken, expectedCaseVersion,
            CaseReportArtifactKind.RepairSpecification,
            targetGenerationId, cancellationToken);

    public Task<IActionResult> OnPostGenerateImagePackAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        Guid targetGenerationId,
        CancellationToken cancellationToken) =>
        GenerateArtifactAsync(
            id, operationKey, editLeaseToken, expectedCaseVersion,
            CaseReportArtifactKind.ImagePack,
            targetGenerationId, cancellationToken);

    /// <summary>
    /// Every Generate, in or out of edit mode: the report (operator, 26
    /// September 2026) and its companion documents (issue 912, 28 September
    /// 2026). Without a lease the handler claims one for the generation.
    /// Posted from the Inspection view, it makes the Inspection report of a
    /// Case that has its Audit (operator, 1 October 2026). The report's
    /// separate fee note runs under the same lease.
    /// </summary>
    private async Task<IActionResult> GenerateArtifactAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        CaseReportArtifactKind kind,
        Guid? targetGenerationId,
        CancellationToken cancellationToken,
        FollowOnKeys? followOnKeys = null)
    {
        var keys = followOnKeys ?? FollowOnKeys.None;
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return await GenerateWithOneOffLeaseAsync(
                id, operationKey, expectedCaseVersion, kind,
                targetGenerationId, keys, cancellationToken);
        }
        var guard = await GuardReportCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        return await GenerateAsync(
            actor, id, operationKey, editLeaseToken, expectedCaseVersion,
            kind, targetGenerationId, keys, cancellationToken);
    }

    /// <summary>
    /// A Generate outside edit mode: the handler claims the Case's edit lease
    /// for this one generation and releases it after, the way Work Centre
    /// actions do. A colleague's live lease refuses the claim with the
    /// claim's own wording. A generation never consumes the lease, so the
    /// release runs whatever the outcome.
    /// </summary>
    private async Task<IActionResult> GenerateWithOneOffLeaseAsync(
        Guid id,
        string operationKey,
        long expectedCaseVersion,
        CaseReportArtifactKind kind,
        Guid? targetGenerationId,
        FollowOnKeys keys,
        CancellationToken cancellationToken)
    {
        var guard = await GuardSectionCommandAsync(
            id, operationKey, editLeaseToken: null, () => RedirectToReport(id), cancellationToken,
            requireLease: false);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        OneCommandEditClaim lease;
        try
        {
            lease = await OneCommandEdit.ClaimAsync(
                id, isTriageCase: false, expectedCaseVersion, actor, NewOperationKey(), cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseCommandFailed(logger, id, "claim_lease", exception);
            TempData["CaseError"] = ClaimLeaseFailureMessage(exception);
            return RedirectToReport(id);
        }

        return await OneCommandEdit.RunClaimedAsync(lease, actor, () => GenerateAsync(
            actor, id, operationKey, lease.Token, expectedCaseVersion,
            kind, targetGenerationId, keys, cancellationToken));
    }

    /// <summary>
    /// The generation itself, once guarded. The report goes on to make its
    /// separate fee note from the generation it confirmed (operator, 7 October
    /// 2026); a report not yet confirmed leaves the fee note to the next
    /// Generate report, which replays the report's operation key. The
    /// Principal's own invoice follows the fee note in the same way (R1147),
    /// and then each companion document the Principal's rules require (R1262).
    /// </summary>
    private async Task<IActionResult> GenerateAsync(
        ActionActor actor,
        Guid id,
        string operationKey,
        string editLeaseToken,
        long expectedCaseVersion,
        CaseReportArtifactKind kind,
        Guid? targetGenerationId,
        FollowOnKeys keys,
        CancellationToken cancellationToken)
    {
        // One custody batch around the report and its companions: the Case
        // folder is proved once for them all (FRD-05, #1952). Opened here, in the
        // flow that awaits both generations, so it reaches each filing.
        var generator = HttpContext.RequestServices.GetRequiredService<IGenerateCaseReport>();
        await using var batch = generator.BeginBatch();
        var request = CaseReportActs.RequestFor(
            actor, id, expectedCaseVersion, editLeaseToken, operationKey, kind, targetGenerationId, WorkSelector);
        var (result, outcome) = await GenerateOneAsync(generator, request, cancellationToken);
        if (outcome is not null)
        {
            return outcome;
        }
        var latest = result!;
        if (!string.IsNullOrWhiteSpace(keys.FeeNote)
            && CaseReportActs.FeeNoteRequestAfter(request, latest, keys.FeeNote) is { } feeNoteRequest)
        {
            // The report stands whatever the fee note's outcome; a fee note
            // that is not generated says so beside the report's confirmation.
            var (feeNoteResult, feeNoteOutcome) = await GenerateOneAsync(
                generator, feeNoteRequest, cancellationToken, followOn: true);
            if (feeNoteOutcome is ForbidResult or NotFoundResult)
            {
                return feeNoteOutcome;
            }
            latest = feeNoteResult ?? latest;
        }
        // The Principal's own invoice follows the fee note from the latest
        // generation, and stands or fails on its own as the fee note does (R1147).
        if (!string.IsNullOrWhiteSpace(keys.PrincipalInvoice)
            && CaseReportActs.PrincipalInvoiceRequestAfter(request, latest, keys.PrincipalInvoice) is { } invoiceRequest)
        {
            var (invoiceResult, invoiceOutcome) = await GenerateOneAsync(
                generator, invoiceRequest, cancellationToken, followOn: true);
            if (invoiceOutcome is ForbidResult or NotFoundResult)
            {
                return invoiceOutcome;
            }
            latest = invoiceResult ?? latest;
        }
        // Then each companion Send report would require and is not yet
        // generated, each standing or failing on its own (R1262). A form
        // without their keys makes none.
        if (keys.ImagePack is { Length: > 0 } imagePackKey && keys.RepairSpec is { Length: > 0 } repairSpecKey)
        {
            var reportActs = HttpContext.RequestServices.GetRequiredService<CaseReportActs>();
            var companions = await reportActs.RequiredCompanionRequestsAfterAsync(
                request,
                latest,
                companion => companion == CaseReportArtifactKind.ImagePack ? imagePackKey : repairSpecKey,
                cancellationToken);
            foreach (var companionRequest in companions)
            {
                var (_, companionOutcome) = await GenerateOneAsync(
                    generator, companionRequest, cancellationToken, followOn: true);
                if (companionOutcome is ForbidResult or NotFoundResult)
                {
                    return companionOutcome;
                }
            }
        }
        return RedirectToReport(id);
    }

    /// <summary>
    /// One artifact's generation: the request, and how each outcome reads
    /// back. A generated artifact answers its result and no action; every
    /// other outcome answers the action that ends the request. One that
    /// follows its report (<paramref name="followOn"/>) leaves the report's
    /// confirmation standing.
    /// </summary>
    private async Task<(CaseReportGenerationResult? Result, IActionResult? Outcome)> GenerateOneAsync(
        IGenerateCaseReport generator,
        GenerateCaseReportRequest request,
        CancellationToken cancellationToken,
        bool followOn = false)
    {
        var id = request.CaseId;
        var kind = request.Kind;
        CaseReportGenerationResult result;
        try
        {
            result = await generator.ExecuteAsync(request, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return (null, Forbid());
        }
        catch (Exception exception) when (IsGenerationFailure(exception))
        {
            LogCaseCommandFailed(logger, id, "generate_report", exception);
            TempData["CaseError"] = exception switch
            {
                // A render refusal says what stopped it, as the preview does.
                ReportRenderRejectedException => exception.Message,
                OperationCanceledException or TimeoutException =>
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.TookTooLong(kind),
                HttpRequestException or IOException =>
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.NotStoredInBox(kind),
                _ => MutationRefusalMessage(exception, NotGenerated(kind)),
            };
            return (null, RedirectToReport(id));
        }

        switch (result.Outcome)
        {
            case CaseReportGenerationOutcome.NotFound:
                return (result, NotFound());
            case CaseReportGenerationOutcome.NotReady:
                TempData["CaseError"] = string.Join(
                    " ",
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerationNotReady + ":",
                    string.Join("; ", result.Reasons.Select(reason =>
                        $"{reason.Requirement}: {reason.WhyOutstanding}")));
                return (result, RedirectToReport(id));
            case CaseReportGenerationOutcome.Pending:
                // Not yet stored is not success: the page says so in amber.
                TempData["CaseWarning"] = Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerationPending;
                return (result, RedirectToReport(id));
            case CaseReportGenerationOutcome.Failed:
                TempData["CaseError"] = NotGenerated(kind);
                return (result, RedirectToReport(id));
            default:
                ClearLeaseState();
                if (kind == CaseReportArtifactKind.AssessmentReport
                    && result.Generation?.Artifacts.FirstOrDefault(artifact => artifact is
                    {
                        Kind: CaseReportArtifactKind.AssessmentReport,
                        Filing: CaseReportArtifactFiling.Stored,
                    }) is { } stored)
                {
                    TempData[OpenReportKey] = stored.Id.ToString("D");
                }
                // The fee note, the Principal's own invoice and the companions
                // that follow their report leave its confirmation standing.
                if (!followOn && kind is not (CaseReportArtifactKind.FeeNote or CaseReportArtifactKind.PrincipalInvoice))
                {
                    TempData["CaseStatus"] = kind switch
                    {
                        CaseReportArtifactKind.AssessmentReport =>
                            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ReportGenerated,
                        CaseReportArtifactKind.RepairSpecification =>
                            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.RepairSpecGenerated,
                        _ => Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ImagesGenerated,
                    };
                }
                return (result, null);
        }
    }

    /// <summary>
    /// The faults a generation tells staff about: a refusal, a file that could
    /// not be read or stored, and a generation that ran out of time. A request
    /// the browser abandoned is none of them, and goes on as cancelled.
    /// </summary>
    private bool IsGenerationFailure(Exception exception) => exception switch
    {
        OperationCanceledException => !HttpContext.RequestAborted.IsCancellationRequested,
        ArgumentException
            or InvalidOperationException
            or IOException
            or InvalidDataException
            or TimeoutException
            or HttpRequestException
            or ReportRenderRejectedException => true,
        _ => false,
    };

    /// <summary>The document that was not generated, named as staff know it.</summary>
    private static string NotGenerated(CaseReportArtifactKind kind) => kind switch
    {
        CaseReportArtifactKind.AssessmentReport =>
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ReportNotGenerated,
        CaseReportArtifactKind.FeeNote =>
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.FeeNoteNotGenerated,
        CaseReportArtifactKind.RepairSpecification =>
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.RepairSpecNotGenerated,
        CaseReportArtifactKind.PrincipalInvoice =>
            Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.PrincipalInvoiceNotGenerated,
        _ => Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ImagesNotGenerated,
    };

    /// <summary>
    /// Reopens a confirmed artifact's immutable bytes — never a regeneration
    /// and never a Pending, Failed or Unknown artifact (the store refuses
    /// those; the page does not decide).
    /// </summary>
    public async Task<IActionResult> OnGetGeneratedArtifactAsync(
        Guid id,
        Guid generationId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await HasAssessmentAccessAsync(id, actor, cancellationToken))
        {
            return NotFound();
        }

        var content = await generatedArtifacts.OpenAsync(
            actor, id, generationId, artifactId, cancellationToken);
        // The file result owns and disposes the stream once the body is
        // written; disposing here would close it before MVC reads it.
        return File(content.Content, content.MediaType, content.FileName);
    }

    /// <summary>
    /// Sends the current generation's report in one step (operator, 6 October
    /// 2026): the staff-reviewed recipients, the documents chosen and the
    /// message submitted go to A's staff send transport under this form's
    /// operation key, so a repeated submission of one form replays one send.
    /// The send boundary re-checks the generation and the attachment hashes.
    /// A's returned state is mapped truthfully: only observation says sent,
    /// and an Unknown outcome never claims one.
    /// </summary>
    public async Task<IActionResult> OnPostSendReportAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        Guid generationId,
        long expectedGenerationVersion,
        string? coveringMessage,
        string[]? toRecipients,
        string[]? ccRecipients,
        CaseReportArtifactKind[]? attach,
        Guid? fromMailboxId,
        Guid? secondGenerationId,
        long? secondExpectedGenerationVersion,
        CaseReportArtifactKind[]? secondAttach,
        string? dispatchFingerprint,
        string? stopOverrideReason,
        string[]? holdsDone,
        string? subject,
        bool deleteCaseCorrespondence,
        Guid[]? offeredFile,
        CancellationToken cancellationToken)
    {
        var guard = await GuardReportCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            CaseReportDeliveryPolicy.CoveringMessage(coveringMessage);
        }
        catch (ArgumentException)
        {
            TempData["CaseError"] = Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.MessageRefused;
            return RedirectToReport(id, reopenSend: true);
        }

        StaffMailOperation operation;
        try
        {
            operation = await sendCaseReport.ExecuteAsync(
                new(
                    actor,
                    id,
                    expectedCaseVersion,
                    editLeaseToken!,
                    generationId,
                    expectedGenerationVersion,
                    operationKey,
                    coveringMessage!,
                    new(toRecipients ?? [], ccRecipients ?? []),
                    attach,
                    WorkSelector,
                    fromMailboxId,
                    // The other work's report, when its tick is on: always
                    // its report, with the documents of it ticked.
                    secondGenerationId is { } secondId && secondExpectedGenerationVersion is { } secondVersion
                        ? new(secondId, secondVersion,
                            [CaseReportArtifactKind.AssessmentReport, .. (secondAttach ?? [])],
                            PostedAttachmentNames("secondFileName"))
                        : null,
                    DispatchFingerprint: dispatchFingerprint,
                    AcknowledgedHolds: holdsDone ?? [],
                    StopOverrideReason: stopOverrideReason,
                    // The subject and the documents' names as staff left them
                    // in the composer (v48 items C and E).
                    Subject: subject,
                    AttachmentNames: PostedAttachmentNames("fileName"),
                    // The dialog's Delete case correspondence tick (R902 to R905).
                    TidiesCaseCorrespondence: deleteCaseCorrespondence,
                    // The offered Case files staff ticked (R1151), and the
                    // names staff gave the Case's files (R1175, R1176).
                    OfferedFileIds: offeredFile ?? [],
                    CaseFileNames: PostedCaseFileNames()),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (StaffMailOutcomeUnknownException exception)
        {
            // Recorded Unknown: shown as a returned Unknown is (R1069).
            Pegasus.Web.Pages.Mail.StaffMailSendLog.OutcomeUnknown(
                logger, exception.Operation.Id, exception.Operation.ApprovedMailboxId, exception);
            operation = exception.Operation;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception,
                "The report was not sent. Retry the operation.");
            return RedirectToReport(id, reopenSend: true);
        }

        ClearLeaseState();
        switch (operation.State)
        {
            case StaffMailState.Sent:
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendObservedSent;
                break;
            case StaffMailState.Submitted:
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendAccepted;
                break;
            case StaffMailState.Failed:
                TempData["CaseError"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendFailed;
                break;
            case StaffMailState.Unknown:
                TempData["CaseError"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendUnknown;
                break;
            case StaffMailState.Cancelled:
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendCancelled;
                break;
            default:
                // Prepared/DraftCreating/DraftReady/Sending: the transport
                // accepted work that is still in flight — neither success
                // nor failure is claimed.
                TempData["CaseStatus"] =
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.SendInProgress;
                break;
        }

        return RedirectToReport(id);
    }

    /// <summary>
    /// The Report-section mutation guard: the estimate guard's rules
    /// (writable case, valid form and live lease) with the Report
    /// section's redirect target. The command store compares the posted Case
    /// version with current persisted state.
    /// </summary>
    private Task<IActionResult?> GuardReportCommandAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        GuardSectionCommandAsync(
            id,
            operationKey,
            editLeaseToken,
            () => RedirectToReport(id),
            cancellationToken);

    /// <summary>
    /// Back to the Report section, in the view the request came from; a
    /// refused send reopens the Send report dialog on its notice (v48 item N).
    /// </summary>
    private RedirectToPageResult RedirectToReport(Guid id, bool reopenSend = false) =>
        RedirectToPage("/Cases/Details", new { id, section = "report", view = ReturnView, dialog = reopenSend ? "send-report" : null });

    /// <summary>
    /// The names staff gave the send's documents (v48 item E): one hidden
    /// <c>{prefix}-{kind}</c> field per Files row, empty while the row keeps
    /// its policy name. Null when no row was renamed.
    /// </summary>
    private Dictionary<CaseReportArtifactKind, string>? PostedAttachmentNames(string prefix)
    {
        Dictionary<CaseReportArtifactKind, string>? names = null;
        foreach (var key in Request.Form.Keys)
        {
            var parts = key.Split('-');
            if (parts.Length == 2
                && parts[0] == prefix
                && Enum.TryParse<CaseReportArtifactKind>(parts[1], ignoreCase: false, out var kind)
                && Request.Form[key].ToString() is { Length: > 0 } name
                && !string.IsNullOrWhiteSpace(name))
            {
                (names ??= [])[kind] = name;
            }
        }
        return names;
    }

    /// <summary>
    /// The names staff gave the Case's files in the composer, one
    /// <c>caseFileName-{occurrence}</c> field per filed estimate or offered
    /// file row (R1175, R1176); a row left on its filed name posts nothing.
    /// </summary>
    private Dictionary<Guid, string>? PostedCaseFileNames()
    {
        Dictionary<Guid, string>? names = null;
        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith(CaseFileNameField, StringComparison.Ordinal)
                && Guid.TryParseExact(key[CaseFileNameField.Length..], "N", out var occurrenceId)
                && Request.Form[key].ToString() is { } name
                && !string.IsNullOrWhiteSpace(name))
            {
                (names ??= [])[occurrenceId] = name;
            }
        }
        return names;
    }

    /// <summary>The prefix of a Case file row's name field in the Send report dialog.</summary>
    public const string CaseFileNameField = "caseFileName-";
}

/// <summary>
/// The Original report card (v48): the Inspection report's printed repair
/// cost, its salvage value when that report was a Total loss, the
/// Engineer's Value it printed with the guide its basis card came from, and
/// the settlement a Total loss report printed (R730).
/// </summary>
public sealed record OriginalReportFigures(
    decimal RepairCostIncVat,
    decimal? SalvageValue,
    decimal EngineerValue,
    string? Guide,
    decimal? Settlement);
