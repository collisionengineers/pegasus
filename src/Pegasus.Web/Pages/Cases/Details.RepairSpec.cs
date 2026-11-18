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

/// <summary>
/// The Repair Spec section's members: the Estimate workbench's state,
/// its fragment and selection, Compare and the snapshots, its own
/// commands (edit line, scaling, restore, duplicate, discard, set current,
/// import, Send to AI) and the Glass's session controls. Moved out of
/// Details.cshtml.cs unchanged so the section has its own file
/// (#1730, R1112).
/// </summary>
public sealed partial class DetailsModel
{
    public IReadOnlyList<LabourRateCard> LabourRateCards { get; private set; } = [];

    public int EstimateImportMaximumBytes => ImportRawEstimate.MaximumDocumentBytes;

    public string EstimateImportExtensions => string.Join(',', estimateParsers
        .SelectMany(parser => parser.FileExtensions)
        .Distinct(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyList<RepairSpecificationVersion> Estimates { get; private set; } = [];

    /// <summary>
    /// The staff names behind each spec version's recorded account, so the
    /// origin line names the person and never an account id (v36 f55,
    /// 9 October 2026).
    /// </summary>
    public IReadOnlyDictionary<string, string> SpecAuthorNames { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Who recorded a spec version, as the page names people: the resolved
    /// account's name; the Automation Actor as AI; an account no longer
    /// resolvable as the shared "another member of staff" wording.
    /// </summary>
    public string SpecAuthorName(string createdBy)
    {
        if (SpecAuthorNames.TryGetValue(createdBy, out var name))
        {
            return name;
        }
        return Guid.TryParse(createdBy, out _) ? "Another member of staff" : "AI";
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveSpecAuthorNamesAsync(
        IReadOnlyList<RepairSpecificationVersion> versions,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var authors = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var createdBy in versions.Select(version => version.CreatedBy).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(createdBy, out var accountId))
            {
                authors[createdBy] = accountId;
            }
        }
        if (authors.Count == 0)
        {
            return names;
        }
        // One read names every distinct author, however many there are.
        var accountNames = await staffAccountQueries.GetNamesAsync(
            [.. authors.Values.Distinct()],
            cancellationToken);
        foreach (var (createdBy, accountId) in authors)
        {
            if (accountNames.TryGetValue(accountId, out var userName) && !string.IsNullOrWhiteSpace(userName))
            {
                names[createdBy] = userName;
            }
        }
        return names;
    }

    public RepairSpecificationVersion? SelectedEstimate { get; private set; }

    public bool EditingNewEstimate { get; private set; }

    public EstimateDetails? EditorDetails { get; private set; }

    public IReadOnlyList<EstimateEditorLine> EditorLines { get; private set; } = [];

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
        && SelectedEstimate is not null;

    public bool SelectedEstimateCanBeCurrent =>
        !AssessmentIsReadOnly
        && AssessmentCanOpen
        && SelectedEstimate is { IsCurrent: false, State: RepairSpecificationState.Draft };

    /// <summary>
    /// The spec the section prices, set once per request: the stored spec as
    /// it is, so hours price at their stored precision as the save and the
    /// report do; a new spec as its header with no lines yet; a redraw as the
    /// spec a save of the posted rows would record (<see cref="RedrawnSpecification"/>).
    /// A Case with no spec has the default header and no lines. The totals, the
    /// scaling range and the column map read it.
    /// </summary>
    public RepairSpecificationVersion EditorSpecification =>
        editorSpecification ??= EstimatePolicy.Provisional(
            Guid.Empty,
            null,
            new EstimateDetails(
                Name: Labels.CaseWorkspace.EngineerSections.Estimate,
                LabourRate: null,
                OtherCosts: null,
                VatPercent: EstimatePolicy.DefaultVatPercent),
            [],
            DateTimeOffset.UtcNow);

    private RepairSpecificationVersion? editorSpecification;

    public EstimateTotals EditorTotals => EstimateTotals.Compute(EditorSpecification);

    /// <summary>
    /// Where the Target % of value slider begins (v38 P6): the lowest share
    /// of the Engineer's Value the default floors allow, or 1 % without one.
    /// </summary>
    public decimal ScaleFloorPercent =>
        EngineerValue is { } value && value > 0m && EditorLines.Any(line => !string.IsNullOrWhiteSpace(line.Description))
            ? RepairSpecificationScaling.FloorPercentOfValue(value, EditorSpecification, ScalingFloors.Default)
            : 1m;

    /// <summary>
    /// The addresses whose postcode suggests the regional uplift (v28 P17),
    /// each named with its outward code: the repairer's, the claimant's and
    /// the storage location's.
    /// </summary>
    public IReadOnlyList<string> RegionalUpliftSuggestions
    {
        get
        {
            var data = Case?.Data ?? estimateData;
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

    public string? ImportCondition =>
        !AssessmentCanOpen
            ? Labels.CaseWorkspace.EngineerSections.NotAvailableForCase
            : null;

    public string? SendToClaudeCondition { get; private set; }

    public string ImportOperationKey { get; private set; } = NewOperationKey();

    public string DuplicateOperationKey { get; private set; } = NewOperationKey();

    public string DiscardOperationKey { get; private set; } = NewOperationKey();

    public string UseEstimateOperationKey { get; private set; } = NewOperationKey();

    public string RestoreOperationKey { get; private set; } = NewOperationKey();

    /// <summary>The selected specification's frozen versions, oldest first (v28 P43).</summary>
    public IReadOnlyList<RepairSpecificationSnapshot> SelectedEstimateSnapshots { get; private set; } = [];

    /// <summary>Every live specification's frozen versions by specification (v38 P2): what Compare's From and To list.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<RepairSpecificationSnapshot>> EstimateSnapshots { get; private set; } =
        new Dictionary<Guid, IReadOnlyList<RepairSpecificationSnapshot>>();

    /// <summary>
    /// Whether the selected specification's latest scaling state is scaled
    /// (v28 P34); an import since the scaling is a new grid and ends it (v38 P3).
    /// </summary>
    public bool SelectedEstimateIsScaled =>
        SelectedEstimateSnapshots
            .Where(snapshot => snapshot.Kind is RepairSpecificationSnapshotKind.Scaled
                or RepairSpecificationSnapshotKind.ScalingRemoved
                or RepairSpecificationSnapshotKind.Imported)
            .MaxBy(snapshot => snapshot.Number)?.Kind == RepairSpecificationSnapshotKind.Scaled;

    /// <summary>
    /// The total the latest scaling started from, the version frozen before
    /// it, for the readout "scaled from estimate" (v38 P6); null unless scaled.
    /// </summary>
    public decimal? ScaledFromGross
    {
        get
        {
            if (!SelectedEstimateIsScaled)
            {
                return null;
            }
            var scaled = SelectedEstimateSnapshots
                .Where(snapshot => snapshot.Kind == RepairSpecificationSnapshotKind.Scaled)
                .MaxBy(snapshot => snapshot.Number);
            return SelectedEstimateSnapshots
                .Where(snapshot => snapshot.Kind == RepairSpecificationSnapshotKind.BeforeScaling
                    && scaled is not null && snapshot.Number < scaled.Number)
                .MaxBy(snapshot => snapshot.Number)?.Gross;
        }
    }

    /// <summary>
    /// One side Compare can read (v38 P2): a live specification's working
    /// draft, or one of its frozen versions, keyed as the query names it.
    /// </summary>
    public sealed record CompareOption(string Key, string Label, RepairSpecificationVersion Specification, RepairSpecificationSnapshot? Version);

    /// <summary>
    /// What Compare's From and To offer: the selected spec's working draft and
    /// its versions newest first, then each other spec and its versions.
    /// </summary>
    public IReadOnlyList<CompareOption> ComparisonOptions { get; private set; } = [];

    /// <summary>Compare is offered with two specs, or one spec that has a frozen version (v38 P2).</summary>
    public bool ComparisonOffered => Estimates.Count >= 2 || SelectedEstimateSnapshots.Count > 0;

    /// <summary>The two sides Compare reads, when the query names both or the dialog opens on its default pair (v28 P19, v38 P2).</summary>
    public CompareOption? ComparisonFrom { get; private set; }

    public CompareOption? ComparisonTo { get; private set; }

    public RepairSpecificationComparison.Diff? Comparison { get; private set; }

    /// <summary>The frozen version the selected spec supplements, and their differences (v28 P20; v38 P3).</summary>
    public RepairSpecificationSnapshot? SupplementaryBase { get; private set; }

    public RepairSpecificationComparison.Diff? SupplementaryDiff { get; private set; }

    /// <summary>The query key for a Compare side: the spec, or the spec and one of its versions.</summary>
    public static string ComparisonKey(Guid specificationId, Guid? snapshotId) =>
        snapshotId is { } version ? $"{specificationId:D}:{version:D}" : specificationId.ToString("D");

    private List<CompareOption> BuildComparisonOptions()
    {
        var options = new List<CompareOption>();
        var culture = CultureInfo.GetCultureInfo("en-GB");
        string Money(decimal value) => "£" + value.ToString("N2", culture);
        void Add(RepairSpecificationVersion specification, bool own)
        {
            var name = own ? Pegasus.Web.Presentation.CaseWorkspaceLabels.Estimate.Working : specification.Details.Name;
            options.Add(new(
                ComparisonKey(specification.SpecificationId, null),
                $"{name} · {Pegasus.Web.Presentation.CaseWorkspaceLabels.Estimate.Now} · {Money(EstimateTotals.Compute(specification).Printed.Gross)}",
                specification,
                null));
            foreach (var version in EstimateSnapshots.GetValueOrDefault(specification.SpecificationId, []).OrderByDescending(item => item.Number))
            {
                var prefix = own ? string.Empty : specification.Details.Name + " · ";
                var sent = version.SentOnReport ? " · " + Pegasus.Web.Presentation.CaseWorkspaceLabels.Estimate.SentOnReport : string.Empty;
                options.Add(new(
                    ComparisonKey(specification.SpecificationId, version.Id),
                    $"{prefix}{Pegasus.Web.Presentation.CaseWorkspaceLabels.Estimate.VersionWord(version.Number)} · {Labels.OfficeTime(version.CreatedAtUtc)} · {Money(version.Gross)}{sent}",
                    version.AsSpecification() with { Details = version.Details },
                    version));
            }
        }
        if (SelectedEstimate is not null)
        {
            Add(SelectedEstimate, own: true);
        }
        foreach (var other in Estimates.Where(item => item.SpecificationId != SelectedEstimate?.SpecificationId))
        {
            Add(other, own: false);
        }
        return options;
    }

    /// <summary>
    /// Compare's sides from the query, or the default pair when the dialog
    /// opens without one: the version the last sent report used against the
    /// selected spec's working draft (v38 P2); with no sent version, nothing
    /// is chosen until the Engineer chooses.
    /// </summary>
    private void ResolveComparison(string? from, string? to, string? dialog)
    {
        if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)
            && string.Equals(dialog, "compare-estimates", StringComparison.Ordinal)
            && SelectedEstimate is not null)
        {
            var sent = ComparisonOptions
                .Where(option => option.Version is { SentOnReport: true })
                .MaxBy(option => option.Version!.CreatedAtUtc);
            from = sent?.Key;
            to = sent is null ? null : ComparisonKey(SelectedEstimate.SpecificationId, null);
        }
        ComparisonFrom = ComparisonOptions.FirstOrDefault(option => string.Equals(option.Key, from, StringComparison.OrdinalIgnoreCase));
        ComparisonTo = ComparisonOptions.FirstOrDefault(option => string.Equals(option.Key, to, StringComparison.OrdinalIgnoreCase));
        Comparison = ComparisonFrom is null || ComparisonTo is null || ComparisonFrom.Key == ComparisonTo.Key
            ? null
            : RepairSpecificationComparison.Compare(ComparisonFrom.Specification, ComparisonTo.Specification);
    }

    public string SendOperationKey { get; private set; } = NewOperationKey();

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
    /// The session that holds this staff member's Glass's login on another
    /// Case: their own, or the Automation's (R1180). The login has one live
    /// slot, so while this is set no launch is offered here; the section says
    /// which Case holds it instead.
    /// </summary>
    public GlassSessionElsewhere? GlassSessionElsewhere { get; private set; }

    /// <summary>
    /// The Case's newest Glass's session the Automation owns, when it has
    /// one. Staff see its line and may close it (R1180); its window and
    /// progress are nobody's to watch.
    /// </summary>
    public GlassRepairEstimateSession? GlassAutomationSession { get; private set; }

    /// <summary>
    /// Whether the Automation's session on this Case holds this staff
    /// member's own Glass's login, so a launch here would be refused.
    /// </summary>
    public bool GlassAccountHeldByAutomation { get; private set; }

    /// <summary>
    /// The session the Estimate section's session line shows: this staff
    /// member's own while it holds the login, else the Automation's on this
    /// Case while it holds a login or has failed, else their own failed one.
    /// </summary>
    public GlassRepairEstimateSession? GlassLineSession =>
        GlassSession is { } own && GlassRepairEstimateSessionPolicy.OccupiesAccount(own.State)
            ? own
            : GlassAutomationSession is { } automation && ShowsGlassLine(automation)
                ? automation
                : GlassSession is { } failed && ShowsGlassLine(failed) ? failed : null;

    /// <summary>Whether a session has a line: while it holds the login, or once it has failed.</summary>
    private static bool ShowsGlassLine(GlassRepairEstimateSession session) =>
        GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State)
        || session.State == GlassRepairEstimateSessionState.Failed;

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
        && !GlassAccountHeldByAutomation
        // Glass's imports into the current work: the Inspection view offers
        // it once a session names the work it was launched from.
        && !IsInspectionView;

    /// <summary>
    /// The repair spec on the screen, which the Glass's slot names: one that
    /// belongs to a Glass's estimate reopens it. The controls fragment has no
    /// spec list of its own and carries the id it was rendered with.
    /// </summary>
    public Guid? GlassSpecificationId => glassSpecificationId ?? SelectedEstimate?.SpecificationId;

    private Guid? glassSpecificationId;

    /// <summary>
    /// Whether the session on the screen failed because its export could not be
    /// read, so its owner may fetch that same estimate again.
    /// </summary>
    public bool CanFetchGlassAgain =>
        CanLaunchGlass
        && GlassSession is { } session
        && GlassRepairEstimateSessionPolicy.CanRefetchExport(session.State, session.FailureCode);

    /// <summary>
    /// Whether the session on the line can be closed: any one that still
    /// holds the account except one mid-import, per the policy, and none while
    /// its provider work is running in the background. The line shows the
    /// staff member's own session or the Automation's, which any member of
    /// staff who can open the assessment may close (R1180).
    /// </summary>
    public bool CanCloseGlass => AssessmentCanOpen
        && !GlassWorkRunning
        && GlassLineSession is { } session
        && GlassRepairEstimateSessionPolicy.CanClose(session.State);

    /// <summary>Whether provider work for the session on the line is queued or running now.</summary>
    public bool GlassWorkRunning { get; private set; }

    /// <summary>
    /// Whether Glass's asks first (operator, 18 November 2026; R1171): the
    /// spec on the screen has lines and belongs to no Glass's estimate, and
    /// this staff member holds no live session of their own on the Case, so
    /// the press would start a new estimate. Import and open rebuilds the
    /// spec in Glass's; Open an empty estimate is the launch as it was.
    /// </summary>
    public bool AsksGlassBuild { get; private set; }

    /// <summary>
    /// The list of what this staff member's build sent to Glass's, shown on
    /// the repair spec it landed as until their next Glass's press on the
    /// Case (R1173); the reopen the build's own work makes in the same window
    /// is part of the build and keeps it. Null anywhere else.
    /// </summary>
    public GlassEstimateBuild? GlassBuildList =>
        glassListedBuild is { BuiltSpecificationId: { } built } build && built == GlassSpecificationId
            ? build
            : null;

    private GlassEstimateBuild? glassListedBuild;

    /// <summary>
    /// What the selected spec's frozen versions decide: Compare's sides, the
    /// scaling readout and the Supplementary difference. Compare's From and To
    /// come from the request's query.
    /// </summary>
    private void ApplyEstimateSnapshots(IReadOnlyList<RepairSpecificationSnapshot> frozen, string? dialog)
    {
        var frozenBySpecification = frozen.ToLookup(item => item.SpecificationId);
        EstimateSnapshots = Estimates.ToDictionary(
            item => item.SpecificationId,
            item => (IReadOnlyList<RepairSpecificationSnapshot>)frozenBySpecification[item.SpecificationId].ToArray());
        if (SelectedEstimate is not null)
        {
            SelectedEstimateSnapshots = EstimateSnapshots.GetValueOrDefault(SelectedEstimate.SpecificationId, []);
            if (SelectedEstimate.Supplementary is { } supplementary)
            {
                SupplementaryBase = SelectedEstimateSnapshots.FirstOrDefault(item => item.Id == supplementary.OfSnapshotId);
                SupplementaryDiff = SupplementaryBase is null
                    ? null
                    : RepairSpecificationComparison.Compare(SupplementaryBase, SelectedEstimate);
            }
        }
        ComparisonOptions = BuildComparisonOptions();
        ResolveComparison(Request.Query["from"], Request.Query["to"], dialog);
    }

    /// <summary>The dialog of the Estimate section the request asks to draw open, when it may open.</summary>
    private string? EstimateDialogOf(string? dialog) => dialog switch
    {
        "glass-build" when AsksGlassBuild => "glass-build",
        "send-to-claude" when SendToClaudeCondition is null => "send-to-claude",
        "compare-estimates" when ComparisonOffered => "compare-estimates",
        "versions" when SelectedEstimate is not null => "versions",
        "delete-estimate" when SelectedEstimateIsEditable
            && SelectedEstimate is { IsCurrent: false } => "delete-estimate",
        _ => null
    };

    /// <summary>
    /// The Repair Spec section's fragment: what the section and its dialogs
    /// draw and nothing else. A spec tab, New repair spec and Compare's From
    /// and To redraw only this part (case-workspace.js showEstimate), so the
    /// reads the rest of the page needs (the direct sections, the report's
    /// inputs, generations and delivery, the extras) are not repeated.
    /// </summary>
    private async Task LoadEstimateSectionAsync(
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
        using var reads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var estimates = reads.Start(token => listEstimates.ExecuteAsync(id, work, token));
        var cards = reads.Start(token => labourRateCards.ListAsync(actor, token));
        var sendingToAi = AssessmentCanOpen && !AssessmentIsReadOnly
            ? reads.Start(token => sendToAiControl.IsEnabledAsync(token))
            : null;
        var glass = reads.Start(token => ReadGlassSessionAsync(id, actor, token));
        await reads.WhenAllAsync();

        Estimates = await estimates;
        LabourRateCards = await cards;
        ApplyEstimateSelection(estimate);
        EvaluateEngineerSectionConditions(sendingToAi is not null && await sendingToAi);
        ApplyGlassSession(await glass);

        // Both of these need only the estimates, so they run together.
        var specificationIds = Estimates.Select(item => item.SpecificationId).ToArray();
        var specAuthors = Estimates;
        using var dependentReads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var authorNames = dependentReads.Start(token => ResolveSpecAuthorNamesAsync(specAuthors, token));
        var snapshotRead = specificationIds.Length > 0
            ? dependentReads.Start(token => specificationSnapshots.ListForSpecificationsAsync(id, specificationIds, token))
            : null;
        var buildOffer = dependentReads.Start(token => ReadGlassBuildOfferAsync(token));
        await dependentReads.WhenAllAsync();
        SpecAuthorNames = await authorNames;
        AsksGlassBuild = await buildOffer;
        ApplyEstimateSnapshots(snapshotRead is null ? [] : await snapshotRead, dialog);
        OpenDialog = EstimateDialogOf(dialog);
    }

    /// <summary>What the Estimate section's Glass's surface shows.</summary>
    private sealed record GlassSessionReads(
        bool AccountEnabled,
        GlassRepairEstimateSession? Session,
        GlassSessionElsewhere? Elsewhere,
        GlassEstimateBuild? ListedBuild = null,
        GlassRepairEstimateSession? Automation = null,
        bool HeldByAutomation = false);

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

    /// <summary>
    /// Whether the press asks first. The spec's link is not on the shared
    /// read model (ADR-0063), so it costs one read, made only where the press
    /// would start a new estimate on a spec that has lines.
    /// </summary>
    private Task<bool> ReadGlassBuildOfferAsync(CancellationToken cancellationToken) =>
        CanLaunchGlass
        && !EditingNewEstimate
        && (GlassSession is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(GlassSession.State))
        && GlassSpecificationId is { } specificationId
        && SelectedEstimate is not { Lines.Count: 0 }
            ? glassSessions.OffersBuildAsync(specificationId, cancellationToken)
            : Task.FromResult(false);

    private void ApplyGlassSession(GlassSessionReads reads)
    {
        GlassAccountEnabled = reads.AccountEnabled;
        GlassSession = reads.Session;
        glassListedBuild = reads.ListedBuild;
        GlassSessionElsewhere = reads.Elsewhere;
        GlassAutomationSession = reads.Automation;
        GlassAccountHeldByAutomation = reads.HeldByAutomation;
        GlassWorkRunning = GlassLineSession is { } line
            && HttpContext.RequestServices.GetRequiredService<Pegasus.Web.Background.ProviderWorkQueue>()
                .IsInFlight(line.Id);
    }

    /// <summary>
    /// Only the enabled account's reference is asked here; the credential's
    /// secret is read by the launch alone. Three statements, as before the
    /// Automation ran sessions: the reference, the Case's sessions (this staff
    /// member's two newest and the Automation's newest, in one), and, when
    /// neither holds the login here, who holds it.
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

        // A host that does not reach Glass's offers no control at all, whatever
        // account the staff member holds.
        if (!glassAvailability.Enabled)
        {
            return new(false, null, null);
        }

        if (await externalCredentials.GetEnabledReferenceAsync(
                actor, ExternalCredentialProvider.GlassRepairEstimate, cancellationToken) is not { } account)
        {
            return new(false, null, null);
        }

        // One query reads the session, for the build list the one before it,
        // and the Case's newest Automation session.
        var reads = await glassSessions.GetForCaseWithBuildAsync(id, staffId, cancellationToken);
        var session = reads?.Session;
        var automation = reads?.Automation;
        var heldByAutomation = automation is not null
            && GlassRepairEstimateSessionPolicy.OccupiesAccount(automation.State)
            && string.Equals(
                automation.NormalizedExternalAccountKey, account.NormalizedExternalAccountKey, StringComparison.Ordinal);
        GlassSessionElsewhere? elsewhere = null;
        if ((session is null || !GlassRepairEstimateSessionPolicy.OccupiesAccount(session.State)) && !heldByAutomation)
        {
            // The login's one live slot may be held from another Case, by
            // this staff member or by the Automation; this Case then says
            // where, and offers no launch. A colleague holding a shared login
            // is not named here.
            var live = await glassSessions.FindLiveForAccountAsync(
                account.NormalizedExternalAccountKey, cancellationToken);
            if (live is not null && live.CaseId != id
                && (live.OwnerKind == ActorKind.Automation || live.PegasusUserId == staffId))
            {
                var references = await listCaseReferences.ExecuteAsync(
                    new(actor, [live.CaseId]), cancellationToken);
                elsewhere = new(
                    live,
                    references.GetValueOrDefault(live.CaseId, live.CaseId.ToString("D")));
            }
        }

        return new(true, session, elsewhere, reads?.Build, automation, heldByAutomation);
    }

