using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollisionSpike.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isSqlite = ActiveProvider == "Microsoft.EntityFrameworkCore.Sqlite";
            var guidType = isSqlite ? "TEXT" : "uniqueidentifier";
            var textType = isSqlite ? "TEXT" : "nvarchar(max)";
            var integerType = isSqlite ? "INTEGER" : "int";
            var longType = isSqlite ? "INTEGER" : "bigint";
            string StringType(int length) => isSqlite ? "TEXT" : $"nvarchar({length})";

            migrationBuilder.AddColumn<string>(
                name: "OcrCandidatesJson",
                table: "QdosIntakeReceipts",
                type: textType,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "QdosIntakeAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: guidType, nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: guidType, nullable: false),
                    SourceLabel = table.Column<string>(type: StringType(500), maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: StringType(260), maxLength: 260, nullable: false),
                    MediaType = table.Column<string>(type: StringType(200), maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: StringType(40), maxLength: 40, nullable: false),
                    Disposition = table.Column<string>(type: StringType(40), maxLength: 40, nullable: false),
                    ContentLength = table.Column<long>(type: longType, nullable: false),
                    ContentHash = table.Column<string>(type: StringType(64), maxLength: 64, nullable: false),
                    StorageKey = table.Column<string>(type: StringType(200), maxLength: 200, nullable: false),
                    PageNumber = table.Column<int>(type: integerType, nullable: true),
                    BoundsJson = table.Column<string>(type: textType, nullable: true),
                    WidthPixels = table.Column<int>(type: integerType, nullable: true),
                    HeightPixels = table.Column<int>(type: integerType, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QdosIntakeAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QdosIntakeAssets_QdosIntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "QdosIntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QdosIntakeAssets_IntakeReceiptId_ContentHash",
                table: "QdosIntakeAssets",
                columns: ["IntakeReceiptId", "ContentHash"]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QdosIntakeAssets");

            migrationBuilder.DropColumn(
                name: "OcrCandidatesJson",
                table: "QdosIntakeReceipts");
        }
    }
}
