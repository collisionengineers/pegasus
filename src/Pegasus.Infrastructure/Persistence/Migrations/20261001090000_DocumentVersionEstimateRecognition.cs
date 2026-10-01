using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Whether the Worker read a document version as an estimate (1 October
/// 2026): NULL until it has. Additive; the runtime roles' table-level grants
/// on <c>DocumentVersions</c> cover the column.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261001090000_DocumentVersionEstimateRecognition")]
public partial class DocumentVersionEstimateRecognition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "IsRecognisedEstimate", table: "DocumentVersions", type: "bit", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "IsRecognisedEstimate", table: "DocumentVersions");
    }
}
