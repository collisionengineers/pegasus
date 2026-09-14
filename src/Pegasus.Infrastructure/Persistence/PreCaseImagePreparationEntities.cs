using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The crop and rotation recorded on one pre-Case image (a retained intake
/// image asset), in the same columns and under the same checks as
/// <see cref="DocumentOccurrenceEntity"/>'s preparation. No row means the image
/// is unprepared: no rotation, the whole frame.
/// </summary>
internal sealed class IntakeAssetPreparationEntity
{
    public Guid IntakeAssetId { get; set; }
    public short RotationDegrees { get; set; }
    public decimal? CropLeft { get; set; }
    public decimal? CropTop { get; set; }
    public decimal? CropWidth { get; set; }
    public decimal? CropHeight { get; set; }
    public long Version { get; set; }
    public DateTimeOffset PreparedAtUtc { get; set; }
    public string PreparedByKind { get; set; } = string.Empty;
    public string PreparedBySubjectId { get; set; } = string.Empty;
    public string OperationKey { get; set; } = string.Empty;
}

/// <summary>One tag on one pre-Case image, as <see cref="DocumentOccurrenceTagEntity"/> is on a Case image.</summary>
internal sealed class IntakeAssetTagEntity
{
    public Guid IntakeAssetId { get; set; }
    public Guid TagId { get; set; }
    public string AppliedByKind { get; set; } = string.Empty;
    public string AppliedBySubjectId { get; set; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; set; }
    public string OperationKey { get; set; } = string.Empty;
}

internal static class PreCaseImagePreparationModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntakeAssetPreparationEntity>(entity =>
        {
            entity.ToTable("IntakeAssetPreparations", table =>
            {
                table.HasCheckConstraint("CK_IntakeAssetPreparations_Rotation", "[RotationDegrees] IN (0, 90, 180, 270)");
                table.HasCheckConstraint("CK_IntakeAssetPreparations_Crop", "([CropLeft] IS NULL AND [CropTop] IS NULL AND [CropWidth] IS NULL AND [CropHeight] IS NULL) OR ([CropLeft] BETWEEN 0 AND 1 AND [CropTop] BETWEEN 0 AND 1 AND [CropWidth] > 0 AND [CropWidth] <= 1 AND [CropHeight] > 0 AND [CropHeight] <= 1 AND [CropLeft] + [CropWidth] <= 1 AND [CropTop] + [CropHeight] <= 1)");
            });
            entity.HasKey(value => value.IntakeAssetId);
            entity.Property(value => value.CropLeft).HasPrecision(8, 7);
            entity.Property(value => value.CropTop).HasPrecision(8, 7);
            entity.Property(value => value.CropWidth).HasPrecision(8, 7);
            entity.Property(value => value.CropHeight).HasPrecision(8, 7);
            entity.Property(value => value.PreparedByKind).HasMaxLength(32).IsRequired();
            entity.Property(value => value.PreparedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(value => value.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasOne<IntakeAssetEntity>().WithMany().HasForeignKey(value => value.IntakeAssetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IntakeAssetTagEntity>(entity =>
        {
            entity.ToTable("IntakeAssetTags");
            entity.HasKey(value => new { value.IntakeAssetId, value.TagId });
            entity.Property(value => value.AppliedByKind).HasMaxLength(32).IsRequired();
            entity.Property(value => value.AppliedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(value => value.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(value => value.TagId);
            entity.HasOne<IntakeAssetEntity>().WithMany().HasForeignKey(value => value.IntakeAssetId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ImageTagEntity>().WithMany().HasForeignKey(value => value.TagId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
