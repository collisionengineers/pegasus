using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Address;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class AddressMcpTools(
    IInspectionAddressResolutionStore store,
    StaffMcpActorResolver actorResolver,
    IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(
        Name = "pegasus_inspection_address_get",
        Title = "Get inspection-address review",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Gets the accepted Core inspection-address evidence and current staff-review state for an intake receipt.")]
    public async Task<InspectionAddressResolutionSnapshot> GetAsync(
        [Description("The durable intake receipt identifier.")] Guid receiptId,
        CancellationToken cancellationToken)
    {
        await actorResolver.RequireAsync(StaffMcpPolicies.ReadScope, cancellationToken);
        RequireNonEmpty(receiptId, nameof(receiptId));
        return await store.GetAsync(receiptId, cancellationToken)
            ?? throw new McpException("The inspection-address review was not found.");
    }

    [McpServerTool(
        Name = "pegasus_inspection_address_resolve",
        Title = "Resolve inspection address",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Records the staff decision for the exact inspection-address suggestion and receipt version. A correction must be explicit and the store rejects stale evidence.")]
    public async Task<InspectionAddressResolutionSnapshot> ResolveAsync(
        [Description("The durable intake receipt identifier.")] Guid receiptId,
        [Description("The receipt version observed by the caller.")] long expectedReceiptVersion,
        [Description("The exact suggestion fingerprint returned by the read tool.")] string suggestionFingerprint,
        [Description("AcceptSuggestion or CorrectSuggestion.")] InspectionAddressStaffDecision decision,
        [Description("The corrected physical address or exact 'Image Based Assessment' value when correcting.")] string? correctedValue,
        [Description("A caller-generated idempotency identifier for this decision.")] Guid operationId,
        CancellationToken cancellationToken)
    {
        var staff = await actorResolver.RequireAsync(
            StaffMcpPolicies.WriteScope,
            cancellationToken);
        RequireNonEmpty(receiptId, nameof(receiptId));
        RequireNonEmpty(operationId, nameof(operationId));
        if (expectedReceiptVersion < 0)
        {
            throw new McpException("The expected receipt version cannot be negative.");
        }
        if (!Enum.IsDefined(decision))
        {
            throw new McpException("The inspection-address decision is not recognized.");
        }

        suggestionFingerprint = RequireText(
            suggestionFingerprint,
            nameof(suggestionFingerprint),
            256);
        correctedValue = string.IsNullOrWhiteSpace(correctedValue)
            ? null
            : correctedValue.Trim();
        if (correctedValue?.Length > 500)
        {
            throw new McpException("The corrected inspection address is too long.");
        }
        if (decision == InspectionAddressStaffDecision.CorrectSuggestion
            && correctedValue is null)
        {
            throw new McpException("A corrected value is required when correcting the suggestion.");
        }
        if (decision == InspectionAddressStaffDecision.AcceptSuggestion
            && correctedValue is not null)
        {
            throw new McpException("A corrected value cannot be supplied when accepting the suggestion.");
        }

        return await store.ResolveAsync(
            new(
                receiptId,
                expectedReceiptVersion,
                suggestionFingerprint,
                decision,
                correctedValue,
                staff.Actor,
                operationId,
                httpContextAccessor.HttpContext?.TraceIdentifier
                    ?? $"mcp:{operationId:N}"),
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
