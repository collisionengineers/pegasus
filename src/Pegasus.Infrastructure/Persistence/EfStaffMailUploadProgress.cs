using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Pegasus.Infrastructure.Email;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfStaffMailUploadProgress(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IDataProtectionProvider dataProtection) : IStaffMailUploadProgress
{
    private const string Purpose = "Pegasus.StaffMail.UploadProgress.v1";

    public async Task<StaffMailUploadSession?> GetAsync(
        Guid operationId, Guid attachmentVersionId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var protectedValue = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(value => value.Id == operationId)
            .Select(value => value.ProtectedUploadSession)
            .SingleOrDefaultAsync(cancellationToken);
        var payload = Read(protectedValue);
        var item = payload.Items.SingleOrDefault(value => value.AttachmentVersionId == attachmentVersionId);
        return item is null ? null : new(
            item.UploadUrl is null ? null : new Uri(item.UploadUrl),
            item.ExpiresAtUtc,
            item.NextOffset,
            item.Completed);
    }

    public Task SaveAsync(
        Guid operationId, Guid attachmentVersionId, StaffMailUploadSession session,
        CancellationToken cancellationToken) =>
        MutateAsync(operationId, payload =>
        {
            payload.Items.RemoveAll(value => value.AttachmentVersionId == attachmentVersionId);
            payload.Items.Add(new(
                attachmentVersionId, session.UploadUrl?.AbsoluteUri,
                session.ExpiresAtUtc, session.NextOffset, session.Completed));
        }, session.ExpiresAtUtc, cancellationToken);

    public Task CompleteAsync(
        Guid operationId, Guid attachmentVersionId, CancellationToken cancellationToken) =>
        MutateAsync(operationId, payload =>
        {
            payload.Items.RemoveAll(value => value.AttachmentVersionId == attachmentVersionId);
            payload.Items.Add(new(attachmentVersionId, null, DateTimeOffset.UnixEpoch, 0, true));
        },
            null,
            cancellationToken);

    private async Task MutateAsync(
        Guid operationId, Action<Payload> mutate, DateTimeOffset? expiry,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<StaffMailSendOperationEntity>().SingleAsync(
            value => value.Id == operationId, cancellationToken);
        var payload = Read(entity.ProtectedUploadSession);
        mutate(payload);
        entity.ProtectedUploadSession = dataProtection.CreateProtector(Purpose)
            .Protect(JsonSerializer.Serialize(payload));
        entity.UploadSessionExpiresAtUtc = payload.Items
            .Where(value => !value.Completed)
            .Select(value => (DateTimeOffset?)value.ExpiresAtUtc)
            .Max();
        if (expiry is not null && entity.UploadSessionExpiresAtUtc < expiry)
        {
            entity.UploadSessionExpiresAtUtc = expiry;
        }
        entity.ConcurrencyToken = Guid.NewGuid();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException("Staff mail upload progress changed concurrently.", exception);
        }
    }

    private Payload Read(string? protectedValue)
    {
        if (protectedValue is null)
        {
            return new([]);
        }
        var json = dataProtection.CreateProtector(Purpose).Unprotect(protectedValue);
        return JsonSerializer.Deserialize<Payload>(json)
            ?? throw new InvalidDataException("The protected staff mail upload progress is invalid.");
    }

    private sealed record Payload(List<Item> Items);
    private sealed record Item(
        Guid AttachmentVersionId, string? UploadUrl,
        DateTimeOffset ExpiresAtUtc, long NextOffset, bool Completed);
}
