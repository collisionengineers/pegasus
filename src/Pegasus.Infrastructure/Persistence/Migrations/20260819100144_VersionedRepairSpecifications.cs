using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VersionedRepairSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseEstimateLines_CaseId_Position",
                table: "CaseEstimateLines");

            migrationBuilder.AddColumn<Guid>(
                name: "RepairSpecificationId",
                table: "CaseEstimateLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaseRepairSpecifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceRoute = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SourceArtifactReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: true),
                    CalculationLabour = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculationParts = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculationPaintMaterials = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculationSpecialistOther = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    RepairerVatRegistered = table.Column<bool>(type: "bit", nullable: true),
                    CalculationVat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculationTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CalculationPolicyVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreationOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SupersedesSpecificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SupersessionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseRepairSpecifications", x => x.Id);
                    table.CheckConstraint("CK_CaseRepairSpecifications_Acceptance", "([State] IN ('Accepted', 'Superseded') AND [AcceptedBy] IS NOT NULL AND [AcceptedAtUtc] IS NOT NULL) OR ([State] = 'Draft' AND [AcceptedBy] IS NULL AND [AcceptedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_CaseRepairSpecifications_Purpose", "[Purpose] IN ('OrdinaryAssessment', 'Audit')");
                    table.CheckConstraint("CK_CaseRepairSpecifications_Role", "[Role] IN ('Ordinary', 'Conservative', 'Maximised')");
                    table.CheckConstraint("CK_CaseRepairSpecifications_SourceRoute", "[SourceRoute] IN ('LegacyUnresolved', 'Manual', 'Glasses', 'AudatexPdf', 'ApprovedAiProposal')");
                    table.CheckConstraint("CK_CaseRepairSpecifications_State", "[State] IN ('Draft', 'Accepted', 'Superseded')");
                    table.CheckConstraint("CK_CaseRepairSpecifications_Version", "[Version] > 0");
                    table.ForeignKey(
                        name: "FK_CaseRepairSpecifications_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve every pre-existing replace-all estimate collection as
            // one unresolved draft. Missing provenance/acceptance is explicit.
            migrationBuilder.Sql(
                """
                INSERT INTO [CaseRepairSpecifications]
                    ([Id], [CaseId], [Version], [Purpose], [Role], [State], [SourceRoute],
                     [CreatedBy], [CreationOperationKey], [CreatedAtUtc])
                SELECT NEWID(), lines.[CaseId], 1, 'OrdinaryAssessment', 'Ordinary', 'Draft',
                       'LegacyUnresolved', 'legacy-migration',
                       CONCAT('legacy-migration:', CONVERT(nvarchar(36), lines.[CaseId])),
                       SYSUTCDATETIME()
                FROM [CaseEstimateLines] AS lines
                GROUP BY lines.[CaseId];

                UPDATE lines
                SET [RepairSpecificationId] = specifications.[Id]
                FROM [CaseEstimateLines] AS lines
                INNER JOIN [CaseRepairSpecifications] AS specifications
                    ON specifications.[CaseId] = lines.[CaseId]
                    AND specifications.[Purpose] = 'OrdinaryAssessment'
                    AND specifications.[Role] = 'Ordinary'
                    AND specifications.[Version] = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CaseEstimateLines_CaseId",
                table: "CaseEstimateLines",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEstimateLines_RepairSpecificationId_Position",
                table: "CaseEstimateLines",
                columns: new[] { "RepairSpecificationId", "Position" },
                unique: true,
                filter: "[RepairSpecificationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_CaseId_CreationOperationKey",
                table: "CaseRepairSpecifications",
                columns: new[] { "CaseId", "CreationOperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_CaseId_Purpose_Role",
                table: "CaseRepairSpecifications",
                columns: new[] { "CaseId", "Purpose", "Role" },
                unique: true,
                filter: "[State] = 'Accepted'");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecifications_CaseId_Purpose_Role_Version",
                table: "CaseRepairSpecifications",
                columns: new[] { "CaseId", "Purpose", "Role", "Version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseEstimateLines_CaseRepairSpecifications_RepairSpecificationId",
                table: "CaseEstimateLines",
                column: "RepairSpecificationId",
                principalTable: "CaseRepairSpecifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CaseEstimateLines_CaseRepairSpecifications_RepairSpecificationId",
                table: "CaseEstimateLines");

            migrationBuilder.DropTable(
                name: "CaseRepairSpecifications");

            migrationBuilder.DropIndex(
                name: "IX_CaseEstimateLines_CaseId",
                table: "CaseEstimateLines");

            migrationBuilder.DropIndex(
                name: "IX_CaseEstimateLines_RepairSpecificationId_Position",
                table: "CaseEstimateLines");

            migrationBuilder.DropColumn(
                name: "RepairSpecificationId",
                table: "CaseEstimateLines");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEstimateLines_CaseId_Position",
                table: "CaseEstimateLines",
                columns: new[] { "CaseId", "Position" },
                unique: true);
        }
    }
}
