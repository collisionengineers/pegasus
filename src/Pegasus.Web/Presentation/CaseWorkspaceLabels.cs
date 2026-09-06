using System.Globalization;
using Pegasus.Core.Assessment;
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
    /// <summary>
    /// The Vehicle section's lookup-chip surface. These live here, not in the
    /// shared OperatorLabels, because they are Case-only: the shared file is
    /// Stream C's and B never edits it.
    /// </summary>
    public static class Vehicle
    {
        public const string LookupDvlaMot = "Look up DVLA & MOT";
        public const string LookupMileage = "MOT mileage";

        public static string UseSuggestion(string value) => $"Use {value}";
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
    /// The report-image preparation surface (B06): the Files section's
    /// per-image controls and the Report section's prepared cards read every
    /// name from here, so the two sections cannot label the same preparation
    /// two different ways.
    /// </summary>
    public static class ReportImages
    {
        public const string SectionTitle = "Report images";
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
            "The artifact is awaiting custody confirmation.";
        public const string GenerationNotReady = "Report not ready";
        public const string CurrentGeneration = "Generated";
        public const string GenerationState = "State";
        public const string DownloadReport = "Report";
        public const string DownloadFeeNote = "Fee note";
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
}
