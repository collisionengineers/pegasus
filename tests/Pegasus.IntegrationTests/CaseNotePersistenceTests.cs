using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-017 shipped with its Core command covered by a recording fake, so
/// nothing ever asserted where the note actually landed. It landed in
/// `CaseHistory`; the Notes tab reads `CaseWorkflowEvents`
/// (`EfCaseQueryStore`). The note persisted, the page said "The note was
/// added.", and the timeline stayed empty. These facts pin the write to the
/// table the read uses.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseNotePersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnOperatorNoteLandsOnTheTimelineTheNotesTabReads()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ICaseNoteStore>();
            await store.AddAsync(
                new(caseId, actor, "operation-one", "The claimant called about the inspection."),
                FixedUtcNow,
                CancellationToken.None);
        }

        await using var context = await database.CreateContextAsync();
        var written = await context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.EventType == AddCaseNote.EventType)
            .ToArrayAsync();

        var note = Assert.Single(written);
        Assert.Equal("The claimant called about the inspection.", note.Reason);
        Assert.Equal(actor.SubjectId, note.ActorSubjectId);
        Assert.Equal(nameof(ActorKind.Staff), note.ActorKind);
        Assert.Equal(FixedUtcNow, note.OccurredAtUtc);

        // A note records itself and changes nothing about the case.
        Assert.Equal(note.BeforeVersion, note.AfterVersion);
        Assert.Equal(1, await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.Version)
            .SingleAsync());

        // And it is not written anywhere the timeline cannot see.
        Assert.Empty(await context.CaseHistory
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ResubmittingTheSameNoteFormLeavesOneEntry()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ICaseNoteStore>();
            var request = new AddCaseNoteRequest(caseId, actor, "operation-one", "Chased the engineer.");
            await store.AddAsync(request, FixedUtcNow, CancellationToken.None);
            await store.AddAsync(request, FixedUtcNow.AddMinutes(5), CancellationToken.None);
        }

        await using var context = await database.CreateContextAsync();
        Assert.Single(await context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.EventType == AddCaseNote.EventType)
            .ToArrayAsync());
    }

    [Fact]
    public async Task DifferentNotesAtTheSameWorkflowVersionBothRemainOnTheTimeline()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ICaseNoteStore>();
            await store.AddAsync(new(caseId, actor, "operation-one", "First note."), FixedUtcNow, CancellationToken.None);
            await store.AddAsync(new(caseId, actor, "operation-two", "Second note."), FixedUtcNow.AddMinutes(1), CancellationToken.None);
        }

        await using var context = await database.CreateContextAsync();
        Assert.Equal(2, await context.CaseWorkflowEvents
            .AsNoTracking()
            .CountAsync(item => item.CaseId == caseId && item.EventType == AddCaseNote.EventType));
    }

    [Fact]
    public async Task ConcurrentResubmissionWithTheSameOperationKeyLeavesOneEntry()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var request = new AddCaseNoteRequest(
            caseId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            "concurrent-operation",
            "The repairer confirmed collection.");

        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ICaseNoteStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<ICaseNoteStore>();
        await Task.WhenAll(
            first.AddAsync(request, FixedUtcNow, CancellationToken.None),
            second.AddAsync(request, FixedUtcNow, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        Assert.Single(await context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.OperationKey == request.OperationKey)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ReusingAnOperationKeyForDifferentNoteContentIsRefused()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICaseNoteStore>();
        await store.AddAsync(new(caseId, actor, "reused-operation", "Original note."), FixedUtcNow, CancellationToken.None);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => store.AddAsync(
            new(caseId, actor, "reused-operation", "Changed note."), FixedUtcNow, CancellationToken.None));

        Assert.Contains("operation key", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheStoreRejectsAnOversizedNoteWithoutTruncatingIt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ICaseNoteStore>();

        var failure = await Assert.ThrowsAsync<ArgumentException>(() => store.AddAsync(
            new(caseId, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), "oversized-operation",
                new string('n', AddCaseNote.MaximumLength + 1)),
            FixedUtcNow,
            CancellationToken.None));

        Assert.Contains(AddCaseNote.MaximumLength.ToString(System.Globalization.CultureInfo.InvariantCulture), failure.Message, StringComparison.Ordinal);
        await using var context = await database.CreateContextAsync();
        Assert.Empty(await context.CaseWorkflowEvents
            .AsNoTracking()
            .Where(item => item.CaseId == caseId && item.OperationKey == "oversized-operation")
            .ToArrayAsync());
    }

    private static async Task<Guid> SeedCaseAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "Notes test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = FixedUtcNow },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = "NOTE",
                IsActive = true,
                Version = 0
            },
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "notes-origin.pdf",
                MediaType = "application/pdf",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"notes:{receiptId:N}",
                ReceivedAtUtc = FixedUtcNow,
                ProcessedAtUtc = FixedUtcNow,
                SourceReaderKey = "notes-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Notes test",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2031,
                Sequence = 3,
                Reference = "NOTE31003",
                Type = "Audit",
                InitialState = "NotReady",
                CustodyState = "Pending",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = FixedUtcNow,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "Review",
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }
}
