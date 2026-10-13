using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Labour-rate cards become the two built-in cards, Standard and
/// Prestige/Aluminium (operator, 13 October 2026): any other card is deleted,
/// a missing built-in card is added at the operator's rate, and the enabled
/// flag goes because both are always offered. A repair spec keeps the rate it
/// recorded. Destructive: Down restores the column, not the deleted cards.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261013090000_LabourRateBuiltInCards")]
public partial class LabourRateBuiltInCards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM [LabourRateCards] WHERE [Label] NOT IN (N'Standard', N'Prestige/Aluminium');");
        migrationBuilder.Sql("""
            INSERT INTO [LabourRateCards] ([Id], [Label], [PanelRate], [Active], [Version], [UpdatedBy], [UpdatedAtUtc], [ConcurrencyToken])
            SELECT card.[Id], card.[Label], card.[PanelRate], 1, 1, N'system', SYSUTCDATETIME(), NEWID()
            FROM (VALUES
                (CAST('5b0f3c1e-8f4a-4d2b-9a57-1c3e2f6a7b01' AS uniqueidentifier), N'Standard', CAST(83.28 AS decimal(18,2))),
                (CAST('5b0f3c1e-8f4a-4d2b-9a57-1c3e2f6a7b02' AS uniqueidentifier), N'Prestige/Aluminium', CAST(103.06 AS decimal(18,2)))
            ) AS card ([Id], [Label], [PanelRate])
            WHERE NOT EXISTS (SELECT 1 FROM [LabourRateCards] existing WHERE existing.[Label] = card.[Label]);
            """);
        migrationBuilder.DropColumn(name: "Active", table: "LabourRateCards");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Active", table: "LabourRateCards",
            type: "bit", nullable: false, defaultValue: true);
    }
}
