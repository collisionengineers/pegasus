using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // ENG-016: the EVA hand-off and the operator export were two acts over
    // one archive format. They are now one -- Export -- and it records the
    // once-per-case First sent to Engineer proxy that was the hand-off's only
    // unique contribution. Three of the four tables go with the act:
    // EvaHandoffRevisions (frozen revision bodies), EvaHandoffOperations and
    // EvaHandoffDownloadOperations (its two replay ledgers).
    //
    // EvaFirstHandoffProxies SURVIVES -- it feeds the dashboard's "Sent to
    // Engineer" count -- but loses RevisionId and OperationKey because the
    // proxy now records only the once-per-case first-send fact. Export does
    // carry an operation key; its per-export replay and audit record belongs
    // in ActionHistory instead. The proxy guarantee remains its CaseId primary
    // key, and both CK_EvaFirstHandoffProxies_* checks remain.
    //
    // This permitted pre-cutover removal follows ADR-0030: recovery is to roll
    // forward, never to restore the removed development-state hand-off path.
    // Down() is EF development scaffolding and is not the supported recovery
    // procedure. Affected capability: EXT-03.
    public partial class DropEvaHandoffTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EvaFirstHandoffProxies_EvaHandoffRevisions_RevisionId",
                table: "EvaFirstHandoffProxies");

            migrationBuilder.DropTable(
                name: "EvaHandoffDownloadOperations");

            migrationBuilder.DropTable(
                name: "EvaHandoffOperations");

            migrationBuilder.DropTable(
                name: "EvaHandoffRevisions");

            migrationBuilder.DropIndex(
                name: "IX_EvaFirstHandoffProxies_RevisionId",
                table: "EvaFirstHandoffProxies");

            migrationBuilder.DropColumn(
                name: "OperationKey",
                table: "EvaFirstHandoffProxies");

            migrationBuilder.DropColumn(
                name: "RevisionId",
                table: "EvaFirstHandoffProxies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OperationKey",
                table: "EvaFirstHandoffProxies",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RevisionId",
                table: "EvaFirstHandoffProxies",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "EvaHandoffRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcceptedCaseVersion = table.Column<long>(type: "bigint", nullable: false),
                    BundleContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    BundleSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    GeneratedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GeneratedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InputFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    JsonContent = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    JsonSha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SchemaVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaHandoffRevisions", x => x.Id);
                    table.CheckConstraint("CK_EvaHandoffRevisions_AcceptedCaseVersion", "[AcceptedCaseVersion] >= 0");
                    table.CheckConstraint("CK_EvaHandoffRevisions_Revision", "[Revision] > 0");
                    table.ForeignKey(
                        name: "FK_EvaHandoffRevisions_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EvaHandoffDownloadOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ActorRolesJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "EvaHandoffOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RequestHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvaHandoffOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EvaHandoffOperations_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EvaHandoffOperations_EvaHandoffRevisions_RevisionId",
                        column: x => x.RevisionId,
                        principalTable: "EvaHandoffRevisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EvaFirstHandoffProxies_RevisionId",
                table: "EvaFirstHandoffProxies",
                column: "RevisionId",
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

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffOperations_CaseId_RecordedAtUtc",
                table: "EvaHandoffOperations",
                columns: new[] { "CaseId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffOperations_OperationKey",
                table: "EvaHandoffOperations",
                column: "OperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffOperations_RevisionId",
                table: "EvaHandoffOperations",
                column: "RevisionId");

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffRevisions_CaseId_InputFingerprint",
                table: "EvaHandoffRevisions",
                columns: new[] { "CaseId", "InputFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EvaHandoffRevisions_CaseId_Revision",
                table: "EvaHandoffRevisions",
                columns: new[] { "CaseId", "Revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EvaFirstHandoffProxies_EvaHandoffRevisions_RevisionId",
                table: "EvaFirstHandoffProxies",
                column: "RevisionId",
                principalTable: "EvaHandoffRevisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
