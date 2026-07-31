using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal static class VehicleModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<VehicleLookupRequestEntity>(entity =>
        {
            entity.ToTable("VehicleLookupRequests", table =>
                table.HasCheckConstraint(
                    "CK_VehicleLookupRequests_ResultingCaseVersion",
                    "[ResultingCaseVersion] >= 0"));
            entity.HasKey(item => item.WorkItemId);
            entity.Property(item => item.Registration).HasMaxLength(20).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.RequestedByKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.RequestedBySubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.RequestedByRolesJson).HasMaxLength(500).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.OperationKey }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.RequestedAtUtc });
            entity.HasOne(item => item.WorkItem)
                .WithOne()
                .HasForeignKey<VehicleLookupRequestEntity>(item => item.WorkItemId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VehicleLookupObservationEntity>(entity =>
        {
            entity.ToTable("VehicleLookupObservations", table =>
            {
                table.HasCheckConstraint(
                    "CK_VehicleLookupObservations_AttemptNumber",
                    "[AttemptNumber] >= 1");
                table.HasCheckConstraint(
                    "CK_VehicleLookupObservations_Mileage",
                    "([MileageValue] IS NULL AND [MileageUnit] IS NULL AND [MileageObservedOn] IS NULL AND [MileageMethodKey] IS NULL AND [MileageMethodVersion] IS NULL AND [MileageSupportingObservationCount] IS NULL) OR " +
                    "([MileageValue] >= 0 AND [MileageUnit] IS NOT NULL AND [MileageObservedOn] IS NOT NULL AND [MileageMethodKey] IS NOT NULL AND [MileageMethodVersion] > 0 AND [MileageSupportingObservationCount] > 0)");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Outcome).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Registration).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Provider).HasMaxLength(100).IsRequired();
            entity.Property(item => item.ProviderVersion).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ResponseIdentity).HasMaxLength(500).IsRequired();
            entity.Property(item => item.Make).HasMaxLength(100);
            entity.Property(item => item.Model).HasMaxLength(100);
            entity.Property(item => item.FuelType).HasMaxLength(100);
            entity.Property(item => item.MotTestsJson).IsRequired();
            entity.Property(item => item.MileageUnit).HasMaxLength(40);
            entity.Property(item => item.MileageObservedOn).HasColumnType("date");
            entity.Property(item => item.MileageMethodKey).HasMaxLength(100);
            entity.Property(item => item.FailureCode).HasMaxLength(100);
            entity.HasIndex(item => new { item.WorkItemId, item.AttemptNumber }).IsUnique();
            entity.HasIndex(item => new { item.Provider, item.ProviderVersion, item.ResponseIdentity });
            entity.HasOne(item => item.Request)
                .WithMany(item => item.Observations)
                .HasForeignKey(item => item.WorkItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<VehicleConfirmationEntity>(entity =>
        {
            entity.ToTable("VehicleConfirmations", table =>
            {
                table.HasCheckConstraint(
                    "CK_VehicleConfirmations_CaseVersions",
                    "[BeforeCaseVersion] >= 0 AND [AfterCaseVersion] > [BeforeCaseVersion]");
                table.HasCheckConstraint(
                    "CK_VehicleConfirmations_Mileage",
                    "([Mileage] IS NULL AND [MileageUnit] IS NULL) OR ([Mileage] >= 0 AND [MileageUnit] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_VehicleConfirmations_PolicyVersion",
                    "[PolicyVersion] > 0");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Decision).HasMaxLength(40).IsRequired();
            entity.Property(item => item.Registration).HasMaxLength(20).IsRequired();
            entity.Property(item => item.Make).HasMaxLength(100);
            entity.Property(item => item.Model).HasMaxLength(100);
            entity.Property(item => item.MileageUnit).HasMaxLength(40);
            entity.Property(item => item.ActorKind).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorSubjectId).HasMaxLength(200).IsRequired();
            entity.Property(item => item.ActorRolesJson).HasMaxLength(500).IsRequired();
            entity.Property(item => item.OperationKey).HasMaxLength(100).IsRequired();
            entity.Property(item => item.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(item => item.Reason).HasMaxLength(500).IsRequired();
            entity.Property(item => item.PolicyKey).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.CaseId, item.OperationKey }).IsUnique();
            entity.HasIndex(item => new { item.CaseId, item.AfterCaseVersion }).IsUnique();
            entity.HasOne(item => item.Case)
                .WithMany()
                .HasForeignKey(item => item.CaseId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.LookupObservation)
                .WithMany(item => item.Confirmations)
                .HasForeignKey(item => item.LookupObservationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
