using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260729185000_TypedCaseDataCompleteness")]
public partial class TypedCaseDataCompleteness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // no-runtime-grant: CaseDataSnapshots - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        // no-runtime-grant: CaseDataFields - granted via the consolidated 20260729199000_RuntimeRoleReconciliation least-privilege migration
        migrationBuilder.CreateTable(
            name: "CaseDataSnapshots",
            columns: table => new
            {
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginIntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OriginSourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                OriginExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                OriginSourceHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                OriginReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                SourceReaderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SourceReaderVersion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ExtractionPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ExtractionPolicyVersion = table.Column<int>(type: "int", nullable: true),
                CompletenessPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CompletenessPolicyVersion = table.Column<int>(type: "int", nullable: false),
                CompletenessPolicySatisfied = table.Column<bool>(type: "bit", nullable: false),
                AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CaseDataSnapshots", item => item.CaseId);
                table.CheckConstraint(
                    "CK_CaseDataSnapshots_CompletenessPolicyVersion",
                    "[CompletenessPolicyVersion] > 0");
                table.CheckConstraint(
                    "CK_CaseDataSnapshots_ExtractionPolicyVersion",
                    "[ExtractionPolicyVersion] IS NULL OR [ExtractionPolicyVersion] > 0");
                table.ForeignKey(
                    name: "FK_CaseDataSnapshots_Cases_CaseId",
                    column: item => item.CaseId,
                    principalTable: "Cases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CaseDataSnapshots_IntakeReceipts_OriginIntakeReceiptId",
                    column: item => item.OriginIntakeReceiptId,
                    principalTable: "IntakeReceipts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CaseDataFields",
            columns: table => new
            {
                CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FieldName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                ValueKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ValueType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                SourceKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                SourceIdentity = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PolicyVersion = table.Column<int>(type: "int", nullable: false),
                ConfirmedByActor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_CaseDataFields",
                    item => new { item.CaseId, item.FieldName, item.ValueKind });
                table.CheckConstraint(
                    "CK_CaseDataFields_Confirmation",
                    "([ValueKind] = 'confirmed' AND [ConfirmedByActor] IS NOT NULL AND [ConfirmedAtUtc] IS NOT NULL) OR ([ValueKind] <> 'confirmed' AND [ConfirmedByActor] IS NULL AND [ConfirmedAtUtc] IS NULL)");
                table.CheckConstraint(
                    "CK_CaseDataFields_FieldName",
                    "[FieldName] IN ('work_provider_code', 'claimant_name', 'claim_number', 'vehicle_registration', 'vehicle_make', 'vehicle_model', 'vehicle_mileage', 'vehicle_mileage_unit', 'accident_circumstances', 'incident_date', 'contact_name', 'contact_email_address', 'contact_phone_number', 'instruction_date', 'vat_status', 'inspection_date', 'inspection_deadline', 'inspection_address', 'inspection_mode')");
                table.CheckConstraint(
                    "CK_CaseDataFields_PolicyVersion",
                    "[PolicyVersion] > 0");
                table.CheckConstraint(
                    "CK_CaseDataFields_SourceKind",
                    "[SourceKind] IN ('intake_evidence', 'mail_route', 'case_acceptance', 'staff_correction', 'vehicle_lookup')");
                table.CheckConstraint(
                    "CK_CaseDataFields_ValueKind",
                    "[ValueKind] IN ('fact', 'suggestion', 'confirmed')");
                table.CheckConstraint(
                    "CK_CaseDataFields_ValueType",
                    "[ValueType] IN ('text', 'integer', 'date', 'inspection_mode')");
                table.ForeignKey(
                    name: "FK_CaseDataFields_CaseDataSnapshots_CaseId",
                    column: item => item.CaseId,
                    principalTable: "CaseDataSnapshots",
                    principalColumn: "CaseId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CaseDataSnapshots_OriginIntakeReceiptId",
            table: "CaseDataSnapshots",
            column: "OriginIntakeReceiptId");
        migrationBuilder.CreateIndex(
            name: "IX_CaseDataFields_FieldName_ValueKind",
            table: "CaseDataFields",
            columns: new[] { "FieldName", "ValueKind" });

        migrationBuilder.Sql(
            """
            INSERT INTO CaseDataSnapshots
                (CaseId, OriginIntakeReceiptId, OriginSourceChannel,
                 OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc,
                 SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey,
                 ExtractionPolicyVersion, CompletenessPolicyKey,
                 CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc)
            SELECT cases.Id, receipts.Id, receipts.SourceChannel,
                   receipts.ExternalReceiptToken, receipts.SourceHash, receipts.ReceivedAtUtc,
                   receipts.SourceReaderKey, receipts.SourceReaderVersion,
                   receipts.ExtractionPolicyKey, receipts.ExtractionPolicyVersion,
                   'case-workflow', 1,
                   CASE WHEN cases.InstructionComplete = 1
                                  AND cases.ImagesComplete = 1
                                  AND cases.InstructionConfirmedByStaff = 1
                                  AND cases.ImagesConfirmedByStaff = 1
                        THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                   cases.CreatedAtUtc
            FROM Cases AS cases
            INNER JOIN IntakeReceipts AS receipts
                ON receipts.Id = cases.OriginIntakeReceiptId;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO CaseDataFields
                (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind,
                 SourceIdentity, SourceLabel, PolicyKey, PolicyVersion,
                 ConfirmedByActor, ConfirmedAtUtc)
            SELECT cases.Id, 'work_provider_code', 'fact', 'text', routes.WorkProviderCode,
                   'mail_route', CONVERT(nvarchar(36), receipts.Id),
                   'migrated accepted mail route', routes.PolicyKey, routes.PolicyVersion,
                   NULL, NULL
            FROM Cases AS cases
            INNER JOIN IntakeReceipts AS receipts
                ON receipts.Id = cases.OriginIntakeReceiptId
            INNER JOIN IntakeMailRouteDecisions AS routes
                ON routes.IntakeReceiptId = receipts.Id
            WHERE routes.Disposition = 'accepted'
              AND NULLIF(LTRIM(RTRIM(routes.WorkProviderCode)), '') IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO CaseDataFields
                (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind,
                 SourceIdentity, SourceLabel, PolicyKey, PolicyVersion,
                 ConfirmedByActor, ConfirmedAtUtc)
            SELECT cases.Id, valueset.FieldName, 'suggestion', valueset.ValueType,
                   valueset.Value, 'intake_evidence', CONVERT(nvarchar(36), receipts.Id),
                   'migrated accepted intake snapshot',
                   COALESCE(receipts.ExtractionPolicyKey, 'legacy-accepted-intake'),
                   COALESCE(receipts.ExtractionPolicyVersion, 1), NULL, NULL
            FROM Cases AS cases
            INNER JOIN IntakeReceipts AS receipts
                ON receipts.Id = cases.OriginIntakeReceiptId
            INNER JOIN InstructionDrafts AS draft
                ON draft.IntakeReceiptId = receipts.Id
            CROSS APPLY (VALUES
                ('claimant_name', 'text', draft.ClaimantName),
                ('claim_number', 'text', draft.ClaimNumber),
                ('vehicle_registration', 'text', draft.VehicleRegistration),
                ('vehicle_make', 'text', draft.VehicleMake),
                ('vehicle_model', 'text', draft.VehicleModel),
                ('vehicle_mileage', 'integer', CONVERT(nvarchar(40), draft.VehicleMileage)),
                ('accident_circumstances', 'text', draft.AccidentCircumstances),
                ('incident_date', 'date', CONVERT(nvarchar(10), draft.DateOfIncident, 23)),
                ('instruction_date', 'date', CONVERT(nvarchar(10), draft.InstructionDate, 23)),
                ('inspection_date', 'date', CONVERT(nvarchar(10), draft.InspectionDate, 23)),
                ('inspection_address', 'text', draft.InspectionAddress),
                ('inspection_mode', 'inspection_mode',
                    CASE WHEN draft.InspectionAddress = 'Image Based Assessment'
                         THEN 'image_based_assessment'
                         WHEN draft.InspectionAddress IS NOT NULL THEN 'physical_address'
                         ELSE NULL END)
            ) AS valueset(FieldName, ValueType, Value)
            WHERE NULLIF(LTRIM(RTRIM(valueset.Value)), '') IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO CaseDataFields
                (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind,
                 SourceIdentity, SourceLabel, PolicyKey, PolicyVersion,
                 ConfirmedByActor, ConfirmedAtUtc)
            SELECT cases.Id, valueset.FieldName, 'confirmed', valueset.ValueType,
                   valueset.Value, 'intake_evidence', CONVERT(nvarchar(36), receipts.Id),
                   'migrated explicitly confirmed accepted instruction',
                   COALESCE(receipts.ExtractionPolicyKey, 'legacy-accepted-migration'),
                   COALESCE(receipts.ExtractionPolicyVersion, 1),
                   links.Actor, links.LinkedAtUtc
            FROM Cases AS cases
            INNER JOIN IntakeReceipts AS receipts
                ON receipts.Id = cases.OriginIntakeReceiptId
            INNER JOIN InstructionDrafts AS draft
                ON draft.IntakeReceiptId = receipts.Id
            INNER JOIN CaseIntakeLinks AS links
                ON links.CaseId = cases.Id
               AND links.IntakeReceiptId = receipts.Id
            CROSS APPLY (VALUES
                ('claimant_name', 'text', draft.ClaimantName),
                ('claim_number', 'text', draft.ClaimNumber),
                ('vehicle_registration', 'text', draft.VehicleRegistration),
                ('vehicle_make', 'text', draft.VehicleMake),
                ('vehicle_model', 'text', draft.VehicleModel),
                ('vehicle_mileage', 'integer', CONVERT(nvarchar(40), draft.VehicleMileage)),
                ('accident_circumstances', 'text', draft.AccidentCircumstances),
                ('incident_date', 'date', CONVERT(nvarchar(10), draft.DateOfIncident, 23)),
                ('instruction_date', 'date', CONVERT(nvarchar(10), draft.InstructionDate, 23)),
                ('inspection_date', 'date', CONVERT(nvarchar(10), draft.InspectionDate, 23)),
                ('inspection_address', 'text', draft.InspectionAddress),
                ('inspection_mode', 'inspection_mode',
                    CASE WHEN draft.InspectionAddress = 'Image Based Assessment'
                         THEN 'image_based_assessment'
                         WHEN draft.InspectionAddress IS NOT NULL THEN 'physical_address'
                         ELSE NULL END)
            ) AS valueset(FieldName, ValueType, Value)
            WHERE cases.InstructionConfirmedByStaff = 1
              AND NULLIF(LTRIM(RTRIM(valueset.Value)), '') IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            INSERT INTO CaseDataFields
                (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind,
                 SourceIdentity, SourceLabel, PolicyKey, PolicyVersion,
                 ConfirmedByActor, ConfirmedAtUtc)
            SELECT cases.Id, 'inspection_deadline', 'confirmed', 'date',
                   CONVERT(nvarchar(10), cases.AcceptedInspectionDeadline, 23),
                   'case_acceptance', CONVERT(nvarchar(36), receipts.Id),
                   'migrated accepted inspection deadline',
                   COALESCE(receipts.ExtractionPolicyKey, 'legacy-accepted-intake'),
                   COALESCE(receipts.ExtractionPolicyVersion, 1), links.Actor, links.LinkedAtUtc
            FROM Cases AS cases
            INNER JOIN IntakeReceipts AS receipts
                ON receipts.Id = cases.OriginIntakeReceiptId
            INNER JOIN CaseIntakeLinks AS links
                ON links.CaseId = cases.Id
               AND links.IntakeReceiptId = receipts.Id
            WHERE cases.AcceptedInspectionDeadline IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CaseDataFields");
        migrationBuilder.DropTable(name: "CaseDataSnapshots");
    }
}
