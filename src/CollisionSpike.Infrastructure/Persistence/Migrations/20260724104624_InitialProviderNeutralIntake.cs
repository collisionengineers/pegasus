using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollisionSpike.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProviderNeutralIntake : Migration
    {
        private static readonly string[] IntakeAssetIndexColumns = ["IntakeReceiptId", "ContentHash"];
        private static readonly string[] SourceIdentityIndexColumns = ["SourceChannel", "ExternalReceiptToken"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isSqlite = ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite";
            var guidType = isSqlite ? "TEXT" : "uniqueidentifier";
            var integerType = isSqlite ? "INTEGER" : "int";
            var longType = isSqlite ? "INTEGER" : "bigint";
            var timestampType = isSqlite ? "TEXT" : "datetimeoffset";
            string TextType(string sqlServerType) => isSqlite ? "TEXT" : sqlServerType;

            migrationBuilder.CreateTable(
                name: "IntakeReceipts",
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
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: false),
                    SourceReaderKey = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: false),
                    SourceReaderVersion = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    ExtractionPolicyKey = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: true),
                    ExtractionPolicyVersion = table.Column<int>(type: integerType, nullable: true),
                    Decision = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    DecisionReason = table.Column<string>(type: TextType("nvarchar(500)"), maxLength: 500, nullable: false),
                    EvidenceJson = table.Column<string>(type: TextType("nvarchar(max)"), nullable: false),
                    FieldsJson = table.Column<string>(type: TextType("nvarchar(max)"), nullable: false),
                    FailureCode = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: TextType("nvarchar(500)"), maxLength: 500, nullable: true),
                    OcrCandidatesJson = table.Column<string>(type: TextType("nvarchar(max)"), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReceipts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstructionDrafts",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: guidType, nullable: false),
                    SuggestedPrincipalCode = table.Column<string>(type: TextType("nvarchar(20)"), maxLength: 20, nullable: true),
                    ClaimantName = table.Column<string>(type: TextType("nvarchar(300)"), maxLength: 300, nullable: true),
                    ClaimNumber = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: true),
                    VehicleRegistration = table.Column<string>(type: TextType("nvarchar(20)"), maxLength: 20, nullable: true),
                    VehicleMake = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: true),
                    VehicleModel = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: true),
                    VehicleMileage = table.Column<long>(type: longType, nullable: true),
                    AccidentCircumstances = table.Column<string>(type: TextType("nvarchar(2000)"), maxLength: 2000, nullable: true),
                    DateOfIncident = table.Column<DateOnly>(type: "date", nullable: true),
                    InstructionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    InspectionAddress = table.Column<string>(type: TextType("nvarchar(1000)"), maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstructionDrafts", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_InstructionDrafts_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: guidType, nullable: false),
                    SourceLabel = table.Column<string>(type: TextType("nvarchar(500)"), maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: TextType("nvarchar(260)"), maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    Disposition = table.Column<string>(type: TextType("nvarchar(40)"), maxLength: 40, nullable: false),
                    ContentLength = table.Column<long>(type: longType, nullable: false),
                    ContentHash = table.Column<string>(type: TextType("nvarchar(64)"), maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    PageNumber = table.Column<int>(type: integerType, nullable: true),
                    BoundsJson = table.Column<string>(type: TextType("nvarchar(max)"), nullable: true),
                    WidthPixels = table.Column<int>(type: integerType, nullable: true),
                    HeightPixels = table.Column<int>(type: integerType, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeAssets_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntakeReceiptEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: guidType, nullable: false),
                    EventType = table.Column<string>(type: TextType("nvarchar(100)"), maxLength: 100, nullable: false),
                    Actor = table.Column<string>(type: TextType("nvarchar(200)"), maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: timestampType, nullable: false),
                    DetailsJson = table.Column<string>(type: TextType("nvarchar(max)"), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeReceiptEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeReceiptEvents_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeAssets_IntakeReceiptId_ContentHash",
                table: "IntakeAssets",
                columns: IntakeAssetIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceiptEvents_IntakeReceiptId",
                table: "IntakeReceiptEvents",
                column: "IntakeReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceipts_SourceChannel_ExternalReceiptToken",
                table: "IntakeReceipts",
                columns: SourceIdentityIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntakeReceipts_SourceHash",
                table: "IntakeReceipts",
                column: "SourceHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstructionDrafts");

            migrationBuilder.DropTable(
                name: "IntakeAssets");

            migrationBuilder.DropTable(
                name: "IntakeReceiptEvents");

            migrationBuilder.DropTable(
                name: "IntakeReceipts");
        }
    }
}
