using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.PrincipalApi;

namespace Pegasus.Infrastructure.Persistence;

internal static class PrincipalSubmissionModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<PrincipalSubmissionEntity>(entity =>
        {
            entity.ToTable("PrincipalSubmissions");
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
                .HasMaxLength(PrincipalSubmissionPolicy.MaximumIdempotencyKeyLength)
                .IsRequired();
            entity.Property(item => item.BodySha256)
                .HasMaxLength(64)
                .IsFixedLength()
                .IsRequired();
            entity.Property(item => item.PrincipalReference)
                .HasMaxLength(PrincipalSubmissionPolicy.MaximumPrincipalReferenceLength);
            entity.Property(item => item.DeclaredInstructionJson).IsRequired();
            entity.HasIndex(item => new { item.PrincipalId, item.IdempotencyKey }).IsUnique();
        });
    }
}
