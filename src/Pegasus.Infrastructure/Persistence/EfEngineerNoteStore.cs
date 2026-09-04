using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfEngineerNoteStore(
    IDbContextFactory<PegasusDbContext> contextFactory) :
    IEngineerNoteStore,
    IEngineerNoteQueries
{
    public async Task AddAsync(
        AddEngineerNoteRequest request,
        DateTimeOffset recordedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }

        var operationKey = request.OperationKey.Trim();
        var note = request.Note.Trim();
        var requestHash = RequestHash(request, operationKey, note);
        var occurredAtUtc = recordedAtUtc.ToUniversalTime();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var existing = await context.EngineerNotes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CaseId == request.CaseId && item.OperationKey == operationKey,
                cancellationToken);
        if (existing is not null)
        {
            RequireExactReplay(existing, requestHash, request.CaseId, request.OperationKey);
            return;
        }

        var workflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException($"Case '{request.CaseId}' was not found.");
        ArchivedCaseGuard.RequireNotArchived(workflow);
        CaseMutationGuard.RequireVersion(workflow, request.ExpectedVersion);
        CaseMutationGuard.RequireLease(
            workflow,
            request.Actor,
            request.EditLeaseToken,
            occurredAtUtc);

        context.EngineerNotes.Add(new EngineerNoteEntity
        {
            Id = Guid.NewGuid(),
            CaseId = request.CaseId,
            Case = workflow.Case,
            OperationKey = operationKey,
            RequestHash = requestHash,
            RecordedByKind = request.Actor.Kind.ToString(),
            RecordedBySubjectId = request.Actor.SubjectId,
            RecordedByRolesJson = RolesJson(request.Actor),
            Note = note,
            RecordedAtUtc = occurredAtUtc
        });
        CaseMutationGuard.ClearLease(workflow);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsConcurrencyConflict(exception))
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                await using var verification =
                    await contextFactory.CreateDbContextAsync(cancellationToken);
                var winner = await verification.EngineerNotes
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.CaseId == request.CaseId
                            && item.OperationKey == operationKey,
                        cancellationToken);
                if (winner is not null)
                {
                    RequireExactReplay(
                        winner,
                        requestHash,
                        request.CaseId,
                        request.OperationKey);
                    return;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
            }
            throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
        }
    }

    public async Task<IReadOnlyList<EngineerNote>> ListNewestFirstAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.EngineerNotes
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);

        return rows.Select(Map).ToArray();
    }

    private static EngineerNote Map(EngineerNoteEntity entity)
    {
        if (!Guid.TryParse(entity.RecordedBySubjectId, out var staffId))
        {
            throw new InvalidDataException(
                $"Engineer note '{entity.Id}' has an invalid staff attribution.");
        }
        return new(entity.Id, entity.CaseId, staffId, entity.Note, entity.RecordedAtUtc);
    }

    private static void RequireExactReplay(
        EngineerNoteEntity existing,
        string requestHash,
        Guid caseId,
        string operationKey)
    {
        if (!FixedTimeHashEquals(existing.RequestHash, requestHash))
        {
            throw new CaseOperationConflictException(caseId, operationKey);
        }
    }

    private static string RequestHash(
        AddEngineerNoteRequest request,
        string operationKey,
        string note)
    {
        var material = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            request.CaseId,
            request.ExpectedVersion,
            actorKind = request.Actor.Kind.ToString(),
            actorSubjectId = request.Actor.SubjectId,
            actorRoles = request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray(),
            operationKey,
            note,
            request.EditLeaseToken
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
    }

    private static bool FixedTimeHashEquals(string left, string right) =>
        left.Length == 64
        && right.Length == 64
        && left.All(char.IsAsciiHexDigit)
        && right.All(char.IsAsciiHexDigit)
        && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));

    private static string RolesJson(ActionActor actor) => JsonSerializer.Serialize(
        actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray());

    private static bool IsConcurrencyConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException
                || current is SqlException { Number: 1205 or 2601 or 2627 })
            {
                return true;
            }
        }
        return false;
    }
}
