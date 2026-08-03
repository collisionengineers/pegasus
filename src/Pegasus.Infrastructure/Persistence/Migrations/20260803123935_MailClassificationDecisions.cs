using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MailClassificationDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeMailClassificationDecisions",
                columns: table => new
                {
                    IntakeReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Family = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Subtype = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsReplyContext = table.Column<bool>(type: "bit", nullable: false),
                    OtherName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OtherReasoning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AmbiguousCandidatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PredicatesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PolicyVersion = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeMailClassificationDecisions", x => x.IntakeReceiptId);
                    table.ForeignKey(
                        name: "FK_IntakeMailClassificationDecisions_IntakeReceipts_IntakeReceiptId",
                        column: x => x.IntakeReceiptId,
                        principalTable: "IntakeReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT SELECT ON OBJECT::[dbo].[IntakeMailClassificationDecisions] TO [pegasus_web_runtime_role];");
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::[dbo].[IntakeMailClassificationDecisions] TO [pegasus_worker_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeMailClassificationDecisions");
        }
    }
}
