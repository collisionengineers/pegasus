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

    // Presentation membership only; types, allowed codes and authority remain
    // owned by AssessmentVocabulary and the workspace command.
    public static class Editors
    {
        public static IReadOnlyDictionary<string, string> Settlement { get; } = new Dictionary<string, string>
        {
            [AssessmentVocabulary.Outcome] = "Outcome",
            [AssessmentVocabulary.SalvageCategory] = "Salvage category",
            [AssessmentVocabulary.SalvageValue] = "Salvage value",
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

        public static string FormName(string path) => $"assessmentFields[{path}]";

        public static string? Label(string field)
        {
            if (field == FormName(AssessmentVocabulary.HistoryCheck)) return "Vehicle history";
            foreach (var entry in Settlement.Concat(Report).Concat(Damage))
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
            || path == AssessmentVocabulary.HistoryCheck;
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
    /// The Valuation section's source-card surface, same ownership rule as
    /// Vehicle.
    /// </summary>
    public static class Valuation
    {
        public const string SectionTitle = "Valuation";
        public const string AddValuation = "Add valuation";
        public const string CazanaCondition = "not a live source";
        public const string AbsentGuideMonth = "Not recorded";

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
    /// The Files section's upload-request dialog (PR 670 port): who the
    /// request goes to, why, and the accepted limits as values.
    /// </summary>
    public static class UploadRequest
    {
        public const string Create = "Create upload request";
        public const string Recipient = "Recipient";
        public const string Reason = "Reason";
        public const string Lifetime = "Lifetime";
        public const string Files = "Files";
        public const string FileSize = "File size";

        public static string Days(TimeSpan lifetime) =>
            lifetime.TotalDays == 1 ? "1 day" : $"{lifetime.TotalDays:0.##} days";
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
        /// never be blindly repeated (ENG-024), so the copy cannot invite a
        /// retry.
        /// </summary>
        public const string SendUnknown = "The send result is not yet known.";
        public const string SendFailed = "The report send failed.";
    }

    /// <summary>
    /// The Estimate section's Glass's surface: the control that opens the
    /// provider's estimator, the Engineer's own session for this Case, and the
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
        /// retry, the same rule the report send follows (ENG-024).
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
