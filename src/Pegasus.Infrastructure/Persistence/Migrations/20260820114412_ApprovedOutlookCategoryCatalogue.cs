using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ApprovedOutlookCategoryCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovedOutlookCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NormalizedDisplayName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    State = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedOutlookCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedOutlookCategories_NormalizedDisplayName",
                table: "ApprovedOutlookCategories",
                column: "NormalizedDisplayName",
                unique: true);

            if (string.Equals(ActiveProvider, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
            {
                migrationBuilder.Sql(
                    "GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[ApprovedOutlookCategories] TO [pegasus_web_runtime_role]; " +
                    "DENY DELETE ON OBJECT::[dbo].[ApprovedOutlookCategories] TO [pegasus_web_runtime_role];");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovedOutlookCategories");
        }
    }
}