    private void ApplyEstimateSelection(string? estimate)
    {
        if (string.Equals(estimate, "new", StringComparison.OrdinalIgnoreCase))
        {
            EditingNewEstimate = true;
            // A new spec starts on the Standard labour-rate card (FRD-25).
            var card = LabourRateCardAdministration.ForNewSpecification(LabourRateCards);
            EditorDetails = new EstimateDetails(
                Name: Labels.CaseWorkspace.EngineerSections.NewEstimate,
                LabourRate: card?.HourlyRate,
                OtherCosts: null,
                VatPercent: EstimatePolicy.DefaultVatPercent,
                Rate: card is null ? null : new EstimateRateSnapshot(card.Id, card.Version, card.HourlyRate));
            EditorLines = [new EstimateEditorLine("", null, null, null, null, null, null, null)];
            editorSpecification = EstimatePolicy.Provisional(Guid.Empty, null, EditorDetails, [], DateTimeOffset.UtcNow);
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
        // The page prices the stored spec itself: its stored positions run
        // from one without gaps, so a mark keeps its row.
        editorSpecification = SelectedEstimate;
        EditorLines = SelectedEstimate.Lines
            .OrderBy(line => line.Position)
            .Select(line => new EstimateEditorLine(
                EstimateOperations.FromLineType(line.Type).ToString(),
                line.Description,
                line.PartNumber,
                line.Quantity?.ToString(CultureInfo.InvariantCulture),
                // Two decimals in every figure box (v36 item X, 9 October 2026): a
                // stored 0.700000 no longer clips the box.
                line.WorkUnits is { } hours ? EstimatePolicy.EditorHoursText(hours) : null,
                line.PaintWorkUnits is { } paintHours ? EstimatePolicy.EditorHoursText(paintHours) : null,
                line.Price?.ToString("0.00", CultureInfo.InvariantCulture),
                line.Materials?.ToString("0.00", CultureInfo.InvariantCulture),
                line.Id))
            .ToList();
    }

    /// <summary>
    /// The Repair Spec section for a tab, New repair spec or Compare switch,
    /// drawn from the same view as the full page. Like every mounted body this
    /// GET reads and writes no TempData; the browser repeats the lease it
    /// holds in a header, and once Core validates it the section is drawn as
    /// the full page draws it for the lease's holder.
    /// </summary>
    private async Task<IActionResult> GetEstimateFragmentAsync(
        Guid id,
        ActionActor actor,
        string? estimate,
        string? dialog,
        string? renderLeaseToken,
        CancellationToken cancellationToken)
    {
        var work = WorkSelector;
        // The section draws the workflow, lease, works and accepted data, so
        // the frame is the narrow one (no documents, notes, Triage, tasks or
        // query route), and the lease check needs only the id, actor and
        // header, so all three reads start together.
        var checksLease = !string.IsNullOrWhiteSpace(renderLeaseToken) && validateCaseRenderLease is not null;
        using var firstReads = new Pegasus.Web.Presentation.BoundedReads(cancellationToken);
        var frameRead = firstReads.Start(async token =>
        {
            using (DocumentReadTelemetry.Start("web.case.frame"))
            {
                return await getCaseSectionFrame.ExecuteAsync(new(id, actor, work), token);
            }
        });
        var workspaceRead = firstReads.Start(async token =>
        {
            using (DocumentReadTelemetry.Start("web.case.workspace"))
            {
                return await getAssessmentWorkspace.ExecuteAsync(new(id, actor, work), token);
            }
        });
        var leaseRead = checksLease
            ? firstReads.Start(token => validateCaseRenderLease!.ExecuteAsync(new(id, actor, renderLeaseToken!), token))
            : null;
        await firstReads.WhenAllAsync();
        estimateFrame = await frameRead;
        if (estimateFrame is null)
        {
            return FragmentNotFound();
        }
        var workspace = await workspaceRead;
        estimateData = workspace?.Data;
        ApplyAssessmentAccess(actor, estimateFrame.Workflow);
        if (leaseRead is not null && await leaseRead)
        {
            AdoptRenderLease(renderLeaseToken!);
        }
        await LoadEstimateSectionAsync(id, actor, workspace, work, estimate, dialog, cancellationToken);
        return Partial(EstimateView, this);
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

        // An empty direction takes Core's default instruction for the Case.
        var instruction = direction?.Trim() ?? string.Empty;
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
            var engineerValue = EngineerValueOf(workspace?.Assessment);
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
                // The spec as edited sets the slider's top (operator, 8 October 2026).
                ceiling = Math.Max(1m, RepairSpecificationScaling.PercentOfValue(engineerValue!.Value, result.GrossBefore))
                    .ToString("0.#", CultureInfo.InvariantCulture),
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
        Guid id, Guid? estimate, [FromHeader(Name = "X-Pegasus-Edit-Lease")] string? renderLeaseToken,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        glassSpecificationId = estimate;
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
        AsksGlassBuild = await ReadGlassBuildOfferAsync(cancellationToken);
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
    ///
    /// Glass's is the one button (operator, 6 October 2026). While this staff
    /// member's own session on the Case still holds the account it is
    /// continued, exactly as <see cref="OnPostResumeGlassAsync"/> continues
    /// one; otherwise the gateway reopens the estimate the repair spec on the
    /// screen belongs to, or starts a new one. On a spec made outside Pegasus
    /// the page asks first (R1171), and Import and open posts here with
    /// <paramref name="build"/>, which rebuilds that spec in Glass's.
    /// </remarks>
    public async Task<IActionResult> OnPostLaunchGlassAsync(
        Guid id,
        long expectedCaseVersion,
        string operationKey,
        string? editLeaseToken,
        Guid? estimate,
        bool build,
        [FromServices] IGlassRepairEstimateGateway glassEstimates,
        [FromServices] Pegasus.Web.Background.ProviderWorkQueue glassWork,
        CancellationToken cancellationToken)
    {
        if (!glassAvailability.Enabled)
        {
            return RefuseUnavailableGlass(id, "LaunchGlass");
        }
        var guard = await GuardEstimateEditAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            // Answered in the Glass's window: a refusal goes back to the Case
            // window that posted it, as every other outcome here does.
            return RefusedGlassGuard(id, "LaunchGlass", guard);
        }
        if (!TryGetActor(out var actor) || !Guid.TryParse(actor.SubjectId, out var staffId))
        {
            return Forbid();
        }

        // The launch that made the session, posted again, is a double-click:
        // it replays below rather than continuing the session at Glass's.
        var own = await glassSessions.GetForCaseAsync(id, staffId, cancellationToken);
        if (own is not null
            && GlassRepairEstimateSessionPolicy.OccupiesAccount(own.State)
            && !string.Equals(own.OperationKey, operationKey.Trim(), StringComparison.Ordinal))
        {
            return await ResumeGlassAsync(
                id, actor, own.Id, own.Version, expectedCaseVersion, editLeaseToken,
                glassEstimates, glassWork, cancellationToken);
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
                    newSessionId)
                {
                    SpecificationId = estimate,
                    Build = build,
                },
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
        if (!glassAvailability.Enabled)
        {
            return RefuseUnavailableGlass(id, "ResumeGlass");
        }
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
        return await ResumeGlassAsync(
            id, actor, sessionId, expectedSessionVersion, expectedCaseVersion, editLeaseToken,
            glassEstimates, glassWork, cancellationToken);
    }

    /// <summary>
    /// Continues this staff member's session on the Case, for the Glass's
    /// button and for Fetch again alike.
    /// </summary>
    private async Task<IActionResult> ResumeGlassAsync(
        Guid id,
        ActionActor actor,
        Guid sessionId,
        long expectedSessionVersion,
        long expectedCaseVersion,
        string? editLeaseToken,
        IGlassRepairEstimateGateway glassEstimates,
        Pegasus.Web.Background.ProviderWorkQueue glassWork,
        CancellationToken cancellationToken)
    {
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
        if (!glassAvailability.Enabled)
        {
            LogGlassCommandRefused(logger, id, "CloseGlass", GlassLabels.Unavailable);
            TempData["CaseError"] = GlassLabels.Unavailable;
            return RedirectToEstimate(id);
        }
        var access = await getAssessmentAccess.ExecuteAsync(new(id, actor), cancellationToken);
        if (access?.CanOpen != true)
        {
            return NotFound();
        }
        // The session posted is this staff member's own newest on the Case, or
        // the Case's newest the Automation owns, which any member of staff who
        // can open the assessment may close (R1180).
        var ownSession = await glassSessions.GetForCaseAsync(id, staffId, cancellationToken);
        if (ownSession?.Id != sessionId
            && (await glassSessions.GetAutomationForCaseAsync(id, cancellationToken))?.Id != sessionId)
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
    /// <summary>
    /// A Glass's command on a host that does not reach the provider. Refused
    /// before the Estimate guard, so no lease is touched for a command that
    /// could never run; answered in the Glass's window like every refusal.
    /// </summary>
    private PartialViewResult RefuseUnavailableGlass(Guid id, string handler)
    {
        LogGlassCommandRefused(logger, id, handler, GlassLabels.Unavailable);
        TempData["CaseError"] = GlassLabels.Unavailable;
        return GlassReturn(id);
    }

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
        // The access answer is the header's own workflow state, so the guard
        // reads the Case once.
        var header = await getCaseHeader.ExecuteAsync(new(id, actor), cancellationToken);
        if (header is null || !AssessmentAccessPolicy.For(actor, header.Workflow).CanOpen)
        {
            return NotFound();
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
        editorSpecification = RedrawnSpecification(id, EditorDetails);
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
            saved ?? EstimateVatPolicy.For(RepairerVatStatuses.Default),
            VatStatus,
            VatCategories);
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

    /// <summary>A posted figure: null when blank, <see cref="decimal.MinusOne"/> when it does not read.</summary>
    private static decimal? PostedFigure(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : decimal.MinusOne;

    /// <summary>
    /// The line one editor row posts, for the save's reader and for a redraw.
    /// A figure that does not read, or an operation the editor does not offer,
    /// is absent and leaves <paramref name="reads"/> false: the save refuses
    /// the post, the redraw still draws the row.
    /// </summary>
    private static EstimateLineInput ToLineInput(EstimateEditorLine row, out bool reads)
    {
        static decimal? Present(decimal? figure) => figure is < 0 ? null : figure;
        var typed = EstimateOperations.TryParse(row.Operation, out var operation);
        var workUnits = PostedFigure(row.LabourHours);
        var paintWorkUnits = PostedFigure(row.PaintHours);
        var price = PostedFigure(row.PartPounds);
        var materials = PostedFigure(row.Materials);
        int? quantity = null;
        var quantityReads = true;
        if (!string.IsNullOrWhiteSpace(row.Quantity))
        {
            quantityReads = int.TryParse(
                row.Quantity.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var posted) && posted >= 0;
            quantity = quantityReads ? posted : null;
        }
        reads = typed && quantityReads
            && workUnits is not < 0 && paintWorkUnits is not < 0 && price is not < 0 && materials is not < 0;
        return new(
            EstimateOperations.ToLineType(typed ? operation : EstimateOperation.Specialist),
            null,
            string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
            Present(workUnits),
            Present(price),
            false,
            string.IsNullOrWhiteSpace(row.PartNumber) ? null : row.PartNumber.Trim(),
            null,
            null,
            null,
            Present(paintWorkUnits),
            quantity,
            Present(materials));
    }

    /// <summary>
    /// The spec a redraw prices: the rows as a save would record them, every
    /// row a line (blank ones too, so a mark keeps its row), carried from the
    /// stored spec by <see cref="EstimatePolicy.Edited"/>. A row the save would
    /// refuse is still drawn, with no refusal: it is priced as posted.
    /// </summary>
    private RepairSpecificationVersion RedrawnSpecification(Guid caseId, EstimateDetails details)
    {
        var lines = EditorLines.Select(row => ToLineInput(row, out _)).ToArray();
        try
        {
            if (TryGetActor(out var actor))
            {
                return EstimatePolicy.Edited(
                    caseId,
                    actor,
                    SelectedEstimate,
                    details,
                    lines,
                    [.. EditorLines.Select(row => row.ExistingLineId)],
                    DateTimeOffset.UtcNow);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or KeyNotFoundException or StaffAuthorizationException)
        {
            // The save would refuse these rows; the redraw draws them as posted.
        }

        return EstimatePolicy.Provisional(caseId, SelectedEstimate, details, lines, DateTimeOffset.UtcNow);
    }

    private EstimateEditorPost ReadEditorPost()
    {
        var form = Request.Form;
        // A discount is typed as a percentage and held as a fraction. A blank
        // box is no discount; anything unreadable stays negative through the
        // conversion, so EstimatePolicy.ValidateDiscounts' [0,1] rule refuses
        // it rather than the screen guessing a value.
        static decimal Fraction(string? value) => (PostedFigure(value) ?? 0m) / 100m;
        // A checked box posts "true" ahead of its hidden "false"; an
        // unchecked box posts the "false" alone, so the first entry is the answer.
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

            var line = ToLineInput(rows[^1], out var reads);
            if (!reads)
            {
                linesAreValid = false;
                continue;
            }

            lines.Add(line);
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
            PostedFigure(form["estimateLabourRate"].ToString()),
            Checked("estimateRegionalUplift"),
            PostedFigure(form["estimateOtherCosts"].ToString()),
            PostedFigure(form["estimateVatPercent"].ToString()),
            // Enum.TryParse accepts any number; a missing or undefined post is
            // the default, and Core refuses a defined status that is no choice.
            Enum.TryParse<RepairerVatStatus>(form["estimateVatStatus"].ToString(), out var status)
                && Enum.IsDefined(status)
                ? status
                : RepairerVatStatuses.Default,
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

    /// <summary>
    /// Retain once, then use the same source-backed import as MCP and Glass's.
    /// The import lands on the spec the section shows (v38 P3, operator,
    /// 12 October 2026); with none, it creates one.
    /// </summary>
    public async Task<IActionResult> OnPostImportEstimateAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        IFormFile? estimateFile,
        Guid? estimateId,
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
        var (activeLeaseToken, importCheck, claimResult) = await ClaimEstimateImportAsync(
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
                        DocumentSemanticRole.ExternalEstimate, DocumentSource.StaffUpload, sourceIdentity,
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
                var lease = await leases.ClaimAsync(
                    new(id, importVersion, actor!, NewOperationKey()), cancellationToken);
                importLeaseToken = lease.Token;
                StoreLeaseAuthority(id, lease.Token);
            }

            if (source.Version.CustodyStatus != DocumentCustodyStatus.Confirmed)
            {
                TempData["CaseError"] = "The estimate source could not be confirmed in Case Files, so nothing was imported.";
                return RedirectToEstimate(id);
            }

            var live = await listEstimates.ExecuteAsync(id, WorkSelector, cancellationToken);
            var target = estimateId is { } selected && selected != Guid.Empty
                ? live.FirstOrDefault(estimate => estimate.SpecificationId == selected)
                : null;
            var importedBefore = (target is null ? live : [target])
                .Any(estimate => string.Equals(
                    estimate.Source.Sha256, source.Version.Sha256, StringComparison.OrdinalIgnoreCase));
            var resultingVersion = checked(importVersion + (importedBefore ? 0 : 1));
            return await ImportRetainedEstimateAsync(new(actor!, id, importVersion, importLeaseToken,
                source.Occurrence.Id, source.Version.Id, source.Version.Sha256, operationKey, string.Empty)
                { Work = WorkSelector, EstimateId = target?.SpecificationId, Checked = importCheck },
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

        var (leaseToken, importCheck, claimResult) = await ClaimEstimateImportAsync(
            actor!, id, expectedVersion, operationKey, editLeaseToken, source.Version.Sha256,
            editor: null, cancellationToken);
        if (claimResult is not null)
        {
            return claimResult;
        }
        try
        {
            // From Files there is no spec in view: the import lands on the
            // spec in use (v38 P3), or creates one when there is none.
            var current = (await listEstimates.ExecuteAsync(id, WorkSelector, cancellationToken))
                .FirstOrDefault(estimate => estimate.IsCurrent);
            return await ImportRetainedEstimateAsync(
                new(actor!, id, expectedVersion, leaseToken!, source.Occurrence.Id, source.Version.Id,
                    source.Version.Sha256, operationKey, string.Empty)
                { Work = WorkSelector, EstimateId = current?.SpecificationId, Checked = importCheck },
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
        var header = await getCaseHeader.ExecuteAsync(new(id, actor), cancellationToken);
        if (header is null) return (null, [], NotFound());
        if (!AssessmentAccessPolicy.For(actor, header.Workflow).CanOpen) return (null, [], NotFound());
        if (header.Workflow.Archive is not null
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
    /// Takes the edit lease when the import starts from read mode, makes
    /// Core's one authority-and-replay check and answers a replay of the same
    /// operation and source. A result means the import ends here; otherwise the
    /// lease token is the one to import under and the check is handed on with
    /// the request. A refusal shows Core's sentence.
    /// <paramref name="editor"/> names the client form whose commit a replay
    /// records, when there is one.
    /// </summary>
    private async Task<(string? LeaseToken, EstimateImportCheck? Check, IActionResult? Result)> ClaimEstimateImportAsync(
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
                var lease = await leases.ClaimAsync(
                    new(id, expectedVersion, actor, NewOperationKey()), cancellationToken);
                activeLeaseToken = lease.Token;
                StoreLeaseAuthority(id, lease.Token);
            }
            catch (StaffAuthorizationException) { return (null, null, Forbid()); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                HandleLeaseFailure(id, null, exception);
                TempData["CaseError"] = MutationRefusalMessage(exception, "The Case cannot be edited right now.");
                return (null, null, RedirectToEstimate(id));
            }
        }

        const string UnconfirmedLine = "The source was retained, but the import could not be confirmed. Retry the same file.";
        EstimateImportCheck check;
        try
        {
            check = await importRawEstimate.CheckAsync(
                new(actor, id, expectedVersion, activeLeaseToken!, Guid.Empty, Guid.Empty,
                    sha256, operationKey, string.Empty), cancellationToken);
        }
        catch (StaffAuthorizationException) { return (null, null, Forbid()); }
        catch (EstimateImportUnconfirmedException unconfirmed)
        {
            // A fault in the replay probe, after the authority was proven: the
            // page's line for a fault it cannot name, Core's words for a refusal.
            TempData["CaseError"] = MutationRefusalMessage(unconfirmed.InnerException!, UnconfirmedLine);
            return (null, null, RedirectToEstimate(id));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            HandleLeaseFailure(id, activeLeaseToken, exception);
            TempData["CaseError"] = exception is InvalidOperationException or KeyNotFoundException or ArgumentException
                ? exception.Message
                : "The Case cannot be edited right now.";
            return (null, null, RedirectToEstimate(id));
        }

        if (check.Replay is not { } replay)
        {
            return (activeLeaseToken, check, null);
        }
        try
        {
            if (editor is not null)
            {
                RecordEditorCommit(editor, operationKey, expectedVersion, expectedVersion);
            }
            TempData["CaseStatus"] = await ImportedMessageAsync(id, replay.EstimateId, cancellationToken);
            return (null, null, RedirectToEstimate(id, replay.EstimateId.ToString("D")));
        }
        catch (StaffAuthorizationException) { return (null, null, Forbid()); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TempData["CaseError"] = MutationRefusalMessage(exception, UnconfirmedLine);
            return (null, null, RedirectToEstimate(id));
        }
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

    private RedirectToPageResult RedirectToEstimate(
        Guid id,
        string? estimate = null,
        string? dialog = null) =>
        RedirectToPage(
            "/Cases/Details",
            new { id, section = "estimate", estimate, dialog, view = ReturnView });

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
