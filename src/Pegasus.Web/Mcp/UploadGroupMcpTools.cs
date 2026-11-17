using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Mcp;

internal sealed record UploadGroupToolMember(
    int Ordinal,
    Guid StagedReceiptId,
    Guid? ReceiptId,
    string FileName,
    string? QueueStatus,
    bool? CouldNotBeRead,
    long? ReceiptVersion,
    string? Outcome,
    string? OutcomeMessage,
    Guid? UnidentifiedItemId);

internal sealed record UploadGroupToolReceiptVersion(
    [property: Description("A member's receipt identifier.")] Guid ReceiptId,
    [property: Description("The version the member's receipt was reviewed at.")] long Version);

internal sealed record UploadGroupToolDestination(
    Guid CaseId,
    string Reference,
    string? Registration,
    string? Claimant,
    string? Principal,
    string Stage,
    long? Version);

internal sealed record UploadGroupToolDetail(
    Guid GroupId,
    string Channel,
    long Version,
    int ExpectedMemberCount,
    DateTimeOffset ReceivedAtUtc,
    bool IsDiscarded,
    bool ImageMatchDeclined,
    IReadOnlyList<UploadGroupToolMember> Members,
    bool CanAttach,
    IReadOnlyList<UploadGroupToolReceiptVersion> AttachVersions,
    IReadOnlyList<UploadGroupToolDestination> Destinations,
    bool CanDiscard,
    IReadOnlyList<UploadGroupToolReceiptVersion> DiscardVersions,
    bool CanLeave,
    string CorrelationId);

internal sealed record UploadGroupActionToolResult(
    Guid GroupId,
    string Action,
    string? Message,
    Guid? CaseId,
    bool IsReplay,
    UploadGroupToolDetail Group,
    string OperationKey);

