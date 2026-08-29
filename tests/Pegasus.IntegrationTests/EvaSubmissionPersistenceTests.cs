using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Eva;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-04. The once-per-case rule is the load-bearing safety property of the
/// whole EVA integration, and it is the one rule no amount of unit testing can
/// prove: EVA has no idempotency, so a second accepted instruction creates a
/// second claim with its own File Reference that no API call can withdraw.
/// Code refuses the second submission, and
/// <c>UX_EvaSubmissions_CaseDelivered</c> refuses it again underneath — but a
/// filtered unique index that is subtly wrong looks identical to a correct one
/// until a real database is asked to enforce it.
///
/// So these tests ask a real database. They also pin the filter itself, which
/// is what makes "at most one success, any number of failures" expressible:
/// the failures are exactly what a caller needs in order to decide whether to
/// try again, and an unfiltered index would delete that history.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class EvaSubmissionPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ACaseReachesEvaAtMostOnce()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);

        await using (var context = await database.CreateContextAsync())
        {
            context.EvaSubmissions.Add(Submission(caseId, EvaSubmissionOutcome.Succeeded));
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            context.EvaSubmissions.Add(Submission(caseId, EvaSubmissionOutcome.Succeeded));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    /// <summary>
    /// A partial delivery closes the rule too. EVA accepted the instruction and
    /// returned no identifier — the claim exists all the same, and a second
    /// send would create another one that no API call can withdraw. The index
    /// is filtered on delivery rather than on success for exactly this case.
    /// </summary>
    [Fact]
    public async Task AnAcceptanceWithoutAnIdentifierAlsoClosesTheCase()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);

        await using (var context = await database.CreateContextAsync())
        {
            context.EvaSubmissions.Add(Submission(caseId, EvaSubmissionOutcome.Partial));
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            context.EvaSubmissions.Add(Submission(caseId, EvaSubmissionOutcome.Succeeded));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
    }

    /// <summary>
    /// Every attempt is kept, not only the successful one. A rejection and an
    /// unknown outcome call for opposite responses, and they are
    /// indistinguishable from the case if the failures do not survive.
    /// </summary>
    [Fact]
    public async Task EveryFailedAttemptIsRetainedAlongsideTheSuccess()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);

        await using (var context = await database.CreateContextAsync())
        {
            context.EvaSubmissions.AddRange(
                Submission(caseId, EvaSubmissionOutcome.Unknown),
                Submission(caseId, EvaSubmissionOutcome.Unknown),
                Submission(caseId, EvaSubmissionOutcome.Rejected),
                Submission(caseId, EvaSubmissionOutcome.Succeeded));
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            var rows = await context.EvaSubmissions
                .AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .ToArrayAsync();

            Assert.Equal(4, rows.Length);
            Assert.Single(rows, row => row.IsDelivered);
        }
    }

    /// <summary>
    /// The two outcome columns must agree. IsDelivered exists only to drive
    /// the filtered index, so a row that says one thing in the enum and
    /// another in the flag would put the wrong rows under the unique
    /// constraint.
    /// </summary>
    [Fact]
    public async Task AnOutcomeCannotDisagreeWithItsSuccessFlag()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);

        await using var context = await database.CreateContextAsync();
        var lying = Submission(caseId, EvaSubmissionOutcome.Rejected);
        lying.IsDelivered = true;
        context.EvaSubmissions.Add(lying);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>
    /// FRD-07 requires the four outcomes stay distinct, so the database knows
    /// their names and refuses a fifth.
    /// </summary>
    [Fact]
    public async Task AnUnknownOutcomeNameIsRefused()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);

        await using var context = await database.CreateContextAsync();
        var invented = Submission(caseId, EvaSubmissionOutcome.Rejected);
        invented.Outcome = "Maybe";
        context.EvaSubmissions.Add(invented);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    /// <summary>
    /// A replay is answered from the attempt that ran under its own operation
    /// key. A case can carry attempts from more than one — an automatic sweep
    /// and a later manual send — and answering by recency would report an
    /// outcome that never belonged to the key being replayed.
    /// </summary>
    [Fact]
    public async Task AttemptsAreDistinguishedByTheirOperationKey()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        const string automatic = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string manual = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        await using (var context = await database.CreateContextAsync())
        {
            var first = Submission(caseId, EvaSubmissionOutcome.Unknown);
            first.OperationKey = automatic;
            first.SubmittedAtUtc = FixedUtcNow;
            var second = Submission(caseId, EvaSubmissionOutcome.Rejected);
            second.OperationKey = manual;
            second.SubmittedAtUtc = FixedUtcNow.AddMinutes(5);
            context.EvaSubmissions.AddRange(first, second);
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            var replayed = await context.EvaSubmissions
                .AsNoTracking()
                .SingleAsync(item => item.CaseId == caseId && item.OperationKey == automatic);

            // The later manual attempt must not answer for the earlier
            // automatic one, which is exactly what ordering by recency did.
            Assert.Equal(nameof(EvaSubmissionOutcome.Unknown), replayed.Outcome);
        }
    }

    private static EvaSubmissionEntity Submission(Guid caseId, EvaSubmissionOutcome outcome) => new()
    {
        Id = Guid.CreateVersion7(),
        CaseId = caseId,
        WorkflowVersion = 1,
        ExternalRef = "EVA31003",
        OperationKey = Guid.NewGuid().ToString("N"),
        Outcome = outcome.ToString(),
        IsDelivered = outcome is EvaSubmissionOutcome.Succeeded or EvaSubmissionOutcome.Partial,
        EvaId = outcome == EvaSubmissionOutcome.Succeeded ? "600005" : null,
        FileReference = outcome == EvaSubmissionOutcome.Succeeded ? "61239" : null,
        FailureCode = outcome == EvaSubmissionOutcome.Succeeded ? null : "eva_unreachable",
        ImagesSent = outcome == EvaSubmissionOutcome.Succeeded ? 3 : 0,
        AttemptCount = 1,
        ActorSubjectId = Guid.NewGuid().ToString("D"),
        SubmittedAtUtc = FixedUtcNow
    };

    private static async Task<Guid> SeedCaseAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "EVA test", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = FixedUtcNow },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = "EVA",
                IsActive = true,
                EvaManualSubmission = true,
                Version = 0
            },
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "eva-origin.pdf",
                MediaType = "application/pdf",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"eva:{receiptId:N}",
                ReceivedAtUtc = FixedUtcNow,
                ProcessedAtUtc = FixedUtcNow,
                SourceReaderKey = "eva-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "EVA test",
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
                Reference = "EVA31003",
                Type = "Inspection",
                InitialState = "Review",
                CustodyState = "Confirmed",
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
