using System.ComponentModel;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal sealed record DocumentActionToolResult(
    Guid CaseId,
    string Action,
    long? CaseVersion,
    Guid? OccurrenceId,
    Guid? TagId,
    string? Outcome,
    string? Message,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Automation Actor document acts (FRD-10, ADR-0064): what a member of staff
/// does to a Case's filed documents and images on the Case page — tag and
/// untag an image, put it in the report or take it out, mark the Audit's
/// original report, remove a document, retry failed custody, and add a word to
/// the shared image-tag vocabulary — through the same Core commands, lease
/// and version guards as the staff Custody page.
/// </summary>
[McpServerToolType]
internal sealed class DocumentActionMcpTools(
    ITagCaseImage tagImage,
    IUntagCaseImage untagImage,
    ISetCaseImageInReport setImageInReport,
    ICreateImageTag createImageTag,
    MarkAsOriginalReport markAsOriginalReport,
    ILogicallyRemoveDocument removeDocument,
    IRetryCaseCustody retryCustody,
    ICaseWorkflowQueries workflowQueries,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    AutomationEditLease leases)
{
    private static readonly string[] Actions =
    [
        "tag", "untag", "set_in_report", "mark_original_report", "remove", "retry_custody", "create_image_tag",
    ];

    [McpServerTool(
        Name = "pegasus_document_action",
        Title = "Act on case document",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Takes one document act a member of staff takes on a Case's files, through the same Core command and guards: tag or untag (one image occurrence and one tagId from pegasus_vocabulary_get's image tags), set_in_report (inReport true puts the image in the report, false takes it out), mark_original_report (occurrenceId and versionId of a filed non-image document on an Audit awaiting its original report; its readable figures fill the Original report cells), remove (reason; the occurrence is removed from the Case while custody content and history are kept), retry_custody (reason, retryTarget; re-arms failed Box custody of the Case's source or Audit reference folder) or create_image_tag (tagName, tagColour; adds a word to the shared vocabulary without changing the Case, so it needs no version or lease). Occurrence and version identifiers come from pegasus_case_get. Every other act needs the expected case version (present an edit lease token for multi-step work, or omit it and the tool holds the lease for this one command) and ends the lease it ran under.")]
    public async Task<DocumentActionToolResult> ActAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("Caller idempotency key prefixed 'mcp:'; replaying the same key with the same inputs records the act once.")] string operationKey,
        [Description("The act: tag, untag, set_in_report, mark_original_report, remove, retry_custody or create_image_tag.")] string action,
        [Description("The case version the caller observed; required for every act except create_image_tag. A stale value fails closed.")] long? expectedVersion = null,
        [Description("The case-scoped document occurrence acted on (tag, untag, set_in_report, mark_original_report, remove).")] Guid? occurrenceId = null,
        [Description("mark_original_report: the occurrence's current document version.")] Guid? versionId = null,
        [Description("tag and untag: the image tag's identifier.")] Guid? tagId = null,
        [Description("set_in_report: true to put the image in the report, false to take it out.")] bool? inReport = null,
        [Description("Why (case history reason, at most 500 characters); required for remove and retry_custody.")] string? reason = null,
        [Description("retry_custody: case_source (the Case's own Box folder) or audit_reference (the Audit's a. folder).")] string? retryTarget = null,
        [Description("create_image_tag: the new tag's name (at most 40 characters, unique without regard to case).")] string? tagName = null,
        [Description("create_image_tag: the tag's colour, one of Blue, Green, Amber, Navy, Red or Grey.")] string? tagColour = null,
        [Description("Edit lease token from pegasus_edit_begin for multi-step work; omit it and the tool holds the lease for this one command.")] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_document_action",
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
                var actor = context.Actor;

                if (verb == "create_image_tag")
                {
                    if (string.IsNullOrWhiteSpace(tagName))
                    {
                        throw new McpException("The create_image_tag action needs tagName.");
                    }
                    var colour = Enum.TryParse<ImageTagColour>(tagColour?.Trim(), ignoreCase: true, out var parsed)
                        && Enum.IsDefined(parsed)
                        && !int.TryParse(tagColour, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                            ? parsed
                            : throw new McpException(
                                "The create_image_tag action needs tagColour, one of: "
                                + string.Join(", ", Enum.GetNames<ImageTagColour>()) + ".");
                    CreateImageTagResult created;
                    try
                    {
                        created = await createImageTag.ExecuteAsync(new(tagName, colour, actor, key), cancellationToken);
                    }
                    catch (ImageTagNameInUseException)
                    {
                        throw new McpException("An image tag with that name already exists.");
                    }
                    return new DocumentActionToolResult(
                        caseId, verb, null, null, created.Tag.Id, created.IsReplay ? "Replay" : "Created", null,
                        key, AutomationMcpAuditor.CorrelationId(context, key));
                }

                var version = expectedVersion
                    ?? throw new McpException($"The {verb} action needs expectedVersion.");
                Guid Occurrence() => occurrenceId is { } id && id != Guid.Empty
                    ? id
                    : throw new McpException($"The {verb} action needs occurrenceId.");
                Guid Tag() => tagId is { } id && id != Guid.Empty
                    ? id
                    : throw new McpException($"The {verb} action needs tagId.");
                string Reason() => string.IsNullOrWhiteSpace(reason)
                    ? throw new McpException($"The {verb} action needs a reason.")
                    : reason;

                // Every input is checked before the lease is claimed, so a
                // malformed call claims nothing.
                var occurrence = verb == "retry_custody" ? Guid.Empty : Occurrence();
                var tag = verb is "tag" or "untag" ? Tag() : Guid.Empty;
                var reasonText = verb is "remove" or "retry_custody" ? Reason() : string.Empty;
                var putInReport = verb == "set_in_report"
                    ? inReport ?? throw new McpException("The set_in_report action needs inReport.")
                    : false;
                var documentVersion = verb == "mark_original_report"
                    ? versionId is { } documentVersionId && documentVersionId != Guid.Empty
                        ? documentVersionId
                        : throw new McpException("The mark_original_report action needs versionId.")
                    : Guid.Empty;
                var target = verb == "retry_custody"
                    ? retryTarget?.Trim() switch
                    {
                        "case_source" => CustodyTargetKind.CaseSource,
                        "audit_reference" => CustodyTargetKind.AuditReference,
                        _ => throw new McpException("The retry_custody action needs retryTarget: case_source or audit_reference."),
                    }
                    : CustodyTargetKind.CaseSource;

                var retry = await leases.RunCaseAsync(
                    caseId,
                    version,
                    editLeaseToken,
                    actor,
                    key,
                    async token =>
                    {
                        switch (verb)
                        {
                            case "tag":
                                await tagImage.ExecuteAsync(
                                    new(caseId, occurrence, tag, actor, key, version, token), cancellationToken);
                                return null;
                            case "untag":
                                await untagImage.ExecuteAsync(
                                    new(caseId, occurrence, tag, actor, key, version, token), cancellationToken);
                                return null;
                            case "set_in_report":
                                await setImageInReport.ExecuteAsync(
                                    new(caseId, occurrence, putInReport, actor, key, version, token), cancellationToken);
                                return null;
                            case "mark_original_report":
                                await markAsOriginalReport.ExecuteAsync(
                                    new(caseId, version, actor, key, token, occurrence, documentVersion), cancellationToken);
                                return null;
                            case "remove":
                                await removeDocument.ExecuteAsync(
                                    new(caseId, occurrence, actor, reasonText, key, version, token), cancellationToken);
                                return null;
                            default:
                                return (RetryCaseCustodyResult?)await retryCustody.ExecuteAsync(
                                    new(caseId, version, actor, key, reasonText, token, target), cancellationToken);
                        }
                    },
                    cancellationToken);
                var workflow = await workflowQueries.GetAsync(caseId, cancellationToken);
                return new DocumentActionToolResult(
                    caseId,
                    verb,
                    workflow?.Version ?? retry?.CaseVersion,
                    occurrence == Guid.Empty ? null : occurrence,
                    tag == Guid.Empty ? null : tag,
                    retry?.Outcome.ToString() ?? "Recorded",
                    retry?.Message,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }
}
