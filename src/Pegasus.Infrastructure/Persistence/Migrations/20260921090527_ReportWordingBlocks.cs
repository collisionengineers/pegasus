using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Engineer's changes to the report's narrative blocks (v28 P30,
    /// ruled 20 September 2026): a renamed heading, wording written in place
    /// of the composed sentence, a new order, a block taken off the report and
    /// paragraphs the Engineer added. One row per Case and block, written by
    /// the one Case save; nothing deletes one. Additive.
    /// </summary>
    public partial class ReportWordingBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CaseReportWordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlockKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: true),
                    Included = table.Column<bool>(type: "bit", nullable: false),
                    Manual = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReportWordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseReportWordings_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseReportWordings_CaseId_BlockKey",
                table: "CaseReportWordings",
                columns: new[] { "CaseId", "BlockKey" },
                unique: true);

            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_web_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_web_runtime_role];
                END;
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                BEGIN
                    GRANT SELECT ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_worker_runtime_role];
                    DENY DELETE ON OBJECT::[dbo].[CaseReportWordings] TO [pegasus_worker_runtime_role];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseReportWordings");
        }
    }
}
