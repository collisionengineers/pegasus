using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The photo capture tables (FRD-28). The intake rows a capture's items
/// become are referred to by id only, with no foreign key, so the intake
/// tables keep their own lifecycle; the Case, the contact and the standing
/// link are real references.
/// </summary>
internal static class PhotoCaptureModelConfiguration
{
    /// <summary>The grouped batches whose record has not yet reached a Case, which the settle's reach reads every run.</summary>
    internal const string UnreachedGroupIndex = "IX_PhotoCaptureBatches_SubmissionGroupId_Unreached";

    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<StandingPhotoLinkEntity>(entity =>
        {
            entity.ToTable("StandingPhotoLinks", table =>
            {
                table.HasCheckConstraint(
                    "CK_StandingPhotoLinks_Kind",
                    $"[Kind] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.StandingLinkKinds)})");
                table.HasCheckConstraint(
                    "CK_StandingPhotoLinks_ForwardedMode",
                    $"[ForwardedMode] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.Modes)})");
                // A contact's link names its contact; the website link names none.
                table.HasCheckConstraint(
                    "CK_StandingPhotoLinks_Organization",
                    "([Kind] = 'contact' AND [OrganizationId] IS NOT NULL) OR ([Kind] = 'website' AND [OrganizationId] IS NULL)");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(60).IsRequired();
            entity.Property(item => item.ForwardedMode).HasMaxLength(10).IsRequired();
            entity.Property(item => item.CreatedByKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CreatedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.Slug).IsUnique();
            // One live link per contact, and one live website link.
            entity.HasIndex(item => item.OrganizationId)
                .IsUnique()
                .HasFilter("[OrganizationId] IS NOT NULL AND [RetiredAtUtc] IS NULL");
            entity.HasIndex(item => item.Kind)
                .IsUnique()
                .HasFilter("[Kind] = 'website' AND [RetiredAtUtc] IS NULL");
            entity.HasOne<OrganizationEntity>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PhotoCaptureEntity>(entity =>
        {
            entity.ToTable("PhotoCaptures", table =>
            {
                table.HasCheckConstraint(
                    "CK_PhotoCaptures_Origin",
                    $"[Origin] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.Origins)})");
                table.HasCheckConstraint(
                    "CK_PhotoCaptures_Destination",
                    $"[Destination] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.Destinations)})");
                table.HasCheckConstraint(
                    "CK_PhotoCaptures_Mode",
                    $"[Mode] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.Modes)})");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Origin).HasMaxLength(30).IsRequired();
            entity.Property(item => item.CaseReference).HasMaxLength(40);
            entity.Property(item => item.Destination).HasMaxLength(20).IsRequired();
            entity.Property(item => item.RoutingReason).HasMaxLength(100);
            entity.Property(item => item.NormalizedRegistration).HasMaxLength(20);
            entity.Property(item => item.CustomerName).HasMaxLength(CaptureLimits.NameLength);
            entity.Property(item => item.Mobile).HasMaxLength(CaptureLimits.MobileLength);
            entity.Property(item => item.Email).HasMaxLength(CaptureLimits.EmailLength);
            entity.Property(item => item.DetailsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(item => item.Note).HasMaxLength(CaptureLimits.NoteLength);
            entity.Property(item => item.Mode).HasMaxLength(10).IsRequired();
            entity.Property(item => item.QuickLinkFor).HasMaxLength(30);
            entity.Property(item => item.Version).IsConcurrencyToken();
            // The two HQ lists read one origin group newest first.
            entity.HasIndex(item => new { item.Origin, item.CreatedAtUtc }).IsDescending(false, true);
            entity.HasIndex(item => item.CaseId);
            // The standing link's daily mint cap counts its captures since a time.
            entity.HasIndex(item => new { item.StandingLinkId, item.CreatedAtUtc });
            entity.HasIndex(item => item.SubmissionGroupId);
            // The settle listing's former index: its OR of a send, an expiry
            // and a cancellation cannot seek on it, so it reads the next one.
            entity.HasIndex(item => new { item.SettledAtUtc, item.SentAtUtc, item.ExpiresAtUtc });
            // The settle's listing pages by (created, id) from its cursor and
            // tests a send, an expiry or a cancellation no settle has recorded
            // yet on the columns included here, so it reads the index in order
            // and stops at its page, seeking each capture's open batch in
            // IX_PhotoCaptureItems_CaptureId_Batch_Ordinal.
            entity.HasIndex(item => new { item.CreatedAtUtc, item.Id })
                .IncludeProperties(item => new
                {
                    item.SettledBatches,
                    item.SentAtUtc,
                    item.SettledAtUtc,
                    item.ExpiresAtUtc,
                    item.RevokedAtUtc
                });
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StandingPhotoLinkEntity>()
                .WithMany()
                .HasForeignKey(item => item.StandingLinkId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<OrganizationEntity>()
                .WithMany()
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PhotoCaptureTokenEntity>(entity =>
        {
            entity.ToTable("PhotoCaptureTokens", table => table.HasCheckConstraint(
                "CK_PhotoCaptureTokens_Purpose",
                $"[Purpose] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.TokenPurposes)})"));
            entity.HasKey(item => item.TokenDigest);
            entity.Property(item => item.TokenDigest).HasMaxLength(64).IsFixedLength().IsUnicode(false);
            entity.Property(item => item.Purpose).HasMaxLength(20).IsRequired();
            entity.HasIndex(item => item.CaptureId);
            entity.HasOne<PhotoCaptureEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaptureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PhotoCaptureItemEntity>(entity =>
        {
            entity.ToTable("PhotoCaptureItems", table =>
            {
                table.HasCheckConstraint(
                    "CK_PhotoCaptureItems_Kind",
                    $"[Kind] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.ItemKinds)})");
                table.HasCheckConstraint(
                    "CK_PhotoCaptureItems_Stage",
                    $"[Stage] IS NULL OR [Stage] IN ({PhotoCaptureCodes.SqlList(PhotoCaptureCodes.Stages)})");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Area).HasMaxLength(CaptureLimits.AreaLength);
            entity.Property(item => item.Stage).HasMaxLength(20);
            entity.Property(item => item.FileName).HasMaxLength(120).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ContentHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
            entity.HasIndex(item => new { item.CaptureId, item.ClientItemId }).IsUnique();
            entity.HasIndex(item => new { item.CaptureId, item.Batch, item.Ordinal }).IsUnique();
            entity.HasIndex(item => item.IntakeAssetId).HasFilter("[IntakeAssetId] IS NOT NULL");
            entity.HasIndex(item => item.StagedReceiptId);
            entity.HasOne<PhotoCaptureEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaptureId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PhotoCaptureBatchEntity>(entity =>
        {
            entity.ToTable("PhotoCaptureBatches");
            entity.HasKey(item => new { item.CaptureId, item.Batch });
            // A group is one batch's; the record's merge finds its batch by it.
            // The reach reads only the grouped batches not yet reached, and a
            // bell still owed is read on every run, so only those rows are
            // indexed for them.
            entity.HasIndex(item => item.SubmissionGroupId)
                .IsUnique()
                .HasFilter("[SubmissionGroupId] IS NOT NULL");
            entity.HasIndex(item => item.SubmissionGroupId, UnreachedGroupIndex)
                .HasFilter("[SubmissionGroupId] IS NOT NULL AND [ReachedAtUtc] IS NULL")
                .IncludeProperties(item => new { item.ClosedAtUtc });
            entity.HasIndex(item => item.BellOwedAtUtc).HasFilter("[BellOwedAtUtc] IS NOT NULL");
            entity.HasOne<PhotoCaptureEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaptureId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StandingPhotoLinkAddressEntity>(entity =>
        {
            entity.ToTable("StandingPhotoLinkAddresses");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(ContactDirectoryPolicy.MaximumNameLength).IsRequired();
            entity.Property(item => item.Address).HasMaxLength(ContactDirectoryPolicy.MaximumAddressLength).IsRequired();
            entity.HasIndex(item => item.StandingPhotoLinkId);
            entity.HasOne<StandingPhotoLinkEntity>()
                .WithMany()
                .HasForeignKey(item => item.StandingPhotoLinkId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
