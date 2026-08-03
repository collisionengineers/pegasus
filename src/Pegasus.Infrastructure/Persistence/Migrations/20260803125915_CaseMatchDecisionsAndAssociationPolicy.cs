using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseMatchDecisionsAndAssociationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MatchPolicyKey",
                table: "IntakeManualAssociations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchPolicyVersion",
                table: "IntakeManualAssociations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CaseMatchIndex",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkProviderCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DurableClaimToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NormalizedVrm = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    NormalizedSurname = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NormalizedFirstInitial = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    IncidentDate = table.Column<DateOnly>(type: "date", nullable: true),
                    MatchPolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MatchPolicyVersion = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseMatchIndex", x => x.CaseId);
                    table.ForeignKey(
                        name: "FK_CaseMatchIndex_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeCaseMatchDecisions",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MatchedCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RedirectedFromCaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MatchKeysJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CandidatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeCaseMatchDecisions", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_IntakeCaseMatchDecisions_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseMatchIndex_WorkProviderCode_DurableClaimToken",
                table: "CaseMatchIndex",
                columns: new[] { "WorkProviderCode", "DurableClaimToken" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseMatchIndex_WorkProviderCode_NormalizedSurname",
                table: "CaseMatchIndex",
                columns: new[] { "WorkProviderCode", "NormalizedSurname" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseMatchIndex_WorkProviderCode_NormalizedVrm",
                table: "CaseMatchIndex",
                columns: new[] { "WorkProviderCode", "NormalizedVrm" });

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.database_principals
                        WHERE name = N'pegasus_web_runtime_role'
                          AND [type] = 'R'
                          AND is_fixed_role = 0
                          AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                        THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
                    IF NOT EXISTS (
                        SELECT 1
                        FROM sys.database_principals
                        WHERE name = N'pegasus_worker_runtime_role'
                          AND [type] = 'R'
                          AND is_fixed_role = 0
                          AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                        THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
                    """);
                // The Web keeps DELETE on CaseMatchIndex (the acceptance-path
                // projector replaces a case's index row in place) and the
                // Worker keeps DELETE on IntakeCaseMatchDecisions
                // (re-evaluation replaces the decision row after snapshotting
                // it to history). Everything else is denied DELETE.
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[CaseMatchIndex] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT ON OBJECT::[dbo].[CaseMatchIndex] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "DENY DELETE ON OBJECT::[dbo].[CaseMatchIndex] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT ON OBJECT::[dbo].[IntakeCaseMatchDecisions] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "DENY DELETE ON OBJECT::[dbo].[IntakeCaseMatchDecisions] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeCaseMatchDecisions] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[IntakeManualAssociations] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "DENY DELETE ON OBJECT::[dbo].[IntakeManualAssociations] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT ON OBJECT::[dbo].[IntakeMutationHistory] TO [pegasus_worker_runtime_role];");
                migrationBuilder.Sql(
                    "DENY DELETE ON OBJECT::[dbo].[IntakeMutationHistory] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseMatchIndex");

            migrationBuilder.DropTable(
                name: "IntakeCaseMatchDecisions");

            migrationBuilder.DropColumn(
                name: "MatchPolicyKey",
                table: "IntakeManualAssociations");

            migrationBuilder.DropColumn(
                name: "MatchPolicyVersion",
                table: "IntakeManualAssociations");
        }
    }
}
