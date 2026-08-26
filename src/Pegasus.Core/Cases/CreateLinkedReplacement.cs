using Pegasus.Core.Identity;
using Pegasus.Core.Custody;

namespace Pegasus.Core.Cases;

/// <summary>
/// The only wrong-principal correction path. The persistence port closes the immutable
/// original, allocates the corrected identity, and records reciprocal linkage in one commit.
/// </summary>
public sealed class CreateLinkedReplacement(
    ILinkedCaseReplacementStore store,
    ICommittedExternalWorkPublisher committedExternalWorkPublisher) : ICreateLinkedReplacement
{
    private readonly ILinkedCaseReplacementStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseAcceptanceOutcome> ExecuteAsync(
        CreateLinkedReplacementRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("The original case identifier is required.", nameof(request));
        }

        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected case version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        RequireText(request.OperationKey, 100, "An operation key is required.", nameof(request));
        RequireText(request.Reason, 500, "A replacement reason is required.", nameof(request));
        RequireText(request.EditLeaseToken, 128, "An active edit lease token is required.", nameof(request));
        var principalCode = CasePrincipalCode.Normalize(request.ReplacementPrincipalCode);

        var outcome = await _store.CreateAsync(
            request with
            {
                OperationKey = request.OperationKey.Trim(),
                Reason = request.Reason.Trim(),
                EditLeaseToken = request.EditLeaseToken.Trim(),
                ReplacementPrincipalCode = principalCode
            },
            cancellationToken);
        if (!outcome.IsDuplicate)
        {
            await committedExternalWorkPublisher.PublishAsync(
                outcome.CustodyWorkId,
                cancellationToken);
        }

        return outcome;
    }

    private static void RequireText(
        string value,
        int maximumLength,
        string message,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
