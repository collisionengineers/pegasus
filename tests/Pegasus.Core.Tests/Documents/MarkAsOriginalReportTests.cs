using Pegasus.Core.Assessment;
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
        var reader = new RecordingReader(Reading);
        var sut = new MarkAsOriginalReport(store, reader);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.ExecuteAsync(
            Request() with { Actor = ActionActor.Provider(Guid.NewGuid()) }));

        Assert.Empty(store.Commands);
        Assert.Empty(reader.Reads);
    }

    [Fact]
    public async Task ADocumentVersionIsRequired()
    {
        var store = new RecordingStore();
        var sut = new MarkAsOriginalReport(store, new RecordingReader(Reading));

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExecuteAsync(
            Request() with { DocumentVersionId = Guid.Empty }));

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
    public async Task AnOpenAuditDocumentReachesTheStoreWithItsOwnReading()
    {
        var store = new RecordingStore();
        var reader = new RecordingReader(Reading);
        var sut = new MarkAsOriginalReport(store, reader);
        var request = Request();

        var result = await sut.ExecuteAsync(request);

        Assert.Equal(request, Assert.Single(store.Commands));
        Assert.Equal(Reading, Assert.Single(store.Readings));
        Assert.Equal(
            (request.Actor, request.CaseId, request.DocumentOccurrenceId, request.DocumentVersionId),
            Assert.Single(reader.Reads));
        Assert.Equal(request.DocumentOccurrenceId, result.DocumentOccurrenceId);
    }

    [Fact]
    public async Task ADocumentThatCannotBeReadIsStillMarked()
    {
        var store = new RecordingStore();
        var sut = new MarkAsOriginalReport(store, new RecordingReader(null));

        await sut.ExecuteAsync(Request());

        Assert.Single(store.Commands);
        Assert.Null(Assert.Single(store.Readings));
    }

    private static readonly OriginalReportReading Reading =
        new(new string('a', 64), "Laird Assessors", "2026-09-01", "roadworthy", "repairable", false);

    private static MarkAsOriginalReportCommand Request() => new(
        Guid.NewGuid(),
        4,
        Staff,
        "mark-original-report",
        "edit-lease-token",
        Guid.NewGuid(),
        Guid.NewGuid());

    private sealed class RecordingReader(OriginalReportReading? reading) : IReadOriginalReport
    {
        public List<(ActionActor, Guid, Guid, Guid)> Reads { get; } = [];

        public Task<OriginalReportReading?> ForIntakeAsync(
            Guid receiptId, Guid standaloneAuditEvidenceId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<OriginalReportReading?> ForDocumentAsync(
            ActionActor actor, Guid caseId, Guid occurrenceId, Guid versionId, CancellationToken cancellationToken)
        {
            Reads.Add((actor, caseId, occurrenceId, versionId));
            return Task.FromResult(reading);
        }
    }

    private sealed class RecordingStore : IMarkAsOriginalReportStore
    {
        public List<MarkAsOriginalReportCommand> Commands { get; } = [];

        public List<OriginalReportReading?> Readings { get; } = [];

        public Task<OriginalReportRecorded> MarkAsOriginalReportAsync(
            MarkAsOriginalReportCommand command,
            OriginalReportReading? reading,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            Readings.Add(reading);
            return Task.FromResult(new OriginalReportRecorded(
                command.CaseId,
                command.DocumentOccurrenceId,
                "report.pdf",
                command.ExpectedVersion + 1));
        }
    }
}
