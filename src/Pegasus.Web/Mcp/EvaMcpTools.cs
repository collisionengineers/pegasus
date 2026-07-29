using System.ComponentModel;
using ModelContextProtocol;
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
    string? JsonSha256);

[McpServerToolType]
internal sealed class EvaMcpTools(
    IEvaHandoffStore store,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = "pegasus_eva_handoff_get",
        Title = "Get EVA handoff readiness",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Gets the focused, offline EVA handoff readiness and selectable custody-confirmed image identities for a case. It makes no EVA network call.")]
    public async Task<EvaHandoffPreparation> GetAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        CancellationToken cancellationToken)
    {
        await actorResolver.RequireAsync(StaffMcpPolicies.ReadScope, cancellationToken);
        RequireNonEmpty(caseId, nameof(caseId));
        return await store.GetPreparationAsync(caseId, cancellationToken)
            ?? throw new McpException("The case or EVA handoff preparation was not found.");
    }

    [McpServerTool(
        Name = "pegasus_eva_handoff_generate",
        Title = "Generate offline EVA handoff",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Generates the deterministic offline EVA handoff bundle through the canonical Core store. It performs no live EVA write and returns hashes rather than bundle bytes.")]
    public async Task<EvaHandoffGenerationReceipt> GenerateAsync(
        [Description("The durable Pegasus case identifier.")] Guid caseId,
        [Description("The case version observed by the caller.")] long expectedCaseVersion,
        [Description("The exact custody-confirmed asset identifiers selected for the handoff.")] IReadOnlyList<Guid> selectedImageIds,
        [Description("A caller-generated idempotency identifier for this generation.")] Guid operationId,
        CancellationToken cancellationToken)
    {
        var staff = await actorResolver.RequireAsync(
            StaffMcpPolicies.WriteScope,
            cancellationToken);
        RequireNonEmpty(caseId, nameof(caseId));
        RequireNonEmpty(operationId, nameof(operationId));
        if (expectedCaseVersion < 0)
        {
            throw new McpException("The expected case version cannot be negative.");
        }
        if (selectedImageIds is null
            || selectedImageIds.Count == 0
            || selectedImageIds.Any(id => id == Guid.Empty)
            || selectedImageIds.Distinct().Count() != selectedImageIds.Count)
        {
            throw new McpException(
                "Select at least one unique, non-empty custody-confirmed image identifier.");
        }

        var result = await store.GenerateAsync(
            new(
                caseId,
                expectedCaseVersion,
                selectedImageIds,
                staff.HistoryActor,
                $"mcp:eva-handoff:{operationId:N}"),
            cancellationToken);
        return new(
            result.Outcome,
            result.Reasons,
            result.Revision,
            result.FirstSentToEngineerRecorded,
            result.Bundle?.FileName,
            result.Bundle?.Sha256,
            result.Bundle?.JsonSha256);
    }

    private static void RequireNonEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new McpException($"'{name}' must be a non-empty identifier.");
        }
    }
}
