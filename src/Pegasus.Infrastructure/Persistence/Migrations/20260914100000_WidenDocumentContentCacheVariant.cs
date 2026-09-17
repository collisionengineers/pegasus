using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A prepared thumbnail's four seven-decimal crop coordinates produce a
/// 53-character variant token. Cache rows need room for the complete identity
/// so the existing per-source variant indexes remain usable.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260914100000_WidenDocumentContentCacheVariant")]
public partial class WidenDocumentContentCacheVariant : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        migrationBuilder.AlterColumn<string>(
            name: "Variant",
            table: "DocumentContentCacheEntries",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Variant",
            table: "DocumentContentCacheEntries",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64);
    }
}
