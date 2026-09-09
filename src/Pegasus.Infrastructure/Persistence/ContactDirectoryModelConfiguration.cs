using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class ContactDirectoryModelConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<OrganizationEntity>(entity =>
        {
            entity.Property(item => item.ContactPerson).HasMaxLength(200);
            entity.Property(item => item.Email).HasMaxLength(320);
            entity.Property(item => item.Telephone).HasMaxLength(50);
            entity.Property(item => item.Address).HasMaxLength(1000);
            entity.Property(item => item.Postcode).HasMaxLength(20);
            entity.Property(item => item.GuidanceTemplate).HasMaxLength(4000);
            entity.Property(item => item.GuidanceTemplateVersion).HasDefaultValue(0L);
            entity.Property(item => item.Active).HasDefaultValue(true);
        });

        builder.Entity<ContactRoleEntity>(entity =>
        {
            entity.ToTable("ContactRoles", table => table.HasCheckConstraint(
                "CK_ContactRoles_Role",
                "[Role] IN ('principal', 'claim_source', 'repairer', 'storage', 'third_party_engineer')"));
            entity.HasKey(item => new { item.OrganizationId, item.Role });
            entity.Property(item => item.Role).HasMaxLength(40).IsRequired();
            entity.HasOne(item => item.Organization)
                .WithMany(item => item.ContactRoles)
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ContactPrincipalLinkEntity>(entity =>
        {
            entity.ToTable("ContactPrincipalLinks", table => table.HasCheckConstraint(
                "CK_ContactPrincipalLinks_Role",
                "[Role] IN ('claim_source', 'repairer', 'storage', 'third_party_engineer')"));
            entity.HasKey(item => new { item.PrincipalId, item.OrganizationId, item.Role });
            entity.Property(item => item.Role).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => new { item.OrganizationId, item.Role });
            entity.HasOne(item => item.Principal)
                .WithMany(item => item.ContactLinks)
                .HasForeignKey(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Organization)
                .WithMany(item => item.PrincipalLinks)
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
