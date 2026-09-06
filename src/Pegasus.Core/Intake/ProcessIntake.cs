using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Core.Intake;

public sealed class ProcessIntake(
    IIntakeSourceReader sourceReader,
    IIntakeReceiptStore receiptStore,
    IIntakeArtifactStore artifactStore,
    IInstructionExtractionPolicy extractionPolicy,
    IMailRoutePolicy mailRoutePolicy,
    IEnumerable<IMailClassificationPolicy> mailClassificationPolicies,
    EvaluateIntakeCaseMatch caseMatchEvaluator,
    TimeProvider timeProvider,
    IRecordAutomaticStandaloneAuditEvidence? automaticStandaloneAuditEvidence = null,
    IRegisterUnidentified? registerUnidentified = null,
    IProviderSubmissionBindings? providerSubmissionBindings = null,
    IRetainedInstructionAnalysisStore? retainedInstructionAnalysisStore = null)
{
    private static readonly ActivitySource Telemetry = new("Pegasus.Core.Intake");

    /// <summary>
    /// What became of the third-party report reading on this intake. Recorded
    /// on every path, including the ones that record nothing: without it an
    /// environment where every reading fails looks exactly like one that
    /// receives no third-party reports at all.
    /// </summary>
    private const string ReportOutcomeTag = "intake.third_party_report.outcome";

    /// <summary>The exception type behind a reading that was not recorded.</summary>
    private const string ReportFailureTag = "intake.third_party_report.failure_type";
    public Task<IntakeReceipt> ExecuteAsync(
        IntakeSource source,
        CancellationToken cancellationToken = default) =>
        // No retry orchestration wraps this direct/manual-upload path, so a
        // reader fault here has no later attempt to defer to: treat it as final.
        ExecuteCoreAsync(
            source,
            retainedSourceStorageKey: null,
            replaceExisting: false,
            isFinalAttempt: true,
            cancellationToken);

    /// <param name="isFinalAttempt">
    /// True when the caller's own retry schedule (if any) has no further
    /// attempt left for this work item. A transient reader fault is only
    /// converted into a terminal technical-failure receipt — and only then
    /// registered as Unidentified — once this is true; otherwise it
    /// propagates so the queued caller can retry and processing stays
    /// in-flight rather than allocating a U-reference.
    /// </param>
    internal Task<IntakeReceipt> ExecuteRetainedAsync(
        IntakeSource source,
        string retainedSourceStorageKey,
        bool replaceExisting = false,
        bool isFinalAttempt = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(retainedSourceStorageKey);
        return ExecuteCoreAsync(
            source,
            retainedSourceStorageKey,
            replaceExisting,
            isFinalAttempt,
            cancellationToken);
    }

    private async Task<IntakeReceipt> ExecuteCoreAsync(
        IntakeSource source,
        string? retainedSourceStorageKey,
        bool replaceExisting,
        bool isFinalAttempt,
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
                await RecordAutomaticAuditEvidenceAsync(
                    existing,
                    existing.MailClassificationDecision,
                    cancellationToken);
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
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception)
            && (isFinalAttempt || !IntakeExceptionPolicy.IsTransientFailure(exception)))
        {
            // A non-transient reader fault is always terminal. A transient
            // fault (I/O, timeout, database, or a dependency-unavailable
            // adapter fault) is only terminal once the caller has no retry
            // left; otherwise it propagates so the retained/queued caller
            // (DurableIntake) retries it on its bounded schedule. Retryable
            // processing must remain in processing and never allocate a
            // U-reference; only a terminal fault after custody succeeds does.
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
            safeSource.SourceIdentity,
            processedAtUtc,
            cancellationToken);
        if (assessment.Decision == IntakeDecision.CaseCreated
            && assessment.MailClassificationDecision is
                { CaseType: CaseType.Audit, StandaloneAuditReport: null })
        {
            assessment = assessment with
            {
                Decision = IntakeDecision.NeedsSorting,
                DecisionReason = "A standalone Audit instruction requires one attached original report stating Repairable or Total loss.",
                InstructionDraft = null,
                MissingFields = []
            };
        }
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
            assessment.CaseMatchDecision,
            safeSource.SourceIdentity.Channel == IntakeSourceChannel.Mailbox
                ? IntakeSearchProjection.Create(readResult, assessment.MailRouteDecision)
                : []);

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
        await RecordAutomaticAuditEvidenceAsync(
            receipt,
            assessment.MailClassificationDecision,
            cancellationToken);
        await RecordThirdPartyReportSourceAsync(receipt, readResult, activity, cancellationToken);
        await RegisterUnidentifiedIfTerminalAsync(receipt, cancellationToken);
        RecordTelemetry(activity, receipt, DecisionCode(receipt.Decision), started);
        return receipt;
    }

    /// <summary>
    /// Identifies the retained source's document role from the document itself
    /// and, when it carries a third-party report signature, records what the
    /// report says as ordinary source candidates (INTK-031).
    ///
    /// Retention is the right place for it: the role is a property of the bytes
    /// that were just retained, not of anything a member of staff does later,
    /// and reading it here means the Received page has the candidates the first
    /// time it is opened. It changes no receipt decision, allocates nothing and
    /// writes no Engineer value — a report remains third-party evidence until
    /// Stream B's own command accepts a figure from it.
    ///
    /// A source with no signature is left alone entirely. The store is optional
    /// for the same reason C01's other analysis composition is: until it is
    /// registered, intake behaves exactly as before.
    /// </summary>
    private async Task RecordThirdPartyReportSourceAsync(
        IntakeReceipt receipt,
        IntakeSourceReadResult readResult,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        if (retainedInstructionAnalysisStore is null)
        {
            // The feature is not composed here, so nothing was attempted and
            // nothing is claimed. Named all the same: an environment missing
            // the registration must not look like one that receives no
            // third-party reports (C05-R-15).
            activity?.SetTag(ReportOutcomeTag, "not_composed");
            return;
        }

        if (readResult.Status != IntakeSourceReadStatus.Readable)
        {
            // The reader failed or refused the format. The report reading never
            // began, which is a different fact from "this document carries no
            // report signature", and only the tag can tell them apart.
            activity?.SetTag(ReportOutcomeTag, "source_not_readable");
            return;
        }

        var asset = IntakeFileIdentity.SourceAsset(receipt);
        if (asset is null)
        {
            // Without exactly one retained source asset there is no hash, no
            // asset identity and therefore no operation key to record under.
            activity?.SetTag(ReportOutcomeTag, "no_single_source_asset");
            return;
        }

        var extraction = ThirdPartyReportExtraction.Extract(
            readResult,
            new(
                receipt.Id,
                asset.ContentHash,
                Occurrence: 0,
                IntakeAssetId: asset.Id,
                ReaderVersion: readResult.ReaderVersion,
                // The retained file's own name, carried as the document-level
                // locator for a row with no page to point at (a scan-only
                // source names no page because its text could not be read).
                // It locates; it is never read as content, so no issuer, family
                // or field value is taken from it (C05-R-16).
                SourceLabel: asset.FileName));
        if (!ThirdPartyReportAnalysis.IsRecordable(extraction))
        {
            // The document was read, carries no report signature and states
            // nothing about itself that a person has to act on. Saying so is
            // what makes the silence on the other paths meaningful.
            activity?.SetTag(ReportOutcomeTag, "no_report_signature");
            return;
        }

        var operationKey = $"{ThirdPartyReportAnalysis.PolicyKey}:{asset.Id}";
        try
        {
            var (_, isReplay) = await retainedInstructionAnalysisStore.RecordAsync(
                new(
                    Guid.NewGuid(),
                    receipt.Id,
                    asset.Id,
                    asset.ContentHash,
                    // Derived from the asset, so re-processing the same
                    // retained bytes leaves the recorded reading standing
                    // instead of writing a second set of candidates for one
                    // document. The conflict below — not the replay — is that
                    // outcome's ordinary path: a re-evaluation always moves the
                    // receipt version, so a second pass over one asset can
                    // never satisfy the store's replay check and "replayed" is
                    // reachable only where the same version is re-recorded
                    // (C05-R-19).
                    operationKey,
                    ThirdPartyReportAnalysis.Outcome(extraction.Selection),
                    receipt.Version,
                    timeProvider.GetUtcNow(),
                    ThirdPartyReportAnalysis.ToCandidates(
                        extraction,
                        readResult.ReaderKey,
                        readResult.ReaderVersion)),
                cancellationToken);
            activity?.SetTag(ReportOutcomeTag, isReplay ? "replayed" : "recorded");
        }
        catch (RetainedInstructionAnalysisConflictException)
        {
            // The key was already used, and the store raises one exception for
            // two different facts (IRetainedInstructionAnalysisStore.RecordAsync):
            // this document was already read at another receipt version — the
            // recorded reading stands, the bytes have not changed, and
            // overwriting is exactly what the key exists to prevent — or the key
            // is bound to another receipt or asset, where nothing was recorded
            // for this receipt at all. One tag for both would state something
            // false in the second case, so the stored row decides which is said
            // (C05-R-18). Named on the span either way, so a conflict is
            // distinguishable from a reading that was never attempted.
            activity?.SetTag(
                ReportOutcomeTag,
                await ConflictOutcomeAsync(
                    retainedInstructionAnalysisStore,
                    operationKey,
                    receipt.Id,
                    asset.Id,
                    cancellationToken));
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // Source evidence is supplementary. A receipt that has already been
            // stored must not fail because a report reading could not be
            // written beside it; the Received page offers analysis on demand.
            //
            // The failure is named on the span rather than swallowed. The
            // intake itself succeeded, so the span's own status stays as the
            // receipt left it: this is a named failure inside work that
            // otherwise did what it was asked.
            activity?.SetTag(ReportOutcomeTag, "not_recorded");
            activity?.SetTag(ReportFailureTag, exception.GetType().Name);
        }
    }

    /// <summary>
    /// Which conflict the analysis store raised, read back from the stored row
    /// rather than assumed. A probe that itself fails claims neither: an
    /// unverified conflict is its own outcome, because the point of the tag is
    /// that no path stays silent and none overstates what it knows.
    /// </summary>
    private static async Task<string> ConflictOutcomeAsync(
        IRetainedInstructionAnalysisStore store,
        string operationKey,
        Guid receiptId,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        try
        {
            var stored = await store.FindByOperationKeyAsync(operationKey, cancellationToken);
            return stored is not null
                && stored.ReceiptId == receiptId
                && stored.IntakeAssetId == assetId
                    ? "recorded_reading_stands"
                    : "analysis_key_bound_elsewhere";
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return "recorded_reading_unverified";
        }
    }

    private async Task RegisterUnidentifiedIfTerminalAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (registerUnidentified is null || !IsUnidentifiedEligible(receipt))
        {
            return;
        }

        await registerUnidentified.ExecuteAsync(
            BuildUnidentifiedRegistrationRequest(receipt),
            cancellationToken);
    }

    /// <summary>
    /// True for a receipt this hook should register directly. Image-only
    /// material at <see cref="IntakeDecision.NeedsSorting"/> is excluded here
    /// because <c>ImageIntakeAutomation</c> still gets a chance to resolve it
    /// to <see cref="IntakeDecision.ImageIntakeRegistered"/>; the queued
    /// caller (<c>ProcessQueuedIntake</c>) registers it as Unidentified itself
    /// once that automation runs and confirms no confident registration was
    /// made, so that material is never silently dropped from both queues.
    ///
    /// A Triage request is deferred for exactly the same reason and by the
    /// same caller: the operator's rule holds it in Unidentified only "until a
    /// vehicle registration is known, then open the Triage", and Triage
    /// creation runs after this hook. Registering here would give every Triage
    /// request an Unidentified item it is about to stop deserving (INTK-033).
    /// </summary>
    internal static bool IsUnidentifiedEligible(IntakeReceipt receipt) =>
        receipt.Decision is IntakeDecision.NeedsSorting
            or IntakeDecision.Unsupported
            or IntakeDecision.OcrRequired
            or IntakeDecision.TechnicalFailure
        && !IsDeferredForAutomation(receipt);

    /// <summary>
    /// Which receipts this hook leaves for the queued caller to register.
    /// Named once because two components need the same membership rule and
    /// need it in opposite polarity — this hook skips them, and
    /// <c>ProcessQueuedIntake</c> registers whichever of them its own
    /// automation did not resolve. Written out twice, a third deferral reason
    /// would have to be added in both places and nothing would catch a miss.
    /// </summary>
    internal static bool IsDeferredForAutomation(IntakeReceipt receipt) =>
        receipt.Decision == IntakeDecision.NeedsSorting
        && (ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt)
            || IsTriageRequest(receipt)
            || SubmitMailboxImageIntake.IsCandidate(receipt));

    /// <summary>
    /// Whether this receipt is a Triage request. One reading, so no surface
    /// re-derives it from the taxonomy.
    ///
    /// Two routes reach the same answer. A mail instruction has it read by the
    /// accepted route classification; a Provider API submission has its
    /// Principal declare it, and carries no classification at all. Both record
    /// the same <see cref="IntakeEvidenceFinding.AcceptedTriageMatch"/>, which
    /// is already what Triage creation itself keys off — so reading the
    /// evidence keeps one answer for both. Without the second clause a declared
    /// Triage opened its Triage record and an Unidentified item beside it, the
    /// two-queues defect INTK-033 closed for the mail route.
    ///
    /// The classification clause stays: a reply in a Triage thread classifies
    /// as a Triage request but is deliberately given no accepted-match
    /// evidence, and must still read as one here.
    /// </summary>
    public static bool IsTriageRequest(IntakeReceipt receipt) =>
        receipt.MailClassificationDecision is { IsTriageRequest: true }
        || receipt.Evidence.Any(item => item.Finding == IntakeEvidenceFinding.AcceptedTriageMatch);

    internal static RegisterUnidentifiedRequest BuildUnidentifiedRegistrationRequest(IntakeReceipt receipt) =>
        new(
            UnidentifiedOrigin.Receipt(receipt.Id),
            MapUnidentifiedReason(receipt),
            receipt.FailureReason ?? receipt.DecisionReason,
            ActionActor.SystemWorker("intake-processing"),
            $"intake-unidentified:{receipt.Id:N}:{receipt.Version}",
            // The queue and detail UI order and display Unidentified work by
            // when the source arrived, not when this processing attempt ran;
            // a delayed or retried attempt must not misreport either.
            receipt.ReceivedAtUtc);

    /// <summary>
    /// Selects the specific reason from evidence the assessment already
    /// established, rather than collapsing every non-Unsupported,
    /// non-TechnicalFailure outcome into <see cref="UnidentifiedReasonCode.NoUsableIdentification"/>.
    /// </summary>
    private static UnidentifiedReasonCode MapUnidentifiedReason(IntakeReceipt receipt) => receipt.Decision switch
    {
        IntakeDecision.Unsupported => UnidentifiedReasonCode.UnsupportedContent,
        IntakeDecision.TechnicalFailure => UnidentifiedReasonCode.TechnicalProcessingFailure,
        _ when receipt.CaseMatchDecision?.Outcome == CaseMatchOutcome.Ambiguous =>
            UnidentifiedReasonCode.ConflictingIdentification,
        _ when receipt.MailClassificationDecision?.Outcome == MailClassificationOutcome.Ambiguous =>
            UnidentifiedReasonCode.AmbiguousOwnershipOrDestination,
        _ when receipt.Evidence.Any(evidence => evidence.Signal == "intake_limit_exceeded") =>
            UnidentifiedReasonCode.UnreadableOrCorruptContent,
        _ => UnidentifiedReasonCode.NoUsableIdentification
    };

    private async Task RecordAutomaticAuditEvidenceAsync(
        IntakeReceipt receipt,
        MailClassificationResult? classification,
        CancellationToken cancellationToken)
    {
        // An e-mailed Audit has its report identified by the route's own
        // classification; a Provider API Audit has it declared, and the verdict
        // with it (operator decision, 2026-08-28). Either way exactly one
        // retained attachment is the original report and one AuditAssessment
        // derives the a./ap. reference.
        var report = classification?.StandaloneAuditReport
            ?? await DeclaredAuditReportAsync(receipt, cancellationToken);
        if (report is null)
        {
            return;
        }

        var reportAsset = receipt.AssetRecords.SingleOrDefault(asset =>
            string.Equals(asset.SourceLabel, report.AssetSourceLabel, StringComparison.Ordinal));
        if (reportAsset is null)
        {
            throw new InvalidDataException(
                "The classified Audit report is not retained as an intake attachment.");
        }
        if (automaticStandaloneAuditEvidence is null)
        {
            throw new InvalidOperationException(
                "Automatic Audit evidence recording is not configured.");
        }

        await automaticStandaloneAuditEvidence.ExecuteAsync(
            new(receipt.Id, receipt.Version, reportAsset.Id, report.Assessment),
            cancellationToken);
    }

    /// <summary>
    /// The original report a Provider API Audit declared, or null when the
    /// receipt is not one. The verdict is the Principal's own: the operator
    /// ruled on 2026-08-28 that a declared verdict decides the reference,
    /// replacing the read of the report's literal outcome for this route.
    /// </summary>
    private async Task<StandaloneAuditReportEvaluation?> DeclaredAuditReportAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        if (receipt.SourceIdentity.Channel != IntakeSourceChannel.ProviderApi)
        {
            return null;
        }

        var binding = await FindProviderBindingAsync(receipt.SourceIdentity, cancellationToken);
        return binding?.Instruction is { Kind: ProviderInstructionKind.Audit, OriginalReportVerdict: { } verdict }
            ? new(ProviderInstructionPolicy.OriginalReportSourceLabel, verdict)
            : null;
    }

    private async Task<IntakeAssessment> AssessAsync(
        IntakeSourceReadResult readResult,
        IntakeSourceIdentity sourceIdentity,
        DateTimeOffset processedAtUtc,
        CancellationToken cancellationToken)
    {
        var sourceChannel = sourceIdentity.Channel;
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

        if (sourceChannel == IntakeSourceChannel.ProviderApi)
        {
            // A provider states its instruction; nothing about it is read out of
            // the submitted files, and no mail route or extraction policy
            // applies. A source with no retained submission binding is refused
            // rather than guessed at.
            var binding = await FindProviderBindingAsync(sourceIdentity, cancellationToken);
            if (binding is null)
            {
                return new(
                    IntakeDecision.NeedsSorting,
                    "The submission this source belongs to was not found.",
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

            // Existing-Case rejection, before any draft, review field,
            // completeness evaluation or allocation. The keys are the four
            // DECLARED identity facts only — nothing is read out of the
            // submitted files — normalized by the Principal's own policy.
            //
            // Assumption: the authenticated Principal's code and the work
            // provider code are one vocabulary. They are matched by ordinal
            // equality against IProviderCaseMatchPolicy.WorkProviderCode, and
            // DeclaredAssessment already feeds binding.PrincipalCode to
            // ProviderInstructionPolicy.ToDraft as the provider code. A
            // Principal with no case-match policy yields null and is not
            // blocked from creating cases.
            var instruction = binding.Instruction;
            var providerMatchDecision = await caseMatchEvaluator.ExecuteDeclaredAsync(
                binding.PrincipalCode,
                new(
                    instruction.ClaimNumber,
                    instruction.VehicleRegistration,
                    instruction.ClaimantName,
                    instruction.DateOfIncident),
                cancellationToken);
            if (providerMatchDecision?.Outcome is CaseMatchOutcome.UniqueMatch
                or CaseMatchOutcome.Ambiguous)
            {
                throw new ProviderExistingCaseMatchException();
            }

            return DeclaredAssessment(
                binding,
                readerEvidence,
                processedAtUtc,
                providerMatchDecision);
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

        var principalContext = EstablishPrincipalContext(mailRouteDecision);
        var mailClassificationDecision = EvaluateMailClassification(
            readResult,
            principalContext?.PrincipalCode);
        var caseMatchDecision = await caseMatchEvaluator.ExecuteAsync(
            readResult,
            mailRouteDecision,
            cancellationToken);
        if (principalContext is null)
        {
            if (readResult.RequiresOcr)
            {
                return new(
                    IntakeDecision.OcrRequired,
                    "Readable content is insufficient to establish a principal and scanned PDF pages require OCR.",
                    readerEvidence,
                    [],
                    null,
                    [],
                    "ocr_required",
                    "The PDF appears to contain scanned pages without enough embedded text for review.",
                    null,
                    null,
                    mailRouteDecision,
                    mailClassificationDecision,
                    caseMatchDecision);
            }

            return new(
                IntakeDecision.NeedsSorting,
                "No accepted intake route established the principal for automatic case creation.",
                readerEvidence,
                [],
                null,
                [],
                null,
                null,
                null,
                null,
                mailRouteDecision,
                mailClassificationDecision,
                caseMatchDecision);
        }

        if (!string.Equals(
                extractionPolicy.PrincipalCode,
                principalContext.PrincipalCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The established principal has no matching instruction extraction policy.");
        }

        var policyResult = extractionPolicy.Extract(
            readResult,
            processedAtUtc,
            principalContext);
        EnsureConsistentPolicyResult(policyResult, principalContext);
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
        // A Triage request is pre-case work, and the accepted route policy has
        // already said so. Left as CaseCreated it went to automatic allocation,
        // which fails closed for want of a case type a Triage request correctly
        // does not carry — producing no case, no Triage and no queue entry at
        // all (INTK-033).
        if (mailClassificationDecision is { IsTriageRequest: true }
            && decision == IntakeDecision.CaseCreated)
        {
            decision = IntakeDecision.NeedsSorting;
            reason = "A Triage request is pre-case work; no case is created from it.";
        }

        var triageMatch = AcceptedTriageMatchEvidence(mailClassificationDecision);

        IntakeEvidence[] evidence = triageMatch is null
            ? [.. readerEvidence, .. policyResult.Evidence]
            : [.. readerEvidence, .. policyResult.Evidence, triageMatch];

        return new(
            decision,
            reason,
            evidence,
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

    /// <summary>
    /// The accepted Triage match, derived from the route's own classification
    /// decision rather than from a separate matcher.
    /// </summary>
    /// <remarks>
    /// FRD-03 says Triage begins when "the exact accepted route policy
    /// classifies a provider request as an assessment request", and ADR-0008
    /// makes that route policy the only owner of message-type classification.
    /// A second matcher asking the same question was therefore a duplicate
    /// owner, and the only implementation it ever had was the null one — so
    /// the gate downstream could never pass and no Triage was ever created
    /// from intake (INTK-033).
    ///
    /// The source is <see cref="IntakeEvidenceSource.SystemDefault"/> because
    /// this finding is a policy judgement over the whole message, not a value
    /// lifted from one part of it; which tell fired is carried precisely by
    /// the detail and by the recorded classification decision itself.
    /// </remarks>
    private static IntakeEvidence? AcceptedTriageMatchEvidence(
        MailClassificationResult? classification)
    {
        // A reply is correspondence about a Triage, not a new assessment
        // request — FRD-03 begins a Triage from a *provider request*. The
        // subject tell is anchored past RE/FW on purpose and the body tell
        // matches quoted text, so every reply in a Triage thread classifies
        // as one; and Triage identity is per message, never per claim or
        // registration, so honouring a reply here would open a second Open
        // Triage on the same vehicle for ordinary thread traffic. The reply
        // still leaves case allocation alone and reaches Unidentified, which
        // is a queue somebody works — today it reaches none.
        if (classification is not { IsTriageRequest: true }
            || classification.Category?.IsReplyContext == true)
        {
            return null;
        }

        var matched = string.Join(
            ", ",
            classification.Predicates.Where(predicate => predicate.Matched).Select(predicate => predicate.Key));
        return new(
            IntakeEvidenceSource.SystemDefault,
            IntakeEvidenceStrength.Strong,
            IntakeEvidenceFinding.AcceptedTriageMatch,
            MailCategory.TriageRequestSubtype,
            $"The accepted route classification recorded this message as a Triage request (predicates: {matched}).",
            classification.PolicyKey,
            classification.PolicyVersion);
    }

    /// <summary>
    /// Classification belongs to the established Principal — the accepted
    /// route's work provider. A Provider API submission never reaches here: its
    /// Principal declares the instruction's type rather than having it read.
    /// </summary>
    private MailClassificationResult? EvaluateMailClassification(
        IntakeSourceReadResult readResult,
        string? principalCode)
    {
        if (principalCode is null)
        {
            return null;
        }

        var policy = mailClassificationPolicies.SingleOrDefault(candidate =>
            string.Equals(
                candidate.WorkProviderCode,
                principalCode,
                StringComparison.Ordinal));
        if (policy is null)
        {
            return null;
        }

        var result = policy.Classify(readResult);
        EnsureConsistentClassificationResult(result);
        return result;
    }

    /// <summary>
    /// A Provider API source is bound to the Principal whose credential
    /// submitted it (API-01): the binding is the retained submission record,
    /// found by the member's source identity, never inferred from the
    /// content or a sender. A source without a binding fails closed to
    /// sorting.
    /// </summary>
    private Task<ProviderSubmissionBinding?> FindProviderBindingAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken) =>
        providerSubmissionBindings is null
            ? Task.FromResult<ProviderSubmissionBinding?>(null)
            : providerSubmissionBindings.FindAsync(sourceIdentity, cancellationToken);

    /// <summary>
    /// The assessment for a submission whose Principal declared its instruction
    /// (API-01). Nothing is extracted: the values are the ones the authenticated
    /// Principal stated, and they are recorded as review fields carrying
    /// <see cref="IntakeEvidenceSource.ProviderDeclaration"/> so the case shows
    /// where each came from.
    ///
    /// This is a substitution, not a second pipeline. Everything downstream —
    /// allocation, Triage creation, custody, action history, the durable Worker
    /// path — runs exactly as it does for an e-mail instruction.
    /// </summary>
    private static IntakeAssessment DeclaredAssessment(
        ProviderSubmissionBinding binding,
        IReadOnlyList<IntakeEvidence> readerEvidence,
        DateTimeOffset processedAtUtc,
        CaseMatchEvaluationResult? caseMatchDecision)
    {
        var instruction = binding.Instruction;
        var isTriage = instruction.Kind == ProviderInstructionKind.Triage;
        var draft = ProviderInstructionPolicy.ToDraft(
            instruction,
            binding.PrincipalCode,
            LondonCalendar.DateAt(processedAtUtc));
        var fields = ProviderInstructionPolicy.ReviewFields(draft);
        var missingFields = InstructionDraftCompleteness.MissingFieldNames(draft);
        IntakeEvidence[] evidence = isTriage
            ?
            [
                .. readerEvidence,
                ProviderInstructionPolicy.DeclarationEvidence(instruction.Kind),
                ProviderInstructionPolicy.TriageEvidence()
            ]
            : [.. readerEvidence, ProviderInstructionPolicy.DeclarationEvidence(instruction.Kind)];

        // The identity-critical fields are the only ones that may withhold a
        // reference; ordinary detail missing from a declaration leaves the case
        // Not ready exactly as it does for an e-mail (FRD-02).
        var missingIdentity = InstructionDraftCompleteness.MissingIdentityCriticalFieldNames(draft);
        var decision = missingIdentity.Count > 0 || isTriage
            ? IntakeDecision.NeedsSorting
            : IntakeDecision.CaseCreated;
        var reason = missingIdentity.Count > 0
            ? $"The declared instruction does not identify the claim: {string.Join(", ", missingIdentity)}."
            : isTriage
                ? "A Triage request is pre-case work; no case is created from it."
                : "The authenticated Principal declared a definitive instruction.";
        return new(
            decision,
            reason,
            evidence,
            fields,
            draft,
            missingFields,
            null,
            null,
            ProviderInstructionPolicy.PolicyKey,
            ProviderInstructionPolicy.PolicyVersion,
            // No mail route and no mail classification apply to a declared
            // instruction; the case-match decision is now recorded even when it
            // is NoMatch or NoKeys, so the receipt carries the evidence that
            // the existing-Case check ran.
            null,
            null,
            caseMatchDecision);
    }

    private static EstablishedPrincipalContext? EstablishPrincipalContext(
        MailRouteEvaluationResult? mailRouteDecision) =>
        mailRouteDecision is
        {
            Disposition: MailRouteDisposition.Accepted,
            SelectedRoute: { } route
        }
            ? new(route.WorkProviderCode, mailRouteDecision.PolicyKey, mailRouteDecision.PolicyVersion)
            : null;

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
        // A Provider API submission's route identity is its credential, so
        // the sender of a forwarded message inside it never selects a route.
        if (sourceChannel == IntakeSourceChannel.ProviderApi)
        {
            return null;
        }

        if (sourceChannel != IntakeSourceChannel.Mailbox
            && !readResult.TransportEvidence.Any(item =>
                item.Source == IntakeEvidenceSource.Sender
                && item.SenderIdentityKind == IntakeSenderIdentityKind.Transport))
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

    private static void EnsureConsistentPolicyResult(
        InstructionExtractionResult policyResult,
        EstablishedPrincipalContext principalContext)
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

        if (policyResult.InstructionDraft is { } draft
            && !string.Equals(
                draft.SuggestedPrincipalCode,
                principalContext.PrincipalCode,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The instruction draft principal does not match the established principal.");
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
        IntakeSourceChannel.ProviderApi => "provider_api",
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
