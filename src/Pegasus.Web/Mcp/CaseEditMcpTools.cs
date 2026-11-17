using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record CaseUpdateDetailsToolResult(
    Guid CaseId,
    long CaseVersion,
    string State,
    string OperationKey);

internal sealed record CaseNoteAddToolResult(
    Guid CaseId,
    string OperationKey);

internal sealed record AssessmentFieldVocabularyItem(
    string Path,
    string Type,
    int MaximumLength,
    bool MustBePositive,
    IReadOnlyList<string>? Codes,
    string? Format,
    string? Label,
    bool Writable,
    string? WhyNotWritable);

internal sealed record CodeMeaningItem(string Code, string Meaning);

internal sealed record ImageTagVocabularyItem(
    Guid TagId,
    string Name,
    string Colour,
    bool IsBuiltIn);

internal sealed record StaffVocabularyItem(Guid StaffId, string Username);

internal sealed record SignOffEngineerVocabularyItem(
    Guid StaffId,
    string PrintedName,
    string? Qualifications,
    bool IsDefault);

internal sealed record VocabularyToolResult(
    IReadOnlyList<AssessmentFieldVocabularyItem> AssessmentFields,
    IReadOnlyList<CodeMeaningItem> EstimateLineTypes,
    IReadOnlyList<CodeMeaningItem> EstimateEvidenceLabels,
    IReadOnlyList<string> RepairerVatStatuses,
    IReadOnlyList<string> EstimateVatCategories,
    IReadOnlyList<string> ValuationSources,
    IReadOnlyList<ImageTagVocabularyItem> ImageTags,
    [property: Description("The enabled staff, by username: the identifiers pegasus_case_action assign_engineer, pegasus_triage_assign and pegasus_case_query assign accept.")] IReadOnlyList<StaffVocabularyItem> Staff,
    [property: Description("The eligible Sign-off Engineers, by printed name: the identifiers pegasus_case_update_details signOffEngineerId accepts.")] IReadOnlyList<SignOffEngineerVocabularyItem> SignOffEngineers,
    [property: Description("The unroadworthy wordings bank, oldest first: the one list offered on every Case for assessment.unroadworthy_reason, which also takes free text.")] IReadOnlyList<string> UnroadworthyReasons,
    string CorrelationId);

internal sealed record DirectoryToolItem(
    Guid OrganizationId,
    long Version,
    string Name,
    string? Email,
    string? Telephone,
    string? Address,
    string? Postcode,
    [property: Description("The codes of the Principals this organisation is linked to; a Case of one of them lists it first.")] IReadOnlyList<string> LinkedPrincipalCodes);

internal sealed record DirectorySearchToolResult(
    string Role,
    IReadOnlyList<DirectoryToolItem> Items,
    string CorrelationId);

