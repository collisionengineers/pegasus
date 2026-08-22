using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // ENG-013 made a vehicle lookup fill the case's own empty fields, but only
    // at the moment the lookup runs. Every case already in the estate had its
    // lookup before that shipped, so its findings still sit only on the
    // observation: QDOS26011 reads "Not recorded" for mileage while its
    // observation holds 121,823 miles from the latest MOT. That is exactly the
    // gap the ticket exists to close, so the recorded past is corrected the
    // same way DOCS-009 corrected semantic roles.
    //
    // Writes the same four fields the code writes, at the same suggestion tier,
    // from the most recent observation per case. A suggestion is outranked by
    // an extracted fact and a staff-confirmed value, so this can never displace
    // what a case already knows - QDOS26010 keeps its extracted 132,389 and
    // gains the lookup's 128,343 only as the suggestion behind it.
    public partial class BackfillVehicleLookupSuggestions : Migration
    {
        // The latest observation per case that actually carries values. A case
        // can hold several attempts; only the newest is a candidate.
        private const string LatestObservation = """
            WITH latest AS (
                SELECT r.[CaseId], o.[Id], o.[Make], o.[Model], o.[MileageValue],
                       o.[MileageUnit], o.[MileageMethodKey], o.[MileageMethodVersion],
                       o.[Provider], o.[ProviderVersion],
                       ROW_NUMBER() OVER (PARTITION BY r.[CaseId] ORDER BY o.[RetrievedAtUtc] DESC, o.[Id]) AS rn
                FROM [dbo].[VehicleLookupObservations] AS o
                INNER JOIN [dbo].[VehicleLookupRequests] AS r ON r.[WorkItemId] = o.[WorkItemId]
                WHERE r.[CaseId] IS NOT NULL
            )
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make and model carry this rule's own key; the mileage carries the
            // calculation's, because that is what classifies it as a derived
            // estimate wherever it is later shown (ENG-010).
            Backfill(migrationBuilder, "vehicle_make", "text", "[Make]", "'vehicle-lookup-gap-fill'", "1");
            Backfill(migrationBuilder, "vehicle_model", "text", "[Model]", "'vehicle-lookup-gap-fill'", "1");
            Backfill(
                migrationBuilder,
                "vehicle_mileage",
                "integer",
                "CONVERT(nvarchar(40), [MileageValue])",
                "[MileageMethodKey]",
                "[MileageMethodVersion]");
            Backfill(
                migrationBuilder,
                "vehicle_mileage_unit",
                "text",
                "[MileageUnit]",
                "[MileageMethodKey]",
                "[MileageMethodVersion]");
        }

        /// <inheritdoc />
        // Removes only the suggestion rows this migration could have written -
        // lookup-sourced, never a fact or a confirmed value, so nothing an
        // operator or an extraction established is touched.
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql(
                """
                DELETE FROM [dbo].[CaseDataFields]
                WHERE [ValueKind] = N'suggestion'
                  AND [SourceKind] = N'vehicle_lookup'
                  AND [FieldName] IN (N'vehicle_make', N'vehicle_model', N'vehicle_mileage', N'vehicle_mileage_unit');
                """);

        private static void Backfill(
            MigrationBuilder migrationBuilder,
            string fieldName,
            string valueType,
            string valueExpression,
            string policyKeyExpression,
            string policyVersionExpression) =>
            migrationBuilder.Sql(
                $"""
                {LatestObservation}
                INSERT INTO [dbo].[CaseDataFields]
                    ([CaseId], [FieldName], [ValueKind], [ValueType], [Value],
                     [SourceKind], [SourceIdentity], [SourceLabel], [PolicyKey], [PolicyVersion])
                SELECT latest.[CaseId], N'{fieldName}', N'suggestion', N'{valueType}',
                       {valueExpression},
                       N'vehicle_lookup',
                       CONVERT(nvarchar(50), latest.[Id]),
                       CONCAT(latest.[Provider], N'/', latest.[ProviderVersion]),
                       {policyKeyExpression},
                       {policyVersionExpression}
                FROM latest
                WHERE latest.rn = 1
                  AND {valueExpression} IS NOT NULL
                  AND {policyKeyExpression} IS NOT NULL
                  AND {policyVersionExpression} > 0
                  -- A case must already hold a data snapshot: CaseDataFields is
                  -- keyed to it, and an unaccepted case has none.
                  AND EXISTS (
                      SELECT 1 FROM [dbo].[CaseDataSnapshots] AS s
                      WHERE s.[CaseId] = latest.[CaseId])
                  -- Never a second row for a field this case already carries at
                  -- the suggestion tier; facts and confirmed values are keyed
                  -- separately and are left alone by construction.
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[CaseDataFields] AS existing
                      WHERE existing.[CaseId] = latest.[CaseId]
                        AND existing.[FieldName] = N'{fieldName}'
                        AND existing.[ValueKind] = N'suggestion');
                """);
    }
}
