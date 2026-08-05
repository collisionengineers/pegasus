using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SendToAiAssessmentToolset : Migration
    {
        private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
        private const string WebRole = "pegasus_web_runtime_role";
        private const string WorkerRole = "pegasus_worker_runtime_role";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiWorkRequests",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CaseVersionAtSend = table.Column<long>(type: "bigint", nullable: false),
                    CapabilityScope = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HandedOffAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReplyStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReplyMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkRequests", x => x.RequestId);
                    table.CheckConstraint("CK_AiWorkRequests_CaseVersion", "[CaseVersionAtSend] >= 0");
                    table.CheckConstraint("CK_AiWorkRequests_State", "[State] IN ('Created', 'HandedOff', 'Completed', 'Failed', 'Cancelled', 'Expired')");
                    table.ForeignKey(
                        name: "FK_AiWorkRequests_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseAssessmentFields",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldPath = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RecordedByKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseAssessmentFields", x => new { x.CaseId, x.FieldPath });
                    table.CheckConstraint("CK_CaseAssessmentFields_Confirmation", "([ConfirmedBy] IS NULL AND [ConfirmedAtUtc] IS NULL) OR ([ConfirmedBy] IS NOT NULL AND [ConfirmedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_CaseAssessmentFields_FieldPath", "[FieldPath] IN ('assessment.category', 'assessment.impact_location', 'assessment.impact_severity', 'assessment.legal_status', 'assessment.outcome', 'assessment.salvage_value', 'assessment.unroadworthy_reason', 'assessment.values.engineer', 'assessment.values.retail', 'assessment.values.trade', 'costs.recovery_charge', 'costs.repairer_vat_registered', 'costs.storage_charge', 'engineer.name', 'engineer.qualifications', 'engineer.signature', 'fee.agreed_fee', 'fee.description_lines', 'incident.assessed', 'narrative.engineers_comments', 'narrative.history_check', 'narrative.nature_of_incident', 'rates.card', 'rates.class', 'rates.manufacturer_approved', 'rates.regional_uplift', 'statement_of_truth', 'vehicle.condition', 'vehicle.engine_cc', 'vehicle.fuel', 'vehicle.mileage_source', 'vehicle.vehicle_type', 'vehicle.vin', 'vehicle.year')");
                    table.CheckConstraint("CK_CaseAssessmentFields_RecordedByKind", "[RecordedByKind] IN ('Staff', 'Automation')");
                    table.ForeignKey(
                        name: "FK_CaseAssessmentFields_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseEstimateLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    LineType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    GuideCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WorkUnits = table.Column<decimal>(type: "decimal(9,1)", precision: 9, scale: 1, nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Unpriced = table.Column<bool>(type: "bit", nullable: false),
                    PartNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Betterment = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    EvidenceLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RecordedByKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEstimateLines", x => x.Id);
                    table.CheckConstraint("CK_CaseEstimateLines_EvidenceLabel", "[EvidenceLabel] IS NULL OR [EvidenceLabel] IN ('official', 'reference', 'case', 'judgement')");
                    table.CheckConstraint("CK_CaseEstimateLines_LineType", "[LineType] IN ('rnr', 'repair', 'new_part', 'check_labour', 'paint_new', 'paint_repair', 'paint_blend', 'paint_prep', 'specialist_fixed', 'specialist_wu')");
                    table.CheckConstraint("CK_CaseEstimateLines_Position", "[Position] > 0");
                    table.CheckConstraint("CK_CaseEstimateLines_Status", "[Status] IS NULL OR [Status] IN ('confirmed', 'estimated', 'provisional')");
                    table.CheckConstraint("CK_CaseEstimateLines_Unpriced", "[Unpriced] = 0 OR [Price] IS NULL");
                    table.ForeignKey(
                        name: "FK_CaseEstimateLines_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SendToAiControl",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SendToAiControl", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkRequests_CaseId_CreatedAtUtc",
                table: "AiWorkRequests",
                columns: new[] { "CaseId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkRequests_CaseId_OperationKey",
                table: "AiWorkRequests",
                columns: new[] { "CaseId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseAssessmentFields_FieldPath",
                table: "CaseAssessmentFields",
                column: "FieldPath");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEstimateLines_CaseId_Position",
                table: "CaseEstimateLines",
                columns: new[] { "CaseId", "Position" },
                unique: true);

            // Runtime least-privilege grants, following the posture fixed by
            // 20260729199000_RuntimeRoleReconciliation: the Web process owns
            // this state; the Worker gets nothing. Assessment fields and
            // estimate lines are current-state rows (their evidence lives in
            // the append-only history tables), so the Web role may delete
            // them; the work-request and control records are never deleted.
            if (!string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal))
            {
                return;
            }

            migrationBuilder.Sql(
                $"""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.database_principals
                    WHERE name = N'{WebRole}'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.database_principals
                    WHERE name = N'{WorkerRole}'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
                """);
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseAssessmentFields] TO [{WebRole}];");
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseEstimateLines] TO [{WebRole}];");
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[AiWorkRequests] TO [{WebRole}];");
            migrationBuilder.Sql(
                $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[SendToAiControl] TO [{WebRole}];");
            foreach (var table in new[] { "AiWorkRequests", "SendToAiControl" })
            {
                migrationBuilder.Sql(
                    $"DENY DELETE ON OBJECT::[dbo].[{table}] TO [{WebRole}];");
                migrationBuilder.Sql(
                    $"DENY DELETE ON OBJECT::[dbo].[{table}] TO [{WorkerRole}];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiWorkRequests");

            migrationBuilder.DropTable(
                name: "CaseAssessmentFields");

            migrationBuilder.DropTable(
                name: "CaseEstimateLines");

            migrationBuilder.DropTable(
                name: "SendToAiControl");
        }
    }
}
