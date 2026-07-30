using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PegasusDbContext))]
    [Migration("20260729186000_CaseTasksArchive")]
    public sealed class CaseTasksArchive : Migration
    {
        private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            RequireSqlServer();
            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "CaseWorkflows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "CaseWorkflows",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedByKind",
                table: "CaseWorkflows",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedByRolesJson",
                table: "CaseWorkflows",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedBySubjectId",
                table: "CaseWorkflows",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaseTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AssigneeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    State = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseTasks", x => x.Id);
                    table.CheckConstraint("CK_CaseTasks_Description", "[Description] <> ''");
                    table.CheckConstraint("CK_CaseTasks_State", "[State] IN ('Open', 'Completed', 'Cancelled')");
                    table.CheckConstraint("CK_CaseTasks_Version", "[Version] >= 0");
                    table.ForeignKey(
                        name: "FK_CaseTasks_AspNetUsers_AssigneeId",
                        column: x => x.AssigneeId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CaseTasks_CaseWorkflows_CaseId",
                        column: x => x.CaseId,
                        principalTable: "CaseWorkflows",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseWorkflows_ArchiveMetadata",
                table: "CaseWorkflows",
                sql: "([ArchivedAtUtc] IS NULL AND [ArchivedByKind] IS NULL AND [ArchivedBySubjectId] IS NULL AND [ArchivedByRolesJson] IS NULL AND [ArchiveReason] IS NULL) OR ([ArchivedAtUtc] IS NOT NULL AND [ArchivedByKind] IS NOT NULL AND [ArchivedBySubjectId] IS NOT NULL AND [ArchivedByRolesJson] IS NOT NULL AND [ArchiveReason] IS NOT NULL AND [ArchiveReason] <> '')");

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_AssigneeId_State",
                table: "CaseTasks",
                columns: new[] { "AssigneeId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseTasks_CaseId_State",
                table: "CaseTasks",
                columns: new[] { "CaseId", "State" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RequireSqlServer();
            migrationBuilder.DropTable(
                name: "CaseTasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseWorkflows_ArchiveMetadata",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "ArchivedByKind",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "ArchivedByRolesJson",
                table: "CaseWorkflows");

            migrationBuilder.DropColumn(
                name: "ArchivedBySubjectId",
                table: "CaseWorkflows");
        }

        private void RequireSqlServer()
        {
            if (!string.Equals(ActiveProvider, SqlServerProvider, StringComparison.Ordinal))
            {
                throw new NotSupportedException(
                    $"Migration provider '{ActiveProvider}' is not supported.");
            }
        }
    }
}
