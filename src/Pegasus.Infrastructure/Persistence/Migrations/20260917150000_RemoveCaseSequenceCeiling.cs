using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260917150000_RemoveCaseSequenceCeiling")]
public partial class RemoveCaseSequenceCeiling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CaseSequences_LastAllocatedSequence",
            table: "CaseSequences");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Cases_Sequence",
            table: "Cases");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CaseSequences_LastAllocatedSequence",
            table: "CaseSequences",
            sql: "[LastAllocatedSequence] >= 0");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Cases_Sequence",
            table: "Cases",
            sql: "[Sequence] >= 1");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "This pre-release case sequence ceiling removal migration is forward-only.");
    }
}
