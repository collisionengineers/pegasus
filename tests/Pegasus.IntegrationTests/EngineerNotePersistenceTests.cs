using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class EngineerNotePersistenceTests
{
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AppendIsAttributedReplaySafeOrderedAndSeparateFromCaseHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        const string token = "engineer-note-lease";
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review, actor, token);
        var first = new AddEngineerNoteRequest(caseId, actor, 3, "note-one", "First note", token);

        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IEngineerNoteStore>();
            await store.AddAsync(first, RecordedAtUtc, CancellationToken.None);
            await store.AddAsync(first, RecordedAtUtc.AddMinutes(1), CancellationToken.None);
        }

        await RestoreLeaseAsync(database, caseId, actor, token);
        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IEngineerNoteStore>();
            await store.AddAsync(
                first with { OperationKey = "note-two", Note = "Correction" },
                RecordedAtUtc.AddMinutes(2),
                CancellationToken.None);
            var notes = await scope.ServiceProvider
                .GetRequiredService<IEngineerNoteQueries>()
                .ListNewestFirstAsync(caseId, CancellationToken.None);

            Assert.Equal(["Correction", "First note"], notes.Select(note => note.Note));
            Assert.All(notes, note => Assert.Equal(Guid.Parse(actor.SubjectId), note.RecordedByStaffId));
        }

        await using var context = await database.CreateContextAsync();
        Assert.Equal(2, await context.EngineerNotes.CountAsync(item => item.CaseId == caseId));
        Assert.Empty(await context.CaseWorkflowEvents
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync());
        Assert.Empty(await context.CaseHistory
            .Where(item => item.CaseId == caseId)
            .ToArrayAsync());
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        Assert.Equal(3, workflow.Version);
        Assert.Null(workflow.EditLeaseTokenHash);
        Assert.Null(workflow.EditLeaseHolder);
        Assert.Null(workflow.EditLeaseExpiresAtUtc);
    }

    [Fact]
    public async Task SameOperationWithDifferentPayloadConflictsBeforeLeaseGuards()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        const string token = "conflict-lease";
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review, actor, token);
        var request = new AddEngineerNoteRequest(caseId, actor, 3, "same-key", "Original", token);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IEngineerNoteStore>();
        await store.AddAsync(request, RecordedAtUtc, CancellationToken.None);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() => store.AddAsync(
            request with { Note = "Altered" },
            RecordedAtUtc.AddMinutes(1),
            CancellationToken.None));
    }

    [Fact]
    public async Task VersionAndLeaseAreRequired()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        const string token = "guard-lease";
        var caseId = await SeedCaseAsync(database, CaseLifecycleState.Review, actor, token);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IEngineerNoteStore>();
        await Assert.ThrowsAsync<CaseVersionConflictException>(() => store.AddAsync(
            new(caseId, actor, 2, "stale", "Stale", token),
            RecordedAtUtc,
            CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() => store.AddAsync(
            new(caseId, actor, 3, "missing", "Missing", ""),
            RecordedAtUtc,
            CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() => store.AddAsync(
            new(caseId, actor, 3, "wrong", "Wrong", "wrong-token"),
            RecordedAtUtc,
            CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() => store.AddAsync(
            new(caseId, actor, 3, "expired", "Expired", token),
            RecordedAtUtc.AddHours(1),
            CancellationToken.None));
    }

    [Fact]
    public async Task TerminalCaseWithAHeldLeaseAcceptsANote()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        const string token = "terminal-lease";
        var caseId = await SeedCaseAsync(
            database,
            CaseLifecycleState.PostReportComplete,
            actor,
            token);

        await using var scope = database.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IEngineerNoteStore>().AddAsync(
            new(caseId, actor, 3, "terminal-note", "Closed-case note", token),
            RecordedAtUtc,
            CancellationToken.None);

        Assert.Single(await scope.ServiceProvider
            .GetRequiredService<IEngineerNoteQueries>()
            .ListNewestFirstAsync(caseId, CancellationToken.None));
    }

    private static async Task<Guid> SeedCaseAsync(
        LocalDbTestDatabase database,
        CaseLifecycleState state,
        ActionActor actor,
        string token)
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "Engineer notes test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = RecordedAtUtc },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = "ENOT",
                IsActive = true,
                Version = 0
            },
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "engineer-note-origin.pdf",
                MediaType = "application/pdf",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"engineer-note:{receiptId:N}",
                ReceivedAtUtc = RecordedAtUtc,
                ProcessedAtUtc = RecordedAtUtc,
                SourceReaderKey = "engineer-note-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Engineer notes test",
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
                Sequence = 4,
                Reference = $"ENOT31{Random.Shared.Next(1000, 9999)}",
                Type = "Inspection",
                InitialState = nameof(CaseLifecycleState.NotReady),
                CustodyState = "Pending",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = RecordedAtUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = state.ToString(),
                Version = 3,
                EditLeaseTokenHash = TokenHash(token),
                EditLeaseHolder = actor.SubjectId,
                EditLeaseHolderKind = actor.Kind.ToString(),
                EditLeaseOperationKey = "claim",
                EditLeaseExpiresAtUtc = RecordedAtUtc.AddMinutes(30),
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }

    private static async Task RestoreLeaseAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        ActionActor actor,
        string token)
    {
        await using var context = await database.CreateContextAsync();
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.EditLeaseTokenHash = TokenHash(token);
        workflow.EditLeaseHolder = actor.SubjectId;
        workflow.EditLeaseHolderKind = actor.Kind.ToString();
        workflow.EditLeaseOperationKey = "claim-again";
        workflow.EditLeaseExpiresAtUtc = RecordedAtUtc.AddMinutes(30);
        await context.SaveChangesAsync();
    }

    private static string TokenHash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
