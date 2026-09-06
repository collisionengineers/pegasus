using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class PegasusDbContext(DbContextOptions<PegasusDbContext> options)
    : IdentityDbContext<PegasusIdentityUser, IdentityRole<Guid>, Guid>(options)
{
    internal DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    internal DbSet<OrganizationRoleEntity> OrganizationRoles => Set<OrganizationRoleEntity>();
    internal DbSet<OrganizationAdministrationOperationEntity> OrganizationAdministrationOperations =>
        Set<OrganizationAdministrationOperationEntity>();
    internal DbSet<PrincipalSequenceLineageEntity> PrincipalSequenceLineages =>
        Set<PrincipalSequenceLineageEntity>();
    internal DbSet<PrincipalEntity> Principals => Set<PrincipalEntity>();
    internal DbSet<CaseSequenceEntity> CaseSequences => Set<CaseSequenceEntity>();
    internal DbSet<CaseEntity> Cases => Set<CaseEntity>();
    internal DbSet<CaseIntakeLinkEntity> CaseIntakeLinks => Set<CaseIntakeLinkEntity>();
    internal DbSet<CaseDataSnapshotEntity> CaseDataSnapshots => Set<CaseDataSnapshotEntity>();
    internal DbSet<CaseDataFieldEntity> CaseDataFields => Set<CaseDataFieldEntity>();
    internal DbSet<IntakeManualAssociationEntity> IntakeManualAssociations =>
        Set<IntakeManualAssociationEntity>();
    internal DbSet<IntakeMutationHistoryEntity> IntakeMutationHistory =>
        Set<IntakeMutationHistoryEntity>();
    internal DbSet<CaseHistoryEntity> CaseHistory => Set<CaseHistoryEntity>();
    internal DbSet<ExternalWorkItemEntity> ExternalWorkItems => Set<ExternalWorkItemEntity>();
    internal DbSet<VehicleLookupRequestEntity> VehicleLookupRequests =>
        Set<VehicleLookupRequestEntity>();
    internal DbSet<VehicleLookupObservationEntity> VehicleLookupObservations =>
        Set<VehicleLookupObservationEntity>();
    internal DbSet<VehicleConfirmationEntity> VehicleConfirmations =>
        Set<VehicleConfirmationEntity>();
    internal DbSet<ImageIntakeEntity> ImageIntakes => Set<ImageIntakeEntity>();
    internal DbSet<ImageIntakeSequenceEntity> ImageIntakeSequences =>
        Set<ImageIntakeSequenceEntity>();
    internal DbSet<ImageIntakeLifecycleEventEntity> ImageIntakeLifecycleEvents =>
        Set<ImageIntakeLifecycleEventEntity>();
    internal DbSet<ImageVrmSuggestionEntity> ImageVrmSuggestions =>
        Set<ImageVrmSuggestionEntity>();
    internal DbSet<TriageEntity> Triage => Set<TriageEntity>();
    internal DbSet<TriageFindingEntity> TriageFindings => Set<TriageFindingEntity>();
    internal DbSet<TriageResponseEvidenceLinkEntity> TriageResponseEvidenceLinks =>
        Set<TriageResponseEvidenceLinkEntity>();
    internal DbSet<TriageHistoryEntity> TriageHistory => Set<TriageHistoryEntity>();
    internal DbSet<SentEmailEvidenceEntity> SentEmailEvidence => Set<SentEmailEvidenceEntity>();
    internal DbSet<EmailResponseEvidenceEntity> EmailResponseEvidence => Set<EmailResponseEvidenceEntity>();
    internal DbSet<ActionHistoryEntity> ActionHistory => Set<ActionHistoryEntity>();
    internal DbSet<SecurityEventEntity> SecurityEvents => Set<SecurityEventEntity>();
    internal DbSet<WorkflowConfigurationEntity> WorkflowConfigurations =>
        Set<WorkflowConfigurationEntity>();
    internal DbSet<ApprovedMailboxEntity> ApprovedMailboxes =>
        Set<ApprovedMailboxEntity>();
    internal DbSet<ApprovedMailboxSubscriptionEntity> ApprovedMailboxSubscriptions =>
        Set<ApprovedMailboxSubscriptionEntity>();
    internal DbSet<CaseWorkflowEntity> CaseWorkflows => Set<CaseWorkflowEntity>();
    internal DbSet<CaseWorkflowEventEntity> CaseWorkflowEvents => Set<CaseWorkflowEventEntity>();
    internal DbSet<CaseEditLeaseOperationEntity> CaseEditLeaseOperations =>
        Set<CaseEditLeaseOperationEntity>();
    internal DbSet<EvaFirstHandoffProxyEntity> EvaFirstHandoffProxies =>
        Set<EvaFirstHandoffProxyEntity>();
    internal DbSet<EvaSubmissionEntity> EvaSubmissions => Set<EvaSubmissionEntity>();
    internal DbSet<CaseReportApprovalEntity> CaseReportApprovals => Set<CaseReportApprovalEntity>();
    internal DbSet<CaseReportSentEvidenceEntity> CaseReportSentEvidence => Set<CaseReportSentEvidenceEntity>();
    internal DbSet<CaseDueWorkEntity> CaseDueWork => Set<CaseDueWorkEntity>();
    internal DbSet<CaseManualChaseEntity> CaseManualChases => Set<CaseManualChaseEntity>();
    internal DbSet<CaseTaskEntity> CaseTasks => Set<CaseTaskEntity>();
    internal DbSet<CaseDueChaserEntity> CaseDueChasers => Set<CaseDueChaserEntity>();
    internal DbSet<CaseAssessmentFieldEntity> CaseAssessmentFields =>
        Set<CaseAssessmentFieldEntity>();
    internal DbSet<CaseEstimateLineEntity> CaseEstimateLines => Set<CaseEstimateLineEntity>();
    internal DbSet<CaseRepairSpecificationEntity> CaseRepairSpecifications =>
        Set<CaseRepairSpecificationEntity>();
    internal DbSet<CaseValuationEntity> CaseValuations => Set<CaseValuationEntity>();
    internal DbSet<EngineerNoteEntity> EngineerNotes => Set<EngineerNoteEntity>();
    internal DbSet<AiWorkRequestEntity> AiWorkRequests => Set<AiWorkRequestEntity>();
    internal DbSet<SendToAiControlEntity> SendToAiControl => Set<SendToAiControlEntity>();
    internal DbSet<AiJobEntity> AiJobs => Set<AiJobEntity>();
    internal DbSet<PrincipalApiCredentialEntity> PrincipalApiCredentials => Set<PrincipalApiCredentialEntity>();
    internal DbSet<ProviderSubmissionEntity> ProviderSubmissions => Set<ProviderSubmissionEntity>();
    internal DbSet<LabourRateCardEntity> LabourRateCards => Set<LabourRateCardEntity>();


    internal DbSet<IntakeReceiptEntity> IntakeReceipts => Set<IntakeReceiptEntity>();

    internal DbSet<IntakeAssetEntity> IntakeAssets => Set<IntakeAssetEntity>();

    internal DbSet<IntakeSearchDocumentEntity> IntakeSearchDocuments => Set<IntakeSearchDocumentEntity>();

    internal DbSet<InstructionDraftEntity> InstructionDrafts => Set<InstructionDraftEntity>();

    internal DbSet<IntakeReceiptEventEntity> IntakeReceiptEvents => Set<IntakeReceiptEventEntity>();
    internal DbSet<IntakeStagedReceiptEntity> IntakeStagedReceipts => Set<IntakeStagedReceiptEntity>();
    internal DbSet<IntakeSubmissionGroupEntity> IntakeSubmissionGroups => Set<IntakeSubmissionGroupEntity>();
    internal DbSet<IntakeSubmissionGroupMemberEntity> IntakeSubmissionGroupMembers =>
        Set<IntakeSubmissionGroupMemberEntity>();

    internal DbSet<IntakeWorkItemEntity> IntakeWorkItems => Set<IntakeWorkItemEntity>();
    internal DbSet<IntakeEvaluationEntity> IntakeEvaluations => Set<IntakeEvaluationEntity>();
    internal DbSet<UnidentifiedItemEntity> UnidentifiedItems => Set<UnidentifiedItemEntity>();
    internal DbSet<UnidentifiedSequenceEntity> UnidentifiedSequences => Set<UnidentifiedSequenceEntity>();
    internal DbSet<UnidentifiedHistoryEntity> UnidentifiedHistory => Set<UnidentifiedHistoryEntity>();
    internal DbSet<IntakeAllocationAttemptEntity> IntakeAllocationAttempts =>
        Set<IntakeAllocationAttemptEntity>();
    internal DbSet<ApprovedInboxPollStateEntity> ApprovedInboxPollStates =>
        Set<ApprovedInboxPollStateEntity>();
    internal DbSet<ApprovedInboxPoisonMessageEntity> ApprovedInboxPoisonMessages =>
        Set<ApprovedInboxPoisonMessageEntity>();
    internal DbSet<RetainedMailboxMessageEntity> RetainedMailboxMessages =>
        Set<RetainedMailboxMessageEntity>();
    internal DbSet<RetainedMailboxAttachmentEntity> RetainedMailboxAttachments =>
        Set<RetainedMailboxAttachmentEntity>();
    internal DbSet<RetainedMailFolderMoveEntity> RetainedMailFolderMoves =>
        Set<RetainedMailFolderMoveEntity>();
    internal DbSet<ApprovedSentPollStateEntity> ApprovedSentPollStates =>
        Set<ApprovedSentPollStateEntity>();
    internal DbSet<ApprovedSentPollOutcomeEntity> ApprovedSentPollOutcomes =>
        Set<ApprovedSentPollOutcomeEntity>();
    internal DbSet<IntakeMailRouteDecisionEntity> IntakeMailRouteDecisions =>
        Set<IntakeMailRouteDecisionEntity>();

    internal DbSet<IntakeMailClassificationDecisionEntity> IntakeMailClassificationDecisions =>
        Set<IntakeMailClassificationDecisionEntity>();
    internal DbSet<IntakeMailClassificationHistoryEntity> IntakeMailClassificationHistory =>
        Set<IntakeMailClassificationHistoryEntity>();

    internal DbSet<CaseMatchIndexEntity> CaseMatchIndex =>
        Set<CaseMatchIndexEntity>();

    internal DbSet<IntakeCaseMatchDecisionEntity> IntakeCaseMatchDecisions =>
        Set<IntakeCaseMatchDecisionEntity>();




    internal DbSet<ProviderDomainPackageEntity> ProviderDomainPackages => Set<ProviderDomainPackageEntity>();

    internal DbSet<ProviderReferenceEntity> ProviderReferences => Set<ProviderReferenceEntity>();

    internal DbSet<ProviderDomainEvidenceEntity> ProviderDomainEvidence => Set<ProviderDomainEvidenceEntity>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RegenerateConcurrencyTokens();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        RegenerateConcurrencyTokens();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RegenerateConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<IApplicationManagedConcurrencyToken>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.ConcurrencyToken = Guid.NewGuid();
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        V1FoundationModelConfiguration.Configure(builder);
        // Registers the OpenIddict application/authorization/scope/token
        // entities backing the Automation Actor client-credentials ingress.
        builder.UseOpenIddict();
        CustodyModelConfiguration.Configure(builder);
        MailboxModelConfiguration.Configure(builder);
        AuditIdentityModelConfiguration.Configure(builder);
        AdministrationPolicyModelConfiguration.Configure(builder);
        CaseDataModelConfiguration.Configure(builder);
        CaseMatchModelConfiguration.Configure(builder);
        VehicleModelConfiguration.Configure(builder);
        EvaHandoffModelConfiguration.Configure(builder);
        EvaSubmissionModelConfiguration.Configure(builder);
        AssessmentModelConfiguration.Configure(builder);
        EngineerNotesModelConfiguration.Configure(builder);
        PrincipalCredentialModelConfiguration.Configure(builder);
        ProviderSubmissionModelConfiguration.Configure(builder);
        IntakeAllocationModelConfiguration.Configure(builder);

        builder.Entity<PegasusIdentityUser>(entity =>
        {
            entity.Property(item => item.IsEnabled).HasDefaultValue(true);
            entity.Property(item => item.MustChangePassword).HasDefaultValue(true);
            entity.Property(item => item.IsSignOffEngineer).HasDefaultValue(false);
            entity.Property(item => item.SignOffPrintedName)
                .HasMaxLength(StaffAccountAdministrationPolicy.MaximumSignOffPrintedNameLength);
            entity.Property(item => item.SignOffQualifications)
                .HasMaxLength(StaffAccountAdministrationPolicy.MaximumSignOffQualificationsLength);
            entity.Property(item => item.SignOffSignatureDigest).HasMaxLength(64);
            entity.Property(item => item.IsDefaultSignOffEngineer).HasDefaultValue(false);
            entity.HasIndex(item => item.IsDefaultSignOffEngineer)
                .IsUnique()
                .HasFilter("[IsDefaultSignOffEngineer] = 1");
        });

        builder.Entity<IntakeReceiptEntity>(entity =>
        {
            entity.ToTable("IntakeReceipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceReaderKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.SourceReaderVersion).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ExtractionPolicyKey).HasMaxLength(100);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.Decision).HasMaxLength(40).IsRequired();
            entity.Property(item => item.DecisionReason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureReason).HasMaxLength(500);
            entity.Property(item => item.EvidenceJson).IsRequired();
            entity.Property(item => item.FieldsJson).IsRequired();
            entity.Property(item => item.OcrCandidatesJson).IsRequired();
            entity.HasIndex(item => item.SourceHash);
            entity.HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique();
            entity.HasIndex(item => new { item.SourceChannel, item.ProcessedAtUtc, item.Id }).IsDescending(false, true, false);
        });

        builder.Entity<IntakeAssetEntity>(entity =>
        {
            entity.ToTable("IntakeAssets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceLabel).HasMaxLength(500).IsRequired();
            entity.Property(item => item.FileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Kind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Disposition).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ContentHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.StorageKey).HasMaxLength(200).IsRequired();
            entity.Property(item => item.BoxFileId).HasMaxLength(200);
            entity.Property(item => item.BoxVersionId).HasMaxLength(200);
            entity.Property(item => item.CustodyStatus).HasMaxLength(40);
            entity.HasIndex(item => new { item.IntakeReceiptId, item.ContentHash });
            entity.HasOne(item => item.IntakeReceipt)
                .WithMany(item => item.Assets)
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntakeSearchDocumentEntity>(entity =>
        {
            entity.ToTable("IntakeSearchDocuments");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceLabel).HasMaxLength(500).IsRequired();
            entity.Property(item => item.AttachmentFileName).HasMaxLength(260);
            entity.HasIndex(item => new { item.IntakeReceiptId, item.Ordinal }).IsUnique();
            entity.HasOne(item => item.IntakeReceipt)
                .WithMany(item => item.SearchDocuments)
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<InstructionDraftEntity>(entity =>
        {
            entity.ToTable("InstructionDrafts");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.SuggestedPrincipalCode).HasMaxLength(20);
            entity.Property(item => item.ClaimantName).HasMaxLength(300);
            entity.Property(item => item.ClaimNumber).HasMaxLength(100);
            entity.Property(item => item.VehicleRegistration).HasMaxLength(20);
            entity.Property(item => item.VehicleMake).HasMaxLength(100);
            entity.Property(item => item.VehicleModel).HasMaxLength(100);
            entity.Property(item => item.AccidentCircumstances).HasMaxLength(2000);
            entity.Property(item => item.DateOfIncident).HasColumnType("date");
            entity.Property(item => item.InstructionDate).HasColumnType("date");
            entity.Property(item => item.InspectionAddress).HasMaxLength(1000);
            entity.Property(item => item.VehicleMileageUnit).HasMaxLength(40);
            entity.Property(item => item.VatStatus).HasMaxLength(100);
            entity.Property(item => item.ClaimantAddress).HasMaxLength(1000);
            entity.Property(item => item.ClaimantContactNumber).HasMaxLength(100);
            entity.Property(item => item.FileHandlerName).HasMaxLength(300);
            entity.Property(item => item.FileHandlerEmailAddress).HasMaxLength(320);
            entity.Property(item => item.FileHandlerPhoneNumber).HasMaxLength(100);
            entity.Property(item => item.Notes).HasMaxLength(2000);
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne(item => item.InstructionDraft)
                .HasForeignKey<InstructionDraftEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IntakeReceiptEventEntity>(entity =>
        {
            entity.ToTable("IntakeReceiptEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.DetailsJson).IsRequired();
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeStagedReceiptEntity>(entity =>
        {
            entity.ToTable("IntakeStagedReceipts");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.MediaType).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.StorageKey).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique();
            entity.HasIndex(item => item.SourceHash);
        });

        builder.Entity<IntakeSubmissionGroupEntity>(entity =>
        {
            entity.ToTable("IntakeSubmissionGroups", table =>
                table.HasCheckConstraint(
                    "CK_IntakeSubmissionGroups_ExpectedMemberCount", "[ExpectedMemberCount] >= 1"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SubmissionToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.SourceChannel, item.SubmissionToken }).IsUnique();
            entity.HasIndex(item => item.ParentReceiptId)
                .IsUnique()
                .HasFilter("[ParentReceiptId] IS NOT NULL");
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.ParentReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeSubmissionGroupMemberEntity>(entity =>
        {
            entity.ToTable("IntakeSubmissionGroupMembers", table =>
                table.HasCheckConstraint("CK_IntakeSubmissionGroupMembers_Ordinal", "[Ordinal] >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceFileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => new { item.GroupId, item.Ordinal }).IsUnique();
            entity.HasIndex(item => item.StagedReceiptId).IsUnique();
            entity.HasOne(item => item.Group)
                .WithMany(item => item.Members)
                .HasForeignKey(item => item.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntakeStagedReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.StagedReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeWorkItemEntity>(entity =>
        {
            entity.ToTable("IntakeWorkItems", table =>
                table.HasCheckConstraint("CK_IntakeWorkItems_AttemptCount", "[AttemptCount] >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LeaseToken).HasMaxLength(64);
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => item.StagedReceiptId).IsUnique();
            entity.HasIndex(item => new { item.State, item.DueAtUtc });
            entity.HasOne(item => item.StagedReceipt)
                .WithOne(item => item.WorkItem)
                .HasForeignKey<IntakeWorkItemEntity>(item => item.StagedReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeEvaluationEntity>(entity =>
        {
            entity.ToTable("IntakeEvaluations");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.StagedReceiptId, item.Revision }).IsUnique();
            entity.HasOne<IntakeStagedReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.StagedReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<OrganizationEntity>(entity =>
        {
            entity.ToTable("Organizations", table =>
            {
                table.HasCheckConstraint("CK_Organizations_Name", "[Name] <> ''");
                table.HasCheckConstraint("CK_Organizations_Version", "[Version] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(300).IsRequired();
            entity.Property(item => item.NormalizedName)
                .HasMaxLength(300)
                .IsRequired()
                .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Name])))", stored: true);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.Name);
            entity.HasIndex(item => item.NormalizedName)
                .IsUnique()
                .HasFilter(null);
        });
        builder.Entity<OrganizationAdministrationOperationEntity>(entity =>
        {
            entity.ToTable("OrganizationAdministrationOperations");
            entity.HasKey(item => item.OperationKey);
            entity.Property(item => item.OperationKey).HasMaxLength(100);
            entity.Property(item => item.CommandKind).HasMaxLength(64).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.ResultJson).IsRequired();
        });


        builder.Entity<OrganizationRoleEntity>(entity =>
        {
            entity.ToTable("OrganizationRoles", table =>
                table.HasCheckConstraint(
                    "CK_OrganizationRoles_Role",
                    "[Role] IN ('work_provider', 'instruction_intermediary')"));
            entity.HasKey(item => new { item.OrganizationId, item.Role });
            entity.Property(item => item.Role).HasMaxLength(40).IsRequired();
            entity.HasOne(item => item.Organization)
                .WithMany(item => item.Roles)
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PrincipalSequenceLineageEntity>(entity =>
        {
            entity.ToTable("PrincipalSequenceLineages");
            entity.HasKey(item => item.Id);
        });

        builder.Entity<PrincipalEntity>(entity =>
        {
            entity.ToTable("Principals", table =>
            {
                table.HasCheckConstraint("CK_Principals_Code", "[Code] <> ''");
                table.HasCheckConstraint("CK_Principals_Version", "[Version] >= 0");
                table.HasCheckConstraint(
                    "CK_Principals_InspectionMode",
                    "[InspectionMode] IN ('physical_address', 'image_based_assessment')");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.Property(item => item.InspectionMode)
                .HasMaxLength(40)
                .IsRequired()
                .HasDefaultValue("physical_address");
            entity.Property(item => item.DefaultInspectionLocationLabel).HasMaxLength(200);
            entity.Property(item => item.DefaultInspectionAddress).HasMaxLength(1000);
            entity.Property(item => item.DefaultInspectionPostcode).HasMaxLength(20);
            entity.Property(item => item.DefaultInspectionSourceKind).HasMaxLength(40);
            entity.Property(item => item.DefaultInspectionSourceRecordId).HasMaxLength(200);
            // EXT-04: both default off, so adding the columns switches nothing
            // on. They are independent by operator decision, which makes
            // automatic-without-manual legal - that principal submits
            // unattended and has no button.
            entity.Property(item => item.EvaManualSubmission)
                .IsRequired()
                .HasDefaultValue(false);
            entity.Property(item => item.EvaAutomaticSubmission)
                .IsRequired()
                .HasDefaultValue(false);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.Code).IsUnique();
            entity.HasIndex(item => item.PredecessorId).IsUnique();
            entity.HasIndex(item => item.SuccessorId).IsUnique();
            entity.HasOne(item => item.Organization)
                .WithMany(item => item.Principals)
                .HasForeignKey(item => item.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SequenceLineage)
                .WithMany(item => item.Principals)
                .HasForeignKey(item => item.SequenceLineageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Predecessor)
                .WithMany()
                .HasForeignKey(item => item.PredecessorId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Successor)
                .WithMany()
                .HasForeignKey(item => item.SuccessorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseSequenceEntity>(entity =>
        {
            entity.ToTable("CaseSequences", table =>
            {
                table.HasCheckConstraint("CK_CaseSequences_Year", "[Year] >= 2000 AND [Year] <= 9999");
                table.HasCheckConstraint(
                    "CK_CaseSequences_LastAllocatedSequence",
                    "[LastAllocatedSequence] >= 0 AND [LastAllocatedSequence] <= 999");
            });
            entity.HasKey(item => new { item.SequenceLineageId, item.Year });
            entity.HasOne(item => item.SequenceLineage)
                .WithMany(item => item.Sequences)
                .HasForeignKey(item => item.SequenceLineageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseEntity>(entity =>
        {
            entity.ToTable("Cases", table =>
            {
                table.HasCheckConstraint("CK_Cases_Sequence", "[Sequence] >= 1 AND [Sequence] <= 999");
                table.HasCheckConstraint("CK_Cases_Version", "[Version] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reference).HasMaxLength(40).IsRequired();
            entity.Property(item => item.AuditReference).HasMaxLength(43);
            entity.Property(item => item.Type).HasMaxLength(40).IsRequired();
            entity.Property(item => item.InitialState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CustodyState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.StandaloneAuditAssessment).HasMaxLength(40);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.Property(item => item.CustodyRootRemoteId).HasMaxLength(200);
            entity.Property(item => item.CustodySourceRemoteId).HasMaxLength(200);
            entity.Property(item => item.CustodySourceContentHash).HasMaxLength(64);
            entity.Property(item => item.CustodySourceETag).HasMaxLength(200);
            entity.HasIndex(item => item.Reference).IsUnique();
            entity.HasIndex(item => item.AuditReference).IsUnique();
            entity.HasIndex(item => item.OriginIntakeReceiptId);
            entity.HasIndex(item => new { item.SequenceLineageId, item.Year, item.Sequence }).IsUnique();
            entity.HasOne(item => item.Principal)
                .WithMany(item => item.Cases)
                .HasForeignKey(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.OriginIntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExternalWorkItemEntity>(entity =>
        {
            entity.ToTable("ExternalWorkItems", table =>
                table.HasCheckConstraint("CK_ExternalWorkItems_AttemptCount", "[AttemptCount] >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Kind).HasMaxLength(100).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.LeaseToken).HasMaxLength(64);
            entity.Property(item => item.ExternalReceipt).HasMaxLength(500);
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureReason).HasMaxLength(500);
            entity.Property(item => item.CaseRootCreationToken).HasMaxLength(26).IsFixedLength();
            entity.Property(item => item.AuditFolderCreationToken).HasMaxLength(26).IsFixedLength();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.State, item.DueAtUtc });
            entity.HasIndex(item => new { item.DueAtUtc, item.Id }).IsDescending(true, false);
            entity.HasIndex(item => new { item.LeaseExpiresAtUtc, item.Id }).IsDescending(true, false);
            entity.HasIndex(item => new { item.CompletedAtUtc, item.Id }).IsDescending(true, false);
            entity.HasIndex(item => item.ImageIntakeId);
            entity.HasOne(item => item.Case)
                .WithMany(item => item.ExternalWork)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.ImageIntake)
                .WithMany()
                .HasForeignKey(item => item.ImageIntakeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseIntakeLinkEntity>(entity =>
        {
            entity.ToTable("CaseIntakeLinks");
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.AcceptanceCommandMaterialJson).HasMaxLength(2048);
            entity.Property(item => item.AcceptanceCommandFingerprint).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(item => item.CustodyWorkId).IsUnique();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasOne(item => item.Case)
                .WithMany(item => item.IntakeLinks)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.CustodyWork)
                .WithOne()
                .HasForeignKey<CaseIntakeLinkEntity>(item => item.CustodyWorkId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IntakeManualAssociationEntity>(entity =>
        {
            entity.ToTable("IntakeManualAssociations", table =>
                table.HasCheckConstraint(
                    "CK_IntakeManualAssociations_Version",
                    "[Version] >= 0"));
            entity.HasKey(item => item.IntakeReceiptId);
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.LastOperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.MatchPolicyKey).HasMaxLength(100);
            entity.HasIndex(item => item.CaseId);
            entity.HasIndex(item => item.LastOperationKey).IsUnique();
            entity.HasOne(item => item.IntakeReceipt)
                .WithOne(item => item.ManualAssociation)
                .HasForeignKey<IntakeManualAssociationEntity>(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<IntakeMutationHistoryEntity>(entity =>
        {
            entity.ToTable("IntakeMutationHistory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.IntakeReceiptId, item.OccurredAtUtc });
            entity.HasIndex(item => new { item.CaseId, item.OccurredAtUtc });
            entity.HasOne(item => item.IntakeReceipt)
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ImageIntakeLifecycleEventEntity>(entity =>
        {
            entity.ToTable("ImageIntakeLifecycleEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.CaseReference).HasMaxLength(50);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.ImageIntakeId, item.OccurredAtUtc });
            entity.HasOne(item => item.ImageIntake)
                .WithMany()
                .HasForeignKey(item => item.ImageIntakeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CaseHistoryEntity>(entity =>
        {
            entity.ToTable("CaseHistory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.OccurredAtUtc });
            entity.HasOne(item => item.Case)
                .WithMany(item => item.History)
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ImageIntakeEntity>(entity =>
        {
            entity.ToTable("ImageIntakes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.NormalizedVehicleRegistration).HasMaxLength(20).IsRequired();
            entity.Property(item => item.ImageIntakeReference).HasMaxLength(30).IsRequired();
            entity.Property(item => item.CreatedByActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CreatedByActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.CreationOperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.LifecycleState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.MergedIntoCaseReference).HasMaxLength(50);
            entity.Property(item => item.ClosureReason).HasMaxLength(500);
            entity.Property(item => item.CustodyState).HasMaxLength(40);
            entity.Property(item => item.CustodyRootRemoteId).HasMaxLength(200);
            entity.HasIndex(item => new { item.LifecycleState, item.CreatedAtUtc });
            entity.HasIndex(item => item.OriginReceiptId).IsUnique();
            entity.HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique();
            entity.HasIndex(item => item.ImageIntakeReference).IsUnique();
            entity.HasIndex(item => item.PrincipalId);
            entity.HasIndex(item => item.CreationOperationKey).IsUnique();
            entity.HasIndex(item => new { item.NormalizedVehicleRegistration, item.CreatedAtUtc });
            // One ImageIntake per submission group (INTK-015); single-receipt
            // registrations carry no group and are exempt via the filter.
            entity.HasIndex(item => item.SubmissionGroupId)
                .IsUnique()
                .HasFilter("[SubmissionGroupId] IS NOT NULL");
            entity.HasOne(item => item.OriginReceipt)
                .WithMany()
                .HasForeignKey(item => item.OriginReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IntakeSubmissionGroupEntity>()
                .WithMany()
                .HasForeignKey(item => item.SubmissionGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Principal)
                .WithMany()
                .HasForeignKey(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ImageIntakeSequenceEntity>(entity =>
        {
            entity.ToTable("ImageIntakeSequences", table =>
                table.HasCheckConstraint(
                    "CK_ImageIntakeSequences_LastAllocatedSequence",
                    "[LastAllocatedSequence] >= 0"));
            entity.HasKey(item => item.NormalizedVehicleRegistration);
            entity.Property(item => item.NormalizedVehicleRegistration).HasMaxLength(20);
        });

        builder.Entity<ImageVrmSuggestionEntity>(entity =>
        {
            entity.ToTable("ImageVrmSuggestions");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.StorageKey).HasMaxLength(400).IsRequired();
            entity.Property(item => item.ContentHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.EngineKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.EngineVersion).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ModelHashes).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SuggestedRegistration).HasMaxLength(20);
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.Property(item => item.FailureReason).HasMaxLength(500);
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Disposition).HasMaxLength(40).IsRequired();
            entity.Property(item => item.DispositionActor).HasMaxLength(200);
            entity.Property(item => item.DispositionReason).HasMaxLength(500);
            entity.Property(item => item.DispositionOperationKey).HasMaxLength(100);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.IntakeReceiptId, item.OccurredAtUtc });
            entity.HasOne(item => item.IntakeReceipt)
                .WithMany()
                .HasForeignKey(item => item.IntakeReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.IntakeAsset)
                .WithMany()
                .HasForeignKey(item => item.IntakeAssetId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TriageEntity>(entity =>
        {
            entity.ToTable("Triage", table =>
            {
                table.HasCheckConstraint("CK_Triage_Version", "[Version] >= 0");
                table.HasCheckConstraint("CK_Triage_Sequence", "[Sequence] > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SourceChannel).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ExternalReceiptToken).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.NormalizedVehicleRegistration).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Reference).HasMaxLength(32).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CreationOperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(item => item.OriginReceiptId).IsUnique();
            entity.HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique();
            entity.HasIndex(item => item.CreationOperationKey).IsUnique();
            entity.HasIndex(item => item.Sequence).IsUnique();
            entity.HasIndex(item => item.Reference).IsUnique();
            entity.HasIndex(item => new { item.State, item.CreatedAtUtc });
            entity.HasOne<IntakeReceiptEntity>()
                .WithMany()
                .HasForeignKey(item => item.OriginReceiptId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CaseEntity>()
                .WithMany()
                .HasForeignKey(item => item.LinkedCaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PrincipalEntity>()
                .WithMany()
                .HasForeignKey(item => item.PrincipalId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TriageFindingEntity>(entity =>
        {
            entity.ToTable("TriageFindings");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Roadworthiness).HasMaxLength(40);
            entity.Property(item => item.Assessment).HasMaxLength(40);
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => item.SupersedesFindingId).IsUnique();
            entity.HasIndex(item => new { item.TriageId, item.RecordedAtUtc });
            entity.HasOne(item => item.Triage)
                .WithMany(item => item.Findings)
                .HasForeignKey(item => item.TriageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TriageFindingEntity>()
                .WithMany()
                .HasForeignKey(item => item.SupersedesFindingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SentEmailEvidenceEntity>(entity =>
        {
            entity.ToTable("SentEmailEvidence", table =>
                table.HasCheckConstraint("CK_SentEmailEvidence_Version", "[Version] >= 0"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MessageIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Subject).HasMaxLength(500).IsRequired();
            entity.Property(item => item.RecipientsJson).IsRequired();
            entity.Property(item => item.MimeSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.MessageIdentity).IsUnique();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.ChaseDueAtUtc, item.TriageId });
            entity.HasIndex(item => new { item.SentAtUtc, item.Id }).IsDescending(true, false);
            entity.HasOne(item => item.Triage)
                .WithMany(item => item.SentEmailEvidence)
                .HasForeignKey(item => item.TriageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmailResponseEvidenceEntity>(entity =>
        {
            entity.ToTable("EmailResponseEvidence");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.MailboxId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.MailboxAddress).HasMaxLength(320).IsRequired();
            entity.Property(item => item.SentFolderIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ImmutableItemIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.MessageIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ConversationIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ReplyChainIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.InReplyToIdentitiesJson).IsRequired();
            entity.Property(item => item.SourceOccurrenceIdentity).HasMaxLength(200).IsRequired();
            entity.Property(item => item.SourceSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.MimeSha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(item => item.SentEvidenceId).IsUnique();
            entity.HasIndex(item => item.PollOutcomeId).IsUnique();
            entity.HasIndex(item => item.MessageIdentity).IsUnique();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.DiscoveredAtUtc, item.Id }).IsDescending(true, false);
            entity.HasOne(item => item.SentEvidence)
                .WithOne(item => item.Response)
                .HasForeignKey<EmailResponseEvidenceEntity>(item => item.SentEvidenceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApprovedSentPollOutcomeEntity>()
                .WithOne()
                .HasForeignKey<EmailResponseEvidenceEntity>(item => item.PollOutcomeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TriageResponseEvidenceLinkEntity>(entity =>
        {
            entity.ToTable("TriageResponseEvidenceLinks");
            entity.HasKey(item => new { item.TriageId, item.SentEvidenceId });
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => item.TriageId).IsUnique();
            entity.HasOne(item => item.Triage)
                .WithMany(item => item.ResponseEvidenceLinks)
                .HasForeignKey(item => item.TriageId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.SentEvidence)
                .WithMany(item => item.TriageLinks)
                .HasForeignKey(item => item.SentEvidenceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<TriageHistoryEntity>(entity =>
        {
            entity.ToTable("TriageHistory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Actor).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.AfterState).HasMaxLength(40).IsRequired();
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.TriageId, item.OccurredAtUtc });
            entity.HasOne(item => item.Triage)
                .WithMany(item => item.History)
                .HasForeignKey(item => item.TriageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ActionHistoryEntity>(entity =>
        {
            entity.ToTable("ActionHistory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.AggregateType).HasMaxLength(100).IsRequired();
            entity.Property(item => item.AggregateId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.EventKind).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(1000);
            entity.Property(item => item.PolicyVersion).HasMaxLength(100);
            entity.HasIndex(item => new { item.AggregateType, item.AggregateId, item.OccurredAtUtc });
            entity.HasIndex(item => new { item.AggregateType, item.CorrelationId });
            entity.HasIndex(item => item.OccurredAtUtc);
        });

        builder.Entity<SecurityEventEntity>(entity =>
        {
            entity.ToTable("SecurityEvents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Type).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.SubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.CorrelationId).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ReasonCode).HasMaxLength(100);
            entity.HasIndex(item => new { item.SubjectId, item.OccurredAtUtc });
            entity.HasIndex(item => item.OccurredAtUtc);
        });

        builder.Entity<UnidentifiedItemEntity>(entity =>
        {
            entity.ToTable("UnidentifiedItems", table =>
            {
                table.HasCheckConstraint("CK_UnidentifiedItems_Sequence", "[Sequence] > 0");
                table.HasCheckConstraint("CK_UnidentifiedItems_Version", "[Version] >= 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Reference).HasMaxLength(32).IsRequired();
            entity.Property(item => item.OriginKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ReasonCode).HasMaxLength(80).IsRequired();
            entity.Property(item => item.SafeDetail).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.State).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CreatedByActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.CreatedByActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.CreatedByActorRolesJson).HasMaxLength(500).IsRequired();
            entity.Property(item => item.ResolvedByActorKind).HasMaxLength(40);
            entity.Property(item => item.ResolvedByActorSubjectId).HasMaxLength(200);
            entity.Property(item => item.ResolvedByActorRolesJson).HasMaxLength(500);
            entity.Property(item => item.ResolutionReason).HasMaxLength(500);
            entity.Property(item => item.ResolutionTargetKind).HasMaxLength(40);
            entity.Property(item => item.ResolutionTargetId).HasMaxLength(200);
            entity.Property(item => item.ResolutionTargetReference).HasMaxLength(200);
            entity.Property(item => item.RegistrationOperationKey).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RegistrationFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Version).IsConcurrencyToken();
            entity.HasIndex(item => item.Sequence).IsUnique();
            entity.HasIndex(item => item.Reference).IsUnique();
            entity.HasIndex(item => new { item.OriginKind, item.OriginId }).IsUnique();
            entity.HasIndex(item => item.RegistrationOperationKey).IsUnique();
            entity.HasIndex(item => new { item.State, item.CreatedAtUtc, item.Sequence });
        });

        builder.Entity<UnidentifiedSequenceEntity>(entity =>
        {
            entity.ToTable("UnidentifiedSequences", table =>
                table.HasCheckConstraint("CK_UnidentifiedSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0"));
            entity.HasKey(item => item.Id);
        });

        builder.Entity<UnidentifiedHistoryEntity>(entity =>
        {
            entity.ToTable("UnidentifiedHistory");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.PreviousState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.NewState).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(200).IsRequired();
            entity.Property(item => item.TargetKind).HasMaxLength(40);
            entity.Property(item => item.TargetId).HasMaxLength(200);
            entity.Property(item => item.TargetReference).HasMaxLength(200);
            entity.HasIndex(item => item.OperationKey).IsUnique();
            entity.HasIndex(item => new { item.UnidentifiedItemId, item.OccurredAtUtc });
            entity.HasOne<UnidentifiedItemEntity>()
                .WithMany()
                .HasForeignKey(item => item.UnidentifiedItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProviderDomainPackageEntity>(entity =>
        {
            entity.ToTable("ProviderDomainPackages", table =>
            {
                table.HasCheckConstraint("CK_ProviderDomainPackages_SchemaVersion", "[SchemaVersion] > 0");
                table.HasCheckConstraint("CK_ProviderDomainPackages_SourceRowCount", "[SourceRowCount] > 0");
            });
            entity.HasKey(item => item.Version);
            entity.Property(item => item.Version).HasMaxLength(64).IsRequired();
            entity.Property(item => item.PackageSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourcePath).HasMaxLength(512).IsRequired();
            entity.Property(item => item.SourceContentSha256).HasMaxLength(64).IsRequired();
            entity.Property(item => item.SourceSheet).HasMaxLength(31).IsRequired();
            entity.HasMany(item => item.Providers)
                .WithOne(item => item.Package)
                .HasForeignKey(item => item.Version)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProviderReferenceEntity>(entity =>
        {
            entity.ToTable("ProviderReferences", table =>
                table.HasCheckConstraint("CK_ProviderReferences_SourceRow", "[SourceRow] > 0"));
            entity.HasKey(item => new { item.Version, item.Code });
            entity.Property(item => item.Version).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.HasMany(item => item.DomainEvidence)
                .WithOne(item => item.Provider)
                .HasForeignKey(item => new { item.Version, item.Code })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ProviderDomainEvidenceEntity>(entity =>
        {
            entity.ToTable("ProviderDomainEvidence");
            entity.HasKey(item => new { item.Version, item.Code, item.DomainSuffix });
            entity.Property(item => item.Version).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Code).HasMaxLength(20).IsRequired();
            entity.Property(item => item.DomainSuffix).HasMaxLength(254).IsRequired();
            entity.HasIndex(item => new { item.Version, item.DomainSuffix });
        });
        CaseWorkflowModelConfiguration.Configure(builder);
        CaseDueChaserModelConfiguration.Configure(builder);
    }
}

public sealed class PegasusIdentityUser : IdentityUser<Guid>
{
    public bool IsEnabled { get; set; } = true;

    public bool MustChangePassword { get; set; } = true;

    public bool IsSignOffEngineer { get; set; }

    public string? SignOffPrintedName { get; set; }

    public string? SignOffQualifications { get; set; }

    public byte[]? SignOffSignature { get; set; }

    public string? SignOffSignatureDigest { get; set; }

    public bool IsDefaultSignOffEngineer { get; set; }
}

internal sealed class OrganizationEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string NormalizedName { get; private set; } = string.Empty;
    public long Version { get; set; }
    public List<OrganizationRoleEntity> Roles { get; set; } = [];
    public List<PrincipalEntity> Principals { get; set; } = [];
}
internal sealed class OrganizationAdministrationOperationEntity
{
    public required string OperationKey { get; set; }
    public required string CommandKind { get; set; }
    public required string RequestHash { get; set; }
    public required string ResultJson { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
}


internal sealed class OrganizationRoleEntity
{
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = null!;
    public required string Role { get; set; }
}

internal sealed class PrincipalSequenceLineageEntity
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<PrincipalEntity> Principals { get; set; } = [];
    public List<CaseSequenceEntity> Sequences { get; set; } = [];
}

internal sealed class PrincipalEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationEntity Organization { get; set; } = null!;
    public required string Code { get; set; }
    public Guid SequenceLineageId { get; set; }
    public PrincipalSequenceLineageEntity SequenceLineage { get; set; } = null!;
    public Guid? PredecessorId { get; set; }
    public PrincipalEntity? Predecessor { get; set; }
    public Guid? SuccessorId { get; set; }
    public PrincipalEntity? Successor { get; set; }
    public bool IsActive { get; set; }
    public string InspectionMode { get; set; } = "physical_address";
    public string? DefaultInspectionLocationLabel { get; set; }
    public string? DefaultInspectionAddress { get; set; }
    public string? DefaultInspectionPostcode { get; set; }
    public string? DefaultInspectionSourceKind { get; set; }
    public string? DefaultInspectionSourceRecordId { get; set; }
    public long? DefaultInspectionSourceVersion { get; set; }
    public bool EvaManualSubmission { get; set; }
    public bool EvaAutomaticSubmission { get; set; }
    public long Version { get; set; }
    public List<CaseEntity> Cases { get; set; } = [];
}

internal sealed class CaseSequenceEntity
{
    public Guid SequenceLineageId { get; set; }
    public PrincipalSequenceLineageEntity SequenceLineage { get; set; } = null!;
    public int Year { get; set; }
    public int LastAllocatedSequence { get; set; }
}

internal interface IApplicationManagedConcurrencyToken
{
    Guid ConcurrencyToken { get; set; }
}

internal sealed class CaseEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public Guid SequenceLineageId { get; set; }
    public int Year { get; set; }
    public int Sequence { get; set; }
    public required string Reference { get; set; }
    public string? AuditReference { get; set; }
    public required string Type { get; set; }
    public required string InitialState { get; set; }
    public required string CustodyState { get; set; }
    public Guid OriginIntakeReceiptId { get; set; }
    public string? StandaloneAuditAssessment { get; set; }
    public Guid? StandaloneAuditEvidenceId { get; set; }
    public DateOnly? AcceptedInspectionDeadline { get; set; }
    public bool InstructionComplete { get; set; }
    public bool ImagesComplete { get; set; }
    public bool InstructionConfirmedByStaff { get; set; }
    public bool ImagesConfirmedByStaff { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public string? CustodyRootRemoteId { get; set; }
    public string? CustodySourceRemoteId { get; set; }
    public string? CustodySourceContentHash { get; set; }
    public string? CustodySourceETag { get; set; }
    public DateTimeOffset? CustodyConfirmedAtUtc { get; set; }
    public string? AuditCustodyRemoteId { get; set; }
    public DateTimeOffset? AuditCustodyConfirmedAtUtc { get; set; }
    public CaseEngineerFindingEntity? EngineerFinding { get; set; }
    public List<CaseIntakeLinkEntity> IntakeLinks { get; set; } = [];
    public List<CaseHistoryEntity> History { get; set; } = [];
    public List<ExternalWorkItemEntity> ExternalWork { get; set; } = [];
}

internal sealed class CaseIntakeLinkEntity
{
    public Guid IntakeReceiptId { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public Guid CustodyWorkId { get; set; }
    public ExternalWorkItemEntity CustodyWork { get; set; } = null!;
    public DateTimeOffset LinkedAtUtc { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string Reason { get; set; }
    public required string OperationKey { get; set; }
    public long? ExpectedIntakeVersion { get; set; }
    public string? AcceptanceCommandMaterialJson { get; set; }
    public string? AcceptanceCommandFingerprint { get; set; }
}
internal sealed class IntakeManualAssociationEntity
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public bool IsActive { get; set; }
    public long Version { get; set; }
    public DateTimeOffset LinkedAtUtc { get; set; }
    public DateTimeOffset? UnlinkedAtUtc { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string Reason { get; set; }
    public required string LastOperationKey { get; set; }
    public string? MatchPolicyKey { get; set; }
    public int? MatchPolicyVersion { get; set; }
}

internal sealed class IntakeMutationHistoryEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public Guid? CaseId { get; set; }
    public CaseEntity? Case { get; set; }
    public required string EventType { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string Reason { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestFingerprint { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public long ExpectedIntakeVersion { get; set; }
    public long BeforeIntakeVersion { get; set; }
    public long AfterIntakeVersion { get; set; }
    public long? ExpectedCaseVersion { get; set; }
    public long? BeforeCaseVersion { get; set; }
    public long? AfterCaseVersion { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}

internal sealed class CaseHistoryEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string EventType { get; set; }
    public required string Actor { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string OperationKey { get; set; }
    public long? BeforeVersion { get; set; }
    public long AfterVersion { get; set; }
}

internal sealed class ExternalWorkItemEntity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The owning formal Case, when the work is case-scoped. Image-case
    /// custody creation has no formal Case yet and carries only
    /// <see cref="ImageIntakeId"/>; the image-case merge carries both.
    /// </summary>
    public Guid? CaseId { get; set; }
    public CaseEntity? Case { get; set; }
    public Guid? ImageIntakeId { get; set; }
    public ImageIntakeEntity? ImageIntake { get; set; }
    public required string Kind { get; set; }
    public required string OperationKey { get; set; }
    public required string State { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public string? ExternalReceipt { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public string? CaseRootCreationToken { get; set; }
    public string? AuditFolderCreationToken { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

internal sealed class TriageEntity : IApplicationManagedConcurrencyToken
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid? PrincipalId { get; set; }
    public Guid OriginReceiptId { get; set; }
    public required string SourceChannel { get; set; }
    public required string ExternalReceiptToken { get; set; }
    public required string SourceHash { get; set; }
    public Guid EvaluationRevisionId { get; set; }
    public required string NormalizedVehicleRegistration { get; set; }
    public required string State { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? LinkedCaseId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreationOperationKey { get; set; }
    public long Version { get; set; }
    public Guid ConcurrencyToken { get; set; }
    public List<TriageFindingEntity> Findings { get; set; } = [];
    public List<TriageResponseEvidenceLinkEntity> ResponseEvidenceLinks { get; set; } = [];
    public List<TriageHistoryEntity> History { get; set; } = [];
    public List<SentEmailEvidenceEntity> SentEmailEvidence { get; set; } = [];
}

internal sealed class TriageFindingEntity
{
    public Guid Id { get; set; }
    public Guid TriageId { get; set; }
    public TriageEntity Triage { get; set; } = null!;
    public string? Roadworthiness { get; set; }
    public string? Assessment { get; set; }
    public Guid? SupersedesFindingId { get; set; }
    public required string Actor { get; set; }
    public required string OperationKey { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
}

internal sealed class SentEmailEvidenceEntity
{
    public Guid Id { get; set; }
    public Guid TriageId { get; set; }
    public TriageEntity Triage { get; set; } = null!;
    public required string MessageIdentity { get; set; }
    public required string Subject { get; set; }
    public required string RecipientsJson { get; set; }
    public required string MimeSha256 { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
    public DateTimeOffset ChaseDueAtUtc { get; set; }
    public required string Actor { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public long Version { get; set; }
    public EmailResponseEvidenceEntity? Response { get; set; }
    public List<TriageResponseEvidenceLinkEntity> TriageLinks { get; set; } = [];
}

internal sealed class EmailResponseEvidenceEntity
{
    public Guid Id { get; set; }
    public Guid SentEvidenceId { get; set; }
    public SentEmailEvidenceEntity SentEvidence { get; set; } = null!;
    public Guid PollOutcomeId { get; set; }
    public required string MailboxId { get; set; }
    public required string MailboxAddress { get; set; }
    public required string SentFolderIdentity { get; set; }
    public required string ImmutableItemIdentity { get; set; }
    public required string MessageIdentity { get; set; }
    public required string ConversationIdentity { get; set; }
    public required string ReplyChainIdentity { get; set; }
    public required string InReplyToIdentitiesJson { get; set; }
    public required string SourceOccurrenceIdentity { get; set; }
    public required string SourceSha256 { get; set; }
    public required string MimeSha256 { get; set; }
    public DateTimeOffset SentAtUtc { get; set; }
    public DateTimeOffset DiscoveredAtUtc { get; set; }
    public required string Actor { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
}

internal sealed class TriageResponseEvidenceLinkEntity
{
    public Guid TriageId { get; set; }
    public TriageEntity Triage { get; set; } = null!;
    public Guid SentEvidenceId { get; set; }
    public SentEmailEvidenceEntity SentEvidence { get; set; } = null!;
    public required string Actor { get; set; }
    public required string OperationKey { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset LinkedAtUtc { get; set; }
}

internal sealed class TriageHistoryEntity
{
    public Guid Id { get; set; }
    public Guid TriageId { get; set; }
    public TriageEntity Triage { get; set; } = null!;
    public required string EventType { get; set; }
    public required string Actor { get; set; }
    public required string ActorKind { get; set; }
    public required string Reason { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public long BeforeVersion { get; set; }
    public long AfterVersion { get; set; }
    public required string AfterState { get; set; }
    public Guid? AfterAssigneeId { get; set; }
    public Guid? AfterLinkedCaseId { get; set; }
}

internal sealed class ActionHistoryEntity
{
    public Guid Id { get; set; }
    public required string AggregateType { get; set; }
    public required string AggregateId { get; set; }
    public required string EventKind { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string Outcome { get; set; }
    public required string CorrelationId { get; set; }
    public string? Reason { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? PolicyVersion { get; set; }
}

internal sealed class SecurityEventEntity
{
    public Guid Id { get; set; }
    public required string Type { get; set; }
    public required string Outcome { get; set; }
    public required string SubjectId { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string CorrelationId { get; set; }
    public string? ReasonCode { get; set; }
}


internal sealed class IntakeReceiptEntity
{
    public Guid Id { get; set; }
    public required string SourceFileName { get; set; }
    public required string MediaType { get; set; }
    public long SourceLength { get; set; }
    public required string SourceHash { get; set; }
    public required string SourceChannel { get; set; }
    public required string ExternalReceiptToken { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public DateTimeOffset ProcessedAtUtc { get; set; }
    public required string SourceReaderKey { get; set; }
    public required string SourceReaderVersion { get; set; }
    public string? ExtractionPolicyKey { get; set; }
    public long Version { get; set; }
    public int? ExtractionPolicyVersion { get; set; }
    public required string Decision { get; set; }
    public required string DecisionReason { get; set; }
    public required string EvidenceJson { get; set; }
    public required string FieldsJson { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public required string OcrCandidatesJson { get; set; }
    public InstructionDraftEntity? InstructionDraft { get; set; }
    public IntakeMailRouteDecisionEntity? MailRouteDecision { get; set; }
    public IntakeMailClassificationDecisionEntity? MailClassificationDecision { get; set; }
    public IntakeCaseMatchDecisionEntity? CaseMatchDecision { get; set; }
    public IntakeManualAssociationEntity? ManualAssociation { get; set; }
    public List<IntakeAssetEntity> Assets { get; set; } = [];
    public List<IntakeSearchDocumentEntity> SearchDocuments { get; set; } = [];
}

internal sealed class IntakeSearchDocumentEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public int Ordinal { get; set; }
    public int? AttachmentOrdinal { get; set; }
    public required string SourceLabel { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? Text { get; set; }
}

internal sealed class InstructionDraftEntity
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public string? SuggestedPrincipalCode { get; set; }
    public string? ClaimantName { get; set; }
    public string? ClaimNumber { get; set; }
    public string? VehicleRegistration { get; set; }
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public long? VehicleMileage { get; set; }
    public string? AccidentCircumstances { get; set; }
    public DateOnly? DateOfIncident { get; set; }
    public DateOnly? InstructionDate { get; set; }
    public DateOnly? InspectionDate { get; set; }
    public string? InspectionAddress { get; set; }

    // Declared-instruction fields (API-01). No extraction policy reads these
    // today; a provider that states its instruction over the API supplies them
    // directly.
    public string? VehicleMileageUnit { get; set; }
    public string? VatStatus { get; set; }
    public string? ClaimantAddress { get; set; }
    public string? ClaimantContactNumber { get; set; }
    public string? FileHandlerName { get; set; }
    public string? FileHandlerEmailAddress { get; set; }
    public string? FileHandlerPhoneNumber { get; set; }
    public string? Notes { get; set; }
}

internal sealed class IntakeAssetEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string SourceLabel { get; set; }
    public required string FileName { get; set; }
    public required string MediaType { get; set; }
    public required string Kind { get; set; }
    public required string Disposition { get; set; }
    public long ContentLength { get; set; }
    public required string ContentHash { get; set; }
    public required string StorageKey { get; set; }
    public string? BoxFileId { get; set; }
    public string? BoxVersionId { get; set; }
    public string? CustodyStatus { get; set; }
    public int? PageNumber { get; set; }
    public string? BoundsJson { get; set; }
    public int? WidthPixels { get; set; }
    public int? HeightPixels { get; set; }
}

internal sealed class IntakeReceiptEventEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public required string EventType { get; set; }
    public required string Actor { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string DetailsJson { get; set; }
}

internal sealed class IntakeStagedReceiptEntity
{
    public Guid Id { get; set; }
    public required string SourceFileName { get; set; }
    public required string MediaType { get; set; }
    public long SourceLength { get; set; }
    public required string SourceHash { get; set; }
    public required string SourceChannel { get; set; }
    public required string ExternalReceiptToken { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public required string Actor { get; set; }
    public required string StorageKey { get; set; }
    public DateTimeOffset StagedAtUtc { get; set; }
    public IntakeWorkItemEntity? WorkItem { get; set; }
}

internal sealed class IntakeSubmissionGroupEntity
{
    public Guid Id { get; set; }
    public required string SourceChannel { get; set; }
    public required string SubmissionToken { get; set; }
    public int ExpectedMemberCount { get; set; }
    public required string Actor { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public Guid? ParentReceiptId { get; set; }
    public List<IntakeSubmissionGroupMemberEntity> Members { get; set; } = [];
}

internal sealed class IntakeSubmissionGroupMemberEntity
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public IntakeSubmissionGroupEntity Group { get; set; } = null!;
    public int Ordinal { get; set; }
    public Guid StagedReceiptId { get; set; }
    public required string SourceFileName { get; set; }
    public required string SourceHash { get; set; }
    public DateTimeOffset AddedAtUtc { get; set; }
}

internal sealed class IntakeWorkItemEntity
{
    public Guid Id { get; set; }
    public Guid StagedReceiptId { get; set; }
    public IntakeStagedReceiptEntity StagedReceipt { get; set; } = null!;
    public required string OperationKey { get; set; }
    public required string State { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public Guid? ProcessedReceiptId { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}

internal sealed class IntakeEvaluationEntity
{
    public Guid Id { get; set; }
    public Guid StagedReceiptId { get; set; }
    public Guid ProcessedReceiptId { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset EvaluatedAtUtc { get; set; }
}

internal sealed class ProviderDomainPackageEntity
{
    public required string Version { get; set; }
    public int SchemaVersion { get; set; }
    public required string PackageSha256 { get; set; }
    public required string SourcePath { get; set; }
    public required string SourceContentSha256 { get; set; }
    public required string SourceSheet { get; set; }
    public int SourceRowCount { get; set; }
    public List<ProviderReferenceEntity> Providers { get; set; } = [];
}

internal sealed class ProviderReferenceEntity
{
    public required string Version { get; set; }
    public required string Code { get; set; }
    public int SourceRow { get; set; }
    public ProviderDomainPackageEntity Package { get; set; } = null!;
    public List<ProviderDomainEvidenceEntity> DomainEvidence { get; set; } = [];
}

internal sealed class ProviderDomainEvidenceEntity
{
    public required string Version { get; set; }
    public required string Code { get; set; }
    public required string DomainSuffix { get; set; }
    public ProviderReferenceEntity Provider { get; set; } = null!;
}
