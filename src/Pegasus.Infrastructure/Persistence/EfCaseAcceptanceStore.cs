using System.Diagnostics;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfCaseAcceptanceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null)
    : ICaseAcceptanceStore
{
    private static readonly TimeZoneInfo LondonTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    public async Task<CaseAcceptanceOutcome> AcceptAsync(
        CaseAcceptanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PrincipalCode);
        ArgumentNullException.ThrowIfNull(request.Completeness);
        if (request.IntakeReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An intake receipt is required for case acceptance.", nameof(request));
        }

        if (request.ExpectedIntakeVersion < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The expected intake version cannot be negative.");
        }

        if (!Enum.IsDefined(request.CaseType)
            || (request.StandaloneAuditAssessment is { } assessment && !Enum.IsDefined(assessment)))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The case type or Audit assessment is invalid.");
        }

        if ((request.CaseType == CaseType.Audit) != (request.StandaloneAuditAssessment is not null))
        {
            throw new ArgumentException(
                "A standalone Audit, and only a standalone Audit, requires an assessment.",
                nameof(request));
        }
        if (request.Actor.Length > 200)
        {
            throw new ArgumentException("The case acceptance actor cannot exceed 200 characters.", nameof(request));
        }

        if (request.OperationKey.Length > 100)
        {
            throw new ArgumentException("The case acceptance operation key cannot exceed 100 characters.", nameof(request));
        }

        if (request.PrincipalCode.Trim().Length > 20)
        {
            throw new ArgumentException("The principal code cannot exceed 20 characters.", nameof(request));
        }

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                return await AcceptOnceAsync(request, cancellationToken);
            }
            catch (Exception exception) when (attempt < 3 && IsRetryableConcurrencyFailure(exception))
            {
                var duplicate = await FindAcceptedAsync(request.IntakeReceiptId, cancellationToken);
                if (duplicate is not null)
                {
                    return duplicate with { IsDuplicate = true };
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
        }

        throw new UnreachableException();
    }

    private async Task<CaseAcceptanceOutcome> AcceptOnceAsync(
        CaseAcceptanceRequest request,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existingLink = await context.CaseIntakeLinks
            .AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.IntakeReceiptId == request.IntakeReceiptId, cancellationToken);
        if (existingLink is not null)
        {
            return Map(existingLink.Case, existingLink.CustodyWorkId, true);
        }

        var receipt = await context.IntakeReceipts
            .SingleOrDefaultAsync(item => item.Id == request.IntakeReceiptId, cancellationToken)
            ?? throw new InvalidOperationException("The intake receipt does not exist.");
        if (receipt.Version != request.ExpectedIntakeVersion)
        {
            throw new DbUpdateConcurrencyException("The intake receipt changed before it could be accepted.");
        }

        if (!string.Equals(receipt.Decision, "draft_ready", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only a ready intake receipt can be accepted as a case.");
        }

        var principalCode = request.PrincipalCode.Trim().ToUpperInvariant();
        var principal = await context.Principals
            .SingleOrDefaultAsync(
                item => item.Code == principalCode && item.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException($"The active principal '{principalCode}' does not exist.");

        var acceptedAtUtc = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        var year = TimeZoneInfo.ConvertTime(acceptedAtUtc, LondonTimeZone).Year;
        var sequence = await context.CaseSequences.SingleOrDefaultAsync(
            item => item.SequenceLineageId == principal.SequenceLineageId && item.Year == year,
            cancellationToken);
        if (sequence is null)
        {
            sequence = new CaseSequenceEntity
            {
                SequenceLineageId = principal.SequenceLineageId,
                Year = year,
                LastAllocatedSequence = 0
            };
            context.CaseSequences.Add(sequence);
        }

        if (sequence.LastAllocatedSequence >= 999)
        {
            throw new CaseIdentitySequenceExhaustedException(principal.Code, year);
        }

        var allocatedSequence = ++sequence.LastAllocatedSequence;
        var reference = $"{principal.Code}{year % 100:00}{allocatedSequence:000}";
        var auditReference = request.CaseType == CaseType.Audit
            ? $"{AuditPrefix(request.StandaloneAuditAssessment!.Value)}{reference}"
            : null;
        var caseId = Guid.NewGuid();
        var custodyWorkId = Guid.NewGuid();
        var initialState = request.Completeness.IsReadyForReview(automaticallyDefinitive: true)
            ? CaseInitialState.Review
            : CaseInitialState.NotReady;

        var caseEntity = new CaseEntity
        {
            Id = caseId,
            PrincipalId = principal.Id,
            Principal = principal,
            SequenceLineageId = principal.SequenceLineageId,
            Year = year,
            Sequence = allocatedSequence,
            Reference = reference,
            AuditReference = auditReference,
            Type = ToCode(request.CaseType),
            InitialState = ToCode(initialState),
            CustodyState = ToCode(CaseCustodyState.Pending),
            OriginIntakeReceiptId = receipt.Id,
            StandaloneAuditAssessment = request.StandaloneAuditAssessment is null
                ? null
                : ToCode(request.StandaloneAuditAssessment.Value),
            InstructionComplete = request.Completeness.InstructionComplete,
            ImagesComplete = request.Completeness.ImagesComplete,
            InstructionConfirmedByStaff = request.Completeness.InstructionConfirmedByStaff,
            ImagesConfirmedByStaff = request.Completeness.ImagesConfirmedByStaff,
            CreatedAtUtc = acceptedAtUtc,
            Version = 0,
            RowVersion = []
        };
        context.Cases.Add(caseEntity);
        context.CaseIntakeLinks.Add(new()
        {
            IntakeReceiptId = receipt.Id,
            Case = caseEntity,
            CaseId = caseId,
            CustodyWorkId = custodyWorkId,
            LinkedAtUtc = acceptedAtUtc,
            Actor = request.Actor,
            OperationKey = request.OperationKey
        });
        context.CaseHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            Case = caseEntity,
            CaseId = caseId,
            EventType = "case_accepted",
            Actor = request.Actor,
            Reason = "Accepted intake",
            OccurredAtUtc = acceptedAtUtc,
            OperationKey = request.OperationKey,
            BeforeVersion = null,
            AfterVersion = 0
        });
        context.ExternalWorkItems.Add(new()
        {
            Id = custodyWorkId,
            Case = caseEntity,
            CaseId = caseId,
            Kind = "create_case_custody",
            OperationKey = $"case-custody:{caseId:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = acceptedAtUtc
        });
        receipt.Version++;

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(caseEntity, custodyWorkId, false);
    }

    private async Task<CaseAcceptanceOutcome?> FindAcceptedAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var link = await context.CaseIntakeLinks
            .AsNoTracking()
            .Include(item => item.Case)
            .ThenInclude(item => item.Principal)
            .SingleOrDefaultAsync(item => item.IntakeReceiptId == receiptId, cancellationToken);
        return link is null ? null : Map(link.Case, link.CustodyWorkId, true);
    }

    private static CaseAcceptanceOutcome Map(CaseEntity entity, Guid custodyWorkId, bool isDuplicate) => new(
        new(
            entity.Id,
            entity.Principal.Code,
            entity.Year,
            entity.Sequence,
            entity.Reference,
            entity.AuditReference),
        ParseInitialState(entity.InitialState),
        ParseCustodyState(entity.CustodyState),
        custodyWorkId,
        isDuplicate);

    private static string AuditPrefix(AuditAssessment assessment) => assessment switch
    {
        AuditAssessment.Repairable => "a.",
        AuditAssessment.TotalLoss => "ap.",
        _ => throw new InvalidOperationException($"Unknown AuditAssessment value '{(int)assessment}'.")
    };

    private static string ToCode(CaseType value) => value switch
    {
        CaseType.Inspection => "inspection",
        CaseType.Audit => "audit",
        CaseType.InspectionAndAudit => "inspection_and_audit",
        _ => throw new InvalidOperationException($"Unknown CaseType value '{(int)value}'.")
    };

    private static string ToCode(AuditAssessment value) => value switch
    {
        AuditAssessment.Repairable => "repairable",
        AuditAssessment.TotalLoss => "total_loss",
        _ => throw new InvalidOperationException($"Unknown AuditAssessment value '{(int)value}'.")
    };

    private static string ToCode(CaseInitialState value) => value switch
    {
        CaseInitialState.NotReady => "not_ready",
        CaseInitialState.Review => "review",
        _ => throw new InvalidOperationException($"Unknown CaseInitialState value '{(int)value}'.")
    };

    private static CaseInitialState ParseInitialState(string value) => value switch
    {
        "not_ready" => CaseInitialState.NotReady,
        "review" => CaseInitialState.Review,
        _ => throw new InvalidDataException($"Unknown persisted case initial state '{value}'.")
    };

    private static string ToCode(CaseCustodyState value) => value switch
    {
        CaseCustodyState.Pending => "pending",
        CaseCustodyState.Confirmed => "confirmed",
        CaseCustodyState.Failed => "failed",
        _ => throw new InvalidOperationException($"Unknown CaseCustodyState value '{(int)value}'.")
    };

    private static CaseCustodyState ParseCustodyState(string value) => value switch
    {
        "pending" => CaseCustodyState.Pending,
        "confirmed" => CaseCustodyState.Confirmed,
        "failed" => CaseCustodyState.Failed,
        _ => throw new InvalidDataException($"Unknown persisted case custody state '{value}'.")
    };

    private static bool IsRetryableConcurrencyFailure(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => true,
        SqliteException { SqliteErrorCode: 5 or 6 or 19 } => true,
        SqlException { Number: 1205 or 2601 or 2627 } => true,
        DbUpdateException { InnerException: { } innerException } =>
            IsRetryableConcurrencyFailure(innerException),
        _ => false
    };
}
