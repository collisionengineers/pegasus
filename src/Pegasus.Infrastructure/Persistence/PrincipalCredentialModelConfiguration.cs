using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

internal static class PrincipalCredentialModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<PrincipalApiCredentialEntity>(entity =>
        {
            var states = string.Join(
                ", ",
                Enum.GetNames<PrincipalCredentialState>().Select(name => $"'{name}'"));
            entity.ToTable("PrincipalApiCredentials", table =>
            {
                table.HasCheckConstraint("CK_PrincipalApiCredentials_State", $"[State] IN ({states})");
                table.HasCheckConstraint("CK_PrincipalApiCredentials_Version", "[Version] >= 1");
            });
            entity.HasKey(item => item.PrincipalId);
            entity.Property(item => item.PrincipalId).ValueGeneratedNever();
            entity.HasOne(item => item.Principal)
                .WithOne()
                .HasForeignKey<PrincipalApiCredentialEntity>(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(item => item.KeyId)
                .HasMaxLength(PrincipalCredentialPolicy.KeyIdLength)
                .IsFixedLength()
                .IsRequired();
            entity.Property(item => item.SecretHash).HasMaxLength(200).IsRequired();
            entity.Property(item => item.State).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.KeyId).IsUnique();
        });
    }
}
