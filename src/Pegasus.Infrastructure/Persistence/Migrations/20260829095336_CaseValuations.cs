using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseValuations : Migration
    {
        private const string WebRole = "pegasus_web_runtime_role";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseValuations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    Mileage = table.Column<long>(type: "bigint", nullable: false),
                    RetailValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TradeValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastEditedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastEditedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseValuations", x => x.Id);
                    table.CheckConstraint("CK_CaseValuations_Mileage", "[Mileage] >= 0");
                    table.CheckConstraint("CK_CaseValuations_RetailValue", "[RetailValue] >= 0");
                    table.CheckConstraint("CK_CaseValuations_Source", "[Source] IN ('Glasses', 'Cazana', 'EngineersValue')");
                    table.CheckConstraint("CK_CaseValuations_TradeValue", "[TradeValue] >= 0");
                    table.ForeignKey(
                        name: "FK_CaseValuations_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseValuations_CaseId_Date_Time",
                table: "CaseValuations",
                columns: new[] { "CaseId", "Date", "Time" });

            // CASE-029 will create and edit valuations from the Web Case
            // workspace, while ENG-028 reads the current Engineer value in
            // the same process. The Worker has no caller. DELETE is absent:
            // a recorded Case valuation is never removed.
            if (IsSqlServer())
            {
                RequireRuntimeRole(migrationBuilder);
                migrationBuilder.Sql(
                    $"GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseValuations] TO [{WebRole}];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (IsSqlServer())
            {
                migrationBuilder.Sql(
                    $"REVOKE SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseValuations] FROM [{WebRole}];");
            }

            migrationBuilder.DropTable(
                name: "CaseValuations");
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
