using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// DevelopmentOffline has no Graph tenant to resolve an address against, so this fake
/// derives a stable identity from the normalized address instead — adding a mailbox
/// locally always succeeds. A test that needs to exercise the honest resolution-failure
/// path substitutes its own <see cref="IResolveApprovedMailboxIdentity"/> (see
/// IntakeWebApplicationFactory's optional-override convention) rather than this fake
/// simulating failure.
/// </summary>
public sealed class LocalApprovedMailboxIdentityResolver : IResolveApprovedMailboxIdentity,
    ICheckApprovedMailboxAccess
{
    public Task<ApprovedMailboxIdentityResolution?> ResolveAsync(
        string address,
        CancellationToken cancellationToken)
    {
        var normalized = ApprovedMailboxAddress.Normalize(address);
        var stem = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..24];
        return Task.FromResult<ApprovedMailboxIdentityResolution?>(new(
            $"local-mailbox-{stem}",
            $"local-inbox-{stem}",
            $"local-sent-{stem}",
            MailLogicalFolders.All
                .Select(folder => new ApprovedMailboxFolderBinding(
                    folder.Type,
                    $"local-{folder.Key}-{stem}"))
                .ToArray()));
    }

    public Task<bool> CanReadInboxAsync(
        ApprovedMailboxIdentityResolution mailbox,
        CancellationToken cancellationToken) => Task.FromResult(true);
}
