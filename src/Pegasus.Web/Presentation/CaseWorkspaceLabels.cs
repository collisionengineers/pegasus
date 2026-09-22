using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Presentation;

/// <summary>
/// The Case workspace labels Stream B owns outright. The shared
/// <c>OperatorLabels</c> file is Stream C's; every label only the report
/// generation and delivery journey needs lives here so no B change touches
/// C's file.
/// </summary>
public static class CaseWorkspaceLabels
{
    public const string HandToEngineer = "Hand to Engineer";

    /// <summary>
    /// The v26 frame's own words: the ribbon controls, the Actions menu, the
    /// section-head Edit and the availability sentences a section states once.
    /// </summary>
    public static class Frame
    {
        public const string EditCase = "Edit Case";
        public const string EditingExpired = "Editing expired · changes are not kept";
        public const string EnableReturn = "Enable return";
        public const string Edit = "Edit";
        public const string Cancel = "Cancel";
        public const string Save = "Save";
        public const string Actions = "Actions";
        public const string More = "More";
        public const string Refresh = "Refresh";
        public const string Scroll = "Scroll";
        public const string Tabs = "Tabs";
        public const string CollapseSection = "Collapse section";
        public const string ExpandSection = "Expand section";
        public const string RenewEditing = "Renew editing";
        public const string Release = "Release";
        public const string TakeOver = "Take over";
        public const string Editing = "Editing";
        public const string Archived = "Archived";
        public const string ReturnToEngineerToEdit = "Return the Case to the Engineer to edit";
        public const string Figures = "Figures";
        public const string NextAction = "Next action";
        public const string RepairCostIncVat = "Repair cost inc VAT";
        public const string EngineersValue = "Engineer's Value";
        public const string RepairCostOfValue = "Repair cost of value";
        public const string PlaceOnHold = "Place on Hold";
        public const string ReleaseHold = "Release Hold";
        public const string ReviewOn = "Review on";
        public const string CorrectPrincipal = "Correct principal";
        public const string CreateAudit = "Create audit";
        public const string MarkReportSent = "Mark report sent";
        public const string MarkCompleted = "Mark completed";
        public const string ReturnToReview = "Return to Review";
        public const string ReturnToEngineer = "Return to Engineer";
        public const string ArchiveCase = "Archive case";
        public const string AssignToMe = "Assign to me";
        public const string AuditCase = "Audit case";
        public const string OriginalCase = "Original case";
        public const string ReplacementCase = "Replacement case";
        public const string LifecycleActions = "Lifecycle actions";
        public const string OutstandingRequirements = "Outstanding requirements";
        public const string UnlinkReportEvidence = "Unlink report evidence";
        public const string CaseDataSaved = "Case data saved";
        public const string AiDraftReady = "AI draft ready";
        public const string ReviewEstimate = "Review estimate";
        public const string OpenQuery = "Open query";
        public const string Review = "Review";
        public const string CaseType = "Case type";
        public const string OurRef = "Our ref";
        public const string ClaimReference = "Claim reference";
        public const string IncidentDate = "Incident date";
        public const string Received = "Received";
        public const string Due = "Due";
        public const string ClaimSource = "Claim source";
        public const string ClaimSourceContact = "Claim source contact";
        public const string Contact = "Contact";
        public const string ContactName = "Contact name";
        public const string ContactEmail = "Contact e-mail";
        public const string ContactPhone = "Contact phone";
        public const string Address = "Address";
        public const string VatStatus = "VAT status";
        public const string Notes = "Notes";
        public const string PrincipalNotes = "Principal notes";
        public const string PrincipalNotesThisCase = "Principal notes · this Case";
        public const string ClaimSourceNotes = "Claim source notes";
        public const string ClaimSourceNotesThisCase = "Claim source notes · this Case";
        public const string AccidentCircumstances = "Accident circumstances";
        public const string NotesFromClient = "Notes from client";
        public const string NoClaimSource = "None";
        public const string ReportSent = "Report sent";
        public const string LeaseExpires = "Lease expires";
    }

