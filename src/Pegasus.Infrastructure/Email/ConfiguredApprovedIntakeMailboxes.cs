using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Email;

/// <summary>
/// The approved estate is the authority for which mailboxes are polled, but the mailbox
/// that is already deployed has no saved identities: its real Graph identities live in
/// deployment configuration, never in this repository, and the Worker holds SELECT-only
/// rights on <c>ApprovedMailboxes</c> so it cannot write them for itself.
/// <para>
/// This decorator closes that gap without a data migration and without a cloud call. A
/// row that carries its own identities is used as saved. A row that carries none is
/// matched by address against the single configured mailbox and, if it matches, polled
/// under exactly the identities the deployment already uses. A row that carries none and
/// matches nothing is skipped, because polling a mailbox nobody has identified is a
/// guess.
/// </para>
/// <para>
/// The database always wins. Configuration is a fallback for the unset case only, never
/// an override, so saving identities is what retires the fallback for that mailbox.
/// </para>
/// </summary>
internal sealed partial class ConfiguredApprovedIntakeMailboxes(
    EfApprovedMailboxStore store,
    IApprovedInboxSourceSettings settings,
    ILogger<ConfiguredApprovedIntakeMailboxes> logger) : IApprovedIntakeMailboxes
{
    // Identity values are exact tenant identifiers and are never logged; the address is,
    // because an operator needs to know which mailbox is unidentified.
    private static readonly HashSet<string> ReportedAddresses = new(StringComparer.Ordinal);
    private static readonly object ReportLock = new();

    public async Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
        CancellationToken cancellationToken)
    {
        var candidates = await store.ListInboundIntakeCandidatesAsync(cancellationToken);
        var configuredAddress = TryNormalize(settings.MailboxAddress);
        var pollable = new List<ApprovedIntakeMailbox>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.MailboxIdentity is { } mailboxIdentity
                && candidate.InboxFolderIdentity is { } inboxFolderIdentity)
            {
                pollable.Add(new(mailboxIdentity, candidate.Address, inboxFolderIdentity));
                continue;
            }

            // Case-insensitively, because the poll store binds an identity to an
            // address the same way. Comparing exactly here would silently drop a
            // mailbox from polling over a difference in case alone.
            if (configuredAddress is not null
                && string.Equals(
                    candidate.Address,
                    configuredAddress,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (ShouldReport(candidate.Address))
                {
                    LogConfiguredFallback(logger, candidate.Address);
                }

                pollable.Add(new(
                    settings.MailboxId,
                    candidate.Address,
                    settings.InboxFolderIdentity));
                continue;
            }

            if (ShouldReport(candidate.Address))
            {
                LogUnidentifiedMailbox(logger, candidate.Address);
            }
        }

        return pollable;
    }

    private static string? TryNormalize(string? address)
    {
        try
        {
            return ApprovedMailboxAddress.Normalize(address!);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static bool ShouldReport(string address)
    {
        lock (ReportLock)
        {
            return ReportedAddresses.Add(address);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Approved mailbox {ApprovedMailboxAddress} has no saved identities; polling it under the deployment's configured mailbox identity.")]
    private static partial void LogConfiguredFallback(ILogger logger, string approvedMailboxAddress);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Approved mailbox {ApprovedMailboxAddress} is not polled: it has no saved mailbox and Inbox folder identity and does not match the configured mailbox.")]
    private static partial void LogUnidentifiedMailbox(ILogger logger, string approvedMailboxAddress);
}
