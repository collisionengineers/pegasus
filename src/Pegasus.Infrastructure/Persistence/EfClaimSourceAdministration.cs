using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// EXT-19/S13: the Claim Source directory store. It reuses the same
/// idempotent-operation and audit-history tables
/// <see cref="EfOrganizationAdministration"/> already established for
/// Organization/Principal administration — one Administrator-only, reasoned,
/// expected-version, idempotent write pattern for every C directory record,
/// not a second copy of it.
/// </summary>
public sealed class EfClaimSourceAdministration(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IClaimSourceAdministration,
      IClaimSourceQueries
{
    private const string SaveClaimSourceKind = "save_claim_source";
    private const string PolicyVersion = "claim-source-administration/v1";

    private readonly IDbContextFactory<PegasusDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    private readonly TimeProvider _timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<ClaimSourceRecord> SaveAsync(
        SaveClaimSourceRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = ClaimSourceAdministrationPolicy.Normalize(request);
        return EfOrganizationAdministration.ExecuteWithConcurrencyRetryAsync(
            token => SaveOnceAsync(normalized, token),
            IsRetryableConcurrencyFailure,
            cancellationToken);
    }

    private async Task<ClaimSourceRecord> SaveOnceAsync(
        SaveClaimSourceRequest request,
        CancellationToken cancellationToken)
    {
        var requestHash = EfOrganizationAdministration.HashRequest(new
        {
            command = SaveClaimSourceKind,
            actorKind = request.Actor.Kind.ToString(),
            actorSubjectId = request.Actor.SubjectId,
            request.Id,
            request.ExpectedVersion,
            request.Name,
            request.ContactName,
            request.Telephone,
            request.Email,
            request.Notes,
            request.Active,
            request.Reason
        });

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var receipt = await EfOrganizationAdministration.FindReceiptAsync(
            context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            var replay = ReadReplay(receipt, requestHash);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        var entity = await context.Set<ClaimSourceEntity>()
            .SingleOrDefaultAsync(item => item.Id == request.Id, cancellationToken);
        ClaimSourceRecord result;
        var now = _timeProvider.GetUtcNow();
        if (entity is null)
        {
            // A create: the caller mints the stable id and starts at
            // expected version 0, the same optimistic-concurrency shape
            // every edit uses afterward.
            ClaimSourceAdministrationPolicy.RequireCurrentVersion(0, request.ExpectedVersion);
            entity = new ClaimSourceEntity
            {
                Id = request.Id,
                Name = request.Name,
                Contact = request.ContactName,
                Telephone = request.Telephone,
                Email = request.Email,
                Notes = request.Notes,
                Active = request.Active,
                UpdatedBy = request.Actor.SubjectId,
                UpdatedAtUtc = now,
                Version = 0
            };
            context.Set<ClaimSourceEntity>().Add(entity);
            result = ToRecord(entity);
            AddReceiptAndHistory(context, request, requestHash, result, before: null, now);
        }
        else
        {
            ClaimSourceAdministrationPolicy.RequireCurrentVersion(entity.Version, request.ExpectedVersion);
            var before = ToRecord(entity);
            var changed = entity.Name != request.Name
                || entity.Contact != request.ContactName
                || entity.Telephone != request.Telephone
                || entity.Email != request.Email
                || entity.Notes != request.Notes
                || entity.Active != request.Active;
            entity.Name = request.Name;
            entity.Contact = request.ContactName;
            entity.Telephone = request.Telephone;
            entity.Email = request.Email;
            entity.Notes = request.Notes;
            entity.Active = request.Active;
            entity.UpdatedBy = request.Actor.SubjectId;
            entity.UpdatedAtUtc = now;
            entity.Version = changed ? checked(entity.Version + 1) : entity.Version;
            result = ToRecord(entity);
            AddReceiptAndHistory(context, request, requestHash, result, before, now);
        }

        await SaveChangesAsync(context, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static void AddReceiptAndHistory(
        PegasusDbContext context,
        SaveClaimSourceRequest request,
        string requestHash,
        ClaimSourceRecord result,
        ClaimSourceRecord? before,
        DateTimeOffset now)
    {
        EfOrganizationAdministration.AddReceipt(
            context,
            request.OperationKey,
            SaveClaimSourceKind,
            requestHash,
            result,
            now);
        EfOrganizationAdministration.AddHistory(
            context,
            "claim_source",
            result.Id,
            before is null ? "claim_source_created" : "claim_source_saved",
            request.Actor,
            request.OperationKey,
            now,
            request.Reason,
            before,
            result);
    }

    public async Task<ClaimSourceRecord?> GetAsync(
        ActionActor actor,
        Guid id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A stable identifier is required.", nameof(id));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<ClaimSourceEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<ClaimSourceRecord>> SearchAsync(
        ActionActor actor,
        string prefix,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        ArgumentNullException.ThrowIfNull(prefix);
        if (limit < 1 || limit > ClaimSourceAdministrationPolicy.MaximumSearchLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"The search limit must be between 1 and {ClaimSourceAdministrationPolicy.MaximumSearchLimit}.");
        }

        var normalizedPrefix = prefix.Trim();
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Set<ClaimSourceEntity>().AsNoTracking();
        if (normalizedPrefix.Length > 0)
        {
            query = query.Where(item => item.Name.StartsWith(normalizedPrefix));
        }

        var rows = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        return rows.Select(ToRecord).ToArray();
    }

    private static ClaimSourceRecord ToRecord(ClaimSourceEntity entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Contact,
            entity.Telephone,
            entity.Email,
            entity.Notes,
            entity.Active,
            entity.Version,
            entity.UpdatedAtUtc);

    private static ClaimSourceRecord ReadReplay(
        OrganizationAdministrationOperationEntity receipt,
        string requestHash)
    {
        if (!string.Equals(receipt.CommandKind, SaveClaimSourceKind, StringComparison.Ordinal)
            || !EfOrganizationAdministration.SameHash(receipt.RequestHash, requestHash))
        {
            throw new ClaimSourceAdministrationException(ClaimSourceAdministrationError.OperationConflict);
        }

        try
        {
            return JsonSerializer.Deserialize<ClaimSourceRecord>(
                    receipt.ResultJson, EfOrganizationAdministration.SerializerOptions)
                ?? throw new ClaimSourceAdministrationException(
                    ClaimSourceAdministrationError.OperationConflict);
        }
        catch (JsonException)
        {
            throw new ClaimSourceAdministrationException(ClaimSourceAdministrationError.OperationConflict);
        }
    }

    private static bool IsRetryableConcurrencyFailure(Exception exception) =>
        exception switch
        {
            ClaimSourceAdministrationException { Error: ClaimSourceAdministrationError.StaleVersion } => true,
            SqlException { Number: 1205 or 2601 or 2627 } => true,
            DbUpdateException { InnerException: { } innerException } =>
                IsRetryableConcurrencyFailure(innerException),
            _ when exception.InnerException is not null =>
                IsRetryableConcurrencyFailure(exception.InnerException),
            _ => false
        };

    private static async Task SaveChangesAsync(
        PegasusDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ClaimSourceAdministrationException(ClaimSourceAdministrationError.StaleVersion);
        }
    }
}
