using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class IntakeMcpTools(
    IIntakeReceiptQueries queries,
    IAcceptIntake acceptIntake,
    IInspectionAddressResolutionStore addressResolution,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = "pegasus_intake_list",
        Title = "List intake receipts",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Lists the durable Pegasus intake queue, optionally filtered by its exact decision.")]
    public async Task<IReadOnlyList<IntakeReceiptSummary>> ListAsync(
        [Description("Optional exact intake decision filter.")] IntakeDecision? decision,
        CancellationToken cancellationToken)
    {
        await actorResolver.RequireAsync(StaffMcpPolicies.ReadScope, cancellationToken);
        if (decision is not null && !Enum.IsDefined(decision.Value))
        {
            throw new McpException("The intake decision is not recognized.");
        }

        return await queries.ListAsync(decision, cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_intake_get",
        Title = "Get intake receipt",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one durable intake receipt, including extraction evidence and stable asset identities but not source bytes.")]
    public async Task<IntakeReceipt> GetAsync(
        [Description("The durable intake receipt identifier.")] Guid receiptId,
        CancellationToken cancellationToken)
    {
        await actorResolver.RequireAsync(StaffMcpPolicies.ReadScope, cancellationToken);
        RequireNonEmpty(receiptId, nameof(receiptId));
        return await queries.GetAsync(receiptId, cancellationToken)
            ?? throw new McpException("The intake receipt was not found.");
    }

    [McpServerTool(
        Name = "pegasus_intake_accept",
        Title = "Accept intake and allocate case",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Accepts a reviewed intake receipt through the canonical Core transaction. The inspection address must already be staff-resolved; incomplete or stale evidence fails closed before reference allocation.")]
    public async Task<CaseAcceptanceOutcome> AcceptAsync(
        [Description("The durable intake receipt identifier.")] Guid receiptId,
        [Description("The receipt version observed by the caller.")] long expectedVersion,
        [Description("A caller-generated idempotency identifier for this acceptance.")] Guid operationId,
        [Description("The exact case type confirmed by staff.")] CaseType caseType,
        [Description("The confirmed active principal code.")] string principalCode,
        [Description("Whether the instruction evidence is complete.")] bool instructionComplete,
        [Description("Whether the image evidence is complete.")] bool imagesComplete,
        [Description("Whether staff confirmed the instruction evidence.")] bool instructionConfirmedByStaff,
        [Description("Whether staff confirmed the image evidence.")] bool imagesConfirmedByStaff,
        [Description("Required only for a standalone Audit case.")] AuditAssessment? standaloneAuditAssessment,
        CancellationToken cancellationToken)
    {
        var staff = await actorResolver.RequireAsync(
            StaffMcpPolicies.WriteScope,
            cancellationToken);
        RequireNonEmpty(receiptId, nameof(receiptId));
        RequireNonEmpty(operationId, nameof(operationId));
        if (expectedVersion < 0)
        {
            throw new McpException("The expected receipt version cannot be negative.");
        }
        if (!Enum.IsDefined(caseType))
        {
            throw new McpException("The case type is not recognized.");
        }
        if (standaloneAuditAssessment is not null
            && !Enum.IsDefined(standaloneAuditAssessment.Value))
        {
            throw new McpException("The Audit assessment is not recognized.");
        }

        principalCode = RequireText(principalCode, nameof(principalCode), 64)
            .ToUpperInvariant();
        var address = await addressResolution.GetAsync(receiptId, cancellationToken);
        if (address is null
            || address.State is not InspectionAddressResolutionState.Accepted
                and not InspectionAddressResolutionState.Corrected)
        {
            throw new McpException(
                "The inspection address must be accepted or corrected before case allocation.");
        }
        if (address.ReceiptVersion != expectedVersion)
        {
            throw new McpException(
                "The intake evidence changed; reload the receipt before accepting it.");
        }

        return await acceptIntake.ExecuteAsync(
            new(
                receiptId,
                expectedVersion,
                staff.HistoryActor,
                $"mcp:intake-accept:{operationId:N}",
                caseType,
                principalCode,
                new(
                    instructionComplete,
                    imagesComplete,
                    instructionConfirmedByStaff,
                    imagesConfirmedByStaff),
                standaloneAuditAssessment),
            cancellationToken);
    }

    private static void RequireNonEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new McpException($"'{name}' must be a non-empty identifier.");
        }
    }

    private static string RequireText(string? value, string name, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new McpException(
                $"'{name}' is required and must be no longer than {maximumLength} characters.");
        }

        return normalized;
    }
}
