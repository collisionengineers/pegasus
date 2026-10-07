using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal sealed record ReportArtifactToolItem(
    Guid ArtifactId,
    string Kind,
    string Status,
    string Filing,
    string? FileName,
    string? MediaType,
    string? Sha256,
    long? ContentLength,
    Guid? DocumentId,
    Guid? DocumentVersionId,
    bool IsApproved);

internal sealed record ReportGenerationToolItem(
    Guid GenerationId,
    string WorkKind,
    string State,
    bool IsCurrent,
    bool IsStale,
    DateTimeOffset GeneratedAtUtc,
    long CaseVersion,
    Guid? SupersededById,
    IReadOnlyList<ReportArtifactToolItem> Artifacts);

internal sealed record ReportApprovalToolItem(
    Guid ApprovalId,
    string ArtifactIdentity,
    string ArtifactSha256,
    string ApprovedByKind,
    string ApprovedBy,
    DateTimeOffset ApprovedAtUtc);

internal sealed record ReportSentEvidenceToolItem(
    Guid EvidenceId,
    string MailboxIdentity,
    string InternetMessageIdentity,
    DateTimeOffset SentAtUtc,
    DateTimeOffset? LinkedAtUtc);

internal sealed record ReportListToolResult(
    Guid CaseId,
    string Work,
    long CaseVersion,
    string State,
    IReadOnlyList<ReportGenerationToolItem> Generations,
    ReportApprovalToolItem? Approval,
    ReportSentEvidenceToolItem? SentEvidence,
    IReadOnlyList<ReportSentEvidenceToolItem> AvailableSentEvidence,
    string CorrelationId);

internal sealed record ReportReadinessToolItem(
    string Requirement,
    string Source,
    string WhyOutstanding,
    string HowToResolve);

