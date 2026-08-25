using Pegasus.Core.Eva;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Eva;

internal sealed class LocalEvaHandoffProxy(TimeProvider timeProvider) : IEvaHandoffProxy
{
    internal const string AdapterKey = "local-eva-generation-proxy";
    internal const string AdapterVersion = "1";

    public Task<EvaHandoffProxyReceipt> RecordFirstGenerationAsync(
        EvaHandoffProxyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.CaseId == Guid.Empty
            || request.BundleSha256.Length != 64
            || request.BundleSha256.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The EVA generation proxy request is invalid.", nameof(request));
        }

        var recordedAtUtc = timeProvider.GetUtcNow();
        if (recordedAtUtc.Offset != TimeSpan.Zero)
        {
            recordedAtUtc = recordedAtUtc.ToUniversalTime();
        }

        return Task.FromResult(new EvaHandoffProxyReceipt(
            AdapterKey,
            AdapterVersion,
            recordedAtUtc,
            ClaimsExternalDelivery: false,
            ClaimsEngineerAssignment: false));
    }
}
