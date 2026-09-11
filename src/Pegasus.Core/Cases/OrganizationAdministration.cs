using Pegasus.Core.Address;
using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Cases;

public enum OrganizationAdministrationError
{
    DuplicateOrganizationName,
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
    PrincipalReportGenerationPolicy ReportGenerationPolicy = PrincipalReportGenerationPolicy.Pegasus,
    PrincipalReportRecipientSettings? ReportRecipients = null,
    string? DefaultInspectionLocationLabel = null,
    string? DefaultInspectionAddress = null,
    string? DefaultInspectionPostcode = null,
    string? DefaultInspectionSourceKind = null,
    Guid? DefaultInspectionSourceRecordId = null,
    long? DefaultInspectionSourceVersion = null);

public sealed record PrincipalAdministrationDetails(
    string Name,
    PrincipalAdministrationSummary Principal);

public sealed record PrincipalListPage(
    IReadOnlyList<PrincipalAdministrationDetails> Principals,
    int PageNumber,
    bool HasMore);

public interface IListPrincipals
{
    Task<PrincipalListPage> ExecuteAsync(
        ActionActor actor, int pageNumber, CancellationToken cancellationToken);
}

public interface IGetPrincipal
{
    Task<PrincipalAdministrationDetails?> ExecuteAsync(
        ActionActor actor, Guid principalId, CancellationToken cancellationToken);
}

public interface IOrganizationAdministrationQueries
{
    Task<IReadOnlyList<PrincipalAdministrationDetails>> ListPrincipalsAsync(
        int offset, int limit, CancellationToken cancellationToken);

    Task<PrincipalAdministrationDetails?> GetPrincipalAsync(
        Guid principalId, CancellationToken cancellationToken);

}

public interface IOrganizationAdministrationStore
{
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
    Task<Principal> UpdatePrincipalReportSettingsAsync(
        UpdatePrincipalReportSettingsRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// EXT-18/S05 item 6: the principal's one default inspection-location
    /// choice — Image Based Assessment, or one sourced/manual physical
    /// address. This never changes B's separate
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
    InspectionAddressEvidenceKind Kind,
    string? Label,
    string? Address,
    string? Postcode,
    string? SourceKind,
    Guid? SourceRecordId,
    long? SourceVersion,
    long ExpectedContactVersion,
    string EditLeaseToken);

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

public sealed class ListPrincipals(IOrganizationAdministrationQueries queries) : IListPrincipals
{
    public const int PageSize = 25;

    public async Task<PrincipalListPage> ExecuteAsync(
        ActionActor actor, int pageNumber, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (pageNumber < 1 || pageNumber > int.MaxValue / PageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }
        var rows = await queries.ListPrincipalsAsync(
            (pageNumber - 1) * PageSize, PageSize + 1, cancellationToken);
        return new(rows.Take(PageSize).ToArray(), pageNumber, rows.Count > PageSize);
    }
}

public sealed class GetPrincipal(IOrganizationAdministrationQueries queries) : IGetPrincipal
{
    public Task<PrincipalAdministrationDetails?> ExecuteAsync(
        ActionActor actor, Guid principalId, CancellationToken cancellationToken)
    {
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("A principal identifier is required.", nameof(principalId));
        }
        return queries.GetPrincipalAsync(principalId, cancellationToken);
    }
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

public sealed class UpdatePrincipalReportSettings(IOrganizationAdministrationStore store)
    : IUpdatePrincipalReportSettings
{
    private readonly IOrganizationAdministrationStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<Principal> ExecuteAsync(
        UpdatePrincipalReportSettingsRequest request,
        CancellationToken cancellationToken) =>
        _store.UpdatePrincipalReportSettingsAsync(
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

    public static Principal PlanPrincipalCreation(
        Guid principalId,
        Guid sequenceLineageId,
        Organization organization,
        string code,
        bool codeAlreadyExists,
        CaseInspectionMode inspectionMode = CaseInspectionMode.PhysicalAddress,
        PrincipalReportGenerationPolicy reportGenerationPolicy = PrincipalReportGenerationPolicy.Pegasus,
        PrincipalReportRecipientSettings? reportRecipients = null)
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
            reportGenerationPolicy,
            PrincipalReportRecipientSettings.Normalize(
                reportRecipients?.IncludeOriginalInstructionSender ?? false,
                reportRecipients?.AdditionalAddresses));
    }

