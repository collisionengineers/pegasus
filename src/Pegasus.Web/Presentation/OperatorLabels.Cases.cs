using System.Globalization;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Web.Presentation;

public static partial class OperatorLabels
{
    /// <summary>Management Reports' Case list (MI-04) and its presets — one list.</summary>
    public static class CaseList
    {
        public const string Title = "Case list";
        public const string ReceivedFrom = "Received from";
        public const string ReceivedTo = "Received to";
        public const string AllTime = "All time";
        public const string IncludeTriage = "Include Triage Cases";
        public const string Preset = "Preset";
        public const string NoPreset = "No preset";
        public const string UsePreset = "Use preset";
        public const string PresetName = "Preset name";
        public const string SaveAsNewPreset = "Save as new preset";
        public const string SavePreset = "Save preset";
        public const string RemovePreset = "Remove preset";
        public const string PresetsUnavailable = "Presets are unavailable.";
        public const string Unavailable = "The Case list is unavailable.";
        public const string ChooseColumns = "Choose at least one column.";
        public const string ChoosePeriod = "Choose both received dates, the first no later than the second, or All time.";
        public const string PresetCreated = "The preset was saved.";
        public const string PresetSaved = "The preset was updated.";
        public const string PresetRemoved = "The preset was removed.";
        public const string PresetNameRequired = "Enter a preset name of up to 100 characters.";
        public const string PresetDuplicateName = "Another preset already has that name.";
        public const string PresetNotFound = "That preset no longer exists.";
        public const string PresetStale = "The preset changed after this page was loaded. Choose it again and retry.";
        public const string PresetExpired = "The form has expired. Retry the operation.";
        public const string PresetNotAccepted = "The preset change was not accepted.";

        public static string Group(Pegasus.Core.Reports.CaseListColumnGroup group) => group switch
        {
            Pegasus.Core.Reports.CaseListColumnGroup.Case => "Case",
            Pegasus.Core.Reports.CaseListColumnGroup.Outcomes => "Outcomes",
            Pegasus.Core.Reports.CaseListColumnGroup.Engineers => "Engineers",
            Pegasus.Core.Reports.CaseListColumnGroup.ClaimAndVehicle => "Claim and vehicle",
            Pegasus.Core.Reports.CaseListColumnGroup.Money => "Money",
            Pegasus.Core.Reports.CaseListColumnGroup.PartiesAndActivity => "Parties and activity",
            _ => Humanise(group.ToString())
        };
    }

    /// <summary>
    /// The Cases queue's footer line (v41 U3, signed off 19 October 2026): the
    /// page number shows only when another page exists.
    /// </summary>
    public static class CaseQueueFooter
    {
        public static string PageLine(int page, bool hasOtherPage) =>
            hasOtherPage ? string.Create(CultureInfo.InvariantCulture, $"Page {page} · newest first") : "Newest first";
    }

    /// <summary>
    /// Search's filter and results words that v41 W2 and W3 gave (signed off
    /// 19 October 2026): the stage filter's name, the two lists' empty choice,
    /// the results' Type column and the preview's copy button.
    /// </summary>
    public static class Search
    {
        public const string CaseStage = "Case stage";
        public const string AllPrincipals = "All principals";
        public const string AllEngineers = "All engineers";
        public const string TypeColumn = "Type";
        public const string CopyCaseReference = "Copy Case/PO";
    }

    /// <summary>Management Reports' tabs (v41 A26); the Case list tab reads <see cref="CaseList.Title"/>.</summary>
    public static class ReportTabs
    {
        public const string Engineers = "Engineers";
        public const string Principals = "Principals";
        public const string Queues = "Queues";
    }

    /// <summary>
    /// Management Reports' own words, the ones no CSV or workbook carries; the
    /// headings and column titles the files share are
    /// <see cref="Pegasus.Core.Reports.AdministrationReportHeadings"/>.
    /// </summary>
    public static class Reports
    {
        public const string Period = "Period";
        public const string From = "From";
        public const string To = "To";
        public const string Apply = "Apply";
        public const string InvalidPeriod = "Choose a valid date range.";
        public const string ThisMonth = "This month";
        public const string LastMonth = "Last month";
        public const string ThisQuarter = "This quarter";
        public const string Last12Months = "Last 12 months";
        public const string Custom = "Custom";

        public const string Work = "Work";

        /// <summary>The Work choice for every report; Inspection and Audit read <see cref="OperatorLabels.CaseTypeName(CaseType)"/>.</summary>
        public const string AllWork = "All";

