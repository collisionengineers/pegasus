using Microsoft.EntityFrameworkCore;

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
            entity.Property(item => item.DeclaredInstructionJson).IsRequired();
        });
    }
}
