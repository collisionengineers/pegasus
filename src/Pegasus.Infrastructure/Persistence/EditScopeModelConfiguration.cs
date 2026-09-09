using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class EditScopeModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<EditScopeEntity>(entity =>
        {
            entity.ToTable("EditScopes", table =>
            {
                table.HasCheckConstraint("CK_EditScopes_ExpectedVersion", "[ExpectedVersion] >= 0");
                table.HasCheckConstraint("CK_EditScopes_Generation", "[Generation] > 0");
            });
            entity.HasKey(item => new { item.ScopeKind, item.RecordId });
            entity.Property(item => item.ScopeKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.HolderKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Holder).HasMaxLength(200).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(item => new { item.HolderKind, item.Holder });
            entity.HasIndex(item => item.ExpiresAtUtc);
        });
    }
}
