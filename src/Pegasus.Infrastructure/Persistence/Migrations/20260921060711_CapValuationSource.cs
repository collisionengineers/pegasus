using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CapValuationSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CaseValuations_Source",
                table: "CaseValuations",
                sql: "[Source] IN ('Glasses', 'Cazana', 'EngineersValue', 'AiMarketResearch', 'Brego', 'SuperCap', 'Cap')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => throw new NotSupportedException(
                "CAP valuation source is a forward-only schema change.");
    }
}
