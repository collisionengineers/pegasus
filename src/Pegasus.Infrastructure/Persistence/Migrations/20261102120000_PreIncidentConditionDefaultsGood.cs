using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// Every Case defaults to a Good pre-incident condition and there is no Not
/// recorded choice (R598, 2 November 2026). A new Case records Good when it is
/// created (<c>NewCaseAssessmentDefaults</c>); this gives every existing work,
/// primary and Audit, open or closed, the same Good row where it records no
/// condition, under the default's recorder so the Case page shows it
/// untagged. Additive data: no recorded condition changes, no schema changes,
/// and a frozen report keeps what it printed.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261102120000_PreIncidentConditionDefaultsGood")]
public partial class PreIncidentConditionDefaultsGood : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            INSERT INTO [CaseAssessmentFields] ([WorkId], [FieldPath], [Value], [RecordedByKind], [RecordedBy], [RecordedAtUtc])
            SELECT [w].[Id], N'vehicle.condition', N'good', N'Automation', N'vehicle-condition-default', SYSDATETIMEOFFSET()
            FROM [CaseWorks] AS [w]
            WHERE NOT EXISTS (
                SELECT 1 FROM [CaseAssessmentFields] AS [f]
                WHERE [f].[WorkId] = [w].[Id] AND [f].[FieldPath] = N'vehicle.condition');
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            DELETE FROM [CaseAssessmentFields]
            WHERE [FieldPath] = N'vehicle.condition' AND [RecordedBy] = N'vehicle-condition-default';
            """);
}
