using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairSpecificationSnapshotSupplementary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupplementaryJson",
                table: "CaseRepairSpecificationSnapshots",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "null");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupplementaryJson",
                table: "CaseRepairSpecificationSnapshots");
        }
    }
}
