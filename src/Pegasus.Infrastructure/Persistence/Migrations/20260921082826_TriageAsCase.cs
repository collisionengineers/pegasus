using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TriageAsCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TriageSequences");

            migrationBuilder.DropIndex(
                name: "IX_Triage_OriginReceiptId",
                table: "Triage");

            migrationBuilder.AlterColumn<Guid>(
                name: "OriginReceiptId",
                table: "Triage",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "EvaluationRevisionId",
                table: "Triage",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Triage_OriginReceiptId",
                table: "Triage",
                column: "OriginReceiptId",
                unique: true,
                filter: "[OriginReceiptId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Triage_Cases_Id",
                table: "Triage",
                column: "Id",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Triage_Cases_Id",
                table: "Triage");

            migrationBuilder.DropIndex(
                name: "IX_Triage_OriginReceiptId",
                table: "Triage");

            migrationBuilder.AlterColumn<Guid>(
                name: "OriginReceiptId",
                table: "Triage",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EvaluationRevisionId",
                table: "Triage",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "TriageSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LastAllocatedSequence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageSequences", x => x.Id);
                    table.CheckConstraint("CK_TriageSequences_LastAllocatedSequence", "[LastAllocatedSequence] >= 0");
                });

            migrationBuilder.InsertData(
                table: "TriageSequences",
                columns: new[] { "Id", "LastAllocatedSequence" },
                values: new object[] { 1, 0L });

            migrationBuilder.CreateIndex(
                name: "IX_Triage_OriginReceiptId",
                table: "Triage",
                column: "OriginReceiptId",
                unique: true);
        }
    }
}
