using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// The repairer VAT status defaults to Registered (operator, 19 October 2026).
/// Unknown is no longer the status a spec stands on when nobody has chosen:
/// every live repair spec still stored as Unknown becomes Registered. Category
/// flags stay as stored, so a hand-picked set keeps its categories. Snapshots
/// are frozen history and are not rewritten. Forward-only: a Registered row
/// that used to be Unknown cannot be told apart from one that was chosen.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261019120000_RepairerVatStatusDefaultsToRegistered")]
public class RepairerVatStatusDefaultsToRegistered : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.Sql("""
            UPDATE [dbo].[CaseRepairSpecifications]
            SET [RepairerVatStatus] = N'Registered'
            WHERE [RepairerVatStatus] = N'Unknown';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RepairerVatStatusDefaultsToRegistered is forward-only: live repair specs stored as Unknown were rewritten to Registered. Restore from an approved backup instead.");
}
