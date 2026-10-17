using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

/// <summary>The one type of an external organisation, chosen when it is added
/// and never changed. A Principal remains a policy-bearing record; a contact of
/// any other type may be linked to Principals.</summary>
public enum ContactRole
{
    Principal,
    ClaimSource,
    Repairer,
    Storage,
    ThirdPartyEngineer
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
    ContactRole Role,
    long Version,
    DateTimeOffset? LastCaseReceivedAtUtc,
    string? LastCaseReference,
    IReadOnlyList<Guid>? LinkedPrincipalIds = null,
    IReadOnlyList<Guid>? OwnPrincipalIds = null,
    string? NotesOnEveryCase = null,
    string? PrincipalCode = null,
    CaseInspectionMode? PrincipalInspectionMode = null);

public sealed record ContactDirectoryQuery(
    ActionActor Actor,
    ContactRole Role,
    string? Search = null,
    int PageNumber = 1);

/// <summary>One page of one type's contacts, in name order. <paramref name="HasNextPage"/>
/// is true only when a further contact follows this page.</summary>
public sealed record ContactDirectoryPage(
    IReadOnlyList<ContactDirectoryRecord> Items,
    int PageNumber,
    bool HasNextPage);

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
    ContactRole Role,
    string? PrincipalCode,
    CaseInspectionMode PrincipalInspectionMode,
    IReadOnlyList<Guid> LinkedPrincipalIds,
    string OperationKey,
    string? NotesOnEveryCase = null);

public interface IContactDirectoryQueries
{
    Task<ContactDirectoryRecord?> GetAsync(ActionActor actor, Guid organizationId, CancellationToken cancellationToken);
    Task<ContactDirectoryPage> ListAsync(ContactDirectoryQuery query, CancellationToken cancellationToken);
    /// <summary>
    /// Every active directory organisation holding one role, for the case
    /// screens that let a member of staff link one to a Case. Casework
    /// authority, not directory administration.
    /// </summary>
    Task<IReadOnlyList<ContactDirectoryRecord>> ListByRoleAsync(
        ActionActor actor, ContactRole role, CancellationToken cancellationToken);
    Task<IReadOnlyList<ContactDirectoryRecord>> FindPossibleMatchesAsync(
        ActionActor actor, string name, CancellationToken cancellationToken);
    /// <summary>
    /// The Principals a contact may link: every active one and, for
    /// <paramref name="contactId"/>, the inactive ones it already links, so a
    /// save of that contact keeps them (operator, 16 October 2026).
    /// </summary>
    Task<IReadOnlyList<PrincipalAdministrationDetails>> ListPrincipalChoicesAsync(
        ActionActor actor, Guid? contactId, CancellationToken cancellationToken);
}

public static class ContactDirectoryQueriesExtensions
{
    /// <summary>
    /// The active directory organisation holding <paramref name="role"/> with
    /// this identifier, or null when there is none, for the case screens that
    /// link one to a Case. Each caller keeps its own refusal.
    /// </summary>
    public static async Task<ContactDirectoryRecord?> FindActiveByRoleAsync(
        this IContactDirectoryQueries queries,
        ActionActor actor,
        ContactRole role,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        (await queries.ListByRoleAsync(actor, role, cancellationToken))
        .SingleOrDefault(item => item.OrganizationId == organizationId);
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
    InvalidPrincipalAssociation,
    StaleVersion,
    OperationConflict
}

public sealed class ContactDirectoryException(ContactDirectoryError error)
    : Exception("The contact could not be saved.")
{
    public ContactDirectoryError Error { get; } = error;
}

public static partial class ContactDirectoryPolicy
{
    public const int MaximumNameLength = 300;
    public const int MaximumContactPersonLength = 200;
    public const int MaximumEmailLength = 320;
    public const int MaximumTelephoneLength = 50;
    public const int MaximumAddressLength = 1000;
    public const int MaximumPostcodeLength = 20;
    public const int MaximumNotesOnEveryCaseLength = 2000;
    public const int MaximumOperationKeyLength = 100;
    public const int PageSize = 50;
    public const int MaximumPage = int.MaxValue / PageSize;

