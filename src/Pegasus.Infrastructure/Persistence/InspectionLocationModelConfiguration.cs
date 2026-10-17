using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;

namespace Pegasus.Infrastructure.Persistence;

internal static class InspectionLocationModelConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<InspectionLocationEntity>(entity =>
        {
            entity.ToTable("InspectionLocations", table =>
            {
                table.HasCheckConstraint("CK_InspectionLocations_Name", "[Name] <> ''");
                table.HasCheckConstraint("CK_InspectionLocations_Address", "[Address] <> ''");
                table.HasCheckConstraint("CK_InspectionLocations_Postcode", "[Postcode] <> ''");
                table.HasCheckConstraint("CK_InspectionLocations_Version", "[Version] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(ContactDirectoryPolicy.MaximumNameLength).IsRequired();
            entity.Property(item => item.Address).HasMaxLength(ContactDirectoryPolicy.MaximumAddressLength).IsRequired();
            entity.Property(item => item.Postcode).HasMaxLength(ContactDirectoryPolicy.MaximumPostcodeLength).IsRequired();
            entity.Property(item => item.Source).HasMaxLength(InspectionLocationPolicy.MaximumSourceLength).IsRequired();
            entity.Property(item => item.SourceRecordId).HasMaxLength(InspectionLocationPolicy.MaximumSourceLength).IsRequired();
            entity.Property(item => item.Active).HasDefaultValue(true);
            entity.Property(item => item.Version).IsConcurrencyToken();
            // A refresh finds its row again by the source row it came from.
            entity.HasIndex(item => new { item.Source, item.SourceRecordId }).IsUnique();
        });

        builder.Entity<InspectionLocationPrincipalLinkEntity>(entity =>
        {
            entity.ToTable("InspectionLocationPrincipalLinks");
            entity.HasKey(item => new { item.PrincipalId, item.InspectionLocationId });
            entity.HasIndex(item => item.InspectionLocationId);
            entity.HasOne(item => item.Principal)
                .WithMany()
                .HasForeignKey(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.InspectionLocation)
                .WithMany(item => item.PrincipalLinks)
                .HasForeignKey(item => item.InspectionLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
