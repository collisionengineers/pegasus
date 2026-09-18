using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

[DbContext(typeof(PegasusDbContext))]
[Migration("20260918090000_RemoveAdministrationEditScopes")]
public partial class RemoveAdministrationEditScopes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DELETE FROM [dbo].[EditScopes]
            WHERE [ScopeKind] IN (
                N'Contact',
                N'StaffAccount',
                N'ValuationPreset',
                N'ApprovedMailbox',
                N'ApprovedOutlookCategory',
                N'NamedConfiguration',
                N'LabourRateCard'
            );
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Retired transient administration scopes cannot be restored safely.");
}
