using System;
using Pegasus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PegasusDbContext))]
    [Migration("20260729120000_DurableIntakeWork")]
    public partial class DurableIntakeWork : Migration
    {
        private static readonly string[] StagedReceiptSourceIdentityIndexColumns = ["SourceChannel", "ExternalReceiptToken"];
        private static readonly string[] EvaluationRevisionIndexColumns = ["StagedReceiptId", "Revision"];
        private static readonly string[] WorkItemDueIndexColumns = ["State", "DueAtUtc"];

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isSqlite = ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite";
            var guidType = isSqlite ? "TEXT" : "uniqueidentifier";
            var integerType = isSqlite ? "INTEGER" : "int";
            var longType = isSqlite ? "INTEGER" : "bigint";
            var timestampType = isSqlite ? "TEXT" : "datetimeoffset";
            string TextType(string sqlServerType) => isSqlite ? "TEXT" : sqlServerType;

            migrationBuilder.CreateTable(
                name: "IntakeStagedReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    SourceFileName = table.Column<string>(type: TextType("nvarchar(260)"), maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    SourceLength = table.Column<long>(type: longType, nullable: false),
                    SourceHash = table.Column<string>(type: TextType("nvarchar(64)"), maxLength: 64, nullable: false),
                    SourceChannel = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    ExternalReceiptToken = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: false),
                    Actor = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    StorageKey = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    StagedAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_IntakeStagedReceipts", x => x.Id));

            migrationBuilder.CreateTable(
                name: "IntakeWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    StagedReceiptId = table.Column<Guid>(type: guidType, nullable: false),
                    OperationKey = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: false),
                    State = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: integerType, nullable: false),
                    DueAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: false),
                    LeaseToken = table.Column<string>(type: TextType("nvarchar(64)"), maxLength: 64, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: true),
                    ProcessedReceiptId = table.Column<Guid>(type: guidType, nullable: true),
                    FailureCode = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeWorkItems", x => x.Id);
                    table.CheckConstraint("CK_IntakeWorkItems_AttemptCount", "AttemptCount >= 0");
                    table.ForeignKey(
                        name: "FK_IntakeWorkItems_IntakeStagedReceipts_StagedReceiptId",
                        column: x => x.StagedReceiptId,
                        principalTable: "IntakeStagedReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IntakeEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    StagedReceiptId = table.Column<Guid>(type: guidType, nullable: false),
                    ProcessedReceiptId = table.Column<Guid>(type: guidType, nullable: false),
                    Revision = table.Column<int>(type: integerType, nullable: false),
                    EvaluatedAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeEvaluations_IntakeStagedReceipts_StagedReceiptId",
                        column: x => x.StagedReceiptId,
                        principalTable: "IntakeStagedReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeStagedReceipts_SourceChannel_ExternalReceiptToken",
                table: "IntakeStagedReceipts",
                columns: StagedReceiptSourceIdentityIndexColumns,
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_IntakeStagedReceipts_SourceHash",
                table: "IntakeStagedReceipts",
                column: "SourceHash");
            migrationBuilder.CreateIndex(
                name: "IX_IntakeEvaluations_StagedReceiptId_Revision",
                table: "IntakeEvaluations",
                columns: EvaluationRevisionIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_OperationKey",
                table: "IntakeWorkItems",
                column: "OperationKey",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_StagedReceiptId",
                table: "IntakeWorkItems",
                column: "StagedReceiptId",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_IntakeWorkItems_State_DueAtUtc",
                table: "IntakeWorkItems",
                columns: WorkItemDueIndexColumns);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IntakeEvaluations");
            migrationBuilder.DropTable(name: "IntakeWorkItems");
            migrationBuilder.DropTable(name: "IntakeStagedReceipts");
        }
    }
}
