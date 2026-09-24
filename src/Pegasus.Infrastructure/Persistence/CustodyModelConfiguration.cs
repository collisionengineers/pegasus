using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class CustodyModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CaseDocumentEntity>(entity =>
        {
            entity.ToTable("CaseDocuments", table => table.HasCheckConstraint(
                "CK_CaseDocuments_CustodyFolder",
                "[CustodyFolder] IN (N'case', N'audit')"));
            entity.HasKey(value => value.Id);
            entity.Property(value => value.CustodyFolder)
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue(CaseCustodyFolders.Case);
            entity.Property(value => value.SourceOccurrenceIdentity).HasMaxLength(512).IsRequired();
            entity.Property(value => value.Ordinal).IsRequired();
            entity.HasIndex(value => new { value.CaseId, value.SourceOccurrenceIdentity }).IsUnique();
            entity.HasIndex(value => new { value.CaseId, value.Ordinal }).IsUnique();
            entity.HasOne<CaseEntity>().WithMany().HasForeignKey(value => value.CaseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentVersionEntity>(entity =>
        {
            entity.ToTable("DocumentVersions", table =>
            {
                table.HasCheckConstraint("CK_DocumentVersions_Version", "[Version] > 0");
                table.HasCheckConstraint("CK_DocumentVersions_ContentLength", "[ContentLength] >= 0");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.FileName).HasMaxLength(255).IsRequired();
            entity.Property(value => value.MediaType).HasMaxLength(128).IsRequired();
            entity.Property(value => value.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(value => value.BoxFileId).HasMaxLength(200);
            entity.Property(value => value.BoxVersionId).HasMaxLength(200);
            entity.Property(value => value.PendingContentStorageKey).HasMaxLength(200);
            entity.Property(value => value.CustodyStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(value => value.CreatedBy).HasMaxLength(256).IsRequired();
            entity.Property(value => value.RemovalReason).HasMaxLength(2000);
            entity.Property(value => value.RemovalOperationKey).HasMaxLength(256);
            entity.HasIndex(value => new { value.DocumentId, value.Version }).IsUnique();
            entity.HasOne<CaseDocumentEntity>().WithMany().HasForeignKey(value => value.DocumentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentOccurrenceEntity>(entity =>
        {
            entity.ToTable("DocumentOccurrences", table =>
            {
                table.HasCheckConstraint("CK_DocumentOccurrences_Rotation", "[RotationDegrees] IN (0, 90, 180, 270)");
                table.HasCheckConstraint("CK_DocumentOccurrences_Crop", "([CropLeft] IS NULL AND [CropTop] IS NULL AND [CropWidth] IS NULL AND [CropHeight] IS NULL) OR ([CropLeft] BETWEEN 0 AND 1 AND [CropTop] BETWEEN 0 AND 1 AND [CropWidth] > 0 AND [CropWidth] <= 1 AND [CropHeight] > 0 AND [CropHeight] <= 1 AND [CropLeft] + [CropWidth] <= 1 AND [CropTop] + [CropHeight] <= 1)");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.SemanticRole).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(value => value.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
            entity.Property(value => value.SourceOccurrenceIdentity).HasMaxLength(512).IsRequired();
            entity.Property(value => value.OperationKey).HasMaxLength(256).IsRequired();
            entity.Property(value => value.Ordinal).IsRequired();
            entity.Property(value => value.PreparationRole).HasMaxLength(20);
            entity.Property(value => value.CropLeft).HasPrecision(8, 7);
            entity.Property(value => value.CropTop).HasPrecision(8, 7);
            entity.Property(value => value.CropWidth).HasPrecision(8, 7);
            entity.Property(value => value.CropHeight).HasPrecision(8, 7);
            entity.Property(value => value.PreparedBy).HasMaxLength(200);
            entity.HasIndex(value => new { value.CaseId, value.OperationKey }).IsUnique();
            entity.HasIndex(value => new { value.CaseId, value.DocumentId });
            entity.HasOne<CaseEntity>().WithMany().HasForeignKey(value => value.CaseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CaseDocumentEntity>().WithMany().HasForeignKey(value => value.DocumentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<DocumentVersionEntity>().WithMany().HasForeignKey(value => value.VersionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImageTagEntity>(entity =>
        {
            entity.ToTable("ImageTags");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(40).IsRequired();
            entity.Property(value => value.NormalizedName).HasMaxLength(40).IsRequired();
            entity.Property(value => value.Colour).HasMaxLength(20).IsRequired();
            entity.Property(value => value.CreatedBy).HasMaxLength(200).IsRequired();
            entity.Property(value => value.CreateOperationKey).HasMaxLength(100).IsRequired();
            entity.Property(value => value.Version).IsConcurrencyToken();
            entity.HasIndex(value => value.NormalizedName).IsUnique();
            entity.HasIndex(value => value.CreateOperationKey).IsUnique();
        });

        modelBuilder.Entity<DocumentOccurrenceTagEntity>(entity =>
        {
            entity.ToTable("DocumentOccurrenceTags");
            entity.HasKey(value => new { value.OccurrenceId, value.TagId });
            entity.Property(value => value.AppliedByKind).HasMaxLength(32).IsRequired();
            entity.Property(value => value.AppliedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(value => value.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(value => value.TagId);
            entity.HasOne<DocumentOccurrenceEntity>().WithMany().HasForeignKey(value => value.OccurrenceId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ImageTagEntity>().WithMany().HasForeignKey(value => value.TagId).OnDelete(DeleteBehavior.Restrict);
        });

    }
}
