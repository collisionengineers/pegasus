using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MailClassificationCorrectionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "IntakeMailClassificationDecisions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAtUtc",
                table: "IntakeMailClassificationDecisions",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "DecidedByActor",
                table: "IntakeMailClassificationDecisions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "IntakeMailClassificationDecisions",
                type: "int",
                nullable: false,
                defaultValue: 1);

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    UPDATE decision
                    SET decision.DecidedByActor = COALESCE(latest.Actor, N'system-worker:legacy-intake'),
                        decision.DecidedAtUtc = COALESCE(latest.OccurredAtUtc, receipt.ProcessedAtUtc),
                        decision.Version = 1
                    FROM IntakeMailClassificationDecisions AS decision
                    INNER JOIN IntakeReceipts AS receipt ON receipt.Id = decision.IntakeReceiptId
                    OUTER APPLY (
                        SELECT TOP (1) eventRow.Actor, eventRow.OccurredAtUtc
                        FROM IntakeReceiptEvents AS eventRow
                        WHERE eventRow.IntakeReceiptId = decision.IntakeReceiptId
                        ORDER BY eventRow.OccurredAtUtc DESC, eventRow.Id DESC
                    ) AS latest;
                    """);
            }

            migrationBuilder.CreateTable(
                name: "IntakeMailClassificationHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CorrectedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeMailClassificationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeMailClassificationHistory_IntakeMailClassificationDecisions_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeMailClassificationDecisions",
                        principalColumn: "IntakeReceiptId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeMailClassificationHistory_IntakeReceiptId_Version",
                table: "IntakeMailClassificationHistory",
                columns: new[] { "IntakeReceiptId", "Version" },
                unique: true);

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT SELECT, UPDATE ON OBJECT::[dbo].[IntakeMailClassificationDecisions] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeMailClassificationHistory] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "DENY UPDATE, DELETE ON OBJECT::[dbo].[IntakeMailClassificationHistory] TO [pegasus_web_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeMailClassificationHistory");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "IntakeMailClassificationDecisions");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                table: "IntakeMailClassificationDecisions");

            migrationBuilder.DropColumn(
                name: "DecidedByActor",
                table: "IntakeMailClassificationDecisions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "IntakeMailClassificationDecisions");
        }
    }
}
