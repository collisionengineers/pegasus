using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The register's reviewed inspection locations, "the remainder in a new
/// <c>InspectionLocations</c> reference table" (R22, operator, 15 October
/// 2026), and their links to Principals. Additive: two new tables and no
/// data. The runtime roles get only what a runtime caller does: Web reads both
/// tables for the Case page and inserts link rows when it replaces a
/// Principal's code; the register loader writes as the operator, outside both
/// roles. Neither role deletes a row.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261017110000_InspectionLocations")]
public partial class InspectionLocations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InspectionLocations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Postcode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                SourceRecordId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionLocations", x => x.Id);
                table.CheckConstraint("CK_InspectionLocations_Address", "[Address] <> ''");
                table.CheckConstraint("CK_InspectionLocations_Name", "[Name] <> ''");
                table.CheckConstraint("CK_InspectionLocations_Postcode", "[Postcode] <> ''");
                table.CheckConstraint("CK_InspectionLocations_Version", "[Version] >= 0");
            });

        migrationBuilder.CreateTable(
            name: "InspectionLocationPrincipalLinks",
            columns: table => new
            {
                PrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                InspectionLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionLocationPrincipalLinks", x => new { x.PrincipalId, x.InspectionLocationId });
                table.ForeignKey(
                    name: "FK_InspectionLocationPrincipalLinks_InspectionLocations_InspectionLocationId",
                    column: x => x.InspectionLocationId,
                    principalTable: "InspectionLocations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_InspectionLocationPrincipalLinks_Principals_PrincipalId",
                    column: x => x.PrincipalId,
                    principalTable: "Principals",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InspectionLocationPrincipalLinks_InspectionLocationId",
            table: "InspectionLocationPrincipalLinks",
            column: "InspectionLocationId");

        migrationBuilder.CreateIndex(
            name: "IX_InspectionLocations_Source_SourceRecordId",
            table: "InspectionLocations",
            columns: new[] { "Source", "SourceRecordId" },
            unique: true);

        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT ON OBJECT::[dbo].[InspectionLocations] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[InspectionLocationPrincipalLinks] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[InspectionLocations] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[InspectionLocationPrincipalLinks] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                DENY DELETE ON OBJECT::[dbo].[InspectionLocations] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[InspectionLocationPrincipalLinks] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "InspectionLocationPrincipalLinks");

        migrationBuilder.DropTable(
            name: "InspectionLocations");
    }
}
