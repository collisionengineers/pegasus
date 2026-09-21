using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// The figures the Decisions section derives rather than asks for (v28 P30,
/// ruled 20 September 2026). Nothing here is typed in: the Engineer types the
/// reserve they want, and this says what the repair cost implies.
/// </summary>
public static class SettlementPolicy
{
    /// <summary>The step the repair reserve is rounded up to.</summary>
    public const decimal ReserveStep = 50m;

    /// <summary>
    /// The repair reserve the Current repair specification implies: the
    /// VAT-inclusive repair cost rounded up to the next £50. A Case whose
    /// outcome is not Repairable, or which has no repair cost, implies none.
    /// </summary>
    public static decimal? ComputedRepairReserve(decimal? repairCostIncVat, string? outcome)
    {
        if (!string.Equals(outcome, "repairable", StringComparison.Ordinal))
        {
            return null;
        }
        if (repairCostIncVat is not { } cost || cost <= 0m)
        {
            return null;
        }
        return Math.Ceiling(cost / ReserveStep) * ReserveStep;
    }
}

/// <summary>
/// One wording an Engineer may insert into an unroadworthy reason (v28 P15).
/// The bank is the firm's own: a wording saved on one of a Principal's Cases
/// is offered on the rest. The wordings print on the assessment report, so
/// they are report wording rather than screen copy.
/// </summary>
public sealed record UnroadworthyReason(
    Guid Id,
    string PrincipalCode,
    string Text,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc);

public static class UnroadworthyReasonBank
{
    public const int MaximumLength = 300;

    /// <summary>
    /// The wordings every firm starts with, in the operator's own words. A
    /// firm's saved wordings are offered after these.
    /// </summary>
    public static IReadOnlyList<string> Standard { get; } =
    [
        "the steering and suspension geometry has been compromised",
        "the rear lamp assemblies are inoperative",
        "the headlamp assemblies are inoperative",
        "the vehicle presents sharp edges likely to cause injury",
        "the supplementary restraint systems have deployed",
        "structural distortion is evident to the body shell",
        "there is a loss of essential fluids",
    ];

    /// <summary>
    /// The one shape a bank wording takes: trimmed, single-spaced, no
    /// trailing stop and starting lower case, so inserting it after "and"
    /// reads as one sentence.
    /// </summary>
    public static string Normalize(string? text)
    {
        var trimmed = string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        trimmed = trimmed.TrimEnd('.');
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("A bank wording is required.", nameof(text));
        }
        if (trimmed.Length > MaximumLength || trimmed.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"A bank wording cannot exceed {MaximumLength} characters or contain control characters.",
                nameof(text));
        }
        return char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
    }

    /// <summary>Whether the bank already offers this wording, standard or the firm's own.</summary>
    public static bool Offers(IEnumerable<string> saved, string normalized)
    {
        ArgumentNullException.ThrowIfNull(saved);
        return Standard.Concat(saved).Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The reason text after inserting a wording: the first joins as its own
    /// sentence, a later one joins the previous with "and".
    /// </summary>
    public static string Insert(string? current, string wording)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wording);
        var normalized = Normalize(wording);
        var text = string.Join(' ', (current ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Contains(normalized, StringComparison.OrdinalIgnoreCase))
        {
            return current ?? string.Empty;
        }
        return text.Length == 0
            ? char.ToUpperInvariant(normalized[0]) + normalized[1..]
            : text.TrimEnd('.') + " and " + normalized;
    }
}

public sealed record SaveUnroadworthyReasonRequest(
    string PrincipalCode,
    string Text,
    ActionActor Actor);

public interface IUnroadworthyReasonBankStore
{
    Task<IReadOnlyList<UnroadworthyReason>> ListAsync(string principalCode, CancellationToken cancellationToken);

    Task<UnroadworthyReason> AddAsync(SaveUnroadworthyReasonRequest request, string normalized, CancellationToken cancellationToken);
}

public interface ISaveUnroadworthyReason
{
    Task<UnroadworthyReason?> ExecuteAsync(SaveUnroadworthyReasonRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Saving a wording to the firm's bank (v28 P15): an Engineer's act. A
/// wording the bank already offers is not saved twice, and answers null.
/// </summary>
public sealed class SaveUnroadworthyReason(IUnroadworthyReasonBankStore store) : ISaveUnroadworthyReason
{
    public async Task<UnroadworthyReason?> ExecuteAsync(
        SaveUnroadworthyReasonRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        RepairSpecificationPolicy.RequireEngineer(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PrincipalCode);
        var normalized = UnroadworthyReasonBank.Normalize(request.Text);
        var saved = await store.ListAsync(request.PrincipalCode, cancellationToken);
        if (UnroadworthyReasonBank.Offers(saved.Select(item => item.Text), normalized))
        {
            return null;
        }
        return await store.AddAsync(request, normalized, cancellationToken);
    }
}
