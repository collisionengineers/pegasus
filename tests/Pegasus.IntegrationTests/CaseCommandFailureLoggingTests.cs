using Microsoft.Extensions.Logging;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Pages.Cases;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A designed case refusal is logged as a Warning with its type and message and
/// no exception object, so it never reaches the exception index that pages the
/// on-call alert. Any other failure keeps its exception (issue 835).
/// </summary>
public sealed class CaseCommandFailureLoggingTests
{
    private static readonly Guid CaseId = Guid.NewGuid();

    // Keyed by name: an exception is not serializable theory data, so the test
    // listing would count one case where five run.
    public static TheoryData<string> DesignedRefusals => new()
    {
        nameof(CaseEditLeaseExpiredException),
        nameof(CaseEditLeaseConflictException),
        nameof(CaseVersionConflictException),
        nameof(CaseOperationConflictException),
        nameof(CaseTerminalMutationException)
    };

    [Theory]
    [MemberData(nameof(DesignedRefusals))]
    public void ADesignedRefusalIsLoggedWithoutAnExceptionPayload(string refusalType)
    {
        Exception refusal = refusalType switch
        {
            nameof(CaseEditLeaseExpiredException) => new CaseEditLeaseExpiredException(CaseId, 3),
            nameof(CaseEditLeaseConflictException) => new CaseEditLeaseConflictException(CaseId, 3),
            nameof(CaseVersionConflictException) => new CaseVersionConflictException(CaseId, 2, 3),
            nameof(CaseOperationConflictException) => new CaseOperationConflictException(CaseId, "operation-1"),
            nameof(CaseTerminalMutationException) => new CaseTerminalMutationException(CaseId),
            _ => throw new ArgumentOutOfRangeException(nameof(refusalType), refusalType, null)
        };
        var logger = new CapturingLogger<CaseCommandFailureLoggingTests>();

        FailureLogger.Log(logger, CaseId, "claim_lease", refusal);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Contains(refusal.GetType().Name, entry.Message, StringComparison.Ordinal);
        Assert.Contains(refusal.Message, entry.Message, StringComparison.Ordinal);
        Assert.Contains("claim_lease", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnexpectedFailureKeepsItsExceptionPayload()
    {
        var logger = new CapturingLogger<CaseCommandFailureLoggingTests>();
        var fault = new InvalidOperationException("The store faulted.");

        FailureLogger.Log(logger, CaseId, "generate_report", fault);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(fault, entry.Exception);
    }

    private sealed class FailureLogger(ILogger logger) : CaseMutationPageModel(logger)
    {
        public static void Log(ILogger logger, Guid caseId, string commandName, Exception exception) =>
            LogCaseCommandFailed(logger, caseId, commandName, exception);
    }
}
