using System.Net.Mail;

namespace Pegasus.Core.Reports;

/// <summary>Principal-owned suggestions for a staff-controlled report delivery.</summary>
public sealed record PrincipalReportRecipientSettings(
    bool IncludeOriginalInstructionSender,
    IReadOnlyList<string> AdditionalAddresses)
{
    public static PrincipalReportRecipientSettings None { get; } = new(false, []);

    public bool Equals(PrincipalReportRecipientSettings? other) =>
        other is not null
        && IncludeOriginalInstructionSender == other.IncludeOriginalInstructionSender
        && AdditionalAddresses.SequenceEqual(other.AdditionalAddresses, StringComparer.Ordinal);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IncludeOriginalInstructionSender);
        foreach (var address in AdditionalAddresses)
        {
            hash.Add(address, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }

    public static PrincipalReportRecipientSettings Normalize(
        bool includeOriginalInstructionSender,
        IEnumerable<string>? additionalAddresses)
    {
        var addresses = (additionalAddresses ?? [])
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (addresses.Any(address => !MailAddress.TryCreate(address, out _)))
        {
            throw new ArgumentException("Each report recipient must be a valid e-mail address.", nameof(additionalAddresses));
        }

        return new(includeOriginalInstructionSender, addresses);
    }
}