/// <summary>
/// Automation Actor Case edits (FRD-10, ADR-0064): the Case facts and notes a
/// staff member records on the Case, written through the same Core commands
/// as the staff Case page; the valuation cards are ValuationMcpTools'. The Automation Actor does
/// anything a staff member can on the Case (operator, 7 October 2026). A
/// Case-detail edit is the staff Case save: each section it touches is
/// submitted whole, built from the Case's current values with the caller's
/// changes over them, so a value the caller does not name is never cleared.
/// </summary>
[McpServerToolType]
internal sealed class CaseEditMcpTools(
    IGetCaseEditBasis getCaseEditBasis,
    IGetCaseAssessment getAssessment,
    ISaveCaseWorkspace saveCaseWorkspace,
    IAddCaseNote addCaseNote,
    IContactDirectoryQueries contactDirectory,
    IReadImageTagVocabulary imageTags,
    IUnroadworthyReasonBankStore unroadworthyReasonBank,
    Pegasus.Core.Identity.ICaseEngineerChoices staffChoices,
    Pegasus.Core.Identity.IStaffAccountQueries staffAccounts,
    AutomationMcpAuditor auditor,
    OneCommandEdit leases)
{
    [McpServerTool(
        Name = "pegasus_case_update_details",
        Title = "Update case details",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Edits the Case facts staff edit on the Case page: claimant, claim, accident, VAT status, repairer (typed, or linked to a directory Repairer), claim source (a directory Claim source) and its contact for this Case, Principal and Claim source notes, due by, vehicle identity and mileage, inspection and the Sign-off Engineer. Omit a value to leave it as it is; send an empty string to clear a text or date value. Values not named are never changed. Needs the expected case version. Dates are yyyy-MM-dd. Every Case's inspection type is Image Based Assessment; inspectionAddress is Inspect at, a place or the words 'Image Based Assessment'. Assessment fields (outcome, roadworthiness, values, damage and the rest) are written with pegasus_assessment_update.")]
    public async Task<CaseUpdateDetailsToolResult> UpdateDetailsAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description(AutomationToolText.ExpectedCaseVersion)] long expectedVersion,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("Why these details are being changed (case history reason).")] string? reason = null,
        [Description("Claimant name.")] string? claimantName = null,
        [Description("Claimant contact number.")] string? claimantContactNumber = null,
        [Description("Claimant address.")] string? claimantAddress = null,
        [Description("Claim number.")] string? claimNumber = null,
        [Description("Incident date, yyyy-MM-dd.")] string? incidentDate = null,
        [Description("Accident circumstances.")] string? accidentCircumstances = null,
        [Description("VAT status text.")] string? vatStatus = null,
        [Description("Repairer name.")] string? repairerName = null,
        [Description("Repairer address.")] string? repairerAddress = null,
        [Description("Links the Case's repairer to an active directory Repairer (pegasus_directory_search, role Repairer): its name, address and version are copied onto the Case, so a later directory edit never rewrites it. Naming the Repairer the Case is already linked to keeps that link and its copy as they are. The empty identifier 00000000-0000-0000-0000-000000000000 removes the link and keeps the typed name.")] Guid? repairerDirectoryId = null,
        [Description("Records the Case's claim source as an active directory Claim source (pegasus_directory_search, role ClaimSource), copied onto the Case with its contact. The empty identifier 00000000-0000-0000-0000-000000000000 records none.")] Guid? claimSourceId = null,
        [Description("The Case's own notes beside its Principal.")] string? principalNotes = null,
        [Description("The Case's own notes beside its Claim source.")] string? claimSourceNotes = null,
        [Description("Due by date, yyyy-MM-dd.")] string? dueBy = null,
        [Description("Claim source contact name for this Case.")] string? claimSourceContactName = null,
        [Description("Claim source contact telephone for this Case.")] string? claimSourceContactTelephone = null,
        [Description("Claim source contact email for this Case.")] string? claimSourceContactEmail = null,
        [Description("Vehicle registration.")] string? vehicleRegistration = null,
        [Description("Vehicle make.")] string? vehicleMake = null,
        [Description("Vehicle model.")] string? vehicleModel = null,
        [Description("Vehicle year.")] string? vehicleYear = null,
        [Description("Vehicle mileage (whole number).")] long? vehicleMileage = null,
        [Description("Vehicle mileage unit: miles or km.")] string? vehicleMileageUnit = null,
        [Description("Inspection address (Inspect at): a place, or the words 'Image Based Assessment'.")] string? inspectionAddress = null,
        [Description("Storage location for the vehicle.")] string? storageLocation = null,
        [Description("Inspection date, yyyy-MM-dd; the report prints it as the date the damage was assessed.")] string? inspectionDate = null,
        [Description("Inspection deadline, yyyy-MM-dd.")] string? inspectionDeadline = null,
        [Description("The Sign-off Engineer's staff identifier, from pegasus_vocabulary_get signOffEngineers. Naming the engineer the Case already shows records nothing. The empty identifier 00000000-0000-0000-0000-000000000000 removes the recorded pick, so the default applies again.")] Guid? signOffEngineerId = null,
        [Description(AutomationToolText.EditLeaseToken)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_case_update_details",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var saved = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    async token =>
                    {
                        var current = await getCaseEditBasis.ExecuteAsync(
                            new(caseId, context.Actor), cancellationToken)
                            ?? throw new McpException("The case was not found.");
                        // Only the inspection, vehicle and report parts read the
                        // recorded assessment; an overview-only call never loads it.
                        CaseAssessmentProjection? assessment = null;
                        async Task LoadAssessmentAsync() =>
                            assessment ??= await getAssessment.ExecuteAsync(caseId, cancellationToken);
                        string? Recorded(string path) => assessment?.Field(path)?.Value;
                        decimal? Money(string path) => decimal.TryParse(
                            Recorded(path), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                            ? value
                            : null;
                        var data = current.Data;
                        var persisted = data.Workspace;

                        CaseWorkspaceOverview? overview = null;
                        if (Any(claimantName, claimantContactNumber, claimantAddress, claimNumber,
                                incidentDate, accidentCircumstances, vatStatus, repairerName, repairerAddress,
                                principalNotes, claimSourceNotes, dueBy, claimSourceContactName,
                                claimSourceContactTelephone, claimSourceContactEmail)
                            || repairerDirectoryId is not null
                            || claimSourceId is not null)
                        {
                            // The claim source is a choice from the active Claim
                            // source records, copied onto the Case as its
                            // snapshot, as the Case page's save makes it: the
                            // same record keeps the snapshot it already has.
                            var claimSource = persisted?.ClaimSource;
                            if (claimSourceId is { } sourceId)
                            {
                                if (sourceId == Guid.Empty)
                                {
                                    claimSource = null;
                                }
                                else if (CaseDetailsSaveRules.ClaimSourceToCopy(claimSource, sourceId) is { } copied)
                                {
                                    var chosen = await contactDirectory.FindActiveByRoleAsync(
                                            context.Actor, ContactRole.ClaimSource, copied, cancellationToken)
                                        ?? throw new McpException("The selected claim source is not an active Claim source record.");
                                    claimSource = CaseDetailsSaveRules.ClaimSourceCopy(chosen);
                                }
                            }
                            claimSource = claimSource is { } source
                                ? source with
                                {
                                    OverrideContactName = claimSourceContactName ?? source.OverrideContactName,
                                    OverrideContactTelephone = claimSourceContactTelephone ?? source.OverrideContactTelephone,
                                    OverrideContactEmailAddress = claimSourceContactEmail ?? source.OverrideContactEmailAddress,
                                }
                                : null;
                            if (claimSource is null
                                && Any(claimSourceContactName, claimSourceContactTelephone, claimSourceContactEmail))
                            {
                                throw new McpException("The Case has no claim source to set a contact for.");
                            }
                            // A linked directory organisation is copied onto the
                            // Case: its identity, version, name and address. The
                            // link the Case already holds is not linked again: it
                            // keeps the copy it has, whether or not the record is
                            // still active, as the Case page's save keeps it (R1022).
                            var linkedRepairer = CaseDetailsSaveRules.RepairerToLink(
                                    persisted?.Repairer,
                                    repairerDirectoryId == Guid.Empty ? null : repairerDirectoryId) is { } directoryId
                                ? await contactDirectory.FindActiveByRoleAsync(
                                        context.Actor, ContactRole.Repairer, directoryId, cancellationToken)
                                    ?? throw new McpException("The selected repairer is not an active directory Repairer.")
                                : null;
                            var unlinkRepairer = repairerDirectoryId == Guid.Empty;
                            overview = new(
                                Text(claimantName, Accepted(data.Claimant.Name)),
                                Text(claimantContactNumber, Accepted(data.Claimant.ContactNumber)),
                                Text(claimantAddress, Accepted(data.Claimant.Address)),
                                Text(claimNumber, Accepted(data.Claim.Number)),
                                Date(incidentDate, nameof(incidentDate), Accepted(data.Accident.IncidentDate)),
                                Text(accidentCircumstances, Accepted(data.Accident.Circumstances)),
                                Text(vatStatus, Accepted(data.Instruction.VatStatus)),
                                linkedRepairer?.Address
                                    ?? Text(repairerAddress, Accepted(data.Inspection.RepairerAddress)),
                                claimSource,
                                CaseDetailsSaveRules.Repairer(
                                    persisted?.Repairer,
                                    linkedRepairer,
                                    unlinkRepairer,
                                    Text(repairerName, Accepted(data.Inspection.RepairerName))),
                                Text(principalNotes, persisted?.PrincipalNotes),
                                Text(claimSourceNotes, persisted?.ClaimSourceNotes),
                                Date(dueBy, nameof(dueBy),
                                    current.Workflow.DueWork?.DueBy ?? Accepted(data.Inspection.Deadline)));
                        }

                        CaseWorkspaceInspection? inspection = null;
                        if (Any(inspectionAddress, storageLocation, inspectionDate, inspectionDeadline))
                        {
                            await LoadAssessmentAsync();
                            var address = Text(inspectionAddress, Accepted(data.Inspection.Address));
                            var treatment = CaseDetailsSaveRules.InspectionTreatment(
                                inspectionAddress is not null, persisted?.InspectionAddressTreatment, address);
                            inspection = new(
                                treatment,
                                address,
                                persisted?.InspectionLocationProvenance,
                                Text(storageLocation, Accepted(data.Inspection.StorageLocation)),
                                Date(inspectionDate, nameof(inspectionDate), Accepted(data.Inspection.InspectionDate)),
                                Date(inspectionDeadline, nameof(inspectionDeadline), Accepted(data.Inspection.Deadline)),
                                persisted?.InspectionVehiclePresent,
                                persisted?.InspectionCondition,
                                persisted?.InspectionNotes,
                                Money(AssessmentVocabulary.SettlementStoragePerDay),
                                Money(AssessmentVocabulary.CostRecoveryCharge));
                        }

                        CaseWorkspaceVehicle? vehicle = null;
                        if (Any(vehicleRegistration, vehicleMake, vehicleModel, vehicleYear, vehicleMileageUnit)
                            || vehicleMileage is not null)
                        {
                            await LoadAssessmentAsync();
                            var mileage = vehicleMileage ?? Accepted(data.Vehicle.Mileage);
                            var unitText = Text(vehicleMileageUnit, Accepted(data.Vehicle.MileageUnit));
                            CaseOdometerUnit? unit = null;
                            if (!string.IsNullOrWhiteSpace(unitText))
                            {
                                unit = CaseOdometer.TryParseUnit(unitText, out var parsed)
                                    ? parsed
                                    : throw new McpException("The mileage unit must be miles or km.");
                            }
                            vehicle = new(
                                Text(vehicleRegistration, Accepted(data.Vehicle.Registration)),
                                Text(vehicleMake, Accepted(data.Vehicle.Make)),
                                Text(vehicleModel, Accepted(data.Vehicle.Model)),
                                new(mileage,
                                    CaseDetailsSaveRules.MileageUnit(mileage, unit),
                                    persisted?.VehicleMileageDisplayUnit),
                                AssessmentFields: null,
                                Text(vehicleYear, Accepted(data.Vehicle.Year)));
                        }

                        if (signOffEngineerId is not null)
                        {
                            await LoadAssessmentAsync();
                        }
                        // The empty identifier names no one: where an engineer
                        // was shown, that removes the recorded pick and the
                        // default applies again, as Unassigned does on the Case
                        // page (R1019).
                        CaseWorkspaceReport? report = signOffEngineerId is not { } engineerId
                            ? null
                            : new(
                                AssessmentFields: null,
                                engineerId == Guid.Empty ? null : engineerId,
                                CaseDetailsSaveRules.RecordedReportDate(Recorded(AssessmentVocabulary.ReportDate)));

                        if (overview is null && inspection is null && vehicle is null && report is null)
                        {
                            throw new McpException("Name at least one value to change.");
                        }
                        return await saveCaseWorkspace.ExecuteAsync(
                            new(caseId, expectedVersion, context.Actor, key, reason, token)
                            {
                                Overview = overview,
                                Inspection = inspection,
                                Vehicle = vehicle,
                                Report = report,
                            },
                            cancellationToken);
                    },
                    cancellationToken);
                return new CaseUpdateDetailsToolResult(
                    caseId,
                    saved.Version,
                    saved.Data.State.ToString(),
                    key);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_case_note_add",
        Title = "Add case note",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Adds a note to the case's timeline, as a member of staff adds one on the Case. A note is append-only and attributed to the Automation actor; it changes no case value and needs no version or edit lease. At most 2000 characters.")]
    public async Task<CaseNoteAddToolResult> AddNoteAsync(
        [Description(AutomationToolText.CaseId)] Guid caseId,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key adds the note once.")] string operationKey,
        [Description("The note text.")] string note,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_case_note_add",
            AutomationMcpAuditor.Resource(caseId),
            operationKey,
            async (context, key) =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                await addCaseNote.ExecuteAsync(new(caseId, context.Actor, key, note), cancellationToken);
                return new CaseNoteAddToolResult(caseId, key);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_vocabulary_get",
        Title = "Get field vocabulary",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the vocabularies the write tools accept: every assessment field path with its type, accepted codes, the format a structured value takes (assessment.impacts, the impacts; damage.impacts, the damage markers), staff label and whether pegasus_assessment_update may write it (for example Roadworthiness is assessment.legal_status and the outcome is assessment.outcome); the estimate line types and evidence labels with their meanings; repairer VAT statuses and VAT categories; valuation sources; the shared image tags pegasus_document_action applies; the enabled staff by username, whose identifiers the assign actions take; the eligible Sign-off Engineers by printed name; and the unroadworthy wordings bank.")]
    public async Task<VocabularyToolResult> GetVocabularyAsync(CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_vocabulary_get",
            "vocabulary",
            async context =>
            {
                // Each read opens its own context, so they run side by side.
                var tagsRead = imageTags.ListAsync(cancellationToken);
                var staffRead = staffChoices.GetAsync(context.Actor, cancellationToken);
                var signOffRead = staffAccounts.ListSignOffEngineersAsync(cancellationToken);
                var bankRead = unroadworthyReasonBank.ListAsync(cancellationToken);
                await Task.WhenAll(tagsRead, staffRead, signOffRead, bankRead);
                return new VocabularyToolResult(
                    AssessmentVocabulary.Definitions.Values
                        .Select(definition =>
                        {
                            var refusal = AssessmentMcpTools.WriteRefusal(definition.Path);
                            return new AssessmentFieldVocabularyItem(
                                definition.Path,
                                definition.Type.ToString(),
                                definition.MaximumLength,
                                definition.MustBePositive,
                                definition.Codes,
                                definition.Format,
                                CaseWorkspaceLabels.Editors.Label(CaseWorkspaceLabels.Editors.FormName(definition.Path)),
                                refusal is null,
                                refusal);
                        })
                        .ToArray(),
                    EstimateLineCodes.Types
                        .Select(type => new CodeMeaningItem(type, OperatorLabels.EstimateLineType(type)))
                        .ToArray(),
                    EstimateLineCodes.EvidenceLabelMeanings
                        .Select(label => new CodeMeaningItem(label.Key, label.Value))
                        .ToArray(),
                    RepairerVatStatuses.ChoosableNames,
                    [nameof(EstimateVatCategories.Labour), nameof(EstimateVatCategories.Parts),
                        nameof(EstimateVatCategories.Materials), nameof(EstimateVatCategories.Specialist)],
                    Enum.GetNames<ValuationSource>(),
                    (await tagsRead)
                        .Select(tag => new ImageTagVocabularyItem(tag.Id, tag.Name, tag.Colour.ToString(), tag.IsBuiltIn))
                        .ToArray(),
                    (await staffRead)
                        .Select(staff => new StaffVocabularyItem(staff.StaffId, staff.DisplayName))
                        .ToArray(),
                    (await signOffRead)
                        .Select(engineer => new SignOffEngineerVocabularyItem(
                            engineer.StaffId, engineer.PrintedName, engineer.Qualifications, engineer.IsDefault))
                        .ToArray(),
                    (await bankRead).Select(wording => wording.Text).ToArray(),
                    context.TraceIdentifier);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_directory_search",
        Title = "Search contact directory",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists the active Contacts directory organisations holding one role, as the Case page offers them to link to a Case: Repairer (for pegasus_case_update_details repairerDirectoryId) or ClaimSource (for claimSourceId). Each carries the codes of the Principals it is linked to; name the Case's principalCode and the organisations linked to that Principal come first, as the Case page lists them, each group in name order. An optional name filter keeps the organisations whose name contains it, ignoring case.")]
    public async Task<DirectorySearchToolResult> SearchDirectoryAsync(
        [Description("The directory role: Repairer or ClaimSource.")] string role,
        [Description("Optional text the organisation's name must contain, ignoring case.")] string? name = null,
        [Description("Optional Principal code (a Case's summary.principal): the organisations linked to that Principal are listed first.")] string? principalCode = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_directory_search",
            "directory",
            async context =>
            {
                var parsed = role?.Trim() switch
                {
                    "Repairer" => ContactRole.Repairer,
                    "ClaimSource" => ContactRole.ClaimSource,
                    _ => throw new McpException("The role must be Repairer or ClaimSource."),
                };
                var filter = name?.Trim();
                var records = await contactDirectory.ListByRoleAsync(context.Actor, parsed, cancellationToken);
                return new DirectorySearchToolResult(
                    parsed.ToString(),
                    records
                        .Where(record => string.IsNullOrEmpty(filter)
                            || record.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        // The directory reads in name order and the sort is
                        // stable, so each group keeps it (R1013, R1016).
                        .OrderByDescending(record => record.IsLinkedTo(principalCode))
                        .Select(record => new DirectoryToolItem(
                            record.OrganizationId,
                            record.Version,
                            record.Name,
                            record.Email,
                            record.Telephone,
                            record.Address,
                            record.Postcode,
                            record.LinkedPrincipalCodes ?? []))
                        .ToArray(),
                    context.TraceIdentifier);
            },
            cancellationToken);
    }

    private static bool Any(params string?[] values) => values.Any(value => value is not null);

    private static string? Accepted(CaseField<string>? field) =>
        (field?.Confirmed ?? field?.Fact)?.Value;

    private static long? Accepted(CaseField<long>? field) =>
        (field?.Confirmed ?? field?.Fact)?.Value;

    private static DateOnly? Accepted(CaseField<DateOnly>? field) =>
        (field?.Confirmed ?? field?.Fact)?.Value;

    /// <summary>Omitted keeps the current value; an empty string clears it.</summary>
    private static string? Text(string? submitted, string? current) =>
        submitted is null ? current : string.IsNullOrWhiteSpace(submitted) ? null : submitted;

    private static DateOnly? Date(string? submitted, string name, DateOnly? current) =>
        submitted is null
            ? current
            : AutomationMcpErrors.OptionalDate(submitted, $"The {name} value must be a yyyy-MM-dd date.");
}
