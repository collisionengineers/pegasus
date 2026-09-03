using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiJobs",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SubjectKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SubjectReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Instruction = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TargetPercentOfEngineerValue = table.Column<int>(type: "int", nullable: true),
                    EngineerValueAtSend = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CreatedByKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TakenBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TakenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProgressNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ResultKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ResultReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResultText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ClosureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiJobs", x => x.JobId);
                    table.CheckConstraint("CK_AiJobs_Kind", "[Kind] IN ('Estimate', 'UnidentifiedResolution', 'QueryResponse', 'UnidentifiedQueuePass')");
                    table.CheckConstraint("CK_AiJobs_ResultKind", "[ResultKind] IS NULL OR [ResultKind] IN ('Estimate', 'ProposedResolution', 'DraftReply')");
                    table.CheckConstraint("CK_AiJobs_State", "[State] IN ('Queued', 'Taken', 'DraftReady', 'Completed', 'Failed', 'Cancelled', 'Expired')");
                    table.CheckConstraint("CK_AiJobs_SubjectKind", "[SubjectKind] IN ('Case', 'Unidentified', 'Queue')");
                    table.CheckConstraint("CK_AiJobs_TargetPercent", "[TargetPercentOfEngineerValue] IS NULL OR [TargetPercentOfEngineerValue] BETWEEN 1 AND 100");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_CreatedAtUtc",
                table: "AiJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_OperationKey",
                table: "AiJobs",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_State_LeaseExpiresAtUtc",
                table: "AiJobs",
                columns: new[] { "State", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AiJobs_SubjectId",
                table: "AiJobs",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiJobs");
        }
    }
}