        public const string Person = "Person";
        public const string AllPeople = "All people";
        public const string DownloadCsv = "Download CSV";
        public const string DownloadWorkbook = "Download workbook";
        public const string PreviousPeriod = "Previous period";
        public const string Unavailable = "Unavailable";
        public const string Now = "Now";
        public const string EngineerActivityTotals = "Engineer activity totals";
        public const string ReportsByPrincipalTotals = "Reports by Principal totals";
        public const string QueuesTotals = "Queues totals";
        public const string CasesCurrentlyHeld = "Cases currently held";
        public const string Unidentified = "Unidentified";
        public const string OldestSince = "Oldest since";
        public const string NoEngineerActivity = "No engineer activity was recorded for this period.";
        public const string NoReports = "No reports were recorded for this period.";
        public const string NoTurnaround = "No holding or turnaround activity was recorded for this period.";
        public const string NothingHeld = "Nothing is currently held.";
    }

    /// <summary>
    /// The Case workspace's Vehicle, Inspection address and Case Files
    /// sections — one list. Appended inside its own nested class; no member
    /// above is reordered or edited.
    /// </summary>
    public static class CaseWorkspace
    {
        // Read-only query correspondence table.
        public const string Received = "Received";
        public const string Sender = "Sender";
        public const string Subject = "Subject";
        public const string Classification = "Classification";
        public const string Type = "Type";
        public const string OpenMessage = "Open message";

        public const string VehicleFactsPanel = "Vehicle";

        /// <summary>
        /// Why the Experian control is drawn disabled (operator decisions
        /// D7/D22). Always supplied: <c>.gated::after</c> renders
        /// <c>attr(data-condition)</c> unguarded, so a <c>.gated</c> span
        /// without one paints an empty pill.
        /// </summary>
        public const string ExperianSeamCondition = "Experian is not connected";

        public const string InspectionAddressPanel = "Inspection address";

        /// <summary>
        /// The Principal's own inspection-address setting, named for the party
        /// the operator knows it by. Shown only where the Case holds something
        /// else, so the row is never a second printing of the same address.
        /// </summary>
        public const string PrincipalDefaultInspectionAddress = "Principal default";
        // Inspect-at choices and storage-location labels.
        public const string InspectAt = "Inspect at";
        public const string Source = "Source";
        public const string ImageBasedAssessment = "Image Based Assessment";
        public const string ClaimantAddress = "Claimant address";
        public const string RepairerLocation = "Repairer location";
        public const string StorageLocation = "Storage location";
        public const string PreviousAddress = "Previous";
        public const string ManualEntry = "Manual entry";
        public const string NotRecorded = "Not recorded";
        // The Case's repairer.
        public const string Repairer = "Repairer";
        public const string RepairerDirectory = "Repairer contact";
        public const string NotLinked = "Not linked";
        // End of the Inspect-at and repairer labels.
        public const string FilesPanel = "Files";
        public const string AddEvidence = "Add evidence";
        public const string SaveAs = "Save as";

        /// <summary>One section of the Case record, as the jump-nav names it.</summary>
        public sealed record CaseSection(string Key, string Label, string Icon);

        /// <summary>
        /// The Case record sections in their fixed order (D30,
        /// FRD-12 §Case workspace). One list: the page model's accepted
        /// <c>?section=</c> vocabulary, the jump-nav, the section hosts and
        /// their headings all read it, so no second section list exists in
        /// Razor, CSS or script.
        /// </summary>
        public static readonly IReadOnlyList<CaseSection> Sections =
        [
            new("overview", "Case details", "icon-layout-dashboard"),
            new("claim", "Claim", "icon-clipboard-list"),
            new("original-report", "Original report", "icon-file-text"),
            new("inspection", "Inspection details", "icon-map-pin"),
            new("vehicle", "Vehicle", "icon-car"),
            new("damage", "Damage", "icon-alert-triangle"),
            new("valuation", "Valuation", "icon-file-text"),
            new("estimate", "Repair Spec", "icon-list"),
            new("settlement", "Decisions", "icon-check-circle"),
            new("report", "Report", "icon-file"),
            new("files", "Files", "icon-folder"),
            new("notes", "Notes", "icon-history"),
            new("queries", "Queries", "icon-reply")
        ];

        /// <summary>The section a <c>?section=</c> value the record does not own selects.</summary>
        public const string DefaultSectionKey = "overview";

