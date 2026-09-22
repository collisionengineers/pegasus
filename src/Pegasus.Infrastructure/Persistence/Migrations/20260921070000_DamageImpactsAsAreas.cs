using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Damage is recorded by area (v28 P5, ruled 20 September 2026): each impact
/// names the plan areas under its disc, or Underside, Interior or Mechanical
/// alone, instead of one diagram zone. A recorded zone becomes the area that
/// held it; the roof, which the plan no longer shows, becomes the complete
/// canonical plan-area closure.
/// Forward only: the zone a row named is not kept.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260921070000_DamageImpactsAsAreas")]
public partial class DamageImpactsAsAreas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            UPDATE f
            SET [Value] = ISNULL(rewritten.Impacts, '[]')
            FROM CaseAssessmentFields f
            CROSS APPLY (
                SELECT '[' + STRING_AGG(CONVERT(nvarchar(max),
                           '{"areas":[' + CASE z.zone
                               WHEN 'front_centre' THEN '"front"' WHEN 'bonnet' THEN '"front"' WHEN 'windscreen' THEN '"front"'
                               WHEN 'front_left_corner' THEN '"left_front"' WHEN 'left_front_wing' THEN '"left_front"' WHEN 'wheel_left_front' THEN '"left_front"'
                               WHEN 'front_right_corner' THEN '"right_front"' WHEN 'right_front_wing' THEN '"right_front"' WHEN 'wheel_right_front' THEN '"right_front"'
                               WHEN 'left_front_door' THEN '"left_side"' WHEN 'left_rear_door' THEN '"left_side"'
                               WHEN 'right_front_door' THEN '"right_side"' WHEN 'right_rear_door' THEN '"right_side"'
                               WHEN 'rear_centre' THEN '"rear"' WHEN 'rear_screen' THEN '"rear"' WHEN 'tailgate' THEN '"rear"'
                               WHEN 'left_quarter' THEN '"left_rear"' WHEN 'rear_left_corner' THEN '"left_rear"' WHEN 'wheel_left_rear' THEN '"left_rear"'
                               WHEN 'right_quarter' THEN '"right_rear"' WHEN 'rear_right_corner' THEN '"right_rear"' WHEN 'wheel_right_rear' THEN '"right_rear"'
                               WHEN 'roof' THEN '"front","left_front","right_front","left_side","right_side","rear","left_rear","right_rear"'
                               ELSE '"' + STRING_ESCAPE(z.zone, 'json') + '"' END
                           + '],"severity":"' + STRING_ESCAPE(z.severity, 'json') + '","note":"' + STRING_ESCAPE(ISNULL(z.note, ''), 'json') + '"}'), ',')
                           WITHIN GROUP (ORDER BY CAST(o.[key] AS int)) + ']' AS Impacts
                FROM OPENJSON(f.[Value]) o
                CROSS APPLY OPENJSON(o.[value]) WITH (zone nvarchar(100) '$.zone', severity nvarchar(100) '$.severity', note nvarchar(400) '$.note') z
            ) rewritten
            WHERE f.FieldPath = 'damage.impacts' AND ISJSON(f.[Value]) = 1 AND f.[Value] LIKE '%"zone"%';
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Damage areas replace diagram zones; the zone each impact named is not kept.");
}
