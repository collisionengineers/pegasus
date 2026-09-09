using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Eva;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-04 persistence coverage for distinct manual submissions and their
/// retained outcomes. The database permits explicit operator re-sends.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class EvaSubmissionPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task TwoManualDeliveredSubmissionsAreRetainedAsDistinctHandoffs()
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
            await context.SaveChangesAsync();
            Assert.Equal(2, await context.EvaSubmissions.CountAsync(item => item.CaseId == caseId));
        }
    }

    /// <summary>
    /// A partial delivery remains a distinct recorded outcome and does not
    /// prevent an explicit later manual handoff.
    /// </summary>
    [Fact]
    public async Task PartialDeliveryDoesNotBlockAnExplicitManualResend()
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
            await context.SaveChangesAsync();
            Assert.Equal(2, await context.EvaSubmissions.CountAsync(item => item.CaseId == caseId));
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
    /// key. A case can carry distinct explicit manual attempts, and answering
    /// by recency would report an outcome that never belonged to the key being
    /// replayed.
    /// </summary>
    [Fact]
    public async Task AttemptsAreDistinguishedByTheirOperationKey()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        const string firstOperation = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string secondOperation = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        await using (var context = await database.CreateContextAsync())
        {
            var first = Submission(caseId, EvaSubmissionOutcome.Unknown);
            first.OperationKey = firstOperation;
            first.SubmittedAtUtc = FixedUtcNow;
            var second = Submission(caseId, EvaSubmissionOutcome.Rejected);
            second.OperationKey = secondOperation;
            second.SubmittedAtUtc = FixedUtcNow.AddMinutes(5);
            context.EvaSubmissions.AddRange(first, second);
            await context.SaveChangesAsync();
        }

        await using (var context = await database.CreateContextAsync())
        {
            var replayed = await context.EvaSubmissions
                .AsNoTracking()
                .SingleAsync(item => item.CaseId == caseId && item.OperationKey == firstOperation);

            // The later manual attempt must not answer for the earlier
            // explicit one, which is exactly what ordering by recency did.
            Assert.Equal(nameof(EvaSubmissionOutcome.Unknown), replayed.Outcome);
        }
    }

    [Fact]
    public async Task LatestSubmissionIsTheChronologicallyLatestAttempt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var caseId = await SeedCaseAsync(database);
        await using (var context = await database.CreateContextAsync())
        {
            var succeeded = Submission(caseId, EvaSubmissionOutcome.Succeeded);
            succeeded.SubmittedAtUtc = FixedUtcNow;
            var rejected = Submission(caseId, EvaSubmissionOutcome.Rejected);
            rejected.SubmittedAtUtc = FixedUtcNow.AddMinutes(1);
            context.EvaSubmissions.AddRange(succeeded, rejected);
            await context.SaveChangesAsync();
        }

        var factory = new PooledDbContextFactory<PegasusDbContext>(
            new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(database.ConnectionString)
                .Options);

        var latest = await new EfEvaSubmissionQueries(factory).GetLatestAsync(caseId);

        Assert.NotNull(latest);
        Assert.Equal(EvaSubmissionOutcome.Rejected, latest.Outcome);
    }

    [Fact]
    public async Task AnExpiredAutomaticDispatchIsReconciliationRequiredAndIsNeverClaimedAgain()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var nowUtc = FixedUtcNow;
        var expiredCaseId = await SeedCaseAsync(database, "expired");
        var pendingCaseId = await SeedCaseAsync(database, "pending");
        var expiredIntentId = Guid.NewGuid();
        var pendingIntentId = Guid.NewGuid();

        await using (var context = await database.CreateContextAsync())
        {
            context.Set<AutomaticEvaReviewSubmissionEntity>().AddRange(
                new()
                {
                    Id = expiredIntentId,
                    CaseId = expiredCaseId,
                    WorkflowVersion = 1,
                    OperationKey = Guid.NewGuid().ToString("N"),
                    State = nameof(AutomaticEvaReviewSubmissionState.Dispatching),
                    CreatedAtUtc = nowUtc.AddMinutes(-2),
                    DueAtUtc = nowUtc.AddMinutes(-2),
                    LeaseToken = "expired-lease",
                    LeaseExpiresAtUtc = nowUtc.AddMinutes(-1)
                },
                new()
                {
                    Id = pendingIntentId,
                    CaseId = pendingCaseId,
                    WorkflowVersion = 1,
                    OperationKey = Guid.NewGuid().ToString("N"),
                    State = nameof(AutomaticEvaReviewSubmissionState.Pending),
                    CreatedAtUtc = nowUtc.AddMinutes(-1),
                    DueAtUtc = nowUtc
                });
            await context.SaveChangesAsync();
        }

        var factory = new PooledDbContextFactory<PegasusDbContext>(
            new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(database.ConnectionString)
                .Options);
        var store = new EfAutomaticEvaReviewSubmissionStore(factory, TimeProvider.System);

        var claim = await store.ClaimAsync(nowUtc, TimeSpan.FromMinutes(1), default);

        Assert.NotNull(claim);
        Assert.Equal(pendingIntentId, claim.Intent.Id);
        await using var verification = await database.CreateContextAsync();
        var expired = await verification.Set<AutomaticEvaReviewSubmissionEntity>()
            .SingleAsync(item => item.Id == expiredIntentId);
        Assert.Equal(nameof(AutomaticEvaReviewSubmissionState.ReconciliationRequired), expired.State);
        Assert.Equal(nowUtc, expired.CompletedAtUtc);
        Assert.Null(expired.LeaseToken);
        Assert.Null(expired.LeaseExpiresAtUtc);
    }

    [Fact]
    public async Task AutomaticFailureRetryQueryIncludesUndeliveredAttemptsAndOuterFailures()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var outerFailureCaseId = await SeedCaseAsync(database, "outer");
        var retainedFailureCaseId = await SeedCaseAsync(database, "retained");
        await using (var context = await database.CreateContextAsync())
        {
            context.Set<AutomaticEvaReviewSubmissionEntity>().Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = outerFailureCaseId,
                WorkflowVersion = 1,
                OperationKey = Guid.NewGuid().ToString("N"),
                State = nameof(AutomaticEvaReviewSubmissionState.ReconciliationRequired),
                CreatedAtUtc = FixedUtcNow,
                DueAtUtc = FixedUtcNow,
                CompletedAtUtc = FixedUtcNow
            });
            context.EvaSubmissions.Add(Submission(retainedFailureCaseId, EvaSubmissionOutcome.Unknown));
            await context.SaveChangesAsync();
        }

        var factory = new PooledDbContextFactory<PegasusDbContext>(
            new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(database.ConnectionString)
                .Options);
        var queries = new EfEvaSubmissionQueries(factory);

        Assert.True(await queries.CanRetryAutomaticFailureAsync(outerFailureCaseId));
        Assert.True(await queries.CanRetryAutomaticFailureAsync(retainedFailureCaseId));
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

    private static async Task<Guid> SeedCaseAsync(
        LocalDbTestDatabase database,
        string fixture = "default")
    {
        await using var context = await database.CreateContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var reference = fixture == "default"
            ? "EVA31003"
            : $"EVA{fixture.ToUpperInvariant()}31003";
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = $"EVA test {fixture}", Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = FixedUtcNow },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = $"EVA{fixture}".ToUpperInvariant(),
                IsActive = true,
                ReportGenerationPolicy = "EvaManualApi",
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
                Reference = reference,
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
