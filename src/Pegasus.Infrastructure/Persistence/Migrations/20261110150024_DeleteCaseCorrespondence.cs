using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeleteCaseCorrespondence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrespondenceMoveAttempts",
                table: "StaffMailSendOperations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CorrespondenceMoveFailureCode",
                table: "StaffMailSendOperations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrespondenceMoveState",
                table: "StaffMailSendOperations",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CorrespondenceMovedAtUtc",
                table: "StaffMailSendOperations",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TidiesCaseCorrespondence",
                table: "StaffMailSendOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_StaffMailSendOperations_CorrespondenceTidyDue",
                table: "StaffMailSendOperations",
                columns: new[] { "State", "ObservedSentAtUtc" },
                filter: "[TidiesCaseCorrespondence] = 1 AND [CorrespondenceMoveState] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StaffMailSendOperations_CorrespondenceTidy",
                table: "StaffMailSendOperations",
                sql: "[TidiesCaseCorrespondence] = 0 OR [Purpose] IN ('CaseReport', 'TriageOutcomeReply') OR ([Purpose] = 'GeneralCorrespondence' AND [ComposeMode] IN ('Reply', 'ReplyAll'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffMailSendOperations_CorrespondenceTidyDue",
                table: "StaffMailSendOperations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StaffMailSendOperations_CorrespondenceTidy",
                table: "StaffMailSendOperations");

            migrationBuilder.DropColumn(
                name: "CorrespondenceMoveAttempts",
                table: "StaffMailSendOperations");

            migrationBuilder.DropColumn(
                name: "CorrespondenceMoveFailureCode",
                table: "StaffMailSendOperations");

            migrationBuilder.DropColumn(
                name: "CorrespondenceMoveState",
                table: "StaffMailSendOperations");

            migrationBuilder.DropColumn(
                name: "CorrespondenceMovedAtUtc",
                table: "StaffMailSendOperations");

            migrationBuilder.DropColumn(
                name: "TidiesCaseCorrespondence",
                table: "StaffMailSendOperations");
        }
    }
}
