namespace Pegasus.Core.Identity;

public sealed record StaffAccountSummary(
    Guid Id,
    string UserName,
    bool IsEnabled,
    bool MustChangePassword,
    IReadOnlyList<StaffRole> Roles)
{
    public StaffAccountSignOffState SignOff { get; init; } =
        StaffAccountSignOffState.NotConfigured;
}

public sealed record StaffAccountSignOffState(
    bool IsSignOffEngineer,
    string? PrintedName,
    string? Qualifications,
    bool HasSignature,
    bool IsDefault)
{
    public static StaffAccountSignOffState NotConfigured { get; } =
        new(false, null, null, false, false);
}

public sealed record SignOffEngineerProfile(
    Guid StaffId,
    string PrintedName,
    string? Qualifications,
    byte[] Signature,
    string SignatureContentType,
    bool IsDefault);

public sealed record ListStaffAccountsRequest(
    ActionActor Actor,
    int PageNumber = 1,
    int PageSize = 50);

public sealed record ListStaffAccountsResult(
    IReadOnlyList<StaffAccountSummary> Accounts,
    int PageNumber,
    int PageSize,
    bool HasPreviousPage,
    bool HasMoreAccounts);

public sealed record GetStaffAccountRequest(
    ActionActor Actor,
    Guid StaffId);

public sealed record GetStaffAccountResult(StaffAccountSummary Account);

public sealed record StaffHeldCaseEditLease(
    Guid CaseId,
    string CaseReference,
    long LeaseGeneration,
    DateTimeOffset ExpiresAtUtc);

public sealed record GetStaffHeldCaseEditLeasesRequest(
    ActionActor Actor,
    Guid StaffId);

public sealed record GetStaffHeldCaseEditLeasesResult(
    Guid StaffId,
    IReadOnlyList<StaffHeldCaseEditLease> Leases);

public sealed record StaffRoleAssignmentProjection(
    Guid StaffId,
    string UserName,
    bool IsEnabled,
    IReadOnlyList<StaffRole> CurrentRoles);

public sealed record GetRoleAssignmentsRequest(
    ActionActor Actor,
    int MaximumResults = 100);

public sealed record GetRoleAssignmentsResult(
    IReadOnlyList<StaffRoleAssignmentProjection> Accounts,
    bool HasMoreAccounts);

public sealed record CreateStaffAccountRequest(
    ActionActor Actor,
    string UserName,
    string TemporaryPassword,
    string Reason,
    string OperationKey);

public sealed record CreateStaffAccountResult(
    StaffAccountSummary Account,
    bool WasReplay);

public sealed record DisableStaffAccountRequest(
    ActionActor Actor,
    Guid StaffId,
    string Reason,
    string OperationKey);

public sealed record DisableStaffAccountResult(
    StaffAccountSummary Account,
    long RevokedAuthorizations,
    long RevokedTokens,
    bool WasReplay);

public sealed record AssignStaffRolesRequest(
    ActionActor Actor,
    Guid StaffId,
    IReadOnlyCollection<StaffRole> Roles,
    string Reason,
    string OperationKey);

public sealed record AssignStaffRolesResult(
    StaffAccountSummary Account,
    long RevokedAuthorizations,
    long RevokedTokens,
    bool WasReplay);

public sealed record EnableStaffAccountRequest(
    ActionActor Actor,
    Guid StaffId,
    string Reason,
    string OperationKey);

public sealed record EnableStaffAccountResult(
    StaffAccountSummary Account,
    bool WasReplay);

public sealed record ForceStaffLogoutRequest(
    ActionActor Actor,
    Guid StaffId,
    string Reason,
    string OperationKey);

public sealed record ForceStaffLogoutResult(
    Guid StaffId,
    long RevokedAuthorizations,
    long RevokedTokens,
    bool WasReplay);

public sealed record ResetStaffPasswordRequest(
    ActionActor Actor,
    Guid StaffId,
    string Reason,
    string OperationKey);

public sealed class ResetStaffPasswordResult(
    Guid staffId,
    string temporaryPassword,
    long revokedAuthorizations,
    long revokedTokens,
    bool wasReplay)
{
    public Guid StaffId { get; } = staffId;
    public string TemporaryPassword { get; } = temporaryPassword;
    public long RevokedAuthorizations { get; } = revokedAuthorizations;
    public long RevokedTokens { get; } = revokedTokens;
    public bool WasReplay { get; } = wasReplay;
    public override string ToString() => nameof(ResetStaffPasswordResult);
}

