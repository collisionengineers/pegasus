using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

public enum OrganizationAdministrationError
{
    DuplicateOrganizationName,
    OrganizationNotFound,
    EmptyOrganizationRoles,
    ActivePrincipalsRequireWorkProvider,
    OrganizationCannotOwnPrincipals,
    DuplicatePrincipalCode,
    PrincipalNotFound,
    PrincipalInactive,
    PrincipalAlreadyReplaced,
    StaleVersion,
    OperationConflict
}

public sealed class OrganizationAdministrationException(
    OrganizationAdministrationError error)
    : Exception("The organization or principal administration request could not be completed.")
{
    public OrganizationAdministrationError Error { get; } = error;
}

public sealed record PrincipalAdministrationSummary(
    Guid Id,
    Guid OrganizationId,
    string Code,
    Guid SequenceLineageId,
    Guid? PredecessorId,
    Guid? SuccessorId,
    bool IsActive,
    long Version,
    int AllocatedCaseCount,
    CaseInspectionMode InspectionMode = CaseInspectionMode.PhysicalAddress);

public sealed record OrganizationListItem(
    Guid Id,
    string Name,
    IReadOnlyList<OrganizationRole> Roles,
    long Version,
    IReadOnlyList<PrincipalAdministrationSummary> Principals,
    bool HasMorePrincipals);

public sealed record OrganizationListPage(
    IReadOnlyList<OrganizationListItem> Organizations,
    int PageNumber,
    int PageSize,
    bool HasPreviousPage,
    bool HasMoreOrganizations);

public sealed record OrganizationDetails(
    Guid Id,
    string Name,
    IReadOnlyList<OrganizationRole> Roles,
    long Version,
    IReadOnlyList<PrincipalAdministrationSummary> Principals,
    bool HasMorePrincipals);

public sealed record ListOrganizationsRequest(
    ActionActor Actor,
    int PageNumber = 1,
    int PageSize = 25);

public sealed record GetOrganizationRequest(
    ActionActor Actor,
    Guid OrganizationId,
    Guid? RequiredPrincipalId = null);

public interface IListOrganizations
{
    Task<OrganizationListPage> ExecuteAsync(
        ListOrganizationsRequest request,
        CancellationToken cancellationToken);
}

public interface IGetOrganization
{
    Task<OrganizationDetails?> ExecuteAsync(
        GetOrganizationRequest request,
        CancellationToken cancellationToken);
}

public sealed record OrganizationQuerySlice(
    IReadOnlyList<OrganizationListItem> Organizations,
    bool HasMoreOrganizations);

public interface IOrganizationAdministrationQueries
{
    Task<OrganizationQuerySlice> ListAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken);

    Task<OrganizationDetails?> GetAsync(
        Guid organizationId,
        int principalLimit,
        Guid? requiredPrincipalId,
        CancellationToken cancellationToken);
}

public interface IOrganizationAdministrationStore
{
    Task<Organization> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken);

    Task<Organization> UpdateOrganizationRolesAsync(
        UpdateOrganizationRolesRequest request,
        CancellationToken cancellationToken);

    Task<Principal> CreatePrincipalAsync(
        CreatePrincipalRequest request,
        CancellationToken cancellationToken);

    Task<Principal> ReplacePrincipalAsync(
        ReplacePrincipalRequest request,
        CancellationToken cancellationToken);
}

public sealed class ListOrganizations(IOrganizationAdministrationQueries queries)
    : IListOrganizations
{
    public const int MaximumPageSize = 100;

    private readonly IOrganizationAdministrationQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<OrganizationListPage> ExecuteAsync(
        ListOrganizationsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(
            request.Actor,
            StaffAccessRight.ManageOrganizationsAndPrincipals);

        if (request.PageNumber < 1
            || request.PageSize < 1
            || request.PageSize > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"Organization pages must be positive and contain at most {MaximumPageSize} rows.");
        }

        var offset = ((long)request.PageNumber - 1L) * request.PageSize;
        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The requested organization page is outside the supported range.");
        }

        var slice = await _queries.ListAsync(
            (int)offset,
            request.PageSize,
            cancellationToken);
        return new(
            slice.Organizations,
            request.PageNumber,
            request.PageSize,
            request.PageNumber > 1,
            slice.HasMoreOrganizations);
    }
}

