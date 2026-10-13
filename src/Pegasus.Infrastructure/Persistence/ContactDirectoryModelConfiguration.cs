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
            entity.Property(item => item.NotesOnEveryCase).HasMaxLength(2000);
            entity.Property(item => item.Active).HasDefaultValue(true);
            // One type per contact, fixed when it is added.
            entity.Property(item => item.Role).HasMaxLength(40).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Organizations_Role",
                "[Role] IN ('principal', 'claim_source', 'repairer', 'storage', 'third_party_engineer')"));
        });

        builder.Entity<ContactPrincipalLinkEntity>(entity =>
        {
            entity.ToTable("ContactPrincipalLinks");
            entity.HasKey(item => new { item.PrincipalId, item.OrganizationId });
            entity.HasIndex(item => item.OrganizationId);
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
