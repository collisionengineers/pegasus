namespace Pegasus.Core.Intake;

/// <summary>Reads the active Principal Contacts' e-mail from the Contacts directory.</summary>
public interface IPrincipalSenderDirectory
{
    Task<PrincipalSenderContacts> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Which active Principal a sender belongs to by the Contacts directory, for a
/// received item whose sender matched no mail route. A Principal's e-mail on a
/// shared mail domain matches by its full mailbox address, any other by its
/// exact domain, as an intermediary's does. The answer is a Principal only
/// when exactly one matches and the directory does not also make the sender
/// an intermediary for a different one (operator, 11 November 2026; R969).
/// It names a Principal for Create case by the Automation actor; it is not a
/// mail route and classifies nothing.
/// </summary>
public sealed class PrincipalSenderContacts
{
    public static PrincipalSenderContacts Empty { get; } = new([]);

    private readonly ILookup<string, string> principalCodesByIdentity;

    /// <param name="contacts">Each active Principal's directory e-mail with its code.</param>
    public PrincipalSenderContacts(IEnumerable<(string? Email, string PrincipalCode)> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        principalCodesByIdentity = contacts
            .Select(contact => (Identity: IntermediaryDomains.IdentityOf(contact.Email?.Trim()), contact.PrincipalCode))
            .Where(contact => contact.Identity is not null)
            .ToLookup(contact => contact.Identity!, contact => contact.PrincipalCode, StringComparer.OrdinalIgnoreCase);
    }

    public string? SingleFor(string? senderAddress, IntermediaryDomains intermediaries)
    {
        ArgumentNullException.ThrowIfNull(intermediaries);
        var sender = senderAddress?.Trim();
        if (IntermediaryDomains.IdentityOf(sender) is not { } identity)
        {
            return null;
        }

        var codes = principalCodesByIdentity[identity].Distinct(StringComparer.Ordinal).ToArray();
        return codes.Length == 1
            && intermediaries.PrincipalCodesFor(sender!).All(code => string.Equals(code, codes[0], StringComparison.Ordinal))
                ? codes[0]
                : null;
    }
}

/// <summary>
/// The firm's website enquiry form, which e-mails each enquiry from one
/// address. A website enquiry is a private customer's, never a Triage request
/// (operator, 11 November 2026; R972).
/// </summary>
public static class WebsiteEnquiryMail
{
    public const string SenderAddress = "mail@noreply.collisionengineers.co.uk";

    public static bool IsFrom(string? senderAddress) =>
        string.Equals(senderAddress?.Trim(), SenderAddress, StringComparison.OrdinalIgnoreCase);
}
