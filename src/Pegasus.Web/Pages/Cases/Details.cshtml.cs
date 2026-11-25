using System.Globalization;
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
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Presentation;
using Pegasus.Infrastructure.Email;
using EstimateVatLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateVat;
using GlassLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.GlassSession;
using FrameLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Frame;
using Labels = Pegasus.Web.Presentation.OperatorLabels;
using EditorLabels = Pegasus.Web.Presentation.CaseWorkspaceLabels.Editors;

namespace Pegasus.Web.Pages.Cases;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[RequestSizeLimit(ImportRawEstimate.MaximumDocumentBytes + 64 * 1024)]
// Save as you go posts the whole Case form on every commit, and the Repair
// Spec grid binds nine inputs a line to it, so a spec at Core's bound is
// 1,800 values before the rest of the form. The framework's default of 1,024
// refused a 108-line spec as a bare 400 before any handler ran (#1994).
[RequestFormLimits(ValueCountLimit = CaseFormValueLimit)]
public sealed partial class DetailsModel(
    IGetCasePageFrame getCasePageFrame,
    IGetCaseSectionFrame getCaseSectionFrame,
    IGetCaseVehicleSection getCaseVehicleSection,
    IGetCaseValuationSection getCaseValuationSection,
    IGetCaseNotesSection getCaseNotesSection,
    IGetCaseTasksSection getCaseTasksSection,
    ICaseEngineerChoices caseEngineerChoices,
    IGetCaseFilesSection getCaseFilesSection,
    IGetCaseQueriesSection getCaseQueriesSection,
    ListCaseReferences listCaseReferences,
    IGetAssessmentAccess getAssessmentAccess,
    IGetAssessmentWorkspace getAssessmentWorkspace,
    ICaseReportSnapshotSource reportSnapshotSource,
    TimeProvider clock,
    ISendToAiControl sendToAiControl,
    IRenderCaseEstimateDocument renderEstimateDocument,
    IEstimateDocumentPresentationStore estimateDocumentPresentations,
    IGeneratedCaseArtifactStore generatedArtifacts,
    ICaseReportGenerationStore reportGenerations,
    IReportRecipientSuggestionQueries reportRecipientSuggestions,
    ResolveCaseMailSender caseMailSenders,
    ICaseReportSendHistoryQueries reportSendHistory,
    RenderEmailTemplate renderEmailTemplate,
    IListCaseEstimates listEstimates,
    LabourRateCardAdministration labourRateCards,
    IEnumerable<IEstimateDocumentParser> estimateParsers,
    IRepairSpecificationSnapshotStore specificationSnapshots,
    IUnroadworthyReasonBankStore unroadworthyReasonBank,
    IPrincipalSalvageMatrixQueries principalSalvageMatrices,
    ICaseAssetPreparationQueries caseAssetPreparationQueries,
    CaseEditLeases leases,
    CaseEditRequestWatch editRequestWatch,
    IHostApplicationLifetime hostLifetime,
    IInspectionAddressChoicesQueries inspectionAddressChoicesQueries,
    IContactDirectoryQueries contactDirectory,
    IImageIntakeQueries imageIntakeQueries,
    IReadImageTagVocabulary readImageTagVocabulary,
    IListCaseValuations listCaseValuations,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    IStaffAccountQueries staffAccountQueries,
    IPerUserExternalCredentialReader externalCredentials,
    IGlassRepairEstimateSessionReader glassSessions,
    GlassRepairEstimateAvailability glassAvailability,
    IAiJobQueries aiJobs,
    IFetchGuideValuation fetchGuideValuation,
    IListValuationPresets listValuationPresets,
    IListAppliedValuations listAppliedValuations,
    Pegasus.Core.Vehicle.RunVehicleHistoryCheck runVehicleHistoryCheck,
    ILogger<DetailsModel> logger,
    IGetCaseKind getCaseKind,
    IValidateCaseRenderLease? validateCaseRenderLease = null,
    IStaffMailSend? staffMailSend = null,
    Pegasus.Core.PhotoCapture.IPhotoCaptureQueries? photoCaptureQueries = null,
    IConfiguration? configuration = null) : CaseMutationPageModel(logger)
{
    /// <summary>Inputs the Repair Spec grid binds to the Case form for each line (<c>_CaseEstimate.cshtml</c>).</summary>
    internal const int EstimateLineInputs = 9;

    /// <summary>The Case form's inputs beside the Repair Spec lines, with room to spare.</summary>
    private const int CaseFormHeadroom = 2048;

    /// <summary>
    /// Form values one commit may carry: a Repair Spec at
    /// <see cref="AssessmentPolicy.MaximumEstimateLines"/> and the rest of the
    /// Case form. Derived from Core's bound, so the page never accepts fewer
    /// lines than Core does (#1994).
    /// </summary>
    internal const int CaseFormValueLimit = AssessmentPolicy.MaximumEstimateLines * EstimateLineInputs + CaseFormHeadroom;

    /// <summary>
    /// The commit this response confirms: the editor, its operation key and the
    /// versions it moved between, which the page script matches against what it
    /// sent. A commit answered in place holds it here; every other command
    /// carries it to the redirected page in TempData.
    /// </summary>
    public string? CommittedEditorCommand =>
        AnswersInPlace ? heldEditorCommit : TempData["CaseEditorCommit"] as string;

    private string? heldEditorCommit;

    /// <summary>
    /// The notices the record draws above its card: what the last command
    /// did, work not yet finished, and a refusal. A commit answered in place
    /// holds them (<see cref="CaseMutationPageModel.AnswersInPlace"/>); every
    /// other page reads them from the redirect's TempData.
    /// </summary>
    public string? StatusNotice => AnswersInPlace ? HeldNotice(StatusTempDataKey) : TempData[StatusTempDataKey] as string;

    public string? WarningNotice => AnswersInPlace ? HeldNotice(WarningTempDataKey) : TempData[WarningTempDataKey] as string;

    public string? ErrorNotice => AnswersInPlace ? HeldNotice(ErrorTempDataKey) : TempData[ErrorTempDataKey] as string;

    private const string WarningTempDataKey = "CaseWarning";

    private void RecordEditorCommit(
        string editor, string operationKey, long expectedVersion, long? resultingVersion = null)
    {
        // Use the operation's original authority version and its known result
        // version, not a later read which could include an intervening write.
        var commit = JsonSerializer.Serialize(new
        {
            editor, operationKey, expectedVersion, version = resultingVersion ?? checked(expectedVersion + 1)
        });
        if (AnswersInPlace)
        {
            heldEditorCommit = commit;
        }
        else
        {
            TempData["CaseEditorCommit"] = commit;
        }
    }

    public bool StaffMailAvailable => staffMailSend is not null
        && staffMailSend is not UnavailableStaffMailSend;

    /// <summary>
    /// "is-collapsed" when this browser folded the panel <paramref name="collapseKey"/>
    /// (its <c>data-collapse</c> value), so the first paint is already folded.
    /// </summary>
    public string? CollapsedClass(string collapseKey) =>
        Pegasus.Web.Presentation.ShellPreferences.PanelCollapsed(Request, collapseKey) ? "is-collapsed" : null;

    /// <summary>
    /// Every image occurrence's report preparation (B06), loaded with the
    /// Files section so its tile, viewer and Case Save share one state.
    /// </summary>
    public IReadOnlyList<CaseAssetPreparation> AssetPreparations { get; private set; } = [];

    /// <summary>
    /// Which section of the Case record the request addresses.
    /// </summary>
    /// <remarks>
    /// The record is one scrolling page (D29), so the section is a jump
    /// target rather than an alternative: it is rendered server-side on the
    /// first response and the jump-nav scrolls to it. The vocabulary is the
    /// eleven keys of <see cref="Labels.CaseWorkspace.Sections"/>; a value
    /// the record does not own selects Overview.
    /// </remarks>
    [BindProperty(SupportsGet = true, Name = "section")]
    public string? SectionFilter { get; set; }

    /// <summary>
    /// The page of each Notes section tab the address asks for (R1256). Text,
    /// so an address that is not a number reads page one rather than
    /// failing the page.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "notesPage")]
    public string? NotesPageFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "historyPage")]
    public string? HistoryPageFilter { get; set; }

    private static int TimelinePage(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var page)
            ? Math.Max(1, page)
            : 1;

    public string Section
    {
        get
        {
            var section = NormalizeSection(SectionFilter);
            return section == "original-report"
                && Case is not null
                && !IsAuditCase
                ? Labels.CaseWorkspace.DefaultSectionKey
                : section;
        }
    }

    private static string NormalizeSection(string? value)
    {
        var key = value?.Trim().ToLowerInvariant();
        return Labels.CaseWorkspace.Sections.Any(section =>
            string.Equals(section.Key, key, StringComparison.Ordinal))
            ? key!
            : Labels.CaseWorkspace.DefaultSectionKey;
    }

    /// <summary>
    /// The sections whose body is fetched as a fragment when it approaches the
    /// viewport, and the view that renders each. Only sections that have a
    /// body below the fold are here: the first three are always rendered, and
    /// a section whose body its owning lane has not built yet is a heading the
    /// frame renders itself.
    /// </summary>
    private static readonly Dictionary<string, string> LazySectionViews =
        new(StringComparer.Ordinal)
        {
            ["vehicle"] = "/Pages/Cases/Shared/_CaseVehicle.cshtml",
            ["valuation"] = "/Pages/Cases/Shared/_CaseValuation.cshtml",
            ["files"] = "/Pages/Cases/Shared/_CaseFiles.cshtml",
            ["notes"] = "/Pages/Cases/Shared/_CaseHistory.cshtml",
            ["queries"] = "/Pages/Cases/Shared/_CaseQueries.cshtml"
        };

    /// <summary>The Tasks dialog, whose body the fragment route also serves.</summary>
    private const string TasksView = "/Pages/Cases/Shared/_CaseTasks.cshtml";

    /// <summary>The Repair Spec section and the dialogs drawn after it, which the fragment route also serves.</summary>
    private const string EstimateView = "/Pages/Cases/Shared/_CaseEstimate.cshtml";

    /// <summary>
    /// Whether <paramref name="key"/> is fetched rather than rendered with the
    /// first response. The addressed section is always rendered, so
    /// <c>?section=</c> works over plain HTTP. Files, Notes and Queries
    /// have no fields in the record's single Save form, so they remain deferred
    /// while editing without replacing entered values elsewhere; their bodies
    /// act through their own posts with the render lease token.
    /// </summary>
    public bool SectionIsDeferred(string key) =>
        !string.Equals(key, Section, StringComparison.Ordinal)
        // A nested section's parent renders with it, so the reader lands on both.
        && !string.Equals(key, SectionLinkKey, StringComparison.Ordinal)
        && !(string.Equals(Section, "vehicle", StringComparison.Ordinal) && IsNestedSection(key))
        && LazySectionViews.ContainsKey(key)
        && (LeaseToken is null || key is "files" or "notes" or "queries");

    /// <summary>
    /// A lease token supplied only for rendering an asynchronously mounted
    /// section. It is never persisted or treated as authority; each POST still
    /// verifies its token, actor and live lease with Core.
    /// </summary>
    public string? RenderLeaseToken => LeaseToken ?? fragmentLeaseToken;

    private string? fragmentLeaseToken;

    /// <summary>
    /// The assigned Engineer's operator-facing name, resolved through the one
    /// staff-account query; null while no Engineer is assigned.
    /// </summary>
    public string? EngineerDisplayName { get; private set; }

    public string SignOffEngineerDisplayName { get; private set; } = Labels.CaseWorkspace.Unassigned;

    public AssignEngineerViewModel? AssignEngineerChoices { get; private set; }

    /// <summary>The operation key the Actions menu's Export case posts.</summary>
    public string ExportOperationKey { get; } = NewOperationKey();

    /// <summary>
    /// The requirements the current configured policy reports as unmet,
    /// accompanied by the Case's due-work reason where available, each with
    /// the section that clears it: the original report is marked on Files
    /// (operator, 8 October 2026); the rest arrive with the Case's details.
    /// </summary>
    public IReadOnlyList<CaseRequirement> OutstandingRequirements
    {
        get
        {
            if (Case?.Data is not { } data)
            {
                return [];
            }

            var why = Case.Workflow.DueWork is { } dueWork
                ? Pegasus.Web.Presentation.OperatorLabels.ChaseReason(dueWork.MissingMaterialReason)
                : null;
            var requirements = new List<CaseRequirement>();
            // A failed document storage holds the Case Not ready whatever else
            // it has, so it is the first requirement and Files clears it (R929).
            if (Case.Frame.CustodyState == CaseCustodyState.Failed)
            {
                requirements.Add(new(
                    Labels.DocumentStorageFailed,
                    Labels.CaseWorkspace.Sections.First(section => section.Key == "files").Label,
                    null,
                    "files"));
            }
            if (OriginalReportMissing)
            {
                requirements.Add(new(Labels.OriginalReportMissing, "Audit", null, "files"));
            }
            // The instruction row names the items still missing, by their field
            // labels, in place of the chase reason (FRD-13; operator, 2 November 2026).
            var missingInstructionItems = data.Completeness.MissingInstructionItems;
            // No registration (R1081) or no mileage (R759) is cleared on the
            // Vehicle section, where the Registration and Mileage boxes are;
            // a Garage authorisation and invoice Audit's External estimate
            // and Repair invoice on Files, where they are filed (R1141,
            // R1144); Instructions and Images on the overview.
            requirements.AddRange(data.Completeness.Evaluation.MissingRequirements
                .Select(requirement => new CaseRequirement(
                    Pegasus.Web.Presentation.OperatorLabels.RequirementIncomplete(requirement),
                    "Case requirements",
                    requirement == "Instructions" && missingInstructionItems.Count > 0
                        ? string.Join(", ", missingInstructionItems)
                        : why,
                    requirement switch
                    {
                        Pegasus.Core.Workflow.CaseWorkflowConfiguration.VehicleRegistrationRequirement
                            or Pegasus.Core.Workflow.CaseWorkflowConfiguration.VehicleMileageRequirement => "vehicle",
                        Pegasus.Core.Workflow.CaseWorkflowConfiguration.ExternalEstimateRequirement
                            or Pegasus.Core.Workflow.CaseWorkflowConfiguration.RepairInvoiceRequirement => "files",
                        _ => "overview"
                    })));
            return requirements;
        }
    }

    public bool OriginalReportMissing =>
        CurrentSummary is { CaseType: var caseType, AuditWorkflow: var auditWorkflow }
        && OriginalReportPolicy.IsMissing(
            caseType,
            auditWorkflow,
            Case?.Data.StandaloneAuditEvidenceId ?? FilesSection?.StandaloneAuditEvidenceId,
            CaseFiles.Current(FilesSection?.Documents ?? Case?.Documents ?? [])
                .Any(file => file.Occurrence.SemanticRole == DocumentSemanticRole.AuditReport));

    /// <summary>An unmet Case requirement and the section that clears it.</summary>
    public sealed record CaseRequirement(string Title, string Source, string? Why, string SectionKey);


    public CasePageFrame? Case { get; private set; }

    /// <summary>
    /// A mounted body carries its own narrow projection. The full GET carries
    /// <see cref="CasePageFrame"/>, which contains only the first response's
    /// persistent bodies. A fragment never synthesizes a page frame merely to
    /// satisfy one section's markup.
    /// </summary>
    public CaseVehicleSection? VehicleSection { get; private set; }
    public CaseValuationSection? ValuationSection { get; private set; }
    public CaseNotesSection? NotesSection { get; private set; }
    public CaseTasksSection? TasksSection { get; private set; }
    public CaseFilesSection? FilesSection { get; private set; }
    public CaseQueriesSection? QueriesSection { get; private set; }

    /// <summary>
    /// The Repair Spec fragment's frame and data: it draws only the workflow,
    /// lease, works and the accepted data, so it reads those and not the page
    /// frame's documents, notes, Triage, tasks and query route.
    /// </summary>
    private CaseSectionFrame? estimateFrame;

    private CaseDataProjection? estimateData;

    public CaseSectionFrame? SectionFrame => VehicleSection?.Frame
        ?? ValuationSection?.Frame
        ?? NotesSection?.Frame
        ?? TasksSection?.Frame
        ?? FilesSection?.Frame
        ?? QueriesSection?.Frame
        ?? estimateFrame;

    public CaseWorkflowRecord? CurrentWorkflow => Case?.Workflow ?? SectionFrame?.Workflow;
    public CaseSearchItem? CurrentSummary => Case?.Summary ?? SectionFrame?.Summary;
    public CaseEditLeaseSnapshot? CurrentEditLease => Case?.ActiveEditLease ?? SectionFrame?.ActiveEditLease;

    /// <summary>
    /// Whether the Engineer sections are read-only: the one Core access rule
    /// follows the shared assessment-writable lifecycle states and is read by
    /// the Engineer forms. The record has no Open Assessment action and no
    /// section visibility gate (D30). An unresolved access answer reads as
    /// read-only. Both views follow the Case's state (operator, 2 October 2026).
    /// </summary>
    public bool AssessmentIsReadOnly { get; private set; } = true;

    /// <summary>
    /// D11: whether GuardEstimateEditAsync/OnPostImportEstimateAsync will
    /// accept a mutation right now. Unresolved access fails closed to false,
    /// the same direction as AssessmentIsReadOnly.
    /// </summary>
    public bool AssessmentCanOpen { get; private set; }

    private const string NotInEditMode = "Enter edit mode to change the assessment.";

    private CaseAssessmentProjection? assessment;

    public CaseAssessmentProjection? Assessment
    {
        get => assessment;
        private set
        {
            assessment = value;
            // The Engineer's Value is read once where the Assessment arrives;
            // every reader on the page shares this one reading (R366).
            EngineerValue = EngineerValueOf(value);
        }
    }

    public RepairSpecificationVersion? CurrentSpecification { get; private set; }

    /// <summary>
    /// The identity of each estimate row the editor shows, as "row:id" pairs by
    /// the index the row posts at. The page draws it from the spec it shows; a
    /// commit answered in place draws it from the spec the commit wrote, and the
    /// script carries it into the rows the operator keeps typing in. Every save
    /// writes its lines afresh, so without it the next commit would name lines
    /// the last one replaced and be refused (a.QDOS26070, 6 October 2026).
    /// </summary>
    public string EstimateLineIdentities =>
        savedEstimateLineIdentities ?? string.Join(
            ' ',
            EditorLines
                .Select((row, index) => row.ExistingLineId is { } id
                    ? string.Create(CultureInfo.InvariantCulture, $"{index}:{id:D}")
                    : null)
                .Where(entry => entry is not null));

    private string? savedEstimateLineIdentities;

    /// <summary>
    /// The spec a commit answered in place wrote, which the script carries
    /// into the editor and the page address. A new spec's first commit creates
    /// it, so without it the next commit posted no spec and created another,
    /// and the catch up drew a blank new spec over the saved one. The page
    /// itself draws it empty: the spec it shows is already the editor's.
    /// </summary>
    public string? WrittenEstimateId => savedEstimateId?.ToString("D");

    private Guid? savedEstimateId;

    private IReadOnlyList<int>? postedEstimateLineRows;

    /// <summary>
    /// The written lines by the rows they were posted from: the editor dropped
    /// blank rows, so the i-th line came from the i-th non-blank row. A count
    /// that differs is a line the save did not keep as posted, and nothing is
    /// carried rather than a wrong id.
    /// </summary>
    private static string? EstimateLineIdentitiesOf(
        IReadOnlyList<int>? postedRows, RepairSpecificationVersion written)
    {
        var lines = written.Lines.OrderBy(line => line.Position).ToArray();
        if (postedRows is null || postedRows.Count != lines.Length)
        {
            return null;
        }

        return string.Join(
            ' ',
            lines.Select((line, index) =>
                string.Create(CultureInfo.InvariantCulture, $"{postedRows[index]}:{line.Id:D}")));
    }

    /// <summary>The Engineer's Value the Assessment holds, resolved when it is assigned.</summary>
    public decimal? EngineerValue { get; private set; }

    private static decimal? EngineerValueOf(CaseAssessmentProjection? assessment) =>
        assessment?.Field(AssessmentVocabulary.ValueEngineer) is { } engineerValue
            && decimal.TryParse(
                engineerValue.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : null;

    public string AssessmentValue(string path) =>
        ShownAssessment(path)?.Value is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : Labels.CaseWorkspace.AbsentValue;

    public bool CanEditCaseData => !string.IsNullOrWhiteSpace(RenderLeaseToken)
        && CurrentWorkflow?.Archive is null;

    public bool CanEditEngineering => CanEditCaseData && AssessmentCanOpen && !AssessmentIsReadOnly;

    public bool CanEditAssessmentField(string path) => CanEditEngineering;

    /// <summary>
    /// Whether this host has Experian connected: the Vehicle history area offers
    /// Run Experian check, else says Experian is not connected (ADR-0089).
    /// </summary>
    public bool ExperianConnected => runVehicleHistoryCheck.IsConnected;

    /// <summary>The value a field's control opens with: the same value its box shows.</summary>
    public string? AssessmentEditorValue(string path) => ShownAssessment(path)?.Value;

    public IReadOnlyList<SignOffEngineerProfile> EligibleSignOffEngineers { get; private set; } = [];

    public string? OpenDialog { get; private set; }

    /// <summary>
    /// The values a refused editor submitted, held for comparison against the values the case now
    /// holds. There is no control that applies, merges, or forces them: the only way forward is to
    /// enter edit mode again and retype.
    /// </summary>
    public IReadOnlyList<ProposedCaseValue> ProposedValues { get; private set; } = [];

    public bool ProposedValuesWereDropped { get; private set; }

    public bool ProposedValuesWereShortened { get; private set; }

    /// <summary>
    /// Who holds edit authority, as an operator may see them. Null when nobody is editing; a
    /// holder whose account cannot be resolved is still disclosed, without an identifier.
    /// </summary>
    public CaseEditAuthorityHolder? EditAuthorityHolder { get; private set; }

    public bool ViewerHoldsEditAuthority { get; private set; }

    /// <summary>
    /// Core lets this viewer take over the live lease (<see cref="CaseEditAuthority.CanTakeOver"/>):
    /// a colleague's, or one the Automation Actor holds.
    /// </summary>
    public bool ViewerMayTakeOver { get; private set; }

    public bool QueryFailed { get; private set; }

    /// <summary>
    /// Review point 12: the adverse dispositions this Case may actually be
    /// closed with right now, for the one Close action that is kept apart from
    /// normal progression. Empty when none is available, including on a Case
    /// that is already closed — closing never deletes a Case, so a terminal
    /// Case simply reads its outcome instead of offering the action again.
    /// </summary>
    public IReadOnlyList<CaseClosureOutcome> AvailableClosureOutcomes { get; private set; } = [];

    /// <summary>
    /// The closure chooser's options, decided by Core's own closure rules
    /// rather than by a second copy of them here. Each named outcome is put to
    /// <see cref="CaseLifecycleRules.ValidateClose"/> and
    /// <see cref="CaseLifecycleRules.RequireClosureIsAllowed"/> exactly as the
    /// Closure handler will put the real request, so an outcome the command
    /// would refuse is never offered and the two cannot drift. The probe is a
    /// question, never a command: nothing is executed and nothing is persisted.
    /// </summary>
    /// <remarks>
    /// An outcome whose resulting state is not terminal is normal progression —
    /// post-report completion — and belongs to the completion action, not to
    /// the adverse group. The store writes the outcome's own name as the state,
    /// so the resulting state is read from the name rather than mapped again.
    /// </remarks>
    private static IReadOnlyList<CaseClosureOutcome> DescribeClosureOutcomes(
        CaseWorkflowRecord workflow,
        ActionActor actor) =>
        [
            .. Enum.GetValues<CaseClosureOutcome>()
                .Where(outcome => IsAdverseDisposition(outcome)
                    && ClosureIsAllowed(workflow, actor, outcome))
        ];

    private static bool IsAdverseDisposition(CaseClosureOutcome outcome) =>
        Enum.TryParse<CaseLifecycleState>(outcome.ToString(), out var resulting)
        && CaseLifecycleRules.IsTerminal(resulting);

    private static bool ClosureIsAllowed(
        CaseWorkflowRecord workflow,
        ActionActor actor,
        CaseClosureOutcome outcome)
    {
        // The envelope the real post will carry, so the probe reaches the
        // outcome rules the same way the command does. The reason is the
        // operator's to write; the chooser only asks which outcomes exist.
        var probe = new CloseCaseRequest(
            workflow.CaseId,
            workflow.Version,
            actor,
            ClosureProbeOperationKey,
            ClosureProbeReason,
            ClosureProbeLeaseToken,
            outcome);
        try
        {
            CaseLifecycleRules.ValidateClose(probe);
            CaseLifecycleRules.RequireClosureIsAllowed(workflow, probe);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or StaffAuthorizationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Placeholders the closure probe carries so the mutation envelope is
    /// well-formed while the outcome rules are asked. Neither reaches a store:
    /// the probe is never executed, and the posted form carries the operator's
    /// own reason and this browser's real lease token.
    /// </summary>
    private const string ClosureProbeOperationKey = "closure-outcome-probe";

    private const string ClosureProbeReason = "closure-outcome-probe";

    /// <summary>The recorded assessment values a Save keeps when its form does not post them.</summary>
    private static readonly string[] RecordedAssessmentPaths =
    [
        AssessmentVocabulary.ReportDate,
        AssessmentVocabulary.SettlementStoragePerDay,
        AssessmentVocabulary.CostRecoveryCharge,
    ];

    private static readonly string ClosureProbeLeaseToken =
        new('0', CaseEditAuthority.LeaseTokenLength);

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        string? estimate,
        string? dialog,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty)
        {
            return NotFound();
        }
        // A Triage Case renders its own workspace (Details.Triage.cs); an
        // unknown id is not found by the Case frame read below.
        if (await getCaseKind.ExecuteAsync(id, cancellationToken) == CaseType.Triage)
        {
            return await GetTriageCaseAsync(
                id,
                actor,
                HttpContext.RequestServices.GetRequiredService<TriageCasePorts>(),
                cancellationToken);
        }

        using var activity = DocumentReadTelemetry.Start("web.case.main");
        try
        {
            // The lease is restored after the first reads, because it uses
            // TempData.
            var work = WorkSelector;
            (Case, var workspace) = await ReadFrameAndWorkspaceAsync(id, actor, work, cancellationToken);
            if (Case is null)
            {
                return NotFound();
            }
            ApplyAssessmentAccess(actor, Case.Workflow);
            // The lease decides how much of the record is rendered now, so it is
            // restored before deciding which section bodies render directly.
            await RestoreLeaseStateAsync(id, actor, Case.ActiveEditLease, leases, cancellationToken);
            // Each phase below starts its independent reads together, at most
            // four at a time and each on its own database context, and sets the
            // page's state only once all of them have finished. The phases stay
            // in order because each uses what the one before it read.
            // The Actions menu's Tasks opens here without script, and a task
            // action returns here: the page draws the Tasks dialog open.
            var tasksOpen = string.Equals(dialog, "tasks", StringComparison.Ordinal);
            using (DocumentReadTelemetry.Start("web.case.direct-sections"))
            {
                await LoadDirectSectionsAsync(id, actor, workspace, work, tasksOpen, cancellationToken);
            }
            using (DocumentReadTelemetry.Start("web.case.engineer-sections"))
            {
                await LoadEngineerSectionsAsync(id, actor, workspace, work, estimate, dialog, cancellationToken);
            }
            // The message record's Change case links land here with hold or
            // close (operator, 27 October 2026): the page draws that dialog
            // open the same way, only where the Case offers the act.
            OpenDialog ??= tasksOpen
                ? "tasks"
                : dialog is "hold" or "close" ? dialog : null;
            using (DocumentReadTelemetry.Start("web.case.extras"))
            {
                await LoadExtrasAsync(id, actor, cancellationToken);
                AvailableClosureOutcomes = DescribeClosureOutcomes(Case.Workflow, actor);
                RestoreProposedValues(id);
            }
            return Page();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseDetailsQueryFailed(logger, id, exception);
            QueryFailed = true;
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Page();
        }
    }

    /// <summary>
    /// The frame and the workspace each need only the Case id and read on their
    /// own database context, so they start together. Each keeps its own phase,
    /// timed around its own read. The access answer is the frame's workflow
    /// state. The full page and the answer to a commit both begin with them.
    /// </summary>
    private async Task<(CasePageFrame? Frame, AssessmentWorkspace? Workspace)> ReadFrameAndWorkspaceAsync(
        Guid id,
        ActionActor actor,
        CaseWorkSelector work,
        CancellationToken cancellationToken,
        CaseDataProjection? knownData = null)
    {
        using var firstReads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var frameRead = firstReads.Start(async token =>
        {
            using (DocumentReadTelemetry.Start("web.case.frame"))
            {
                // A commit's answer hands in the data the save itself projected,
                // so the frame reads one row less (#1951).
                return await getCasePageFrame.ExecuteAsync(new(id, actor, Data: knownData, Work: work), token);
            }
        });
        var workspaceRead = firstReads.Start(async token =>
        {
            using (DocumentReadTelemetry.Start("web.case.workspace"))
            {
                return await getAssessmentWorkspace.ExecuteAsync(new(id, actor, work), token);
            }
        });
        await firstReads.WhenAllAsync();
        return (await frameRead, await workspaceRead);
    }

    /// <summary>
    /// The sign-off list the report snapshot read this request, kept whether or
    /// not the assessment can open so the extras do not read it again.
    /// </summary>
    private IReadOnlyList<SignOffEngineerProfile>? snapshotSignOffEngineers;

    private async Task LoadEngineerSectionsAsync(
        Guid id,
        ActionActor actor,
        AssessmentWorkspace? workspace,
        CaseWorkSelector work,
        string? estimate,
        string? dialog,
        CancellationToken cancellationToken)
    {
        if (workspace is null)
        {
            await EvaluateEngineerSectionConditionsAsync(cancellationToken);
            return;
        }

        Assessment = workspace.Assessment;
        CurrentSpecification = workspace.CurrentSpecification;
        // Every report read is the viewed work's (operator, 2 October 2026).
        // Readiness drives the Next action, and only while the assessment can
        // open. The wording is read in every state so the Incident narrative
        // and the statement of truth show what the report prints even on a
        // Held, Query or closed Case; only the editable wording blocks wait for
        // the assessment.
        var canOpen = AssessmentCanOpen;
        var asksSendToAi = AssessmentCanOpen && !AssessmentIsReadOnly;
        // The snapshot source takes what this page already read for the work
        // rather than reading the workspace, preparations, valuations and the
        // Case's works again.
        var reuse = new ReportProjectionReuse(
            work,
            workspace,
            AssetPreparations,
            appliedValuationsLoaded ? AppliedValuations : null,
            Case!.Frame);

        using var reads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        // The authors' names depend only on the estimates, so they are read in
        // the same slot and overlap the other reads in this phase. The workspace
        // already holds the Current spec with its lines, so the list does not
        // read those again.
        var estimates = reads.Start(async token =>
        {
            var listed = await listEstimates.ExecuteAsync(id, work, workspace.CurrentSpecification, token);
            return (Listed: listed, AuthorNames: await ResolveSpecAuthorNamesAsync(listed, token));
        });
        var cards = reads.Start(token => labourRateCards.ListAsync(actor, token));
        var bankWordings = reads.Start(unroadworthyReasonBank.ListAsync);
        var salvageMatrix = CanEditEngineering
            ? reads.Start(token => principalSalvageMatrices.GetForCaseAsync(id, token))
            : null;
        var reportInputs = reads.Start(token => reportSnapshotSource.GetAsync(id, actor, work, reuse, token));
        var currentGeneration = reads.Start(token =>
            reportGenerations.GetCurrentAsync(actor, id, work, token));
        // The other work's report goes in the same send once the Audit exists.
        var otherWork = work == CaseWorkSelector.Primary ? CaseWorkSelector.Current : CaseWorkSelector.Primary;
        var otherGeneration = Works is { HasAudit: true }
            ? reads.Start(token => reportGenerations.GetCurrentAsync(actor, id, otherWork, token))
            : null;
        var sendingToAi = asksSendToAi
            ? reads.Start(token => sendToAiControl.IsEnabledAsync(token))
            : null;
        var glass = reads.Start(token => ReadGlassSessionAsync(id, actor, token));
        await reads.WhenAllAsync();

        (Estimates, SpecAuthorNames) = await estimates;
        LabourRateCards = await cards;
        ApplyEstimateSelection(estimate);
        UnroadworthyReasonWordings = [.. (await bankWordings).Select(item => item.Text)];
        PrincipalSalvageMatrix = salvageMatrix is null ? null : await salvageMatrix;
        var inputs = await reportInputs;
        snapshotSignOffEngineers = inputs?.Readiness.EligibleSignOffEngineers;
        if (inputs is not null && canOpen)
        {
            var readiness = CaseReportReadiness.Evaluate(inputs.Readiness);
            ReportDraftPreparation = new(readiness.Reasons);
            EligibleSignOffEngineers = inputs.Readiness.EligibleSignOffEngineers;
            SelectedSignOffEngineerId = readiness.Signatory?.StaffId;
        }
        if (inputs is not null)
        {
            IncidentNarrative = ReportWordingComposition.NatureOfIncidentOf(inputs.Projection);
            IncidentNarrativeFollowsImpacts = !ReportWordingComposition.NatureOfIncidentIsWorded(inputs.Projection);
            StatementParts = AssessmentReportContract.StatementPartsOf(inputs.Projection);
            if (AssessmentCanOpen)
            {
                var wording = WordingOf(inputs.Projection);
                ReportWording = wording.Offered;
                ReportWordingComposed = wording.Snapshot is { } wordingSnapshot
                    ? ReportWording.Where(block => !block.Manual).ToDictionary(
                        block => block.Key,
                        block => ReportWordingComposition.ComposedText(
                            block.Key, wordingSnapshot, wordingSnapshot.Presentation()),
                        StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
                ReportWordingHeadings = wording.Snapshot is { } headingSnapshot
                    ? ReportWording.Where(block => !block.Manual).ToDictionary(
                        block => block.Key,
                        block => ReportWordingComposition.StandardTitle(block.Key, headingSnapshot.Presentation()),
                        StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }
        CurrentReportGeneration = await currentGeneration;
        OtherReportGeneration = otherGeneration is null ? null : await otherGeneration;
        EvaluateEngineerSectionConditions(sendingToAi is not null && await sendingToAi);
        ApplyGlassSession(await glass);

        // What the first reads decided: every spec's frozen versions in one
        // read (v38 P2: Compare and Supplementary read them), and the
        // delivery reads a stored report needs, the viewed work's too.
        var generated = CurrentReportGeneration is not null;
        var specificationIds = Estimates.Select(item => item.SpecificationId).ToArray();
        var snapshotRead = specificationIds.Length > 0
            ? reads.Start(token => specificationSnapshots.ListForSpecificationsAsync(id, specificationIds, token))
            : null;
        var suggestions = generated
            ? reads.Start(token => reportRecipientSuggestions.GetAsync(id, work, token))
            : null;
        var history = generated
            ? reads.Start(token => reportSendHistory.GetAsync(id, work, token))
            : null;
        var senders = generated
            ? reads.Start(token => caseMailSenders.OptionsAsync(id, token))
            : null;
        var otherHistory = generated && OtherReportGeneration is not null
            ? reads.Start(token => reportSendHistory.GetAsync(id, otherWork, token))
            : null;
        await reads.WhenAllAsync();

        ApplyEstimateSnapshots(snapshotRead is null ? [] : await snapshotRead, dialog);
        ReportSendHistory = history is null
            ? CaseReportSendHistory.None
            : await history;
        DeliveryRecipientSuggestions = suggestions is null ? null : await suggestions;
        if (DeliveryRecipientSuggestions is { } dispatchFacts)
        {
            // The generation's own facts complete what the query reads, and
            // the plan is made once here for every part of the form.
            DeliveryRecipientSuggestions = dispatchFacts with
            {
                History = ReportSendHistory,
                ConfirmedArtifacts =
                [
                    .. CurrentReportGeneration?.Artifacts
                        .Where(artifact => artifact.Status == CaseReportArtifactStatus.Confirmed)
                        .Select(artifact => artifact.Kind) ?? []
                ]
            };
            DeliveryDispatchPlan = ReportDispatchPolicy.Plan(DeliveryRecipientSuggestions, clock.GetUtcNow());
        }
        ReportAddressBook = DeliveryRecipientSuggestions is { } addressBook
            ? CaseReportDeliveryPolicy.AddressBook(addressBook)
            : [];
        ReportSenders = senders is null ? ReportSenders : await senders;
        OtherReportSendHistory = otherHistory is null
            ? CaseReportSendHistory.None
            : await otherHistory;
        ReportDeliveryMessage = await RenderReportDeliveryMessageAsync(actor, cancellationToken);
        ApplyReportDeliverySubject();
        OriginalReport = ReadOriginalReport();
        // A refused send lands back here with the dialog open on its notice
        // (v48 item N), only where the page offers the send.
        OpenDialog = EstimateDialogOf(dialog)
            ?? (CanSendReport && string.Equals(dialog, "send-report", StringComparison.Ordinal) ? "send-report" : null);
    }

    private async Task EvaluateEngineerSectionConditionsAsync(CancellationToken cancellationToken) =>
        EvaluateEngineerSectionConditions(
            AssessmentCanOpen && !AssessmentIsReadOnly && await sendToAiControl.IsEnabledAsync(cancellationToken));

    /// <param name="sendingToAiEnabled">
    /// Whether sending to AI is switched on; asked only while the assessment
    /// can open and is not read-only.
    /// </param>
    private void EvaluateEngineerSectionConditions(bool sendingToAiEnabled)
    {
        if (!AssessmentCanOpen)
        {
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.NotAvailableForCase;
        }
        else if (IsInspectionView)
        {
            // The AI draft lands on the current work: the Inspection view
            // offers it once the job names the work it was asked from.
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.NotAvailableForCase;
        }
        else if (!sendingToAiEnabled)
        {
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.SendingToAiDisabled;
        }
        else if (EngineerValue is null)
        {
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.EngineerValueRequired;
        }

        ReportDraftCondition = !AssessmentCanOpen || ReportDraftPreparation is null
            ? Labels.CaseWorkspace.EngineerSections.NotAvailableForCase
            : ReportDraftPreparation.CanGenerate
                ? null
                : Labels.CaseWorkspace.EngineerSections.NotReady;
    }

    /// <summary>
    /// One Case section's body, for the frame's lazy mount, on the record's
    /// own fragment path <c>/Cases/{id}/Section?section=&lt;key&gt;</c>. It runs the same
    /// authorized load, lease restoration and section-specific supplemental
    /// query as the full GET and returns only the named body. Frame-only
    /// lookups, such as the assigned Engineer's display name, are deliberately
    /// not repeated for a mounted body that cannot render them.
    /// </summary>
    public async Task<IActionResult> OnGetSectionAsync(
        Guid id,
        string? section,
        string? estimate,
        string? dialog,
        [FromHeader(Name = "X-Pegasus-Edit-Lease")] string? renderLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty)
        {
            return FragmentNotFound();
        }

        // The Tasks dialog's body is fetched here as the dialog opens; it is
        // not a section, so the section vocabulary does not name it.
        var key = string.Equals(section, "tasks", StringComparison.Ordinal) ? "tasks" : NormalizeSection(section);
        var view = key switch
        {
            "tasks" => TasksView,
            "estimate" => EstimateView,
            _ => LazySectionViews.GetValueOrDefault(key)
        };
        if (view is null)
        {
            return FragmentNotFound();
        }

        using var activity = DocumentReadTelemetry.Start("web.case.fragment." + key);
        try
        {
            SectionFilter = key;
            // A mounted body uses its own focused read. The full response uses
            // its bounded page frame; neither path materializes every Case body.
            switch (key)
            {
                case "vehicle":
                    VehicleSection = await getCaseVehicleSection.ExecuteAsync(
                        new(id, actor, Work: WorkSelector), cancellationToken);
                    if (VehicleSection is null)
                    {
                        return FragmentNotFound();
                    }
                    Assessment = VehicleSection.Assessment;
                    ApplyAssessmentAccess(actor, VehicleSection.Frame.Workflow);
                    break;
                case "valuation":
                    ValuationSection = await getCaseValuationSection.ExecuteAsync(
                        new(id, actor, Work: WorkSelector), cancellationToken);
                    if (ValuationSection is null)
                    {
                        return FragmentNotFound();
                    }
                    Assessment = ValuationSection.Assessment;
                    ApplyAssessmentAccess(actor, ValuationSection.Frame.Workflow);
                    break;
                case "notes":
                    NotesSection = await getCaseNotesSection.ExecuteAsync(
                        new(
                            id,
                            actor,
                            NotesPage: TimelinePage(NotesPageFilter),
                            HistoryPage: TimelinePage(HistoryPageFilter)),
                        cancellationToken);
                    if (NotesSection is null)
                    {
                        return FragmentNotFound();
                    }
                    break;
                case "tasks":
                    TasksSection = await getCaseTasksSection.ExecuteAsync(new(id, actor), cancellationToken);
                    if (TasksSection is null)
                    {
                        return FragmentNotFound();
                    }
                    break;
                case "estimate":
                    return await GetEstimateFragmentAsync(
                        id, actor, estimate, dialog, renderLeaseToken, cancellationToken);
                case "queries":
                    QueriesSection = await getCaseQueriesSection.ExecuteAsync(new(id, actor), cancellationToken);
                    if (QueriesSection is null)
                    {
                        return FragmentNotFound();
                    }
                    // The mounted body draws the same assignee control and Take
                    // as the full page, so it reads the same roster (the
                    // signed-in person first) that ReadWorkspaceExtrasAsync does.
                    if (QueriesSection.Queries.Any(query => query.IsOpen))
                    {
                        var viewerStaffId = Pegasus.Core.Notifications.StaffNotificationPolicy.StaffId(actor);
                        var roster = (await caseEngineerChoices.GetAsync(actor, cancellationToken))
                            .OrderBy(choice => choice.StaffId == viewerStaffId ? 0 : 1)
                            .ToArray();
                        AssignEngineerChoices = new(roster, viewerStaffId);
                    }
                    break;
                case "files":
                    FilesSection = await getCaseFilesSection.ExecuteAsync(new(id, actor), cancellationToken);
                    if (FilesSection is null)
                    {
                        return FragmentNotFound();
                    }
                    ApplyAssessmentAccess(actor, FilesSection.Frame.Workflow);
                    break;
            }

            // A mounted body is an asynchronous GET. It must not read or write
            // cookie-backed TempData: its response can otherwise race a Claim,
            // Save or release redirect and replace the browser's lease state.
            // The browser can repeat its already-rendered token in a header so
            // Files keeps its supported controls. It is rendering data only;
            // the POST handlers remain the authority boundary.
            if (!string.IsNullOrWhiteSpace(renderLeaseToken)
                && validateCaseRenderLease is not null
                && await validateCaseRenderLease.ExecuteAsync(
                    new(id, actor, renderLeaseToken), cancellationToken))
            {
                fragmentLeaseToken = renderLeaseToken;
            }
            if (key == "files")
            {
                // The preparations are independent of the intakes and images.
                var files = ReadFilesAsync(id, CanEditCaseData, cancellationToken);
                var preparations = caseAssetPreparationQueries.ListForCaseAsync(id, cancellationToken);
                await Task.WhenAll(files, preparations);
                ApplyFiles(files.Result);
                AssetPreparations = preparations.Result;
            }
            if (key == "valuation")
            {
                await LoadValuationSectionAsync(id, actor, cancellationToken);
            }
            return Partial(view, this);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCaseDetailsQueryFailed(logger, id, exception);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task LoadDirectSectionsAsync(
        Guid id,
        ActionActor actor,
        AssessmentWorkspace? workspace,
        CaseWorkSelector work,
        bool readsTasks,
        CancellationToken cancellationToken)
    {
        var query = new GetCaseSectionQuery(
            id,
            actor,
            workspace,
            HasAssessmentWorkspace: true,
            Data: Case!.Data,
            Documents: Case.Documents,
            Frame: Case.Frame,
            Work: work,
            // The roster read by the Tasks body depends on it, and the extras
            // below name the same colleague once they have run.
            ColleagueIsEditing: Case.ActiveEditLease is { } lease
                && !CaseEditAuthority.IsHolder(lease.HolderKind, lease.Holder, actor),
            NotesPage: TimelinePage(NotesPageFilter),
            HistoryPage: TimelinePage(HistoryPageFilter));
        var rendered = LazySectionViews.Keys.Where(key => !SectionIsDeferred(key)).ToHashSet(StringComparer.Ordinal);
        using var reads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var vehicle = rendered.Contains("vehicle")
            ? reads.Start(token => getCaseVehicleSection.ExecuteAsync(query, token))
            : null;
        var valuation = rendered.Contains("valuation")
            ? reads.Start(token => getCaseValuationSection.ExecuteAsync(query, token))
            : null;
        var files = rendered.Contains("files")
            ? reads.Start(token => getCaseFilesSection.ExecuteAsync(query, token))
            : null;
        var notes = rendered.Contains("notes")
            ? reads.Start(token => getCaseNotesSection.ExecuteAsync(query, token))
            : null;
        // The Tasks dialog's body is drawn with the page only when the page
        // draws the dialog open; otherwise it is fetched as the dialog opens.
        // It carries the enabled staff an open task's Assign offers.
        var tasks = readsTasks
            ? reads.Start(token => getCaseTasksSection.ExecuteAsync(query, token))
            : null;
        var queries = rendered.Contains("queries")
            ? reads.Start(token => getCaseQueriesSection.ExecuteAsync(query, token))
            : null;
        // Report Preview is part of the initial Case response even when the
        // heavier Files gallery is deferred.
        var preparations = reads.Start(token => caseAssetPreparationQueries.ListForCaseAsync(id, token));
        var valuationReads = rendered.Contains("valuation")
            ? reads.Start(token => ReadValuationSectionAsync(id, actor, work, token))
            : null;
        await reads.WhenAllAsync();

        if (vehicle is not null)
        {
            VehicleSection = await vehicle
                ?? throw new InvalidOperationException("The Case vehicle section is unavailable.");
            Assessment ??= VehicleSection.Assessment;
        }
        if (valuation is not null)
        {
            ValuationSection = await valuation
                ?? throw new InvalidOperationException("The Case valuation section is unavailable.");
            Assessment ??= ValuationSection.Assessment;
        }
        if (files is not null)
        {
            FilesSection = await files
                ?? throw new InvalidOperationException("The Case files section is unavailable.");
        }
        if (notes is not null)
        {
            NotesSection = await notes
                ?? throw new InvalidOperationException("The Case notes section is unavailable.");
        }
        if (tasks is not null)
        {
            TasksSection = await tasks
                ?? throw new InvalidOperationException("The Case tasks section is unavailable.");
        }
        if (queries is not null)
        {
            QueriesSection = await queries
                ?? throw new InvalidOperationException("The Case queries section is unavailable.");
        }
        AssetPreparations = await preparations;
        if (valuationReads is not null)
        {
            ApplyValuationSection(await valuationReads);
        }
    }

    /// <summary>
    /// The record's remaining reads: the directory choices, the Principal's
    /// previous addresses and reviewed inspection locations, the Files
    /// galleries, the frame's names and Engineer choices, the lease holder,
    /// and the Case's AI jobs, read once for both the Next action's drafts
    /// and the Valuation section's pending research.
    /// </summary>
    private async Task LoadExtrasAsync(Guid id, ActionActor actor, CancellationToken cancellationToken)
    {
        var details = Case!;
        var canEditCaseData = CanEditCaseData;
        var inspectionRendered = !SectionIsDeferred("inspection");
        var filesRendered = !SectionIsDeferred("files");
        var extrasInputs = new WorkspaceExtrasInputs(
            details,
            actor,
            AssessmentCanOpen,
            EligibleSignOffEngineers,
            snapshotSignOffEngineers,
            LeaseToken);
        var activeLease = details.ActiveEditLease;
        var viewerHoldsLease = activeLease is not null
            && CaseEditAuthority.IsHolder(activeLease.HolderKind, activeLease.Holder, actor);

        using var reads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var claimSources = canEditCaseData
            ? reads.Start(token => contactDirectory.ListByRoleAsync(actor, ContactRole.ClaimSource, token))
            : null;
        var previousAddresses = inspectionRendered
            ? reads.Start(token => inspectionAddressChoicesQueries.GetPreviousAddressesAsync(id, token))
            : null;
        var directoryLocations = inspectionRendered
            ? reads.Start(token => inspectionAddressChoicesQueries.GetDirectoryLocationsAsync(id, token))
            : null;
        var repairers = inspectionRendered && canEditCaseData
            ? reads.Start(token => contactDirectory.ListByRoleAsync(actor, ContactRole.Repairer, token))
            : null;
        var files = filesRendered
            ? reads.Start(token => ReadFilesAsync(id, canEditCaseData, token))
            : null;
        var extras = reads.Start(token => ReadWorkspaceExtrasAsync(extrasInputs, token));
        var holder = activeLease is { } lease && !viewerHoldsLease
            ? reads.Start(token => describeEditAuthorityHolder.ExecuteAsync(
                lease.HolderKind,
                lease.Holder,
                actor,
                token))
            : null;
        var requester = viewerHoldsLease && details.Frame.EditRequest is { Answer: null } asking
            ? reads.Start(token => describeEditAuthorityHolder.ExecuteAsync(
                asking.RequesterKind,
                asking.Requester,
                actor,
                token))
            : null;
        var caseAiJobs = reads.Start(token => aiJobs.ListOpenForSubjectAsync(id, token));
        await reads.WhenAllAsync();

        if (claimSources is not null)
        {
            ClaimSourceChoices = await claimSources;
        }
        if (previousAddresses is not null && directoryLocations is not null)
        {
            InspectionAddressChoices = Pegasus.Core.Address.InspectionAddressChoices.Resolve(
                InspectionAddressChoicesData.Of(details.Data, await previousAddresses, await directoryLocations));
        }
        if (repairers is not null)
        {
            RepairerChoices = await repairers;
        }
        if (files is not null)
        {
            ApplyFiles(await files);
        }
        var workspaceExtras = await extras;
        EngineerDisplayName = workspaceExtras.EngineerDisplayName;
        SignOffEngineerDisplayName = workspaceExtras.SignOffEngineerDisplayName;
        AssignEngineerChoices = workspaceExtras.AssignEngineerChoices;
        if (activeLease is not null)
        {
            ApplyLeaseState(
                activeLease,
                details.Frame.EditRequest,
                actor,
                viewerHoldsLease,
                holder is null ? CaseEditAuthorityHolder.Unnamed : await holder,
                requester is null ? null : await requester);
        }
        var jobs = await caseAiJobs;
        AiDrafts = AiDraftPolicy.Drafts(jobs, details.Data.RequiredAiDraftTargetDays);
        PendingMarketResearch = MarketResearchPolicy.PendingOf(jobs);
    }

    public Task<IActionResult> OnPostClaimLeaseAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        bool takeOver,
        string? section,
        Guid? estimate,
        CancellationToken cancellationToken) =>
        ClaimLeaseAsync(
            leases,
            id,
            expectedVersion,
            operationKey,
            takeOver,
            // A repairer VAT blocker claims on the Current spec, so the spec
            // it opens is the one the report prints (issue 898).
            () => RedirectToSection(id, section, estimate?.ToString("D")),
            cancellationToken);

    /// <summary>
    /// The full-POST fallback lands back on the section the operator was
    /// looking at (v25 decision 3); the scripted path never navigates. A save
    /// that carried a repair specification keeps that specification selected,
    /// and every post returns to the view it was made in.
    /// </summary>
    private RedirectToPageResult RedirectToSection(Guid id, string? section, string? estimate = null) =>
        (string.IsNullOrWhiteSpace(section) || NormalizeSection(section) == Labels.CaseWorkspace.DefaultSectionKey)
            && estimate is null
            ? RedirectToDetails(id)
            : RedirectToPage("/Cases/Details", new
            {
                id,
                section = string.IsNullOrWhiteSpace(section) ? null : NormalizeSection(section),
                estimate,
                view = ReturnView
            });

    public Task<IActionResult> OnPostHeartbeatLeaseAsync(
        Guid id,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        HeartbeatLeaseAsync(leases, id, editLeaseToken, cancellationToken);

    /// <summary>Ask to edit: the request to the member of staff holding the lease (FRD-14, R550).</summary>
    public Task<IActionResult> OnPostRequestEditAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? section,
        CancellationToken cancellationToken) =>
        RequestEditAsync(leases, id, expectedVersion, operationKey, () => RedirectToSection(id, section), cancellationToken);

    /// <summary>The holder's Accept or Deny, or the requester's withdrawal (R553).</summary>
    public Task<IActionResult> OnPostAnswerEditRequestAsync(
        Guid id,
        Guid requestId,
        string? answer,
        string? editLeaseToken,
        string? section,
        CancellationToken cancellationToken) =>
        AnswerEditRequestAsync(leases, id, requestId, answer, editLeaseToken, () => RedirectToSection(id, section), cancellationToken);

    /// <summary>The long poll the editing and the waiting page hold on the Case's lease and request (R551).</summary>
    public Task<IActionResult> OnPostWatchEditRequestAsync(
        Guid id,
        string? stamp,
        CancellationToken cancellationToken) =>
        WatchEditRequestAsync(editRequestWatch, describeEditAuthorityHolder, hostLifetime, id, stamp, cancellationToken);

    public Task<IActionResult> OnPostReleaseLeaseAsync(
        Guid id,
        string operationKey,
        string editLeaseToken,
        string? section,
        CancellationToken cancellationToken) =>
        ReleaseLeaseAsync(
            leases,
            id,
            operationKey,
            editLeaseToken,
            () => RedirectToSection(id, section),
            cancellationToken);

    /// <summary>
    /// The release a page sends as its operator leaves the Case by a link (FRD-14). It is not an
    /// operator action and has no page to return to: it answers 204 whether or not a lease was
    /// still there to release, and it reads and writes no TempData, because the page the operator
    /// is heading to is loading at the same moment and owns that cookie. Antiforgery is validated
    /// as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseLeaseBeaconAsync(
        Guid id,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new NoContentResult();
        }

        try
        {
            await leases.ReleaseAsync(
                new(id, actor, NewOperationKey(), editLeaseToken),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IsLeaseLoss(exception))
        {
            // The lease had already lapsed or moved to a colleague.
        }
        return new NoContentResult();
    }

    public async Task<IActionResult> OnPostSaveAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? reason,
        bool saveUnroadworthyReason,
        string editLeaseToken,
        string? claimantName,
        string? claimNumber,
        string? vehicleRegistration,
        string? vehicleMake,
        string? vehicleModel,
        string? vehicleYear,
        long? vehicleMileage,
        string? vehicleMileageUnit,
        string? accidentCircumstances,
        DateOnly? incidentDate,
        DateOnly? dueBy,
        string? vatStatus,
        DateOnly? inspectionDate,
        DateOnly? inspectionDeadline,
        string? inspectionAddress,
        string? claimantContactNumber,
        string? claimantAddress,
        string? storageLocation,
        [FromForm(Name = "assessmentFields")] Dictionary<string, string?>? assessmentFields,
        decimal? storagePerDay,
        decimal? recoveryCharge,
        Guid? signOffEngineerId,
        DateOnly? reportDate,
        AssetPreparationEditForm[]? preparationEdits,
        ReportWordingEditForm[]? wordingEdits,
        string? damageMarkers,
        string? repairerName,
        string? repairerAddress,
        Guid? repairerDirectoryId,
        string? principalNotes,
        string? claimSourceNotes,
        Guid? claimSourceId,
        string? claimSourceContactName,
        string? claimSourceContactTelephone,
        string? claimSourceContactEmail,
        GuideEntryForm[]? guideEntries,
        ValuationSelectionForm? selection,
        string? section,
        CancellationToken cancellationToken)
    {
        // A commit made by the page script is answered with the parts it swaps
        // (FRD-16); every other post is answered by the redirect it always was.
        if (IsScriptRequest)
        {
            AnswerInPlace();
        }
        assessmentFields ??= [];
        // The save writes the work of the view it was posted from (operator,
        // 2 October 2026) and reads that work's values.
        var work = WorkSelector;
        // The Repair Spec editor and the valuation calculator join the Case
        // form (one Save, 23 September 2026). They read like the estimate and
        // valuation commands did, so they answer to the assessment access
        // those commands required.
        var carriesEstimate = Posted("estimateName");
        var carriesCalculator = selection?.Opening is not null;
        if ((carriesEstimate || carriesCalculator)
            && TryGetActor(out var accessActor)
            && !await HasAssessmentAccessAsync(id, accessActor, cancellationToken))
        {
            return NotFound();
        }
        string? bankWording = null;
        if (saveUnroadworthyReason)
        {
            assessmentFields.TryGetValue(AssessmentVocabulary.UnroadworthyReason, out bankWording);
        }

        string? bankStatus = null;
        string? bankError = null;
        string? savedEstimate = null;
        CaseDataProjection? savedData = null;
        var result = await ExecuteCaseCommandAsync(
            id,
            editLeaseToken,
            "save_case",
            async actor =>
            {
                if (saveUnroadworthyReason)
                {
                    try
                    {
                        _ = UnroadworthyReasonBank.Normalize(bankWording);
                    }
                    catch (ArgumentException exception)
                    {
                        bankError = MutationRefusalMessage(
                            exception, "The wording was not saved to the bank. Retry the operation.");
                        throw;
                    }
                }

                if (!ModelState.IsValid)
                {
                    throw new InvalidOperationException(UnreadableValueMessage());
                }

                if (assessmentFields.Keys.Any(path => !EditorLabels.IsAssessmentField(path)))
                {
                    throw new InvalidOperationException("This field is not part of the Case editor.");
                }
                CaseWorkspaceEstimate? estimate = null;
                if (carriesEstimate)
                {
                    estimate = await EstimateEditorPartAsync(id, actor, cancellationToken);

                    // Until the save lands, the page keeps the specification it showed.
                    savedEstimate = estimate.EstimateId?.ToString("D") ?? "new";
                }
                var recordedCards = guideEntries is { Length: > 0 } || carriesCalculator
                    ? await listCaseValuations.ExecuteAsync(id, work, cancellationToken)
                    : [];
                var adoption = carriesCalculator
                    ? ChosenCalculation(selection, guideEntries ?? [], recordedCards)
                    : null;
                var preparationSubmitted = preparationEdits is { Length: > 0 };
                var wordingSubmitted = wordingEdits is { Length: > 0 };
                var damageFields = assessmentFields.Where(field => EditorLabels.Damage.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                var damageSubmitted = Posted(nameof(damageMarkers)) || damageFields.Count > 0;
                // The vehicle's identity (VIN, type, body) and its transmission are
                // edited wherever the Vehicle section edits, like its registration —
                // not an Engineer field.
                var vehicleIdentityFields = assessmentFields.Where(field => EditorLabels.Vehicle.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                // D4/FRD-12: an image preparation is not an engineering field.
                // Cropping, rotating and ordering the Case's own photographs is
                // offered wherever the Case edit lease is held, so it is gated
                // on the record being editable at all rather than on assessment
                // access — the gate that showed no Crop on a Review-state Case
                // and refused the one taken from the Report section.
                var engineeringSubmitted = assessmentFields.Count > vehicleIdentityFields.Count
                    || Posted(nameof(storagePerDay)) || Posted(nameof(recoveryCharge))
                    || Posted(nameof(signOffEngineerId)) || Posted(nameof(reportDate))
                    || damageSubmitted || wordingSubmitted || guideEntries is { Length: > 0 }
                    || estimate is not null || adoption is not null;
                // The edit basis and the recorded values each open their own
                // context, so they read side by side; a missing Case is still
                // reported first. The save keeps three recorded values it does not
                // post, so those are all it reads of the assessment.
                using var saveReads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
                var basisRead = saveReads.Start(readToken => getCaseEditBasis.ExecuteAsync(new(id, actor, work), readToken));
                var recordedRead = saveReads.Start(readToken => getAssessmentWorkspace.FieldValuesAsync(
                    new(id, actor, work), RecordedAssessmentPaths, readToken));
                await saveReads.WhenAllAsync();
                var current = await basisRead
                    ?? throw new KeyNotFoundException("The Case is unavailable.");
                var data = current.Data;
                // This read supplies unshown values, never new write authority.
                // The transaction still receives the submitted version and lease.
                if (engineeringSubmitted
                    && AssessmentAccessPolicy.IsReadOnly(new(current.Workflow.State)))
                {
                    throw new InvalidOperationException("Engineering fields are read-only in this Case state.");
                }
                if (preparationSubmitted && current.Workflow.Archive is not null)
                {
                    throw new InvalidOperationException("The case is read-only once archived.");
                }
                var recorded = await recordedRead;
                string? Recorded(string path) => recorded.GetValueOrDefault(path);
                decimal? Money(string path) => decimal.TryParse(Recorded(path), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var value) ? value : null;
                var persisted = data.Workspace;
                var address = Submitted(nameof(inspectionAddress), inspectionAddress, Accepted(data.Inspection.Address)?.Value);
                var treatment = CaseDetailsSaveRules.InspectionTreatment(
                    Posted(nameof(inspectionAddress)), persisted?.InspectionAddressTreatment, address);
                var mileageValue = Submitted(nameof(vehicleMileage), vehicleMileage, Accepted(data.Vehicle.Mileage)?.Value);
                var mileageUnit = Submitted(nameof(vehicleMileageUnit), vehicleMileageUnit, Accepted(data.Vehicle.MileageUnit)?.Value);
                CaseOdometerUnit? originalUnit = null;
                if (!string.IsNullOrWhiteSpace(mileageUnit))
                {
                    if (!CaseOdometer.TryParseUnit(mileageUnit, out var parsedUnit))
                        throw new InvalidOperationException("The mileage unit is invalid.");
                    originalUnit = parsedUnit;
                }
                // The mileage value and its selected unit are saved together.
                // Emptying the box clears the unit; a mileage submitted without
                // a unit is read in miles.
                originalUnit = CaseDetailsSaveRules.MileageUnit(mileageValue, originalUnit);
                var reportFields = assessmentFields.Where(field => EditorLabels.Report.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                // Every control opens holding its value, so emptying it clears it.
                var settlementFields = assessmentFields
                    .Where(field => EditorLabels.Settlement.ContainsKey(field.Key) || EditorLabels.OriginalReport.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                AssessmentPolicy.RequireOriginalReportScope(
                    settlementFields.Keys,
                    current.Summary.CaseType,
                    current.Summary.AuditWorkflow);
                // The report's three values are Valuation's own boxes (operator,
                // 26 September 2026), saved like any field.
                var valuationFields = assessmentFields.Where(field => EditorLabels.Valuation.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                var reportSubmitted = reportFields.Count > 0 || Posted(nameof(signOffEngineerId)) || Posted(nameof(reportDate));
                var overviewSubmitted = new[] { nameof(claimantName), nameof(claimantContactNumber), nameof(claimantAddress),
                    nameof(claimNumber),
                    nameof(incidentDate), nameof(dueBy), nameof(accidentCircumstances), nameof(vatStatus),
                    nameof(repairerName), nameof(repairerAddress), nameof(repairerDirectoryId),
                    nameof(principalNotes), nameof(claimSourceNotes), nameof(claimSourceId),
                    nameof(claimSourceContactName), nameof(claimSourceContactTelephone),
                    nameof(claimSourceContactEmail) }.Any(Posted);
                // The claim source is a choice from the active Claim source
                // records, copied onto the Case as its snapshot. An unposted
                // select keeps the recorded source; an empty one records none;
                // the same record keeps the snapshot it already has.
                var claimSource = persisted?.ClaimSource;
                var claimSourceChanged = false;
                if (overviewSubmitted && Posted(nameof(claimSourceId)))
                {
                    if (claimSourceId is not { } sourceId)
                    {
                        claimSourceChanged = claimSource is not null;
                        claimSource = null;
                    }
                    else if (CaseDetailsSaveRules.ClaimSourceToCopy(persisted?.ClaimSource, sourceId) is { } copied)
                    {
                        claimSourceChanged = true;
                        var chosen = await contactDirectory.FindActiveByRoleAsync(
                                actor, ContactRole.ClaimSource, copied, cancellationToken)
                            ?? throw new InvalidOperationException("The selected claim source is not an active Claim source record.");
                        claimSource = CaseDetailsSaveRules.ClaimSourceCopy(chosen);
                    }
                }
                // The contact boxes show the effective contact; Core keeps an
                // override only where the posted value differs from the copy
                // (CaseWorkspaceClaimSource.SubmittedOverride).
                if (claimSource is not null && !claimSourceChanged)
                {
                    claimSource = claimSource with
                    {
                        OverrideContactName = SubmittedText(
                            nameof(claimSourceContactName),
                            claimSourceContactName,
                            claimSource.OverrideContactName),
                        OverrideContactTelephone = SubmittedText(
                            nameof(claimSourceContactTelephone),
                            claimSourceContactTelephone,
                            claimSource.OverrideContactTelephone),
                        OverrideContactEmailAddress = SubmittedText(
                            nameof(claimSourceContactEmail),
                            claimSourceContactEmail,
                            claimSource.OverrideContactEmailAddress)
                    };
                }
                // A directory organisation is copied onto the Case as it is
                // linked — its identity, its version and its own name and
                // address — so a later directory edit never rewrites this
                // Case (FRD-04). Every save carries the select back, so the
                // link the Case already holds is not linked again: it keeps
                // the copy it has, whether or not the record is still active,
                // and the name and address posted are the Case's own
                // (operator, 12 November 2026, R1022). Without a link the
                // Case keeps the extracted or keyed text, and the member of
                // staff saving it is its confirmation.
                var linkKept = CaseDetailsSaveRules.KeepsRepairerLink(persisted?.Repairer, repairerDirectoryId);
                var linkedRepairer = overviewSubmitted
                    && CaseDetailsSaveRules.RepairerToLink(persisted?.Repairer, repairerDirectoryId) is { } directoryId
                    ? await contactDirectory.FindActiveByRoleAsync(
                            actor, ContactRole.Repairer, directoryId, cancellationToken)
                        ?? throw new InvalidOperationException("The selected repairer is not an active directory Repairer.")
                    : null;
                // An unposted selector is not an unlink: only the rendered
                // control, which offers "not linked" explicitly, may clear it.
                var unlinked = Posted(nameof(repairerDirectoryId)) && !linkKept;
                var repairer = CaseDetailsSaveRules.Repairer(
                    persisted?.Repairer,
                    linkedRepairer,
                    unlinked,
                    Submitted(nameof(repairerName), repairerName, Accepted(data.Inspection.RepairerName)?.Value));
                var inspectionSubmitted = new[] { nameof(inspectionAddress), nameof(storageLocation), nameof(inspectionDate),
                    nameof(inspectionDeadline), nameof(storagePerDay), nameof(recoveryCharge) }.Any(Posted);
                var vehicleSubmitted = new[] { nameof(vehicleRegistration), nameof(vehicleMake), nameof(vehicleModel),
                    nameof(vehicleYear), nameof(vehicleMileage), nameof(vehicleMileageUnit) }.Any(Posted)
                    || assessmentFields.ContainsKey(AssessmentVocabulary.HistoryCheck)
                    || assessmentFields.ContainsKey(AssessmentVocabulary.VehicleCondition)
                    || vehicleIdentityFields.Count > 0;
                // The history check and pre-incident condition are Engineer
                // fields the Vehicle section renders; they travel with its request.
                foreach (var path in new[] { AssessmentVocabulary.HistoryCheck, AssessmentVocabulary.VehicleCondition })
                {
                    if (assessmentFields.TryGetValue(path, out var vehicleFinding))
                    {
                        vehicleIdentityFields[path] = vehicleFinding;
                    }
                }
                var markers = !Posted(nameof(damageMarkers))
                    ? null
                    : AssessmentPolicy.ParseMarkers(damageMarkers);
                var recordedDate = CaseDetailsSaveRules.RecordedReportDate(Recorded(AssessmentVocabulary.ReportDate));
                // The guide source cards have no Save of their own (23 September
                // 2026): this save records the ones whose boxes were changed, and
                // records the calculation when the operator changed it.
                List<ValuationDetails> guideValuations = guideEntries is { Length: > 0 }
                    ? GuideEntriesToRecord(guideEntries, recordedCards)
                    : [];
                var workspaceSave = await saveCaseWorkspace.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken)
                {
                    Work = work,
                    // Save as you go: the session's lease is kept and its
                    // next token issued inside the save (R770).
                    ContinuesSession = true,
                    Valuation = guideValuations.Count == 0 && adoption is null && valuationFields.Count == 0
                        ? null
                        : new(guideValuations, adoption, valuationFields.Count == 0 ? null : valuationFields),
                    Estimate = estimate,
                    Overview = !overviewSubmitted ? null : new(
                        Submitted(nameof(claimantName), claimantName, Accepted(data.Claimant.Name)?.Value),
                        Submitted(nameof(claimantContactNumber), claimantContactNumber, Accepted(data.Claimant.ContactNumber)?.Value),
                        Submitted(nameof(claimantAddress), claimantAddress, Accepted(data.Claimant.Address)?.Value),
                        Submitted(nameof(claimNumber), claimNumber, Accepted(data.Claim.Number)?.Value),
                        Submitted(nameof(incidentDate), incidentDate, Accepted(data.Accident.IncidentDate)?.Value),
                        Submitted(nameof(accidentCircumstances), accidentCircumstances, Accepted(data.Accident.Circumstances)?.Value),
                        Submitted(nameof(vatStatus), vatStatus, Accepted(data.Instruction.VatStatus)?.Value),
                        linkedRepairer?.Address
                            ?? Submitted(nameof(repairerAddress), repairerAddress,
                                Accepted(data.Inspection.RepairerAddress)?.Value),
                        claimSource,
                        repairer,
                        Submitted(nameof(principalNotes), principalNotes, persisted?.PrincipalNotes),
                        Submitted(nameof(claimSourceNotes), claimSourceNotes, persisted?.ClaimSourceNotes),
                        Submitted(
                            nameof(dueBy),
                            dueBy,
                            current.Workflow.DueWork?.DueBy
                                ?? Accepted(data.Inspection.Deadline)?.Value)),
                    Inspection = !inspectionSubmitted ? null : new(treatment, address, persisted?.InspectionLocationProvenance,
                        Submitted(nameof(storageLocation), storageLocation, Accepted(data.Inspection.StorageLocation)?.Value),
                        Submitted(nameof(inspectionDate), inspectionDate, Accepted(data.Inspection.InspectionDate)?.Value),
                        Submitted(nameof(inspectionDeadline), inspectionDeadline, Accepted(data.Inspection.Deadline)?.Value),
                        persisted?.InspectionVehiclePresent, persisted?.InspectionCondition,
                        persisted?.InspectionNotes,
                        Submitted(nameof(storagePerDay), storagePerDay, Money(AssessmentVocabulary.SettlementStoragePerDay)),
                        Submitted(nameof(recoveryCharge), recoveryCharge, Money(AssessmentVocabulary.CostRecoveryCharge))),
                    Vehicle = !vehicleSubmitted ? null : new(
                        Submitted(nameof(vehicleRegistration), vehicleRegistration, Accepted(data.Vehicle.Registration)?.Value),
                        Submitted(nameof(vehicleMake), vehicleMake, Accepted(data.Vehicle.Make)?.Value),
                        Submitted(nameof(vehicleModel), vehicleModel, Accepted(data.Vehicle.Model)?.Value),
                        new(mileageValue,
                            originalUnit,
                            persisted?.VehicleMileageDisplayUnit),
                        vehicleIdentityFields.Count > 0 ? vehicleIdentityFields : null,
                        Submitted(nameof(vehicleYear), vehicleYear, Accepted(data.Vehicle.Year)?.Value)),
                    Damage = !damageSubmitted ? null : new(markers, damageFields),
                    ImagePreparation = !preparationSubmitted ? null : new(
                        [.. preparationEdits!.Select(edit => edit.ToRequest())]),
                    Settlement = settlementFields.Count == 0 ? null : new(settlementFields),
                    Report = !reportSubmitted ? null : new(reportFields,
                        Submitted(nameof(signOffEngineerId), signOffEngineerId, current.Workflow.SignOffEngineerId),
                        Submitted(nameof(reportDate), reportDate, recordedDate)),
                    // v28 P30: wording that reads the same as the composed
                    // sentence is no change, so the block keeps tracking its
                    // fields. The composed sentence is read here rather than
                    // posted back, so the form cannot claim one it never saw.
                    ReportWording = !wordingSubmitted ? null : await ReportWordingOf(
                        id, actor, work, wordingEdits!, cancellationToken)
                }, cancellationToken);
                RecordEditorCommit("case-edit-form", operationKey, expectedVersion, workspaceSave.Version);
                savedData = workspaceSave.Data;
                if (workspaceSave.SessionLease is { } keptLease)
                {
                    ContinueSession(keptLease.Token);
                }
                if (workspaceSave.Estimate is { } writtenEstimate)
                {
                    savedEstimate = writtenEstimate.SpecificationId.ToString("D");
                    savedEstimateId = writtenEstimate.SpecificationId;
                    savedEstimateLineIdentities = EstimateLineIdentitiesOf(postedEstimateLineRows, writtenEstimate);
                }

                if (saveUnroadworthyReason)
                {
                    try
                    {
                        var saved = await saveUnroadworthyReasonAction.ExecuteAsync(
                            new(bankWording!, actor),
                            cancellationToken);
                        bankStatus = saved is null
                            ? Labels.UnroadworthyReasons.AlreadyOffered
                            : "The wording was saved to the bank.";
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        bankError = MutationRefusalMessage(
                            exception, "The wording was not saved to the bank. Retry the operation.");
                    }
                }
            },
            "Case saved.",
            // Save as you go (operator, 29 September 2026): every save is a
            // commit of the open edit session and the page keeps editing. The
            // save keeps the lease and issues its next token itself (R770);
            // the reclaim stays for a replay that handed no lease back. Done
            // releases it.
            caseId => RedirectToSection(caseId, section, savedEstimate),
            keepEditing: true,
            // A refusal names its own reason (a field's value, a spec line, a
            // valuation figure); a lost lease or a changed Case keeps the
            // shared one.
            refusal: SaveRefusalMessage);

        if (bankError is not null)
        {
            Say(StatusTempDataKey, null);
            Say(ErrorTempDataKey, bankError);
        }
        else if (bankStatus is not null)
        {
            Say(StatusTempDataKey, bankStatus);
        }

        // The command ran (a refusal redirects too); only Forbid has nothing to answer.
        return AnswersInPlace && result is RedirectToPageResult
            ? await AnswerCommitAsync(id, section, preparationEdits is { Length: > 0 }, savedData, result, cancellationToken)
            : result;
    }

    private bool Posted(string field) => Request.HasFormContentType && Request.Form.ContainsKey(field);

    private T Submitted<T>(string field, T submitted, T recorded) => Posted(field) ? submitted : recorded;

    private string? SubmittedText(string field, string? submitted, string? recorded) =>
        Posted(field) ? Request.Form[field].ToString() : recorded;

    public static CaseDataValue<T>? Accepted<T>(CaseField<T>? field) where T : notnull =>
        field?.Confirmed ?? field?.Fact;

    private async Task<CaseWorkspaceReportWording> ReportWordingOf(
        Guid caseId,
        ActionActor actor,
        CaseWorkSelector work,
        IReadOnlyList<ReportWordingEditForm> edits,
        CancellationToken cancellationToken)
    {
        var wording = await Pegasus.Web.Presentation.ReportWordingEdits.ReadAsync(
            reportSnapshotSource, caseId, actor, work, clock.GetUtcNow(),
            Pegasus.Web.Presentation.ReportWordingEdits.UnavailableOnPage, cancellationToken);
        return new([.. edits.Select(edit => edit.ToRecord(wording.Snapshot, wording.Presentation))]);
    }

    private static ObjectResult EnhancedPreviewRefusal(string detail)
    {
        var refusal = new ObjectResult(new ProblemDetails
        {
            Detail = detail,
            Status = StatusCodes.Status422UnprocessableEntity,
        })
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
        };
        refusal.ContentTypes.Add("application/problem+json");
        return refusal;
    }

    /// <summary>
    /// What every section command on the record requires before it touches
    /// the case: an authorized actor, an assessment this command may open
    /// (never a read-only one; R1095), a live form, and an edit lease unless
    /// the command claims one for itself. Only the section the refusal lands on
    /// differs between the Report, Valuation and Files commands, so the
    /// checks themselves are written once.
    /// </summary>
    private async Task<IActionResult?> GuardSectionCommandAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Func<IActionResult> redirect,
        CancellationToken cancellationToken,
        bool requireLease = true)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access is null || !access.CanOpen)
        {
            return NotFound();
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return redirect();
        }
        if (requireLease && string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["CaseError"] = NotInEditMode;
            return redirect();
        }

        return null;
    }

    /// <summary>
    /// One posted image edit. The form carries what the operator chose; the
    /// order only reaches Core for the one role that owns one, so switching a
    /// supporting image to another role in the same submit clears its order
    /// instead of being refused for carrying one.
    /// </summary>
    /// <summary>
    /// One report wording block as the Report section posts it back (v28 P30).
    /// A heading or wording matching the composed one is stored as no change,
    /// so a block the Engineer left alone keeps tracking its fields.
    /// </summary>
    public sealed class ReportWordingEditForm
    {
        public string Key { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Text { get; set; }

        public int? Order { get; set; }

        public bool Included { get; set; }

        public bool Manual { get; set; }

        // The browser posts a line break as two characters; the composed
        // sentence it is compared with holds one, which the shared rule
        // normalises.
        public CaseReportWording ToRecord(
            AssessmentReportSnapshot? snapshot,
            AssessmentReportPresentation? presentation) =>
            Pegasus.Web.Presentation.ReportWordingEdits.ToRecord(
                Key, Title, Text, Order, Included, Manual, snapshot, presentation);
    }

    public sealed class AssetPreparationEditForm
    {
        public Guid OccurrenceId { get; set; }

        public long ExpectedPreparationVersion { get; set; }

        public int? Order { get; set; }

        public int Rotation { get; set; }

        public decimal CropLeft { get; set; }

        public decimal CropTop { get; set; }

        public decimal CropWidth { get; set; }

        public decimal CropHeight { get; set; }

        public bool FullPage { get; set; }

        public CaseAssetPreparationEdit ToRequest() =>
            new(
                OccurrenceId,
                ExpectedPreparationVersion,
                Order,
                (CaseAssetRotation)Rotation,
                new(CropLeft, CropTop, CropWidth, CropHeight),
                FullPage);
    }

    /// <summary>
    /// The Repair Spec editor as the Case form posts it: the specification it
    /// shows, whole, read on exactly the terms the estimate editor always read
    /// it. A line that does not read as a number refuses the save.
    /// </summary>
    private async Task<CaseWorkspaceEstimate> EstimateEditorPartAsync(
        Guid caseId, ActionActor actor, CancellationToken cancellationToken)
    {
        var editor = ReadEditorPost();
        if (editor.Lines is null)
        {
            throw new InvalidOperationException(
                "Check the estimate's lines: an operation, a quantity, hours or an amount does not read as a number.");
        }

        postedEstimateLineRows = editor.LineRows;
        var existing = await ResolveEstimateAsync(caseId, editor.EstimateId, cancellationToken);
        var details = EditorDetailsFrom(editor, existing);
        var selectedRateCard = ParseSelectedRateCard();
        return new(
            editor.EstimateId,
            details,
            editor.Lines,
            editor.ExistingLineIds,
            await ReadSupplementaryAsync(caseId, actor, existing, details, editor, cancellationToken))
        {
            SelectedRateCardId = selectedRateCard.Id,
            SelectedRateCardVersion = selectedRateCard.Version,
        };
    }

    /// <summary>
    /// What the editor said about the frozen version this spec supplements
    /// (v28 P20; v38 P3): the version the import named, the reason and whether
    /// the report explains it. Dismiss posts no version, which clears it. The
    /// statement is composed from the version and the lines as posted, so the
    /// record and the report say the same thing.
    /// </summary>
    private async Task<RepairSpecificationSupplementary?> ReadSupplementaryAsync(
        Guid caseId,
        ActionActor actor,
        RepairSpecificationVersion? existing,
        EstimateDetails details,
        EstimateEditorPost editor,
        CancellationToken cancellationToken)
    {
        if (existing is null || !Guid.TryParse(Request.Form["supplementaryOf"], out var baseId) || baseId == Guid.Empty)
        {
            return null;
        }
        var reason = Request.Form["supplementaryReason"].ToString();
        if (RepairSpecificationComparison.SupplementaryReasons.All(item => item.Code != reason))
        {
            reason = RepairSpecificationComparison.SupplementaryReasons[0].Code;
        }
        var baseVersion = await specificationSnapshots.GetAsync(
            caseId, existing.SpecificationId, baseId, cancellationToken);
        if (baseVersion is null)
        {
            return null;
        }
        // The spec a Save would record: the save's own carry, so a stored
        // Specialist line compares as its own kind and its hours stay priced.
        var diff = RepairSpecificationComparison.Compare(
            baseVersion,
            EstimatePolicy.Edited(
                caseId, actor, existing, details, editor.Lines!, editor.ExistingLineIds, DateTimeOffset.UtcNow));
        var explain = bool.TryParse(Request.Form["supplementaryExplain"].FirstOrDefault(), out var flag) && flag;
        return new(baseId, reason, explain, RepairSpecificationComparison.SupplementaryStatement(diff, reason));
    }

    /// <summary>
    /// The estimate header the editor posted. The rate card is kept only
    /// while the posted rate is still the one it priced: a retyped rate is
    /// the Engineer's own, not the card's. Every other header fact — the
    /// discounts and the VAT policy — is now posted, so nothing is carried
    /// forward unseen.
    /// </summary>
    private (Guid? Id, long? Version) ParseSelectedRateCard()
    {
        var value = Request.Form["selectedRateCard"].ToString();
        if (string.IsNullOrWhiteSpace(value)) return (null, null);
        var parts = value.Split(':');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var id)
            || !long.TryParse(parts[1], out var version) || version < 1)
            throw new ArgumentException("Select a current labour-rate card.");
        return (id, version);
    }

    private static readonly HashSet<string> CaseDataMembers = typeof(CaseEditableData)
        .GetProperties()
        .Select(property => property.Name)
        .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// What a refused Case save tells staff (FRD-16; operator, 17 November
    /// 2026, R1118): Core's own sentence for the value it refused, with the
    /// field's label in front when the sentence names no field. A lost lease
    /// or a changed Case gives none here and keeps the shared sentence.
    /// </summary>
    private static string SaveRefusalMessage(Exception exception)
    {
        if (exception is not ArgumentException argument || exception is ArgumentNullException)
        {
            return MutationRefusalMessage(exception, string.Empty);
        }

        if (argument.ParamName is not { Length: > 0 } member)
        {
            return argument.Message;
        }

        // The parameter is Core's own name for the value, never one staff sent.
        var sentence = argument.Message.Replace(
            $" (Parameter '{member}')", string.Empty, StringComparison.Ordinal);
        if (!CaseDataMembers.Contains(member))
        {
            return sentence;
        }

        var label = FieldLabel(char.ToLowerInvariant(member[0]) + member[1..]);
        return sentence.Contains(label, StringComparison.OrdinalIgnoreCase)
            ? sentence
            : $"{label}: {sentence}";
    }

    /// <summary>
    /// The refusal for a value the server could not read into its box, such
    /// as 1e5 in Mileage: "Mileage is invalid." (R1118). A value inside a
    /// composite row has no label of its own and keeps the general sentence.
    /// </summary>
    private string UnreadableValueMessage()
    {
        var field = ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .Select(entry => entry.Key)
            .FirstOrDefault(key => key.Length > 0 && key.All(char.IsAsciiLetterOrDigit));
        return field is null ? "A submitted Case field is invalid." : $"{FieldLabel(field)} is invalid.";
    }

    private static string MutationRefusalMessage(Exception exception, string fallback) =>
        exception is InvalidOperationException
            and not CaseVersionConflictException
            and not CaseEditLeaseConflictException
            and not CaseEditLeaseExpiredException
            and not CaseOperationConflictException
            ? exception.Message
            : fallback;

    private async Task<bool> HasAssessmentAccessAsync(
        Guid caseId,
        ActionActor actor,
        CancellationToken cancellationToken) =>
        (await getAssessmentAccess.ExecuteAsync(
            new(caseId, actor),
            cancellationToken))?.CanOpen == true;

    /// <summary>
    /// A GET's access answer, from the workflow its own read already carries.
    /// It decides only which controls render: it neither restores cookies nor
    /// grants a lease, and every POST repeats Core authorization against the
    /// live record.
    /// </summary>
    private void ApplyAssessmentAccess(ActionActor actor, CaseWorkflowRecord workflow)
    {
        var access = AssessmentAccessPolicy.For(actor, workflow);
        AssessmentIsReadOnly = access.IsReadOnly;
        AssessmentCanOpen = access.CanOpen;
    }

    private static bool IsOperationKeyValid(string value) =>
        Guid.TryParseExact(value, "N", out var operationId) && operationId != Guid.Empty;

    /// <summary>The page state the frame's extra reads start from.</summary>
    private sealed record WorkspaceExtrasInputs(
        CasePageFrame Details,
        ActionActor Actor,
        bool AssessmentCanOpen,
        IReadOnlyList<SignOffEngineerProfile> EligibleSignOffEngineers,
        IReadOnlyList<SignOffEngineerProfile>? SnapshotSignOffEngineers,
        string? LeaseToken);

    private sealed record WorkspaceExtras(
        string? EngineerDisplayName,
        string SignOffEngineerDisplayName,
        AssignEngineerViewModel AssignEngineerChoices);

    /// <summary>
    /// The values the workspace frame names that the case projection does not
    /// carry directly: the assigned and Sign-off Engineer names, and the
    /// Engineer choices available in Review and With Engineer.
    /// </summary>
    private async Task<WorkspaceExtras> ReadWorkspaceExtrasAsync(
        WorkspaceExtrasInputs inputs,
        CancellationToken cancellationToken)
    {
        var details = inputs.Details;
        var workflow = details.Workflow;
        // Where Assign Engineer runs, its choices are the enabled staff, the
        // signed-in person first, as the Triage Assign list reads them (#1213).
        // They name the assigned Engineer too, so that account's name is read on
        // its own only when the list does not hold it.
        var viewerStaffId = Pegasus.Core.Notifications.StaffNotificationPolicy.StaffId(inputs.Actor);

        // The sign-off list is the one the report snapshot already read this
        // request (R276); it is read here only when no snapshot ran, and then
        // beside the roster and name reads, which do not depend on it.
        using var reads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var fallbackProfiles = !inputs.AssessmentCanOpen && inputs.SnapshotSignOffEngineers is null
            ? reads.Start(token => staffAccountQueries.ListSignOffEngineersAsync(token))
            : null;
        var rosterAndName = reads.Start(async token =>
        {
            IReadOnlyList<CaseEngineerChoice> listed = CaseLifecycleRules.CanBeAssigned(workflow.State) || workflow.HasOpenQuery
                ? [.. (await caseEngineerChoices.GetAsync(inputs.Actor, token))
                    .OrderBy(choice => choice.StaffId == viewerStaffId ? 0 : 1)]
                : [];
            string? displayName = null;
            if (workflow.AssignedEngineerId is { } engineerId)
            {
                displayName = listed.FirstOrDefault(choice => choice.StaffId == engineerId)?.DisplayName;
                if (displayName is null)
                {
                    var names = await staffAccountQueries.GetNamesAsync([engineerId], token);
                    displayName = names.TryGetValue(engineerId, out var name)
                        ? name
                        : ActorDisplayNames.UnknownStaff;
                }
            }

            return (Roster: listed, EngineerDisplayName: displayName);
        });
        await reads.WhenAllAsync();
        var (roster, engineerDisplayName) = await rosterAndName;
        var profiles = inputs.AssessmentCanOpen ? inputs.EligibleSignOffEngineers
            : inputs.SnapshotSignOffEngineers
                ?? await fallbackProfiles!;
        var signOffEngineer = CaseSignOffEngineerResolver.Resolve(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles);
        var signOffEngineerDisplayName = signOffEngineer?.PrintedName
            ?? Labels.CaseWorkspace.Unassigned;

        return new(
            engineerDisplayName,
            signOffEngineerDisplayName,
            new(roster, viewerStaffId));
    }

    /// <summary>
    /// Reads the retained values only for the case they were submitted against. A refusal on one
    /// case survives a visit to another, so nothing is consumed until it belongs to this page.
    /// </summary>
    private void RestoreProposedValues(Guid caseId)
    {
        if (PeekGuid(ProposedValuesCaseIdKey) != caseId)
        {
            TempData.Keep(ProposedValuesCaseIdKey);
            TempData.Keep(ProposedValuesKey);
            TempData.Keep(ProposedValuesDroppedKey);
            TempData.Keep(ProposedValuesShortenedKey);
            return;
        }

        TempData.Remove(ProposedValuesCaseIdKey);
        var payload = TempData[ProposedValuesKey] as string;
        ProposedValuesWereDropped = TempData[ProposedValuesDroppedKey] is true;
        ProposedValuesWereShortened = TempData[ProposedValuesShortenedKey] is true;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        RetainedProposedValue[]? retained;
        try
        {
            retained = JsonSerializer.Deserialize<RetainedProposedValue[]>(payload);
        }
        catch (JsonException)
        {
            ProposedValuesWereDropped = true;
            return;
        }

        ProposedValues = retained is null
            ? []
            : retained
                .Select(value => new ProposedCaseValue(
                    FieldLabel(value.Field),
                    DisplayValue(value.Field, value.Value),
                    CurrentValue(value.Field)))
                .ToArray();
    }

    /// <summary>
    /// Renders a proposed checkbox value in the same words as the current one, so the two columns
    /// compare rather than reading "true" beside "Yes".
    /// </summary>
    private string DisplayValue(string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Labels.CaseWorkspace.AbsentValue;
        if (field == "signOffEngineerId")
        {
            return Guid.TryParse(value, out var id)
                ? EligibleSignOffEngineers.FirstOrDefault(engineer => engineer.StaffId == id)?.PrintedName
                    ?? "Unavailable sign-off Engineer"
                : "Unavailable sign-off Engineer";
        }
        return EditorLabels.Label(field) is not null && bool.TryParse(value, out _)
            ? YesOrNo(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) : value;
    }

    private static string YesOrNo(bool value) => value ? "Yes" : "No";

    private string? CurrentValue(string field)
    {
        if (field.StartsWith("assessmentFields[", StringComparison.Ordinal) && field.EndsWith(']'))
        {
            var value = AssessmentEditorValue(field[17..^1]);
            return DisplayValue(field, value ?? string.Empty);
        }
        if (field == "signOffEngineerId") return SignOffEngineerDisplayName;
        if (field == "storagePerDay") return AssessmentValue(AssessmentVocabulary.SettlementStoragePerDay);
        if (field == "recoveryCharge") return AssessmentValue(AssessmentVocabulary.CostRecoveryCharge);
        if (field == "reportDate") return AssessmentValue(AssessmentVocabulary.ReportDate);
        if (Case?.Data is not { } data)
        {
            return null;
        }

        return field switch
        {
            "claimantName" => Accepted(data.Claimant.Name)?.Value,
            "claimNumber" => Accepted(data.Claim.Number)?.Value,
            "vehicleRegistration" => Accepted(data.Vehicle.Registration)?.Value,
            "vehicleMake" => Accepted(data.Vehicle.Make)?.Value,
            "vehicleModel" => Accepted(data.Vehicle.Model)?.Value,
            "vehicleYear" => Accepted(data.Vehicle.Year)?.Value,
            "vehicleMileage" => Accepted(data.Vehicle.Mileage)?.Value.ToString(
                CultureInfo.InvariantCulture),
            "vehicleMileageUnit" => Accepted(data.Vehicle.MileageUnit)?.Value,
            "accidentCircumstances" => Accepted(data.Accident.Circumstances)?.Value,
            "incidentDate" => Accepted(data.Accident.IncidentDate)?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "dueBy" => (Case?.Workflow.DueWork?.DueBy
                    ?? Accepted(data.Inspection.Deadline)?.Value)
                ?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "vatStatus" => Accepted(data.Instruction.VatStatus)?.Value,
            "inspectionDate" => Accepted(data.Inspection.InspectionDate)?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "inspectionDeadline" => Accepted(data.Inspection.Deadline)?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "inspectionAddress" => Accepted(data.Inspection.Address)?.Value,
            "storageLocation" => Accepted(data.Inspection.StorageLocation)?.Value,
            "claimSourceContactName" => data.Workspace?.ClaimSource?.OverrideContactName,
            "claimSourceContactTelephone" => data.Workspace?.ClaimSource?.OverrideContactTelephone,
            "claimSourceContactEmail" => data.Workspace?.ClaimSource?.OverrideContactEmailAddress,

            // The corrected-vehicle-suggestion form posts unprefixed names against the same case
            // fields, so the case's confirmed vehicle values are what it is compared with.
            "registration" => Accepted(data.Vehicle.Registration)?.Value,
            "make" => Accepted(data.Vehicle.Make)?.Value,
            "model" => Accepted(data.Vehicle.Model)?.Value,
            "mileage" => Accepted(data.Vehicle.Mileage)?.Value.ToString(
                CultureInfo.InvariantCulture),
            "mileageUnit" => Accepted(data.Vehicle.MileageUnit)?.Value,
            _ => null
        };
    }

    private static string FieldLabel(string field) => EditorLabels.Label(field) ?? (field switch
    {
        "claimantName" => "Claimant",
        "claimNumber" => "Claim number",
        "vehicleRegistration" => "Registration",
        "vehicleMake" => "Vehicle make",
        "vehicleModel" => "Vehicle model",
        "vehicleYear" => "Year",
        "vehicleMileage" => "Mileage",
        "vehicleMileageUnit" => "Mileage unit",
        "accidentCircumstances" => "Accident circumstances",
        "incidentDate" => "Incident date",
        "dueBy" => FrameLabels.Due,
        "vatStatus" => FrameLabels.VatStatus,
        "inspectionDate" => "Inspection date",
        "inspectionDeadline" => "Inspection deadline",
        "inspectionAddress" => "Inspection address",
        "storageLocation" => Labels.CaseWorkspace.StorageLocation,
        "claimSourceContactName" => FrameLabels.ContactName,
        "claimSourceContactTelephone" => FrameLabels.ContactPhone,
        "claimSourceContactEmail" => FrameLabels.ContactEmail,
        "reason" => "Reason",
        _ => Humanize(field)
    });

    private static string Humanize(string field)
    {
        var text = new StringBuilder(field.Length + 8);
        foreach (var character in field)
        {
            if (char.IsUpper(character) && text.Length > 0)
            {
                text.Append(' ');
                text.Append(char.ToLowerInvariant(character));
                continue;
            }

            text.Append(text.Length == 0 ? char.ToUpperInvariant(character) : character);
        }

        return text.ToString();
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "The authorized case detail query failed for case {CaseId}.")]
    private static partial void LogCaseDetailsQueryFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);
}

/// <summary>
/// One field of a refused submission beside the value the case now holds, for comparison only.
/// </summary>
public sealed record ProposedCaseValue(string Label, string Proposed, string? Current);