internal sealed record ReportActionToolResult(
    Guid CaseId,
    string Action,
    string Outcome,
    long CaseVersion,
    string State,
    Guid? GenerationId,
    IReadOnlyList<ReportArtifactToolItem> Artifacts,
    IReadOnlyList<ReportReadinessToolItem> NotReadyReasons,
    string? FeeNoteOutcome,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor report tools (FRD-10, ADR-0064): the Case's report
/// generations, and the acts staff take on them — Generate report (which goes
/// on to make the separate fee note), the Repair Spec and images companion
/// documents, recording report approval, and linking or unlinking retained
/// report-Sent evidence — through the same Core commands, lease and version
/// guards as the Case page. The operator approved report generation and
/// approval for the Automation Actor (7 October 2026). Sending stays with
/// staff: nothing here sends anything.
/// </summary>
[McpServerToolType]
internal sealed class ReportMcpTools(
    ICaseReportGenerationStore reportGenerations,
    IGenerateCaseReport generateReport,
    IRecordCaseReportApproval recordApproval,
    ILinkReportEvidence linkEvidence,
    IUnlinkReportEvidence unlinkEvidence,
    IGetCaseHeader getCaseHeader,
    IGetCasePageFrame getPageFrame,
    IGetAssessmentAccess getAssessmentAccess,
    ICaseWorkflowQueries workflowQueries,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    AutomationEditLease leases)
{
    private const int MaximumOperationKeyLength = 100;

    private static readonly string[] Actions =
    [
        "generate_report", "generate_repair_spec", "generate_image_pack",
        "record_approval", "link_sent_evidence", "unlink_sent_evidence",
    ];

    [McpServerTool(
        Name = "pegasus_report_list",
        Title = "List case reports",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists a Case's report generations for one work, as the Case page reads them: each generation's identifier, state (Pending, Confirmed or Stale), whether it is the work's current one, when it was generated and at which case version, and its artifacts (AssessmentReport, FeeNote, RepairSpecification, ImagePack) with filing state, file name, SHA-256 and whether the recorded report approval names it. Also returns the work's recorded report approval, its linked report-Sent evidence, and the retained Sent evidence available to link.")]
    public async Task<ReportListToolResult> ListAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("current (the default; the Audit once a Case has one) or inspection (the Inspection report of a Case that has its Audit).")] string? work = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        return await auditor.RecordAsync(
            context,
            "pegasus_report_list",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            operationKey: null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var selector = ParseWork(work);
                var header = await getCaseHeader.ExecuteAsync(new(caseId, context.Actor), cancellationToken)
                    ?? throw new McpException("The case was not found.");
                var (approval, sent) = WorkEvidence(header, selector);
                var generations = await reportGenerations.ListAsync(context.Actor, caseId, selector, cancellationToken);
                var current = await reportGenerations.GetCurrentAsync(context.Actor, caseId, selector, cancellationToken);
                var frame = await getPageFrame.ExecuteAsync(new(caseId, context.Actor, Work: selector), cancellationToken);
                return new ReportListToolResult(
                    caseId,
                    selector == CaseWorkSelector.Primary ? "inspection" : "current",
                    header.Workflow.Version,
                    header.Workflow.State.ToString(),
                    generations
                        .Select(generation => MapGeneration(generation, current?.Id, approval))
                        .ToArray(),
                    approval is null
                        ? null
                        : new ReportApprovalToolItem(
                            approval.ApprovalId,
                            approval.ArtifactIdentity,
                            approval.ArtifactSha256,
                            approval.ApprovedBy.Kind.ToString(),
                            approval.ApprovedBy.SubjectId,
                            approval.ApprovedAtUtc),
                    sent is null
                        ? null
                        : new ReportSentEvidenceToolItem(sent.EvidenceId, sent.MailboxIdentity, sent.InternetMessageIdentity,
                            sent.SentAtUtc, sent.LinkedAtUtc),
                    (frame?.AvailableReportSentEvidence ?? [])
                        .Select(evidence => new ReportSentEvidenceToolItem(
                            evidence.EvidenceId,
                            evidence.MailboxIdentity,
                            evidence.InternetMessageIdentity,
                            evidence.SentAtUtc,
                            null))
                        .ToArray(),
                    context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_report_action",
        Title = "Act on case report",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Takes one report act a member of staff takes on the Case page, through the same Core command and guards: generate_report (freezes the Case's accepted facts and renders the immutable report, filed to the Case; once it is confirmed the separate fee note is made from the same generation), generate_repair_spec or generate_image_pack (a companion document of a confirmed generation, generationId defaulting to the work's current one), record_approval (records approval of one stored generated artifact, artifactId from pegasus_report_list; while the Case is With Engineer, or on the Inspection report of a Case that has its Audit; this is not a send), link_sent_evidence (links retained report-Sent evidence, evidenceId from pegasus_report_list; the current work's report moves the Case to Post report) or unlink_sent_evidence (reason; the currently linked evidence unless evidenceId names it). Nothing here sends a report. A generation that is not ready answers outcome NotReady with the reasons and records nothing. Needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command). A generation keeps the lease; approval and the evidence acts end it.")]
    public async Task<ReportActionToolResult> ActAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key returns the same outcome without rendering again.")] string operationKey,
        [Description("The act: generate_report, generate_repair_spec, generate_image_pack, record_approval, link_sent_evidence or unlink_sent_evidence.")] string action,
        [Description("current (the default; the Audit once a Case has one) or inspection (the Inspection report of a Case that has its Audit). unlink_sent_evidence acts on the current work only.")] string? work = null,
        [Description("generate_repair_spec and generate_image_pack: the confirmed generation the companion document extends; omit for the work's current generation.")] Guid? generationId = null,
        [Description("record_approval: the stored generated artifact approved (pegasus_report_list).")] Guid? artifactId = null,
        [Description("link_sent_evidence: the retained report-Sent evidence to link; unlink_sent_evidence: the linked evidence, defaulting to the one linked now.")] Guid? evidenceId = null,
        [Description("Why (case history reason, at most 500 characters). Required for record_approval, link_sent_evidence and unlink_sent_evidence.")] string? reason = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_report_action",
            caseId == Guid.Empty ? "invalid" : caseId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "case identifier");
                var verb = action?.Trim() ?? string.Empty;
                if (!Actions.Contains(verb, StringComparer.Ordinal))
                {
                    throw new McpException("The action must be one of: " + string.Join(", ", Actions) + ".");
                }
                var selector = ParseWork(work);
                var result = verb switch
                {
                    "generate_report" => await GenerateAsync(
                        context, caseId, expectedVersion, key, CaseReportArtifactKind.AssessmentReport,
                        null, selector, editLeaseToken, cancellationToken),
                    "generate_repair_spec" => await GenerateAsync(
                        context, caseId, expectedVersion, key, CaseReportArtifactKind.RepairSpecification,
                        generationId, selector, editLeaseToken, cancellationToken),
                    "generate_image_pack" => await GenerateAsync(
                        context, caseId, expectedVersion, key, CaseReportArtifactKind.ImagePack,
                        generationId, selector, editLeaseToken, cancellationToken),
                    _ => await RecordAsync(
                        context, verb, caseId, expectedVersion, key, selector, artifactId, evidenceId,
                        RequireReason(verb, reason), editLeaseToken, cancellationToken),
                };
                var workflow = await workflowQueries.GetAsync(caseId, cancellationToken)
                    ?? throw new McpException("The case was not found.");
                return new ReportActionToolResult(
                    caseId,
                    verb,
                    result.Outcome,
                    workflow.Version,
                    workflow.State.ToString(),
                    result.Generation?.Id,
                    (result.Generation?.Artifacts ?? [])
                        .Select(artifact => MapArtifact(artifact, approvedSha256: null))
                        .ToArray(),
                    result.Reasons
                        .Select(item => new ReportReadinessToolItem(
                            item.Requirement, item.Source, item.WhyOutstanding, item.HowToResolve))
                        .ToArray(),
                    result.FeeNoteOutcome,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }

    /// <summary>
    /// One Generate, as the Case page's: the Case must be open in a state its
    /// assessment can be read in, and a read-only Case generates only the
    /// Inspection report of a Case that has its Audit. The report goes on to
    /// make its separate fee note from the generation it confirmed, under the
    /// same lease; a report not yet confirmed leaves the fee note to the next
    /// generate_report, which replays the report's operation key.
    /// </summary>
    private async Task<ActOutcome> GenerateAsync(
        AutomationActorContext context,
        Guid caseId,
        long expectedVersion,
        string key,
        CaseReportArtifactKind kind,
        Guid? targetGenerationId,
        CaseWorkSelector selector,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        var actor = context.Actor;
        var access = await getAssessmentAccess.ExecuteAsync(new(caseId, actor), cancellationToken);
        if (access is null || !access.CanOpen)
        {
            throw new McpException("The case was not found or its report cannot be generated in its state.");
        }
        if (access.IsReadOnly
            && !(selector == CaseWorkSelector.Primary
                && (await getCaseHeader.ExecuteAsync(new(caseId, actor), cancellationToken))?.Works is { HasAudit: true }))
        {
            throw new McpException("The case is read-only once Complete.");
        }
        if (kind != CaseReportArtifactKind.AssessmentReport && targetGenerationId is null)
        {
            targetGenerationId = (await reportGenerations.GetCurrentAsync(actor, caseId, selector, cancellationToken))?.Id
                ?? throw new McpException("The work has no current report generation to extend; generate the report first.");
        }

        return await leases.RunCaseAsync(
            caseId,
            expectedVersion,
            editLeaseToken,
            actor,
            key,
            async token =>
            {
                var result = await GenerateOneAsync(
                    actor, caseId, expectedVersion, token, key, kind, targetGenerationId, selector, cancellationToken);
                string? feeNote = null;
                if (kind == CaseReportArtifactKind.AssessmentReport
                    && result.Generation is { State: CaseReportGenerationState.Confirmed } generation
                    && generation.Artifacts.Any(artifact => artifact is
                    {
                        Kind: CaseReportArtifactKind.AssessmentReport,
                        Status: CaseReportArtifactStatus.Confirmed,
                    })
                    && !generation.Artifacts.Any(artifact => artifact is
                    {
                        Kind: CaseReportArtifactKind.FeeNote,
                        Status: CaseReportArtifactStatus.Confirmed,
                    }))
                {
                    // The report stands whatever the fee note's outcome.
                    var feeNoteResult = await GenerateOneAsync(
                        actor, caseId, expectedVersion, token, Derive(key, ":fee-note"),
                        CaseReportArtifactKind.FeeNote, generation.Id, selector, cancellationToken);
                    feeNote = feeNoteResult.Outcome.ToString();
                    if (feeNoteResult.Generation is { } withFeeNote)
                    {
                        result = result with { Generation = withFeeNote };
                    }
                }
                return new ActOutcome(result.Outcome.ToString(), result.Generation, result.Reasons, feeNote);
            },
            cancellationToken);
    }

    private async Task<CaseReportGenerationResult> GenerateOneAsync(
        ActionActor actor,
        Guid caseId,
        long expectedVersion,
        string token,
        string key,
        CaseReportArtifactKind kind,
        Guid? targetGenerationId,
        CaseWorkSelector selector,
        CancellationToken cancellationToken)
    {
        CaseReportGenerationResult result;
        try
        {
            result = await generateReport.ExecuteAsync(
                new(
                    actor,
                    caseId,
                    expectedVersion,
                    token,
                    key,
                    kind,
                    kind switch
                    {
                        CaseReportArtifactKind.AssessmentReport => "Generate the immutable case report",
                        CaseReportArtifactKind.FeeNote => "Generate the immutable fee note",
                        CaseReportArtifactKind.RepairSpecification => "Generate the immutable Repair Spec",
                        _ => "Generate the immutable images",
                    },
                    targetGenerationId,
                    selector),
                cancellationToken);
        }
        catch (ReportRenderRejectedException refusal)
        {
            // A render refusal says what stopped it, as the Case page shows it.
            throw new McpException(refusal.Message);
        }
        return result.Outcome == CaseReportGenerationOutcome.NotFound
            ? throw new McpException("The case or report generation was not found.")
            : result;
    }

    private async Task<ActOutcome> RecordAsync(
        AutomationActorContext context,
        string verb,
        Guid caseId,
        long expectedVersion,
        string key,
        CaseWorkSelector selector,
        Guid? artifactId,
        Guid? evidenceId,
        string reason,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        var actor = context.Actor;
        ReportApprovalSubmission? submission = null;
        CaseReportGenerationRecord? approved = null;
        Guid evidence = Guid.Empty;
        switch (verb)
        {
            case "record_approval":
                var id = artifactId is { } requested && requested != Guid.Empty
                    ? requested
                    : throw new McpException("The record_approval action needs artifactId.");
                var generations = await reportGenerations.ListAsync(actor, caseId, selector, cancellationToken);
                approved = generations.FirstOrDefault(generation => generation.Artifacts.Any(item => item.Id == id))
                    ?? throw new McpException("The artifact is not a report artifact of this work.");
                var artifact = approved.Artifacts.Single(item => item.Id == id);
                if (artifact.Status != CaseReportArtifactStatus.Confirmed || string.IsNullOrWhiteSpace(artifact.Sha256))
                {
                    throw new McpException("Only a stored generated artifact can be approved.");
                }
                // The approval identity is derived from the call, so a replay
                // of the same operation key records the same approval once.
                submission = new(ApprovalId(caseId, key), artifact.Id.ToString("D"), artifact.Sha256);
                break;
            case "link_sent_evidence":
                evidence = evidenceId is { } link && link != Guid.Empty
                    ? link
                    : throw new McpException("The link_sent_evidence action needs evidenceId.");
                break;
            default:
                evidence = evidenceId is { } named && named != Guid.Empty
                    ? named
                    : (await workflowQueries.GetAsync(caseId, cancellationToken))?.ReportSentEvidence?.EvidenceId
                        ?? throw new McpException("The case has no linked report-Sent evidence to unlink.");
                break;
        }

        await leases.RunCaseAsync(
            caseId,
            expectedVersion,
            editLeaseToken,
            actor,
            key,
            token => verb switch
            {
                "record_approval" => recordApproval.ExecuteAsync(
                    new(caseId, expectedVersion, actor, key, reason, token, submission!, selector),
                    cancellationToken),
                "link_sent_evidence" => linkEvidence.ExecuteAsync(
                    new(caseId, expectedVersion, actor, key, reason, token, evidence, selector),
                    cancellationToken),
                _ => unlinkEvidence.ExecuteAsync(
                    new(caseId, expectedVersion, actor, key, reason, token, evidence),
                    cancellationToken),
            },
            cancellationToken);
        return new ActOutcome("Recorded", approved, [], null);
    }

    private static string RequireReason(string verb, string? reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new McpException($"The {verb} action needs a reason.")
            : reason;

    private static CaseWorkSelector ParseWork(string? work) => work?.Trim() switch
    {
        null or "" or "current" => CaseWorkSelector.Current,
        "inspection" => CaseWorkSelector.Primary,
        _ => throw new McpException("The work must be current or inspection."),
    };

    /// <summary>The selected work's recorded approval and linked Sent evidence.</summary>
    private static (ReportApprovalEvidence? Approval, ApprovedMailboxReportSentEvidence? Sent) WorkEvidence(
        CaseHeader header,
        CaseWorkSelector selector) =>
        header.Works?.Select(selector) is { } selected
            ? (selected.ReportApproval, selected.ReportSentEvidence)
            : (header.Workflow.ReportApproval, header.Workflow.ReportSentEvidence);

    private static ReportGenerationToolItem MapGeneration(
        CaseReportGenerationRecord generation,
        Guid? currentId,
        ReportApprovalEvidence? approval) => new(
        generation.Id,
        generation.WorkKind.ToString(),
        generation.State.ToString(),
        generation.Id == currentId,
        generation.State == CaseReportGenerationState.Stale || generation.SupersededById is not null,
        generation.GeneratedAtUtc,
        generation.CaseVersion,
        generation.SupersededById,
        generation.Artifacts.Select(artifact => MapArtifact(artifact, approval?.ArtifactSha256)).ToArray());

    private static ReportArtifactToolItem MapArtifact(CaseReportArtifactRecord artifact, string? approvedSha256) => new(
        artifact.Id,
        artifact.Kind.ToString(),
        artifact.Status.ToString(),
        artifact.Filing.ToString(),
        artifact.FileName,
        artifact.MediaType,
        artifact.Sha256,
        artifact.ContentLength,
        artifact.DocumentId,
        artifact.VersionId,
        approvedSha256 is not null
            && artifact.Sha256 is not null
            && string.Equals(artifact.Sha256, approvedSha256, StringComparison.OrdinalIgnoreCase));

    /// <summary>A stable approval identity for one Case and operation key.</summary>
    private static Guid ApprovalId(Guid caseId, string operationKey) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"report-approval:{caseId:D}:{operationKey}")).AsSpan(0, 16));

    /// <summary>A follow-on act's own operation key, derived within Core's key length.</summary>
    private static string Derive(string operationKey, string suffix)
    {
        var room = MaximumOperationKeyLength - suffix.Length;
        return (operationKey.Length > room ? operationKey[..room] : operationKey) + suffix;
    }

    /// <summary>One act's outcome before the Case's new version is read back.</summary>
    private sealed record ActOutcome(
        string Outcome,
        CaseReportGenerationRecord? Generation,
        IReadOnlyList<AssessmentReadinessItem> Reasons,
        string? FeeNoteOutcome);
}
