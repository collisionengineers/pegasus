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
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
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

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
[RequestSizeLimit(ImportRawEstimate.MaximumDocumentBytes + 64 * 1024)]
public sealed partial class DetailsModel(
    IGetCaseHeader getCaseHeader,
    IGetCaseEditBasis getCaseEditBasis,
    ICaseWorkflowQueries caseWorkflows,
    IGetCasePageFrame getCasePageFrame,
    IGetCaseVehicleSection getCaseVehicleSection,
    IGetCaseValuationSection getCaseValuationSection,
    IGetCaseNotesSection getCaseNotesSection,
    IGetCaseFilesSection getCaseFilesSection,
    ICaseDocumentQueries caseDocuments,
    IListCaseReferences listCaseReferences,
    IGetAssessmentAccess getAssessmentAccess,
    IGetAssessmentWorkspace getAssessmentWorkspace,
    ICaseReportSnapshotSource reportSnapshotSource,
    TimeProvider clock,
    ICreateAiJob createAiJob,
    ISendToAiControl sendToAiControl,
    IRenderCaseEstimateDocument renderEstimateDocument,
    IEstimateDocumentPresentationStore estimateDocumentPresentations,
    IGeneratedCaseArtifactStore generatedArtifacts,
    ICaseReportGenerationStore reportGenerations,
    ISendCaseReport sendCaseReport,
    IReportRecipientSuggestionQueries reportRecipientSuggestions,
    ICaseReportSendHistoryQueries reportSendHistory,
    RenderEmailTemplate renderEmailTemplate,
    IListCaseEstimates listEstimates,
    LabourRateCardAdministration labourRateCards,
    IDuplicateEstimate duplicateEstimate,
    IDiscardEstimate discardEstimate,
    ISetCurrentEstimate setCurrentEstimate,
    IRepairSpecificationStore repairSpecifications,
    IImportRawEstimate importRawEstimate,
    IEnumerable<IEstimateDocumentParser> estimateParsers,
    IRepairSpecificationSnapshotStore specificationSnapshots,
    IUnroadworthyReasonBankStore unroadworthyReasonBank,
    IPrincipalSalvageMatrixQueries principalSalvageMatrices,
    ISaveUnroadworthyReason saveUnroadworthyReasonAction,
    IScaleRepairSpecification scaleRepairSpecification,
    IRemoveRepairSpecificationScaling removeRepairSpecificationScaling,
    IRestoreRepairSpecificationSnapshot restoreRepairSpecificationSnapshot,
    IAddCaseDocument addCaseDocument,
    ICaseAssetPreparationQueries caseAssetPreparationQueries,
    IAcquireCaseEditLease acquireLease,
    IHeartbeatCaseEditLease heartbeatLease,
    IResumeCaseEditLease resumeLease,
    IReleaseCaseEditLease releaseLease,
    ISaveCaseWorkspace saveCaseWorkspace,
    IInspectionAddressChoicesQueries inspectionAddressChoicesQueries,
    IContactDirectoryQueries contactDirectory,
    IImageIntakeQueries imageIntakeQueries,
    IReadImageTagVocabulary readImageTagVocabulary,
    IListCaseValuations listCaseValuations,
    IDescribeCaseEditAuthorityHolder describeEditAuthorityHolder,
    IStaffAccountQueries staffAccountQueries,
    IEvaSubmissionModeStore evaModeStore,
    IEvaSubmissionQueries evaSubmissionQueries,
    IPerUserExternalCredentialReader externalCredentials,
    IGlassRepairEstimateSessionReader glassSessions,
    ICreateAudit createAudit,
    IAiJobQueries aiJobs,
    ICaseWorkflowConfiguration workflowConfiguration,
    IStartMarketResearch startMarketResearch,
    IFetchGuideValuation fetchGuideValuation,
    IListValuationPresets listValuationPresets,
    IPreviewValuationCalculation previewValuation,
    IListAppliedValuations listAppliedValuations,
    ILogger<DetailsModel> logger,
    IGetCaseKind getCaseKind,
    IValidateCaseRenderLease? validateCaseRenderLease = null,
    ISubmitCaseToEva? submitCaseToEva = null,
    IStaffMailSend? staffMailSend = null) : CaseMutationPageModel(logger)
{
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
    /// The Case's recorded valuation source cards (B01 port/B03): one card
    /// per source with its figures, loaded with the valuation section.
    /// </summary>
    public IReadOnlyList<CaseValuation> Valuations { get; private set; } = [];
    public IReadOnlyList<LabourRateCard> LabourRateCards { get; private set; } = [];

    public IReadOnlyList<InspectionAddressChoice> InspectionAddressChoices { get; private set; } = [];

    /// <summary>
    /// The Contacts directory's Repairer organisations, offered so a member of
    /// staff can link this Case's repairer to a maintained record.
    /// Loaded only while the record is being edited.
    /// </summary>
    public IReadOnlyList<ContactDirectoryRecord> RepairerChoices { get; private set; } = [];

    /// <summary>
    /// The active Claim source records the Overview's claim source select
    /// offers while the record is being edited; each option carries the
    /// record's notes and contact line so the notes band follows a change
    /// without another request.
    /// </summary>
    public IReadOnlyList<ContactDirectoryRecord> ClaimSourceChoices { get; private set; } = [];

    /// <summary>
    /// "is-collapsed" when this browser folded the panel <paramref name="collapseKey"/>
    /// (its <c>data-collapse</c> value), so the first paint is already folded.
    /// </summary>
    public string? CollapsedClass(string collapseKey) =>
        Pegasus.Web.Presentation.ShellPreferences.PanelCollapsed(Request, collapseKey) ? "is-collapsed" : null;

    /// <summary>
    /// "is-active" on the addressed section when this browser's layout is
    /// Tabs, so a Tabs first paint shows that section rather than none.
    /// </summary>
    public string? ActiveTabClass(string sectionKey) =>
        Pegasus.Web.Presentation.ShellPreferences.CaseLayout(Request) == "tabs"
        && string.Equals(sectionKey, Section, StringComparison.Ordinal)
            ? "is-active"
            : null;

    /// <summary>
    /// The associated Vehicle images records whose photographs are still the
    /// record's own. Once a record's merge files them they are Case images,
    /// drawn once as tiles, and the record has no group here.
    /// </summary>
    public IReadOnlyList<ImageIntakeSummary> ImageIntakes { get; private set; } = [];

    /// <summary>
    /// The shared image-tag vocabulary, loaded only when the Files section
    /// renders and the operator holds the edit lease: a read-only visit's
    /// tiles draw their tags as chips alone, so they never ask for it.
    /// </summary>
    public IReadOnlyList<ImageTag> TagVocabulary { get; private set; } = [];

    /// <summary>
    /// The gallery entries for each associated Image-initiated Case, loaded
    /// only when the Files section's body is being rendered.
    /// </summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>> ImagesByIntake { get; private set; } =
        new Dictionary<Guid, IReadOnlyList<ImageIntakeImage>>();

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
            ["notes"] = "/Pages/Cases/Shared/_CaseHistory.cshtml"
        };

    /// <summary>
    /// Whether <paramref name="key"/> is fetched rather than rendered with the
    /// first response. The addressed section is always rendered, so
    /// <c>?section=</c> works over plain HTTP. Files and Notes have no fields
    /// in the record's single Save form, so they remain deferred while editing
    /// without replacing entered values elsewhere; their bodies act through
    /// their own posts with the render lease token.
    /// </summary>
    public bool SectionIsDeferred(string key) =>
        !string.Equals(key, Section, StringComparison.Ordinal)
        // A nested section's parent renders with it, so the reader lands on both.
        && !string.Equals(key, SectionLinkKey, StringComparison.Ordinal)
        && !(string.Equals(Section, "vehicle", StringComparison.Ordinal) && IsNestedSection(key))
        && LazySectionViews.ContainsKey(key)
        && (LeaseToken is null || key is "files" or "notes");

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

    public EvaHandoffViewModel? EvaHandoff { get; private set; }

    /// <summary>
    /// The requirements the current configured policy reports as unmet,
    /// accompanied by the Case's due-work reason where available.
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
            if (OriginalReportMissing)
            {
                requirements.Add(new("Original report missing", "Audit", null));
            }
            requirements.AddRange(data.Completeness.Evaluation.MissingRequirements
                .Select(requirement => new CaseRequirement(
                    Pegasus.Web.Presentation.OperatorLabels.RequirementIncomplete(requirement), "Case requirements", why)));
            return requirements;
        }
    }

    public bool OriginalReportMissing =>
        CurrentSummary?.CaseType is { } caseType
        && OriginalReportPolicy.IsMissing(
            caseType,
            Case?.Data.StandaloneAuditEvidenceId ?? FilesSection?.StandaloneAuditEvidenceId,
            CaseFiles.Current(FilesSection?.Documents ?? Case?.Documents ?? [])
                .Any(file => file.Occurrence.SemanticRole == DocumentSemanticRole.AuditReport));

    public sealed record CaseRequirement(string Title, string Source, string? Why);


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
    public CaseFilesSection? FilesSection { get; private set; }

    public CaseSectionFrame? SectionFrame => VehicleSection?.Frame
        ?? ValuationSection?.Frame
        ?? NotesSection?.Frame
        ?? FilesSection?.Frame;

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

    public int EstimateImportMaximumBytes => ImportRawEstimate.MaximumDocumentBytes;

    public string EstimateImportExtensions => string.Join(',', estimateParsers
        .SelectMany(parser => parser.FileExtensions)
        .Distinct(StringComparer.OrdinalIgnoreCase));
    private const string NotInEditMode = "Enter edit mode to change the assessment.";

    public CaseAssessmentProjection? Assessment { get; private set; }

    public RepairSpecificationVersion? CurrentSpecification { get; private set; }

    public IReadOnlyList<RepairSpecificationVersion> Estimates { get; private set; } = [];

    public RepairSpecificationVersion? SelectedEstimate { get; private set; }

    public bool EditingNewEstimate { get; private set; }

    public EstimateDetails? EditorDetails { get; private set; }

    public IReadOnlyList<EstimateEditorLine> EditorLines { get; private set; } = [];

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
    public bool ShowingPostedEstimate { get; private set; }

    public string? PostedEstimateValue(string name) =>
        ShowingPostedEstimate && Request.HasFormContentType
            ? Request.Form[name].FirstOrDefault() ?? string.Empty
            : null;

    public bool SelectedEstimateIsEditable =>
        !AssessmentIsReadOnly
        && AssessmentCanOpen
        && (EditingNewEstimate || SelectedEstimate?.State == RepairSpecificationState.Draft);

    public bool SelectedEstimateCanBeDuplicated =>
        !AssessmentIsReadOnly
        && AssessmentCanOpen
        && SelectedEstimate is { State: not RepairSpecificationState.Discarded };

    public bool SelectedEstimateCanBeCurrent =>
        !AssessmentIsReadOnly
        && AssessmentCanOpen
        && SelectedEstimate is { IsCurrent: false, State: RepairSpecificationState.Draft };

    public EstimateTotals EditorTotals
    {
        get
        {
            var details = EditorDetails ?? SelectedEstimate?.Details
                ?? new EstimateDetails(
                    Name: Labels.CaseWorkspace.EngineerSections.Estimate,
                    LabourRate: null,
                    OtherCosts: null,
                    VatPercent: EstimatePolicy.DefaultVatPercent);
            var stored = SelectedEstimate?.Lines.ToDictionary(line => line.Id)
                ?? new Dictionary<Guid, CaseEstimateLineRecord>();
            // A stored line keeps its Specialist kind, as the Save carries it,
            // so hours priced by work units stay priced here too.
            string LineType(EstimateEditorLine line)
            {
                var edited = EstimateOperations.TryParse(line.Operation, out var operation)
                    ? EstimateOperations.ToLineType(operation)
                    : "specialist_fixed";
                return line.ExistingLineId is { } id && stored.TryGetValue(id, out var saved)
                    ? EstimateOperations.Carry(edited, saved.Type)
                    : edited;
            }
            return EstimateTotals.Compute(new(
                SelectedEstimate?.SpecificationId ?? Guid.Empty,
                SelectedEstimate?.CaseId ?? Guid.Empty,
                SelectedEstimate?.Version ?? 1,
                RepairSpecificationState.Draft,
                SelectedEstimate?.Source ?? new(RepairSpecificationSourceRoute.Manual, null, null, null),
                [.. EditorLines.Select((line, index) => new CaseEstimateLineRecord(
                    Guid.Empty,
                    index + 1,
                    LineType(line),
                    null,
                    line.Description,
                    ParseNumber(line.LabourHours),
                    ParseNumber(line.PartPounds),
                    false,
                    line.PartNumber,
                    null,
                    null,
                    null,
                    ActorKind.Staff,
                    SelectedEstimate?.CreatedBy ?? string.Empty,
                    DateTimeOffset.UtcNow,
                    ParseNumber(line.PaintHours),
                    string.IsNullOrWhiteSpace(line.Quantity)
                        ? null
                        : (int?)ParseNumber(line.Quantity),
                    ParseNumber(line.Materials)))],
                SelectedEstimate?.CreatedBy ?? string.Empty,
                SelectedEstimate?.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                details,
                SelectedEstimate?.IsCurrent ?? false,
                SelectedEstimate?.AiJobId,
                SelectedEstimate?.DiscardReason));
        }
    }

    /// <summary>
    /// The addresses whose postcode suggests the regional uplift (v28 P17),
    /// each named with its outward code: the repairer's, the claimant's and
    /// the storage location's.
    /// </summary>
    public IReadOnlyList<string> RegionalUpliftSuggestions
    {
        get
        {
            var data = Case?.Data;
            if (data is null)
            {
                return [];
            }
            var candidates = new (string Label, string? Address)[]
            {
                (Pegasus.Web.Presentation.CaseWorkspaceLabels.Estimate.Repairer, Accepted(data.Inspection.RepairerAddress)?.Value),
                (Labels.CaseWorkspace.RibbonClaimant, Accepted(data.Claimant.Address)?.Value),
                (Pegasus.Web.Presentation.CaseWorkspaceLabels.Inspection.Storage, Accepted(data.Inspection.StorageLocation)?.Value),
            };
            return candidates
                .Where(candidate => RegionalUpliftPolicy.Suggests(candidate.Address))
                .Select(candidate => $"{candidate.Label} ({RegionalUpliftPolicy.OutwardCode(candidate.Address)})")
                .ToArray();
        }
    }

    public decimal? EngineerValue =>
        Assessment?.Field(AssessmentVocabulary.ValueEngineer) is { } engineerValue
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

    public bool IsPostReportReadOnly => CurrentWorkflow?.State is
        CaseLifecycleState.PostReportComplete or CaseLifecycleState.Query;

    public bool CanEditCaseData => !IsPostReportReadOnly
        && !string.IsNullOrWhiteSpace(RenderLeaseToken)
        && CurrentWorkflow?.Archive is null;

    public bool CanEditEngineering => CanEditCaseData && AssessmentCanOpen && !AssessmentIsReadOnly;

    public bool CanEditAssessmentField(string path) => CanEditEngineering;

    /// <summary>The value a field's control opens with: the same value its box shows.</summary>
    public string? AssessmentEditorValue(string path) => ShownAssessment(path)?.Value;

    public IReadOnlyList<SignOffEngineerProfile> EligibleSignOffEngineers { get; private set; } = [];

    public Guid? SelectedSignOffEngineerId { get; private set; }

    public string? ImportCondition =>
        !AssessmentCanOpen
            ? Labels.CaseWorkspace.EngineerSections.NotAvailableForCase
            : AssessmentIsReadOnly
                ? Labels.CaseWorkspace.EngineerSections.ReadOnlyOnceComplete
                : null;

    public string? SendToClaudeCondition { get; private set; }

    public AssessmentReportDraftPreparation? ReportDraftPreparation { get; private set; }

    /// <summary>
    /// The report's narrative blocks as the Report section offers them (v28
    /// P30), the ones taken off the report among them. Empty while the Case
    /// cannot yet be projected into a report: the readiness rail already
    /// states what is outstanding.
    /// </summary>
    public IReadOnlyList<ReportWordingBlock> ReportWording { get; private set; } = [];

    /// <summary>The addresses the delivery form offers beside its fields (v28 P21).</summary>
    public IReadOnlyList<ReportRecipientCandidate> ReportAddressBook { get; private set; } = [];

    /// <summary>What the Case's report has already been sent (v28 P23).</summary>
    public CaseReportSendHistory ReportSendHistory { get; private set; } = CaseReportSendHistory.None;

    /// <summary>
    /// What each offered block says when nobody has written their own, so
    /// Recompose puts the composed sentence back without a round trip.
    /// </summary>
    public IReadOnlyDictionary<string, string> ReportWordingComposed { get; private set; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// The Damage section's Incident narrative: the report's Nature of Incident
    /// block as Core composes it for this view's work (operator, 24 September
    /// 2026), in every state the workspace loads. Null while there is none.
    /// </summary>
    public string? IncidentNarrative { get; private set; }

    /// <summary>
    /// The accepted statement of truth this Case's report prints, from Core, shown
    /// read-only in the Report section in every state the workspace loads. Empty
    /// only when the report inputs cannot be read.
    /// </summary>
    public IReadOnlyList<string> StatementOfTruth { get; private set; } = [];

    public string? ReportDraftCondition { get; private set; }

    public bool ReportDraftNotReady =>
        ReportDraftPreparation is { CanGenerate: false }
        && ReportDraftReasons.Count > 0;

    /// <summary>The report blockers in the order the page shows what clears them.</summary>
    public IReadOnlyList<AssessmentReadinessItem> ReportDraftReasons =>
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

    /// <summary>Principal suggestions offered for staff review before preparation.</summary>
    public ReportRecipientSuggestions? DeliveryRecipientSuggestions { get; private set; }

    public string? OpenDialog { get; private set; }

    public string ImportOperationKey { get; private set; } = NewOperationKey();

    public string DuplicateOperationKey { get; private set; } = NewOperationKey();

    public string DiscardOperationKey { get; private set; } = NewOperationKey();

    public string UseEstimateOperationKey { get; private set; } = NewOperationKey();

    public string RestoreOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// The unroadworthy reason wordings offered on this Case (v28 P15): the
    /// standard ones, then the Principal's own, in the order they were saved.
    /// </summary>
    public IReadOnlyList<string> UnroadworthyReasonWordings { get; private set; } =
        UnroadworthyReasonBank.Standard;

    /// <summary>
    /// The salvage matrix of this Case's Principal (29 September 2026), read
    /// while the Engineer sections edit; null when the Principal has none.
    /// </summary>
    public SalvageMatrix? PrincipalSalvageMatrix { get; private set; }

    /// <summary>
    /// Whether the salvage value the box opens with is still the matrix's to
    /// fill: empty, or the figure the matrix gives for the category and
    /// Engineer's Value the page opens with.
    /// </summary>
    public bool SalvageMatrixFollows =>
        PrincipalSalvageMatrix is { } matrix
        && matrix.Follows(
            RecordedOutcome,
            AssessmentEditorValue(AssessmentVocabulary.SalvageCategory),
            EngineerValue,
            SalvageValueFigure);

    /// <summary>The repair reserve the Current repair specification implies (v28 P30), or null.</summary>
    public decimal? ComputedRepairReserve =>
        SettlementPolicy.ComputedRepairReserve(RepairCostIncVat, RecordedOutcome);

    /// <summary>The selected specification's frozen versions, oldest first (v28 P43).</summary>
    public IReadOnlyList<RepairSpecificationSnapshot> SelectedEstimateSnapshots { get; private set; } = [];

    /// <summary>Whether the selected specification's latest scaling state is scaled (v28 P34).</summary>
    public bool SelectedEstimateIsScaled =>
        SelectedEstimateSnapshots
            .Where(snapshot => snapshot.Kind is RepairSpecificationSnapshotKind.Scaled
                or RepairSpecificationSnapshotKind.ScalingRemoved)
            .MaxBy(snapshot => snapshot.Number)?.Kind == RepairSpecificationSnapshotKind.Scaled;

    /// <summary>The two specifications Compare reads, when the query names both (v28 P19).</summary>
    public RepairSpecificationVersion? ComparisonFrom { get; private set; }

    public RepairSpecificationVersion? ComparisonTo { get; private set; }

    public RepairSpecificationComparison.Diff? Comparison { get; private set; }

    /// <summary>The specification the selected one supplements, and their differences (v28 P20).</summary>
    public RepairSpecificationVersion? SupplementaryBase { get; private set; }

    public RepairSpecificationComparison.Diff? SupplementaryDiff { get; private set; }

    public string SendOperationKey { get; private set; } = NewOperationKey();

    public string GenerateReportOperationKey { get; private set; } = NewOperationKey();

    public string GenerateFeeNoteOperationKey { get; private set; } = NewOperationKey();

    public string GenerateRepairSpecOperationKey { get; private set; } = NewOperationKey();

    public string GenerateImagePackOperationKey { get; private set; } = NewOperationKey();

    public string SendReportOperationKey { get; private set; } = NewOperationKey();

    public string LaunchGlassOperationKey { get; private set; } = NewOperationKey();

    public string ResumeGlassOperationKey { get; private set; } = NewOperationKey();

    /// <summary>
    /// Whether this staff member holds an enabled Glass's account. Only the answer
    /// is kept: the reader hands back the account's secret material, and
    /// nothing but this boolean survives the call.
    /// </summary>
    public bool GlassAccountEnabled { get; private set; }

    /// <summary>
    /// This staff member's own Glass's session for this Case, when they have
    /// one. Another staff member's session runs inside another external
    /// account and is never read here.
    /// </summary>
    public GlassRepairEstimateSession? GlassSession { get; private set; }

    /// <summary>
    /// This staff member's session that holds their Glass's account on another
    /// Case. The account has one live slot, so while this is set no launch is
    /// offered here; the section says which Case holds it instead.
    /// </summary>
    public GlassSessionElsewhere? GlassSessionElsewhere { get; private set; }

    /// <summary>
    /// Whether the Estimate section offers the Glass's control at all: a staff
    /// member on a writable open assessment, holding an enabled account
    /// that no other Case is using. Without the account the control is absent
    /// rather than disabled — the capability belongs to the operator's own
    /// credential, not to this deployment.
    /// </summary>
    public bool CanLaunchGlass =>
        AssessmentCanOpen && !AssessmentIsReadOnly && GlassAccountEnabled
        && GlassSessionElsewhere is null
        // Glass's imports into the current work: the Inspection view offers
        // it once a session names the work it was launched from.
        && !IsInspectionView;

    /// <summary>
    /// Whether the session on the screen can be picked back up: an open
    /// calculation to return to, or a held result waiting for the Case's edit
    /// authority.
    /// </summary>
    public bool CanResumeGlass =>
        CanLaunchGlass
        && GlassSession is { } session
        && GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State);

    /// <summary>
    /// Whether the session on the screen failed because its export could not be
    /// read, so its owner may fetch that same estimate again.
    /// </summary>
    public bool CanFetchGlassAgain =>
        CanLaunchGlass
        && GlassSession is { } session
        && GlassRepairEstimateSessionPolicy.CanRefetchExport(session.State, session.FailureCode);

    /// <summary>
    /// Whether the session on the screen can be closed by its owner: any one
    /// that still holds the account except one mid-import, per the policy, and
    /// none while its provider work is running in the background.
    /// </summary>
    public bool CanCloseGlass => AssessmentCanOpen
        && !GlassWorkRunning
        && GlassSession is { } session
        && GlassRepairEstimateSessionPolicy.CanClose(session.State);

    /// <summary>Whether provider work for this staff member's session is queued or running now.</summary>
    public bool GlassWorkRunning { get; private set; }

    private static decimal? ParseNumber(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
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

    private static readonly string ClosureProbeLeaseToken =
        new('0', CaseEditAuthority.LeaseTokenLength);

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        string? estimate,
        string? dialog,
        [FromServices] TriageCasePorts triagePorts,
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
            return await GetTriageCaseAsync(id, actor, triagePorts, cancellationToken);
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
            await RestoreLeaseStateAsync(id, actor, Case.ActiveEditLease, resumeLease, cancellationToken);
            // Each phase below starts its independent reads together, at most
            // four at a time and each on its own database context, and sets the
            // page's state only once all of them have finished. The phases stay
            // in order because each uses what the one before it read.
            using (DocumentReadTelemetry.Start("web.case.direct-sections"))
            {
                await LoadDirectSectionsAsync(id, actor, workspace, work, cancellationToken);
            }
            using (DocumentReadTelemetry.Start("web.case.engineer-sections"))
            {
                await LoadEngineerSectionsAsync(id, actor, workspace, work, estimate, dialog, cancellationToken);
            }
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
        CancellationToken cancellationToken)
    {
        using var firstReads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var frameRead = firstReads.Start(async token =>
        {
            using (DocumentReadTelemetry.Start("web.case.frame"))
            {
                return await getCasePageFrame.ExecuteAsync(new(id, actor, Work: work), token);
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
        var principalCode = Case!.Workflow.Identity.PrincipalCode;
        // The snapshot source takes what this page already read for the work
        // rather than reading the workspace, preparations, valuations and the
        // Case's works again.
        var reuse = new ReportProjectionReuse(
            work,
            workspace,
            AssetPreparations,
            appliedValuationsLoaded ? AppliedValuations : null,
            Case.Frame);

        using var reads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var estimates = reads.Start(token => listEstimates.ExecuteAsync(id, work, token));
        var cards = reads.Start(token => labourRateCards.ListAsync(actor, token));
        var savedReasons = reads.Start(token => unroadworthyReasonBank.ListAsync(principalCode, token));
        var salvageMatrix = CanEditEngineering
            ? reads.Start(token => principalSalvageMatrices.GetForCaseAsync(id, token))
            : null;
        var reportInputs = reads.Start(token => reportSnapshotSource.GetAsync(id, actor, work, reuse, token));
        var currentGeneration = reads.Start(token =>
            reportGenerations.GetCurrentAsync(actor, id, work, token));
        var sendingToAi = asksSendToAi
            ? reads.Start(token => sendToAiControl.IsEnabledAsync(token))
            : null;
        var glass = reads.Start(token => ReadGlassSessionAsync(id, actor, token));
        await reads.WhenAllAsync();

        Estimates = await estimates;
        LabourRateCards = await cards;
        ApplyEstimateSelection(estimate);
        var saved = await savedReasons;
        UnroadworthyReasonWordings = [.. UnroadworthyReasonBank.Standard, .. saved.Select(item => item.Text)];
        PrincipalSalvageMatrix = salvageMatrix is null ? null : await salvageMatrix;
        var inputs = await reportInputs;
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
            StatementOfTruth = AssessmentReportContract.StatementOfTruthOf(inputs.Projection);
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
            }
        }
        CurrentReportGeneration = await currentGeneration;
        EvaluateEngineerSectionConditions(sendingToAi is not null && await sendingToAi);
        ApplyGlassSession(await glass);

        // What the first reads decided: the selected estimate's snapshots, and
        // the delivery reads a stored report needs, the viewed work's too.
        var selectedSpecificationId = SelectedEstimate?.SpecificationId;
        var generated = CurrentReportGeneration is not null;
        var snapshots = selectedSpecificationId is { } specificationId
            ? reads.Start(token => specificationSnapshots.ListAsync(id, specificationId, token))
            : null;
        var suggestions = generated
            ? reads.Start(token => reportRecipientSuggestions.GetAsync(id, work, token))
            : null;
        var history = generated
            ? reads.Start(token => reportSendHistory.GetAsync(id, work, token))
            : null;
        await reads.WhenAllAsync();

        if (SelectedEstimate is not null && snapshots is not null)
        {
            SelectedEstimateSnapshots = await snapshots;
            if (SelectedEstimate.Supplementary is { } supplementary)
            {
                SupplementaryBase = Estimates.FirstOrDefault(item => item.SpecificationId == supplementary.OfSpecificationId);
                SupplementaryDiff = SupplementaryBase is null
                    ? null
                    : RepairSpecificationComparison.Compare(SupplementaryBase, SelectedEstimate);
            }
        }
        if (Guid.TryParse(Request.Query["from"], out var fromId) && Guid.TryParse(Request.Query["to"], out var toId) && fromId != toId)
        {
            ComparisonFrom = Estimates.FirstOrDefault(item => item.SpecificationId == fromId);
            ComparisonTo = Estimates.FirstOrDefault(item => item.SpecificationId == toId);
            Comparison = ComparisonFrom is null || ComparisonTo is null
                ? null
                : RepairSpecificationComparison.Compare(ComparisonFrom, ComparisonTo);
        }
        DeliveryRecipientSuggestions = suggestions is null ? null : await suggestions;
        ReportAddressBook = DeliveryRecipientSuggestions is { } addressBook
            ? CaseReportDeliveryPolicy.AddressBook(addressBook)
            : [];
        ReportSendHistory = history is null
            ? CaseReportSendHistory.None
            : await history;
        ReportDeliveryMessage = await RenderReportDeliveryMessageAsync(actor, cancellationToken);
        OpenDialog = dialog switch
        {
            "send-to-claude" when SendToClaudeCondition is null => "send-to-claude",
            "compare-estimates" when Estimates.Count >= 2 => "compare-estimates",
            "versions" when SelectedEstimate is not null => "versions",
            "delete-estimate" when SelectedEstimateIsEditable
                && SelectedEstimate is { IsCurrent: false } => "delete-estimate",
            _ => null
        };
    }

    /// <summary>What the Estimate section's Glass's surface shows.</summary>
    private sealed record GlassSessionReads(
        bool AccountEnabled,
        GlassRepairEstimateSession? Session,
        GlassSessionElsewhere? Elsewhere);

    /// <summary>
    /// The Estimate section's Glass's surface: whether this staff member holds
    /// an enabled account, and the session they already have for this Case.
    /// The session belongs to that account, so a Case page without Glass's
    /// makes no session query at all.
    /// </summary>
    private async Task LoadGlassSessionAsync(
        Guid id,
        ActionActor actor,
        CancellationToken cancellationToken) =>
        ApplyGlassSession(await ReadGlassSessionAsync(id, actor, cancellationToken));

    private void ApplyGlassSession(GlassSessionReads reads)
    {
        GlassAccountEnabled = reads.AccountEnabled;
        GlassSession = reads.Session;
        GlassSessionElsewhere = reads.Elsewhere;
        GlassWorkRunning = GlassSession is { } own
            && HttpContext.RequestServices.GetRequiredService<Pegasus.Web.Background.ProviderWorkQueue>()
                .IsInFlight(own.Id);
    }

    /// <summary>
    /// Only whether the account is enabled is asked here; the credential's
    /// secret is read by the launch alone.
    /// </summary>
    private async Task<GlassSessionReads> ReadGlassSessionAsync(
        Guid id,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (actor.Kind != ActorKind.Staff || !Guid.TryParse(actor.SubjectId, out var staffId))
        {
            return new(false, null, null);
        }

        if (!await externalCredentials.IsEnabledAsync(
                actor, ExternalCredentialProvider.GlassRepairEstimate, cancellationToken))
        {
            return new(false, null, null);
        }

        var session = await glassSessions.GetForCaseAsync(id, staffId, cancellationToken);
        GlassSessionElsewhere? elsewhere = null;
        if (session is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State))
        {
            // The account's one live slot may be held from another Case;
            // this Case then says where, and offers no launch.
            var live = await glassSessions.GetLiveForUserAsync(staffId, cancellationToken);
            if (live is not null && live.CaseId != id)
            {
                var references = await listCaseReferences.ExecuteAsync(
                    new(actor, [live.CaseId]), cancellationToken);
                elsewhere = new(
                    live,
                    references.GetValueOrDefault(live.CaseId, live.CaseId.ToString("D")));
            }
        }

        return new(true, session, elsewhere);
    }

    private void ApplyEstimateSelection(string? estimate)
    {
        if (string.Equals(estimate, "new", StringComparison.OrdinalIgnoreCase))
        {
            EditingNewEstimate = true;
            // A new spec starts on the one enabled labour-rate card (FRD-25).
            var card = LabourRateCardAdministration.ForNewSpecification(LabourRateCards);
            EditorDetails = new EstimateDetails(
                Name: Labels.CaseWorkspace.EngineerSections.NewEstimate,
                LabourRate: card?.HourlyRate,
                OtherCosts: null,
                VatPercent: EstimatePolicy.DefaultVatPercent,
                Rate: card is null ? null : new EstimateRateSnapshot(card.Id, card.Version, card.HourlyRate));
            EditorLines = [new EstimateEditorLine("", null, null, null, null, null, null, null)];
            return;
        }

        SelectedEstimate = Guid.TryParse(estimate, out var estimateId)
            ? Estimates.FirstOrDefault(item => item.SpecificationId == estimateId)
            : null;
        SelectedEstimate ??= Estimates.FirstOrDefault(item => item.IsCurrent)
            ?? Estimates.MaxBy(item => item.Version);
        if (SelectedEstimate is null)
        {
            return;
        }

        EditorDetails = SelectedEstimate.Details;
        EditorLines = SelectedEstimate.Lines
            .OrderBy(line => line.Position)
            .Select(line => new EstimateEditorLine(
                EstimateOperations.FromLineType(line.Type).ToString(),
                line.Description,
                line.PartNumber,
                line.Quantity?.ToString(CultureInfo.InvariantCulture),
                line.WorkUnits?.ToString(CultureInfo.InvariantCulture),
                line.PaintWorkUnits?.ToString(CultureInfo.InvariantCulture),
                line.Price?.ToString("0.##", CultureInfo.InvariantCulture),
                line.Materials?.ToString("0.##", CultureInfo.InvariantCulture),
                line.Id))
            .ToList();
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
        else if (AssessmentIsReadOnly)
        {
            SendToClaudeCondition = Labels.CaseWorkspace.EngineerSections.ReadOnlyOnceComplete;
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

        var key = NormalizeSection(section);
        if (!LazySectionViews.TryGetValue(key, out var view))
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
                    NotesSection = await getCaseNotesSection.ExecuteAsync(new(id, actor), cancellationToken);
                    if (NotesSection is null)
                    {
                        return FragmentNotFound();
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
                ApplyFiles(await ReadFilesAsync(id, CanEditCaseData, cancellationToken));
                await LoadAssetPreparationsAsync(id, cancellationToken);
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

    private async Task LoadAssetPreparationsAsync(Guid caseId, CancellationToken cancellationToken)
    {
        AssetPreparations = await caseAssetPreparationQueries.ListForCaseAsync(caseId, cancellationToken);
    }

    /// <summary>The Files body's image-intake records, tag vocabulary and galleries.</summary>
    private sealed record FilesReads(
        IReadOnlyList<ImageIntakeSummary> ImageIntakes,
        IReadOnlyList<ImageTag> TagVocabulary,
        IReadOnlyDictionary<Guid, IReadOnlyList<ImageIntakeImage>> ImagesByIntake);

    /// <summary>
    /// The Files body alone needs its image-intake and instruction-photo lists.
    /// Keeping those reads with that body prevents the initial record response
    /// and unrelated section fragments from preparing galleries the operator
    /// has not opened. The picker needs the whole vocabulary; a read-only visit
    /// draws chips only, so it does not ask for it.
    /// </summary>
    private async Task<FilesReads> ReadFilesAsync(
        Guid caseId,
        bool readsVocabulary,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ImageIntakeSummary> intakes =
        [
            .. (await imageIntakeQueries.ListForCaseAsync(caseId, cancellationToken))
                .Where(intake => !intake.PhotographsAreCaseImages)
        ];
        IReadOnlyList<ImageTag> vocabulary = readsVocabulary
            ? await readImageTagVocabulary.ListAsync(cancellationToken)
            : [];
        var images = await imageIntakeQueries.ListImagesAsync(
            intakes.Select(intake => intake.Id).ToArray(),
            cancellationToken);
        return new(intakes, vocabulary, images);
    }

    private void ApplyFiles(FilesReads files)
    {
        ImageIntakes = files.ImageIntakes;
        TagVocabulary = files.TagVocabulary;
        ImagesByIntake = files.ImagesByIntake;
    }

    private async Task LoadDirectSectionsAsync(
        Guid id,
        ActionActor actor,
        AssessmentWorkspace? workspace,
        CaseWorkSelector work,
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
            Work: work);
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
        AssetPreparations = await preparations;
        if (valuationReads is not null)
        {
            ApplyValuationSection(await valuationReads);
        }
    }

    /// <summary>
    /// The record's remaining reads: the directory choices, the Principal's
    /// previous addresses, the Files galleries, the frame's names and EVA
    /// state, the lease holder, and the Case's AI jobs, read once for both
    /// the Next action's drafts and the Valuation section's pending research.
    /// </summary>
    private async Task LoadExtrasAsync(Guid id, ActionActor actor, CancellationToken cancellationToken)
    {
        var details = Case!;
        var canEditCaseData = CanEditCaseData;
        var inspectionRendered = !SectionIsDeferred("inspection");
        var filesRendered = !SectionIsDeferred("files");
        var extrasInputs = new WorkspaceExtrasInputs(
            details,
            AssessmentCanOpen,
            EligibleSignOffEngineers,
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
        var caseAiJobs = reads.Start(token => aiJobs.ListForSubjectAsync(id, token));
        var configuration = reads.Start(token => workflowConfiguration.GetCurrentAsync(token));
        await reads.WhenAllAsync();

        if (claimSources is not null)
        {
            ClaimSourceChoices = await claimSources;
        }
        if (previousAddresses is not null)
        {
            InspectionAddressChoices = Pegasus.Core.Address.InspectionAddressChoices.Resolve(
                InspectionAddressChoicesData.Of(details.Data, await previousAddresses));
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
        EvaHandoff = workspaceExtras.EvaHandoff;
        if (activeLease is not null)
        {
            ViewerHoldsEditAuthority = viewerHoldsLease;
            EditAuthorityHolder = holder is null ? CaseEditAuthorityHolder.Unnamed : await holder;
        }
        var jobs = await caseAiJobs;
        AiDrafts = AiDraftPolicy.Drafts(jobs, (await configuration).AiDraftTargetDays);
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
            acquireLease,
            resumeLease,
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
        HeartbeatLeaseAsync(heartbeatLease, id, editLeaseToken, cancellationToken);

    public Task<IActionResult> OnPostReleaseLeaseAsync(
        Guid id,
        string operationKey,
        string editLeaseToken,
        string? section,
        CancellationToken cancellationToken) =>
        ReleaseLeaseAsync(
            releaseLease,
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
            await releaseLease.ExecuteAsync(
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
        string? vehicleMileageSource,
        string? accidentCircumstances,
        DateOnly? incidentDate,
        DateOnly? dueBy,
        string? contactName,
        string? contactEmailAddress,
        string? contactPhoneNumber,
        string? vatStatus,
        DateOnly? inspectionDate,
        DateOnly? inspectionDeadline,
        string? inspectionAddress,
        CaseInspectionMode? inspectionMode,
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
        string? damageImpacts,
        string? repairerName,
        string? repairerAddress,
        Guid? repairerDirectoryId,
        string? principalNotes,
        string? claimSourceNotes,
        string? clientNotes,
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
        string? saveError = null;
        string? savedEstimate = null;
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
                    throw new InvalidOperationException("A submitted Case field is invalid.");
                }

                if (assessmentFields.Keys.Any(path => !EditorLabels.IsAssessmentField(path)))
                {
                    throw new InvalidOperationException("This field is not part of the Case editor.");
                }
                CaseWorkspaceEstimate? estimate = null;
                if (carriesEstimate)
                {
                    try
                    {
                        estimate = await EstimateEditorPartAsync(id, actor, cancellationToken);
                    }
                    catch (InvalidOperationException exception)
                    {
                        saveError = MutationRefusalMessage(exception, string.Empty);
                        throw;
                    }

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
                var damageSubmitted = Posted(nameof(damageImpacts)) || damageFields.Count > 0;
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
                var current = await getCaseEditBasis.ExecuteAsync(new(id, actor, work), cancellationToken)
                    ?? throw new KeyNotFoundException("The Case is unavailable.");
                var data = current.Data;
                // This read supplies unshown values, never new write authority.
                // The transaction still receives the submitted version and lease.
                if (engineeringSubmitted
                    && AssessmentAccessPolicy.IsReadOnly(new(current.Workflow.State)))
                {
                    throw new InvalidOperationException("Engineering fields are read-only in this Case state.");
                }
                if (preparationSubmitted
                    && (current.Workflow.State is CaseLifecycleState.PostReportComplete
                            or CaseLifecycleState.Query
                        || current.Workflow.Archive is not null))
                {
                    throw new InvalidOperationException("The case is read-only once Complete.");
                }
                var workspace = await getAssessmentWorkspace.ExecuteAsync(new(id, actor, work), cancellationToken);
                var assessment = workspace?.Assessment;
                string? Recorded(string path) => assessment?.Field(path)?.Value;
                decimal? Money(string path) => decimal.TryParse(Recorded(path), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var value) ? value : null;
                var persisted = data.Workspace;
                var address = Submitted(nameof(inspectionAddress), inspectionAddress, Accepted(data.Inspection.Address)?.Value);
                var treatment = !Posted(nameof(inspectionAddress)) && persisted?.InspectionAddressTreatment is { } recordedTreatment
                    ? recordedTreatment
                    : CaseDataPolicy.InferInspectionMode(address) switch
                    {
                        CaseInspectionMode.ImageBasedAssessment => CaseReportAddressTreatment.ImageBasedAssessment,
                        CaseInspectionMode.PhysicalAddress => CaseReportAddressTreatment.PhysicalVehicleLocation,
                        _ => CaseReportAddressTreatment.Undetermined
                    };
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
                originalUnit = mileageValue is null ? null : originalUnit ?? CaseOdometerUnit.Miles;
                var reportFields = assessmentFields.Where(field => EditorLabels.Report.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                // Every control opens holding its value, so emptying it clears it.
                var settlementFields = assessmentFields
                    .Where(field => EditorLabels.Settlement.ContainsKey(field.Key) || EditorLabels.OriginalReport.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                AssessmentPolicy.RequireOriginalReportScope(
                    settlementFields.Keys,
                    current.Summary.CaseType);
                // The report's three values are Valuation's own boxes (operator,
                // 26 September 2026), saved like any field.
                var valuationFields = assessmentFields.Where(field => EditorLabels.Valuation.ContainsKey(field.Key))
                    .ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal);
                var reportSubmitted = reportFields.Count > 0 || Posted(nameof(signOffEngineerId)) || Posted(nameof(reportDate));
                var overviewSubmitted = new[] { nameof(claimantName), nameof(claimantContactNumber), nameof(claimantAddress),
                    nameof(claimNumber), nameof(contactName), nameof(contactEmailAddress), nameof(contactPhoneNumber),
                    nameof(incidentDate), nameof(dueBy), nameof(accidentCircumstances), nameof(vatStatus),
                    nameof(repairerName), nameof(repairerAddress), nameof(repairerDirectoryId),
                    nameof(principalNotes), nameof(claimSourceNotes), nameof(clientNotes), nameof(claimSourceId),
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
                    else if (sourceId != persisted?.ClaimSource?.ClaimSourceId)
                    {
                        claimSourceChanged = true;
                        var chosen = (await contactDirectory.ListByRoleAsync(actor, ContactRole.ClaimSource, cancellationToken))
                            .SingleOrDefault(item => item.OrganizationId == sourceId)
                            ?? throw new InvalidOperationException("The selected claim source is not an active Claim source record.");
                        claimSource = new CaseWorkspaceClaimSource(
                            chosen.OrganizationId, chosen.Version, chosen.Name,
                            chosen.ContactPerson, chosen.Telephone, chosen.Email);
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
                // A linked directory organisation is copied onto the
                // Case — its identity, its version and its own name and
                // address — so a later directory edit never rewrites this
                // Case. Without a link the Case keeps the extracted or keyed
                // text, and the member of staff saving it is its confirmation.
                var linkedRepairer = overviewSubmitted && repairerDirectoryId is { } directoryId
                    ? (await contactDirectory.ListByRoleAsync(actor, ContactRole.Repairer, cancellationToken))
                        .SingleOrDefault(item => item.OrganizationId == directoryId)
                        ?? throw new InvalidOperationException("The selected repairer is not an active directory Repairer.")
                    : null;
                // An unposted selector is not an unlink: only the rendered
                // control, which offers "not linked" explicitly, may clear it.
                var selectorPosted = Posted(nameof(repairerDirectoryId));
                var repairer = linkedRepairer is not null
                    ? new CaseWorkspaceRepairer(
                        linkedRepairer.OrganizationId, linkedRepairer.Version, linkedRepairer.Name)
                    : new CaseWorkspaceRepairer(
                        selectorPosted ? null : persisted?.Repairer?.DirectoryOrganizationId,
                        selectorPosted ? null : persisted?.Repairer?.DirectoryOrganizationVersion,
                        Submitted(nameof(repairerName), repairerName, Accepted(data.Inspection.RepairerName)?.Value));
                var inspectionSubmitted = new[] { nameof(inspectionAddress), nameof(storageLocation), nameof(inspectionDate),
                    nameof(inspectionDeadline), nameof(storagePerDay), nameof(recoveryCharge) }.Any(Posted);
                var vehicleSubmitted = new[] { nameof(vehicleRegistration), nameof(vehicleMake), nameof(vehicleModel),
                    nameof(vehicleYear), nameof(vehicleMileage), nameof(vehicleMileageUnit),
                    nameof(vehicleMileageSource) }.Any(Posted)
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
                var impacts = !Posted(nameof(damageImpacts))
                    ? null
                    : AssessmentPolicy.ParseImpacts(damageImpacts);
                var recordedDate = DateOnly.TryParseExact(Recorded(AssessmentVocabulary.ReportDate), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : (DateOnly?)null;
                // The guide source cards have no Save of their own (23 September
                // 2026): this save records the ones whose boxes were changed, and
                // records the calculation when the operator changed it.
                List<ValuationDetails> guideValuations = guideEntries is { Length: > 0 }
                    ? GuideEntriesToRecord(guideEntries, recordedCards)
                    : [];
                SaveCaseWorkspaceResult workspaceSave;
                try
                {
                    workspaceSave = await saveCaseWorkspace.ExecuteAsync(new(id, expectedVersion, actor, operationKey, reason, editLeaseToken)
                    {
                        Work = work,
                        Valuation = guideValuations.Count == 0 && adoption is null && valuationFields.Count == 0
                            ? null
                            : new(guideValuations, adoption, valuationFields.Count == 0 ? null : valuationFields),
                        Estimate = estimate,
                        Overview = !overviewSubmitted ? null : new(
                            Submitted(nameof(claimantName), claimantName, Accepted(data.Claimant.Name)?.Value),
                            Submitted(nameof(claimantContactNumber), claimantContactNumber, Accepted(data.Claimant.ContactNumber)?.Value),
                            Submitted(nameof(claimantAddress), claimantAddress, Accepted(data.Claimant.Address)?.Value),
                            Submitted(nameof(claimNumber), claimNumber, Accepted(data.Claim.Number)?.Value),
                            Submitted(nameof(contactName), contactName, Accepted(data.Contact.Name)?.Value),
                            Submitted(nameof(contactEmailAddress), contactEmailAddress, Accepted(data.Contact.EmailAddress)?.Value),
                            Submitted(nameof(contactPhoneNumber), contactPhoneNumber, Accepted(data.Contact.PhoneNumber)?.Value),
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
                            Submitted(nameof(clientNotes), clientNotes, persisted?.ClientNotes),
                            Submitted(
                                nameof(dueBy),
                                dueBy,
                                current.Workflow.DueWork?.DueBy
                                    ?? Accepted(data.Inspection.Deadline)?.Value)),
                        Inspection = !inspectionSubmitted ? null : new(treatment, address, persisted?.InspectionLocationProvenance,
                            Submitted(nameof(storageLocation), storageLocation, Accepted(data.Inspection.StorageLocation)?.Value),
                            persisted?.StorageBusiness,
                            Submitted(nameof(inspectionDate), inspectionDate, Accepted(data.Inspection.InspectionDate)?.Value),
                            Submitted(nameof(inspectionDeadline), inspectionDeadline, Accepted(data.Inspection.Deadline)?.Value),
                            persisted?.InspectionVehiclePresent, persisted?.InspectionCondition,
                            persisted?.InspectionContactName, persisted?.InspectionContactTelephone,
                            persisted?.InspectionContactEmailAddress, persisted?.InspectionNotes,
                            Submitted(nameof(storagePerDay), storagePerDay, Money(AssessmentVocabulary.SettlementStoragePerDay)),
                            Submitted(nameof(recoveryCharge), recoveryCharge, Money(AssessmentVocabulary.CostRecoveryCharge))),
                        Vehicle = !vehicleSubmitted ? null : new(
                            Submitted(nameof(vehicleRegistration), vehicleRegistration, Accepted(data.Vehicle.Registration)?.Value),
                            Submitted(nameof(vehicleMake), vehicleMake, Accepted(data.Vehicle.Make)?.Value),
                            Submitted(nameof(vehicleModel), vehicleModel, Accepted(data.Vehicle.Model)?.Value),
                            new(mileageValue,
                                originalUnit,
                                Submitted(nameof(vehicleMileageSource), vehicleMileageSource,
                                    Recorded(AssessmentVocabulary.VehicleMileageSource)),
                                persisted?.VehicleMileageDisplayUnit),
                            vehicleIdentityFields.Count > 0 ? vehicleIdentityFields : null,
                            Submitted(nameof(vehicleYear), vehicleYear, Accepted(data.Vehicle.Year)?.Value)),
                        Damage = !damageSubmitted ? null : new(impacts, damageFields),
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
                }
                catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
                {
                    // A refusal names its own reason (a spec line, a valuation
                    // figure, a calculation that cannot be recorded); a lost lease
                    // or a changed Case keeps the shared one.
                    saveError = MutationRefusalMessage(exception, string.Empty);
                    throw;
                }
                RecordEditorCommit("case-edit-form", operationKey, expectedVersion, workspaceSave.Version);
                if (workspaceSave.Estimate is { } writtenEstimate)
                {
                    savedEstimate = writtenEstimate.SpecificationId.ToString("D");
                    savedEstimateLineIdentities = EstimateLineIdentitiesOf(postedEstimateLineRows, writtenEstimate);
                }

                if (saveUnroadworthyReason)
                {
                    try
                    {
                        var saved = await saveUnroadworthyReasonAction.ExecuteAsync(
                            new(current.Workflow.Identity.PrincipalCode, bankWording!, actor),
                            cancellationToken);
                        bankStatus = saved is null
                            ? "The bank already offers that wording."
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
            // commit of the open edit session, so the lease the save consumed
            // is claimed again and the page keeps editing. Done releases it.
            caseId => RedirectToSection(caseId, section, savedEstimate),
            keepEditing: true);

        if (bankError is not null)
        {
            Say(StatusTempDataKey, null);
            Say(ErrorTempDataKey, bankError);
        }
        else if (!string.IsNullOrEmpty(saveError))
        {
            Say(ErrorTempDataKey, saveError);
        }
        else if (bankStatus is not null)
        {
            Say(StatusTempDataKey, bankStatus);
        }

        // The command ran (a refusal redirects too); only Forbid has nothing to answer.
        return AnswersInPlace && result is RedirectToPageResult
            ? await AnswerCommitAsync(id, section, preparationEdits is { Length: > 0 }, result, cancellationToken)
            : result;
    }

    /// <summary>
    /// The report's wording blocks for a Case whose report can be projected:
    /// the snapshot the blocks compose from, and every block the section
    /// offers. A Case that cannot yet be projected has neither.
    /// </summary>
    private (AssessmentReportSnapshot? Snapshot, IReadOnlyList<ReportWordingBlock> Offered) WordingOf(
        AssessmentReportProjectionInput projection)
    {
        var projected = AssessmentReportProjection.Project(projection with
        {
            ReportDate = Pegasus.Core.LondonCalendar.DateAt(clock.GetUtcNow()),
        });
        return projected.Snapshot is { } snapshot
            ? (snapshot, ReportWordingComposition.Offered(snapshot, projection.Wording ?? []))
            : (null, []);
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
        var inputs = await reportSnapshotSource.GetAsync(caseId, actor, work, reuse: null, cancellationToken);
        if (inputs is null)
        {
            throw new InvalidOperationException("The report wording is unavailable. Refresh the Case and retry.");
        }
        var snapshot = WordingOf(inputs.Projection).Snapshot
            ?? throw new InvalidOperationException("The report wording is unavailable. Refresh the Case and retry.");
        var presentation = snapshot.Presentation();
        return new([.. edits.Select(edit => edit.ToRecord(snapshot, presentation))]);
    }

    /// <summary>
    /// The working preview is reachable both as a plain link (report only)
    /// and from the generate form, where the operator's "Include fee note"
    /// choice must be previewed exactly as it would be generated (R34B).
    /// </summary>
    public Task<IActionResult> OnGetPreviewReportDraftAsync(
        Guid id,
        bool includeFeeNote,
        CancellationToken cancellationToken) =>
        PreviewReportDraftAsync(id, includeFeeNote, CaseReportArtifactKind.AssessmentReport, cancellationToken);

    public Task<IActionResult> OnPostPreviewReportDraftAsync(
        Guid id,
        bool includeFeeNote,
        CancellationToken cancellationToken) =>
        PreviewReportDraftAsync(id, includeFeeNote, CaseReportArtifactKind.AssessmentReport, cancellationToken);

    /// <summary>
    /// The included images as they would print (v28 P42): the same working
    /// snapshot the report preview renders, as the image pack alone. Nothing
    /// is persisted; a Case with no image in the report has none to show.
    /// </summary>
    public Task<IActionResult> OnGetPreviewImagePackAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        PreviewReportDraftAsync(id, includeFeeNote: false, CaseReportArtifactKind.ImagePack, cancellationToken);

    private async Task<IActionResult> PreviewReportDraftAsync(
        Guid id,
        bool includeFeeNote,
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
                .ExecuteAsync(id, actor, kind, includeFeeNote, cancellationToken);
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

    public async Task<IActionResult> OnGetEstimateDocumentAsync(
        Guid id,
        Guid estimateId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        var result = await renderEstimateDocument.ExecuteAsync(
            id, estimateId, actor, cancellationToken);
        switch (result.Outcome)
        {
            case RenderCaseEstimateDocumentOutcome.NotFound:
                return NotFound();
            case RenderCaseEstimateDocumentOutcome.NotRenderable:
                var reason = string.Join(" ", result.Reasons);
                if (Request.Headers.ContainsKey("X-Pegasus-Document-Preview"))
                {
                    return EnhancedPreviewRefusal(reason);
                }
                TempData["CaseError"] = reason;
                return RedirectToEstimate(id, estimateId.ToString("D"));
            case RenderCaseEstimateDocumentOutcome.Rendered:
                var artifact = result.Artifact!;
                await estimateDocumentPresentations.RecordPreviewedAsync(
                    new(actor, id, estimateId, result.EstimateVersion!.Value, DateTimeOffset.UtcNow),
                    cancellationToken);
                Response.Headers.ContentDisposition =
                    new ContentDispositionHeaderValue("inline")
                    {
                        FileName = artifact.SuggestedFileName,
                    }.ToString();
                return File(artifact.Pdf, "application/pdf");
            default:
                throw new InvalidOperationException("Unsupported estimate document outcome.");
        }
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
    /// B05's immutable generation: freezes the accepted snapshot inside the
    /// store's short transaction and renders through the registered
    /// renderer, one artifact per request. The draft handlers above stay for
    /// the labelled ungenerated working preview; this is the real report.
    /// R34B: the operator chooses whether the fee note is part of this
    /// report or the separate document <see cref="OnPostGenerateFeeNoteAsync"/>
    /// still produces, and that choice is frozen with the snapshot.
    /// </summary>
    public Task<IActionResult> OnPostGenerateReportAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        bool includeFeeNote,
        CancellationToken cancellationToken) =>
        GenerateArtifactAsync(
            id, operationKey, editLeaseToken, expectedCaseVersion,
            CaseReportArtifactKind.AssessmentReport, includeFeeNote,
            targetGenerationId: null, cancellationToken);

    public Task<IActionResult> OnPostGenerateFeeNoteAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        Guid targetGenerationId,
        CancellationToken cancellationToken) =>
        GenerateArtifactAsync(
            id, operationKey, editLeaseToken, expectedCaseVersion,
            CaseReportArtifactKind.FeeNote, includeFeeNote: false,
            targetGenerationId, cancellationToken);

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
            CaseReportArtifactKind.RepairSpecification, includeFeeNote: false,
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
            CaseReportArtifactKind.ImagePack, includeFeeNote: false,
            targetGenerationId, cancellationToken);

    /// <summary>
    /// Every Generate, in or out of edit mode: the report (operator, 26
    /// September 2026) and its companion documents (issue 912, 28 September
    /// 2026). Without a lease the handler claims one for the generation.
    /// Posted from the Inspection view, it makes the Inspection report of a
    /// Case that has its Audit (operator, 1 October 2026).
    /// </summary>
    private async Task<IActionResult> GenerateArtifactAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedCaseVersion,
        CaseReportArtifactKind kind,
        bool includeFeeNote,
        Guid? targetGenerationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return await GenerateWithOneOffLeaseAsync(
                id, operationKey, expectedCaseVersion, kind, includeFeeNote,
                targetGenerationId, cancellationToken);
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
            kind, includeFeeNote, targetGenerationId, cancellationToken);
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
        bool includeFeeNote,
        Guid? targetGenerationId,
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

        CaseEditLease lease;
        try
        {
            lease = await acquireLease.ExecuteAsync(
                new ClaimCaseEditLeaseRequest(id, expectedCaseVersion, actor, NewOperationKey()),
                cancellationToken);
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

        try
        {
            return await GenerateAsync(
                actor, id, operationKey, lease.Token, expectedCaseVersion,
                kind, includeFeeNote, targetGenerationId, cancellationToken);
        }
        finally
        {
            await Pegasus.Web.Presentation.CaseEditLeaseRelease.ReleaseQuietlyAsync(
                releaseLease, logger, id, actor, lease);
        }
    }

    /// <summary>The generation itself, once guarded: the request, and how each outcome reads back.</summary>
    private async Task<IActionResult> GenerateAsync(
        ActionActor actor,
        Guid id,
        string operationKey,
        string editLeaseToken,
        long expectedCaseVersion,
        CaseReportArtifactKind kind,
        bool includeFeeNote,
        Guid? targetGenerationId,
        CancellationToken cancellationToken)
    {
        CaseReportGenerationResult result;
        try
        {
            result = await HttpContext.RequestServices
                .GetRequiredService<IGenerateCaseReport>()
                .ExecuteAsync(
                new(
                    actor,
                    id,
                    expectedCaseVersion,
                    editLeaseToken,
                    operationKey,
                    kind,
                    kind switch
                    {
                        CaseReportArtifactKind.AssessmentReport => "Generate the immutable case report",
                        CaseReportArtifactKind.FeeNote => "Generate the immutable fee note",
                        CaseReportArtifactKind.RepairSpecification => "Generate the immutable Repair Spec",
                        _ => "Generate the immutable images",
                    },
                    includeFeeNote,
                    targetGenerationId,
                    WorkSelector),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
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
            return RedirectToReport(id);
        }

        switch (result.Outcome)
        {
            case CaseReportGenerationOutcome.NotFound:
                return NotFound();
            case CaseReportGenerationOutcome.NotReady:
                TempData["CaseError"] = string.Join(
                    " ",
                    Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerationNotReady + ":",
                    string.Join("; ", result.Reasons.Select(reason =>
                        $"{reason.Requirement}: {reason.WhyOutstanding}")));
                return RedirectToReport(id);
            case CaseReportGenerationOutcome.Pending:
                // Not yet stored is not success: the page says so in amber.
                TempData["CaseWarning"] = Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.GenerationPending;
                return RedirectToReport(id);
            case CaseReportGenerationOutcome.Failed:
                TempData["CaseError"] = NotGenerated(kind);
                return RedirectToReport(id);
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
                TempData["CaseStatus"] = kind switch
                {
                    CaseReportArtifactKind.AssessmentReport =>
                        Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ReportGenerated,
                    CaseReportArtifactKind.FeeNote =>
                        Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.FeeNoteGenerated,
                    CaseReportArtifactKind.RepairSpecification =>
                        Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.RepairSpecGenerated,
                    _ => Pegasus.Web.Presentation.CaseWorkspaceLabels.ReportDelivery.ImagesGenerated,
                };
                return RedirectToReport(id);
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
            return RedirectToReport(id);
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
                    WorkSelector),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception,
                "The report was not sent. Retry the operation.");
            return RedirectToReport(id);
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
    /// What every section command on the record requires before it touches
    /// the case: an authorized actor, an assessment this command may open, a
    /// case that is not read-only, a live form, and an edit lease unless the
    /// command claims one for itself. Only the section the refusal lands on
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
        // A command posted from the Inspection view of a Case that has its
        // Audit acts on the Inspection's own work whatever the Case's state:
        // the Case's read-only states are the Audit's (operator, 1 October
        // 2026).
        if (access.IsReadOnly && !await PostedFromInspectionViewAsync(id, actor, cancellationToken))
        {
            TempData["CaseError"] = "The case is read-only once Complete.";
            return redirect();
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
    /// Whether a posted command names the Inspection view of a Case that has
    /// its Audit, read from the Case header; the view alone names nothing on
    /// a Case with one work.
    /// </summary>
    private async Task<bool> PostedFromInspectionViewAsync(Guid id, ActionActor actor, CancellationToken cancellationToken) =>
        WorkSelector == CaseWorkSelector.Primary
        && (await getCaseHeader.ExecuteAsync(new GetCaseHeaderQuery(id, actor), cancellationToken))?.Works is { HasAudit: true };

    /// <summary>Back to the Report section, in the view the request came from.</summary>
    private RedirectToPageResult RedirectToReport(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = "report", view = ReturnView });

    private static DateOnly? ParseGuideMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var month))
        {
            throw new ArgumentException("The guide month is invalid.", nameof(value));
        }
        return new DateOnly(month.Year, month.Month, 1);
    }

    /// <summary>
    /// The valuation-section mutation guard: the report guard's rules with
    /// the valuation redirect target.
    /// </summary>
    private Task<IActionResult?> GuardValuationCommandAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken) =>
        GuardSectionCommandAsync(
            id,
            operationKey,
            editLeaseToken,
            () => RedirectToValuation(id),
            cancellationToken);

    private RedirectToPageResult RedirectToValuation(Guid id) =>
        RedirectToPage("/Cases/Details", new { id, section = "valuation", view = ReturnView });

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

        public CaseReportWording ToRecord(
            AssessmentReportSnapshot? snapshot,
            AssessmentReportPresentation? presentation)
        {
            var title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim();
            // The browser posts a line break as two characters; the composed
            // sentence it is compared with holds one.
            var text = string.IsNullOrWhiteSpace(Text)
                ? null
                : ReportWordingComposition.LineBreaks(Text.Trim());
            if (!Manual && snapshot is not null && presentation is not null)
            {
                if (title is not null
                    && string.Equals(title, ReportWordingComposition.StandardTitle(Key, presentation), StringComparison.Ordinal))
                {
                    title = null;
                }
                if (text is not null && string.Equals(
                    text,
                    ReportWordingComposition.ComposedText(Key, snapshot, presentation).Trim(),
                    StringComparison.Ordinal))
                {
                    text = null;
                }
            }
            var order = !Manual && Order == ReportWordingComposition.StandardIndex(Key) ? null : Order;
            return new(Key, title, text, order, Included, Manual);
        }
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

    public async Task<IActionResult> OnPostSendToClaudeAsync(
        Guid id,
        string operationKey,
        string? direction,
        int? targetPercent,
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
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }

        var header = await getCaseHeader.ExecuteAsync(new(id, actor), cancellationToken);
        if (header is null)
        {
            return NotFound();
        }

        var trimmedDirection = direction?.Trim();
        var instruction = string.IsNullOrWhiteSpace(trimmedDirection)
            ? $"Draft an estimate for case {header.Summary.Reference}."
            : trimmedDirection;
        try
        {
            await createAiJob.ExecuteAsync(
                new(
                    AiJobKind.Estimate,
                    id,
                    header.Summary.Reference,
                    instruction,
                    targetPercent,
                    actor,
                    operationKey),
                cancellationToken);
        }
        catch (ArgumentException)
        {
            TempData["CaseError"] = "Choose a target between 1 and 100 percent of the Engineer's Value.";
            return RedirectToEstimate(id);
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            TempData["CaseError"] = exception.Message;
            return RedirectToEstimate(id);
        }

        TempData["CaseStatus"] =
            "Sent to AI. The job is queued; its estimate opens from the Work Centre when ready.";
        return RedirectToEstimate(id);
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
    /// Re-renders an estimate form with one row added or removed. The posted
    /// values are not persisted until the Case is saved.
    /// </summary>
    public async Task<IActionResult> OnPostEditLineAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var editor = ReadEditorPost();
        IReadOnlyList<EstimateEditorLine> rows = editor.Rows;
        if (Request.Form.TryGetValue("clearLines", out var cleared) && cleared.ToString() == "true")
        {
            rows = [];
        }
        else if (Request.Form.TryGetValue("removeLine", out var removed)
            && int.TryParse(removed.ToString(), out var removeAt)
            && removeAt >= 0 && removeAt < rows.Count)
        {
            rows = rows.Where((_, index) => index != removeAt).ToArray();
        }
        else
        {
            rows = [.. rows, new EstimateEditorLine("", null, null, null, null, null, null, null)];
        }

        return await RedrawEditorAsync(id, editor.EstimateId, editor, rows, cancellationToken);
    }

    /// <summary>
    /// What the editor said about the specification this one supplements
    /// (v28 P20): the base, the reason and whether the report explains it.
    /// The statement is composed from the base and the lines as posted, so
    /// the record and the report say the same thing.
    /// </summary>
    private async Task<RepairSpecificationSupplementary?> ReadSupplementaryAsync(
        Guid caseId,
        ActionActor actor,
        RepairSpecificationVersion? existing,
        EstimateDetails details,
        EstimateEditorPost editor,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(Request.Form["supplementaryOf"], out var baseId) || baseId == Guid.Empty)
        {
            return null;
        }
        var reason = Request.Form["supplementaryReason"].ToString();
        if (RepairSpecificationComparison.SupplementaryReasons.All(item => item.Code != reason))
        {
            reason = RepairSpecificationComparison.SupplementaryReasons[0].Code;
        }
        var baseSpecification = await repairSpecifications.GetVersionAsync(caseId, baseId, cancellationToken);
        if (baseSpecification is null || baseSpecification.SpecificationId == existing?.SpecificationId)
        {
            return null;
        }
        // The spec a Save would record: the save's own carry, so a stored
        // Specialist line compares as its own kind and its hours stay priced.
        var diff = RepairSpecificationComparison.Compare(
            baseSpecification,
            EstimatePolicy.Edited(
                caseId, actor, existing, details, editor.Lines!, editor.ExistingLineIds, DateTimeOffset.UtcNow));
        var explain = bool.TryParse(Request.Form["supplementaryExplain"].FirstOrDefault(), out var flag) && flag;
        return new(baseId, reason, explain, RepairSpecificationComparison.SupplementaryStatement(diff, reason));
    }

    /// <summary>
    /// The Target % of value preview (v28 P34, issue 897): what Apply would
    /// make of the spec as the editor holds it, for the bar's percentage and
    /// floors. Script calls it as the slider moves; it writes nothing. Core
    /// scales and totals the spec; the figures come back as the editor shows
    /// them, each line by the posted row it came from.
    /// </summary>
    public async Task<IActionResult> OnPostPreviewEstimateScaleAsync(
        Guid id,
        decimal? targetPercent,
        decimal? floorRate,
        decimal? floorPrice,
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

        var refused = new JsonResult(new { status = "refused" });
        var editor = ReadEditorPost();
        if (editor.Lines is null || targetPercent is not { } percent)
        {
            return refused;
        }
        try
        {
            var existing = await ResolveEstimateAsync(id, editor.EstimateId, cancellationToken);
            var workspace = await getAssessmentWorkspace.ExecuteAsync(new(id, actor, WorkSelector), cancellationToken);
            decimal? engineerValue = workspace?.Assessment.Field(AssessmentVocabulary.ValueEngineer) is { } field
                && decimal.TryParse(field.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var recorded)
                    ? recorded
                    : null;
            var target = RepairSpecificationScaling.TargetGross(engineerValue, percent);
            var floors = new ScalingFloors(
                floorRate ?? ScalingFloors.Default.LabourRatePerHour,
                floorPrice ?? ScalingFloors.Default.PricePercent);
            // The spec a Save would record: the save's own line checks and
            // carry, so the preview never shows money the save refuses or drops.
            var edited = EstimatePolicy.Edited(
                id, actor, existing, EditorDetailsFrom(editor, existing), editor.Lines, editor.ExistingLineIds,
                DateTimeOffset.UtcNow);
            var result = RepairSpecificationScaling.Scale(edited, target, floors);
            var printed = result.Totals.Printed;
            static string? Amount(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture);
            return new JsonResult(new
            {
                status = "ok",
                readout = RepairSpecificationWording.ScaleReadout(result, percent),
                labourRate = Amount(result.Details.LabourRate),
                lines = editor.LineRows.Select((row, index) => new
                {
                    row,
                    price = Amount(result.Lines[index].Price),
                    materials = Amount(result.Lines[index].Materials),
                }),
                rollup = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["parts"] = RepairSpecificationWording.Money(printed.Parts),
                    ["panelLabour"] = RepairSpecificationWording.Money(printed.PanelLabour),
                    ["paintLabour"] = RepairSpecificationWording.Money(printed.PaintLabour),
                    ["materials"] = RepairSpecificationWording.Money(printed.Materials),
                    ["specialist"] = RepairSpecificationWording.Money(printed.Specialist),
                    ["offPattern"] = RepairSpecificationWording.Money(
                        decimal.Round(result.Totals.Raw.OffPattern, 2, MidpointRounding.AwayFromZero)),
                    ["net"] = RepairSpecificationWording.Money(printed.Net),
                    ["vat"] = RepairSpecificationWording.Money(printed.Vat),
                    ["gross"] = RepairSpecificationWording.Money(printed.Gross),
                },
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return refused;
        }
    }

    /// <summary>
    /// Apply (v28 P34): scales the saved specification. The page saves the
    /// Case, the specification with it, before it asks (data-case-save-first),
    /// so the draft and both frozen versions are the ones the operator sees.
    /// </summary>
    public async Task<IActionResult> OnPostScaleEstimateAsync(
        Guid id,
        long? expectedVersion,
        string operationKey,
        string? editLeaseToken,
        Guid? estimateId,
        decimal? targetPercent,
        decimal? floorRate,
        decimal? floorPrice,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (expectedVersion is null || estimateId is null)
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id, estimateId?.ToString("D"));
        }
        try
        {
            if (targetPercent is null)
            {
                throw new ArgumentException("A target percentage of the Engineer's Value is required.");
            }
            var floors = new ScalingFloors(floorRate ?? ScalingFloors.Default.LabourRatePerHour, floorPrice ?? ScalingFloors.Default.PricePercent);
            var saved = await scaleRepairSpecification.ExecuteAsync(
                new ScaleRepairSpecificationRequest(
                    id, expectedVersion.Value, actor, operationKey, editLeaseToken!, estimateId.Value,
                    targetPercent.Value,
                    floors) { Work = WorkSelector },
                cancellationToken);
            ClearLeaseState();
            await ReclaimLeaseAsync(id, cancellationToken);
            TempData["CaseStatus"] = "The repair spec was scaled.";
            return RedirectToEstimate(id, saved.SpecificationId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(exception, "The repair spec was not scaled. Retry the operation.");
            return RedirectToEstimate(id, estimateId?.ToString("D"));
        }
    }

    /// <summary>Remove scaling (v28 P34): the specification returns to the version frozen before the last Apply.</summary>
    public async Task<IActionResult> OnPostRemoveEstimateScalingAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
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
            await removeRepairSpecificationScaling.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, editLeaseToken!, estimateId) { Work = WorkSelector },
                cancellationToken);
            ClearLeaseState();
            await ReclaimLeaseAsync(id, cancellationToken);
            TempData["CaseStatus"] = "The scaling was removed.";
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(exception, "The scaling was not removed. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    /// <summary>Restore (v28 P43): a frozen version becomes the draft; the outgoing draft is frozen first.</summary>
    public async Task<IActionResult> OnPostRestoreEstimateSnapshotAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        Guid snapshotId,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
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
            await restoreRepairSpecificationSnapshot.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, editLeaseToken!, estimateId, snapshotId) { Work = WorkSelector },
                cancellationToken);
            ClearLeaseState();
            await ReclaimLeaseAsync(id, cancellationToken);
            TempData["CaseStatus"] = "The version was restored.";
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(exception, "The version was not restored. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    /// <summary>Creates an Engineer's working copy of the selected estimate.</summary>
    public async Task<IActionResult> OnPostDuplicateEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
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
            var copy = await duplicateEstimate.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, "Estimate duplicated", editLeaseToken!, estimateId) { Work = WorkSelector },
                cancellationToken);
            ClearLeaseState();
            await ReclaimLeaseAsync(id, cancellationToken);
            TempData["CaseStatus"] = "The repair spec was duplicated.";
            return RedirectToEstimate(id, copy.SpecificationId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The estimate was not duplicated because the case changed or another editor holds it. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    /// <summary>Discards a non-current draft estimate with the supplied reason.</summary>
    public async Task<IActionResult> OnPostDiscardEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["CaseError"] = "Give the reason this repair spec is deleted.";
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }

        try
        {
            await discardEstimate.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, reason.Trim(), editLeaseToken!, estimateId) { Work = WorkSelector },
                cancellationToken);
            ClearLeaseState();
            await ReclaimLeaseAsync(id, cancellationToken);
            TempData["CaseStatus"] = "The repair spec was discarded.";
            return RedirectToEstimate(id);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The estimate was not deleted because the case changed or another editor holds it. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    /// <summary>
    /// Makes an estimate Current. Core derives its calculation basis and
    /// completes a cited Draft-ready Estimate job when applicable.
    /// </summary>
    public async Task<IActionResult> OnPostSetCurrentEstimateAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        Guid estimateId,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
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
            await setCurrentEstimate.ExecuteAsync(
                new(id, currentCaseVersion, actor, operationKey, "Estimate made current", editLeaseToken!, estimateId) { Work = WorkSelector },
                cancellationToken);
            ClearLeaseState();
            await ReclaimLeaseAsync(id, cancellationToken);
            TempData["CaseStatus"] = "The repair spec is now the case's current repair spec.";
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The estimate was not made current because the case changed or another editor holds it. Retry the operation.");
            return RedirectToEstimate(id, estimateId.ToString("D"));
        }
    }

    /// <summary>Read the owning staff member's Glass's controls without changing Case or lease state.</summary>
    public async Task<IActionResult> OnGetGlassSessionAsync(
        Guid id, [FromHeader(Name = "X-Pegasus-Edit-Lease")] string? renderLeaseToken,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TryGetActor(out var actor)) { return Forbid(); }
        Case = await getCasePageFrame.ExecuteAsync(new(id, actor, Work: WorkSelector), cancellationToken);
        if (Case is null) { return FragmentNotFound(); }
        ApplyAssessmentAccess(actor, Case.Workflow);
        if (!AssessmentCanOpen) { return FragmentNotFound(); }
        // Like lazy section reads, this GET never restores or writes TempData.
        if (!string.IsNullOrWhiteSpace(renderLeaseToken) && validateCaseRenderLease is not null
            && await validateCaseRenderLease.ExecuteAsync(new(id, actor, renderLeaseToken), cancellationToken))
        {
            fragmentLeaseToken = renderLeaseToken;
        }
        await LoadGlassSessionAsync(id, actor, cancellationToken);
        return Partial("/Pages/Cases/Shared/_GlassControls.cshtml", this);
    }

    /// <summary>
    /// Starts a Glass's Repair Estimate for this Case and sends the staff
    /// member's own browser to the Glass's window, which opens the provider's
    /// estimator once the background launch has prepared it.
    /// </summary>
    /// <remarks>
    /// The launch stands on exactly the authority a Case write stands on — the
    /// Estimate section's own guard, so the version, the edit lease and the
    /// operation key are the ones every other Estimate command presents — and
    /// the gateway re-proves them against the Case and records the session
    /// before this request answers. The provider work runs in the background.
    /// The address the operator is finally sent to is never a form value: it is
    /// read back from the session's protected state by the staff member who
    /// created it, because it carries the one-use token the provider will
    /// return with.
    ///
    /// The gateway is a handler service rather than a page dependency: it is
    /// built from <c>Glass:*</c> configuration, and a host that has none must
    /// still serve every other part of the Case record.
    /// </remarks>
    public async Task<IActionResult> OnPostLaunchGlassAsync(
        Guid id,
        long expectedCaseVersion,
        string operationKey,
        string? editLeaseToken,
        [FromServices] IGlassRepairEstimateGateway glassEstimates,
        [FromServices] Pegasus.Web.Background.ProviderWorkQueue glassWork,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            // Answered in the Glass's window: a refusal goes back to the Case
            // window that posted it, as every other outcome here does.
            return RefusedGlassGuard(id, "LaunchGlass", guard);
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        // The new session's id is chosen here and held busy before the session
        // exists, so no window can find it idle between its claim and its work.
        // A double-click replays one operation key and gets the session the
        // first click created and holds, so it waits on the same work.
        var newSessionId = Guid.NewGuid();
        using var reservation = glassWork.Reserve(newSessionId);
        try
        {
            var step = await glassEstimates.PrepareLaunchAsync(
                new GlassRepairEstimateLaunchRequest(
                    actor,
                    id,
                    expectedCaseVersion,
                    editLeaseToken!,
                    operationKey,
                    newSessionId),
                cancellationToken);
            return await ContinueGlassAsync(actor, step, reservation, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IsGlassRefusal(exception))
        {
            return RefuseGlassCommand(id, editLeaseToken, exception, GlassLabels.LaunchRefused);
        }
    }

    /// <summary>
    /// Picks this staff member's Glass's session back up: an open calculation is
    /// re-opened at the provider, and a held result is imported now that the
    /// Case's edit authority has been regained.
    /// </summary>
    /// <remarks>
    /// Every resume carries the presented Case version and lease. The gateway
    /// proves current authority and unchanged vehicle facts before provider work,
    /// and the provider work runs in the background as a launch's does.
    /// The form must name this staff member's session on this Case.
    /// </remarks>
    public async Task<IActionResult> OnPostResumeGlassAsync(
        Guid id,
        long expectedCaseVersion,
        string operationKey,
        string? editLeaseToken,
        Guid sessionId,
        long expectedSessionVersion,
        [FromServices] IGlassRepairEstimateGateway glassEstimates,
        [FromServices] Pegasus.Web.Background.ProviderWorkQueue glassWork,
        CancellationToken cancellationToken)
    {
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return RefusedGlassGuard(id, "ResumeGlass", guard);
        }
        if (!TryGetActor(out var actor) || !Guid.TryParse(actor.SubjectId, out var staffId))
        {
            return Forbid();
        }

        var held = await glassSessions.GetForCaseAsync(id, staffId, cancellationToken);
        if (held is null || held.Id != sessionId)
        {
            TempData["CaseError"] = GlassLabels.ResumeRefused;
            return GlassReturn(id);
        }
        if (glassWork.IsInFlight(sessionId))
        {
            // Work for this session is already running: wait on it.
            return GlassOpening(this, sessionId);
        }

        // Held busy before the claim, so no window settles it before its work runs.
        using var reservation = glassWork.Reserve(sessionId);
        try
        {
            // Every resume proves current authority and unchanged vehicle facts.
            var step = await glassEstimates.PrepareResumeAsync(
                new GlassRepairEstimateResumeRequest(
                    actor,
                    sessionId,
                    expectedSessionVersion,
                    expectedCaseVersion,
                    editLeaseToken!),
                cancellationToken);
            return await ContinueGlassAsync(actor, step, reservation, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (IsGlassRefusal(exception))
        {
            return RefuseGlassCommand(id, editLeaseToken, exception, GlassLabels.ResumeRefused);
        }
    }

    /// <summary>
    /// Where a launch or a resume leaves the operator: the provider work it
    /// owes is queued and the Glass's window waits for it, then opens the
    /// estimator or reports what the session came to.
    /// </summary>
    /// <remarks>
    /// The work is queued under the reservation this request took before the
    /// claim. A full queue never drops it: the work runs here, as the request
    /// always ran it before, and the window then reports what it came to.
    /// </remarks>
    private async Task<IActionResult> ContinueGlassAsync(
        ActionActor actor,
        GlassRepairEstimateStep step,
        Pegasus.Web.Background.ProviderWorkReservation reservation,
        CancellationToken cancellationToken)
    {
        if (step.Continuation != GlassRepairEstimateContinuation.None)
        {
            var work = Pegasus.Web.Pages.Integrations.Glass.GlassSessionWork.For(actor, step);
            if (reservation.Admit(work) == Pegasus.Web.Background.ProviderWorkAdmission.Full)
            {
                await reservation.RunHereAsync(work, HttpContext.RequestServices, cancellationToken);
            }
        }

        return GlassOpening(this, step.Session.Id);
    }

    /// <summary>The Glass's window for a session whose provider work is queued, running or done.</summary>
    public static RedirectToPageResult GlassOpening(PageModel page, Guid sessionId)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Response.Headers.CacheControl = "no-store";
        return page.RedirectToPage("/Integrations/Glass/Opening", new { sessionId });
    }

    /// <summary>
    /// Where every Glass's answer but the estimator itself goes: the provider
    /// runs in a window opened from the Case record, so the answer hands that
    /// record's Estimate section back to the window that holds it rather than
    /// rendering it where the provider was. Shared with the provider's return;
    /// a command posted from the Case returns to the view it was posted from.
    /// </summary>
    public static PartialViewResult GlassReturn(PageModel page, Guid caseId, string? view = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        page.Response.Headers.CacheControl = "no-store";
        return page.Partial(
            "_GlassReturn",
            page.Url.Page("/Cases/Details", new { id = caseId, section = "estimate", view })!);
    }

    private PartialViewResult GlassReturn(Guid id) => GlassReturn(this, id, ReturnView);

    /// <summary>
    /// The one operator-facing reading of a settled Glass's session, shared by
    /// the Estimate section's commands and the provider's own return.
    /// </summary>
    public static IActionResult ReportSessionOutcome(
        GlassRepairEstimateSession session,
        ITempDataDictionary tempData,
        Func<IActionResult> estimateSection)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(tempData);
        ArgumentNullException.ThrowIfNull(estimateSection);
        tempData[session.State == GlassRepairEstimateSessionState.Completed ? "CaseStatus" : "CaseError"] =
            GlassLabels.OutcomeMessage(session.State);
        return estimateSection();
    }

    /// <summary>
    /// What a launch, a resume or the Glass's window reports for a session
    /// that is not open at the provider.
    /// </summary>
    public static IActionResult ReportGlassSession(
        PageModel page, GlassRepairEstimateSession session, string refusal)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(session);
        if (session.State is GlassRepairEstimateSessionState.Prepared
            or GlassRepairEstimateSessionState.Launching)
        {
            // Never opened at the provider and never settled: nothing to
            // report but the refusal the command carries.
            page.TempData["CaseError"] = refusal;
            return GlassReturn(page, session.CaseId);
        }

        return ReportSessionOutcome(session, page.TempData, () => GlassReturn(page, session.CaseId));
    }

    public async Task<IActionResult> OnPostCloseGlassAsync(
        Guid id, Guid sessionId, long expectedSessionVersion, bool externalSessionClosed,
        string? reason, [FromServices] IGlassRepairEstimateGateway glassEstimates,
        [FromServices] Pegasus.Web.Background.ProviderWorkQueue glassWork, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || actor.Kind != ActorKind.Staff
            || !Guid.TryParse(actor.SubjectId, out var staffId))
        {
            return Forbid();
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access?.CanOpen != true)
        {
            return NotFound();
        }
        var ownSession = await glassSessions.GetForCaseAsync(id, staffId, cancellationToken);
        if (ownSession?.Id != sessionId)
        {
            return NotFound();
        }
        if (glassWork.IsInFlight(sessionId))
        {
            // Closing while Glass's is being prepared or brought back would
            // leave whatever that work creates at the provider without a session.
            TempData["CaseError"] = GlassLabels.CloseWhileWorking;
            return RedirectToEstimate(id);
        }
        try
        {
            await glassEstimates.CloseAsync(new(actor, sessionId, expectedSessionVersion,
                externalSessionClosed, reason ?? string.Empty), cancellationToken);
            TempData["CaseStatus"] = GlassLabels.Closed;
        }
        catch (GlassRepairEstimateSessionConflictException conflict)
            when (conflict.Conflict == GlassRepairEstimateSessionConflict.Version)
        {
            TempData["CaseError"] = GlassLabels.CloseChanged;
        }
        catch (Exception exception) when (IsGlassRefusal(exception) || exception is StaffAuthorizationException)
        {
            TempData["CaseError"] = GlassLabels.CloseRefused;
        }
        return RedirectToEstimate(id);
    }

    /// <summary>
    /// A Glass's command the Estimate guard refused before the gateway was
    /// asked. Logged once with the notice the guard set, so a report of the
    /// same screen can be told apart from a provider outcome.
    /// </summary>
    private IActionResult RefusedGlassGuard(Guid id, string handler, IActionResult guard)
    {
        if (guard is not RedirectToPageResult)
        {
            return guard;
        }
        LogGlassCommandRefused(logger, id, handler, TempData["CaseError"] as string ?? guard.GetType().Name);
        return GlassReturn(id);
    }

    private PartialViewResult RefuseGlassCommand(
        Guid id, string? editLeaseToken, Exception exception, string refusal)
    {
        LogGlassCommandRefused(
            logger,
            id,
            refusal == GlassLabels.ResumeRefused ? "ResumeGlass" : "LaunchGlass",
            exception is GlassRepairEstimateSessionConflictException conflict
                ? $"{conflict.GetType().Name}:{conflict.Conflict}"
                : exception.GetType().Name);
        HandleLeaseFailure(id, editLeaseToken, exception);
        TempData["CaseError"] = exception switch
        {
            // A stale session version, a spent callback or another staff member's
            // session says nothing an operator can act on beyond the refusal.
            GlassRepairEstimateSessionConflictException
            {
                Conflict: not GlassRepairEstimateSessionConflict.ActiveAccount
            } => refusal,
            // The Case moved since this page rendered (custody confirming a
            // file, another save) or its edit lease ended: reloading is the cure.
            CaseVersionConflictException or CaseEditLeaseConflictException or CaseEditLeaseExpiredException
                => GlassLabels.CaseChanged,
            _ => MutationRefusalMessage(exception, refusal),
        };
        return GlassReturn(id);
    }

    private static bool IsGlassRefusal(Exception exception) =>
        exception is GlassRepairEstimateRefusalException
            or GlassRepairEstimateSessionConflictException
            or CaseVersionConflictException
            or CaseEditLeaseConflictException
            or CaseEditLeaseExpiredException
            or ArgumentException
            or KeyNotFoundException;

    private long currentCaseVersion;

    private async Task<IActionResult?> GuardEstimateEditAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return Forbid();
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access?.CanOpen != true)
        {
            return NotFound();
        }
        if (access.IsReadOnly)
        {
            TempData["CaseError"] = "The case is read-only once Complete.";
            return RedirectToEstimate(id);
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return RedirectToEstimate(id);
        }
        if (string.IsNullOrWhiteSpace(editLeaseToken))
        {
            TempData["CaseError"] = NotInEditMode;
            return RedirectToEstimate(id);
        }

        var header = await getCaseHeader.ExecuteAsync(new(id, actor), cancellationToken);
        if (header is null)
        {
            return NotFound();
        }
        currentCaseVersion = header.Workflow.Version;
        return null;
    }

    private async Task<RepairSpecificationVersion?> ResolveEstimateAsync(
        Guid caseId,
        Guid? estimateId,
        CancellationToken cancellationToken) =>
        estimateId is { } selected
            ? await repairSpecifications.GetVersionAsync(caseId, selected, cancellationToken)
            : null;

    private async Task<IActionResult> RedrawEditorAsync(
        Guid id,
        Guid? estimateId,
        EstimateEditorPost editor,
        IReadOnlyList<EstimateEditorLine> rows,
        CancellationToken cancellationToken)
    {
        var result = await OnGetAsync(
            id,
            estimateId?.ToString("D"),
            null,
            Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                .GetRequiredService<TriageCasePorts>(HttpContext.RequestServices),
            cancellationToken);
        if (Case is null)
        {
            return result;
        }

        SelectedEstimate = estimateId is { } selected
            ? Estimates.FirstOrDefault(item => item.SpecificationId == selected)
            : null;
        EditingNewEstimate = estimateId is null;
        // Redrawing a row must not change what the totals mean: the header
        // is read back on exactly the terms the save reads it.
        EditorDetails = EditorDetailsFrom(editor, SelectedEstimate);
        EditorLines = rows.Count > 0 ? rows : [new EstimateEditorLine("", null, null, null, null, null, null, null)];
        ShowingPostedEstimate = true;
        return Page();
    }

    private sealed record EstimateEditorPost(
        string? Name,
        decimal? LabourRate,
        bool RegionalUplift,
        decimal? OtherCosts,
        decimal? VatPercent,
        RepairerVatStatus VatStatus,
        EstimateVatCategories VatCategories,
        EstimateDiscounts Discounts,
        Guid? EstimateId,
        IReadOnlyList<EstimateEditorLine> Rows,
        IReadOnlyList<EstimateLineInput>? Lines,
        IReadOnlyList<Guid?> ExistingLineIds,
        IReadOnlyList<int> LineRows)
    {
        /// <summary>
        /// The posted VAT policy, revised from the spec's saved one
        /// (<see cref="EstimateVatPolicy.Revised"/>): categories the operator
        /// did not choose by hand follow a changed status; categories that
        /// differ from the status's own are the operator's override.
        /// </summary>
        public EstimateVatPolicy VatPolicyFrom(EstimateVatPolicy? saved) => EstimateVatPolicy.Revised(
            saved ?? EstimateVatPolicy.For(RepairerVatStatus.Unknown),
            VatStatus,
            VatCategories);
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

    private static EstimateDetails EditorDetailsFrom(
        EstimateEditorPost editor, RepairSpecificationVersion? existing) => EstimatePolicy.RetainEditorRate(new(
        editor.Name ?? string.Empty,
        editor.LabourRate,
        editor.OtherCosts,
        editor.VatPercent ?? EstimatePolicy.DefaultVatPercent,
        editor.Discounts,
        editor.VatPolicyFrom(existing?.Details.VatPolicy),
        RegionalUplift: editor.RegionalUplift), existing?.Details);

    private EstimateEditorPost ReadEditorPost()
    {
        var form = Request.Form;
        static decimal? Money(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : decimal.MinusOne;
        // A discount is typed as a percentage and held as a fraction. A blank
        // box is no discount; anything unreadable stays negative through the
        // conversion, so EstimatePolicy.ValidateDiscounts' [0,1] rule refuses
        // it rather than the screen guessing a value.
        static decimal Fraction(string? value) => (Money(value) ?? 0m) / 100m;
        // A checked box posts "true" ahead of its hidden "false"; an
        // unchecked box posts the "false" alone (CaseMutationPageModel's
        // BooleanFormFields convention), so the first entry is the answer.
        bool Checked(string field) =>
            bool.TryParse(form[field].FirstOrDefault(), out var value) && value;

        var operations = form["lineOperation"].ToArray();
        var postedLineIds = form["lineId"].ToArray();
        var descriptions = form["lineDescription"].ToArray();
        var partNumbers = form["linePartNumber"].ToArray();
        var quantities = form["lineQuantity"].ToArray();
        var labourHoursValues = form["lineLabourHours"].ToArray();
        var paintHoursValues = form["linePaintHours"].ToArray();
        var partPoundsValues = form["linePartPounds"].ToArray();
        var materialsValues = form["lineMaterials"].ToArray();
        var rows = new List<EstimateEditorLine>(operations.Length);
        var lines = new List<EstimateLineInput>(operations.Length);
        var existingLineIds = new List<Guid?>(operations.Length);
        // The posted row each line came from: blank rows are not lines.
        var lineRows = new List<int>(operations.Length);
        var linesAreValid = true;
        static string Field(string?[] values, int index) =>
            index >= 0 && index < values.Length && values[index] is not null ? values[index]! : string.Empty;
        for (var index = 0; index < operations.Length; index++)
        {
            var operation = operations[index] ?? string.Empty;
            var description = Field(descriptions, index);
            var partNumber = Field(partNumbers, index);
            var quantity = Field(quantities, index);
            var labourHours = Field(labourHoursValues, index);
            var paintHours = Field(paintHoursValues, index);
            var partPounds = Field(partPoundsValues, index);
            var lineMaterials = Field(materialsValues, index);
            var existingLineId = Guid.TryParse(Field(postedLineIds, index), out var parsedLineId)
                ? parsedLineId
                : (Guid?)null;
            rows.Add(new EstimateEditorLine(
                operation, description, partNumber, quantity, labourHours, paintHours, partPounds, lineMaterials, existingLineId));

            var isEmpty = string.IsNullOrWhiteSpace(description)
                && string.IsNullOrWhiteSpace(partNumber)
                && string.IsNullOrWhiteSpace(quantity)
                && string.IsNullOrWhiteSpace(labourHours)
                && string.IsNullOrWhiteSpace(paintHours)
                && string.IsNullOrWhiteSpace(partPounds)
                && string.IsNullOrWhiteSpace(lineMaterials);
            if (isEmpty)
            {
                continue;
            }
            existingLineIds.Add(existingLineId);
            lineRows.Add(index);

            var typed = EstimateOperations.TryParse(operation, out var parsedOperation);
            var workUnits = Money(labourHours);
            var paintWorkUnits = Money(paintHours);
            var price = Money(partPounds);
            var materials = Money(lineMaterials);
            int? parsedQuantity = null;
            if (!string.IsNullOrWhiteSpace(quantity))
            {
                parsedQuantity = int.TryParse(
                    quantity.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var quantityValue) && quantityValue >= 0
                    ? quantityValue
                    : -1;
            }
            if (!typed
                || workUnits == decimal.MinusOne
                || paintWorkUnits == decimal.MinusOne
                || price == decimal.MinusOne
                || materials == decimal.MinusOne
                || parsedQuantity == -1
                || workUnits is < 0
                || paintWorkUnits is < 0
                || price is < 0
                || materials is < 0)
            {
                linesAreValid = false;
                continue;
            }

            lines.Add(new(
                EstimateOperations.ToLineType(parsedOperation),
                null,
                string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                workUnits,
                price,
                false,
                string.IsNullOrWhiteSpace(partNumber) ? null : partNumber.Trim(),
                null,
                null,
                null,
                paintWorkUnits,
                parsedQuantity,
                materials));
        }

        Guid? estimateId = Guid.TryParse(form["estimateId"].ToString(), out var parsedId)
            && parsedId != Guid.Empty
            ? parsedId
            : null;
        var categories = EstimateVatCategories.None;
        foreach (var category in EstimateVatLabels.Categories)
        {
            if (Checked(VatCategoryField(category)))
            {
                categories |= category;
            }
        }

        return new(
            form["estimateName"].ToString(),
            Money(form["estimateLabourRate"].ToString()),
            Checked("estimateRegionalUplift"),
            Money(form["estimateOtherCosts"].ToString()),
            Money(form["estimateVatPercent"].ToString()),
            // Enum.TryParse accepts any number, so a posted value that is not
            // one of the three named states falls back to Unknown rather than
            // reaching Core as an undefined status.
            Enum.TryParse<RepairerVatStatus>(form["estimateVatStatus"].ToString(), out var status)
                && Enum.IsDefined(status)
                ? status
                : RepairerVatStatus.Unknown,
            categories,
            new(
                Fraction(form["estimateDiscountParts"].ToString()),
                Fraction(form["estimateDiscountMaterials"].ToString()),
                Fraction(form["estimateDiscountSpecialist"].ToString()),
                Fraction(form["estimateDiscountOverall"].ToString())),
            estimateId,
            rows,
            linesAreValid ? lines : null,
            existingLineIds,
            lineRows);
    }

    /// <summary>
    /// The form field one VAT category is posted under, so the screen and
    /// the reader never name the same box two different ways.
    /// </summary>
    public static string VatCategoryField(EstimateVatCategories category) =>
        "estimateVat" + category;

    /// <summary>Retain once, then use the same source-backed import as MCP and Glass's.</summary>
    public async Task<IActionResult> OnPostImportEstimateAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        IFormFile? estimateFile,
        CancellationToken cancellationToken)
    {
        var (actor, documents, refusal) = await StartEstimateImportAsync(
            id, expectedVersion, operationKey, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }
        if (!Request.HasFormContentType || Request.Form.Files.Count != 1
            || !string.Equals(Request.Form.Files[0].Name, "estimateFile", StringComparison.Ordinal)
            || estimateFile is null)
        {
            TempData["CaseError"] = "Choose exactly one estimate file.";
            return RedirectToEstimate(id);
        }
        estimateFile = Request.Form.Files[0];
        var fileName = Path.GetFileName(estimateFile.FileName);
        if (estimateFile.Length is <= 0 or > ImportRawEstimate.MaximumDocumentBytes
            || EstimateFormatCount(fileName, estimateFile.ContentType) != 1)
        {
            TempData["CaseError"] = "Choose a non-empty supported estimate file of 32 MiB or less.";
            return RedirectToEstimate(id);
        }

        // Bound the actual stream too; the form's advertised length is not authority.
        await using var input = estimateFile.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > ImportRawEstimate.MaximumDocumentBytes)
            {
                TempData["CaseError"] = "Choose an estimate file of 32 MiB or less.";
                return RedirectToEstimate(id);
            }
            buffer.Write(chunk, 0, count);
        }
        if (buffer.Length == 0)
        {
            TempData["CaseError"] = "Choose a non-empty estimate file.";
            return RedirectToEstimate(id);
        }

        var fileBytes = buffer.ToArray();
        var uploadedSha256 = Convert.ToHexStringLower(SHA256.HashData(fileBytes));
        var (activeLeaseToken, claimResult) = await ClaimEstimateImportAsync(
            actor!, id, expectedVersion, operationKey, editLeaseToken, uploadedSha256,
            "case-estimate-import-form", cancellationToken);
        if (claimResult is not null)
        {
            return claimResult;
        }

        // The same bytes already confirmed in Case Files — a retry, or a file
        // that arrived by email — are imported from there, not stored again,
        // when one estimate format names that stored file: the import reads
        // its stored name and type, not the dropped file's. The bytes are
        // the dropped file's, which the import parses now, so this does not
        // wait for the Worker to have read the stored copy.
        var sourceIdentity = $"estimate-import:{operationKey}";
        var reusable = CaseFiles.Live(documents)
            .Where(file => file.Version.ContentLength == fileBytes.LongLength
                && string.Equals(file.Version.Sha256, uploadedSha256, StringComparison.OrdinalIgnoreCase)
                && file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed
                && file.Occurrence.Source != DocumentSource.Generated
                && EstimateFormatCount(file.Version.FileName, file.Version.MediaType) == 1)
            .OrderBy(file => file.Occurrence.Ordinal)
            .FirstOrDefault();

        CaseFile source;
        var newDocumentStored = false;
        if (reusable is not null
            && !string.Equals(reusable.Occurrence.SourceOccurrenceIdentity, sourceIdentity, StringComparison.Ordinal))
        {
            source = reusable;
        }
        else
        {
            AddCaseDocumentResult retained;
            try
            {
                retained = await addCaseDocument.ExecuteAsync(
                    new(id, fileName, estimateFile.ContentType, fileBytes,
                        DocumentSemanticRole.Other, DocumentSource.StaffUpload, sourceIdentity,
                        actor!, $"{operationKey}-document", expectedVersion, activeLeaseToken!),
                    cancellationToken);
            }
            catch (StaffAuthorizationException) { return Forbid(); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                HandleLeaseFailure(id, activeLeaseToken, exception);
                TempData["CaseError"] = MutationRefusalMessage(
                    exception, Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateImport.StorageFailed);
                return RedirectToEstimate(id);
            }
            source = new(retained.Occurrence, retained.Version);
            newDocumentStored = !retained.IsReplay;
        }

        var importVersion = expectedVersion;
        var importLeaseToken = activeLeaseToken!;
        try
        {
            if (newDocumentStored)
            {
                // A new Case document consumes one version and its lease. Claim
                // exactly that version before the importer performs its mutation.
                importVersion = checked(expectedVersion + 1);
                var lease = await acquireLease.ExecuteAsync(
                    new(id, importVersion, actor!, NewOperationKey()), cancellationToken);
                importLeaseToken = lease.Token;
                StoreLeaseAuthority(id, lease.Token);
            }

            if (source.Version.CustodyStatus != DocumentCustodyStatus.Confirmed)
            {
                TempData["CaseError"] = "The estimate source could not be confirmed in Case Files, so nothing was imported.";
                return RedirectToEstimate(id);
            }

            var importedBefore = (await listEstimates.ExecuteAsync(id, WorkSelector, cancellationToken))
                .Any(estimate => estimate.State != RepairSpecificationState.Discarded
                    && string.Equals(
                        estimate.Source.Sha256, source.Version.Sha256, StringComparison.OrdinalIgnoreCase));
            var resultingVersion = checked(importVersion + (importedBefore ? 0 : 1));
            return await ImportRetainedEstimateAsync(new(actor!, id, importVersion, importLeaseToken,
                source.Occurrence.Id, source.Version.Id, source.Version.Sha256, operationKey, string.Empty) { Work = WorkSelector },
                cancellationToken, expectedVersion, resultingVersion);
        }
        catch (StaffAuthorizationException) { return Forbid(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleLeaseFailure(id, PeekLeaseToken(), exception);
            TempData["CaseError"] = MutationRefusalMessage(exception, "The source was retained, but the import could not be confirmed. Retry the same file.");
            return RedirectToEstimate(id);
        }
    }

    /// <summary>
    /// Imports an estimate the Case already holds in Case Files — one that
    /// arrived by email, say — through the same import as a drop, so no second
    /// copy is stored (operator, 25 September 2026).
    /// </summary>
    public async Task<IActionResult> OnPostImportCaseFileEstimateAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        Guid occurrenceId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var (actor, documents, refusal) = await StartEstimateImportAsync(
            id, expectedVersion, operationKey, cancellationToken);
        if (refusal is not null)
        {
            return refusal;
        }
        var source = CaseFiles.Live(documents)
            .FirstOrDefault(file => file.Occurrence.Id == occurrenceId && file.Version.Id == versionId);
        if (source is null || !IsImportableEstimate(source))
        {
            TempData["CaseError"] = Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateImport.NotAnEstimateFile;
            return RedirectToEstimate(id);
        }

        var (leaseToken, claimResult) = await ClaimEstimateImportAsync(
            actor!, id, expectedVersion, operationKey, editLeaseToken, source.Version.Sha256,
            editor: null, cancellationToken);
        if (claimResult is not null)
        {
            return claimResult;
        }
        try
        {
            return await ImportRetainedEstimateAsync(
                new(actor!, id, expectedVersion, leaseToken!, source.Occurrence.Id, source.Version.Id,
                    source.Version.Sha256, operationKey, string.Empty) { Work = WorkSelector },
                cancellationToken);
        }
        catch (StaffAuthorizationException) { return Forbid(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleLeaseFailure(id, PeekLeaseToken(), exception);
            TempData["CaseError"] = MutationRefusalMessage(exception, "The estimate could not be imported. Retry the import.");
            return RedirectToEstimate(id);
        }
    }

    /// <summary>
    /// Whether a Case file can be imported as a repair spec from its Files row:
    /// the Engineer sections are editable and <see cref="IsImportableEstimate"/>
    /// holds. Importing a file already imported replays to the spec it made.
    /// </summary>
    public bool CanImportEstimate(CaseFile file) => CanEditEngineering && IsImportableEstimate(file);

    /// <summary>
    /// A confirmed file Pegasus did not generate that the Worker, reading it
    /// once after it was filed, found to be an estimate
    /// (<see cref="RecogniseFiledEstimates"/>). The name alone never makes a
    /// file one: every PDF names the PDF format.
    /// </summary>
    private static bool IsImportableEstimate(CaseFile file) =>
        file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed
        && file.Occurrence.Source != DocumentSource.Generated
        && file.Version.IsRecognisedEstimate == true;

    private int EstimateFormatCount(string fileName, string mediaType)
    {
        var extension = Path.GetExtension(fileName);
        return estimateParsers.Count(parser =>
            parser.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)
            && parser.CanParse(fileName, mediaType));
    }

    /// <summary>
    /// The checks every estimate import makes before it reads or retains a
    /// source: the staff member, assessment access, a writable Case, a live
    /// form and the Case version the form was rendered at. Once they pass, the
    /// Case's documents, which the import reuses or reads its source from.
    /// </summary>
    private async Task<(ActionActor? Actor, IReadOnlyList<CaseDocument> Documents, IActionResult? Refusal)> StartEstimateImportAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            ClearLeaseState();
            return (null, [], Forbid());
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access?.CanOpen != true) return (null, [], NotFound());
        var header = await getCaseHeader.ExecuteAsync(new(id, actor), cancellationToken);
        if (header is null) return (null, [], NotFound());
        if (access.IsReadOnly || header.Workflow.Archive is not null
            || CaseLifecycleRules.IsTerminal(header.Workflow.State))
        {
            TempData["CaseError"] = "The Case is read-only and cannot accept estimate imports.";
            return (null, [], RedirectToEstimate(id));
        }
        if (!IsOperationKeyValid(operationKey))
        {
            TempData["CaseError"] = "The form has expired. Retry the operation.";
            return (null, [], RedirectToEstimate(id));
        }
        if (header.Workflow.Version != expectedVersion)
        {
            TempData["CaseError"] = "The Case changed before the estimate was imported. Reload and try again.";
            return (null, [], RedirectToEstimate(id));
        }
        return (actor, await caseDocuments.ListAsync(id, cancellationToken), null);
    }

    /// <summary>
    /// Takes the edit lease when the import starts from read mode, proves the
    /// import authority and answers a replay of the same operation and source.
    /// A result means the import ends here; otherwise the lease token is the
    /// one to import under. <paramref name="editor"/> names the client form
    /// whose commit a replay records, when there is one.
    /// </summary>
    private async Task<(string? LeaseToken, IActionResult? Result)> ClaimEstimateImportAsync(
        ActionActor actor,
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        string sha256,
        string? editor,
        CancellationToken cancellationToken)
    {
        var activeLeaseToken = editLeaseToken;
        if (string.IsNullOrWhiteSpace(activeLeaseToken))
        {
            try
            {
                var lease = await acquireLease.ExecuteAsync(
                    new(id, expectedVersion, actor, NewOperationKey()), cancellationToken);
                activeLeaseToken = lease.Token;
                StoreLeaseAuthority(id, lease.Token);
            }
            catch (StaffAuthorizationException) { return (null, Forbid()); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                HandleLeaseFailure(id, null, exception);
                TempData["CaseError"] = MutationRefusalMessage(exception, "The Case cannot be edited right now.");
                return (null, RedirectToEstimate(id));
            }
        }

        try
        {
            await repairSpecifications.RequireImportAuthorityAsync(
                new(actor, id, expectedVersion, activeLeaseToken!, Guid.Empty, Guid.Empty,
                    sha256, operationKey, string.Empty), cancellationToken);
        }
        catch (StaffAuthorizationException) { return (null, Forbid()); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleLeaseFailure(id, activeLeaseToken, exception);
            TempData["CaseError"] = MutationRefusalMessage(exception, "The Case cannot be edited right now.");
            return (null, RedirectToEstimate(id));
        }

        try
        {
            if (await repairSpecifications.ProbeSourceHashReplayAsync(
                    id, operationKey, sha256, cancellationToken)
                is { } replay)
            {
                if (editor is not null)
                {
                    RecordEditorCommit(editor, operationKey, expectedVersion, expectedVersion);
                }
                TempData["CaseStatus"] = await ImportedMessageAsync(id, replay.EstimateId, cancellationToken);
                return (null, RedirectToEstimate(id, replay.EstimateId.ToString("D")));
            }
        }
        catch (StaffAuthorizationException) { return (null, Forbid()); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The source was retained, but the import could not be confirmed. Retry the same file.");
            return (null, RedirectToEstimate(id));
        }
        return (activeLeaseToken, null);
    }

    private async Task<IActionResult> ImportRetainedEstimateAsync(
        ImportRawEstimateRequest request,
        CancellationToken cancellationToken,
        long? commandExpectedVersion = null,
        long? commandResultingVersion = null)
    {
        try
        {
            var result = await importRawEstimate.ExecuteAsync(request, cancellationToken);
            // A source-hash replay consumes no edit authority, so the posted
            // token is still live and is kept; a real import consumed it, and
            // the session carries on with a fresh lease (v25 decision F).
            var after = await getCaseHeader.ExecuteAsync(new(request.CaseId, request.Actor), cancellationToken);
            if (after?.ActiveEditLease is null)
            {
                ClearLeaseState();
                await ReclaimLeaseAsync(request.CaseId, cancellationToken);
            }
            else
            {
                StoreLeaseAuthority(request.CaseId, request.EditLeaseToken);
            }
            if (commandExpectedVersion is { } originalVersion && commandResultingVersion is { } finalVersion)
            {
                RecordEditorCommit("case-estimate-import-form", request.OperationKey, originalVersion, finalVersion);
            }
            TempData["CaseStatus"] = await ImportedMessageAsync(request.CaseId, result.EstimateId, cancellationToken);
            return RedirectToEstimate(request.CaseId, result.EstimateId.ToString("D"));
        }
        catch (EstimateParseRejectedException exception)
        {
            TempData["CaseError"] = exception.Message;
        }
        return RedirectToEstimate(request.CaseId);
    }

    /// <summary>
    /// What an import reports about the spec it returned. A staff import makes
    /// the spec it creates the one in use; a same-file replay returns the spec
    /// that file already made and leaves the spec in use as it was (FRD-25), so
    /// the message states which of the two happened rather than claiming use.
    /// </summary>
    private async Task<string> ImportedMessageAsync(Guid caseId, Guid estimateId, CancellationToken cancellationToken)
    {
        var estimates = await listEstimates.ExecuteAsync(caseId, WorkSelector, cancellationToken);
        return estimates.Any(estimate => estimate.SpecificationId == estimateId && estimate.IsCurrent)
            ? Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateImport.Imported
            : Pegasus.Web.Presentation.CaseWorkspaceLabels.EstimateImport.AlreadyImported;
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

    private RedirectToPageResult RedirectToEstimate(
        Guid id,
        string? estimate = null,
        string? dialog = null) =>
        RedirectToPage(
            "/Cases/Details",
            new { id, section = "estimate", estimate, dialog, view = ReturnView });

    /// <summary>The page state the frame's extra reads start from.</summary>
    private sealed record WorkspaceExtrasInputs(
        CasePageFrame Details,
        bool AssessmentCanOpen,
        IReadOnlyList<SignOffEngineerProfile> EligibleSignOffEngineers,
        string? LeaseToken);

    private sealed record WorkspaceExtras(
        string? EngineerDisplayName,
        string SignOffEngineerDisplayName,
        EvaHandoffViewModel EvaHandoff);

    /// <summary>
    /// The values the workspace frame names that the case projection does not
    /// carry directly: the assigned and Sign-off Engineer names, the Engineer
    /// choices available in Review, and whether API submission is a composed
    /// route this principal allows.
    /// </summary>
    private async Task<WorkspaceExtras> ReadWorkspaceExtrasAsync(
        WorkspaceExtrasInputs inputs,
        CancellationToken cancellationToken)
    {
        var details = inputs.Details;
        var workflow = details.Workflow;
        // In Review the Engineer choices list the staff accounts, which name
        // the assigned Engineer too, so that account is read on its own only
        // when the list does not hold it.
        IReadOnlyList<StaffAccountSummary> roster = workflow.State == CaseLifecycleState.Review
            ? (await staffAccountQueries.ListAsync(0, 100, cancellationToken)).Accounts
            : [];
        string? engineerDisplayName = null;
        if (workflow.AssignedEngineerId is { } engineerId)
        {
            var account = roster.FirstOrDefault(listed => listed.Id == engineerId)
                ?? await staffAccountQueries.GetAsync(engineerId, cancellationToken);
            engineerDisplayName = account?.UserName ?? ActorDisplayNames.UnknownStaff;
        }

        var profiles = inputs.AssessmentCanOpen ? inputs.EligibleSignOffEngineers
            : await staffAccountQueries.ListSignOffEngineersAsync(cancellationToken);
        var signOffEngineer = CaseSignOffEngineerResolver.Resolve(
            workflow.SignOffEngineerId,
            workflow.AssignedEngineerId,
            profiles);
        var signOffEngineerDisplayName = signOffEngineer?.PrintedName
            ?? Labels.CaseWorkspace.Unassigned;

        IReadOnlyList<EvaHandoffEngineerOption> engineerOptions = roster
            .Where(account => account.IsEnabled)
            .Select(account => new EvaHandoffEngineerOption(account.Id, account.UserName))
            .ToArray();

        var modes = await evaModeStore.GetForPrincipalAsync(
            workflow.Identity.PrincipalCode,
            cancellationToken);
        var canRetryAutomaticFailure = modes.Policy
            == PrincipalReportGenerationPolicy.EvaAutomaticApiOnReview
            && await evaSubmissionQueries.CanRetryAutomaticFailureAsync(
                workflow.CaseId,
                cancellationToken);
        var latestEvaSubmission = await evaSubmissionQueries.GetLatestAsync(
            workflow.CaseId,
            cancellationToken);
        return new(
            engineerDisplayName,
            signOffEngineerDisplayName,
            new(
                workflow.CaseId,
                workflow.Version,
                workflow.State,
                inputs.LeaseToken,
                engineerDisplayName ?? Labels.CaseWorkspace.Unassigned,
                engineerOptions,
                signOffEngineerDisplayName,
                signOffEngineer?.StaffId,
                profiles.Select(profile => new EvaHandoffEngineerOption(
                    profile.StaffId,
                    profile.PrintedName)).ToArray(),
                details.Data?.Completeness.Values.InstructionComplete ?? false,
                details.Data?.Completeness.Values.ImagesComplete ?? false,
                modes.Policy,
                submitCaseToEva is not null,
                EvaSubmissionPolicy.AllowsManualSubmission(modes) || canRetryAutomaticFailure,
                NewOperationKey(),
                NewOperationKey(),
                canRetryAutomaticFailure,
                canRetryAutomaticFailure && latestEvaSubmission is not { IsDelivered: true }));
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
        return BooleanFormFields.Contains(field) || (EditorLabels.Label(field) is not null && bool.TryParse(value, out _))
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
        // The mileage source is a typed assessment path, not a case-data field,
        // and the retained value reads as the word the select offered, not the
        // stored code. A code with no word of its own is shown as it stands.
        if (field == "vehicleMileageSource")
        {
            var code = AssessmentEditorValue(AssessmentVocabulary.VehicleMileageSource);
            return code is not null && CaseVehicleMileageSourcePolicy.StaffChoices.Contains(code)
                ? Pegasus.Web.Presentation.CaseWorkspaceLabels.Vehicle.MileageSource(code)
                : code;
        }
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
            "contactName" => Accepted(data.Contact.Name)?.Value,
            "contactEmailAddress" => Accepted(data.Contact.EmailAddress)?.Value,
            "contactPhoneNumber" => Accepted(data.Contact.PhoneNumber)?.Value,
            "vatStatus" => Accepted(data.Instruction.VatStatus)?.Value,
            "inspectionDate" => Accepted(data.Inspection.InspectionDate)?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "inspectionDeadline" => Accepted(data.Inspection.Deadline)?.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "inspectionAddress" => Accepted(data.Inspection.Address)?.Value,
            "inspectionMode" => Accepted(data.Inspection.Mode)?.Value.ToString(),
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

            // Two handlers name the same completeness flags differently; both compare against the
            // one projected value.
            "instructionComplete" or "instructionsComplete" =>
                YesOrNo(data.Completeness.Values.InstructionComplete),
            "imagesComplete" => YesOrNo(data.Completeness.Values.ImagesComplete),
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
        "vehicleMileageSource" => "Mileage source",
        "accidentCircumstances" => "Accident circumstances",
        "incidentDate" => "Incident date",
        "dueBy" => FrameLabels.Due,
        "contactName" => "Contact name",
        "contactEmailAddress" => "Contact email",
        "contactPhoneNumber" => "Contact phone",
        "vatStatus" => FrameLabels.VatStatus,
        "inspectionDate" => "Inspection date",
        "inspectionDeadline" => "Inspection deadline",
        "inspectionAddress" => "Inspection address",
        "inspectionMode" => "Inspection mode",
        "storageLocation" => Labels.CaseWorkspace.StorageLocation,
        "claimSourceContactName" => FrameLabels.ContactName,
        "claimSourceContactTelephone" => FrameLabels.ContactPhone,
        "claimSourceContactEmail" => FrameLabels.ContactEmail,
        "reason" => "Reason",

        // The completeness flags are labelled as the form the editor was looking at labelled them.
        "instructionComplete" or "instructionsComplete" => "Instructions complete",
        "imagesComplete" => "Images complete",
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Glass's {Handler} on case {CaseId} was refused before the provider: {Reason}")]
    private static partial void LogGlassCommandRefused(
        ILogger logger,
        Guid caseId,
        string handler,
        string reason);
}

/// <summary>
/// This Engineer's live Glass's session on another Case, and that Case's
/// reference for the Estimate section to name.
/// </summary>
public sealed record GlassSessionElsewhere(GlassRepairEstimateSession Session, string CaseReference);

/// <summary>
/// One field of a refused submission beside the value the case now holds, for comparison only.
/// </summary>
public sealed record ProposedCaseValue(string Label, string Proposed, string? Current);

/// <summary>
/// One editable estimate-line row as posted by the Case Estimate section.
/// Core owns the conversion from its operation word to a persisted line type.
/// </summary>
public sealed record EstimateEditorLine(
    string Operation,
    string? Description,
    string? PartNumber,
    string? Quantity,
    string? LabourHours,
    string? PaintHours,
    string? PartPounds,
    string? Materials,
    Guid? ExistingLineId = null);