    public static PrincipalReplacementPlan PlanPrincipalReplacement(
        Principal predecessor,
        long expectedVersion,
        Guid successorId,
        string successorCode,
        bool codeAlreadyExists)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
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
                predecessor.OrganizationId,
                normalizedCode,
                predecessor.SequenceLineageId,
                predecessor.Id,
                null,
                true,
                0,
                predecessor.InspectionMode,
                predecessor.ReportGenerationPolicy,
                predecessor.ReportRecipients));
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

    public static UpdatePrincipalReportSettingsRequest Normalize(
        UpdatePrincipalReportSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        RequireExpectedVersion(request.ExpectedContactVersion, nameof(request.ExpectedContactVersion));
        return request with
        {
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = NormalizeOptionalText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason)),
            EditLeaseToken = NormalizeEditLeaseToken(request.EditLeaseToken)
        };
    }

    /// <summary>
    /// EXT-18/S05 item 6: an Image Based Assessment choice carries no address;
    /// a physical choice requires one.
    /// </summary>
    public static UpdatePrincipalDefaultInspectionLocationRequest Normalize(
        UpdatePrincipalDefaultInspectionLocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireIdentifier(request.PrincipalId, nameof(request.PrincipalId));
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        RequireExpectedVersion(request.ExpectedContactVersion, nameof(request.ExpectedContactVersion));
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
            EditLeaseToken = NormalizeEditLeaseToken(request.EditLeaseToken)
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
    /// The report route and suggested recipients change, and nothing else
    /// does. The code, the organization, the lineage and the allocation
    /// history are untouched.
    /// </summary>
    public static Principal PlanPrincipalReportSettingsUpdate(
        Principal current,
        long expectedVersion,
        PrincipalReportGenerationPolicy reportGenerationPolicy,
        PrincipalReportRecipientSettings reportRecipients)
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

        ArgumentNullException.ThrowIfNull(reportRecipients);
        if (!Enum.IsDefined(reportGenerationPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(reportGenerationPolicy));
        }
        var normalizedRecipients = PrincipalReportRecipientSettings.Normalize(
            reportRecipients.IncludeOriginalInstructionSender,
            reportRecipients.AdditionalAddresses);
        var changed = current.ReportGenerationPolicy != reportGenerationPolicy
            || !Equals(current.ReportRecipients ?? PrincipalReportRecipientSettings.None, normalizedRecipients);
        return current with
        {
            ReportGenerationPolicy = reportGenerationPolicy,
            ReportRecipients = normalizedRecipients,
            Version = changed ? checked(current.Version + 1) : current.Version
        };
    }

    public static CreatePrincipalRequest Normalize(CreatePrincipalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor);
        RequireDefinedInspectionMode(request.InspectionMode);
        if (!Enum.IsDefined(request.ReportGenerationPolicy))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The report generation policy is invalid.");
        }
        return request with
        {
            Name = NormalizeOrganizationName(request.Name),
            Code = NormalizePrincipalCode(request.Code),
            ReportRecipients = PrincipalReportRecipientSettings.Normalize(
                request.ReportRecipients?.IncludeOriginalInstructionSender ?? false,
                request.ReportRecipients?.AdditionalAddresses),
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
        RequireExpectedVersion(request.ExpectedVersion, nameof(request.ExpectedVersion));
        RequireExpectedVersion(request.ExpectedContactVersion, nameof(request.ExpectedContactVersion));
        return request with
        {
            SuccessorCode = NormalizePrincipalCode(request.SuccessorCode),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = NormalizeOptionalText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason)),
            EditLeaseToken = NormalizeEditLeaseToken(request.EditLeaseToken)
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

    internal static string? NormalizeOptionalText(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeRequiredText(value, maximumLength, parameterName);
    }

    private static string NormalizeEditLeaseToken(string? value) =>
        NormalizeRequiredText(value ?? string.Empty, 200, nameof(value));
}
