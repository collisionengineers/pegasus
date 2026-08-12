using System.Diagnostics;
using System.Security.Cryptography;

namespace Pegasus.Core.Intake;

public sealed class ProcessIntake(
    IIntakeSourceReader sourceReader,
    IIntakeReceiptStore receiptStore,
    IIntakeArtifactStore artifactStore,
    IInstructionExtractionPolicy extractionPolicy,
    IMailRoutePolicy mailRoutePolicy,
    IEnumerable<IMailClassificationPolicy> mailClassificationPolicies,
    EvaluateIntakeCaseMatch caseMatchEvaluator,
    TimeProvider timeProvider)
{
    private static readonly ActivitySource Telemetry = new("Pegasus.Core.Intake");

    public Task<IntakeReceipt> ExecuteAsync(
        IntakeSource source,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(source, retainedSourceStorageKey: null, replaceExisting: false, cancellationToken);

    internal Task<IntakeReceipt> ExecuteRetainedAsync(
        IntakeSource source,
        string retainedSourceStorageKey,
        bool replaceExisting = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(retainedSourceStorageKey);
        return ExecuteCoreAsync(source, retainedSourceStorageKey, replaceExisting, cancellationToken);
    }

    private async Task<IntakeReceipt> ExecuteCoreAsync(
        IntakeSource source,
        string? retainedSourceStorageKey,
        bool replaceExisting,
        CancellationToken cancellationToken)
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

            if (!replaceExisting)
            {
                activity?.SetTag("intake.reader_result", "not_read_replay");
                activity?.SetTag("intake.reader_key", existing.SourceReaderKey);
                RecordTelemetry(activity, existing, "replay", started);
                return existing with { IsDuplicate = true };
            }
        }

        IntakeAssetRecord sourceAsset;
        if (retainedSourceStorageKey is null)
        {
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
        }
        else
        {
            sourceAsset = new(
                Guid.NewGuid(),
                "uploaded source",
                safeSource.FileName,
                safeSource.MediaType,
                IntakeAssetKind.Source,
                IntakeAssetDisposition.Source,
                safeSource.Content.Length,
                sourceHash,
                retainedSourceStorageKey,
                null,
                null,
                null,
                null);
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
        var assessment = await AssessAsync(
            readResult,
            safeSource.SourceIdentity.Channel,
            processedAtUtc,
            cancellationToken);
        activity?.SetTag("intake.policy_key", assessment.ExtractionPolicyKey);
        activity?.SetTag("intake.policy_version", assessment.ExtractionPolicyVersion);
        activity?.SetTag(
            "intake.mail_route_disposition",
            assessment.MailRouteDecision?.Disposition.ToString());
        activity?.SetTag(
            "intake.mail_route_policy_key",
            assessment.MailRouteDecision?.PolicyKey);
        activity?.SetTag(
            "intake.mail_route_policy_version",
            assessment.MailRouteDecision?.PolicyVersion);
        activity?.SetTag(
            "intake.mail_classification_outcome",
            assessment.MailClassificationDecision?.Outcome.ToString());
        activity?.SetTag(
            "intake.mail_classification_policy_key",
            assessment.MailClassificationDecision?.PolicyKey);
        activity?.SetTag(
            "intake.mail_classification_policy_version",
            assessment.MailClassificationDecision?.PolicyVersion);
        activity?.SetTag(
            "intake.case_match_outcome",
            assessment.CaseMatchDecision?.Outcome.ToString());
        activity?.SetTag(
            "intake.case_match_policy_key",
            assessment.CaseMatchDecision?.PolicyKey);
        activity?.SetTag(
            "intake.case_match_policy_version",
            assessment.CaseMatchDecision?.PolicyVersion);
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
            readResult.ScannedPdfPages,
            assessment.MailRouteDecision,
            assessment.MailClassificationDecision,
            assessment.CaseMatchDecision);

        IntakeReceipt receipt;
        try
        {
            receipt = replaceExisting
                ? await receiptStore.ReplaceEvaluationAsync(draft, cancellationToken)
                : await receiptStore.StoreAsync(draft, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            RecordFailureTelemetry(activity, "persistence_failure", started);
            throw;
        }
        RecordTelemetry(activity, receipt, DecisionCode(receipt.Decision), started);
        return receipt;
    }

    private async Task<IntakeAssessment> AssessAsync(
        IntakeSourceReadResult readResult,
        IntakeSourceChannel sourceChannel,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken)
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
                null,
                null);
        }

        var mailRouteDecision = EvaluateMailRoute(readResult, sourceChannel);
        if (mailRouteDecision is not null
            && mailRouteDecision.Disposition != MailRouteDisposition.Accepted)
        {
            return new(
                IntakeDecision.NeedsSorting,
                mailRouteDecision.Reason,
                readerEvidence,
                [],
                null,
                [],
                null,
                null,
                null,
                null,
                mailRouteDecision);
        }

        var mailClassificationDecision = EvaluateMailClassification(readResult, mailRouteDecision);
        var caseMatchDecision = await caseMatchEvaluator.ExecuteAsync(
            readResult,
            mailRouteDecision,
            cancellationToken);
        var policyResult = extractionPolicy.Extract(readResult, processedAtUtc);
        EnsureConsistentPolicyResult(policyResult);
        var (decision, reason, failureCode, failureReason) = policyResult.Applicability switch
        {
            InstructionPolicyApplicability.Applicable => (
                IntakeDecision.CaseCreated,
                "A definitive instruction was identified and is eligible for case allocation.",
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
        if (caseMatchDecision is { Outcome: CaseMatchOutcome.Ambiguous }
            && decision == IntakeDecision.CaseCreated)
        {
            decision = IntakeDecision.NeedsSorting;
            reason = "Competing candidate cases match this message; the association requires manual sorting.";
        }

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
            policyResult.PolicyVersion,
            mailRouteDecision,
            mailClassificationDecision,
            caseMatchDecision);
    }

    private MailClassificationResult? EvaluateMailClassification(
        IntakeSourceReadResult readResult,
        MailRouteEvaluationResult? mailRouteDecision)
    {
        if (mailRouteDecision is not
            { Disposition: MailRouteDisposition.Accepted, SelectedRoute: { } route })
        {
            return null;
        }

        var policy = mailClassificationPolicies.SingleOrDefault(candidate =>
            string.Equals(
                candidate.WorkProviderCode,
                route.WorkProviderCode,
                StringComparison.Ordinal));
        if (policy is null)
        {
            return null;
        }

        var result = policy.Classify(readResult);
        EnsureConsistentClassificationResult(result);
        return result;
    }

    private static void EnsureConsistentClassificationResult(MailClassificationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Predicates);
        ArgumentNullException.ThrowIfNull(result.AmbiguousCandidates);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.PolicyKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(result.PolicyVersion);

        if (result.Predicates.Any(predicate =>
                string.IsNullOrWhiteSpace(predicate.Key)
                || string.IsNullOrWhiteSpace(predicate.Detail))
            || result.Predicates
                .Select(predicate => predicate.Key)
                .Distinct(StringComparer.Ordinal)
                .Count() != result.Predicates.Count)
        {
            throw new InvalidOperationException(
                "The mail-classification policy returned incomplete or duplicate predicate evidence.");
        }

        var consistent = result.Outcome switch
        {
            MailClassificationOutcome.Classified =>
                result.Category is not null && result.AmbiguousCandidates.Count == 0,
            MailClassificationOutcome.Ambiguous =>
                result.Category is null && result.AmbiguousCandidates.Count > 1,
            MailClassificationOutcome.Unclassified =>
                result.Category is null && result.AmbiguousCandidates.Count == 0,
            _ => false
        };
        if (!consistent)
        {
            throw new InvalidOperationException(
                "The mail-classification outcome is inconsistent with its category and candidate evidence.");
        }
    }

    private MailRouteEvaluationResult? EvaluateMailRoute(
        IntakeSourceReadResult readResult,
        IntakeSourceChannel sourceChannel)
    {
        if (sourceChannel != IntakeSourceChannel.Mailbox)
        {
            return null;
        }

        var result = mailRoutePolicy.Evaluate(readResult);
        EnsureConsistentMailRouteResult(result);
        return result;
    }

    private static void EnsureConsistentMailRouteResult(MailRouteEvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Predicates);
        ArgumentNullException.ThrowIfNull(result.TransportIdentities);
        ArgumentNullException.ThrowIfNull(result.OriginalIdentities);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(result.PolicyKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(result.PolicyVersion);

        if (result.Predicates.Any(predicate =>
                string.IsNullOrWhiteSpace(predicate.Key)
                || string.IsNullOrWhiteSpace(predicate.Detail))
            || result.Predicates
                .Select(predicate => predicate.Key)
                .Distinct(StringComparer.Ordinal)
                .Count() != result.Predicates.Count)
        {
            throw new InvalidOperationException(
                "The mail-route policy returned incomplete or duplicate predicate evidence.");
        }

        if (result.TransportIdentities
                .Concat(result.OriginalIdentities)
                .Any(identity =>
                    string.IsNullOrWhiteSpace(identity.Address)
                    || string.IsNullOrWhiteSpace(identity.SourceLabel)))
        {
            throw new InvalidOperationException(
                "The mail-route policy returned incomplete sender identity evidence.");
        }
        if (result.EffectiveSender is { } effectiveSender
            && (string.IsNullOrWhiteSpace(effectiveSender.Address)
                || string.IsNullOrWhiteSpace(effectiveSender.SourceLabel)))
        {
            throw new InvalidOperationException(
                "The mail-route policy returned an incomplete effective sender identity.");
        }


        if (result.Disposition == MailRouteDisposition.Accepted)
        {
            if (result.SelectedRoute is null || result.EffectiveSender is null)
            {
                throw new InvalidOperationException(
                    "An accepted mail route requires a selected route and effective sender.");
            }
            if (!result.TransportIdentities
                    .Concat(result.OriginalIdentities)
                    .Any(identity =>
                        string.Equals(
                            identity.Address,
                            result.EffectiveSender.Address,
                            StringComparison.OrdinalIgnoreCase)
                        && string.Equals(
                            identity.SourceLabel,
                            result.EffectiveSender.SourceLabel,
                            StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "The accepted mail-route effective sender is not present in its identity evidence.");
            }


            ArgumentException.ThrowIfNullOrWhiteSpace(result.SelectedRoute.RouteOwnerCode);
            ArgumentException.ThrowIfNullOrWhiteSpace(result.SelectedRoute.WorkProviderCode);
            if (!Enum.IsDefined(result.SelectedRoute.Kind))
            {
                throw new InvalidOperationException("The selected mail-route kind is not recognized.");
            }

            return;
        }

        if (result.SelectedRoute is not null)
        {
            throw new InvalidOperationException(
                "A mail route that was not accepted cannot contain a selected route.");
        }

        if (!Enum.IsDefined(result.Disposition))
        {
            throw new InvalidOperationException("The mail-route disposition is not recognized.");
        }
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
        IntakeSourceChannel.Automation => "automation",
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
        IntakeDecision.CaseCreated => "case_created",
        IntakeDecision.NeedsSorting => "needs_sorting",
        IntakeDecision.BlockedIntake => "blocked_intake",
        IntakeDecision.Unsupported => "unsupported",
        IntakeDecision.OcrRequired => "ocr_required",
        IntakeDecision.TechnicalFailure => "technical_failure",
        IntakeDecision.ImageIntakeRegistered => "image_intake_registered",
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
        int? ExtractionPolicyVersion,
        MailRouteEvaluationResult? MailRouteDecision,
        MailClassificationResult? MailClassificationDecision = null,
        CaseMatchEvaluationResult? CaseMatchDecision = null)
    {
        public static IntakeAssessment Failure(
            IntakeDecision decision,
            string decisionReason,
            string failureCode,
            string failureReason,
            IReadOnlyList<IntakeEvidence> evidence) =>
            new(decision, decisionReason, evidence, [], null, [], failureCode, failureReason, null, null, null);
    }
}
