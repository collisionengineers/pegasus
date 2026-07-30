using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Eva;

namespace Pegasus.Web.Mcp;

internal sealed record EvaHandoffGenerationReceipt(
    GenerateEvaHandoffOutcome Outcome,
    IReadOnlyList<string> Reasons,
    int? Revision,
    bool FirstSentToEngineerRecorded,
    string? FileName,
    string? Sha256,
    string? JsonSha256,
    string? ProvenanceSha256,
    bool IsTruncated);

[McpServerToolType]
internal sealed class ReportsGenerateEvaMcpTool(
    IGenerateEvaHandoff generateEvaHandoff,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.ReportsGenerateEva,
        Title = "Generate EVA handoff",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Generates the deterministic manual EVA bundle from accepted case evidence; it makes no EVA network call.")]
    public Task<StaffMcpResult<EvaHandoffGenerationReceipt>> ExecuteAsync(
        Guid caseId,
        long expectedCaseVersion,
        Guid overviewImageOccurrenceId,
        Guid mainDamageImageOccurrenceId,
        IReadOnlyList<Guid> orderedImageOccurrenceIds,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireIdentifier(
                overviewImageOccurrenceId,
                nameof(overviewImageOccurrenceId));
            StaffMcpInput.RequireIdentifier(
                mainDamageImageOccurrenceId,
                nameof(mainDamageImageOccurrenceId));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireText(reason, nameof(reason), 300);
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            ArgumentNullException.ThrowIfNull(orderedImageOccurrenceIds);
            if (overviewImageOccurrenceId == mainDamageImageOccurrenceId
                || orderedImageOccurrenceIds.Count is < 2 or > 100
                || orderedImageOccurrenceIds.Any(id => id == Guid.Empty)
                || orderedImageOccurrenceIds.Distinct().Count() != orderedImageOccurrenceIds.Count
                || !orderedImageOccurrenceIds.Contains(overviewImageOccurrenceId)
                || !orderedImageOccurrenceIds.Contains(mainDamageImageOccurrenceId))
            {
                throw new ModelContextProtocol.McpException(
                    "Select distinct previews within a unique image order of 2 through 100 items.");
            }

            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            var result = await generateEvaHandoff.ExecuteAsync(
                new(
                    caseId,
                    expectedCaseVersion,
                    overviewImageOccurrenceId,
                    mainDamageImageOccurrenceId,
                    orderedImageOccurrenceIds,
                    staff.Actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken);
            return new EvaHandoffGenerationReceipt(
                result.Outcome,
                result.Reasons.Take(100).ToArray(),
                result.Revision,
                result.FirstSentToEngineerRecorded,
                result.Bundle?.FileName,
                result.Bundle?.Sha256,
                result.Bundle?.JsonSha256,
                result.Bundle?.ProvenanceSha256,
                result.Reasons.Count > 100);
        });
}
