using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One paid ask of a guide provider for a Case (R900): its key is unique, so a
/// source is asked once per Case, guide month, registration and mileage. A row
/// is <c>Asking</c> while its claim runs and <c>Answered</c> once figures came
/// back; an ask that answered no figures leaves no row.
/// </summary>
internal sealed class GuideValuationFetchEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public required string Source { get; set; }
    public DateOnly GuideMonth { get; set; }
    public required string Registration { get; set; }
    public long Mileage { get; set; }
    public required string MileageUnit { get; set; }
    public required string State { get; set; }
    public required string ClaimToken { get; set; }
    public DateTimeOffset ClaimExpiresAtUtc { get; set; }
    public DateTimeOffset? AnsweredAtUtc { get; set; }
    public required string ActorSubjectId { get; set; }
}

internal static class GuideValuationFetchModelConfiguration
{
    public const string Asking = "Asking";
    public const string Answered = "Answered";

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<GuideValuationFetchEntity>(entity =>
        {
            var sources = string.Join(
                ", ",
                Enum.GetNames<ValuationSource>().Select(name => $"'{name}'"));
            entity.ToTable("GuideValuationFetches", table =>
            {
                table.HasCheckConstraint(
                    "CK_GuideValuationFetches_Source", $"[Source] IN ({sources})");
                table.HasCheckConstraint(
                    "CK_GuideValuationFetches_State", $"[State] IN ('{Asking}', '{Answered}')");
                table.HasCheckConstraint(
                    "CK_GuideValuationFetches_Mileage", "[Mileage] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.Property(item => item.Source).HasMaxLength(30).IsRequired();
            entity.Property(item => item.GuideMonth).HasColumnType("date");
            entity.Property(item => item.Registration).HasMaxLength(32).IsRequired();
            entity.Property(item => item.MileageUnit).HasMaxLength(20).IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ClaimToken).HasMaxLength(32).IsFixedLength().IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new
            {
                item.CaseId,
                item.Source,
                item.GuideMonth,
                item.Registration,
                item.Mileage,
                item.MileageUnit
            }).IsUnique();
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class EfGuideValuationFetchLedger(
    IDbContextFactory<PegasusDbContext> contextFactory) : IGuideValuationFetchLedger
{
    public async Task<string?> TryClaimAsync(
        GuideValuationFetchKey key,
        ActionActor actor,
        DateTimeOffset nowUtc,
        DateTimeOffset claimExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(actor);
        var token = Guid.NewGuid().ToString("N");
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            context.Set<GuideValuationFetchEntity>().Add(new GuideValuationFetchEntity
            {
                Id = Guid.NewGuid(),
                CaseId = key.CaseId,
                Source = key.Source.ToString(),
                GuideMonth = key.GuideMonth,
                Registration = key.Registration,
                Mileage = key.Mileage,
                MileageUnit = key.MileageUnit,
                State = GuideValuationFetchModelConfiguration.Asking,
                ClaimToken = token,
                ClaimExpiresAtUtc = claimExpiresAtUtc,
                ActorSubjectId = actor.SubjectId
            });
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                return token;
            }
            catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
            {
                // Asked before: only a lapsed claim may be taken over below.
            }
        }

        await using var retry = await contextFactory.CreateDbContextAsync(cancellationToken);
        var taken = await Matching(retry, key)
            .Where(item => item.State == GuideValuationFetchModelConfiguration.Asking
                && item.ClaimExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.ClaimToken, token)
                    .SetProperty(item => item.ClaimExpiresAtUtc, claimExpiresAtUtc)
                    .SetProperty(item => item.ActorSubjectId, actor.SubjectId),
                cancellationToken);
        return taken == 1 ? token : null;
    }

    public async Task MarkAnsweredAsync(
        GuideValuationFetchKey key,
        string claimToken,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await Matching(context, key)
            .Where(item => item.ClaimToken == claimToken)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(item => item.State, GuideValuationFetchModelConfiguration.Answered)
                    .SetProperty(item => item.AnsweredAtUtc, nowUtc),
                cancellationToken);
    }

    public async Task ReleaseAsync(
        GuideValuationFetchKey key,
        string claimToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await Matching(context, key)
            .Where(item => item.ClaimToken == claimToken
                && item.State == GuideValuationFetchModelConfiguration.Asking)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static IQueryable<GuideValuationFetchEntity> Matching(
        PegasusDbContext context,
        GuideValuationFetchKey key)
    {
        var source = key.Source.ToString();
        return context.Set<GuideValuationFetchEntity>().Where(item =>
            item.CaseId == key.CaseId
            && item.Source == source
            && item.GuideMonth == key.GuideMonth
            && item.Registration == key.Registration
            && item.Mileage == key.Mileage
            && item.MileageUnit == key.MileageUnit);
    }
}
