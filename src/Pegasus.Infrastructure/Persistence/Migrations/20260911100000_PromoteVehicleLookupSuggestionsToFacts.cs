using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// A vehicle lookup now fills the Case's own empty fields as working values
/// carrying Lookup provenance, and the suggestion chip that asked staff to
/// accept them is gone. Every estate case whose lookup ran before that change
/// still holds its findings one tier down, where nothing displays them any
/// more: such a case would go back to reading "Not recorded" for a mileage
/// the lookup already knows.
///
/// So each lookup-sourced suggestion becomes the fact it would be written as
/// today, unless the case has since gained an extracted fact or a staff value
/// for that field — in which case the suggestion lost, and a row nothing can
/// ever read is deleted rather than left behind.
///
/// The manufacture year moves house at the same time. It used to be typed as
/// the assessment path 'vehicle.year' and is now a case-owned field, so each
/// recorded year is carried across and the retired assessment rows are
/// deleted — the path has no reader left, and two homes for one year is how a
/// report and a record come to disagree. A case that never had a typed year
/// but whose lookup did read one gains it as a Lookup-sourced fact, the same
/// row today's lookup would write.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20260911100000_PromoteVehicleLookupSuggestionsToFacts")]
public partial class PromoteVehicleLookupSuggestionsToFacts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (!ActiveProvider.Contains("SqlServer", StringComparison.Ordinal))
            throw new NotSupportedException($"Migration provider '{ActiveProvider}' is not supported.");

        // The literals are CaseDataCodes.Fact, .Suggestion, .Confirmed and
        // .VehicleLookup. Spelled out because a migration describes the rows a
        // past release wrote, and must not move when a constant does.
        migrationBuilder.Sql(
            """
            UPDATE suggestion
            SET suggestion.[ValueKind] = N'fact'
            FROM dbo.CaseDataFields AS suggestion
            WHERE suggestion.[ValueKind] = N'suggestion'
              AND suggestion.[SourceKind] = N'vehicle_lookup'
              AND NOT EXISTS (
                  SELECT 1
                  FROM dbo.CaseDataFields AS existing
                  WHERE existing.[CaseId] = suggestion.[CaseId]
                    AND existing.[FieldName] = suggestion.[FieldName]
                    AND existing.[ValueKind] IN (N'fact', N'confirmed'));
            """);

        // What is left lost to a value the case already holds.
        migrationBuilder.Sql(
            """
            DELETE FROM dbo.CaseDataFields
            WHERE [ValueKind] = N'suggestion'
              AND [SourceKind] = N'vehicle_lookup';
            """);

        // A year a member of staff typed against the retired assessment path
        // is the same year the case-owned field now holds, so it is written
        // there in the shape the case-data editor writes: staff provenance,
        // the case-data edit policy, and confirmed only where it was confirmed.
        migrationBuilder.Sql(
            """
            INSERT INTO dbo.CaseDataFields
                ([CaseId], [FieldName], [ValueKind], [ValueType], [Value],
                 [SourceKind], [SourceIdentity], [SourceLabel], [PolicyKey], [PolicyVersion],
                 [ConfirmedByActor], [ConfirmedAtUtc])
            SELECT assessment.[CaseId],
                   N'vehicle_year',
                   CASE WHEN assessment.[ConfirmedAtUtc] IS NULL THEN N'fact' ELSE N'confirmed' END,
                   N'text',
                   assessment.[Value],
                   N'staff_correction',
                   assessment.[RecordedBy],
                   N'staff case-data confirmation',
                   N'case-data-edit',
                   1,
                   CASE
                       WHEN assessment.[ConfirmedAtUtc] IS NULL THEN NULL
                       ELSE COALESCE(assessment.[ConfirmedBy], assessment.[RecordedBy])
                   END,
                   assessment.[ConfirmedAtUtc]
            FROM dbo.CaseAssessmentFields AS assessment
            WHERE assessment.[FieldPath] = N'vehicle.year'
              -- CaseDataFields is keyed to the case's data snapshot, which an
              -- unaccepted case has not got.
              AND EXISTS (
                  SELECT 1 FROM dbo.CaseDataSnapshots AS snapshot
                  WHERE snapshot.[CaseId] = assessment.[CaseId])
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.CaseDataFields AS existing
                  WHERE existing.[CaseId] = assessment.[CaseId]
                    AND existing.[FieldName] = N'vehicle_year');
            """);

        // The path itself is retired: its rows are now read by nothing.
        migrationBuilder.Sql(
            """
            DELETE FROM dbo.CaseAssessmentFields
            WHERE [FieldPath] = N'vehicle.year';
            """);

        // A case nobody typed a year for still has one if its lookup read it,
        // so the newest observation carrying a year fills the gap exactly as a
        // lookup running today would: a Lookup-sourced fact, identified by the
        // observation it came from.
        migrationBuilder.Sql(
            """
            WITH latest AS (
                SELECT request.[CaseId],
                       observation.[Id],
                       observation.[ManufactureYear],
                       observation.[Provider],
                       observation.[ProviderVersion],
                       ROW_NUMBER() OVER (
                           PARTITION BY request.[CaseId]
                           ORDER BY observation.[RecordedAtUtc] DESC, observation.[Id]) AS rn
                FROM dbo.VehicleLookupObservations AS observation
                INNER JOIN dbo.VehicleLookupRequests AS request
                    ON request.[WorkItemId] = observation.[WorkItemId]
                WHERE request.[CaseId] IS NOT NULL
                  AND observation.[ManufactureYear] IS NOT NULL
            )
            INSERT INTO dbo.CaseDataFields
                ([CaseId], [FieldName], [ValueKind], [ValueType], [Value],
                 [SourceKind], [SourceIdentity], [SourceLabel], [PolicyKey], [PolicyVersion])
            SELECT latest.[CaseId],
                   N'vehicle_year',
                   N'fact',
                   N'text',
                   CONVERT(nvarchar(10), latest.[ManufactureYear]),
                   N'vehicle_lookup',
                   LOWER(CONVERT(nvarchar(36), latest.[Id])),
                   CONCAT(latest.[Provider], N'/', latest.[ProviderVersion]),
                   N'vehicle-lookup-fill',
                   1
            FROM latest
            WHERE latest.rn = 1
              AND EXISTS (
                  SELECT 1 FROM dbo.CaseDataSnapshots AS snapshot
                  WHERE snapshot.[CaseId] = latest.[CaseId])
              AND NOT EXISTS (
                  SELECT 1 FROM dbo.CaseDataFields AS existing
                  WHERE existing.[CaseId] = latest.[CaseId]
                    AND existing.[FieldName] = N'vehicle_year');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException(
            "Promoting lookup suggestions to facts is forward-only: the suggestion tier no "
            + "longer exists for vehicle lookups, so there is nothing to restore them to.");
}