    // Presentation membership only; types, allowed codes and authority remain
    // owned by AssessmentVocabulary and the workspace command.
    public static class Editors
    {
        public static IReadOnlyDictionary<string, string> Settlement { get; } = new Dictionary<string, string>
        {
            [AssessmentVocabulary.Outcome] = "Outcome",
            [AssessmentVocabulary.SalvageCategory] = "Salvage category",
            [AssessmentVocabulary.SalvageValue] = "Salvage value",
            [AssessmentVocabulary.LegalStatus] = "Roadworthiness",
            [AssessmentVocabulary.UnroadworthyReason] = "Unroadworthy reason",
            [AssessmentVocabulary.SettlementExcess] = "Excess",
            [AssessmentVocabulary.SettlementBetterment] = "Betterment",
            [AssessmentVocabulary.SettlementClaimantVatRegistered] = "Claimant VAT registered",
            [AssessmentVocabulary.SettlementReserve] = "Reserve",
            [AssessmentVocabulary.SettlementRepairDelays] = "Repair delays",
            [AssessmentVocabulary.SettlementReportDelay] = "Report delay",
            [AssessmentVocabulary.SettlementHireStart] = "Hire start",
            [AssessmentVocabulary.SettlementHireDailyCost] = "Hire daily cost",
            [AssessmentVocabulary.SettlementDiminution] = "Diminution",
            [AssessmentVocabulary.SettlementSalvageAt] = "Salvage location",
            [AssessmentVocabulary.SettlementSalvageAgent] = "Salvage agent",
            [AssessmentVocabulary.SettlementSalvageAgentReference] = "Salvage agent reference",
            [AssessmentVocabulary.SettlementSalvageMoved] = "Salvage moved",
            [AssessmentVocabulary.SettlementSalvageOwnerRetains] = "Owner retains salvage",
            [AssessmentVocabulary.SettlementSalvageValueAgreed] = "Salvage value agreed",
            [AssessmentVocabulary.SettlementSalvageSettled] = "Salvage settled"
        };

        public static IReadOnlyDictionary<string, string> Report { get; } = new Dictionary<string, string>
        {
            [AssessmentVocabulary.EngineersComments] = "Engineer's comments",
            [AssessmentVocabulary.AgreedFee] = "Agreed fee",
            [AssessmentVocabulary.FeeDescriptionLines] = "Fee description",
            [AssessmentVocabulary.ReportDiscloseGuideSource] = "Disclose guide source",
            [AssessmentVocabulary.ReportValuationCommentary] = "Valuation commentary",
            [AssessmentVocabulary.ReportValuationCommentaryText] = "Valuation commentary text",
            [AssessmentVocabulary.ReportIncludeUnrelatedDamage] = "Include unrelated damage",
            [AssessmentVocabulary.ReportDateOverride] = "Override report date"
        };

        public static IReadOnlyDictionary<string, string> Damage { get; } = new Dictionary<string, string>
        {
            [AssessmentVocabulary.DamageTyreRightFront] = "Right-front tyre",
            [AssessmentVocabulary.DamageTyreLeftFront] = "Left-front tyre",
            [AssessmentVocabulary.DamageTyreRightRear] = "Right-rear tyre",
            [AssessmentVocabulary.DamageTyreLeftRear] = "Left-rear tyre",
            [AssessmentVocabulary.DamageBeltRightFront] = "Right-front belt",
            [AssessmentVocabulary.DamageBeltLeftFront] = "Left-front belt",
            [AssessmentVocabulary.DamageBeltRightRear] = "Right-rear belt",
            [AssessmentVocabulary.DamageBeltLeftRear] = "Left-rear belt",
            [AssessmentVocabulary.DamageSpareTyre] = "Spare tyre",
            [AssessmentVocabulary.DamageCentreBelt] = "Centre belt",
            [AssessmentVocabulary.DamageUnrelated] = "Unrelated damage",
            [AssessmentVocabulary.DamageUnrelatedDeduction] = "Unrelated-damage deduction",
            [AssessmentVocabulary.DamageMaterialTransfer] = "Material transfer"
        };

        /// <summary>The vehicle's identity the Vehicle section edits beside the registration.</summary>
        public static IReadOnlyDictionary<string, string> Vehicle { get; } = new Dictionary<string, string>
        {
            [AssessmentVocabulary.VehicleVin] = "VIN",
            [AssessmentVocabulary.VehicleType] = "Vehicle type",
            [AssessmentVocabulary.VehicleBody] = "Body type"
        };

        public static string FormName(string path) => $"assessmentFields[{path}]";

        public static string? Label(string field)
        {
            if (field == FormName(AssessmentVocabulary.HistoryCheck)) return "Vehicle history";
            if (field == FormName(AssessmentVocabulary.VehicleCondition)) return "Pre-incident condition";
            foreach (var entry in Settlement.Concat(Report).Concat(Damage).Concat(Vehicle))
            {
                if (field == FormName(entry.Key)) return entry.Value;
            }
            return field switch
            {
                "storagePerDay" => "Storage per day",
                "recoveryCharge" => "Recovery charge",
                "signOffEngineerId" => "Sign-off Engineer",
                "reportDate" => "Report date",
                _ => null
            };
        }

        public static bool IsAssessmentField(string path) =>
            Settlement.ContainsKey(path) || Report.ContainsKey(path) || Damage.ContainsKey(path)
            || Vehicle.ContainsKey(path) || path == AssessmentVocabulary.HistoryCheck
            || path == AssessmentVocabulary.VehicleCondition;
    }

