namespace Pegasus.Core.Identity;

public static class ApprovedMailboxAddress
{
    public static string Normalize(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("An approved mailbox address is required.", nameof(address));
        }

        var normalized = address.Trim().ToLowerInvariant();
        if (normalized.Length > 320
            || normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("Enter a valid mailbox address.", nameof(address));
        }

        var atIndex = normalized.IndexOf('@', StringComparison.Ordinal);
        if (atIndex < 1
            || atIndex != normalized.LastIndexOf('@')
            || atIndex == normalized.Length - 1)
        {
            throw new ArgumentException("Enter a valid mailbox address.", nameof(address));
        }

        var localPart = normalized[..atIndex];
        var domain = normalized[(atIndex + 1)..];
        if (localPart.Length > 64
            || localPart.StartsWith('.')
            || localPart.EndsWith('.')
            || localPart.Contains("..", StringComparison.Ordinal)
            || localPart.Any(character =>
                !(char.IsAsciiLetterOrDigit(character)
                    || ".!#$%&'*+-/=?^_`{|}~".Contains(character))))
        {
            throw new ArgumentException("Enter a valid mailbox address.", nameof(address));
        }

        var labels = domain.Split('.');
        if (domain.Length > 255
            || labels.Any(label =>
                label.Length is < 1 or > 63
                || label.StartsWith('-')
                || label.EndsWith('-')
                || label.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) || character == '-'))))
        {
            throw new ArgumentException("Enter a valid mailbox address.", nameof(address));
        }

        return normalized;
    }
}
