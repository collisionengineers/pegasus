using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Documents;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class PublicUploadOccurrenceReplacementSchemaTests
{
    [Fact]
    public void ReplacementLineageIsNullableRestrictedAndSessionScoped()
    {
        var options = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=PegasusSchemaOnly;Integrated Security=true;Encrypt=false")
            .Options;
        using var context = new PegasusDbContext(options);
        var occurrence = context.Model.FindEntityType(typeof(PublicUploadOccurrenceEntity));
        Assert.NotNull(occurrence);

        Assert.True(occurrence!.FindProperty(nameof(PublicUploadOccurrenceEntity.ReplacesOccurrenceId))!.IsNullable);
        var relationship = Assert.Single(
            occurrence.GetForeignKeys(),
            key => key.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(PublicUploadOccurrenceEntity.SessionId), nameof(PublicUploadOccurrenceEntity.ReplacesOccurrenceId)]));
        Assert.Same(occurrence, relationship.PrincipalEntityType);
        Assert.Equal(
            [nameof(PublicUploadOccurrenceEntity.SessionId), nameof(PublicUploadOccurrenceEntity.Id)],
            relationship.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior);
        var lookup = Assert.Single(
            occurrence.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(PublicUploadOccurrenceEntity.SessionId), nameof(PublicUploadOccurrenceEntity.ReplacesOccurrenceId)]));
        Assert.False(lookup.IsUnique);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task MigratedDatabaseEnforcesReplacementWithinTheSameSession()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var context = await database.CreateContextAsync();
        var seeded = await SeededPrincipals.QdosAsync(context);
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var firstSessionId = Guid.NewGuid();
        var secondSessionId = Guid.NewGuid();
        var firstLinkId = Guid.NewGuid();
        var secondLinkId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        context.AddRange(
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "replacement.eml",
                MediaType = "message/rfc822",
                SourceLength = 1,
                SourceHash = new string('A', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"replacement:{Guid.NewGuid():N}",
                ReceivedAtUtc = now,
                ProcessedAtUtc = now,
                SourceReaderKey = "replacement-schema-test",
                SourceReaderVersion = "1",
                Decision = "case_created",
                DecisionReason = "Replacement schema test fixture.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = seeded.Id,
                SequenceLineageId = seeded.SequenceLineageId,
                Year = 2031,
                Sequence = 1,
                Reference = "QDOS001",
                Type = "Desktop",
                InitialState = "Not ready",
                CustodyState = "Confirmed",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = now,
                ConcurrencyToken = Guid.NewGuid()
            });
        context.AddRange(
            Link(firstLinkId, caseId, now, "first"),
            Link(secondLinkId, caseId, now, "second"),
            Session(firstSessionId, firstLinkId),
            Session(secondSessionId, secondLinkId));
        var predecessor = Occurrence(firstSessionId, null, "predecessor");
        var otherSessionOccurrence = Occurrence(secondSessionId, null, "other-session");
        context.AddRange(predecessor, otherSessionOccurrence);
        await context.SaveChangesAsync();

        context.Add(Occurrence(firstSessionId, predecessor.Id, "same-session-replacement"));
        await context.SaveChangesAsync();

        context.Add(Occurrence(firstSessionId, otherSessionOccurrence.Id, "cross-session-replacement"));
        var crossSession = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(crossSession.InnerException).Number);
        context.ChangeTracker.Clear();

        context.Add(Occurrence(firstSessionId, Guid.NewGuid(), "missing-replacement"));
        var missing = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(missing.InnerException).Number);

        const string foreignKeyName =
            "FK_PublicUploadOccurrences_PublicUploadOccurrences_SessionId_ReplacesOccurrenceId";
        Assert.Equal("NO_ACTION", await database.ScalarAsync<string>(
            $"SELECT delete_referential_action_desc FROM sys.foreign_keys WHERE name = '{foreignKeyName}'"));
        Assert.Equal("SessionId,ReplacesOccurrenceId", await database.ScalarAsync<string>(
            $"""
            SELECT STRING_AGG(COL_NAME(parent_object_id, parent_column_id), ',')
                WITHIN GROUP (ORDER BY constraint_column_id)
            FROM sys.foreign_key_columns
            WHERE constraint_object_id = OBJECT_ID('{foreignKeyName}')
            """));
    }

    private static RequestUploadLinkEntity Link(
        Guid id,
        Guid caseId,
        DateTimeOffset now,
        string operation) => new()
        {
            Id = id,
            CaseId = caseId,
            TokenDigest = new string(operation[0], 64),
            Status = RequestUploadStatus.Active,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(1),
            LimitsVersion = "replacement-v1",
            Version = 1,
            CreateOperationKey = $"request:{operation}"
        };

    private static PublicUploadSessionEntity Session(Guid id, Guid linkId) => new()
    {
        Id = id,
        RequestUploadLinkId = linkId,
        LimitsVersion = "replacement-v1",
        Version = 0,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static PublicUploadOccurrenceEntity Occurrence(
        Guid sessionId,
        Guid? replacesOccurrenceId,
        string operation) => new()
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            ReplacesOccurrenceId = replacesOccurrenceId,
            OperationKey = operation,
            ProposedName = "evidence.txt",
            MediaType = "text/plain",
            Size = 1,
            Sha256 = new string('B', 64),
            CustodyState = "Confirmed"
        };
}
