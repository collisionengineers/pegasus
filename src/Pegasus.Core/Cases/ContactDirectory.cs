using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

/// <summary>Directory roles for external organisations. A Principal remains a
/// policy-bearing record; the other roles may be associated with it.</summary>
public enum ContactRole
{
    Principal,
    ClaimSource,
    Repairer,
    Storage,
    ThirdPartyEngineer
}

public enum ContactSort
{
    Name,
    Type,
    LastCase
}

public sealed record ContactDirectoryRecord(
    Guid OrganizationId,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Telephone,
    string? Address,
    string? Postcode,
    bool Active,
    IReadOnlyList<ContactRole> Roles,
    long Version,
    DateTimeOffset? LastCaseReceivedAtUtc,
    string? LastCaseReference,
    IReadOnlyList<ContactPrincipalAssociation>? PrincipalAssociations = null,
    IReadOnlyList<Guid>? OwnPrincipalIds = null,
    string? GuidanceTemplate = null,
    long GuidanceTemplateVersion = 0);

public sealed record ContactPrincipalAssociation(
    Guid OrganizationId,
    ContactRole Role,
    Guid PrincipalId);

public sealed record ContactDirectoryQuery(
    ActionActor Actor,
    ContactRole? Role,
    ContactSort Sort,
    string? Search = null,
    int Limit = 200);

public sealed record SaveContactRequest(
    ActionActor Actor,
    Guid OrganizationId,
    long ExpectedVersion,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Telephone,
    string? Address,
    string? Postcode,
    bool Active,
    IReadOnlyList<ContactRole> Roles,
    string? PrincipalCode,
    CaseInspectionMode PrincipalInspectionMode,
    IReadOnlyList<ContactPrincipalAssociation> PrincipalAssociations,
    string OperationKey,
    string EditLeaseToken,
    string? GuidanceTemplate = null,
    long GuidanceTemplateVersion = 0);

public interface IContactDirectoryQueries
{
    Task<ContactDirectoryRecord?> GetAsync(ActionActor actor, Guid organizationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContactDirectoryRecord>> ListAsync(ContactDirectoryQuery query, CancellationToken cancellationToken);
    /// <summary>
    /// Every active directory organisation holding one role, for the case
    /// screens that let a member of staff link one to a Case. Casework
    /// authority, not directory administration.
    /// </summary>
    Task<IReadOnlyList<ContactDirectoryRecord>> ListByRoleAsync(
        ActionActor actor, ContactRole role, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContactDirectoryRecord>> FindPossibleMatchesAsync(
        ActionActor actor, string name, CancellationToken cancellationToken);
    Task<IReadOnlyList<PrincipalAdministrationDetails>> ListPrincipalChoicesAsync(
        ActionActor actor, CancellationToken cancellationToken);
}

public interface IContactDirectoryAdministration
{
    Task<ContactDirectoryRecord> SaveAsync(SaveContactRequest request, CancellationToken cancellationToken);
}

public enum ContactDirectoryError
{
    ContactNotFound,
    DuplicateOrganizationName,
    DuplicatePrincipalCode,
    PrincipalNotFound,
    InvalidPrincipalAssociation,
    StaleVersion,
    OperationConflict
}

public sealed class ContactDirectoryException(ContactDirectoryError error)
    : Exception("The contact could not be saved.")
{
    public ContactDirectoryError Error { get; } = error;
}

public static class ContactDirectoryPolicy
{
    public const int MaximumNameLength = 300;
    public const int MaximumContactPersonLength = 200;
    public const int MaximumEmailLength = 320;
    public const int MaximumTelephoneLength = 50;
    public const int MaximumAddressLength = 1000;
    public const int MaximumPostcodeLength = 20;
    public const int MaximumGuidanceTemplateLength = 4000;
    public const int MaximumOperationKeyLength = 100;
    public const int MaximumListLimit = 200;

    public static SaveContactRequest Normalize(SaveContactRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (request.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException("A contact identifier is required.", nameof(request));
        }
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The expected version cannot be negative.");
        }

        var roles = request.Roles.Distinct().OrderBy(role => role).ToArray();
        if (roles.Length == 0)
        {
            throw new ArgumentException("Select at least one contact type.", nameof(request));
        }
        if (roles.Contains(ContactRole.Principal) && string.IsNullOrWhiteSpace(request.PrincipalCode))
        {
            throw new ArgumentException("A principal code is required for a principal contact.", nameof(request));
        }
        if (!roles.Contains(ContactRole.Principal) && !string.IsNullOrWhiteSpace(request.PrincipalCode))
        {
            throw new ArgumentException("A principal code is only valid for a principal contact.", nameof(request));
        }
        var associations = request.PrincipalAssociations ?? [];
        if (associations.Any(association => association.OrganizationId != request.OrganizationId
                || association.PrincipalId == Guid.Empty
                || association.Role == ContactRole.Principal
                || !roles.Contains(association.Role))
            || associations.DistinctBy(association => (association.Role, association.PrincipalId)).Count()
                != associations.Count)
        {
            throw new ArgumentException(
                "Each linked principal must be selected once for one selected non-principal type.",
                nameof(request));
        }
        if (associations.Count > 0 && roles.All(role => role == ContactRole.Principal))
        {
            throw new ArgumentException("Select a non-principal type before linking a contact to a principal.", nameof(request));
        }

        return request with
        {
            Name = Required(request.Name, MaximumNameLength, nameof(request.Name)),
            ContactPerson = Optional(request.ContactPerson, MaximumContactPersonLength, nameof(request.ContactPerson)),
            Email = Optional(request.Email, MaximumEmailLength, nameof(request.Email)),
            Telephone = Optional(request.Telephone, MaximumTelephoneLength, nameof(request.Telephone)),
            Address = Optional(request.Address, MaximumAddressLength, nameof(request.Address)),
            Postcode = Optional(request.Postcode, MaximumPostcodeLength, nameof(request.Postcode)),
            GuidanceTemplate = Optional(request.GuidanceTemplate, MaximumGuidanceTemplateLength, nameof(request.GuidanceTemplate)),
            Roles = roles,
            PrincipalAssociations = associations,
            PrincipalCode = string.IsNullOrWhiteSpace(request.PrincipalCode)
                ? null : OrganizationAdministrationPolicy.NormalizePrincipalCode(request.PrincipalCode),
            OperationKey = Required(request.OperationKey, MaximumOperationKeyLength, nameof(request.OperationKey)),
            EditLeaseToken = string.IsNullOrWhiteSpace(request.EditLeaseToken)
                ? string.Empty
                : Required(request.EditLeaseToken, 200, nameof(request.EditLeaseToken))
        };
    }

    internal static string Required(string value, int max, string name) =>
        OrganizationAdministrationPolicy.NormalizeRequiredText(value, max, name);

    internal static string? Optional(string? value, int max, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, max, name);
}
