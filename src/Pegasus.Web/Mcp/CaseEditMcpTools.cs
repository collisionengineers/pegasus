using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record CaseUpdateDetailsToolResult(
    Guid CaseId,
    long CaseVersion,
    string State,
    string OperationKey,
    string CorrelationId);

internal sealed record CaseNoteAddToolResult(
    Guid CaseId,
    string OperationKey,
    string CorrelationId);

internal sealed record ValuationToolItem(
    Guid ValuationId,
    string Source,
    string Date,
    long? Mileage,
    decimal? RetailValue,
    decimal? TradeValue,
    string? GuideMonth,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc);

internal sealed record ValuationListToolResult(
    Guid CaseId,
    IReadOnlyList<ValuationToolItem> Valuations,
    string CorrelationId);

internal sealed record ValuationSaveToolResult(
    Guid CaseId,
    long CaseVersion,
    IReadOnlyList<ValuationToolItem> Valuations,
    string OperationKey,
    string CorrelationId);

internal sealed record GuideCardToolInput(
    [property: Description("The guide the figures come from: Glasses, Cazana, EngineersValue, Brego, SuperCap or Cap.")] string Source,
    [property: Description("Retail value in pounds.")] decimal? RetailValue = null,
    [property: Description("Trade value in pounds.")] decimal? TradeValue = null,
    [property: Description("The guide month the figures are for, yyyy-MM.")] string? GuideMonth = null);

internal sealed record ValuationAdditionToolInput(
    [property: Description("The valuation addition preset identifier.")] Guid PresetId,
    [property: Description("The preset version the amount was read at.")] long PresetVersion,
    [property: Description("The addition's label.")] string? Label,
    [property: Description("The addition amount in pounds.")] decimal Amount);

internal sealed record ValuationAdoptionToolInput(
    [property: Description("The recorded guide card the calculation is based on (pegasus_valuation_list); omit when the basis is a card saved in this same call, and name its guideSource instead.")] Guid? GuideValuationId,
    [property: Description("The basis card's source when it is saved in this same call.")] string? GuideSource,
    [property: Description("Whether commercial VAT applies.")] bool CommercialVat,
    [property: Description("A prior total-loss percentage deduction, if any.")] decimal? PriorTotalLossPercentage,
    [property: Description("Additions to the basis value.")] IReadOnlyList<ValuationAdditionToolInput>? Additions,
    [property: Description("Condition deduction in pounds.")] decimal ConditionDeduction);

internal sealed record AssessmentFieldVocabularyItem(
    string Path,
    string Type,
    int MaximumLength,
    bool MustBePositive,
    IReadOnlyList<string>? Codes,
    string? Label,
    bool Writable,
    string? WhyNotWritable);

internal sealed record CodeMeaningItem(string Code, string Meaning);

internal sealed record VocabularyToolResult(
    IReadOnlyList<AssessmentFieldVocabularyItem> AssessmentFields,
    IReadOnlyList<CodeMeaningItem> EstimateLineTypes,
    IReadOnlyList<CodeMeaningItem> EstimateEvidenceLabels,
    IReadOnlyList<string> RepairerVatStatuses,
    IReadOnlyList<string> EstimateVatCategories,
    IReadOnlyList<string> ValuationSources,
    string CorrelationId);

