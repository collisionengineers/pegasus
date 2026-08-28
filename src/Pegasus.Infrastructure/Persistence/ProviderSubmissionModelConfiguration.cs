using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.ProviderApi;

namespace Pegasus.Infrastructure.Persistence;

internal static class ProviderSubmissionModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ProviderSubmissionEntity>(entity =>
        {
            entity.ToTable("ProviderSubmissions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).ValueGeneratedNever();
            entity.HasOne(item => item.Principal)
                .WithMany()
                .HasForeignKey(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.KeyId)
                .HasMaxLength(PrincipalCredentialPolicy.KeyIdLength)
                .IsFixedLength()
                .IsRequired();
            entity.Property(item => item.IdempotencyKey)
                .HasMaxLength(ProviderSubmissionPolicy.MaximumIdempotencyKeyLength)
                .IsRequired();
            entity.Property(item => item.ProviderReference)
                .HasMaxLength(ProviderSubmissionPolicy.MaximumProviderReferenceLength);
            entity.HasIndex(item => new { item.PrincipalId, item.IdempotencyKey }).IsUnique();
        });
    }
}
