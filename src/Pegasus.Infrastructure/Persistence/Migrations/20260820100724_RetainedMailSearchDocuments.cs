using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetainedMailSearchDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeSearchDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeSearchDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeSearchDocuments_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeSearchDocuments_IntakeReceiptId_Ordinal",
                table: "IntakeSearchDocuments",
                columns: new[] { "IntakeReceiptId", "Ordinal" },
                unique: true);

            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                    GRANT SELECT ON OBJECT::[dbo].[IntakeSearchDocuments] TO [pegasus_web_runtime_role];
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                    GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeSearchDocuments] TO [pegasus_worker_runtime_role];
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
                    REVOKE SELECT ON OBJECT::[dbo].[IntakeSearchDocuments] FROM [pegasus_web_runtime_role];
                IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
                    REVOKE SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeSearchDocuments] FROM [pegasus_worker_runtime_role];
                """);

            migrationBuilder.DropTable(
                name: "IntakeSearchDocuments");
        }
    }
}