/// <summary>
/// Automation Actor Case edits (FRD-10, ADR-0064): the Case facts, notes and
/// valuation cards a staff member records on the Case, written through the
/// same Core commands as the staff Case page. The Automation Actor does
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
    IListCaseValuations listValuations,
    TimeProvider timeProvider,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    AutomationEditLease leases)
{
    [McpServerTool(
        Name = "pegasus_case_update_details",
        Title = "Update case details",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Edits the Case facts staff edit on the Case page, through the same Case save: claimant, claim, contact, accident, VAT status, repairer, notes, due by, vehicle identity and mileage, inspection and the Sign-off Engineer. Omit a value to leave it as it is; send an empty string to clear a text or date value. Values not named are never changed. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command). Dates are yyyy-MM-dd; inspectionMode is 'physical_address' or 'image_based_assessment'. Assessment fields (outcome, roadworthiness, values, damage and the rest) are written with pegasus_assessment_update.")]
    public async Task<CaseUpdateDetailsToolResult> UpdateDetailsAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Why these details are being changed (case history reason).")] string? reason = null,
        [Description("Claimant name.")] string? claimantName = null,
        [Description("Claimant contact number.")] string? claimantContactNumber = null,
        [Description("Claimant address.")] string? claimantAddress = null,
        [Description("Claim number.")] string? claimNumber = null,
        [Description("Contact name.")] string? contactName = null,
        [Description("Contact email address.")] string? contactEmailAddress = null,
        [Description("Contact phone number.")] string? contactPhoneNumber = null,
        [Description("Incident date, yyyy-MM-dd.")] string? incidentDate = null,
        [Description("Accident circumstances.")] string? accidentCircumstances = null,
        [Description("VAT status text.")] string? vatStatus = null,
        [Description("Repairer name.")] string? repairerName = null,
        [Description("Repairer address.")] string? repairerAddress = null,
        [Description("The Case's own notes beside its Principal.")] string? principalNotes = null,
        [Description("The Case's own notes beside its Claim source.")] string? claimSourceNotes = null,
        [Description("Notes from client.")] string? clientNotes = null,
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
        [Description("Inspection address.")] string? inspectionAddress = null,
        [Description("Inspection mode: physical_address or image_based_assessment.")] string? inspectionMode = null,
        [Description("Storage location for the vehicle.")] string? storageLocation = null,
        [Description("Inspection date, yyyy-MM-dd; the report prints it as the date the damage was assessed.")] string? inspectionDate = null,
        [Description("Inspection deadline, yyyy-MM-dd.")] string? inspectionDeadline = null,
        [Description("The Sign-off Engineer's staff identifier.")] Guid? signOffEngineerId = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_update_details",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var mode = ParseInspectionMode(inspectionMode);
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
                        var assessment = await getAssessment.ExecuteAsync(caseId, cancellationToken);
                        string? Recorded(string path) => assessment?.Field(path)?.Value;
                        decimal? Money(string path) => decimal.TryParse(
                            Recorded(path), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                            ? value
                            : null;
                        var data = current.Data;
                        var persisted = data.Workspace;

                        CaseWorkspaceOverview? overview = null;
                        if (Any(claimantName, claimantContactNumber, claimantAddress, claimNumber, contactName,
                                contactEmailAddress, contactPhoneNumber, incidentDate, accidentCircumstances,
                                vatStatus, repairerName, repairerAddress, principalNotes, claimSourceNotes,
                                clientNotes, dueBy, claimSourceContactName, claimSourceContactTelephone,
                                claimSourceContactEmail))
                        {
                            var claimSource = persisted?.ClaimSource is { } source
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
                            overview = new(
                                Text(claimantName, Accepted(data.Claimant.Name)),
                                Text(claimantContactNumber, Accepted(data.Claimant.ContactNumber)),
                                Text(claimantAddress, Accepted(data.Claimant.Address)),
                                Text(claimNumber, Accepted(data.Claim.Number)),
                                Text(contactName, Accepted(data.Contact.Name)),
                                Text(contactEmailAddress, Accepted(data.Contact.EmailAddress)),
                                Text(contactPhoneNumber, Accepted(data.Contact.PhoneNumber)),
                                Date(incidentDate, nameof(incidentDate), Accepted(data.Accident.IncidentDate)),
                                Text(accidentCircumstances, Accepted(data.Accident.Circumstances)),
                                Text(vatStatus, Accepted(data.Instruction.VatStatus)),
                                Text(repairerAddress, Accepted(data.Inspection.RepairerAddress)),
                                claimSource,
                                new CaseWorkspaceRepairer(
                                    persisted?.Repairer?.DirectoryOrganizationId,
                                    persisted?.Repairer?.DirectoryOrganizationVersion,
                                    Text(repairerName, Accepted(data.Inspection.RepairerName))),
                                Text(principalNotes, persisted?.PrincipalNotes),
                                Text(claimSourceNotes, persisted?.ClaimSourceNotes),
                                Text(clientNotes, persisted?.ClientNotes),
                                Date(dueBy, nameof(dueBy),
                                    current.Workflow.DueWork?.DueBy ?? Accepted(data.Inspection.Deadline)));
                        }

                        CaseWorkspaceInspection? inspection = null;
                        if (Any(inspectionAddress, inspectionMode, storageLocation, inspectionDate, inspectionDeadline))
                        {
                            var address = Text(inspectionAddress, Accepted(data.Inspection.Address));
                            var treatment = mode is not null
                                ? Treatment(mode)
                                : inspectionAddress is null && persisted?.InspectionAddressTreatment is { } recorded
                                    ? recorded
                                    : Treatment(CaseDataPolicy.InferInspectionMode(address));
                            inspection = new(
                                treatment,
                                address,
                                persisted?.InspectionLocationProvenance,
                                Text(storageLocation, Accepted(data.Inspection.StorageLocation)),
                                persisted?.StorageBusiness,
                                Date(inspectionDate, nameof(inspectionDate), Accepted(data.Inspection.InspectionDate)),
                                Date(inspectionDeadline, nameof(inspectionDeadline), Accepted(data.Inspection.Deadline)),
                                persisted?.InspectionVehiclePresent,
                                persisted?.InspectionCondition,
                                persisted?.InspectionContactName,
                                persisted?.InspectionContactTelephone,
                                persisted?.InspectionContactEmailAddress,
                                persisted?.InspectionNotes,
                                Money(AssessmentVocabulary.SettlementStoragePerDay),
                                Money(AssessmentVocabulary.CostRecoveryCharge));
                        }

                        CaseWorkspaceVehicle? vehicle = null;
                        if (Any(vehicleRegistration, vehicleMake, vehicleModel, vehicleYear, vehicleMileageUnit)
                            || vehicleMileage is not null)
                        {
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
                                    mileage is null ? null : unit ?? CaseOdometerUnit.Miles,
                                    Recorded(AssessmentVocabulary.VehicleMileageSource),
                                    persisted?.VehicleMileageDisplayUnit),
                                AssessmentFields: null,
                                Text(vehicleYear, Accepted(data.Vehicle.Year)));
                        }

                        CaseWorkspaceReport? report = signOffEngineerId is not { } engineerId
                            ? null
                            : new(
                                AssessmentFields: null,
                                engineerId,
                                DateOnly.TryParseExact(Recorded(AssessmentVocabulary.ReportDate), "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var reportDate)
                                    ? reportDate
                                    : null);

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
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
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
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key adds the note once.")] string operationKey,
        [Description("The note text.")] string note,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_case_note_add",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                await addCaseNote.ExecuteAsync(new(caseId, context.Actor, key, note), cancellationToken);
                return new CaseNoteAddToolResult(caseId, key, AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_valuation_list",
        Title = "List case valuations",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists the valuation cards recorded on a case: each guide card, the Engineer's Value card and any AI market research card, with its figures and identifier.")]
    public async Task<ValuationListToolResult> ListValuationsAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.AssessmentScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_valuation_list",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var valuations = await listValuations.ExecuteAsync(caseId, CaseWorkSelector.Current, cancellationToken);
                return new ValuationListToolResult(
                    caseId, valuations.Select(Map).ToArray(), context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_valuation_save",
        Title = "Save case valuation cards",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Records valuation guide cards on a case and, optionally, the valuation calculation against a basis card, as the Valuation section's Save does. A card for the same source and guide month replaces the earlier one. The Retail, Trade and Engineer's values themselves are assessment fields (assessment.values.*), written with pegasus_assessment_update. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command).")]
    public async Task<ValuationSaveToolResult> SaveValuationsAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("The guide cards to record.")] IReadOnlyList<GuideCardToolInput>? guideCards = null,
        [Description("The calculation to record against its basis card.")] ValuationAdoptionToolInput? adoption = null,
        [Description("Why the valuation is being recorded (case history reason).")] string? reason = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.AssessmentScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_valuation_save",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var recordedAt = LondonCalendar.TimeAt(timeProvider.GetUtcNow());
                var cards = (guideCards ?? [])
                    .Select(card => new ValuationDetails(
                        ParseSource(card.Source),
                        DateOnly.FromDateTime(recordedAt),
                        TimeOnly.FromDateTime(recordedAt),
                        // A guide card carries no mileage: the Case's own is used
                        // wherever a valuation needs one (operator, 24 September 2026).
                        null,
                        card.RetailValue,
                        card.TradeValue,
                        ParseGuideMonth(card.GuideMonth)))
                    .ToArray();
                var selection = adoption is null
                    ? null
                    : new ValuationCalculationSelection(
                        adoption.GuideValuationId ?? Guid.Empty,
                        adoption.CommercialVat,
                        adoption.PriorTotalLossPercentage,
                        (adoption.Additions ?? [])
                            .Select(addition => new ValuationAdditionSelection(
                                addition.PresetId, addition.PresetVersion, addition.Label, addition.Amount))
                            .ToArray(),
                        adoption.ConditionDeduction)
                    {
                        GuideSource = adoption.GuideSource is { } source ? ParseSource(source) : null,
                    };
                if (cards.Length == 0 && selection is null)
                {
                    throw new McpException("Name at least one guide card or a calculation to record.");
                }
                var saved = await leases.RunCaseAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    token => saveCaseWorkspace.ExecuteAsync(
                        new(caseId, expectedVersion, context.Actor, key, reason, token)
                        {
                            Valuation = new(cards, selection),
                        },
                        cancellationToken),
                    cancellationToken);
                var valuations = await listValuations.ExecuteAsync(caseId, CaseWorkSelector.Current, cancellationToken);
                return new ValuationSaveToolResult(
                    caseId,
                    saved.Version,
                    valuations.Select(Map).ToArray(),
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
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
    [Description("Returns the vocabularies the write tools accept: every assessment field path with its type, accepted codes, staff label and whether pegasus_assessment_update may write it (for example Roadworthiness is assessment.legal_status and the outcome is assessment.outcome); the estimate line types and evidence labels with their meanings; repairer VAT statuses and VAT categories; and valuation sources.")]
    public async Task<VocabularyToolResult> GetVocabularyAsync(CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_vocabulary_get",
            "vocabulary",
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(() => Task.FromResult(new VocabularyToolResult(
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
                Enum.GetNames<RepairerVatStatus>(),
                [nameof(EstimateVatCategories.Labour), nameof(EstimateVatCategories.Parts),
                    nameof(EstimateVatCategories.Materials), nameof(EstimateVatCategories.Specialist)],
                Enum.GetNames<ValuationSource>(),
                context.TraceIdentifier))),
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

    private static DateOnly? Date(string? submitted, string name, DateOnly? current)
    {
        if (submitted is null)
        {
            return current;
        }
        if (string.IsNullOrWhiteSpace(submitted))
        {
            return null;
        }
        return DateOnly.TryParseExact(submitted.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new McpException($"The {name} value must be a yyyy-MM-dd date.");
    }

    private static CaseInspectionMode? ParseInspectionMode(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim() switch
            {
                "physical_address" => CaseInspectionMode.PhysicalAddress,
                "image_based_assessment" => CaseInspectionMode.ImageBasedAssessment,
                _ => throw new McpException(
                    "The inspection mode must be physical_address or image_based_assessment.")
            };

    private static CaseReportAddressTreatment Treatment(CaseInspectionMode? mode) => mode switch
    {
        CaseInspectionMode.ImageBasedAssessment => CaseReportAddressTreatment.ImageBasedAssessment,
        CaseInspectionMode.PhysicalAddress => CaseReportAddressTreatment.PhysicalVehicleLocation,
        _ => CaseReportAddressTreatment.Undetermined
    };

    private static ValuationSource ParseSource(string value) =>
        Enum.TryParse<ValuationSource>(value?.Trim(), ignoreCase: true, out var source)
            && Enum.IsDefined(source)
            ? source
            : throw new McpException(
                "The valuation source must be one of: " + string.Join(", ", Enum.GetNames<ValuationSource>()) + ".");

    private static DateOnly? ParseGuideMonth(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : DateOnly.TryParseExact(value.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var month)
                ? month
                : throw new McpException("The guide month must be yyyy-MM.");

    private static ValuationToolItem Map(CaseValuation valuation) => new(
        valuation.ValuationId,
        valuation.Details.Source.ToString(),
        valuation.Details.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        valuation.Details.Mileage,
        valuation.Details.RetailValue,
        valuation.Details.TradeValue,
        valuation.Details.GuideMonth?.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        valuation.RecordedBy,
        valuation.RecordedAtUtc);
}
