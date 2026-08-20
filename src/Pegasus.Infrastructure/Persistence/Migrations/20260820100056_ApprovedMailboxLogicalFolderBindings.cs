using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApprovedMailboxLogicalFolderBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovedMailboxFolderBindings",
                columns: table => new
                {
                    ApprovedMailboxId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FolderType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FolderIdentity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedMailboxFolderBindings", x => new { x.ApprovedMailboxId, x.FolderType });
                    table.ForeignKey(
                        name: "FK_ApprovedMailboxFolderBindings_ApprovedMailboxes_ApprovedMailboxId",
                        column: x => x.ApprovedMailboxId,
                        principalTable: "ApprovedMailboxes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            if (string.Equals(
                    ActiveProvider,
                    "Microsoft.EntityFrameworkCore.SqlServer",
                    StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, DELETE ON OBJECT::[dbo].[ApprovedMailboxFolderBindings] TO [pegasus_web_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovedMailboxFolderBindings");
        }
    }
}
