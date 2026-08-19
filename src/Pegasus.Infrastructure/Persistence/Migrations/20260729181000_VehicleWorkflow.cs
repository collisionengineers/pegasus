using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729181000_VehicleWorkflow")]
public sealed class VehicleWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // no-runtime-grant: VehicleLookupRequests - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: VehicleLookupObservations - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: VehicleConfirmations - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        migrationBuilder.CreateTable(
            name: "VehicleLookupRequests",
            columns: table => new
            {
                WorkItemId = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                Registration = table.Column<string>(maxLength: 20, nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestFingerprint = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                RequestedByKind = table.Column<string>(maxLength: 40, nullable: false),
                RequestedBySubjectId = table.Column<string>(maxLength: 200, nullable: false),
                RequestedByRolesJson = table.Column<string>(maxLength: 500, nullable: false),
                RequestedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                ResultingCaseVersion = table.Column<long>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleLookupRequests", item => item.WorkItemId);
                table.CheckConstraint(
                    "CK_VehicleLookupRequests_ResultingCaseVersion",
                    "[ResultingCaseVersion] >= 0");
                table.ForeignKey(
                    name: "FK_VehicleLookupRequests_Cases_CaseId",
                    column: item => item.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleLookupRequests_ExternalWorkItems_WorkItemId",
                    column: item => item.WorkItemId,
                    principalTable: "ExternalWorkItems",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VehicleLookupObservations",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                WorkItemId = table.Column<Guid>(nullable: false),
                AttemptNumber = table.Column<int>(nullable: false),
                Outcome = table.Column<string>(maxLength: 40, nullable: false),
                Registration = table.Column<string>(maxLength: 20, nullable: false),
                Provider = table.Column<string>(maxLength: 100, nullable: false),
                ProviderVersion = table.Column<string>(maxLength: 200, nullable: false),
                ResponseIdentity = table.Column<string>(maxLength: 500, nullable: false),
                RetrievedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                EffectiveAtUtc = table.Column<DateTimeOffset>(nullable: true),
                SourceObservedAtUtc = table.Column<DateTimeOffset>(nullable: true),
                Make = table.Column<string>(maxLength: 100, nullable: true),
                Model = table.Column<string>(maxLength: 100, nullable: true),
                ManufactureYear = table.Column<int>(nullable: true),
                EngineCapacityCc = table.Column<int>(nullable: true),
                FuelType = table.Column<string>(maxLength: 100, nullable: true),
                MotTestsJson = table.Column<string>(nullable: false),
                MileageValue = table.Column<long>(nullable: true),
                MileageUnit = table.Column<string>(maxLength: 40, nullable: true),
                MileageObservedOn = table.Column<DateOnly>(type: "date", nullable: true),
                MileageMethodKey = table.Column<string>(maxLength: 100, nullable: true),
                MileageMethodVersion = table.Column<int>(nullable: true),
                MileageSupportingObservationCount = table.Column<int>(nullable: true),
                FailureCode = table.Column<string>(maxLength: 100, nullable: true),
                FailureRetryable = table.Column<bool>(nullable: true),
                FailureRetryAfterTicks = table.Column<long>(nullable: true),
                RecordedAtUtc = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleLookupObservations", item => item.Id);
                table.CheckConstraint(
                    "CK_VehicleLookupObservations_AttemptNumber",
                    "[AttemptNumber] >= 1");
                table.CheckConstraint(
                    "CK_VehicleLookupObservations_Mileage",
                    "([MileageValue] IS NULL AND [MileageUnit] IS NULL AND [MileageObservedOn] IS NULL AND [MileageMethodKey] IS NULL AND [MileageMethodVersion] IS NULL AND [MileageSupportingObservationCount] IS NULL) OR " +
                    "([MileageValue] >= 0 AND [MileageUnit] IS NOT NULL AND [MileageObservedOn] IS NOT NULL AND [MileageMethodKey] IS NOT NULL AND [MileageMethodVersion] > 0 AND [MileageSupportingObservationCount] > 0)");
                table.ForeignKey(
                    name: "FK_VehicleLookupObservations_VehicleLookupRequests_WorkItemId",
                    column: item => item.WorkItemId,
                    principalTable: "VehicleLookupRequests",
                    principalColumn: "WorkItemId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "VehicleConfirmations",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CaseId = table.Column<Guid>(nullable: false),
                LookupObservationId = table.Column<Guid>(nullable: false),
                Decision = table.Column<string>(maxLength: 40, nullable: false),
                Registration = table.Column<string>(maxLength: 20, nullable: false),
                Make = table.Column<string>(maxLength: 100, nullable: true),
                Model = table.Column<string>(maxLength: 100, nullable: true),
                Mileage = table.Column<long>(nullable: true),
                MileageUnit = table.Column<string>(maxLength: 40, nullable: true),
                ActorKind = table.Column<string>(maxLength: 40, nullable: false),
                ActorSubjectId = table.Column<string>(maxLength: 200, nullable: false),
                ActorRolesJson = table.Column<string>(maxLength: 500, nullable: false),
                OperationKey = table.Column<string>(maxLength: 100, nullable: false),
                RequestFingerprint = table.Column<string>(fixedLength: true, maxLength: 64, nullable: false),
                Reason = table.Column<string>(maxLength: 500, nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(nullable: false),
                BeforeCaseVersion = table.Column<long>(nullable: false),
                AfterCaseVersion = table.Column<long>(nullable: false),
                PolicyKey = table.Column<string>(maxLength: 100, nullable: false),
                PolicyVersion = table.Column<int>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleConfirmations", item => item.Id);
                table.CheckConstraint(
                    "CK_VehicleConfirmations_CaseVersions",
                    "[BeforeCaseVersion] >= 0 AND [AfterCaseVersion] > [BeforeCaseVersion]");
                table.CheckConstraint(
                    "CK_VehicleConfirmations_Mileage",
                    "([Mileage] IS NULL AND [MileageUnit] IS NULL) OR ([Mileage] >= 0 AND [MileageUnit] IS NOT NULL)");
                table.CheckConstraint(
                    "CK_VehicleConfirmations_PolicyVersion",
                    "[PolicyVersion] > 0");
                table.ForeignKey(
                    name: "FK_VehicleConfirmations_Cases_CaseId",
                    column: item => item.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_VehicleConfirmations_VehicleLookupObservations_LookupObservationId",
                    column: item => item.LookupObservationId,
                    principalTable: "VehicleLookupObservations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleLookupRequests_CaseId_OperationKey",
            table: "VehicleLookupRequests",
            columns: new[] { "CaseId", "OperationKey" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VehicleLookupRequests_CaseId_RequestedAtUtc",
            table: "VehicleLookupRequests",
            columns: new[] { "CaseId", "RequestedAtUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_VehicleLookupObservations_Provider_ProviderVersion_ResponseIdentity",
            table: "VehicleLookupObservations",
            columns: new[] { "Provider", "ProviderVersion", "ResponseIdentity" });
        migrationBuilder.CreateIndex(
            name: "IX_VehicleLookupObservations_WorkItemId_AttemptNumber",
            table: "VehicleLookupObservations",
            columns: new[] { "WorkItemId", "AttemptNumber" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VehicleConfirmations_CaseId_AfterCaseVersion",
            table: "VehicleConfirmations",
            columns: new[] { "CaseId", "AfterCaseVersion" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VehicleConfirmations_CaseId_OperationKey",
            table: "VehicleConfirmations",
            columns: new[] { "CaseId", "OperationKey" },
            unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_VehicleConfirmations_LookupObservationId",
            table: "VehicleConfirmations",
            column: "LookupObservationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VehicleConfirmations");
        migrationBuilder.DropTable(name: "VehicleLookupObservations");
        migrationBuilder.DropTable(name: "VehicleLookupRequests");
    }
}
