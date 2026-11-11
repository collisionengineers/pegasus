using Pegasus.Core.Identity;

namespace Pegasus.Core.Assessment;

/// <summary>
/// One wording an Engineer may insert into an unroadworthy reason (v28 P15).
/// The bank is one list offered on every Case, kept from Administration and
/// added to from a Case (operator, 11 November 2026; R952 to R954). The
/// wordings print on the assessment report, so they are report wording rather
/// than screen copy.
/// </summary>
public sealed record UnroadworthyReason(
    Guid Id,
    string Text,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc);

public static class UnroadworthyReasonBank
{
    public const int MaximumLength = 300;

    /// <summary>
    /// The one shape a bank wording takes: trimmed, single-spaced, no
    /// trailing stop and starting lower case, so inserting it after the
    /// report's opening or after "and" reads as one sentence.
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

    /// <summary>Whether the bank already offers this wording.</summary>
    public static bool Offers(IEnumerable<string> wordings, string normalized)
    {
        ArgumentNullException.ThrowIfNull(wordings);
        return wordings.Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record SaveUnroadworthyReasonRequest(
    string Text,
    ActionActor Actor);

/// <summary>
/// Rewords one bank wording. <see cref="RenderedText"/> is the wording the
/// page showed: a wording reworded or removed since is refused as stale.
/// </summary>
public sealed record RewordUnroadworthyReasonRequest(
    Guid Id,
    string RenderedText,
    string Text,
    ActionActor Actor);

/// <summary>Removes one bank wording, if it still reads as the page showed it.</summary>
public sealed record RemoveUnroadworthyReasonRequest(
    Guid Id,
    string RenderedText,
    ActionActor Actor);

public enum UnroadworthyReasonChange
{
    Done,

    /// <summary>Another wording in the bank already reads this way.</summary>
    AlreadyOffered,

    /// <summary>The wording was reworded or removed after the page rendered it.</summary>
    Stale
}

public interface IUnroadworthyReasonBankStore
{
    /// <summary>The bank, oldest first.</summary>
    Task<IReadOnlyList<UnroadworthyReason>> ListAsync(CancellationToken cancellationToken);

    Task<UnroadworthyReason?> AddAsync(SaveUnroadworthyReasonRequest request, string normalized, CancellationToken cancellationToken);

    Task<UnroadworthyReasonChange> RewordAsync(
        Guid id, string renderedText, string normalized, CancellationToken cancellationToken);

    Task<UnroadworthyReasonChange> RemoveAsync(Guid id, string renderedText, CancellationToken cancellationToken);
}

public interface ISaveUnroadworthyReason
{
    Task<UnroadworthyReason?> ExecuteAsync(SaveUnroadworthyReasonRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Saving a wording to the bank (v28 P15): a casework act, staff's or the
/// Automation actor's, from a Case or from Administration. A wording the bank
/// already offers is not saved twice, and answers null.
/// </summary>
public sealed class SaveUnroadworthyReason(IUnroadworthyReasonBankStore store) : ISaveUnroadworthyReason
{
    public async Task<UnroadworthyReason?> ExecuteAsync(
        SaveUnroadworthyReasonRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        var normalized = UnroadworthyReasonBank.Normalize(request.Text);
        var wordings = await store.ListAsync(cancellationToken);
        if (UnroadworthyReasonBank.Offers(wordings.Select(item => item.Text), normalized))
        {
            return null;
        }
        return await store.AddAsync(request, normalized, cancellationToken);
    }
}

/// <summary>
/// An Administrator rewords a bank wording (operator, 11 November 2026; R953).
/// A reason already recorded on a Case is its own text and does not change.
/// </summary>
public sealed class RewordUnroadworthyReason(IUnroadworthyReasonBankStore store)
{
    public Task<UnroadworthyReasonChange> ExecuteAsync(
        RewordUnroadworthyReasonRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageWorkflowConfiguration);
        RequireRenderedWording(request.Id, request.RenderedText);
        return store.RewordAsync(
            request.Id,
            request.RenderedText,
            UnroadworthyReasonBank.Normalize(request.Text),
            cancellationToken);
    }

    internal static void RequireRenderedWording(Guid id, string renderedText)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(renderedText))
        {
            throw new ArgumentException(
                "A bank wording and the text it was read with are required.", nameof(renderedText));
        }
    }
}

/// <summary>
/// An Administrator removes a bank wording (operator, 11 November 2026; R953).
/// Nothing names a wording but the bank, so the row goes.
/// </summary>
public sealed class RemoveUnroadworthyReason(IUnroadworthyReasonBankStore store)
{
    public Task<UnroadworthyReasonChange> ExecuteAsync(
        RemoveUnroadworthyReasonRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageWorkflowConfiguration);
        RewordUnroadworthyReason.RequireRenderedWording(request.Id, request.RenderedText);
        return store.RemoveAsync(request.Id, request.RenderedText, cancellationToken);
    }
}
