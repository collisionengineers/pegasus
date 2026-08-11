using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QdosAllocationRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaseType",
                table: "IntakeMailClassificationDecisions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntakeAllocationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<long>(type: "bigint", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExpectedReceiptVersion = table.Column<long>(type: "bigint", nullable: false),
                    CaseType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PrincipalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstructionComplete = table.Column<bool>(type: "bit", nullable: false),
                    ImagesComplete = table.Column<bool>(type: "bit", nullable: false),
                    InstructionConfirmedByStaff = table.Column<bool>(type: "bit", nullable: false),
                    ImagesConfirmedByStaff = table.Column<bool>(type: "bit", nullable: false),
                    StandaloneAuditEvidenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AcceptedInspectionDeadline = table.Column<DateOnly>(type: "date", nullable: true),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CommandHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    RecoveryDisposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    SafeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CaseReference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AuditReference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAllocationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeAllocationAttempts_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAllocationAttempts_OperationKey",
                table: "IntakeAllocationAttempts",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAllocationAttempts_IntakeReceiptId_AttemptNumber",
                table: "IntakeAllocationAttempts",
                columns: new[] { "IntakeReceiptId", "AttemptNumber" },
                unique: true);

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    """
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.database_principals
                        WHERE name = N'pegasus_web_runtime_role'
                          AND [type] = 'R'
                          AND is_fixed_role = 0
                          AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                        THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.database_principals
                        WHERE name = N'pegasus_worker_runtime_role'
                          AND [type] = 'R'
                          AND is_fixed_role = 0
                          AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                        THROW 51000, 'The fixed Pegasus Worker runtime role is missing or invalid.', 1;
                    """);
                // Worker performs the initial attempt; Web performs only
                // authenticated staff create/retry. Both need the same durable
                // begin/complete/cancel operations.
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeAllocationAttempts] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeAllocationAttempts] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeAllocationAttempts");

            migrationBuilder.DropColumn(
                name: "CaseType",
                table: "IntakeMailClassificationDecisions");
        }
    }
}
