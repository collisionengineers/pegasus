using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class TypedCaseDataMigrationTests
{
    private const string PreviousMigration = "20260729184000_DueChaserSweep";
    private const string TypedDataMigration = "20260729185000_TypedCaseDataCompleteness";
    private const string ConfirmedCaseId = "85000000-0000-0000-0000-000000000001";
    private const string UnconfirmedCaseId = "85000000-0000-0000-0000-000000000002";
    private const string AcceptedActor = "85000000-0000-0000-0000-000000000099";

    [Fact]
    public async Task UpgradeRetainsExplicitlyConfirmedActiveCaseValuesAndLeavesUnconfirmedValuesSuggested()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(ExistingCasesSql);
        await context.Database.MigrateAsync(TypedDataMigration);

        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM CaseDataSnapshots WHERE CaseId = '{ConfirmedCaseId}'"));
        Assert.Equal(
            9,
            await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{ConfirmedCaseId}' AND ValueKind = 'suggestion'"));
        Assert.Equal(
            9,
            await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{ConfirmedCaseId}' AND ValueKind = 'confirmed'"));
        Assert.Equal(
            "Jane Upgrade",
            await database.ScalarAsync<string>(
                $"SELECT Value FROM CaseDataFields WHERE CaseId = '{ConfirmedCaseId}' AND FieldName = 'claimant_name' AND ValueKind = 'confirmed'"));
        Assert.Equal(
            AcceptedActor,
            await database.ScalarAsync<string>(
                $"SELECT ConfirmedByActor FROM CaseDataFields WHERE CaseId = '{ConfirmedCaseId}' AND FieldName = 'claimant_name' AND ValueKind = 'confirmed'"));
        Assert.Equal(
            "intake_evidence",
            await database.ScalarAsync<string>(
                $"SELECT SourceKind FROM CaseDataFields WHERE CaseId = '{ConfirmedCaseId}' AND FieldName = 'claimant_name' AND ValueKind = 'confirmed'"));
        Assert.Equal(
            "legacy-accepted-migration",
            await database.ScalarAsync<string>(
                $"SELECT PolicyKey FROM CaseDataFields WHERE CaseId = '{ConfirmedCaseId}' AND FieldName = 'claimant_name' AND ValueKind = 'confirmed'"));

        Assert.Equal(
            1,
            await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{UnconfirmedCaseId}' AND ValueKind = 'suggestion'"));
        Assert.Equal(
            0,
            await database.ScalarAsync<int>(
                $"SELECT COUNT(*) FROM CaseDataFields WHERE CaseId = '{UnconfirmedCaseId}' AND ValueKind = 'confirmed'"));
    }

    private const string ExistingCasesSql =
        """
        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('85000000-0000-0000-0000-000000000010', 'Typed migration provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('85000000-0000-0000-0000-000000000011', '2031-05-06T10:30:00+00:00');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version)
        VALUES
            ('85000000-0000-0000-0000-000000000012',
             '85000000-0000-0000-0000-000000000010', 'QDOS',
             '85000000-0000-0000-0000-000000000011', 1, 0);

        INSERT INTO IntakeReceipts
            (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
             ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
             SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson,
             FieldsJson, OcrCandidatesJson)
        VALUES
            ('85000000-0000-0000-0000-000000000020', 'confirmed.eml', 'message/rfc822', 1,
             'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA', 'manual_upload',
             'typed-migration-confirmed', '2031-05-06T10:30:00+00:00', '2031-05-06T10:30:00+00:00',
             'migration-reader', '1', 0, 'draft_ready', 'accepted',
             '{"version":1,"data":[]}', '{"version":1,"data":[]}', '{"version":1,"data":[]}'),
            ('85000000-0000-0000-0000-000000000021', 'unconfirmed.eml', 'message/rfc822', 1,
             'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB', 'manual_upload',
             'typed-migration-unconfirmed', '2031-05-06T10:31:00+00:00', '2031-05-06T10:31:00+00:00',
             'migration-reader', '1', 0, 'draft_ready', 'accepted',
             '{"version":1,"data":[]}', '{"version":1,"data":[]}', '{"version":1,"data":[]}');

        INSERT INTO InstructionDrafts
            (IntakeReceiptId, ClaimantName, ClaimNumber, VehicleRegistration,
             VehicleMake, VehicleModel, VehicleMileage, InspectionAddress, InspectionDate)
        VALUES
            ('85000000-0000-0000-0000-000000000020', 'Jane Upgrade', 'QDOS-UPGRADE-1',
             'AB12CDE', 'Example', 'Model', 42000, '1 Upgrade Street, London', '2031-05-20'),
            ('85000000-0000-0000-0000-000000000021', 'Still Suggested', NULL,
             NULL, NULL, NULL, NULL, NULL, NULL);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type,
             InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete,
             ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff,
             CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('85000000-0000-0000-0000-000000000001',
             '85000000-0000-0000-0000-000000000012',
             '85000000-0000-0000-0000-000000000011', 2031, 1, 'QDOS31001',
             'inspection', 'review', 'pending',
             '85000000-0000-0000-0000-000000000020', 1, 1, 1, 1,
             '2031-05-06T10:30:00+00:00', 0, NEWID()),
            ('85000000-0000-0000-0000-000000000002',
             '85000000-0000-0000-0000-000000000012',
             '85000000-0000-0000-0000-000000000011', 2031, 2, 'QDOS31002',
             'inspection', 'not_ready', 'pending',
             '85000000-0000-0000-0000-000000000021', 0, 0, 0, 0,
             '2031-05-06T10:31:00+00:00', 0, NEWID());

        INSERT INTO CaseWorkflows
            (CaseId, State, AssignedEngineerId, Version, ConcurrencyToken)
        VALUES
            ('85000000-0000-0000-0000-000000000001', 'ReportPreparation',
             '85000000-0000-0000-0000-000000000098', 7, NEWID()),
            ('85000000-0000-0000-0000-000000000002', 'NotReady', NULL, 0, NEWID());

        INSERT INTO ExternalWorkItems
            (Id, CaseId, Kind, OperationKey, State, AttemptCount, DueAtUtc)
        VALUES
            ('85000000-0000-0000-0000-000000000030',
             '85000000-0000-0000-0000-000000000001', 'create_case_custody',
             'migration-custody-confirmed', 'completed', 1, '2031-05-06T10:30:00+00:00'),
            ('85000000-0000-0000-0000-000000000031',
             '85000000-0000-0000-0000-000000000002', 'create_case_custody',
             'migration-custody-unconfirmed', 'completed', 1, '2031-05-06T10:31:00+00:00');

        INSERT INTO CaseIntakeLinks
            (IntakeReceiptId, CaseId, CustodyWorkId, LinkedAtUtc, Actor, OperationKey,
             ExpectedIntakeVersion)
        VALUES
            ('85000000-0000-0000-0000-000000000020',
             '85000000-0000-0000-0000-000000000001',
             '85000000-0000-0000-0000-000000000030', '2031-05-06T10:30:00+00:00',
             '85000000-0000-0000-0000-000000000099', 'migration-accept-confirmed', 0),
            ('85000000-0000-0000-0000-000000000021',
             '85000000-0000-0000-0000-000000000002',
             '85000000-0000-0000-0000-000000000031', '2031-05-06T10:31:00+00:00',
             '85000000-0000-0000-0000-000000000097', 'migration-accept-unconfirmed', 0);
        """;
}
