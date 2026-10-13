using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Guidance for new Cases is removed (operator, 13 October 2026): a Principal's or
/// Claim source's standing text is its Notes on every Case, read live. The two
/// columns go; the guidance lines already in Cases' notes timelines stay as recorded.
/// Destructive and forward-only.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261013092000_RemoveContactGuidance")]
public partial class RemoveContactGuidance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "GuidanceTemplate", table: "Organizations");
        migrationBuilder.DropColumn(name: "GuidanceTemplateVersion", table: "Organizations");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "RemoveContactGuidance is forward-only: it drops Organizations.GuidanceTemplate and GuidanceTemplateVersion. Restore from an approved backup instead.");
}
