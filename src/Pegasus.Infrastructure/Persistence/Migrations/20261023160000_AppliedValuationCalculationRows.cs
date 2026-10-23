using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pegasus.Infrastructure.Persistence.Migrations;

/// <summary>
/// An applied valuation's calculation, and the additions (the applied presets)
/// within it, as rows beside the snapshot's JSON (register A4-03, R352). Every
/// read of the applied valuations, and the basis of a new calculation, opened
/// and deserialised every snapshot's JSON; they now read these rows, and the
/// JSON stays the immutable record the hash covers. Additive: two new tables,
/// filled from the existing snapshots here (a snapshot the JSON cannot fill
/// stops the migration rather than being given an invented value), written
/// from now on beside each snapshot. Web writes and reads them as it does the
/// snapshot table: SELECT and INSERT, no DELETE for either role.
/// </summary>
[DbContext(typeof(PegasusDbContext))]
[Migration("20261023160000_AppliedValuationCalculationRows")]
public partial class AppliedValuationCalculationRows : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppliedValuationCalculations",
            columns: table => new
            {
                SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CaseVersion = table.Column<long>(type: "bigint", nullable: false),
                GuideValuationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                GuideValuationStampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                GuideRetailValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                CommercialVatApplied = table.Column<bool>(type: "bit", nullable: false),
                CommercialVatAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                ValueIncludingVat = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                PriorTotalLossPercentage = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                PriorTotalLossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                AdditionsTotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                ConditionDeduction = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                Proposal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppliedValuationCalculations", x => x.SnapshotId);
                table.ForeignKey(
                    name: "FK_AppliedValuationCalculations_AppliedValuationSnapshots_SnapshotId",
                    column: x => x.SnapshotId,
                    principalTable: "AppliedValuationSnapshots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "AppliedValuationAdditions",
            columns: table => new
            {
                SnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Ordinal = table.Column<int>(type: "int", nullable: false),
                PresetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PresetVersion = table.Column<long>(type: "bigint", nullable: false),
                Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SuggestedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppliedValuationAdditions", x => new { x.SnapshotId, x.Ordinal });
                table.ForeignKey(
                    name: "FK_AppliedValuationAdditions_AppliedValuationSnapshots_SnapshotId",
                    column: x => x.SnapshotId,
                    principalTable: "AppliedValuationSnapshots",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        if (ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;

        // The snapshot JSON is the application's own camelCase serialisation
        // of AppliedValuationSnapshot. A missing key leaves a NOT NULL column
        // empty and stops the migration.
        migrationBuilder.Sql(
            """
            INSERT INTO dbo.AppliedValuationCalculations
                ([SnapshotId], [CaseVersion], [GuideValuationId], [GuideValuationStampUtc],
                 [GuideRetailValue], [CommercialVatApplied], [CommercialVatAmount],
                 [ValueIncludingVat], [PriorTotalLossPercentage], [PriorTotalLossAmount],
                 [AdditionsTotal], [ConditionDeduction], [Proposal])
            SELECT snapshot.[Id], parsed.[CaseVersion], parsed.[GuideValuationId],
                parsed.[GuideValuationStampUtc], parsed.[GuideRetailValue],
                parsed.[CommercialVatApplied], parsed.[CommercialVatAmount],
                parsed.[ValueIncludingVat], parsed.[PriorTotalLossPercentage],
                parsed.[PriorTotalLossAmount], parsed.[AdditionsTotal],
                parsed.[ConditionDeduction], parsed.[Proposal]
            FROM dbo.AppliedValuationSnapshots AS snapshot
            CROSS APPLY OPENJSON(snapshot.[SnapshotJson]) WITH (
                [CaseVersion] bigint '$.caseVersion',
                [GuideValuationId] uniqueidentifier '$.guideValuationId',
                [GuideValuationStampUtc] datetimeoffset '$.guideValuationStampUtc',
                [GuideRetailValue] decimal(18,2) '$.calculation.guideRetailValue',
                [CommercialVatApplied] bit '$.calculation.commercialVatApplied',
                [CommercialVatAmount] decimal(18,2) '$.calculation.commercialVatAmount',
                [ValueIncludingVat] decimal(18,2) '$.calculation.valueIncludingVat',
                [PriorTotalLossPercentage] decimal(18,2) '$.calculation.priorTotalLossPercentage',
                [PriorTotalLossAmount] decimal(18,2) '$.calculation.priorTotalLossAmount',
                [AdditionsTotal] decimal(18,2) '$.calculation.additionsTotal',
                [ConditionDeduction] decimal(18,2) '$.calculation.conditionDeduction',
                [Proposal] decimal(18,2) '$.calculation.proposal') AS parsed;

            INSERT INTO dbo.AppliedValuationAdditions
                ([SnapshotId], [Ordinal], [PresetId], [PresetVersion], [Label],
                 [SuggestedAmount], [Amount])
            SELECT snapshot.[Id], CONVERT(int, entry.[key]), parsed.[PresetId],
                parsed.[PresetVersion], parsed.[Label], parsed.[SuggestedAmount],
                parsed.[Amount]
            FROM dbo.AppliedValuationSnapshots AS snapshot
            CROSS APPLY OPENJSON(snapshot.[SnapshotJson], '$.calculation.additions') AS entry
            CROSS APPLY OPENJSON(entry.[value]) WITH (
                [PresetId] uniqueidentifier '$.presetId',
                [PresetVersion] bigint '$.presetVersion',
                [Label] nvarchar(200) '$.label',
                [SuggestedAmount] decimal(18,2) '$.suggestedAmount',
                [Amount] decimal(18,2) '$.amount') AS parsed;
            """);

        // The same posture as AppliedValuationSnapshots (20260906054658_V1PlatformFoundation):
        // Web reads and inserts, nothing is updated or deleted, the Worker has no caller.
        migrationBuilder.Sql(
            """
            IF DATABASE_PRINCIPAL_ID(N'pegasus_web_runtime_role') IS NOT NULL
            BEGIN
                GRANT SELECT, INSERT ON OBJECT::[dbo].[AppliedValuationCalculations] TO [pegasus_web_runtime_role];
                GRANT SELECT, INSERT ON OBJECT::[dbo].[AppliedValuationAdditions] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[AppliedValuationCalculations] TO [pegasus_web_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[AppliedValuationAdditions] TO [pegasus_web_runtime_role];
            END;
            IF DATABASE_PRINCIPAL_ID(N'pegasus_worker_runtime_role') IS NOT NULL
            BEGIN
                DENY DELETE ON OBJECT::[dbo].[AppliedValuationCalculations] TO [pegasus_worker_runtime_role];
                DENY DELETE ON OBJECT::[dbo].[AppliedValuationAdditions] TO [pegasus_worker_runtime_role];
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AppliedValuationAdditions");

        migrationBuilder.DropTable(
            name: "AppliedValuationCalculations");
    }
}