public sealed record DeleteStaffAccountRequest(
    ActionActor Actor,
    Guid StaffId,
    string Reason,
    string OperationKey);

public sealed record DeleteStaffAccountResult(
    Guid StaffId,
    long RevokedAuthorizations,
    long RevokedTokens,
    bool CredentialsCleared,
    bool WasReplay);

public sealed record UpdateStaffAccountSignOffRequest(
    ActionActor Actor,
    Guid StaffId,
    bool IsSignOffEngineer,
    string? PrintedName,
    string? Qualifications,
    byte[]? Signature,
    bool IsDefault,
    string Reason,
    string OperationKey);

public sealed record UpdateStaffAccountSignOffResult(
    StaffAccountSummary Account,
    bool WasReplay);

public sealed record StaffAccountQuerySlice(
    IReadOnlyList<StaffAccountSummary> Accounts,
    bool HasMoreAccounts);

public interface IStaffAccountQueries
{
    Task<StaffAccountQuerySlice> ListAsync(
        int offset,
        int limit,
        CancellationToken cancellationToken);

    Task<StaffAccountSummary?> GetAsync(
        Guid staffId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
        CancellationToken cancellationToken);

    Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
        Guid staffId,
        CancellationToken cancellationToken);
}

public interface ICreateStaffAccountStore
{
    Task<CreateStaffAccountResult> CreateAsync(
        CreateStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IDisableStaffAccountStore
{
    Task<DisableStaffAccountResult> DisableAsync(
        DisableStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IAssignStaffRolesStore
{
    Task<AssignStaffRolesResult> AssignAsync(
        AssignStaffRolesRequest request,
        CancellationToken cancellationToken);
}

public interface IEnableStaffAccountStore
{
    Task<EnableStaffAccountResult> EnableAsync(
        EnableStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IStaffHeldCaseEditLeaseQueries
{
    Task<IReadOnlyList<StaffHeldCaseEditLease>> ListHeldCaseEditLeasesAsync(
        Guid staffId,
        CancellationToken cancellationToken);
}

public interface IForceStaffLogoutStore
{
    Task<ForceStaffLogoutResult> ForceLogoutAsync(
        ForceStaffLogoutRequest request,
        CancellationToken cancellationToken);
}

public interface IResetStaffPasswordStore
{
    Task<ResetStaffPasswordResult> ResetPasswordAsync(
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken);
}

public interface IDeleteStaffAccountStore
{
    Task<DeleteStaffAccountResult> DeleteAsync(
        DeleteStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IUpdateStaffAccountSignOffStore
{
    Task<UpdateStaffAccountSignOffResult> UpdateAsync(
        UpdateStaffAccountSignOffRequest request,
        CancellationToken cancellationToken);
}

public interface IListStaffAccounts
{
    Task<ListStaffAccountsResult> ExecuteAsync(
        ListStaffAccountsRequest request,
        CancellationToken cancellationToken);
}

public interface IGetStaffAccount
{
    Task<GetStaffAccountResult?> ExecuteAsync(
        GetStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IGetRoleAssignments
{
    Task<GetRoleAssignmentsResult> ExecuteAsync(
        GetRoleAssignmentsRequest request,
        CancellationToken cancellationToken);
}

public interface ICreateStaffAccount
{
    Task<CreateStaffAccountResult> ExecuteAsync(
        CreateStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IDisableStaffAccount
{
    Task<DisableStaffAccountResult> ExecuteAsync(
        DisableStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IAssignStaffRoles
{
    Task<AssignStaffRolesResult> ExecuteAsync(
        AssignStaffRolesRequest request,
        CancellationToken cancellationToken);
}

public interface IEnableStaffAccount
{
    Task<EnableStaffAccountResult> ExecuteAsync(
        EnableStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IGetStaffHeldCaseEditLeases
{
    Task<GetStaffHeldCaseEditLeasesResult> ExecuteAsync(
        GetStaffHeldCaseEditLeasesRequest request,
        CancellationToken cancellationToken);
}

public interface IForceStaffLogout
{
    Task<ForceStaffLogoutResult> ExecuteAsync(
        ForceStaffLogoutRequest request,
        CancellationToken cancellationToken);
}

public interface IResetStaffPassword
{
    Task<ResetStaffPasswordResult> ExecuteAsync(
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken);
}

public interface IDeleteStaffAccount
{
    Task<DeleteStaffAccountResult> ExecuteAsync(
        DeleteStaffAccountRequest request,
        CancellationToken cancellationToken);
}

public interface IUpdateStaffAccountSignOff
{
    Task<UpdateStaffAccountSignOffResult> ExecuteAsync(
        UpdateStaffAccountSignOffRequest request,
        CancellationToken cancellationToken);
}

public sealed class ListStaffAccounts(IStaffAccountQueries queries)
    : IListStaffAccounts
{
    public const int MaximumPageSize = 100;

    private readonly IStaffAccountQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<ListStaffAccountsResult> ExecuteAsync(
        ListStaffAccountsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageStaffAccounts);
        ValidatePage(request.PageNumber, request.PageSize);

        var offset = ((long)request.PageNumber - 1L) * request.PageSize;
        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The requested staff-account page is outside the supported range.");
        }

        var slice = await _queries.ListAsync(
            (int)offset,
            request.PageSize,
            cancellationToken);
        return new(
            slice.Accounts,
            request.PageNumber,
            request.PageSize,
            request.PageNumber > 1,
            slice.HasMoreAccounts);
    }

    internal static void ValidateMaximumResults(int maximumResults, string parameterName)
    {
        if (maximumResults < 1 || maximumResults > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Staff-account queries must request between 1 and {MaximumPageSize} rows.");
        }
    }

    private static void ValidatePage(int pageNumber, int pageSize)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pageNumber),
                "The staff-account page number must be positive.");
        }

        ValidateMaximumResults(pageSize, nameof(pageSize));
    }
}

public sealed class GetStaffAccount(IStaffAccountQueries queries)
    : IGetStaffAccount
{
    private readonly IStaffAccountQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<GetStaffAccountResult?> ExecuteAsync(
        GetStaffAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageStaffAccounts);
        StaffAccountAdministrationPolicy.RequireStaffId(request.StaffId);

        var account = await _queries.GetAsync(request.StaffId, cancellationToken);
        return account is null ? null : new(account);
    }
}

public sealed class GetRoleAssignments(IStaffAccountQueries queries)
    : IGetRoleAssignments
{
    private readonly IStaffAccountQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<GetRoleAssignmentsResult> ExecuteAsync(
        GetRoleAssignmentsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.AssignStaffRoles);
        ListStaffAccounts.ValidateMaximumResults(
            request.MaximumResults,
            nameof(request.MaximumResults));

        var slice = await _queries.ListAsync(0, request.MaximumResults, cancellationToken);
        return new(
            slice.Accounts
                .Select(account => new StaffRoleAssignmentProjection(
                    account.Id,
                    account.UserName,
                    account.IsEnabled,
                    account.Roles))
                .ToArray(),
            slice.HasMoreAccounts);
    }
}

public sealed class CreateStaffAccount(ICreateStaffAccountStore store)
    : ICreateStaffAccount
{
    private readonly ICreateStaffAccountStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<CreateStaffAccountResult> ExecuteAsync(
        CreateStaffAccountRequest request,
        CancellationToken cancellationToken) =>
        _store.CreateAsync(
            StaffAccountAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class DisableStaffAccount(IDisableStaffAccountStore store)
    : IDisableStaffAccount
{
    private readonly IDisableStaffAccountStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<DisableStaffAccountResult> ExecuteAsync(
        DisableStaffAccountRequest request,
        CancellationToken cancellationToken) =>
        _store.DisableAsync(
            StaffAccountAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class AssignStaffRoles(IAssignStaffRolesStore store)
    : IAssignStaffRoles
{
    private readonly IAssignStaffRolesStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<AssignStaffRolesResult> ExecuteAsync(
        AssignStaffRolesRequest request,
        CancellationToken cancellationToken) =>
        _store.AssignAsync(
            StaffAccountAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class EnableStaffAccount(IEnableStaffAccountStore store)
    : IEnableStaffAccount
{
    private readonly IEnableStaffAccountStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<EnableStaffAccountResult> ExecuteAsync(
        EnableStaffAccountRequest request,
        CancellationToken cancellationToken) =>
        _store.EnableAsync(
            StaffAccountAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public sealed class GetStaffHeldCaseEditLeases(IStaffHeldCaseEditLeaseQueries queries)
    : IGetStaffHeldCaseEditLeases
{
    private readonly IStaffHeldCaseEditLeaseQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public async Task<GetStaffHeldCaseEditLeasesResult> ExecuteAsync(
        GetStaffHeldCaseEditLeasesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ManageStaffAccounts);
        StaffAccountAdministrationPolicy.RequireStaffId(request.StaffId);
        return new(
            request.StaffId,
            await _queries.ListHeldCaseEditLeasesAsync(request.StaffId, cancellationToken));
    }
}

public sealed class ForceStaffLogout(IForceStaffLogoutStore store) : IForceStaffLogout
{
    private readonly IForceStaffLogoutStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<ForceStaffLogoutResult> ExecuteAsync(
        ForceStaffLogoutRequest request,
        CancellationToken cancellationToken) =>
        _store.ForceLogoutAsync(StaffAccountAdministrationPolicy.Normalize(request), cancellationToken);
}

public sealed class ResetStaffPassword(IResetStaffPasswordStore store) : IResetStaffPassword
{
    private readonly IResetStaffPasswordStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<ResetStaffPasswordResult> ExecuteAsync(
        ResetStaffPasswordRequest request,
        CancellationToken cancellationToken) =>
        _store.ResetPasswordAsync(StaffAccountAdministrationPolicy.Normalize(request), cancellationToken);
}

public sealed class DeleteStaffAccount(IDeleteStaffAccountStore store) : IDeleteStaffAccount
{
    private readonly IDeleteStaffAccountStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<DeleteStaffAccountResult> ExecuteAsync(
        DeleteStaffAccountRequest request,
        CancellationToken cancellationToken) =>
        _store.DeleteAsync(StaffAccountAdministrationPolicy.Normalize(request), cancellationToken);
}

public sealed class UpdateStaffAccountSignOff(IUpdateStaffAccountSignOffStore store)
    : IUpdateStaffAccountSignOff
{
    private readonly IUpdateStaffAccountSignOffStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<UpdateStaffAccountSignOffResult> ExecuteAsync(
        UpdateStaffAccountSignOffRequest request,
        CancellationToken cancellationToken) =>
        _store.UpdateAsync(
            StaffAccountAdministrationPolicy.Normalize(request),
            cancellationToken);
}

public static class SignOffSignaturePolicy
{
    public const string MediaType = "image/png";
    public const int MaximumBytes = 1024 * 1024;

    private static ReadOnlySpan<byte> PngSignature =>
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    public static byte[]? Validate(byte[]? signature, string parameterName)
    {
        if (signature is null)
        {
            return null;
        }

        if (signature.Length == 0
            || signature.Length > MaximumBytes
            || !signature.AsSpan().StartsWith(PngSignature))
        {
            throw new ArgumentException(
                "The signature must be a non-empty PNG image no larger than 1 MiB.",
                parameterName);
        }

        return signature.ToArray();
    }
}

public static class SignOffEngineerEligibility
{
    public static bool IsEligible(
        bool isEnabled,
        IReadOnlyCollection<StaffRole> roles,
        bool isSignOffEngineer,
        byte[]? signature) =>
        isEnabled
        && roles.Contains(StaffRole.Engineer)
        && isSignOffEngineer
        && signature is { Length: > 0 };
}

public static class StaffAccountAdministrationPolicy
{
    public const int MaximumUserNameLength = 256;
    public const int MaximumPasswordLength = 256;
    public const int MinimumPasswordLength = 8;
    public const int MaximumReasonLength = 1000;
    public const int MaximumOperationKeyLength = 100;
    public const int MaximumSignOffPrintedNameLength = 256;
    public const int MaximumSignOffQualificationsLength = 500;

    public static CreateStaffAccountRequest Normalize(CreateStaffAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor, StaffAccessRight.ManageStaffAccounts);
        ValidateTemporaryPassword(request.TemporaryPassword, nameof(request.TemporaryPassword));

        return request with
        {
            UserName = NormalizeRequiredText(
                request.UserName,
                MaximumUserNameLength,
                nameof(request.UserName)),
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static DisableStaffAccountRequest Normalize(DisableStaffAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor, StaffAccessRight.ManageStaffAccounts);
        RequireStaffId(request.StaffId);
        RequireDifferentStaffAccount(request.Actor, request.StaffId);
        return request with
        {
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static AssignStaffRolesRequest Normalize(AssignStaffRolesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor, StaffAccessRight.AssignStaffRoles);
        RequireStaffId(request.StaffId);
        ArgumentNullException.ThrowIfNull(request.Roles);
        var roles = request.Roles.Distinct().OrderBy(role => role).ToArray();
        if (roles.Length == 0 || roles.Any(role => !Enum.IsDefined(role)))
        {
            throw new ArgumentException(
                "An enabled staff account requires at least one recognized current role.",
                nameof(request));
        }

        return request with
        {
            Roles = roles,
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static EnableStaffAccountRequest Normalize(EnableStaffAccountRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor, StaffAccessRight.ManageStaffAccounts);
        RequireStaffId(request.StaffId);
        return request with
        {
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    public static ForceStaffLogoutRequest Normalize(ForceStaffLogoutRequest request) =>
        NormalizeAdministrativeAction(
            request,
            request.Actor,
            request.StaffId,
            request.Reason,
            request.OperationKey,
            (reason, operationKey) => request with
        {
            Reason = reason,
            OperationKey = operationKey
        });

    public static ResetStaffPasswordRequest Normalize(ResetStaffPasswordRequest request) =>
        NormalizeAdministrativeAction(
            request,
            request.Actor,
            request.StaffId,
            request.Reason,
            request.OperationKey,
            (reason, operationKey) => request with
        {
            Reason = reason,
            OperationKey = operationKey
        });

    public static DeleteStaffAccountRequest Normalize(DeleteStaffAccountRequest request) =>
        NormalizeAdministrativeAction(
            request,
            request.Actor,
            request.StaffId,
            request.Reason,
            request.OperationKey,
            (reason, operationKey) => request with
        {
            Reason = reason,
            OperationKey = operationKey
        });

    public static UpdateStaffAccountSignOffRequest Normalize(
        UpdateStaffAccountSignOffRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(request.Actor, StaffAccessRight.ManageStaffAccounts);
        RequireStaffId(request.StaffId);

        var printedName = NormalizeOptionalText(
            request.PrintedName,
            MaximumSignOffPrintedNameLength,
            nameof(request.PrintedName));
        if (request.IsSignOffEngineer && printedName is null)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.SignOffPrintedNameRequired);
        }

        if (request.IsDefault && !request.IsSignOffEngineer)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.IneligibleSignOffEngineer);
        }

        var signature = SignOffSignaturePolicy.Validate(
            request.Signature,
            nameof(request.Signature));
        return request with
        {
            PrintedName = printedName,
            Qualifications = NormalizeOptionalText(
                request.Qualifications,
                MaximumSignOffQualificationsLength,
                nameof(request.Qualifications)),
            Signature = signature,
            Reason = NormalizeRequiredText(
                request.Reason,
                MaximumReasonLength,
                nameof(request.Reason)),
            OperationKey = NormalizeRequiredText(
                request.OperationKey,
                MaximumOperationKeyLength,
                nameof(request.OperationKey))
        };
    }

    internal static void ValidateTemporaryPassword(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length < MinimumPasswordLength || value.Length > MaximumPasswordLength)
        {
            throw new ArgumentException(
                $"The temporary password must contain between {MinimumPasswordLength} and " +
                $"{MaximumPasswordLength} characters.",
                parameterName);
        }
    }

    internal static void RequireStaffId(Guid staffId)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("A staff account identifier is required.", nameof(staffId));
        }
    }

    internal static void RequireDifferentStaffAccount(ActionActor actor, Guid staffId)
    {
        if (actor.Kind == ActorKind.Staff
            && Guid.TryParse(actor.SubjectId, out var actorStaffId)
            && actorStaffId == staffId)
        {
            throw new StaffAccountAdministrationException(
                StaffAccountAdministrationError.SelfAction);
        }
    }

    private static T NormalizeAdministrativeAction<T>(
        T request,
        ActionActor actor,
        Guid staffId,
        string reason,
        string operationKey,
        Func<string, string, T> normalized)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);
        RequireAdministrator(actor, StaffAccessRight.ManageStaffAccounts);
        RequireStaffId(staffId);
        RequireDifferentStaffAccount(actor, staffId);
        return normalized(
            NormalizeRequiredText(reason, MaximumReasonLength, nameof(reason)),
            NormalizeRequiredText(operationKey, MaximumOperationKeyLength, nameof(operationKey)));
    }

    internal static string NormalizeRequiredText(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maximumLength} characters.",
                parameterName);
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

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static void RequireAdministrator(ActionActor actor, StaffAccessRight accessRight)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, accessRight);
    }
}

public enum StaffAccountAdministrationError
{
    DuplicateUserName,
    InvalidAccount,
    StaffAccountNotFound,
    DisabledAccount,
    LastAdministrator,
    SelfAction,
    OperationConflict,
    SignOffEngineerRequiresEngineerRole,
    SignOffPrintedNameRequired,
    IneligibleSignOffEngineer
}

public sealed class StaffAccountAdministrationException(
    StaffAccountAdministrationError error)
    : Exception("The staff account request could not be completed.")
{
    public StaffAccountAdministrationError Error { get; } = error;
}
