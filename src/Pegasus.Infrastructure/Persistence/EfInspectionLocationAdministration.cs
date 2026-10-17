using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The one writer of the reviewed inspection locations: an upsert by source
/// row, in one transaction, with the directory's receipt and history. FRD-06:
/// the pipeline "keeps operator-confirmed rows across refreshes, and is
/// deterministic and auditable". A save whose review is not later than the
/// stored row's writes nothing; a later one replaces the row's fields and its
/// whole link set. A row a refresh leaves out is never touched.
/// </summary>
public sealed class EfInspectionLocationAdministration(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider)
    : IInspectionLocationAdministration
{
    private const string SaveKind = "save_inspection_location";

    public Task<InspectionLocationSaveResult> SaveAsync(
        SaveInspectionLocationRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = InspectionLocationPolicy.Normalize(request);
        return StoreConcurrencyRetry.ExecuteAsync(token => SaveOnceAsync(normalized, token), cancellationToken);
    }

    private async Task<InspectionLocationSaveResult> SaveOnceAsync(
        SaveInspectionLocationRequest request,
        CancellationToken cancellationToken)
    {
        var requestHash = EfOrganizationAdministration.HashRequest(new
        {
            command = SaveKind, request.Name, request.Address, request.Postcode, request.Active, request.Source,
            request.SourceRecordId, request.ReviewedAtUtc, request.PrincipalIds
        });
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var receipt = await EfOrganizationAdministration.FindReceiptAsync(context, request.OperationKey, cancellationToken);
        if (receipt is not null)
        {
            if (receipt.CommandKind != SaveKind || !EfOrganizationAdministration.SameHash(receipt.RequestHash, requestHash))
                throw new ContactDirectoryException(ContactDirectoryError.OperationConflict);
            var replay = JsonSerializer.Deserialize<InspectionLocationSaveResult>(
                    receipt.ResultJson, EfOrganizationAdministration.SerializerOptions)
                ?? throw new ContactDirectoryException(ContactDirectoryError.OperationConflict);
            await transaction.CommitAsync(cancellationToken);
            return replay;
        }

        // Any existing Principal, active or not: the register loads evidenced
        // codes inactive (operator, 15 and 16 October 2026).
        var principalIds = request.PrincipalIds.ToArray();
        if (await context.Principals.CountAsync(item => principalIds.Contains(item.Id), cancellationToken) != principalIds.Length)
            throw new ContactDirectoryException(ContactDirectoryError.InvalidPrincipalAssociation);

        var entity = await context.InspectionLocations.Include(item => item.PrincipalLinks)
            .SingleOrDefaultAsync(
                item => item.Source == request.Source && item.SourceRecordId == request.SourceRecordId,
                cancellationToken);
        if (entity is not null && request.ReviewedAtUtc <= entity.ReviewedAtUtc)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(entity.Id, entity.Version, InspectionLocationSaveOutcome.KeptExisting);
        }

        var creating = entity is null;
        InspectionLocationHistory? before = null;
        if (entity is null)
        {
            entity = new InspectionLocationEntity
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Address = request.Address,
                Postcode = request.Postcode,
                Source = request.Source,
                SourceRecordId = request.SourceRecordId,
                Version = 0
            };
            context.InspectionLocations.Add(entity);
        }
        else
        {
            before = ToHistory(entity, entity.PrincipalLinks.Select(link => link.PrincipalId));
            context.RemoveRange(entity.PrincipalLinks);
            entity.Version = checked(entity.Version + 1);
        }
        entity.Name = request.Name;
        entity.Address = request.Address;
        entity.Postcode = request.Postcode;
        entity.Active = request.Active;
        entity.ReviewedAtUtc = request.ReviewedAtUtc;
        foreach (var principalId in principalIds)
        {
            context.InspectionLocationPrincipalLinks.Add(
                new() { PrincipalId = principalId, InspectionLocationId = entity.Id });
        }

        var result = new InspectionLocationSaveResult(
            entity.Id,
            entity.Version,
            creating ? InspectionLocationSaveOutcome.Created : InspectionLocationSaveOutcome.Updated);
        var now = timeProvider.GetUtcNow();
        EfOrganizationAdministration.AddReceipt(context, request.OperationKey, SaveKind, requestHash, result, now);
        EfOrganizationAdministration.AddHistory(
            context,
            "inspection_location",
            entity.Id,
            creating ? "inspection_location_created" : "inspection_location_saved",
            request.Actor,
            request.OperationKey,
            now,
            null,
            before,
            ToHistory(entity, principalIds));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static InspectionLocationHistory ToHistory(InspectionLocationEntity entity, IEnumerable<Guid> principalIds) =>
        new(
            entity.Id,
            entity.Name,
            entity.Address,
            entity.Postcode,
            entity.Active,
            entity.Source,
            entity.SourceRecordId,
            entity.ReviewedAtUtc,
            entity.Version,
            principalIds.Order().ToArray());

    private sealed record InspectionLocationHistory(
        Guid Id,
        string Name,
        string Address,
        string Postcode,
        bool Active,
        string Source,
        string SourceRecordId,
        DateTimeOffset ReviewedAtUtc,
        long Version,
        IReadOnlyList<Guid> PrincipalIds);
}
