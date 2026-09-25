using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20260926090000_RepairSpecInUseOnCreate: a spec is live (Draft) or
/// Discarded, so Accepted and Superseded rows become Draft with their Current
/// choice kept, the unused routes become Manual, and the acceptance,
/// supersession and frozen-calculation columns go, with the line status and
/// its write-only off-pattern copy.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RepairSpecInUseOnCreateMigrationTests
{
    private const string CaseWorksBaseline = "20260924090000_IntakeAssetBoxParentFolder";
    private const string PreviousMigration = "20260925190000_EditLeaseTakeoverHistoryEvents";
    private const string CaseId = "a5000000-0000-0000-0000-000000000001";
    private const string AcceptedId = "a6000000-0000-0000-0000-000000000001";
    private const string SupersededId = "a6000000-0000-0000-0000-000000000002";
    private const string LegacyId = "a6000000-0000-0000-0000-000000000003";
    private const string ProposalId = "a6000000-0000-0000-0000-000000000004";
    private const string DiscardedId = "a6000000-0000-0000-0000-000000000005";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    private static readonly string[] DroppedSpecificationColumns =
    [
        "AcceptedAtUtc",
        "AcceptedBy",
        "CalculationBreakdownJson",
        "CalculationLabour",
        "CalculationPaintMaterials",
        "CalculationParts",
        "CalculationPolicyVersion",
        "CalculationSpecialistOther",
        "CalculationTotal",
        "CalculationVat",
        "RepairerVatRegistered",
        "SupersedesSpecificationId",
        "SupersessionReason",
        "VatOverrideReason"
    ];

    [Fact]
    public async Task MigrationKeepsEveryLiveSpecAsADraftWithItsCurrentChoice()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(CaseWorksBaseline);
        await database.ExecuteAsync(CaseSql);
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(SpecificationsSql);

        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.Equal(5, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseRepairSpecifications WHERE WorkId = '{CaseId}'"));
        Assert.Equal(4, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseRepairSpecifications WHERE WorkId = '{CaseId}' AND State = N'Draft'"));
        Assert.Equal("Discarded", await database.ScalarAsync<string>(
            $"SELECT State FROM CaseRepairSpecifications WHERE Id = '{DiscardedId}'"));
        // The Current choice survives: the accepted spec in use is still the one in use.
        Assert.Equal(AcceptedId, await database.ScalarAsync<string>(
            $"SELECT CONVERT(nvarchar(36), Id) FROM CaseRepairSpecifications WHERE WorkId = '{CaseId}' AND IsCurrent = 1"),
            ignoreCase: true);
        Assert.Equal("Manual", await database.ScalarAsync<string>(
            $"SELECT SourceRoute FROM CaseRepairSpecifications WHERE Id = '{LegacyId}'"));
        Assert.Equal("Manual", await database.ScalarAsync<string>(
            $"SELECT SourceRoute FROM CaseRepairSpecifications WHERE Id = '{ProposalId}'"));
        Assert.Equal("Glasses", await database.ScalarAsync<string>(
            $"SELECT SourceRoute FROM CaseRepairSpecifications WHERE Id = '{AcceptedId}'"));
        // The lines and their values stay; only the status and the off-pattern copy go.
        Assert.Equal(125.00m, await database.ScalarAsync<decimal>(
            $"SELECT Price FROM CaseEstimateLines WHERE RepairSpecificationId = '{AcceptedId}'"));

        foreach (var column in DroppedSpecificationColumns)
        {
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'CaseRepairSpecifications') AND name = N'{column}'"));
        }
        foreach (var column in new[] { "Status", "CurrentValuesJson" })
        {
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'CaseEstimateLines') AND name = N'{column}'"));
        }
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name IN (N'CK_CaseRepairSpecifications_Acceptance', N'CK_CaseEstimateLines_Status')"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name = N'CK_CaseRepairSpecifications_Discard'"));

        // The narrowed constraints refuse the retired state and route.
        await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            $"UPDATE CaseRepairSpecifications SET State = N'Accepted' WHERE Id = '{LegacyId}'"));
        await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            $"UPDATE CaseRepairSpecifications SET SourceRoute = N'LegacyUnresolved' WHERE Id = '{LegacyId}'"));
    }

    private const string CaseSql =
        $"""
        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('a5000000-0000-0000-0000-000000000011', N'Repair spec migration provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('a5000000-0000-0000-0000-000000000012', '{Recorded}');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
        VALUES
            ('a5000000-0000-0000-0000-000000000013', 'a5000000-0000-0000-0000-000000000011', N'RSMG',
             'a5000000-0000-0000-0000-000000000012', NULL, NULL, 1, 0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{CaseId}', 'a5000000-0000-0000-0000-000000000013', 'a5000000-0000-0000-0000-000000000012',
             2031, 1, N'RSMG31001', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID());
        """;

    // The primary work's id is the Case's own (CaseWorksAndTriageCases).
    private const string SpecificationsSql =
        $"""
        INSERT INTO CaseRepairSpecifications
            (Id, WorkId, Version, State, SourceRoute, SourceArtifactReference, SourceVersion, SourceSha256,
             Name, IsCurrent, RepairerVatStatus, VatPercent, RegionalUplift, SupplementaryExplainOnReport,
             CreatedBy, CreationOperationKey, CreatedAtUtc, AcceptedBy, AcceptedAtUtc,
             CalculationLabour, CalculationParts, CalculationPaintMaterials, CalculationSpecialistOther,
             RepairerVatRegistered, CalculationVat, CalculationTotal, CalculationPolicyVersion,
             CalculationBreakdownJson, SupersedesSpecificationId, SupersessionReason,
             DiscardedBy, DiscardedAtUtc, DiscardReason)
        VALUES
            ('{SupersededId}', '{CaseId}', 1, N'Superseded', N'Manual', NULL, NULL, NULL,
             N'Earlier', 0, N'Unknown', 20, 0, 0,
             N'engineer', N'spec-1', '{Recorded}', N'engineer', '{Recorded}',
             100, 0, 0, 0, 1, 20, 120, N'repair-specification/v4', NULL, NULL, NULL,
             NULL, NULL, NULL),
            ('{AcceptedId}', '{CaseId}', 2, N'Accepted', N'Glasses', N'estimate-import:migration', N'v1', REPLICATE(N'a', 64),
             N'Glass''s 1', 1, N'Registered', 20, 0, 0,
             N'engineer', N'spec-2', '{Recorded}', N'engineer', '{Recorded}',
             100, 125, 0, 0, 1, 45, 270, N'repair-specification/v4', NULL, '{SupersededId}', N'Revised',
             NULL, NULL, NULL),
            ('{LegacyId}', '{CaseId}', 3, N'Draft', N'LegacyUnresolved', NULL, NULL, NULL,
             N'Estimate 3', 0, N'Unknown', 20, 0, 0,
             N'engineer', N'spec-3', '{Recorded}', NULL, NULL,
             NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
             NULL, NULL, NULL),
            ('{ProposalId}', '{CaseId}', 4, N'Draft', N'ApprovedAiProposal', N'proposal:1', N'v1', REPLICATE(N'b', 64),
             N'Proposal', 0, N'Unknown', 20, 0, 0,
             N'engineer', N'spec-4', '{Recorded}', NULL, NULL,
             NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
             NULL, NULL, NULL),
            ('{DiscardedId}', '{CaseId}', 5, N'Discarded', N'Manual', NULL, NULL, NULL,
             N'Wrong', 0, N'Unknown', 20, 0, 0,
             N'engineer', N'spec-5', '{Recorded}', NULL, NULL,
             NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
             N'engineer', '{Recorded}', N'Entered in error');

        INSERT INTO CaseEstimateLines
            (Id, WorkId, RepairSpecificationId, Position, LineType, Description, Price, Unpriced, Status,
             CurrentValuesJson, RecordedByKind, RecordedBy, RecordedAtUtc)
        VALUES
            (NEWID(), '{CaseId}', '{AcceptedId}', 1, N'new_part', N'Front bumper', 125.00, 0, N'provisional',
             N'[]', N'Staff', N'engineer', '{Recorded}');
        """;
}
