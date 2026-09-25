using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The Case's instruction date is its Received date (operator, 24 September
/// 2026), so the separate instruction date's two stores go: every
/// <c>instruction_date</c> row in <c>CaseDataFields</c> (fact, suggestion and
/// staff-confirmed alike) and <c>InstructionDrafts.InstructionDate</c>. Nothing
/// reads either. What a source said stays where it already is: a receipt's
/// extracted review fields and a provider's declared instruction.
/// Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260925120000_RetireInstructionDate")]
public partial class RetireInstructionDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        // The literal is the retired CaseDataFieldNames.InstructionDate, spelled
        // out because a migration describes the rows a past release wrote.
        migrationBuilder.Sql(
            """
            DELETE FROM dbo.CaseDataFields
            WHERE [FieldName] = N'instruction_date';
            """);

        migrationBuilder.DropColumn(
            name: "InstructionDate",
            table: "InstructionDrafts");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RetireInstructionDate is forward-only: it drops InstructionDrafts.InstructionDate and deletes every instruction_date case-data row. Restore from an approved backup instead.");
}
