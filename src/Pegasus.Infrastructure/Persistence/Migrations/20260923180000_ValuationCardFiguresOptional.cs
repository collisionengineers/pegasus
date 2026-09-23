using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A guide source's valuation card holds whatever staff entered, so its
/// mileage, retail and trade may be absent (operator, 23 September 2026).
/// Additive: the columns only become nullable; every recorded row keeps its
/// figures. The non-negative checks are dropped around the change and
/// re-added unchanged, since a check constraint pins the column it reads.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260923180000_ValuationCardFiguresOptional")]
public partial class ValuationCardFiguresOptional : Migration
{
    private static readonly (string Constraint, string Column)[] Checks =
    [
        ("CK_CaseValuations_Mileage", "Mileage"),
        ("CK_CaseValuations_RetailValue", "RetailValue"),
        ("CK_CaseValuations_TradeValue", "TradeValue")
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        DropChecks(migrationBuilder);
        migrationBuilder.AlterColumn<long>(
            name: "Mileage",
            table: "CaseValuations",
            type: "bigint",
            nullable: true,
            oldClrType: typeof(long),
            oldType: "bigint");
        migrationBuilder.AlterColumn<decimal>(
            name: "RetailValue",
            table: "CaseValuations",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,2)",
            oldPrecision: 18,
            oldScale: 2);
        migrationBuilder.AlterColumn<decimal>(
            name: "TradeValue",
            table: "CaseValuations",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: true,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,2)",
            oldPrecision: 18,
            oldScale: 2);
        AddChecks(migrationBuilder);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropChecks(migrationBuilder);
        migrationBuilder.AlterColumn<long>(
            name: "Mileage",
            table: "CaseValuations",
            type: "bigint",
            nullable: false,
            defaultValue: 0L,
            oldClrType: typeof(long),
            oldType: "bigint",
            oldNullable: true);
        migrationBuilder.AlterColumn<decimal>(
            name: "RetailValue",
            table: "CaseValuations",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,2)",
            oldPrecision: 18,
            oldScale: 2,
            oldNullable: true);
        migrationBuilder.AlterColumn<decimal>(
            name: "TradeValue",
            table: "CaseValuations",
            type: "decimal(18,2)",
            precision: 18,
            scale: 2,
            nullable: false,
            defaultValue: 0m,
            oldClrType: typeof(decimal),
            oldType: "decimal(18,2)",
            oldPrecision: 18,
            oldScale: 2,
            oldNullable: true);
        AddChecks(migrationBuilder);
    }

    private static void DropChecks(MigrationBuilder migrationBuilder)
    {
        foreach (var (constraint, _) in Checks)
        {
            migrationBuilder.DropCheckConstraint(name: constraint, table: "CaseValuations");
        }
    }

    private static void AddChecks(MigrationBuilder migrationBuilder)
    {
        foreach (var (constraint, column) in Checks)
        {
            migrationBuilder.AddCheckConstraint(
                name: constraint,
                table: "CaseValuations",
                sql: $"[{column}] >= 0");
        }
    }
}
