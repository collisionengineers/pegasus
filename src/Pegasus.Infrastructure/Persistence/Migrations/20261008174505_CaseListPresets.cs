using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseListPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseListPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ColumnKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RemovedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseListPresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseListPresets_Name",
                table: "CaseListPresets",
                column: "Name",
                unique: true,
                filter: "[RemovedAtUtc] IS NULL");

            // Administrators keep the presets through the Web; a removal is soft,
            // so neither runtime deletes a row.
            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseListPresets] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseListPresets] TO [pegasus_web_runtime_role];
                END;
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                BEGIN
                    DENY DELETE ON OBJECT::[dbo].[CaseListPresets] TO [pegasus_worker_runtime_role];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseListPresets");
        }
    }
}
