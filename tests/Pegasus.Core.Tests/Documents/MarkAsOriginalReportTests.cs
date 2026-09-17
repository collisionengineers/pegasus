using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Documents;

public sealed class MarkAsOriginalReportTests
{
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task PerformCaseworkIsRequired()
    {
        var store = new RecordingStore();
        var sut = new MarkAsOriginalReport(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.ExecuteAsync(
            Request() with { Actor = ActionActor.RequestLink(Guid.NewGuid()) }));

        Assert.Empty(store.Commands);
    }

    [Fact]
    public void AClosedCaseIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OriginalReportPolicy.RequireEligible(
                CaseType.Audit,
                CaseLifecycleState.ProviderCancelled,
                DocumentSemanticRole.Instruction));
    }

    [Fact]
    public void ACompletedCaseIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OriginalReportPolicy.RequireEligible(
                CaseType.Audit,
                CaseLifecycleState.PostReportComplete,
                DocumentSemanticRole.Instruction));
    }

    [Fact]
    public void ANonAuditCaseIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OriginalReportPolicy.RequireEligible(
                CaseType.Inspection,
                CaseLifecycleState.NotReady,
                DocumentSemanticRole.Instruction));
    }

    [Fact]
    public void AnImageOccurrenceIsRefused()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OriginalReportPolicy.RequireEligible(
                CaseType.Audit,
                CaseLifecycleState.NotReady,
                DocumentSemanticRole.Image));
    }

    [Fact]
    public async Task AnOpenAuditDocumentReachesTheStore()
    {
        var store = new RecordingStore();
        var sut = new MarkAsOriginalReport(store);
        var request = Request();

        var result = await sut.ExecuteAsync(request);

        Assert.Equal(request, Assert.Single(store.Commands));
        Assert.Equal(request.DocumentOccurrenceId, result.DocumentOccurrenceId);
    }

    private static MarkAsOriginalReportCommand Request() => new(
        Guid.NewGuid(),
        4,
        Staff,
        "mark-original-report",
        "edit-lease-token",
        Guid.NewGuid());

    private sealed class RecordingStore : IMarkAsOriginalReportStore
    {
        public List<MarkAsOriginalReportCommand> Commands { get; } = [];

        public Task<OriginalReportRecorded> MarkAsOriginalReportAsync(
            MarkAsOriginalReportCommand command,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return Task.FromResult(new OriginalReportRecorded(
                command.CaseId,
                command.DocumentOccurrenceId,
                "report.pdf",
                command.ExpectedVersion + 1));
        }
    }
}
