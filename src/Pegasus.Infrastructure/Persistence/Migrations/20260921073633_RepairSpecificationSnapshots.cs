using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Frozen repair specification versions (v28 P43) and what a specification
    /// says about the one it supplements (P20). Additive.
    /// </summary>
    public partial class RepairSpecificationSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SupplementaryExplainOnReport",
                table: "CaseRepairSpecifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplementaryOfSpecificationId",
                table: "CaseRepairSpecifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplementaryReason",
                table: "CaseRepairSpecifications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupplementaryStatement",
                table: "CaseRepairSpecifications",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaseRepairSpecificationSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LinesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SentOnReport = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseRepairSpecificationSnapshots", x => x.Id);
                    table.CheckConstraint("CK_CaseRepairSpecificationSnapshots_Kind", "[Kind] IN ('Imported', 'BeforeScaling', 'Scaled', 'BeforeRestore', 'Sent')");
                    table.CheckConstraint("CK_CaseRepairSpecificationSnapshots_Number", "[Number] > 0");
                    table.ForeignKey(
                        name: "FK_CaseRepairSpecificationSnapshots_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecificationSnapshots_CaseId",
                table: "CaseRepairSpecificationSnapshots",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseRepairSpecificationSnapshots_SpecificationId_Number",
                table: "CaseRepairSpecificationSnapshots",
                columns: new[] { "SpecificationId", "Number" },
                unique: true);

            // Web freezes and reads versions (v28 P43); the Worker reads only; neither deletes one.
            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [pegasus_web_runtime_role];
                END;
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseRepairSpecificationSnapshots] TO [pegasus_worker_runtime_role];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseRepairSpecificationSnapshots");

            migrationBuilder.DropColumn(
                name: "SupplementaryExplainOnReport",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "SupplementaryOfSpecificationId",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "SupplementaryReason",
                table: "CaseRepairSpecifications");

            migrationBuilder.DropColumn(
                name: "SupplementaryStatement",
                table: "CaseRepairSpecifications");
        }
    }
}