public sealed class GetOrganization(IOrganizationAdministrationQueries queries)
    : IGetOrganization
{
    public const int MaximumPrincipalCount = 100;

    private readonly IOrganizationAdministrationQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<OrganizationDetails?> ExecuteAsync(
        GetOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(
            request.Actor,
            StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (request.OrganizationId == Guid.Empty)
        {
            throw new ArgumentException(
                "An organization identifier is required.",
                nameof(request));
        }
        if (request.RequiredPrincipalId == Guid.Empty)
        {
            throw new ArgumentException(
                "A required principal identifier cannot be empty.",
                nameof(request));
        }


        return _queries.GetAsync(
            request.OrganizationId,
            MaximumPrincipalCount,
            request.RequiredPrincipalId,
            cancellationToken);
    }
}

public sealed class CreateOrganization(IOrganizationAdministrationStore store)
    : ICreateOrganization
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Organization> ExecuteAsync(
        CreateOrganizationRequest request,
        CancellationToken cancellationToken) =>
        _store.CreateOrganizationAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class UpdateOrganizationRoles(IOrganizationAdministrationStore store)
    : IUpdateOrganizationRoles
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Organization> ExecuteAsync(
        UpdateOrganizationRolesRequest request,
        CancellationToken cancellationToken) =>
        _store.UpdateOrganizationRolesAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class CreatePrincipal(IOrganizationAdministrationStore store)
    : ICreatePrincipal
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Principal> ExecuteAsync(
        CreatePrincipalRequest request,
        CancellationToken cancellationToken) =>
        _store.CreatePrincipalAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class ReplacePrincipal(IOrganizationAdministrationStore store)
    : IReplacePrincipal
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Principal> ExecuteAsync(
        ReplacePrincipalRequest request,
        CancellationToken cancellationToken) =>
        _store.ReplacePrincipalAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed record PrincipalReplacementPlan(
    Principal Predecessor,
    Principal Successor);

public static class OrganizationAdministrationPolicy
{
    public const int MaximumOrganizationNameLength = 300;
    public const int MaximumPrincipalCodeLength = 20;
    public const int MaximumOperationKeyLength = 100;
    public const int MaximumReasonLength = 500;

    public static void RequireUniqueOrganizationName(bool alreadyExists)
    {
        if (alreadyExists)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.DuplicateOrganizationName);
        }
    }

    public static Organization PlanRoleUpdate(
        Organization current,
        long expectedVersion,
        IReadOnlyList<OrganizationRole> requestedRoles,
        bool hasActivePrincipals)
    {
        ArgumentNullException.ThrowIfNull(current);
        RequireExpectedVersion(expectedVersion, nameof(expectedVersion));
        if (current.Version != expectedVersion)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.StaleVersion);
        }

        var roles = NormalizeRoles(requestedRoles);
        if (hasActivePrincipals && !roles.Contains(OrganizationRole.WorkProvider))
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.ActivePrincipalsRequireWorkProvider);
        }

        var changed = !current.Roles
            .OrderBy(role => role)
            .SequenceEqual(roles);
        return current with
        {
            Roles = roles,
            Version = changed ? checked(current.Version + 1) : current.Version
        };
    }

    public static Principal PlanPrincipalCreation(
        Guid principalId,
        Guid sequenceLineageId,
        Organization organization,
        string code,
        bool codeAlreadyExists,
        CaseInspectionMode inspectionMode = CaseInspectionMode.PhysicalAddress)
    {
        RequireIdentifier(principalId, nameof(principalId));
        RequireIdentifier(sequenceLineageId, nameof(sequenceLineageId));
        ArgumentNullException.ThrowIfNull(organization);
        RequireOrganizationCanOwnPrincipals(organization);
        RequireUniquePrincipalCode(codeAlreadyExists);
        RequireDefinedInspectionMode(inspectionMode);
        return new(
            principalId,
            organization.Id,
            NormalizePrincipalCode(code),
            sequenceLineageId,
            null,
            null,
            true,
            0,
            inspectionMode);
    }

    public static PrincipalReplacementPlan PlanPrincipalReplacement(
        Principal predecessor,
        long expectedVersion,
        Organization successorOrganization,
        Guid successorId,
        string successorCode,
        bool codeAlreadyExists)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        ArgumentNullException.ThrowIfNull(successorOrganization);
        RequireExpectedVersion(expectedVersion, nameof(expectedVersion));
        RequireIdentifier(successorId, nameof(successorId));
        if (predecessor.Version != expectedVersion)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.StaleVersion);
        }
        if (predecessor.SuccessorId is not null)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.PrincipalAlreadyReplaced);
        }
        if (!predecessor.IsActive)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.PrincipalInactive);
        }

        RequireOrganizationCanOwnPrincipals(successorOrganization);
        RequireUniquePrincipalCode(codeAlreadyExists);
        var normalizedCode = NormalizePrincipalCode(successorCode);
        return new(
            predecessor with
            {
                SuccessorId = successorId,
                IsActive = false,
                Version = checked(predecessor.Version + 1)
            },
            new(
                successorId,
                successorOrganization.Id,
                normalizedCode,
                predecessor.SequenceLineageId,
                predecessor.Id,
                null,
                true,
                0,
                predecessor.InspectionMode));
    }

    public static void RequireOrganizationCanOwnPrincipals(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);
        if (!organization.Roles.Contains(OrganizationRole.WorkProvider))
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.OrganizationCannotOwnPrincipals);
        }
    }

    public static void RequireUniquePrincipalCode(bool alreadyExists)
    {
        if (alreadyExists)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.DuplicatePrincipalCode);
        }
    }

    public static CreateOrganizationRequest Normalize(CreateOrganizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        return request with
        {
            Name = NormalizeOrganizationName(request.Name),
            Roles = NormalizeRoles(request.Roles),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static UpdateOrganizationRolesRequest Normalize(
        UpdateOrganizationRolesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.OrganizationId, nameof(request.OrganizationId));
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        return request with
        {
            Roles = NormalizeRoles(request.Roles),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason))
        };
    }

    public static CreatePrincipalRequest Normalize(CreatePrincipalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.OrganizationId, nameof(request.OrganizationId));
        RequireDefinedInspectionMode(request.InspectionMode);
        return request with
        {
            Code = NormalizePrincipalCode(request.Code),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static ReplacePrincipalRequest Normalize(ReplacePrincipalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        RequireIdentifier(
            request.SuccessorOrganizationId,
            nameof(request.SuccessorOrganizationId));
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        return request with
        {
            SuccessorCode = NormalizePrincipalCode(request.SuccessorCode),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason))
        };
    }

    public static string NormalizeOrganizationName(string value)
    {
        var normalized = NormalizeRequiredText(
            value,
            MaximumOrganizationNameLength,
            nameof(value));
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                "An organization name cannot contain control characters.",
                nameof(value));
        }

        return normalized;
    }

    public static string NormalizePrincipalCode(string value)
    {
        var normalized = NormalizeRequiredText(
            value,
            MaximumPrincipalCodeLength,
            nameof(value)).ToUpperInvariant();
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character)))
        {
            throw new ArgumentException(
                "A principal code can contain only ASCII letters and digits.",
                nameof(value));
        }

        return normalized;
    }

    private static OrganizationRole[] NormalizeRoles(
        IReadOnlyList<OrganizationRole> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.EmptyOrganizationRoles);
        }
        if (roles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(roles),
                "Every organization role must be recognized.");
        }

        return roles.Distinct().OrderBy(role => role).ToArray();
    }

    private static void RequireAdministrator(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(
            actor,
            StaffAccessRight.ManageOrganizationsAndPrincipals);
    }

    private static void RequireIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "A stable identifier is required.",
                parameterName);
        }
    }

    private static void RequireDefinedInspectionMode(CaseInspectionMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                "The principal inspection mode is invalid.");
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

    private static string NormalizeRequiredText(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
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
