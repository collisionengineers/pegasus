using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EngineerNotes : Migration
    {
        private const string WebRole = "pegasus_web_runtime_role";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EngineerNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RecordedByKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedBySubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedByRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineerNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EngineerNotes_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EngineerNotes_CaseId_OperationKey",
                table: "EngineerNotes",
                columns: new[] { "CaseId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EngineerNotes_CaseId_RecordedAtUtc_Id",
                table: "EngineerNotes",
                columns: new[] { "CaseId", "RecordedAtUtc", "Id" },
                descending: new[] { false, true, true });

            // CASE-039: the Web Case workspace appends and reads Engineer
            // notes. The Worker has no caller, and append-only notes have no
            // UPDATE or DELETE grant.
            if (IsSqlServer())
            {
                RequireRuntimeRole(migrationBuilder);
                migrationBuilder.Sql(
                    $"GRANT SELECT, INSERT ON OBJECT::[dbo].[EngineerNotes] TO [{WebRole}];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (IsSqlServer())
            {
                migrationBuilder.Sql(
                    $"REVOKE SELECT, INSERT ON OBJECT::[dbo].[EngineerNotes] FROM [{WebRole}];");
            }

            migrationBuilder.DropTable(
                name: "EngineerNotes");
        }

        private bool IsSqlServer() =>
            string.Equals(
                ActiveProvider,
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);

        private static void RequireRuntimeRole(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                IF NOT EXISTS (
                    SELECT 1 FROM sys.database_principals
                    WHERE name = N'pegasus_web_runtime_role'
                      AND [type] = 'R'
                      AND is_fixed_role = 0
                      AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                    THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
                """);
    }
}