/// <summary>
/// A stored upload of several files (staff Upload, v30 Upload E) for the
/// Automation Actor: the upload as its one submission decision reads it
/// (<see cref="UploadGroupState"/>), and that decision's three acts, attach
/// every member to one Case, leave the matched Cases undecided, and discard the
/// upload, through the same commands as the upload page under
/// <c>automation.intake</c>. Any member of staff who does casework may act on
/// any upload, so the Automation Actor may too.
/// </summary>
[McpServerToolType]
internal sealed class UploadGroupMcpTools(
    IIntakeSubmissionGroupStore groups,
    IQueuedIntakeStatusQueries statuses,
    IGetIntake intake,
    IUploadOutcomeQueries outcomeQueries,
    IUploadCaseDecision caseDecision,
    IDeclineImageMatch declineImageMatch,
    DiscardIntakeSubmissionGroup discardSubmission,
    AutomationMcpAuditor auditor)
{
    private const string GroupIdDescription = "The upload's submission group identifier: originId of a SubmissionGroup Unidentified item from pegasus_unidentified_get, or submissionGroupId from pegasus_image_intake_get.";

    [McpServerTool(
        Name = "pegasus_upload_group_get",
        Title = "Get an upload of several files",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets one stored upload as its page reads it: each file's queue status, receipt and outcome; whether every file may now be attached to one Case (canAttach), with the receipt versions attach takes and the Cases the files share as destinations, or with a query, those the search finds; and whether the upload may be discarded (canDiscard), with the receipt versions discard takes.")]
    public async Task<UploadGroupToolDetail> GetAsync(
        [Description(GroupIdDescription)] Guid groupId,
        [Description("Optional search text over reference, registration, claimant and claim number for the Cases every open file may join; omit it for the suggested Cases.")] string? query = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_upload_group_get",
            AutomationMcpAuditor.Resource(groupId),
            async context =>
            {
                var state = await RequireStateAsync(context, groupId, cancellationToken);
                return await DetailAsync(context, state, query, context.TraceIdentifier, cancellationToken);
            },
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_upload_group_action",
        Title = "Decide an upload of several files",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("The one submission decision on a stored upload, as on its page. attach adds every file to one Case or Triage Case: caseId and expectedCaseVersion from pegasus_upload_group_get destinations, and receiptVersions, every file's receipt with the version attachVersions gives; refused unless canAttach. leave leaves the matched Cases undecided, so the images register as a Vehicle images record. discard retains the upload but discards it as a terminal decision: expectedGroupVersion and receiptVersions from discardVersions; refused unless canDiscard. Requires a mcp:-prefixed operation key; an attach sent again under it carries on from the files it already added.")]
    public async Task<UploadGroupActionToolResult> ActAsync(
        [Description(GroupIdDescription)] Guid groupId,
        [Description("attach, leave or discard.")] string action,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("attach only: the Case or Triage Case identifier.")] Guid? caseId = null,
        [Description("attach only: the destination's version from pegasus_upload_group_get.")] long? expectedCaseVersion = null,
        [Description("discard only: the upload's version from pegasus_upload_group_get.")] long? expectedGroupVersion = null,
        [Description("attach and discard: every file's receipt with its reviewed version, from attachVersions or discardVersions.")] IReadOnlyList<UploadGroupToolReceiptVersion>? receiptVersions = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallAsync(
            "pegasus_upload_group_action",
            AutomationMcpAuditor.Resource(groupId),
            operationKey,
            async (context, key) =>
            {
                var correlation = AutomationMcpAuditor.CorrelationId(context, key);
                var state = await RequireStateAsync(context, groupId, cancellationToken);
                var versions = receiptVersions?.ToDictionary(item => item.ReceiptId, item => item.Version);
                switch (action?.Trim())
                {
                    case "attach":
                    {
                        if (expectedGroupVersion is not null)
                        {
                            throw new McpException("attach takes caseId, expectedCaseVersion and receiptVersions.");
                        }

                        var target = AutomationMcpErrors.RequireId(
                            caseId ?? throw new McpException("attach needs caseId."), "case identifier");
                        var caseVersion = expectedCaseVersion ?? throw new McpException("attach needs expectedCaseVersion.");
                        if (!state.ReadyForAttachment || !state.TryGetRoster(versions, out var roster))
                        {
                            throw new McpException(UploadGroupState.AttachIncompleteMessage);
                        }

                        var attached = await caseDecision.AttachGroupAsync(
                            state.Group.Id,
                            roster,
                            target,
                            null,
                            AutomationMcpErrors.DeriveOperationGuid("upload-attach-group", context.Actor, key),
                            versions!,
                            caseVersion,
                            context.Actor,
                            cancellationToken);
                        if (!attached.Succeeded)
                        {
                            throw new McpException(attached.Message);
                        }

                        return await ResultAsync(context, groupId, "attach", attached.Message, attached.CaseId, isReplay: false, key, correlation, cancellationToken);
                    }
                    case "leave":
                    {
                        if (caseId is not null || expectedCaseVersion is not null
                            || expectedGroupVersion is not null || receiptVersions is not null)
                        {
                            throw new McpException("leave takes no other parameter.");
                        }

                        // Leave on an upload already left is answered as done (R1110).
                        if (!state.AlreadyLeft && !state.OffersLeave)
                        {
                            throw new McpException(
                                "leave is not offered on this upload; pegasus_upload_group_get says whether it is.");
                        }

                        await declineImageMatch.ExecuteAsync(
                            state.Group.Id, state.OffersLeave, state.LeaveFiles, context.Actor, cancellationToken);
                        return await ResultAsync(context, groupId, "leave", null, null, isReplay: false, key, correlation, cancellationToken);
                    }
                    case "discard":
                    {
                        if (caseId is not null || expectedCaseVersion is not null)
                        {
                            throw new McpException("discard takes expectedGroupVersion and receiptVersions.");
                        }

                        var groupVersion = expectedGroupVersion ?? throw new McpException("discard needs expectedGroupVersion.");
                        if (!state.CanDiscard || versions is null || !state.HasCurrentDiscardRoster(groupVersion, versions))
                        {
                            throw new McpException(UploadGroupState.DiscardChangedMessage);
                        }

                        var discarded = await discardSubmission.ExecuteAsync(
                            new(
                                state.Group.Id,
                                groupVersion,
                                versions,
                                context.Actor,
                                AutomationMcpErrors.DeriveOperationGuid("upload-discard-group", context.Actor, key)),
                            cancellationToken);
                        if (!discarded.Succeeded)
                        {
                            throw new McpException(discarded.Message);
                        }

                        return await ResultAsync(context, groupId, "discard", discarded.Message, null, discarded.IsReplay, key, correlation, cancellationToken);
                    }
                    default:
                        throw new McpException("action must be attach, leave or discard.");
                }
            },
            cancellationToken);
    }

    private async Task<UploadGroupActionToolResult> ResultAsync(
        AutomationActorContext context,
        Guid groupId,
        string action,
        string? message,
        Guid? caseId,
        bool isReplay,
        string key,
        string correlation,
        CancellationToken cancellationToken)
    {
        var state = await RequireStateAsync(context, groupId, cancellationToken);
        return new UploadGroupActionToolResult(
            groupId,
            action,
            message,
            caseId,
            isReplay,
            await DetailAsync(context, state, query: null, correlation, cancellationToken),
            key);
    }

    private async Task<UploadGroupState> RequireStateAsync(
        AutomationActorContext context,
        Guid groupId,
        CancellationToken cancellationToken) =>
        await UploadGroupState.LoadAsync(
            groups,
            statuses,
            intake,
            outcomeQueries,
            AutomationMcpErrors.RequireId(groupId, "upload group identifier"),
            context.Actor,
            cancellationToken)
        ?? throw new McpException("The upload was not found.");

    /// <summary>
    /// The upload as its page shows the decision: the Cases every open file
    /// shares are offered only while the whole upload awaits that one decision.
    /// </summary>
    private async Task<UploadGroupToolDetail> DetailAsync(
        AutomationActorContext context,
        UploadGroupState state,
        string? query,
        string correlation,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UploadCaseSuggestion> destinations = !state.AwaitsDecision
            ? []
            : string.IsNullOrWhiteSpace(query)
                ? await caseDecision.GetSuggestionsForUploadsAsync(state.OpenMemberReceiptIds, context.Actor, cancellationToken)
                : await caseDecision.SearchForUploadsAsync(state.OpenMemberReceiptIds, query.Trim(), context.Actor, cancellationToken);
        return new UploadGroupToolDetail(
            state.Group.Id,
            state.Group.Channel.ToString(),
            state.Group.Version,
            state.Group.ExpectedMemberCount,
            state.Group.ReceivedAtUtc,
            state.Group.Discard is not null,
            state.Group.ImageMatchDeclined is not null,
            state.Members
                .OrderBy(member => member.Member.Ordinal)
                .Select(member => new UploadGroupToolMember(
                    member.Member.Ordinal,
                    member.Member.StagedReceiptId,
                    member.Status?.ProcessedReceiptId ?? member.Status?.StagedReceiptId,
                    member.Member.SourceFileName,
                    member.Status?.Status.ToString(),
                    member.Member.CouldNotBeRead,
                    member.Receipt?.Version,
                    member.Outcome?.Kind.ToString(),
                    member.Outcome?.Message,
                    member.Outcome?.UnidentifiedItemId))
                .ToArray(),
            state.ReadyForAttachment,
            Versions(state.GroupMemberReceiptIds, state.OpenMemberReceiptVersions),
            destinations.Select(item => new UploadGroupToolDestination(
                item.CaseId,
                item.Reference,
                item.Registration,
                item.Claimant,
                item.Principal,
                item.Stage,
                item.Version)).ToArray(),
            state.CanDiscard,
            Versions(state.GroupMemberReceiptIds, state.GroupMemberReceiptVersions),
            state.OffersLeave,
            correlation);
    }

    /// <summary>The versions in the upload's own member order.</summary>
    private static UploadGroupToolReceiptVersion[] Versions(
        IReadOnlyList<Guid> order,
        IReadOnlyDictionary<Guid, long> versions) =>
        order
            .Where(versions.ContainsKey)
            .Select(receiptId => new UploadGroupToolReceiptVersion(receiptId, versions[receiptId]))
            .ToArray();
}
