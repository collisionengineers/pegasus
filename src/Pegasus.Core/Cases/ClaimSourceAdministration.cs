using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

public sealed record ClaimSourceRecord(
    Guid Id, string Name, string? ContactName, string? Telephone, string? Email,
    string? Notes, bool Active, long Version, DateTimeOffset UpdatedAtUtc);
public sealed record SaveClaimSourceRequest(
    ActionActor Actor, Guid Id, long ExpectedVersion, string Name, string? ContactName,
    string? Telephone, string? Email, string? Notes, bool Active, string Reason,
    string OperationKey);
public interface IClaimSourceAdministration
{
    Task<ClaimSourceRecord> SaveAsync(SaveClaimSourceRequest request, CancellationToken cancellationToken);
}
public interface IClaimSourceQueries
{
    Task<ClaimSourceRecord?> GetAsync(ActionActor actor, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClaimSourceRecord>> SearchAsync(
        ActionActor actor, string prefix, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// EXT-19: the Claim Source directory. A Claim Source is a linked but
/// distinct record from principal, sender, insurer and third-party engineer
/// (S13/S03) — B copies its accepted id/value/version into the Case, and a
/// later directory edit never rewrites that snapshot.
/// </summary>
public enum ClaimSourceAdministrationError
{
    ClaimSourceNotFound,
    StaleVersion,
    OperationConflict
}

public sealed class ClaimSourceAdministrationException(ClaimSourceAdministrationError error)
    : Exception("The claim source administration request could not be completed.")
{
    public ClaimSourceAdministrationError Error { get; } = error;
}

public static class ClaimSourceAdministrationPolicy
{
    public const int MaximumNameLength = 300;
    public const int MaximumContactNameLength = 200;
    public const int MaximumTelephoneLength = 50;
    public const int MaximumEmailLength = 320;
    public const int MaximumNotesLength = 2000;
    public const int MaximumOperationKeyLength = 100;
    public const int MaximumReasonLength = 500;
    public const int MaximumSearchLimit = 200;

    /// <summary>
    /// A single request shape covers create and edit: the caller mints a new
    /// stable <see cref="SaveClaimSourceRequest.Id"/> and expected version 0
    /// to create, or supplies an existing id and its current version to edit
    /// — the same optimistic-concurrency shape every other directory write in
    /// this file uses, so one Administrator-only, reasoned, idempotent
    /// command serves list/create/edit/disable (EXT-19 item 8).
    /// </summary>
    public static SaveClaimSourceRequest Normalize(SaveClaimSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.Id, nameof(request.Id));
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        return request with
        {
            Name = OrganizationAdministrationPolicy.NormalizeRequiredText(
                request.Name, MaximumNameLength, nameof(request.Name)),
            ContactName = NormalizeOptional(
                request.ContactName, MaximumContactNameLength, nameof(request.ContactName)),
            Telephone = NormalizeOptional(
                request.Telephone, MaximumTelephoneLength, nameof(request.Telephone)),
            Email = NormalizeOptional(request.Email, MaximumEmailLength, nameof(request.Email)),
            Notes = NormalizeOptional(request.Notes, MaximumNotesLength, nameof(request.Notes)),
            Reason = OrganizationAdministrationPolicy.NormalizeRequiredText(
                request.Reason, MaximumReasonLength, nameof(request.Reason)),
            OperationKey = OrganizationAdministrationPolicy.NormalizeRequiredText(
                request.OperationKey, MaximumOperationKeyLength, nameof(request.OperationKey))
        };
    }

    /// <summary>
    /// Reserved and unreachable today (C06 review R-12): <see cref="Normalize"/>'s
    /// single create-or-update <see cref="SaveClaimSourceRequest"/> shape
    /// (assumption 1) means the store never looks a claim source up before
    /// deciding whether to create or update it, so nothing currently calls
    /// this. Kept — with <see cref="ClaimSourceAdministrationError.ClaimSourceNotFound"/> —
    /// for the page models' error-message mapping and for a future store
    /// path (e.g. an explicit edit-only command) that does look one up
    /// first.
    /// </summary>
    public static void RequireFound(bool found)
    {
        if (!found)
        {
            throw new ClaimSourceAdministrationException(
                ClaimSourceAdministrationError.ClaimSourceNotFound);
        }
    }

    public static void RequireCurrentVersion(long currentVersion, long expectedVersion)
    {
        if (currentVersion != expectedVersion)
        {
            throw new ClaimSourceAdministrationException(
                ClaimSourceAdministrationError.StaleVersion);
        }
    }

    private static void RequireAdministrator(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
    }

    private static void RequireIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A stable identifier is required.", parameterName);
        }
    }

    private static void RequireExpectedVersion(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                "The expected version cannot be negative.");
        }
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}