    /// <summary>The page a list is asked for, held within 1 to <see cref="MaximumPage"/>
    /// so the offset of its first row always fits an <see cref="int"/>.</summary>
    public static int NormalizePage(int pageNumber) => Math.Clamp(pageNumber, 1, MaximumPage);

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

        if (!Enum.IsDefined(request.Role))
        {
            throw new ArgumentException("Select a contact type.", nameof(request));
        }
        var isPrincipal = request.Role == ContactRole.Principal;
        if (isPrincipal && string.IsNullOrWhiteSpace(request.PrincipalCode))
        {
            throw new ArgumentException("A principal code is required for a principal contact.", nameof(request));
        }
        if (!isPrincipal && !string.IsNullOrWhiteSpace(request.PrincipalCode))
        {
            throw new ArgumentException("A principal code is only valid for a principal contact.", nameof(request));
        }
        var linkedPrincipalIds = request.LinkedPrincipalIds ?? [];
        if (linkedPrincipalIds.Any(id => id == Guid.Empty) || linkedPrincipalIds.Distinct().Count() != linkedPrincipalIds.Count)
        {
            throw new ArgumentException("Each linked principal must be selected once.", nameof(request));
        }
        if (linkedPrincipalIds.Count > 0 && isPrincipal)
        {
            throw new ArgumentException("A principal contact cannot link to a principal.", nameof(request));
        }

        return request with
        {
            Name = Required(request.Name, MaximumNameLength, nameof(request.Name)),
            ContactPerson = Optional(request.ContactPerson, MaximumContactPersonLength, nameof(request.ContactPerson)),
            Email = Optional(request.Email, MaximumEmailLength, nameof(request.Email)),
            Telephone = OptionalTelephone(request.Telephone, MaximumTelephoneLength, nameof(request.Telephone)),
            Address = Optional(request.Address, MaximumAddressLength, nameof(request.Address)),
            Postcode = Optional(request.Postcode, MaximumPostcodeLength, nameof(request.Postcode)),
            // Only a Principal or Claim source record carries notes on every Case (decided
            // 13 September); for any other contact type the field is absent, not kept.
            NotesOnEveryCase = isPrincipal || request.Role == ContactRole.ClaimSource
                ? Optional(request.NotesOnEveryCase, MaximumNotesOnEveryCaseLength, nameof(request.NotesOnEveryCase))
                : null,
            LinkedPrincipalIds = linkedPrincipalIds,
            PrincipalCode = string.IsNullOrWhiteSpace(request.PrincipalCode)
                ? null : OrganizationAdministrationPolicy.NormalizePrincipalCode(request.PrincipalCode),
            OperationKey = Required(request.OperationKey, MaximumOperationKeyLength, nameof(request.OperationKey))
        };
    }

    internal static string Required(string value, int max, string name) =>
        OrganizationAdministrationPolicy.NormalizeRequiredText(value, max, name);

    internal static string? Optional(string? value, int max, string name) =>
        string.IsNullOrWhiteSpace(value) ? null : Required(value, max, name);

    /// <summary>
    /// UK numbers start with 0 and are commonly written with spaces (D7): a
    /// telephone is trimmed, its internal whitespace collapsed to single
    /// spaces, and then accepted only as digits, spaces and one optional
    /// leading <c>+</c>. Letters and other punctuation are refused as a field
    /// error, never silently dropped.
    /// </summary>
    internal static string? OptionalTelephone(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var collapsed = TelephoneWhitespace().Replace(value.Trim(), " ");
        if (!TelephoneShape().IsMatch(collapsed))
        {
            throw new ArgumentException("Telephone must be digits.", name);
        }

        return Required(collapsed, max, name);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex TelephoneWhitespace();

    [System.Text.RegularExpressions.GeneratedRegex(@"^\+?[0-9 ]+$")]
    private static partial System.Text.RegularExpressions.Regex TelephoneShape();
}
