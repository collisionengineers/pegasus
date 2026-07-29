using System.Diagnostics;
using System.Security.Cryptography;

namespace Pegasus.Core.Intake;

public sealed class ProcessIntake(
    IIntakeSourceReader sourceReader,
    IIntakeReceiptStore receiptStore,
    IIntakeArtifactStore artifactStore,
    IInstructionExtractionPolicy extractionPolicy,
    TimeProvider timeProvider)
{
    private static readonly ActivitySource Telemetry = new("Pegasus.Core.Intake");

    public async Task<IntakeReceipt> ExecuteAsync(
        IntakeSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceIdentity.ExternalReceiptToken);

        using var activity = Telemetry.StartActivity("process_intake");
        activity?.SetTag("intake.source_channel", ChannelCode(source.SourceIdentity.Channel));
        var started = timeProvider.GetTimestamp();

        var safeSource = source with { FileName = Path.GetFileName(source.FileName) };
        var sourceHash = Convert.ToHexString(SHA256.HashData(source.Content.Span));
        var existing = await receiptStore.FindBySourceIdentityAsync(
            source.SourceIdentity,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SourceHash, sourceHash, StringComparison.Ordinal))
            {
                throw new IntakeSourceIdentityConflictException();
            }

            activity?.SetTag("intake.reader_result", "not_read_replay");
            activity?.SetTag("intake.reader_key", existing.SourceReaderKey);
            RecordTelemetry(activity, existing, "replay", started);
            return existing with { IsDuplicate = true };
        }

        IntakeAssetRecord sourceAsset;
        try
        {
            sourceAsset = await RetainAsync(
                new(
                    "uploaded source",
                    safeSource.FileName,
                    safeSource.MediaType,
                    safeSource.Content,
                    IntakeAssetKind.Source,
                    IntakeAssetDisposition.Source),
                cancellationToken);
        }
        catch (IntakeArtifactRetentionException)
        {
            activity?.SetTag("intake.reader_result", "not_run_retention_failure");
            RecordFailureTelemetry(activity, "artifact_retention_failure", started);
            throw;
        }

        IntakeSourceReadResult readResult;
        try
        {
            readResult = await sourceReader.ReadAsync(safeSource, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            readResult = new(
                IntakeSourceReadStatus.TechnicalFailure,
                [],
                [],
                [],
                false,
                "source_reader_failure",
                "The uploaded source could not be read because of a technical failure.",
                ReaderKey: "intake_source_reader",
                ReaderVersion: "1");
        }

        activity?.SetTag("intake.reader_result", ReadStatusCode(readResult.Status));
        activity?.SetTag("intake.reader_key", readResult.ReaderKey);

        var assets = new List<IntakeAssetRecord> { sourceAsset };
        try
        {
            foreach (var candidate in readResult.AssetCandidates)
            {
                assets.Add(await RetainAsync(candidate, cancellationToken));
            }
        }
        catch (IntakeArtifactRetentionException)
        {
            RecordFailureTelemetry(activity, "artifact_retention_failure", started);
            throw;
        }

        var processedAtUtc = timeProvider.GetUtcNow();
        var assessment = Assess(readResult, safeSource.SourceIdentity.Channel, processedAtUtc);
        activity?.SetTag("intake.policy_key", assessment.ExtractionPolicyKey);
        activity?.SetTag("intake.policy_version", assessment.ExtractionPolicyVersion);
        var draft = new IntakeReceiptDraft(
            safeSource.FileName,
            safeSource.MediaType,
            safeSource.Content.Length,
            sourceHash,
            safeSource.SourceIdentity,
            safeSource.ReceivedAtUtc,
            processedAtUtc,
            safeSource.Actor,
            assessment.Decision,
            assessment.DecisionReason,
            assessment.Evidence,
            assessment.Fields,
            assessment.InstructionDraft,
            assessment.MissingFields,
            assessment.FailureCode,
            assessment.FailureReason,
            readResult.ReaderKey,
            readResult.ReaderVersion,
            assessment.ExtractionPolicyKey,
            assessment.ExtractionPolicyVersion,
            assets,
            readResult.ScannedPdfPages);

        IntakeReceipt receipt;
        try
        {
            receipt = await receiptStore.StoreAsync(draft, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            RecordFailureTelemetry(activity, "persistence_failure", started);
            throw;
        }
        RecordTelemetry(activity, receipt, DecisionCode(receipt.Decision), started);
        return receipt;
    }

    private IntakeAssessment Assess(
        IntakeSourceReadResult readResult,
        IntakeSourceChannel sourceChannel,
        DateTimeOffset processedAtUtc)
    {
        var readerEvidence = readResult.Issues
            .Select(issue => new IntakeEvidence(
                issue.Source,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.Information,
                issue.Code,
                issue.Reason))
            .ToArray();

        if (readResult.Status == IntakeSourceReadStatus.Unsupported)
        {
            return IntakeAssessment.Failure(
                IntakeDecision.Unsupported,
                "The uploaded source is not readable as a supported email, document, PDF or image.",
                readResult.FailureCode ?? "unsupported_source",
                readResult.FailureReason ?? "The file is unsupported or corrupt.",
                readerEvidence);
        }

        if (readResult.Status == IntakeSourceReadStatus.TechnicalFailure)
        {
            return IntakeAssessment.Failure(
                IntakeDecision.TechnicalFailure,
                "The uploaded source could not be assessed because of a technical failure.",
                readResult.FailureCode ?? "technical_failure",
                readResult.FailureReason ?? "The source could not be processed at this time.",
                readerEvidence);
        }

        if (readResult.IsIncomplete)
        {
            return new(
                IntakeDecision.NeedsSorting,
                "The source was retained, but processing could not be completed safely and requires manual sorting.",
                readerEvidence,
                [],
                null,
                [],
                null,
                null,
                null,
                null);
        }

        var policyResult = extractionPolicy.Extract(readResult, processedAtUtc);
        policyResult = ApplyMailRoutePolicy(readResult, sourceChannel, policyResult);
        EnsureConsistentPolicyResult(policyResult);
        var (decision, reason, failureCode, failureReason) = policyResult.Applicability switch
        {
            InstructionPolicyApplicability.Applicable => (
                IntakeDecision.DraftReady,
                "A reviewable instruction draft was extracted. This does not create or classify a case.",
                null,
                null),
            InstructionPolicyApplicability.Indeterminate when readResult.RequiresOcr => (
                IntakeDecision.OcrRequired,
                "Readable content is insufficient to decide which principal instruction policy applies.",
                "ocr_required",
                "The PDF appears to contain scanned pages without enough embedded text for review."),
            InstructionPolicyApplicability.NotApplicable or InstructionPolicyApplicability.Indeterminate => (
                IntakeDecision.NeedsSorting,
                "The readable content does not provide enough evidence to suggest a principal.",
                null,
                null),
            _ => throw new InvalidOperationException(
                $"Unknown instruction policy applicability value '{(int)policyResult.Applicability}'.")
        };
        return new(
            decision,
            reason,
            [.. readerEvidence, .. policyResult.Evidence],
            policyResult.Fields,
            policyResult.InstructionDraft,
            policyResult.MissingFields,
            failureCode,
            failureReason,
            policyResult.PolicyKey,
            policyResult.PolicyVersion);
    }

    private InstructionExtractionResult ApplyMailRoutePolicy(
        IntakeSourceReadResult readResult,
        IntakeSourceChannel sourceChannel,
        InstructionExtractionResult policyResult)
    {
        if (sourceChannel != IntakeSourceChannel.Mailbox
            || policyResult.Applicability != InstructionPolicyApplicability.Applicable)
        {
            return policyResult;
        }

        var route = (extractionPolicy as IMailRoutePolicy)?.Evaluate(readResult);
        return route is { Disposition: MailRouteDisposition.Accepted, SelectedRoute: not null }
            ? policyResult
            : policyResult with
            {
                Applicability = InstructionPolicyApplicability.Indeterminate,
                InstructionDraft = null
            };
    }

    private static void EnsureConsistentPolicyResult(InstructionExtractionResult policyResult)
    {
        if (policyResult.Applicability == InstructionPolicyApplicability.Applicable
            && policyResult.InstructionDraft is null)
        {
            throw new InvalidOperationException(
                "The instruction extraction policy returned Applicable without an instruction draft.");
        }

        if (policyResult.Applicability is InstructionPolicyApplicability.NotApplicable
                or InstructionPolicyApplicability.Indeterminate
            && policyResult.InstructionDraft is not null)
        {
            throw new InvalidOperationException(
                $"The instruction extraction policy returned {policyResult.Applicability} with an instruction draft.");
        }
    }

    private async Task<IntakeAssetRecord> RetainAsync(
        IntakeAssetCandidate candidate,
        CancellationToken cancellationToken)
    {
        var contentHash = Convert.ToHexString(SHA256.HashData(candidate.Content.Span));
        string storageKey;
        try
        {
            storageKey = await artifactStore.StoreAsync(contentHash, candidate.Content, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            throw new IntakeArtifactRetentionException(exception);
        }

        return new(
            Guid.NewGuid(),
            candidate.SourceLabel,
            Path.GetFileName(candidate.FileName),
            candidate.MediaType,
            candidate.Kind,
            candidate.Disposition,
            candidate.Content.Length,
            contentHash,
            storageKey,
            candidate.PageNumber,
            candidate.Bounds,
            candidate.WidthPixels,
            candidate.HeightPixels);
    }

    private void RecordTelemetry(
        Activity? activity,
        IntakeReceipt receipt,
        string outcome,
        long started)
    {
        activity?.SetTag("intake.receipt_id", receipt.Id);
        activity?.SetTag("intake.policy_key", receipt.ExtractionPolicyKey);
        activity?.SetTag("intake.policy_version", receipt.ExtractionPolicyVersion);
        activity?.SetTag("intake.outcome", outcome);
        activity?.SetTag(
            "intake.duration_ms",
            timeProvider.GetElapsedTime(started, timeProvider.GetTimestamp()).TotalMilliseconds);
    }

    private void RecordFailureTelemetry(Activity? activity, string failureCategory, long started)
    {
        activity?.SetTag("intake.outcome", "technical_error");
        activity?.SetTag("intake.failure_category", failureCategory);
        activity?.SetTag(
            "intake.duration_ms",
            timeProvider.GetElapsedTime(started, timeProvider.GetTimestamp()).TotalMilliseconds);
    }

    private static string ChannelCode(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        _ => throw new InvalidOperationException($"Unknown intake source channel value '{(int)channel}'.")
    };

    private static string ReadStatusCode(IntakeSourceReadStatus status) => status switch
    {
        IntakeSourceReadStatus.Readable => "readable",
        IntakeSourceReadStatus.Unsupported => "unsupported",
        IntakeSourceReadStatus.TechnicalFailure => "technical_failure",
        _ => throw new InvalidOperationException($"Unknown intake reader result value '{(int)status}'.")
    };

    private static string DecisionCode(IntakeDecision decision) => decision switch
    {
        IntakeDecision.DraftReady => "draft_ready",
        IntakeDecision.NeedsSorting => "needs_sorting",
        IntakeDecision.Unsupported => "unsupported",
        IntakeDecision.OcrRequired => "ocr_required",
        IntakeDecision.TechnicalFailure => "technical_failure",
        _ => throw new InvalidOperationException($"Unknown intake decision value '{(int)decision}'.")
    };

    private sealed record IntakeAssessment(
        IntakeDecision Decision,
        string DecisionReason,
        IReadOnlyList<IntakeEvidence> Evidence,
        IReadOnlyList<InstructionReviewField> Fields,
        InstructionDraft? InstructionDraft,
        IReadOnlyList<string> MissingFields,
        string? FailureCode,
        string? FailureReason,
        string? ExtractionPolicyKey,
        int? ExtractionPolicyVersion)
    {
        public static IntakeAssessment Failure(
            IntakeDecision decision,
            string decisionReason,
            string failureCode,
            string failureReason,
            IReadOnlyList<IntakeEvidence> evidence) =>
            new(decision, decisionReason, evidence, [], null, [], failureCode, failureReason, null, null);
    }
}
