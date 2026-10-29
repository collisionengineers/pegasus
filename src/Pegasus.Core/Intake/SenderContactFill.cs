using System.Text.RegularExpressions;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// The Case contact a new Case takes from its instruction e-mail (operator,
/// 28 October 2026; R462): the sender's name and e-mail first, a telephone
/// number from the sender's signature second. The sender is the mail route's
/// effective sender (<see cref="PrincipalMailRoutePolicy"/>). For a staff
/// forward that is the forwarded original's address, and the outer From name
/// and signature are the forwarder's, so only the e-mail is read. The values
/// fill a contact field only where the Case holds neither an extracted fact
/// nor a confirmed value, the fill rule
/// <see cref="Pegasus.Core.Vehicle.VehicleLookupFillPolicy.Fills"/> states,
/// so what the instruction or staff recorded is never replaced.
/// </summary>
public static partial class SenderContactFill
{
    public const string PolicyKey = "sender-contact-fill";
    public const int PolicyVersion = 1;

    /// <summary>
    /// The Case-data values the e-mail offers, keyed by field name. Nothing
    /// when the route proved no effective sender. A value longer than its
    /// Case-data limit is left out rather than cut.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CaseDataValues(
        string? effectiveSenderAddress,
        string? senderAddress,
        string? senderDisplayName,
        string? bodyPlainText)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var address = effectiveSenderAddress?.Trim();
        if (string.IsNullOrEmpty(address) || address.Length > CaseDataLimits.EmailAddress)
        {
            return values;
        }

        values[CaseDataFieldNames.ContactEmailAddress] = address;
        if (!string.Equals(senderAddress?.Trim(), address, StringComparison.OrdinalIgnoreCase))
        {
            return values;
        }

        var name = senderDisplayName?.Trim();
        if (!string.IsNullOrEmpty(name)
            && name.Length <= CaseDataLimits.PersonName
            && !string.Equals(name, address, StringComparison.OrdinalIgnoreCase))
        {
            values[CaseDataFieldNames.ContactName] = name;
        }

        if (SignatureTelephone(bodyPlainText) is { } telephone)
        {
            values[CaseDataFieldNames.ContactPhoneNumber] = telephone;
        }

        return values;
    }

    /// <summary>
    /// The first UK telephone number printed below the sender's sign-off line
    /// ("Kind regards", "Thanks", "Yours faithfully" and the like, the
    /// sign-offs the corpus instructions use), in the sender's own message:
    /// anything from a forwarded or quoted header block on is an earlier
    /// message (<see cref="StaffForwardBodyCleaner.ForwardedHeaderPattern"/>).
    /// A number above the sign-off is the message's own content, such as the
    /// claimant's, and is never read. Null when there is no sign-off or no
    /// number below it.
    /// </summary>
    public static string? SignatureTelephone(string? bodyPlainText)
    {
        if (string.IsNullOrWhiteSpace(bodyPlainText))
        {
            return null;
        }

        var text = bodyPlainText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        var earlierMessage = StaffForwardBodyCleaner.ForwardedHeaderPattern.Match(text);
        if (earlierMessage.Success)
        {
            text = text[..earlierMessage.Index];
        }

        var lines = text.Split('\n');
        var signOff = Array.FindIndex(lines, line => SignOffRegex().IsMatch(line));
        if (signOff < 0)
        {
            return null;
        }

        for (var index = signOff + 1; index < lines.Length; index++)
        {
            // Outlook prints a decorated link target beside the number
            // ("0800 093 0982<tel:0800%20093%200982>") and image placeholders
            // in brackets; neither is part of the printed number.
            var match = TelephoneRegex().Match(DecorationRegex().Replace(lines[index], string.Empty));
            if (match.Success && match.Value.Length <= CaseDataLimits.Telephone)
            {
                return match.Value;
            }
        }

        return null;
    }

    [GeneratedRegex(
        @"(?i)^\s*(?:kind regards|regards|best regards|many thanks|thanks|thank you|yours faithfully|yours sincerely)\s*[,.!]?\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SignOffRegex();

    // A UK number: +44 (optionally "(0)") or 0, then a non-zero digit and
    // nine or ten digits in all, spaced or hyphenated within one line.
    [GeneratedRegex(
        @"(?<![\d+])(?:\+44[ \t]?(?:\(0\)[ \t]?)?|0)[1-9](?:[ \t-]?\d){8,9}(?!\d)",
        RegexOptions.CultureInvariant)]
    private static partial Regex TelephoneRegex();

    [GeneratedRegex(@"<[^<>]*>|\[[^\[\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex DecorationRegex();
}
