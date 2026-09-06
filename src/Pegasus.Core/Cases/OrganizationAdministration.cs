using Pegasus.Core.Address;
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
    CaseInspectionMode InspectionMode = CaseInspectionMode.PhysicalAddress,
    bool EvaManualSubmission = false,
    string? DefaultInspectionLocationLabel = null,
    string? DefaultInspectionAddress = null,
    string? DefaultInspectionPostcode = null,
    string? DefaultInspectionSourceKind = null,
    Guid? DefaultInspectionSourceRecordId = null,
    long? DefaultInspectionSourceVersion = null);

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

    /// <summary>
    /// EXT-04: change an existing principal's EVA submission settings. Unlike
    /// a replacement this creates no new principal and moves no reference — it
    /// is the one principal attribute that may change in place.
    /// </summary>
    Task<Principal> UpdatePrincipalEvaSubmissionAsync(
        UpdatePrincipalEvaSubmissionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// EXT-18/S05 item 6: the principal's one default inspection-location
    /// choice — Image Based Assessment, or one sourced/manual physical
    /// address kept alongside a staff reason. This never changes B's separate
    /// CE assessment method and never touches the shared <see cref="Principal"/>
    /// record; it is C's own directory-facing summary field.
    /// </summary>
    Task<PrincipalAdministrationSummary> UpdatePrincipalDefaultInspectionLocationAsync(
        UpdatePrincipalDefaultInspectionLocationRequest request,
        CancellationToken cancellationToken);
}

/// <param name="Kind">
/// <see cref="InspectionAddressEvidenceKind.ImageBasedAssessment"/>
/// clears every address field below; <see cref="InspectionAddressEvidenceKind.PhysicalAddress"/>
/// requires <paramref name="Address"/>.
/// </param>
public sealed record UpdatePrincipalDefaultInspectionLocationRequest(
    ActionActor Actor,
    Guid PrincipalId,
    long ExpectedVersion,
    string OperationKey,
    string Reason,
    InspectionAddressEvidenceKind Kind,
    string? Label,
    string? Address,
    string? Postcode,
    string? SourceKind,
    Guid? SourceRecordId,
    long? SourceVersion);

public interface IUpdatePrincipalDefaultInspectionLocation
{
    Task<PrincipalAdministrationSummary> ExecuteAsync(
        UpdatePrincipalDefaultInspectionLocationRequest request,
        CancellationToken cancellationToken);
}

public sealed class UpdatePrincipalDefaultInspectionLocation(IOrganizationAdministrationStore store)
    : IUpdatePrincipalDefaultInspectionLocation
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<PrincipalAdministrationSummary> ExecuteAsync(
        UpdatePrincipalDefaultInspectionLocationRequest request,
        CancellationToken cancellationToken) =>
        _store.UpdatePrincipalDefaultInspectionLocationAsync(
            OrganizationAdministrationPolicy.Normalize(request),
            cancellationToken);
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

public sealed class UpdatePrincipalEvaSubmission(IOrganizationAdministrationStore store)
    : IUpdatePrincipalEvaSubmission
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Principal> ExecuteAsync(
        UpdatePrincipalEvaSubmissionRequest request,
        CancellationToken cancellationToken) =>
        _store.UpdatePrincipalEvaSubmissionAsync(
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

    /// <remarks>
    /// EXT-18 item 7: automatic EVA submission is retired from C's
    /// administration surface. A principal is always created with it false;
    /// only the explicit optional manual EVA setting remains.
    /// </remarks>
    public static Principal PlanPrincipalCreation(
        Guid principalId,
        Guid sequenceLineageId,
        Organization organization,
        string code,
        bool codeAlreadyExists,
        CaseInspectionMode inspectionMode = CaseInspectionMode.PhysicalAddress,
        bool evaManualSubmission = false)
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
            inspectionMode,
            evaManualSubmission,
            EvaAutomaticSubmission: false);
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
                predecessor.InspectionMode,
                predecessor.EvaManualSubmission,
                EvaAutomaticSubmission: false));
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

    public static UpdatePrincipalEvaSubmissionRequest Normalize(
        UpdatePrincipalEvaSubmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        return request with
        {
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

    /// <summary>
    /// EXT-18/S05 item 6: an Image Based Assessment choice carries no address;
    /// a physical choice requires one and, when it corrects a sourced value,
    /// keeps the reason a staff override always requires.
    /// </summary>
    public static UpdatePrincipalDefaultInspectionLocationRequest Normalize(
        UpdatePrincipalDefaultInspectionLocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        if (!Enum.IsDefined(request.Kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The default inspection location kind is invalid.");
        }

        var normalized = request with
        {
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason))
        };

        if (normalized.Kind == InspectionAddressEvidenceKind.ImageBasedAssessment)
        {
            return normalized with
            {
                Label = null,
                Address = null,
                Postcode = null,
                SourceKind = null,
                SourceRecordId = null,
                SourceVersion = null
            };
        }

        if (string.IsNullOrWhiteSpace(normalized.Address))
        {
            throw new ArgumentException(
                "A physical default inspection location requires an address.",
                nameof(request));
        }

        return normalized with
        {
            Address = normalized.Address.Trim(),
            Postcode = string.IsNullOrWhiteSpace(normalized.Postcode)
                ? null
                : normalized.Postcode.Trim()
        };
    }

    /// <summary>
    /// EXT-04/EXT-18 item 7: the manual EVA setting changes, and nothing else
    /// does. The code, the organization, the lineage and the allocation
    /// history are untouched, and automatic EVA submission is retired from
    /// this administration surface — it is always set to false here, never
    /// read from staff input.
    /// </summary>
    public static Principal PlanPrincipalEvaSubmissionUpdate(
        Principal current,
        long expectedVersion,
        bool evaManualSubmission)
    {
        ArgumentNullException.ThrowIfNull(current);
        RequireExpectedVersion(expectedVersion, nameof(expectedVersion));
        if (current.Version != expectedVersion)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.StaleVersion);
        }

        // A replaced principal keeps its settings as a record of what it did;
        // changing them would rewrite history for work already allocated, and
        // the successor is the one that decides what happens next.
        if (!current.IsActive)
        {
            throw new OrganizationAdministrationException(
                OrganizationAdministrationError.PrincipalInactive);
        }

        var changed = current.EvaManualSubmission != evaManualSubmission
            || current.EvaAutomaticSubmission;
        return current with
        {
            EvaManualSubmission = evaManualSubmission,
            EvaAutomaticSubmission = false,
            Version = changed ? checked(current.Version + 1) : current.Version
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

    internal static string NormalizeRequiredText(
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
