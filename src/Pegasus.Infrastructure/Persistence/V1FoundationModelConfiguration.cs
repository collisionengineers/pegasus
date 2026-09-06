using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class V1FoundationModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<UserExternalCredentialEntity>(e =>
        {
            e.ToTable("UserExternalCredentials"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Provider, x.UserId }).IsUnique();
            e.HasIndex(x => new { x.Provider, x.NormalizedAccountKey });
            e.Property(x => x.Provider).HasMaxLength(50); e.Property(x => x.NormalizedAccountKey).HasMaxLength(320);
            e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<StaffMailSendOperationEntity>(e =>
        {
            var states = string.Join(", ", Enum.GetNames<Pegasus.Core.Operations.StaffMailState>().Select(x => $"'{x}'"));
            var stages = string.Join(", ", Enum.GetNames<Pegasus.Core.Operations.StaffMailAttemptStage>().Select(x => $"'{x}'"));
            e.ToTable("StaffMailSendOperations", t => { t.HasCheckConstraint("CK_StaffMailSendOperations_State", $"[State] IN ({states})"); t.HasCheckConstraint("CK_StaffMailSendOperations_AttemptStage", $"[AttemptStage] IS NULL OR [AttemptStage] IN ({stages})"); }); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ActorSubjectId, x.MailboxId, x.OperationKey }).IsUnique();
            e.Property(x => x.ActorSubjectId).HasMaxLength(200); e.Property(x => x.OperationKey).HasMaxLength(100);
            e.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.CorrelationMarker).HasMaxLength(100);
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(40); e.Property(x => x.ComposeMode).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.State).HasConversion<string>().HasMaxLength(40); e.Property(x => x.AttemptStage).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<TriageSequenceEntity>(e =>
        {
            e.ToTable("TriageSequences", t => t.HasCheckConstraint("CK_TriageSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0"));
            e.HasKey(x => x.Id); e.HasData(new TriageSequenceEntity { Id = 1, LastAllocatedSequence = 0 });
        });
        builder.Entity<ValuationPresetEntity>(e =>
        {
            e.ToTable("ValuationPresets"); e.HasKey(x => x.Id); e.HasIndex(x => x.Label).IsUnique();
            e.Property(x => x.Label).HasMaxLength(200); e.Property(x => x.UpdatedBy).HasMaxLength(200);
            e.Property(x => x.SuggestedAmount).HasPrecision(18, 2);
            e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            e.HasData(
                new ValuationPresetEntity { Id=Guid.Parse("00000000-0000-4000-8000-00000000f001"), Label="Tow bar", SuggestedAmount=300m, Active=true, Version=1, UpdatedBy="system:v1-foundation", UpdatedAtUtc=DateTimeOffset.UnixEpoch, ConcurrencyToken=Guid.Parse("00000000-0000-4000-8000-00000000f101") },
                new ValuationPresetEntity { Id=Guid.Parse("00000000-0000-4000-8000-00000000f002"), Label="PCO plated", SuggestedAmount=1500m, Active=true, Version=1, UpdatedBy="system:v1-foundation", UpdatedAtUtc=DateTimeOffset.UnixEpoch, ConcurrencyToken=Guid.Parse("00000000-0000-4000-8000-00000000f102") },
                new ValuationPresetEntity { Id=Guid.Parse("00000000-0000-4000-8000-00000000f003"), Label="Decals", SuggestedAmount=500m, Active=true, Version=1, UpdatedBy="system:v1-foundation", UpdatedAtUtc=DateTimeOffset.UnixEpoch, ConcurrencyToken=Guid.Parse("00000000-0000-4000-8000-00000000f103") },
                new ValuationPresetEntity { Id=Guid.Parse("00000000-0000-4000-8000-00000000f004"), Label="Camper conversion", SuggestedAmount=0m, Active=true, Version=1, UpdatedBy="system:v1-foundation", UpdatedAtUtc=DateTimeOffset.UnixEpoch, ConcurrencyToken=Guid.Parse("00000000-0000-4000-8000-00000000f104") },
                new ValuationPresetEntity { Id=Guid.Parse("00000000-0000-4000-8000-00000000f005"), Label="Driving tuition", SuggestedAmount=500m, Active=true, Version=1, UpdatedBy="system:v1-foundation", UpdatedAtUtc=DateTimeOffset.UnixEpoch, ConcurrencyToken=Guid.Parse("00000000-0000-4000-8000-00000000f105") });
        });
        builder.Entity<LabourRateCardEntity>(e =>
        {
            e.ToTable("LabourRateCards"); e.HasKey(x => x.Id);
            e.Property(x => x.Label).HasMaxLength(200); e.Property(x => x.PanelRate).HasPrecision(18, 2);
            e.Property(x => x.UpdatedBy).HasMaxLength(200); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<AppliedValuationSnapshotEntity>(e =>
        {
            e.ToTable("AppliedValuationSnapshots"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.CaseId, x.SnapshotHash }).IsUnique();
            e.HasIndex(x => new { x.CaseId, x.AcceptedAtUtc });
            e.Property(x => x.SnapshotHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.AcceptedEngineerValue).HasPrecision(18, 2);
        });
        builder.Entity<GlassRepairEstimateSessionEntity>(e =>
        {
            e.ToTable("GlassRepairEstimateSessions"); e.HasKey(x => x.Id); e.HasIndex(x => x.OperationKey).IsUnique();
            e.HasIndex(x => x.ActiveAccountKey).IsUnique().HasFilter("[ActiveAccountKey] IS NOT NULL");
            e.Property(x => x.State).HasConversion<string>().HasMaxLength(40);
            e.Property(x => x.CallbackDigest).HasMaxLength(64).IsFixedLength(); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<CaseReportGenerationEntity>(e =>
        {
            e.ToTable("CaseReportGenerations"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.CaseId, x.SnapshotHash }).IsUnique().HasFilter("[State] <> N'Stale'");
            e.Property(x => x.SnapshotHash).HasMaxLength(64).IsFixedLength();
        });
        builder.Entity<GeneratedCaseArtifactEntity>(e =>
        {
            e.ToTable("GeneratedCaseArtifacts", t => t.HasCheckConstraint("CK_GeneratedCaseArtifacts_Custody", "[State] <> 'Confirmed' OR ([VersionId] IS NOT NULL AND [Sha256] IS NOT NULL AND [FailureCode] IS NULL)")); e.HasKey(x => x.Id); e.HasIndex(x => new { x.GenerationId, x.Kind }).IsUnique();
            e.HasIndex(x => x.OperationKey).IsUnique(); e.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        });
        builder.Entity<CaseReportDeliveryIntentEntity>(e =>
        {
            e.ToTable("CaseReportDeliveryIntents"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.GenerationId, x.OperationKey }).IsUnique();
            e.Property(x => x.PayloadHash).HasMaxLength(64).IsFixedLength(); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<RetainedInstructionAnalysisEntity>(e =>
        {
            e.ToTable("RetainedInstructionAnalyses"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.IntakeReceiptId, x.IntakeAssetId, x.OperationKey }).IsUnique();
            e.Property(x => x.SourceSha256).HasMaxLength(64).IsFixedLength();
        });
        builder.Entity<IntakeSourceCandidateEntity>(e =>
        {
            e.ToTable("IntakeSourceCandidates", t => t.HasCheckConstraint(
                "CK_IntakeSourceCandidates_Source",
                "([DocumentVersionId] IS NOT NULL AND [IntakeAssetId] IS NULL) OR ([DocumentVersionId] IS NULL AND [IntakeAssetId] IS NOT NULL)"));
            e.HasKey(x => x.Id); e.HasIndex(x => x.AnalysisId);
            e.Property(x => x.SourceSha256).HasMaxLength(64).IsFixedLength();
        });
        builder.Entity<IntakeOcrOperationEntity>(e =>
        {
            e.ToTable("IntakeOcrOperations", t => t.HasCheckConstraint("CK_IntakeOcrOperations_Source", "([DocumentVersionId] IS NULL AND [IntakeAssetId] IS NOT NULL) OR ([DocumentVersionId] IS NOT NULL AND [IntakeAssetId] IS NULL)")); e.HasKey(x => x.Id); e.HasIndex(x => x.OperationKey).IsUnique();
            e.HasIndex(x => new { x.DocumentVersionId, x.SourceSha256 }); e.Property(x => x.SourceSha256).HasMaxLength(64).IsFixedLength();
            e.Property(x => x.ResponseSha256).HasMaxLength(64).IsFixedLength(); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<DocumentContentCacheEntryEntity>(e =>
        {
            e.ToTable("DocumentContentCacheEntries", t => t.HasCheckConstraint("CK_DocumentContentCacheEntries_Source", "([DocumentVersionId] IS NULL AND [IntakeAssetId] IS NOT NULL) OR ([DocumentVersionId] IS NOT NULL AND [IntakeAssetId] IS NULL)")); e.HasKey(x => x.Id); e.HasIndex(x => x.DocumentVersionId).IsUnique(); e.HasIndex(x => x.IntakeAssetId).IsUnique();
            e.Property(x => x.VerifiedSha256).HasMaxLength(64).IsFixedLength(); e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<ClaimSourceEntity>(e =>
        {
            e.ToTable("ClaimSources"); e.HasKey(x => x.Id); e.HasIndex(x => x.Name);
            e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<OrganizationDirectoryEntryEntity>(e =>
        {
            e.ToTable("OrganizationDirectoryEntries"); e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            e.Property(x => x.Telephone).HasMaxLength(50); e.Property(x => x.Email).HasMaxLength(320); e.Property(x => x.UpdatedBy).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(50); e.Property(x => x.Name).HasMaxLength(300); e.Property(x => x.NormalizedName).HasMaxLength(300);
            e.Property(x => x.Postcode).HasMaxLength(20); e.Property(x => x.NormalizedPostcode).HasMaxLength(20);
            e.HasIndex(x => new { x.Role, x.NormalizedName, x.Id });
            e.HasIndex(x => new { x.Role, x.NormalizedPostcode, x.Id });
        });
        builder.Entity<PublicUploadSessionEntity>(e =>
        {
            e.ToTable("PublicUploadSessions"); e.HasKey(x => x.Id); e.HasIndex(x => x.RequestUploadLinkId).IsUnique();
            e.Property(x => x.Version).IsConcurrencyToken(); e.Property(x => x.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
        });
        builder.Entity<PublicUploadOccurrenceEntity>(e =>
        {
            e.ToTable("PublicUploadOccurrences"); e.HasKey(x => x.Id); e.HasIndex(x => new { x.SessionId, x.OperationKey }).IsUnique();
            e.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength();
        });
        builder.Entity<UserExternalCredentialEntity>().HasOne<PegasusIdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<GlassRepairEstimateSessionEntity>().HasOne<CaseEntity>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<GlassRepairEstimateSessionEntity>().HasOne<PegasusIdentityUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CaseReportGenerationEntity>().HasOne<CaseEntity>().WithMany().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<GeneratedCaseArtifactEntity>().HasOne<CaseReportGenerationEntity>().WithMany().HasForeignKey(x => x.GenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<GeneratedCaseArtifactEntity>().HasOne<DocumentVersionEntity>().WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CaseReportDeliveryIntentEntity>().HasOne<CaseReportGenerationEntity>().WithMany().HasForeignKey(x => x.GenerationId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RetainedInstructionAnalysisEntity>().HasOne<IntakeReceiptEntity>().WithMany().HasForeignKey(x => x.IntakeReceiptId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<RetainedInstructionAnalysisEntity>().HasOne<IntakeAssetEntity>().WithMany().HasForeignKey(x => x.IntakeAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<IntakeSourceCandidateEntity>().HasOne<RetainedInstructionAnalysisEntity>().WithMany().HasForeignKey(x => x.AnalysisId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<IntakeSourceCandidateEntity>().HasOne<DocumentVersionEntity>().WithMany().HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<IntakeSourceCandidateEntity>().HasOne<IntakeAssetEntity>().WithMany().HasForeignKey(x => x.IntakeAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<IntakeOcrOperationEntity>().HasOne<DocumentVersionEntity>().WithMany().HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<IntakeOcrOperationEntity>().HasOne<IntakeAssetEntity>().WithMany().HasForeignKey(x => x.IntakeAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DocumentContentCacheEntryEntity>().HasOne<DocumentVersionEntity>().WithMany().HasForeignKey(x => x.DocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DocumentContentCacheEntryEntity>().HasOne<IntakeAssetEntity>().WithMany().HasForeignKey(x => x.IntakeAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PublicUploadSessionEntity>().HasOne<RequestUploadLinkEntity>().WithMany().HasForeignKey(x => x.RequestUploadLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PublicUploadOccurrenceEntity>().HasOne<PublicUploadSessionEntity>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