        public const string SectionNav = "Case sections";

        // The identity ribbon the frame itself renders (D29, D31).
        public const string RibbonRegistration = "Registration";
        public const string RibbonClaimant = "Claimant";
        public const string RibbonPrincipal = "Principal";
        public const string RibbonState = "State";
        public const string RibbonEngineer = "Engineer";

        /// <summary>
        /// What the record prints where a value it would show is not held.
        /// </summary>
        public const string AbsentValue = "Not recorded";

        public const string SignOffEngineer = "Sign-off Engineer";
        public const string Unassigned = "Unassigned";
        public const string ReasonForAction = "Reason for action";

        /// <summary>The reason dialog's confirming button.</summary>
        public const string ConfirmAction = "Confirm Action";

        // Review point 12: the adverse disposition, named apart from the
        // progression actions it must never sit among.
        public const string CloseCase = "Close case";
        public const string ClosureOutcome = "Outcome";
        // end review point 12

        // C08 labels batch: Stream B's documents/chase port.
        public const string Reason = "Reason";
        // end C08 labels batch

        // The Engineer sections moved from the retired Assessment
        // page. Keep this block together so the parallel Case lanes can merge
        // their own vocabulary without interleaving it.
        public static class EngineerSections
        {
            public const string Damage = "Damage";
            public const string ImpactLocation = "Impact location";
            public const string ImpactSeverity = "Impact severity";
            public const string IncidentNarrative = "Incident narrative";
            public const string Estimate = "Repair Spec";
            public const string Estimates = "Repair specs";
            public const string NoEstimatesRecorded = "No repair specs recorded";
            public const string NewEstimate = "New repair spec";
            public const string Current = "Current";
            public const string Duplicate = "Duplicate";
            public const string Compare = "Compare";
            public const string CompareEstimates = "Compare repair specs";
            public const string State = "State";
            public const string UseEstimate = "Use repair spec";
            public const string AddLine = "Add line";
            public const string DeleteAllLines = "Delete all lines";
            public const string LinesWord = "lines";
            public const string EstimateName = "Repair spec name";
            public const string Source = "Source";
            public const string LabourRate = "Labour rate";
            public const string LabourRatePerHour = "Labour rate (\u00a3/h)";
            public const string RegionalUplift = "Regional uplift";
            public const string MaterialPounds = "Material (\u00a3)";
            public const string OtherCostsPounds = "Other costs (\u00a3)";
            public const string Vat = "VAT";
            public const string VatPercent = "VAT %";
            public const string Operation = "Operation";
            public const string Description = "Description";
            public const string PartNumber = "Part number";
            public const string Quantity = "Quantity";
            public const string QuantityShort = "Qty";
            public const string LabourHours = "Labour hours";
            public const string PaintHours = "Paint hours";
            public const string PaintHoursShort = "Paint h";
            public const string PartAmount = "Part amount";
            public const string Action = "Action";
            public const string NoneRecorded = "None recorded";
            public const string ImportEstimate = "Import estimate";
            public const string Other = "Other";
            public const string Reason = "Reason";
            public const string Cancel = "Cancel";
            public const string CloseDialog = "Close dialog";
            public const string Replace = "Replace";
            public const string Repair = "Repair";
            public const string RemoveAndRefit = "R&I";
            public const string PaintOperation = "Paint";

            public static string EstimateState(RepairSpecificationState state) => state switch
            {
                RepairSpecificationState.Draft => "Draft",
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
            };

            public const string Settlement = "Decisions";
            public const string RecoveryCharge = "Recovery charge";

            public const string Report = "Report";
            public const string StatementOfTruth = "Statement of truth";

            public const string Direction = "Direction";
            public const string TargetEstimate = "Target Estimate";
            public const string CaseValuation = "Case Valuation";
            public const string TargetAmount = "Target amount";

            public const string Parts = "Parts";
            public const string Type = "Type";
            public const string ToBeConfirmed = "To be confirmed";

            public const string ReadOnlyOnceComplete = "Read-only once Complete";
            public const string SendingToAiDisabled = "Sending to AI is disabled by an Administrator";
            public const string EngineerValueRequired = "An Engineer's Value is required";
            public const string NotAvailableForCase = "Not available for this case";
            public const string NotReady = "Not ready";

            public static string LineField(string label, int line) => $"{label}, line {line}";

            public static string RemoveLine(int line) => $"Remove line {line}";
        }
        // End of the Engineer sections' labels.
    }
}
