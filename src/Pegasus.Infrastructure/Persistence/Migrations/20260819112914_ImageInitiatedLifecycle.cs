using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageInitiatedLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAtUtc",
                table: "ImageIntakes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "ImageIntakes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LifecycleState",
                table: "ImageIntakes",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "awaiting_instruction");

            migrationBuilder.AddColumn<long>(
                name: "LifecycleVersion",
                table: "ImageIntakes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "MergedIntoCaseId",
                table: "ImageIntakes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergedIntoCaseReference",
                table: "ImageIntakes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImageIntakeLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImageIntakeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BeforeVersion = table.Column<long>(type: "bigint", nullable: false),
                    AfterVersion = table.Column<long>(type: "bigint", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageIntakeLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageIntakeLifecycleEvents_ImageIntakes_ImageIntakeId",
                        column: x => x.ImageIntakeId,
                        principalTable: "ImageIntakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_LifecycleState_CreatedAtUtc",
                table: "ImageIntakes",
                columns: new[] { "LifecycleState", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakeLifecycleEvents_ImageIntakeId_OccurredAtUtc",
                table: "ImageIntakeLifecycleEvents",
                columns: new[] { "ImageIntakeId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakeLifecycleEvents_OperationKey",
                table: "ImageIntakeLifecycleEvents",
                column: "OperationKey",
                unique: true);

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                // A pre-existing ImageIntake whose origin receipt already has a
                // resolved case association (manual, active) or an accepted
                // case link is already merged in every practical sense: the
                // formal Case exists and the association is live. Backfilling
                // it as `awaiting_instruction` would put a settled record back
                // into the awaiting queue. Resolution mirrors
                // EfImageIntakeStore.CurrentCaseId: a manual association row,
                // once present, owns the answer (active -> its case, reversed
                // -> none); otherwise an accepted case-intake link applies.
                migrationBuilder.Sql(
                    """
                    UPDATE ii
                    SET ii.LifecycleState = N'merged_into_instruction_case',
                        ii.MergedIntoCaseId = resolved.CaseId,
                        ii.MergedIntoCaseReference = c.Reference
                    FROM ImageIntakes AS ii
                    CROSS APPLY (
                        SELECT
                            CASE
                                WHEN ma.IntakeReceiptId IS NOT NULL THEN
                                    CASE WHEN ma.IsActive = 1 THEN ma.CaseId ELSE NULL END
                                ELSE cil.CaseId
                            END AS CaseId
                        FROM (SELECT 1 AS Probe) AS one
                        LEFT JOIN IntakeManualAssociations AS ma ON ma.IntakeReceiptId = ii.OriginReceiptId
                        LEFT JOIN CaseIntakeLinks AS cil ON cil.IntakeReceiptId = ii.OriginReceiptId
                    ) AS resolved
                    INNER JOIN Cases AS c ON c.Id = resolved.CaseId
                    WHERE resolved.CaseId IS NOT NULL;
                    """);

                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "DENY UPDATE, DELETE ON OBJECT::[dbo].[ImageIntakeLifecycleEvents] TO [pegasus_web_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImageIntakeLifecycleEvents");

            migrationBuilder.DropIndex(
                name: "IX_ImageIntakes_LifecycleState_CreatedAtUtc",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "LifecycleVersion",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "MergedIntoCaseId",
                table: "ImageIntakes");

            migrationBuilder.DropColumn(
                name: "MergedIntoCaseReference",
                table: "ImageIntakes");
        }
    }
}
