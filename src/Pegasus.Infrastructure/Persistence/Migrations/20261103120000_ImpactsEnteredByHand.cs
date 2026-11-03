using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Impacts are entered by hand and the damage markers are positions only (v46,
/// operator, 3 November 2026; superseding R639). Every stored
/// <c>damage.impacts</c> becomes its markers' discs, in order: a drawn disc is
/// kept as drawn, a damage recorded by plan area alone becomes the disc its
/// areas were drawn with (centred between the areas' centres and reaching
/// them, as <c>DamageAreaGeometry.Disc</c> drew it before this release), and
/// an Underside, Interior or Mechanical damage, which had no disc, is dropped.
/// The derived <c>assessment.impact_location</c> and
/// <c>assessment.impact_severity</c> rows are deleted, and no Case starts with
/// an entered impact (v46 item F). Forward only: each damage's areas,
/// severity and note are not kept. A frozen report keeps what it printed.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261103120000_ImpactsEnteredByHand")]
public partial class ImpactsEnteredByHand : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            SELECT [f].[WorkId], CAST([o].[key] AS int) AS [Ordinal], [o].[value] AS [Entry]
            INTO #DamageEntries
            FROM [CaseAssessmentFields] AS [f]
            CROSS APPLY OPENJSON([f].[Value]) AS [o]
            WHERE [f].[FieldPath] = N'damage.impacts' AND ISJSON([f].[Value]) = 1;

            CREATE TABLE #DamageMarkers ([WorkId] uniqueidentifier NOT NULL, [Ordinal] int NOT NULL, [X] float NOT NULL, [Y] float NOT NULL, [R] float NOT NULL);

            INSERT INTO #DamageMarkers ([WorkId], [Ordinal], [X], [Y], [R])
            SELECT [e].[WorkId], [e].[Ordinal],
                   CAST(JSON_VALUE([e].[Entry], '$.disc.x') AS float),
                   CAST(JSON_VALUE([e].[Entry], '$.disc.y') AS float),
                   CAST(JSON_VALUE([e].[Entry], '$.disc.r') AS float)
            FROM #DamageEntries AS [e]
            WHERE JSON_QUERY([e].[Entry], '$.disc') IS NOT NULL;

            ;WITH [Centres] ([Area], [X], [Y]) AS
            (
                SELECT [Area], [X], [Y] FROM (VALUES
                    (N'front', 0.5E0, 0.1E0), (N'left_front', 0.18E0, 0.2E0), (N'right_front', 0.82E0, 0.2E0),
                    (N'left_side', 0.1E0, 0.53E0), (N'right_side', 0.9E0, 0.53E0), (N'rear', 0.5E0, 0.92E0),
                    (N'left_rear', 0.18E0, 0.84E0), (N'right_rear', 0.82E0, 0.84E0)) AS [c] ([Area], [X], [Y])
            ),
            [AreaPoints] AS
            (
                SELECT [e].[WorkId], [e].[Ordinal], [c].[X], [c].[Y]
                FROM #DamageEntries AS [e]
                CROSS APPLY OPENJSON([e].[Entry], '$.areas') AS [a]
                INNER JOIN [Centres] AS [c] ON [c].[Area] = [a].[value]
                WHERE JSON_QUERY([e].[Entry], '$.disc') IS NULL
            ),
            [Means] AS
            (
                SELECT [WorkId], [Ordinal], AVG([X]) AS [CX], AVG([Y]) AS [CY]
                FROM [AreaPoints]
                GROUP BY [WorkId], [Ordinal]
            ),
            [Spreads] AS
            (
                -- The spread is measured on the canonical plan, one unit wide
                -- and 394/156 tall, in widths; the margin is 0.08 and the disc
                -- is clamped between the one-area radius 0.12 and 0.5.
                SELECT [m].[WorkId], [m].[Ordinal], [m].[CX], [m].[CY],
                       MAX(SQRT(SQUARE([p].[X] - [m].[CX]) + SQUARE(([p].[Y] - [m].[CY]) * 394E0 / 156E0))) + 0.08E0 AS [Reach]
                FROM [Means] AS [m]
                INNER JOIN [AreaPoints] AS [p] ON [p].[WorkId] = [m].[WorkId] AND [p].[Ordinal] = [m].[Ordinal]
                GROUP BY [m].[WorkId], [m].[Ordinal], [m].[CX], [m].[CY]
            )
            INSERT INTO #DamageMarkers ([WorkId], [Ordinal], [X], [Y], [R])
            SELECT [WorkId], [Ordinal], [CX], [CY],
                   CASE WHEN [Reach] < 0.12E0 THEN 0.12E0 WHEN [Reach] > 0.5E0 THEN 0.5E0 ELSE [Reach] END
            FROM [Spreads];

            UPDATE [f]
            SET [Value] = ISNULL((
                SELECT N'[' + STRING_AGG(CONVERT(nvarchar(max),
                           N'{"x":' + CONVERT(nvarchar(20), CAST(ROUND(CAST([m].[X] AS decimal(18, 10)), 4) AS decimal(9, 4)))
                           + N',"y":' + CONVERT(nvarchar(20), CAST(ROUND(CAST([m].[Y] AS decimal(18, 10)), 4) AS decimal(9, 4)))
                           + N',"r":' + CONVERT(nvarchar(20), CAST(ROUND(CAST([m].[R] AS decimal(18, 10)), 4) AS decimal(9, 4)))
                           + N'}'), N',') WITHIN GROUP (ORDER BY [m].[Ordinal]) + N']'
                FROM #DamageMarkers AS [m]
                WHERE [m].[WorkId] = [f].[WorkId]), N'[]')
            FROM [CaseAssessmentFields] AS [f]
            WHERE [f].[FieldPath] = N'damage.impacts' AND ISJSON([f].[Value]) = 1;

            DELETE FROM [CaseAssessmentFields]
            WHERE [FieldPath] IN (N'assessment.impact_location', N'assessment.impact_severity');

            DROP TABLE #DamageMarkers;
            DROP TABLE #DamageEntries;
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Impacts entered by hand replace the derived impact; each damage's areas, severity and note are not kept.");
}
