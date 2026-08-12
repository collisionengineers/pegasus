using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ManualInspectionAuditCustody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseEngineerFindings_CustodyWorkId",
                table: "CaseEngineerFindings");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustodyWorkId",
                table: "CaseEngineerFindings",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEngineerFindings_CustodyWorkId",
                table: "CaseEngineerFindings",
                column: "CustodyWorkId",
                unique: true,
                filter: "[CustodyWorkId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseEngineerFindings_CustodyWorkId",
                table: "CaseEngineerFindings");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustodyWorkId",
                table: "CaseEngineerFindings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseEngineerFindings_CustodyWorkId",
                table: "CaseEngineerFindings",
                column: "CustodyWorkId",
                unique: true);
        }
    }
}
