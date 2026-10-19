using System.Collections.Frozen;

namespace Pegasus.Core.Intake;

/// <summary>
/// Reads the intermediaries from the Contacts directory: an active Claim Source
/// with an e-mail, linked to an active Principal ("intermediary is claim source
/// now", operator, 15 October 2026).
/// </summary>
public interface IIntermediaryRegistry
{
    Task<IntermediaryDomains> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Which sender is an intermediary for which Principal. A Claim Source's e-mail
/// on a shared mail domain matches by its full mailbox address, any other by its
/// exact domain (operator, 16 October 2026). No subdomain or suffix widening.
/// </summary>
public sealed class IntermediaryDomains
{
    /// <summary>
    /// The shared mail domains: the one list, also the values of the reviewed
    /// register's <c>shared_mail_domain</c> flag.
    /// </summary>
    public static readonly FrozenSet<string> SharedMailDomains = new[]
    {
        "aol.co.uk", "aol.com", "btinternet.com", "gmail.com", "googlemail.com", "hotmail.co.uk",
        "hotmail.com", "icloud.com", "live.co.uk", "live.com", "mail.com", "me.com", "msn.com",
        "outlook.co.uk", "outlook.com", "protonmail.com", "sky.com", "talktalk.net", "tiscali.co.uk",
        "virginmedia.com", "yahoo.co.uk", "yahoo.com", "ymail.com"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static IntermediaryDomains Empty { get; } = new([]);

    private readonly ILookup<string, string> principalCodesByIdentity;

    /// <param name="contacts">Each linked Claim Source's e-mail with one linked Principal's code.</param>
    public IntermediaryDomains(IEnumerable<(string? Email, string PrincipalCode)> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        principalCodesByIdentity = contacts
            .Select(contact => (Identity: Identity(contact.Email?.Trim()), contact.PrincipalCode))
            .Where(contact => contact.Identity is not null)
            .ToLookup(contact => contact.Identity!, contact => contact.PrincipalCode, StringComparer.OrdinalIgnoreCase);
    }

    public bool IsIntermediaryFor(string senderAddress, string principalCode) =>
        Identity(senderAddress) is { } identity
        && principalCodesByIdentity[identity].Contains(principalCode, StringComparer.Ordinal);

    // A blank, malformed or multi-address e-mail has no identity.
    private static string? Identity(string? address) =>
        address is not null && PrincipalMailRoutePolicy.TryGetMailboxDomain(address, out var domain)
            ? SharedMailDomains.Contains(domain) ? address : domain
            : null;
}
