using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A lookup observation keeps the MOT mileage estimate for its retrieval date
/// (FRD-06; operator, 16 October 2026), and a Case save that replaces the Case
/// mileage the estimate filled records its disposition on the same row. Additive:
/// one nullable column, null on every observation recorded before it. The Worker
/// already inserts observations. The Web role held SELECT only, so it gains
/// UPDATE on the table for the disposition; the bootstrap audit rejects column
/// grants. Neither role deletes an observation.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261017100000_VehicleLookupMileageEstimate")]
public partial class VehicleLookupMileageEstimate : Migration
{
    private const string WebRole = "pegasus_web_runtime_role";

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "MileageEstimateJson",
            table: "VehicleLookupObservations",
            type: "nvarchar(max)",
            nullable: true);

        if (!IsSqlServer())
        {
            return;
        }

        RequireWebRole(migrationBuilder);
        migrationBuilder.Sql(
            $"GRANT UPDATE ON OBJECT::[dbo].[VehicleLookupObservations] TO [{WebRole}];");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        if (IsSqlServer())
        {
            migrationBuilder.Sql(
                $"REVOKE UPDATE ON OBJECT::[dbo].[VehicleLookupObservations] FROM [{WebRole}];");
        }

        migrationBuilder.DropColumn(name: "MileageEstimateJson", table: "VehicleLookupObservations");
    }

    private bool IsSqlServer() =>
        string.Equals(
            ActiveProvider,
            "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.Ordinal);

    private static void RequireWebRole(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            IF NOT EXISTS (
                SELECT 1 FROM sys.database_principals
                WHERE name = N'pegasus_web_runtime_role'
                  AND [type] = 'R'
                  AND is_fixed_role = 0
                  AND owning_principal_id = DATABASE_PRINCIPAL_ID(N'dbo'))
                THROW 51000, 'The fixed Pegasus Web runtime role is missing or invalid.', 1;
            """);
}
