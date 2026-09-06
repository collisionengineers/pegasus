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