    /// <summary>
    /// The Damage section's own words (v26 Plan clicker): the workbench
    /// captions, the derived cells and the recorded-zones list.
    /// </summary>
    public static class Damage
    {
        public const string DiagramLabel = "Vehicle damage diagram, plan view";
        public const string Front = "FRONT";
        public const string Rear = "REAR";
        public const string RecordedZones = "Recorded zones";
        public const string NoDamageRecorded = "No damage recorded.";
        public const string Severity = "Severity";
        public const string Note = "Note";
        public const string NoNote = "No note";
        public const string Remove = "Remove";
        public const string TyresAndBelts = "Tyres & seat belts";
        public const string Multiple = "Multiple";
        public const string OtherAreas = "Other vehicle areas";

        /// <summary>A code word as the cell prints it: "not_fitted" reads "Not fitted".</summary>
        public static string CodeWord(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return code;
            }
            var words = code.Replace('_', ' ');
            return char.ToUpperInvariant(words[0]) + words[1..];
        }

        /// <summary>The impact location as the derived cell prints it, from Core's headline codes.</summary>
        public static string Location(string? code) =>
            code is null ? OperatorLabels.CaseWorkspace.AbsentValue
            : code == "multiple" ? Multiple
            : AssessmentVocabulary.DamageZones.TryGetValue(code, out var zone) ? zone.Display
            : code == "wheel" ? "Wheel"
            : CodeWord(code);

