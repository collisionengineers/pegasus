using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseEditRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EditRequestAnswer",
                table: "CaseWorkflows",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditRequestHolder",
                table: "CaseWorkflows",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EditRequestId",
                table: "CaseWorkflows",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditRequestOperationKey",
                table: "CaseWorkflows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EditRequestedAtUtc",
                table: "CaseWorkflows",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditRequester",
                table: "CaseWorkflows",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EditRequesterKind",
                table: "CaseWorkflows",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseWorkflows_EditRequestAnswer",
                table: "CaseWorkflows",
                sql: "[EditRequestAnswer] IS NULL OR [EditRequestAnswer] IN ('accepted', 'declined', 'withdrawn')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseWorkflows_EditRequestShape",
                table: "CaseWorkflows",
                sql: "([EditRequestId] IS NULL AND [EditRequesterKind] IS NULL AND [EditRequester] IS NULL AND [EditRequestHolder] IS NULL AND [EditRequestOperationKey] IS NULL AND [EditRequestedAtUtc] IS NULL AND [EditRequestAnswer] IS NULL) OR ([EditRequestId] IS NOT NULL AND [EditRequesterKind] IS NOT NULL AND [EditRequester] IS NOT NULL AND [EditRequestHolder] IS NOT NULL AND [EditRequestOperationKey] IS NOT NULL AND [EditRequestedAtUtc] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseWorkflows_EditRequestAnswer",
                table: "CaseWorkflows");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseWorkflows_EditRequestShape",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "EditRequestAnswer",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "EditRequestHolder",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "EditRequestId",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "EditRequestOperationKey",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "EditRequestedAtUtc",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "EditRequester",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "EditRequesterKind",
                table: "CaseWorkflows");
        }
    }
}
