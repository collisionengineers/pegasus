using System.Net.Mail;

namespace Pegasus.Core.Reports;

/// <summary>The report route a Principal selects for newly entering Review cases.</summary>
public enum PrincipalReportGenerationPolicy
{
    Pegasus,
    EvaZip,
    EvaManualApi,
    EvaAutomaticApiOnReview
}

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

public static class PrincipalReportGenerationPolicyRules
{
    public static bool IsEva(PrincipalReportGenerationPolicy policy) => policy is not PrincipalReportGenerationPolicy.Pegasus;
    public static bool AllowsManualApi(PrincipalReportGenerationPolicy policy) => policy == PrincipalReportGenerationPolicy.EvaManualApi;
    public static bool RequiresAutomaticApiOnReview(PrincipalReportGenerationPolicy policy) => policy == PrincipalReportGenerationPolicy.EvaAutomaticApiOnReview;
}
