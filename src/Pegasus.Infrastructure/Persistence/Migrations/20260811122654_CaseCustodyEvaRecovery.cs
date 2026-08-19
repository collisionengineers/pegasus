using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaseCustodyEvaRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // no-runtime-grant: EvaHandoffDownloadOperations - KNOWN GAP: no runtime GRANT exists anywhere for this table though Web reads/writes it via EvaHandoffStore; flagged in DELIV-012 report for a follow-up migration, not fixed here because this migration is already applied to production
            migrationBuilder.AddColumn<string>(
                name: "AuditFolderCreationToken",
                table: "ExternalWorkItems",
                type: "nchar(26)",
                fixedLength: true,
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaseRootCreationToken",
                table: "ExternalWorkItems",
                type: "nchar(26)",
                fixedLength: true,
                maxLength: 26,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "DocumentOccurrences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Ordinal",
                table: "CaseDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                """
                WITH OrderedDocuments AS
                (
                    SELECT Id,
                           ROW_NUMBER() OVER (PARTITION BY CaseId ORDER BY Id) + 1 AS EvidenceOrdinal
                    FROM CaseDocuments
                )
                UPDATE document
                SET Ordinal = ordered.EvidenceOrdinal
                FROM CaseDocuments AS document
                INNER JOIN OrderedDocuments AS ordered ON ordered.Id = document.Id;

                UPDATE occurrence
                SET Ordinal = document.Ordinal
                FROM DocumentOccurrences AS occurrence
                INNER JOIN CaseDocuments AS document ON document.Id = occurrence.DocumentId;
                """);

            migrationBuilder.CreateTable(
                name: "EvaHandoffDownloadOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaHandoffDownloadOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaHandoffDownloadOperations_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaHandoffDownloadOperations_EvaHandoffRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "EvaHandoffRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CaseDocuments_CaseId_Ordinal",
                table: "CaseDocuments",
                columns: new[] { "CaseId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffDownloadOperations_CaseId_PreparedAtUtc",
                table: "EvaHandoffDownloadOperations",
                columns: new[] { "CaseId", "PreparedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffDownloadOperations_OperationKey",
                table: "EvaHandoffDownloadOperations",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffDownloadOperations_RevisionId",
                table: "EvaHandoffDownloadOperations",
                column: "RevisionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvaHandoffDownloadOperations");

            migrationBuilder.DropIndex(
                name: "IX_CaseDocuments_CaseId_Ordinal",
                table: "CaseDocuments");

            migrationBuilder.DropColumn(
                name: "AuditFolderCreationToken",
                table: "ExternalWorkItems");

            migrationBuilder.DropColumn(
                name: "CaseRootCreationToken",
                table: "ExternalWorkItems");

            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "DocumentOccurrences");

            migrationBuilder.DropColumn(
                name: "Ordinal",
                table: "CaseDocuments");
        }
    }
}
