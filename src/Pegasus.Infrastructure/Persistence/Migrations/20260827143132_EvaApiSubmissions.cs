using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EvaApiSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EvaAutomaticSubmission",
                table: "Principals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EvaManualSubmission",
                table: "Principals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EvaSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowVersion = table.Column<long>(type: "bigint", nullable: false),
                    ExternalRef = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSucceeded = table.Column<bool>(type: "bit", nullable: false),
                    EvaId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImagesSent = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaSubmissions", x => x.Id);
                    table.CheckConstraint("CK_EvaSubmissions_Counts", "[ImagesSent] >= 0 AND [AttemptCount] >= 1 AND [WorkflowVersion] >= 0");
                    table.CheckConstraint("CK_EvaSubmissions_Outcome", "[Outcome] IN ('Succeeded', 'Rejected', 'Partial', 'Unknown')");
                    table.CheckConstraint("CK_EvaSubmissions_SucceededAgreesWithOutcome", "([IsSucceeded] = 1 AND [Outcome] = 'Succeeded') OR ([IsSucceeded] = 0 AND [Outcome] <> 'Succeeded')");
                    table.ForeignKey(
                        name: "FK_EvaSubmissions_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaSubmissions_CaseOperationKey",
                table: "EvaSubmissions",
                columns: new[] { "CaseId", "OperationKey" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaSubmissions_CaseSubmittedAt",
                table: "EvaSubmissions",
                columns: new[] { "CaseId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_EvaSubmissions_CaseSucceeded",
                table: "EvaSubmissions",
                column: "CaseId",
                unique: true,
                filter: "[IsSucceeded] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaSubmissions");

            migrationBuilder.DropColumn(
                name: "EvaAutomaticSubmission",
                table: "Principals");

            migrationBuilder.DropColumn(
                name: "EvaManualSubmission",
                table: "Principals");
        }
    }
}
