using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

/// <summary>
/// 20260924180000_CaseWorksAndTriageCases: it refuses data the new schema
/// cannot carry, gives every Case its primary work, moves the per-work rows
/// onto that work without changing them, and cannot be reverted.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseWorksMigrationTests
{
    private const string PreviousMigration = "20260924090000_IntakeAssetBoxParentFolder";
    private const string CaseWorksMigration = "20260924180000_CaseWorksAndTriageCases";
    private const string ResetInstruction =
        "Run the approved pegasus-wipe-intake-data reset before applying CaseWorksAndTriageCases.";
    private const string CaseId = "a1000000-0000-0000-0000-000000000001";
    private const string SecondCaseId = "a1000000-0000-0000-0000-000000000002";
    private const string SpecificationId = "a3000000-0000-0000-0000-000000000001";
    private const string GenerationId = "a4000000-0000-0000-0000-000000000001";
    private const string Recorded = "2031-05-06T10:30:00+00:00";

    private static readonly string[] WorkTables =
    [
        "CaseDataSnapshots",
        "CaseDataFields",
        "CaseAssessmentFields",
        "CaseFieldProposals",
        "CaseRepairSpecifications",
        "CaseEstimateLines",
        "CaseRepairSpecificationSnapshots",
        "CaseValuations",
        "AppliedValuationSnapshots",
        "CaseReportWordings"
    ];

    public static TheoryData<string, string> Refusals => new()
    {
        { "linked_audit", "A linked Audit Case exists." },
        { "audit_reference", "An Audit reference exists." },
        { "triage_case", "A Triage-type Case exists." },
        { "triage_record", "A Triage record exists." },
        { "engineer_finding", "An engineer finding exists." },
        { "orphan_applied_valuation", "An applied valuation without its Case exists." }
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task MigrationRefusesDataTheNewSchemaCannotCarry(string precondition, string refusal)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(CasesSql);
        await database.ExecuteAsync(PreconditionSql(precondition));

        var refused = await Assert.ThrowsAsync<SqlException>(
            () => context.Database.MigrateAsync(CaseWorksMigration));

        Assert.Contains(refusal, refused.Message, StringComparison.Ordinal);
        Assert.Contains(ResetInstruction, refused.Message, StringComparison.Ordinal);
        Assert.Equal(PreviousMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseWorks'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'Cases') AND name = N'AuditOfCaseId'"));
    }

    [Fact]
    public async Task MigrationGivesEveryCaseItsPrimaryWorkAndKeepsEveryPerWorkRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(CasesSql);
        await database.ExecuteAsync(PerWorkRowsSql);

        await context.Database.MigrateAsync(CaseWorksMigration);

        Assert.Equal(CaseWorksMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(2, await database.ScalarAsync<int>("SELECT COUNT(*) FROM CaseWorks"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM Cases AS c
            WHERE NOT EXISTS (
                SELECT 1 FROM CaseWorks AS w
                WHERE w.Id = c.Id AND w.CaseId = c.Id AND w.Kind = N'primary'
                  AND w.CreatedAtUtc = c.CreatedAtUtc
                  AND w.ReportApprovalId IS NULL AND w.ReportSentEvidenceId IS NULL)
            """));

        foreach (var table in WorkTables)
        {
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'{table}') AND name = N'WorkId' AND is_nullable = 0"));
            Assert.Equal(0, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'{table}') AND name = N'CaseId'"));
            Assert.Equal(1, await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM [{table}] WHERE WorkId = '{CaseId}'"));
        }

        Assert.Equal("Case works claimant", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseDataFields WHERE WorkId = '{CaseId}' AND FieldName = N'claimant_name' AND ValueKind = N'fact'"));
        Assert.Equal("[]", await database.ScalarAsync<string>(
            $"SELECT Value FROM CaseAssessmentFields WHERE WorkId = '{CaseId}' AND FieldPath = N'damage.impacts'"));
        Assert.Equal("Proposed claimant", await database.ScalarAsync<string>(
            $"SELECT ProposedValue FROM CaseFieldProposals WHERE WorkId = '{CaseId}'"));
        Assert.Equal("Migration estimate", await database.ScalarAsync<string>(
            $"SELECT Name FROM CaseRepairSpecifications WHERE Id = '{SpecificationId}' AND WorkId = '{CaseId}'"));
        Assert.Equal(SpecificationId, await database.ScalarAsync<string>(
            $"SELECT CONVERT(nvarchar(36), RepairSpecificationId) FROM CaseEstimateLines WHERE WorkId = '{CaseId}'"),
            ignoreCase: true);
        Assert.Equal("Glasses", await database.ScalarAsync<string>(
            $"SELECT Source FROM CaseValuations WHERE WorkId = '{CaseId}'"));
        Assert.Equal(new string('b', 64), await database.ScalarAsync<string>(
            $"SELECT SnapshotHash FROM AppliedValuationSnapshots WHERE WorkId = '{CaseId}'"));
        Assert.Equal("migration.block", await database.ScalarAsync<string>(
            $"SELECT BlockKey FROM CaseReportWordings WHERE WorkId = '{CaseId}'"));

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseReportGenerations WHERE Id = '{GenerationId}' AND CaseId = '{CaseId}' AND WorkId = '{CaseId}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'CaseReportGenerations') AND name = N'WorkId' AND is_nullable = 0"));
        Assert.Equal("case", await database.ScalarAsync<string>(
            $"SELECT CustodyFolder FROM CaseDocuments WHERE CaseId = '{CaseId}'"));

        Assert.Equal(12, await database.ScalarAsync<int>(
            """
            SELECT COUNT(*)
            FROM sys.foreign_keys
            WHERE name IN (
                N'FK_CaseWorks_Cases_CaseId',
                N'FK_CaseDataSnapshots_CaseWorks_WorkId',
                N'FK_CaseDataFields_CaseDataSnapshots_WorkId',
                N'FK_CaseAssessmentFields_CaseWorks_WorkId',
                N'FK_CaseFieldProposals_CaseWorks_WorkId',
                N'FK_CaseRepairSpecifications_CaseWorks_WorkId',
                N'FK_CaseEstimateLines_CaseWorks_WorkId',
                N'FK_CaseRepairSpecificationSnapshots_CaseWorks_WorkId',
                N'FK_CaseValuations_CaseWorks_WorkId',
                N'FK_AppliedValuationSnapshots_CaseWorks_WorkId',
                N'FK_CaseReportWordings_CaseWorks_WorkId',
                N'FK_CaseReportGenerations_CaseWorks_WorkId')
            """));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.foreign_keys WHERE name LIKE N'FK[_]%[_]CaseId' AND OBJECT_NAME(parent_object_id) IN (N'CaseDataSnapshots', N'CaseDataFields', N'CaseAssessmentFields', N'CaseFieldProposals', N'CaseRepairSpecifications', N'CaseEstimateLines', N'CaseRepairSpecificationSnapshots', N'CaseValuations', N'AppliedValuationSnapshots', N'CaseReportWordings')"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name IN (N'CaseEngineerFindings', N'TriageSequences')"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'Triage') AND name = N'CaseId'"));
        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'Triage') AND name IN (N'Id', N'Sequence', N'Reference', N'PrincipalId', N'LinkedCaseId')"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'Cases') AND name = N'InitialState' AND is_nullable = 1"));
        Assert.Equal(3, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.check_constraints WHERE name IN (N'CK_Cases_InitialState', N'CK_Cases_AuditReference', N'CK_Triage_Origin')"));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task MigrationCannotBeReverted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false, useTemplate: false);
        await using var context = await database.CreateContextAsync();
        await context.Database.MigrateAsync(CaseWorksMigration);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => context.Database.MigrateAsync(PreviousMigration));

        Assert.StartsWith("CaseWorksAndTriageCases is forward-only", exception.Message, StringComparison.Ordinal);
        Assert.Equal(CaseWorksMigration, (await context.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(1, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM sys.tables WHERE name = N'CaseWorks'"));
    }

    private static string PreconditionSql(string precondition) => precondition switch
    {
        "linked_audit" =>
            $"""
            INSERT INTO Cases
                (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
                 CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version,
                 ConcurrencyToken, AuditOfCaseId)
            VALUES
                ('a1000000-0000-0000-0000-000000000003', 'a2000000-0000-0000-0000-000000000003',
                 'a2000000-0000-0000-0000-000000000002', 2031, 1, N'a.CWMG31001', N'audit', N'review',
                 N'pending', 1, 1, '{Recorded}', 0, NEWID(), '{CaseId}');
            """,
        "audit_reference" =>
            $"UPDATE Cases SET Type = N'inspection_and_audit', AuditReference = N'a.' + Reference WHERE Id = '{CaseId}';",
        "triage_case" =>
            $"UPDATE Cases SET Type = N'triage' WHERE Id = '{CaseId}';",
        "triage_record" =>
            $$"""
            INSERT INTO IntakeReceipts
                (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
                 ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
                 SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Decision,
                 DecisionReason, EvidenceJson, FieldsJson, FailureCode, FailureReason,
                 OcrCandidatesJson, Version)
            VALUES
                ('a5000000-0000-0000-0000-000000000001', 'triage.eml', 'message/rfc822', 1,
                 REPLICATE('C', 64), 'manual_upload', 'case-works-triage', '{{Recorded}}', '{{Recorded}}',
                 'migration_test_reader', '1', 'migration_test_policy', 1, 'case_created', 'Triage',
                 '{"version":1,"data":[]}', '{"version":1,"data":[]}', NULL, NULL,
                 '{"version":1,"data":[]}', 0);
            INSERT INTO Triage
                (Id, OriginReceiptId, SourceChannel, ExternalReceiptToken, SourceHash,
                 EvaluationRevisionId, NormalizedVehicleRegistration, State, CreatedAtUtc,
                 CreationOperationKey, Version, ConcurrencyToken, Sequence, Reference, PrincipalId)
            VALUES
                ('a6000000-0000-0000-0000-000000000001', 'a5000000-0000-0000-0000-000000000001',
                 N'manual_upload', N'case-works-triage', REPLICATE(N'C', 64), NEWID(), N'AB12CDE',
                 N'open', '{{Recorded}}', N'case-works-triage', 0, NEWID(), 1, N'T-00001',
                 'a2000000-0000-0000-0000-000000000003');
            """,
        "engineer_finding" =>
            $"""
            INSERT INTO CaseEngineerFindings
                (CaseId, Assessment, OperationKey, Reason, RecordedAtUtc, RecordedByKind,
                 RecordedByRolesJson, RecordedBySubjectId, RequestHash)
            VALUES
                ('{CaseId}', N'agreed', N'case-works-finding', N'Migration test', '{Recorded}',
                 N'Staff', N'[]', N'migration-test', REPLICATE(N'd', 64));
            """,
        "orphan_applied_valuation" =>
            $$"""
            INSERT INTO AppliedValuationSnapshots
                (Id, CaseId, SnapshotJson, CalculationPolicyVersion, GeneratedByKind,
                 GeneratedBySubjectId, SnapshotHash, AcceptedEngineerValue, AcceptedBy,
                 AcceptedAtUtc, Reason, PolicyVersion)
            VALUES
                (NEWID(), 'a1000000-0000-0000-0000-000000000009', N'{}', N'1', N'Staff',
                 N'migration-test', REPLICATE(N'e', 64), 1000, N'migration-test',
                 '{{Recorded}}', N'Migration test', N'1');
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(precondition), precondition, null)
    };

    // Two Cases of one Principal at the schema before CaseWorksAndTriageCases.
    private const string CasesSql =
        $"""
        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('a2000000-0000-0000-0000-000000000001', N'Case works migration provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('a2000000-0000-0000-0000-000000000002', '{Recorded}');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
        VALUES
            ('a2000000-0000-0000-0000-000000000003', 'a2000000-0000-0000-0000-000000000001', N'CWMG',
             'a2000000-0000-0000-0000-000000000002', NULL, NULL, 1, 0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState,
             CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('{CaseId}', 'a2000000-0000-0000-0000-000000000003', 'a2000000-0000-0000-0000-000000000002',
             2031, 1, N'CWMG31001', N'inspection', N'review', N'pending', 1, 1, '{Recorded}', 0, NEWID()),
            ('{SecondCaseId}', 'a2000000-0000-0000-0000-000000000003', 'a2000000-0000-0000-0000-000000000002',
             2031, 2, N'CWMG31002', N'inspection_and_audit', N'not_ready', N'pending', 0, 0,
             '2031-05-07T09:15:00+00:00', 0, NEWID());
        """;

    // One row in each per-work table (keyed on CaseId before the migration),
    // a report generation and a Case document.
    private const string PerWorkRowsSql =
        $$"""
        INSERT INTO CaseDataSnapshots
            (CaseId, AcceptedAtUtc, CompletenessPolicyKey, CompletenessPolicySatisfied, CompletenessPolicyVersion)
        VALUES ('{{CaseId}}', '{{Recorded}}', N'case-workflow', 1, 1);

        INSERT INTO CaseDataFields
            (CaseId, FieldName, ValueKind, ValueType, Value, SourceKind, SourceIdentity, SourceLabel,
             PolicyKey, PolicyVersion)
        VALUES
            ('{{CaseId}}', N'claimant_name', N'fact', N'text', N'Case works claimant', N'staff_correction',
             N'migration-test', N'Migration test', N'case-workflow', 1);

        INSERT INTO CaseAssessmentFields (CaseId, FieldPath, Value, RecordedByKind, RecordedBy, RecordedAtUtc)
        VALUES ('{{CaseId}}', N'damage.impacts', N'[]', N'Staff', N'migration-test', '{{Recorded}}');

        INSERT INTO CaseFieldProposals (CaseId, FieldPath, ProposedValue, ProposedBy, ProposedAtUtc)
        VALUES ('{{CaseId}}', N'claimant_name', N'Proposed claimant', N'migration-test', '{{Recorded}}');

        INSERT INTO CaseRepairSpecifications
            (Id, CaseId, Version, State, SourceRoute, Name, IsCurrent, RepairerVatStatus, VatPercent,
             RegionalUplift, SupplementaryExplainOnReport, CreatedBy, CreationOperationKey, CreatedAtUtc)
        VALUES
            ('{{SpecificationId}}', '{{CaseId}}', 1, N'Draft', N'Manual', N'Migration estimate', 0, N'Unknown', 20,
             0, 0, N'migration-test', N'case-works-estimate', '{{Recorded}}');

        INSERT INTO CaseEstimateLines
            (Id, CaseId, RepairSpecificationId, Position, LineType, Description, Unpriced,
             RecordedByKind, RecordedBy, RecordedAtUtc)
        VALUES
            (NEWID(), '{{CaseId}}', '{{SpecificationId}}', 1, N'repair', N'Front bumper', 0,
             N'Staff', N'migration-test', '{{Recorded}}');

        INSERT INTO CaseRepairSpecificationSnapshots
            (Id, CaseId, SpecificationId, Number, Kind, Origin, SentOnReport, Gross, LinesJson,
             DetailsJson, SupplementaryJson, ContentHash, CreatedBy, CreatedAtUtc)
        VALUES
            (NEWID(), '{{CaseId}}', '{{SpecificationId}}', 1, N'Imported', N'Migration test', 0, 0, N'[]',
             N'{}', N'{}', REPLICATE(N'a', 64), N'migration-test', '{{Recorded}}');

        INSERT INTO CaseValuations (Id, CaseId, Date, Time, Source, RecordedBy, RecordedAtUtc)
        VALUES (NEWID(), '{{CaseId}}', '2031-05-06', '10:30:00', N'Glasses', N'migration-test', '{{Recorded}}');

        INSERT INTO AppliedValuationSnapshots
            (Id, CaseId, SnapshotJson, CalculationPolicyVersion, GeneratedByKind, GeneratedBySubjectId,
             SnapshotHash, AcceptedEngineerValue, AcceptedBy, AcceptedAtUtc, Reason, PolicyVersion)
        VALUES
            (NEWID(), '{{CaseId}}', N'{}', N'1', N'Staff', N'migration-test', REPLICATE(N'b', 64), 1000,
             N'migration-test', '{{Recorded}}', N'Migration test', N'1');

        INSERT INTO CaseReportWordings (Id, CaseId, BlockKey, Included, Manual, UpdatedBy, UpdatedAtUtc)
        VALUES (NEWID(), '{{CaseId}}', N'migration.block', 1, 0, N'migration-test', '{{Recorded}}');

        INSERT INTO CaseReportGenerations
            (Id, CaseId, CaseVersion, SnapshotHash, SnapshotJson, TemplateVersion, RendererVersion,
             State, GeneratedAtUtc, Version)
        VALUES
            ('{{GenerationId}}', '{{CaseId}}', 0, REPLICATE(N'f', 64), N'{}', N'1', N'1',
             N'Confirmed', '{{Recorded}}', 1);

        INSERT INTO CaseDocuments (Id, CaseId, Ordinal, SourceOccurrenceIdentity)
        VALUES (NEWID(), '{{CaseId}}', 1, N'migration:case-works');
        """;
}
