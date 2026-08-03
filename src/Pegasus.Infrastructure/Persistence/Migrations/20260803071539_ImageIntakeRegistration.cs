using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImageIntakeRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImageIntakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OriginReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceChannel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalReceiptToken = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EvaluationRevisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormalizedVehicleRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ImageIntakeReference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByActorKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedByActorSubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreationOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageIntakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageIntakes_IntakeReceipts_OriginReceiptId",
                        column: x => x.OriginReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImageIntakeSequences",
                columns: table => new
                {
                    NormalizedVehicleRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastAllocatedSequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageIntakeSequences", x => x.NormalizedVehicleRegistration);
                    table.CheckConstraint("CK_ImageIntakeSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ImageVrmSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ContentHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    EngineKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EngineVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ModelHashes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SuggestedRegistration = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Confidence = table.Column<double>(type: "float", nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Disposition = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DispositionActor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DispositionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DispositionOperationKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisposedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageVrmSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImageVrmSuggestions_IntakeAssets_IntakeAssetId",
                        column: x => x.IntakeAssetId,
                        principalTable: "IntakeAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImageVrmSuggestions_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_CreationOperationKey",
                table: "ImageIntakes",
                column: "CreationOperationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_ImageIntakeReference",
                table: "ImageIntakes",
                column: "ImageIntakeReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_NormalizedVehicleRegistration_CreatedAtUtc",
                table: "ImageIntakes",
                columns: new[] { "NormalizedVehicleRegistration", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_OriginReceiptId",
                table: "ImageIntakes",
                column: "OriginReceiptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageIntakes_SourceChannel_ExternalReceiptToken",
                table: "ImageIntakes",
                columns: new[] { "SourceChannel", "ExternalReceiptToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImageVrmSuggestions_IntakeAssetId",
                table: "ImageVrmSuggestions",
                column: "IntakeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageVrmSuggestions_IntakeReceiptId_OccurredAtUtc",
                table: "ImageVrmSuggestions",
                columns: new[] { "IntakeReceiptId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImageVrmSuggestions_OperationKey",
                table: "ImageVrmSuggestions",
                column: "OperationKey",
                unique: true);

            foreach (var role in RuntimeRoles)
            {
                foreach (var (table, permissions) in RuntimeGrants)
                {
                    migrationBuilder.Sql(
                        $"GRANT {permissions} ON OBJECT::[dbo].[{table}] TO [{role}];");
                    migrationBuilder.Sql(
                        $"DENY DELETE ON OBJECT::[dbo].[{table}] TO [{role}];");
                }
            }
        }

        private static readonly string[] RuntimeRoles =
        [
            "pegasus_web_runtime_role",
            "pegasus_worker_runtime_role"
        ];

        // ImageIntakes rows are immutable after creation (no UPDATE anywhere);
        // sequences increment and suggestions take disposition updates. DELETE
        // is denied everywhere.
        private static readonly (string Table, string Permissions)[] RuntimeGrants =
        [
            ("ImageIntakes", "SELECT, INSERT"),
            ("ImageIntakeSequences", "SELECT, INSERT, UPDATE"),
            ("ImageVrmSuggestions", "SELECT, INSERT, UPDATE")
        ];

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var role in RuntimeRoles)
            {
                foreach (var (table, _) in RuntimeGrants)
                {
                    migrationBuilder.Sql(
                        $"REVOKE DELETE ON OBJECT::[dbo].[{table}] FROM [{role}];");
                    migrationBuilder.Sql(
                        $"REVOKE SELECT, INSERT, UPDATE ON OBJECT::[dbo].[{table}] FROM [{role}];");
                }
            }

            migrationBuilder.DropTable(
                name: "ImageIntakes");

            migrationBuilder.DropTable(
                name: "ImageIntakeSequences");

            migrationBuilder.DropTable(
                name: "ImageVrmSuggestions");
        }
    }
}
