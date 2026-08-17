using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropBoxFileRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoxFileRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BoxFileRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LinkTokenDigest = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    RevokeOperationKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoxFileRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BoxFileRequests_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BoxFileRequests_CaseId_CreateOperationKey",
                table: "BoxFileRequests",
                columns: new[] { "CaseId", "CreateOperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BoxFileRequests_CreatedAtUtc_Id",
                table: "BoxFileRequests",
                columns: new[] { "CreatedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_BoxFileRequests_DeactivatedAtUtc_Id",
                table: "BoxFileRequests",
                columns: new[] { "DeactivatedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_BoxFileRequests_LinkTokenDigest",
                table: "BoxFileRequests",
                column: "LinkTokenDigest",
                unique: true);
        }
    }
}
