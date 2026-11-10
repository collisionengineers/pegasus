using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuideValuationFetches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuideValuationFetches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GuideMonth = table.Column<DateOnly>(type: "date", nullable: false),
                    Registration = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Mileage = table.Column<long>(type: "bigint", nullable: false),
                    MileageUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClaimToken = table.Column<string>(type: "nchar(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ClaimExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AnsweredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideValuationFetches", x => x.Id);
                    table.CheckConstraint("CK_GuideValuationFetches_Mileage", "[Mileage] >= 0");
                    table.CheckConstraint("CK_GuideValuationFetches_Source", "[Source] IN ('Glasses', 'Cazana', 'EngineersValue', 'AiMarketResearch', 'Brego', 'SuperCap', 'Cap')");
                    table.CheckConstraint("CK_GuideValuationFetches_State", "[State] IN ('Asking', 'Answered')");
                    table.ForeignKey(
                        name: "FK_GuideValuationFetches_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuideValuationFetches_CaseId_Source_GuideMonth_Registration_Mileage_MileageUnit",
                table: "GuideValuationFetches",
                columns: new[] { "CaseId", "Source", "GuideMonth", "Registration", "Mileage", "MileageUnit" },
                unique: true);

            // The runtime roles' grants on the new table (RuntimePermissions.csv):
            // the Web claims, answers and releases a guide valuation ask (R900);
            // the Worker never touches one.
            migrationBuilder.Sql("""
                GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[GuideValuationFetches] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[GuideValuationFetches] TO [pegasus_worker_runtime_role];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuideValuationFetches");
        }
    }
}