        public static string SeverityWord(string? code) =>
            code is not null && AssessmentVocabulary.DamageSeverities.TryGetValue(code, out var severity)
                ? severity.Display
                : OperatorLabels.CaseWorkspace.AbsentValue;
    }

    /// <summary>
    /// The Settlement section's own words (v26 § Settlement): the figures
    /// strip, the Decisions strip and its proposal column.
    /// </summary>
    public static class Settlement
    {
        public const string Decisions = "Decisions";
        public const string Proposed = "Proposed";
        public const string Awaiting = "Awaiting";
        public const string Accepted = "Accepted";
        public const string Corrected = "Corrected";
        public const string Accept = "Accept";
        public const string AcceptAll = "Accept all";
        public const string ApplyInValuation = "Apply in Valuation";
        public const string ValuationLink = "Valuation";
        public const string EngineersValue = "Engineer's Value";
        public const string SalvageValue = "Salvage value";
        public const string Equity = "Equity";
        public const string EquityMeta = "Engineer's Value − salvage";
        public const string RepairCostIncVat = "Repair cost inc VAT";
        public const string CashInLieu = "Cash in lieu";
        public const string LabourHours = "Labour hours";
        public const string RepairCostOfValue = "Repair cost of value";
        public const string ExceedsEngineersValue = "Exceeds Engineer's Value";
        public const string FromCurrentEstimate = "From current estimate";
        public const string CurrentEstimate = "current estimate";
        public const string ApplyInValuationMeta = "Apply in Valuation";
        public const string CostsHireDelays = "Costs, hire & delays";
        public const string Salvage = "Salvage";
        public const string StorageCharge = "Storage charge";
        public const string RepairDays = "Repair days";
        public const string AwaitingReview = "awaiting review";
        public const string AiProposal = "AI proposal";
    }

    /// <summary>The Report section's own words (v26 § Report).</summary>
    public static class Report
    {
        public const string More = "More";
        public const string PreviewDraft = "Preview draft";
        public const string DownloadDraft = "Download draft";
        public const string NotReady = "Report not ready";
        public const string NoGeneration = "No generation yet.";
        public const string Generated = "Generated";
        public const string DraftSuffix = " (draft).pdf";
        public const string SignOffEngineer = "Sign-off Engineer";
        public const string ReportDate = "Report date";
        public const string ReportContent = "Report content";
        public const string DiscloseGuideSource = "Disclose guide source";
        public const string ValuationCommentary = "Valuation commentary";
        public const string UnrelatedDamage = "Unrelated damage";
        public const string ImagesInReport = "Images in report";
        public const string InReport = "in report";
        public const string ReportImagePreparation = "Report image preparation";
        public const string ReviewedRecipients = "Reviewed recipients";
        public const string AddTo = "Add To recipient";
        public const string AddCc = "Add Cc recipient";
    }

    /// <summary>
    /// The Estimate section's own words (v26 § Estimate): the head controls,
    /// the tab source tags, the grid's source column, the Glass's session
    /// line and the work-lists. "Send to AI" is the one name the AI has here.
    /// </summary>
    public static class Estimate
    {
        public const string SendToAi = "Send to AI";
        public const string Import = "Import";
        public const string Expand = "Expand";
        public const string ExpandEstimate = "Expand estimate";
        public const string CloseFullScreen = "Close full screen";
        public const string Discard = "Discard";
        public const string DiscardEstimate = "Discard estimate";
        public const string Blend = "Blend";
        public const string LabourRateCard = "Labour-rate card";
        public const string KeepEnteredRate = "Keep entered rate";
        public const string PartNumberShort = "Part no.";
        public const string UnitPounds = "Unit £";
        public const string Hours = "Hours";
        public const string DiscountsPercent = "Discounts %";
        public const string Overall = "Overall";
        public const string Overridden = "Overridden";
        public const string ResetToRepairerStatus = "Reset to repairer status";
        public const string Repairer = "Repairer";
        public const string MainNewParts = "Main new parts required";
        public const string RepairsRequired = "Repairs required";
        public const string AdditionalOperations = "Additional operations";
        public const string None = "none";
        public const string On = "on";
        public const string RepairCostIncVat = "Repair cost inc VAT";
        public const string DraftSuffix = "draft";
        public const string EstimateFile = "Estimate file";
        public const string SourceImported = "imported";
        public const string SourceAmended = "amended";
        public const string SourceManual = "manual";
        public const string RouteManual = "Manual";
        public const string RouteGlasses = "Glass's";
        public const string RouteAudatex = "Audatex PDF";
        public const string RouteJson = "JSON";
        public const string RouteAi = "AI";
        public const string RouteUnknown = "Recorded";
        public const string HeldFromAnotherCase = "Your account is held from another Case";
        public const string Started = "started";
        public const string AvailableWhileEditing = "Available while editing";
        public const string AddALine = "Add a line";
        public const string RemoveThisLine = "Remove this line";

        public static string DiscardPrompt(string name) =>
            $"Discard {name} and its lines from this case?";
    }

    public static class EstimateImport
    {
        public const string Complete = "Complete import";
        public const string Sources = "Retained estimate sources";
        public const string SourceRetained = "Estimate source retained.";
        public const string Imported = "Estimate imported as a Draft.";
    }

    /// <summary>
    /// The Vehicle section's own surface. These live here, not in the shared
    /// OperatorLabels, because they are Case-only: the shared file is Stream
    /// C's and B never edits it.
    /// </summary>
    public static class Vehicle
    {
        public const string LookupDvlaMot = "Look up DVLA & MOT";
        public const string Registration = "Registration";
        public const string Make = "Make";
        public const string Model = "Model";
        public const string Year = "Year";
        public const string Vin = "VIN";
        public const string VehicleType = "Vehicle type";
        public const string BodyType = "Body type";
        public const string EngineCc = "Engine";
        public const string Fuel = "Fuel";
        public const string Colour = "Colour";
        public const string Transmission = "Transmission";
        public const string Body = "Body";
        public const string TaxExpiry = "Tax expiry";
        public const string MotExpiry = "MOT expiry";
        public const string MileageAndCondition = "Mileage & condition";
        public const string Mileage = "Mileage";
        public const string MileageSourceLabel = "Mileage source";
        public const string PreIncidentCondition = "Pre-incident condition";
        public const string OdometerUnit = "Odometer unit";
        public const string Miles = "Miles";
        public const string Kilometres = "Kilometres";
        public const string History = "Vehicle history";

        /// <summary>The provenance word on a value a lookup wrote.</summary>
        public const string LookupWord = "Lookup";

        /// <summary>13 September (Work Centre D1): a blocking external failure reads in operator words at the point of use.</summary>
        public const string LookupFailedPrefix = "Lookup failed · ";

        /// <summary>
        /// The head's one lookup line: the shared outcome wording, with a
        /// failure read as "Lookup failed · {reason} (at)" rather than "Failed:".
        /// </summary>
        public static string LookupLine(Pegasus.Core.Vehicle.VehicleLookupObservation? observation)
        {
            var line = OperatorLabels.VehicleLookup.Outcome(observation);
            return line.StartsWith("Failed: ", StringComparison.Ordinal)
                ? LookupFailedPrefix + line["Failed: ".Length..]
                : line;
        }

        /// <summary>The pre-incident condition code as the operator reads it.</summary>
        public static string ConditionLabel(string code) => code switch
        {
            "poor" => "Poor",
            "below_average" => "Below average",
            "average" => "Average",
            "good" => "Good",
            "excellent" => "Excellent",
            _ => OperatorLabels.Humanise(code)
        };

        /// <summary>
        /// A lookup-sourced assessment value as a read value: a date in the
        /// office's short form, an enumerated code as words, everything else
        /// as recorded.
        /// </summary>
        public static string AssessmentValue(string path, string value)
        {
            var definition = AssessmentVocabulary.Definitions[path];
            if (definition.Type == AssessmentFieldType.Date
                && DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            }
            if (definition.Type == AssessmentFieldType.Enumerated)
            {
                return OperatorLabels.Humanise(value);
            }
            return path == AssessmentVocabulary.VehicleEngineCc ? value + " cc" : value;
        }

        /// <summary>
        /// Who told staff the mileage, for the codes staff may pick from once
        /// they have entered the figure themselves. Every other code is
        /// derived from the figure's own provenance and is never offered.
        /// </summary>
        public static string MileageSource(string code) => code switch
        {
            CaseVehicleMileageSourcePolicy.Owner => "Owner",
            CaseVehicleMileageSourcePolicy.Repairer => "Repairer",
            CaseVehicleMileageSourcePolicy.Principal => "Principal",
            _ => throw new ArgumentOutOfRangeException(nameof(code))
        };
    }

    /// <summary>
    /// The Inspection details section's own words (v26), same ownership rule
    /// as Vehicle; the shared Inspect-at, Repairer and Storage location labels
    /// stay in OperatorLabels.CaseWorkspace.
    /// </summary>
    public static class Inspection
    {
        public const string SectionTitle = "Inspection details";
        public const string InspectionType = "Inspection type";
        public const string InspectionDate = "Inspection date";
        public const string Address = "Address";
        public const string Storage = "Storage";
        public const string StoragePerDay = "Storage per day";
    }

    /// <summary>
    /// The Valuation section's source-card surface, same ownership rule as
    /// Vehicle.
    /// </summary>
    public static class Valuation
    {
        public const string SectionTitle = "Valuation";
        public static string NotConnected(ValuationSource source) => SourceLabel(source) + " is not connected";

        /// <summary>Get valuation on a source that answered with nothing (operator's words, 18 September 2026).</summary>
        public const string Error = "Error. Contact an administrator.";
        public static string CazanaSeam => NotConnected(ValuationSource.Cazana);
        public const string AbsentGuideMonth = "Not recorded";

        // v26: the calculator (v25 decision 8) and the per-source Get valuation row.
        public const string EngineersValueHead = "Engineer's Value";
        public const string ValuationMonth = "Valuation month";
        public const string GetValuation = "Get valuation";
        public const string Basis = "Basis";
        public const string Retail = "Retail";
        public const string Trade = "Trade";
        public const string Calculation = "Calculation";
        public const string FromRetail = "from {0} retail";
        public const string Researching = "Researching";
        public const string PreviousTotalLoss = "Previous total loss";
        public const string ConditionDeduction = "Condition deduction";
        public const string CommercialVat = "Commercial VAT";
        public const string AddVat = "Add 20 % VAT";
        public const string ClaimantVatRegistered = "Claimant is VAT registered";
        public const string ValueIncreases = "Value increases";
        public const string OtherAddition = "Other…";
        public const string ApplyAsEngineersValue = "Apply as Engineer's Value";
        public const string AppliedEngineersValue = "Applied Engineer's Value";
        public const string NoneYet = "None yet";
        public const string AppliedBy = "Applied by";
        public const string Adjustments = "Adjustments";
        public const string NoAdjustments = "None";
        public const string Applied = "Applied";
        public const string NotApplied = "Not applied";
        public const string GuideMonth = "Guide month";
        public const string Mileage = "Mileage";
        public const string Listings = "listings";
        public const string ChooseBasis = "Choose a basis card to calculate.";
        public const string GuideRetail = "Guide retail";
        public const string ProposedEngineersValue = "Proposed Engineer's Value";

        public static string SourceLabel(ValuationSource source) => source switch
        {
            ValuationSource.Glasses => "Glass's",
            ValuationSource.Cazana => "Cazana",
            ValuationSource.EngineersValue => "Engineer's Value",
            ValuationSource.AiMarketResearch => "AI market research",
            ValuationSource.Brego => "Brego",
            ValuationSource.SuperCap => "Super CAP",
            _ => source.ToString(),
        };

        /// <summary>The hook slug for a source, one list beside its label.</summary>
        public static string SourceSlug(ValuationSource source) => source switch
        {
            ValuationSource.Glasses => "glasses",
            ValuationSource.Cazana => "cazana",
            ValuationSource.EngineersValue => "engineers-value",
            ValuationSource.AiMarketResearch => "ai-market-research",
            ValuationSource.Brego => "brego",
            ValuationSource.SuperCap => "super-cap",
            _ => source.ToString().ToLowerInvariant(),
        };
    }

    /// <summary>
    /// The Notes section's manual-chase form (PR 670 port): a chase names
    /// who was chased and what was said; the time is the server's.
    /// </summary>
    public static class Chase
    {
        public const string Recipient = "Recipient";
        public const string Content = "Content";
        public const string RecordChase = "Record chase";
    }

    /// <summary>
    /// The estimate totals block's row labels (B04). The five printed
    /// components, the net and the gross are what the canonical breakdown
    /// carries, so the block names them rather than the flat pre-B04 rows;
    /// Parts and VAT keep the shared labels they already have.
    /// </summary>
    public static class EstimateTotals
    {
        public const string PanelLabour = "Panel labour";
        public const string PaintLabour = "Paint labour";
        public const string Materials = "Materials";
        public const string Specialist = "Specialist";
        public const string Net = "Net";
        public const string Gross = "Gross";
    }

    /// <summary>
    /// The estimate header's VAT surface (B08): the repairer's status, the
    /// categories the estimate's percentage is charged on, and the condition
    /// that gates Use estimate while neither has been recorded. The category
    /// names are the totals block's own, so the screen never labels the same
    /// money two ways.
    /// </summary>
    public static class EstimateVat
    {
        public const string RepairerStatus = "Repairer VAT status";
        public const string ChargedOn = "VAT charged on";
        public const string NoCategories = "Nothing";
        public const string UnknownStatusCondition = "No repairer VAT status recorded";

        /// <summary>
        /// The four categories of <see cref="EstimateVatCategories.All"/>, in
        /// the order the screen states them. A fifth category added to Core
        /// must be added here to appear at all.
        /// </summary>
        public static IReadOnlyList<EstimateVatCategories> Categories { get; } =
        [
            EstimateVatCategories.Labour,
            EstimateVatCategories.Parts,
            EstimateVatCategories.Materials,
            EstimateVatCategories.Specialist,
        ];

        public static string StatusLabel(RepairerVatStatus status) => status switch
        {
            RepairerVatStatus.Unknown => "Unknown",
            RepairerVatStatus.Registered => "Registered",
            RepairerVatStatus.NotRegistered => "Not registered",
            _ => status.ToString(),
        };

        public static string CategoryLabel(EstimateVatCategories category) => category switch
        {
            EstimateVatCategories.Labour => "Labour",
            EstimateVatCategories.Parts => OperatorLabels.CaseWorkspace.EngineerSections.Parts,
            EstimateVatCategories.Materials => EstimateTotals.Materials,
            EstimateVatCategories.Specialist => EstimateTotals.Specialist,
            _ => category.ToString(),
        };

        /// <summary>The charged categories as a value, in the order above.</summary>
        public static string ChargedLabel(EstimateVatPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);
            var charged = Categories.Where(policy.Charges).Select(CategoryLabel).ToArray();
            return charged.Length == 0 ? NoCategories : string.Join(" · ", charged);
        }
    }

    /// <summary>
    /// The estimate header's four discounts (B08). Core holds them as
    /// fractions; the screen states and reads them as percentages, so the
    /// one conversion lives beside the one set of names.
    /// </summary>
    public static class EstimateDiscount
    {
        public const string Parts = "Parts discount";
        public const string Materials = "Materials discount";
        public const string Specialist = "Specialist discount";
        public const string Overall = "Overall discount";

        /// <summary>The editor's form label: the name with its unit.</summary>
        public static string Percent(string label) => label + " %";

        /// <summary>
        /// The one conversion between Core's fraction and the percentage the
        /// screen states, so the editor's box and the read-only value can
        /// never disagree about the same discount.
        /// </summary>
        public static string PercentValue(decimal fraction) =>
            (fraction * 100m).ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>The read-only value: the percentage with its unit.</summary>
        public static string Value(decimal fraction) => PercentValue(fraction) + "%";
    }

    /// <summary>
    /// The report-image preparation surface (B06): where each image sits in
    /// the generated report. Named "Report position" rather than "Report
    /// images" because an image's own classification is now its tags; this
    /// vocabulary is composition — one Close-up, one Overview, ordered
    /// Supporting.
    /// </summary>
    public static class ReportImages
    {
        public const string SectionTitle = "Report position";
        public const string Role = "Role";
        public const string Order = "Order";
        public const string Rotation = "Rotation";
        public const string Crop = "Crop";
        public const string CropLeft = "Left";
        public const string CropTop = "Top";
        public const string CropWidth = "Width";
        public const string CropHeight = "Height";
        public const string Save = "Save";
        public const string Reset = "Reset";
        public const string MoveUp = "Move up";
        public const string MoveDown = "Move down";
        public const string RotateLeft = "Rotate left";
        public const string RotateRight = "Rotate right";
        public const string FullFrame = "Full frame";

        /// <summary>The reason each preparation command records on the case.</summary>
        public const string SaveReason = "Report images prepared.";
        public const string ResetReason = "Report image preparation reset.";
        public const string WasSaved = "The report image preparation was saved.";
        public const string WasReset = "The report image preparation was reset.";
        public const string SaveRefused =
            "The report image preparation was not saved. Retry the operation.";
        public const string ResetRefused =
            "The report image preparation was not reset. Retry the operation.";

        public static string RoleLabel(CaseAssetReportRole role) => role switch
        {
            CaseAssetReportRole.NotUsed => "Not used",
            CaseAssetReportRole.CloseUp => "Close-up",
            CaseAssetReportRole.Overview => "Overview",
            CaseAssetReportRole.Supporting => "Supporting",
            _ => role.ToString(),
        };

        public static string RotationLabel(CaseAssetRotation rotation) =>
            rotation == CaseAssetRotation.None
                ? "None"
                : ((int)rotation).ToString(CultureInfo.InvariantCulture) + "°";

        /// <summary>
        /// The crop as a value: the whole rotated source, or the four
        /// fractions that select part of it.
        /// </summary>
        public static string CropLabel(CaseAssetCrop crop)
        {
            ArgumentNullException.ThrowIfNull(crop);
            return crop.IsFull
                ? FullFrame
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} {1:0.##} · {2} {3:0.##} · {4} {5:0.##} · {6} {7:0.##}",
                    CropLeft,
                    crop.Left,
                    CropTop,
                    crop.Top,
                    CropWidth,
                    crop.Width,
                    CropHeight,
                    crop.Height);
        }
    }

    /// <summary>
    /// The Report section's generation and delivery surface. Labels only —
    /// values come from the persisted generation and preparation records,
    /// and a Sent claim never appears here because transport observation is
    /// Stream A's.
    /// </summary>
    public static class ReportDelivery
    {
        public const string GenerateReport = "Generate report";
        public const string GenerateFeeNote = "Generate fee note";
        public const string ReportGenerated = "The report was generated.";
        public const string FeeNoteGenerated = "The fee note was generated.";
        public const string GenerationPending =
            "The report is still being filed to Box.";
        public const string GenerationNotReady = "Report not ready";
        public const string CurrentGeneration = "Generated";
        public const string GenerationState = "State";
        public const string IncludeFeeNote = "Include fee note";
        public const string DownloadReport = "Report";
        public const string DownloadFeeNote = "Fee note";
        public const string DownloadReportWithFeeNote = "Report with fee note";
        public const string GenerationStaleNotice =
            "A newer fact changed after this generation. Generate again before delivery.";
        public const string PrepareDelivery = "Prepare delivery";
        public const string DeliveryPrepared = "Delivery prepared";
        public const string SendPreparedReport = "Send prepared report";
        public const string SendObservedSent = "The report send was observed as sent.";
        public const string SendAccepted = "The report send was accepted.";
        public const string SendInProgress = "The report send is in progress.";
        public const string SendCancelled = "The send was cancelled.";
        /// <summary>
        /// One consequence sentence, no retry advice: an Unknown outcome must
        /// never be blindly repeated, so the copy cannot invite a
        /// retry.
        /// </summary>
        public const string SendUnknown = "The send result is not yet known.";
        public const string SendFailed = "The report send failed.";
    }

    /// <summary>
    /// The Estimate section's Glass's surface: the control that opens the
    /// provider's estimator, the staff member's own session for this Case, and the
    /// outcomes the provider's return can land on. The state words are the one
    /// operator-facing vocabulary for
    /// <see cref="GlassRepairEstimateSessionState"/>, so the Case section and
    /// the return page cannot name the same session two ways.
    /// </summary>
    public static class GlassSession
    {
        public const string Launch = "Glass's";
        public const string Resume = "Resume";
        public const string Close = "Close session";
        public const string CloseReason = "Reason";
        public const string ExternalClosedConfirmation = "Glass's is closed and no estimate remains open";
        public const string CloseConsequence = "Closing this record releases the Glass's account for another estimate.";
        public const string Closed = "The Glass's session was closed.";
        public const string CloseRefused = "The Glass's session was not closed.";
        public const string State = "State";
        public const string Failure = "Failure";
        public const string OpenOn = "Open on";

        /// <summary>The outcomes a launch, a return or a resume reports.</summary>
        public const string Imported = "The Glass's estimate was recorded as a draft.";

        public const string AwaitingImport = "The Glass's estimate is held. Not yet recorded.";

        public const string NotImported = "The Glass's estimate was not recorded.";

        /// <summary>
        /// An uncertain provider outcome states what is known and invites no
        /// retry, the same rule the report send follows.
        /// </summary>
        public const string OutcomeUnknown = "The Glass's session result is not yet known.";

        public const string LaunchRefused =
            "The Glass's estimate was not started. Retry the operation.";

        public const string ResumeRefused =
            "The Glass's session was not resumed. Retry the operation.";

        /// <summary>
        /// What a settled session reports, wherever it settled: the Estimate
        /// section's own commands and the provider's return read the same one
        /// sentence per outcome.
        /// </summary>
        public static string OutcomeMessage(GlassRepairEstimateSessionState state) => state switch
        {
            GlassRepairEstimateSessionState.Completed => Imported,
            GlassRepairEstimateSessionState.AwaitingImport => AwaitingImport,
            GlassRepairEstimateSessionState.Unknown => OutcomeUnknown,
            _ => NotImported,
        };

        public static string StateLabel(GlassRepairEstimateSessionState state) => state switch
        {
            GlassRepairEstimateSessionState.Prepared => "Prepared",
            GlassRepairEstimateSessionState.Launching => "Starting",
            GlassRepairEstimateSessionState.Active => "Open",
            GlassRepairEstimateSessionState.Importing => "Recording",
            GlassRepairEstimateSessionState.AwaitingImport => "Waiting",
            GlassRepairEstimateSessionState.Completed => "Recorded",
            GlassRepairEstimateSessionState.Failed => "Failed",
            GlassRepairEstimateSessionState.Unknown => "Unknown",
            GlassRepairEstimateSessionState.Expired => "Expired",
            GlassRepairEstimateSessionState.Cancelled => "Cancelled",
            _ => state.ToString(),
        };
    }

    /// <summary>
    /// The Files section (v26 § Files): the tabs, the row actions and the
    /// empty states. Values only; the custody words are the shared labels'.
    /// </summary>
    public static class Files
    {
        public const string CorrespondenceTab = "Correspondence";
        public const string BoxConfirmed = "Box · confirmed";
        public const string OpenInBox = "Open in Box";
        public const string View = "View";
        public const string Remove = "Remove";
        public const string RemoveFile = "Remove file";
        public const string Compose = "Compose";
        public const string StorageNotReady = "Storage not ready";
        public const string NoDocuments = "No documents on this Case.";
        public const string NoImages = "No images on this Case.";
        public const string NoCorrespondence = "No retained correspondence is associated with this Case.";
        public const string ImageIntake = "Image intake";
        public const string Photographs = "photographs";
    }

    /// <summary>
    /// The Case record's full-screen viewer (v26 § Image viewer): its
    /// controls and the crop editor on its stage.
    /// </summary>
    public static class Viewer
    {
        public const string Title = "Image viewer";
        public const string Rotate = "Rotate";
        public const string Zoom = "Zoom";
        public const string Fit = "Fit";
        public const string Download = "Download";
        public const string DownloadDraft = "Download draft";
        public const string InReport = "In report";
        public const string Close = "Close";
        public const string Previous = "Previous";
        public const string Next = "Next";
        public const string Aspect = "Aspect";
        public const string AspectFree = "Free";
        public const string AspectSquare = "Square";
        public const string SaveCrop = "Save crop";
        public const string Cancel = "Cancel";
    }

    /// <summary>
    /// Image tags: the shared vocabulary an operator puts on a Case image, in
    /// the Files section's Images tab. Names and button text only.
    /// </summary>
    public static class ImageTags
    {
        public const string DocumentsTab = "Documents";
        public const string ImagesTab = "Images";
        public const string Tag = "Tag";
        public const string Tags = "Tags";
        public const string NewTag = "New tag";
        public const string Name = "Name";
        public const string Colour = "Colour";
        public const string Create = "Create";
        public const string Crop = "Crop";
        public const string Done = "Done";
        public const string WasApplied = "The tag was applied.";
        public const string WasRemoved = "The tag was removed.";
        public const string WasCreated = "The tag was created.";
        public const string NameRequired = "Enter a tag name.";
        public const string NameInUse = "That tag name is already in use.";
        public const string NotCreated = "The tag was not created.";

        /// <summary>How many chips a tile draws before it counts the rest.</summary>
        public const int VisibleChips = 3;

        /// <summary>The count of tags a tile has beyond the ones it drew.</summary>
        public static string MoreChips(int count) =>
            "+" + count.ToString(CultureInfo.InvariantCulture);

        /// <summary>The palette entry as the picker names it.</summary>
        public static string ColourLabel(ImageTagColour colour) => colour switch
        {
            ImageTagColour.Blue => "Blue",
            ImageTagColour.Green => "Green",
            ImageTagColour.Amber => "Amber",
            ImageTagColour.Navy => "Navy",
            ImageTagColour.Red => "Red",
            ImageTagColour.Grey => "Grey",
            _ => throw new ArgumentOutOfRangeException(nameof(colour))
        };

        /// <summary>The CSS token the chip and swatch are painted with.</summary>
        public static string ColourToken(ImageTagColour colour) =>
            colour.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// The per-Engineer Glass repair-estimate credential page's words, same
    /// ownership rule as the sections above: Case- and Glass-only labels live
    /// here so no Stream B change touches the shared OperatorLabels file.
    /// Nothing here names, echoes or describes a secret.
    /// </summary>
    public static class GlassCredential
    {
        public const string Title = "Glass's repair estimate credential";
        public const string Account = "Account";
        public const string Username = "Username";
        public const string Password = "Password";
        public const string Generation = "Generation";
        public const string Version = "Version";
        public const string Updated = "Updated";
        public const string Save = "Save credential";
        public const string Clear = "Clear credential";
        public const string Enabled = "Enabled";
        public const string DisabledState = "Disabled";
        public const string NotConfigured = "Not configured";
        public const string Saved = "The credential was saved.";
        public const string Cleared = "The credential was cleared.";
        public const string UsernameRequired = "Enter a username.";
        public const string PasswordRequired = "Enter a password.";
        public const string StaleVersion =
            "The credential changed after this page was loaded. Review the current version and retry.";
        public const string NotAccepted = "The change was not accepted.";
    }
}
