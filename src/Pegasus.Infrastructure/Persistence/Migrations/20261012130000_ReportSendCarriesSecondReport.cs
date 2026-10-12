using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// One Case report send may carry both works' reports of an Inspection +
    /// Audit Case (operator, 12 October 2026): the send records the other
    /// work's generation and its frozen version beside its own context.
    /// Additive; no grant changes, as both runtime roles already hold the table.
    /// </summary>
    public partial class ReportSendCarriesSecondReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SecondContextId",
                table: "StaffMailSendOperations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SecondContextVersion",
                table: "StaffMailSendOperations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffMailSendOperations_SecondReport",
                table: "StaffMailSendOperations",
                sql: "([SecondContextId] IS NULL AND [SecondContextVersion] IS NULL) OR ([SecondContextId] IS NOT NULL AND [SecondContextVersion] > 0 AND [Purpose] = 'CaseReport' AND [SecondContextId] <> [ContextId])");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffMailSendOperations_SecondReport",
                table: "StaffMailSendOperations");

            migrationBuilder.DropColumn(
                name: "SecondContextId",
                table: "StaffMailSendOperations");

            migrationBuilder.DropColumn(
                name: "SecondContextVersion",
                table: "StaffMailSendOperations");
        }
    }
}
