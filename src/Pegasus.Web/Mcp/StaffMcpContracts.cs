using ModelContextProtocol;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Tasks;
using Pegasus.Core.Triage;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal enum StaffMcpCallOutcome
{
    Succeeded,
    NotFound,
    Conflict,
    Denied,
    Unavailable,
    Invalid
}

internal sealed record StaffMcpResult<T>(
    StaffMcpCallOutcome Outcome,
    T? Value,
    string? ErrorCode = null,
    long? CurrentVersion = null)
{
    public static StaffMcpResult<T> Succeeded(T value) =>
        new(StaffMcpCallOutcome.Succeeded, value);

    public static StaffMcpResult<T> NotFound() =>
        new(StaffMcpCallOutcome.NotFound, default, "not_found");
}

internal sealed record StaffMcpMutationReceipt(bool Completed = true);

internal static class StaffMcpCall
{
    public static async Task<StaffMcpResult<T>> ExecuteAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return StaffMcpResult<T>.Succeeded(await action());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CaseVersionConflictException exception)
        {
            return Conflict<T>(exception.ActualVersion);
        }
        catch (TriageVersionConflictException exception)
        {
            return Conflict<T>(exception.ActualVersion);
        }
        catch (CaseTaskVersionConflictException exception)
        {
            return Conflict<T>(exception.ActualVersion);
        }
        catch (CaseEditLeaseConflictException)
        {
            return Conflict<T>(errorCode: "lease_conflict");
        }
        catch (CaseEditLeaseExpiredException)
        {
            return Conflict<T>(errorCode: "lease_expired");
        }
        catch (IntakeVersionConflictException)
        {
            return Conflict<T>();
        }
        catch (IntakeOperationConflictException)
        {
            return Conflict<T>();
        }
        catch (IntakeAssociationConflictException)
        {
            return Conflict<T>();
        }
        catch (DocumentRequestUnavailableException)
        {
            return Unavailable<T>();
        }
        catch (VehicleSuggestionUnavailableException)
        {
            return Unavailable<T>();
        }
        catch (VehicleLookupUnavailableException)
        {
            return Unavailable<T>();
        }
        catch (StaffMcpAuthorizationException)
        {
            return Denied<T>();
        }
        catch (StaffAuthorizationException)
        {
            return Denied<T>();
        }
        catch (McpException)
        {
            return new(StaffMcpCallOutcome.Invalid, default, "invalid_request");
        }
        catch (KeyNotFoundException)
        {
            return StaffMcpResult<T>.NotFound();
        }
        catch (ArgumentException)
        {
            return new(StaffMcpCallOutcome.Invalid, default, "invalid_request");
        }
        catch (InvalidOperationException)
        {
            return Conflict<T>();
        }
        catch (Exception exception)
        {
            throw new McpException("The tool could not complete the request.", exception);
        }
    }

    public static Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteAsync(async () =>
        {
            await action();
            return new StaffMcpMutationReceipt();
        });
    }

    private static StaffMcpResult<T> Conflict<T>(
        long? currentVersion = null,
        string errorCode = "conflict") =>
        new(StaffMcpCallOutcome.Conflict, default, errorCode, currentVersion);

    private static StaffMcpResult<T> Denied<T>() =>
        new(StaffMcpCallOutcome.Denied, default, "denied");

    private static StaffMcpResult<T> Unavailable<T>() =>
        new(StaffMcpCallOutcome.Unavailable, default, "unavailable");
}

internal static class StaffMcpInput
{
    public static void RequireIdentifier(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new McpException($"'{parameterName}' must be a non-empty identifier.");
        }
    }

    public static void RequireVersion(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new McpException($"'{parameterName}' cannot be negative.");
        }
    }

    public static string RequireOperationKey(string? value) =>
        RequireText(value, "operationKey", 100);

    public static string RequireReason(string? value) =>
        RequireText(value, "reason", 500);

    public static string RequireLease(string? value) =>
        RequireText(value, "editLeaseToken", 128);

    public static string RequireText(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpException($"'{parameterName}' is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new McpException($"'{parameterName}' cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    public static void RequirePage(int page, int pageSize)
    {
        if (page is < 1 or > 10_000)
        {
            throw new McpException("'page' is outside the supported range.");
        }
        if (pageSize is < 1 or > 100)
        {
            throw new McpException("'pageSize' must be between 1 and 100.");
        }
    }
}
