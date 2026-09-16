using Pegasus.Core.Cases;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Intake.Unidentified;

/// <summary>
/// The one primary operator action for an Unidentified item. The facts record
/// is deliberately small so list projections can use this policy without
/// loading the detail page's readings, Triage, or image-intake records.
/// </summary>
public enum UnidentifiedNextStep
{
    None,
    AddOriginalReport,
    AuditReportCustodyPending,
    AuditReportCustodyFailed,
    AuditReportProcessing,
    CreateCase,
    LinkToCase,
    RegisterImages,
    OpenTriage,
    ReviewGroup,
    RequestAgain,
    OpenSource
}

/// <summary>
/// Current facts that can change which action is safe to make primary for an
/// Unidentified item. They are values already decided by their owning
/// policies; this policy only orders the actions.
/// </summary>
public sealed record UnidentifiedNextStepFacts(
    UnidentifiedState State,
    UnidentifiedReasonCode ReasonCode,
    bool CanCreateCase,
    bool CanRegisterImages,
    bool CanOpenTriage,
    bool CanLinkCase,
    IncomingArtifactCustodyState? SuppliedOriginalReportCustodyState,
    bool IsReevaluationPending,
    bool HasManualUploadGroup,
    bool HasSourceMessage,
    bool HasSourceFile);

public static class UnidentifiedNextStepPolicy
{
    /// <summary>
    /// Selects one primary action from the current, already-authorised facts.
    /// A supplied Audit report remains a processing state only while its
    /// re-evaluation is actually pending. A later terminal failure therefore
    /// cannot strand the item behind a permanently pending presentation.
    /// </summary>
    public static UnidentifiedNextStep Primary(UnidentifiedNextStepFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.State != UnidentifiedState.Open)
        {
            return UnidentifiedNextStep.None;
        }

        if (facts.SuppliedOriginalReportCustodyState == IncomingArtifactCustodyState.Failed)
        {
            return UnidentifiedNextStep.AuditReportCustodyFailed;
        }

        if (facts.SuppliedOriginalReportCustodyState is not null
            && facts.SuppliedOriginalReportCustodyState != IncomingArtifactCustodyState.Confirmed)
        {
            return UnidentifiedNextStep.AuditReportCustodyPending;
        }

        if (facts.SuppliedOriginalReportCustodyState == IncomingArtifactCustodyState.Confirmed
            && facts.IsReevaluationPending)
        {
            return UnidentifiedNextStep.AuditReportProcessing;
        }

        if (facts.ReasonCode == UnidentifiedReasonCode.AuditOriginalReportMissing)
        {
            if (facts.SuppliedOriginalReportCustodyState is null)
            {
                return UnidentifiedNextStep.AddOriginalReport;
            }
        }

        if (facts.ReasonCode == UnidentifiedReasonCode.CouldNotBeRead)
        {
            return facts.HasSourceMessage
                ? UnidentifiedNextStep.RequestAgain
                : facts.HasSourceFile
                    ? UnidentifiedNextStep.OpenSource
                    : UnidentifiedNextStep.None;
        }

        if (facts.CanCreateCase)
        {
            return UnidentifiedNextStep.CreateCase;
        }

        if (facts.CanRegisterImages)
        {
            return UnidentifiedNextStep.RegisterImages;
        }

        if (facts.CanOpenTriage)
        {
            return UnidentifiedNextStep.OpenTriage;
        }

        if (facts.CanLinkCase)
        {
            return UnidentifiedNextStep.LinkToCase;
        }

        if (facts.HasManualUploadGroup)
        {
            return UnidentifiedNextStep.ReviewGroup;
        }

        return facts.HasSourceMessage
            ? UnidentifiedNextStep.RequestAgain
            : facts.HasSourceFile
                ? UnidentifiedNextStep.OpenSource
                : UnidentifiedNextStep.None;
    }

    /// <summary>Builds the batchable facts from the detail page's richer context.</summary>
    public static UnidentifiedNextStepFacts FactsFrom(UnidentifiedItemContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var receipt = context.Receipt;
        return new(
            context.Item.State,
            context.Item.ReasonCode,
            CanCreateCase(context.Item, receipt),
            context.CanRegisterImages,
            context.CanOpenTriage,
            receipt is not null && IntakeAssociationDestinationPolicy.CanOffer(receipt),
            SuppliedOriginalReportCustodyState(receipt),
            receipt is { Decision: IntakeDecision.BlockedIntake, FailureCode: "reevaluation_pending" },
            context.IsManualUploadGroup,
            context.SourceMessageId is not null,
            context.SourceAssetId is not null);
    }

    /// <summary>
    /// The existing case-acceptance eligibility, including the Audit evidence
    /// gate. A report-less Audit never reaches Create Case merely because its
    /// receipt still says NeedsSorting.
    /// </summary>
    public static bool CanCreateCase(UnidentifiedItem item, IntakeReceipt? receipt) =>
        CanReviewForCaseCreation(item, receipt)
        && receipt!.Decision == IntakeDecision.CaseCreated;

    /// <summary>
    /// Review and correction can remain a secondary action on incomplete
    /// material. Only a definitive proposal makes Create Case primary.
    /// </summary>
    public static bool CanReviewForCaseCreation(UnidentifiedItem item, IntakeReceipt? receipt) =>
        item.State == UnidentifiedState.Open
        && receipt is not null
        && receipt.CurrentCaseId is null
        && receipt.AllocationState is null
        && (IntakeDecisionPolicy.CanBecomeCase(receipt.Decision) || receipt.Decision == IntakeDecision.OcrRequired)
        && receipt.MailClassificationDecision is not
            { CaseType: CaseType.Audit, StandaloneAuditReport: null }
        && CanUseSuppliedOriginalReportForCaseCreation(receipt);

    /// <summary>
    /// The custody state of the one staff-supplied Audit report, or null when
    /// the receipt has none. A non-confirmed report cannot determine a Case.
    /// </summary>
    public static IncomingArtifactCustodyState? SuppliedOriginalReportCustodyState(IntakeReceipt? receipt) =>
        receipt?.AssetRecords.SingleOrDefault(asset =>
            asset.Disposition == IntakeAssetDisposition.SuppliedOriginalReport)?.CustodyState;

    /// <summary>
    /// A supplied report is usable only after its custody is confirmed. A
    /// receipt without such a report follows its existing evidence path.
    /// </summary>
    public static bool CanUseSuppliedOriginalReportForCaseCreation(IntakeReceipt? receipt) =>
        SuppliedOriginalReportCustodyState(receipt) is not { } custody
        || custody == IncomingArtifactCustodyState.Confirmed;

    /// <summary>The existing image-intake eligibility, exposed for list projections.</summary>
    public static bool CanRegisterImages(
        UnidentifiedItem item,
        IntakeReceipt? receipt,
        bool hasImageIntake) =>
        item.State == UnidentifiedState.Open
        && receipt is not null
        && !hasImageIntake
        && receipt.Decision == IntakeDecision.NeedsSorting
        && ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt);

    /// <summary>The existing Triage eligibility, exposed for list projections.</summary>
    public static bool CanOpenTriage(
        UnidentifiedItem item,
        IntakeReceipt? receipt,
        bool hasTriage) =>
        item.State == UnidentifiedState.Open
        && receipt is not null
        && !hasTriage
        && receipt.Decision == IntakeDecision.NeedsSorting
        && receipt.Evidence.Count(evidence =>
            evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch) == 1;
}
