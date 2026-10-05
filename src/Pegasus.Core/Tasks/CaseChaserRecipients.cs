using System.Net.Mail;

namespace Pegasus.Core.Tasks;

/// <summary>
/// The addresses a Case chaser opens addressed to (operator, 5 October 2026):
/// the sender of the instruction that opened the Case, the sender of each
/// image-intake e-mail paired to the Case, and the e-mail on the repairer's
/// directory record. Each is read as recorded; none is a rule about who must
/// be chased, and staff change the list before sending.
/// </summary>
public sealed record CaseChaserRecipients(
    string? OriginalInstructionSender,
    IReadOnlyList<string> ImageSourceSenders,
    string? RepairerEmail)
{
    public static CaseChaserRecipients None { get; } = new(null, [], null);
}

public interface ICaseChaserRecipientQueries
{
    /// <summary>The recorded addresses of <paramref name="caseId"/>, or <see langword="null"/> when the Case does not exist.</summary>
    Task<CaseChaserRecipients?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}

/// <summary>
/// The one owner of how a chaser is addressed from those records: valid
/// addresses only, each once, instruction sender first, then the image
/// sources, then the repairer.
/// </summary>
public static class CaseChaserAddressing
{
    public static IReadOnlyList<string> To(CaseChaserRecipients recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(recipients.ImageSourceSenders);
        var candidates = new List<string?> { recipients.OriginalInstructionSender };
        candidates.AddRange(recipients.ImageSourceSenders);
        candidates.Add(recipients.RepairerEmail);
        return
        [
            .. candidates
                .Select(Valid)
                .OfType<string>()
                .GroupBy(address => address, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
        ];
    }

    private static string? Valid(string? address)
    {
        var trimmed = address?.Trim();
        return string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out _)
            ? null
            : trimmed;
    }
}
