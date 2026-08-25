using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MailboxImageIntake : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentReceiptId",
                table: "IntakeSubmissionGroups",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSubmissionGroups_ParentReceiptId",
                table: "IntakeSubmissionGroups",
                column: "ParentReceiptId",
                unique: true,
                filter: "[ParentReceiptId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_IntakeSubmissionGroups_IntakeReceipts_ParentReceiptId",
                table: "IntakeSubmissionGroups",
                column: "ParentReceiptId",
                principalTable: "IntakeReceipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT INSERT ON OBJECT::[dbo].[IntakeSubmissionGroupMembers] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "REVOKE INSERT ON OBJECT::[dbo].[IntakeSubmissionGroupMembers] FROM [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "REVOKE INSERT ON OBJECT::[dbo].[IntakeSubmissionGroups] FROM [pegasus_worker_runtime_role];");
            }

            migrationBuilder.DropForeignKey(
                name: "FK_IntakeSubmissionGroups_IntakeReceipts_ParentReceiptId",
                table: "IntakeSubmissionGroups");

            migrationBuilder.DropIndex(
                name: "IX_IntakeSubmissionGroups_ParentReceiptId",
                table: "IntakeSubmissionGroups");

            migrationBuilder.DropColumn(
                name: "ParentReceiptId",
                table: "IntakeSubmissionGroups");
        }
    }
}
