using Microsoft.EntityFrameworkCore;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class RepairSpecificationMigrationTests
{
    private const string PreviousMigration = "20260814094632_DropBoxFileRequests";
    private const string RepairSpecificationMigration = "20260819100144_VersionedRepairSpecifications";
    private const string CaseId = "93000000-0000-0000-0000-000000000001";
    private const string LineId = "93000000-0000-0000-0000-000000000002";

    [Fact]
    public async Task UpgradeRetainsLegacyEstimateLinesAsExplicitlyUnresolvedDraft()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using var context = await database.CreateContextAsync();

        await context.Database.MigrateAsync(PreviousMigration);
        await database.ExecuteAsync(LegacyEstimateSql);
        await context.Database.MigrateAsync(RepairSpecificationMigration);

        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseEstimateLines WHERE Id = '{LineId}'"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM CaseEstimateLines WHERE Id = '{LineId}' AND RepairSpecificationId IS NOT NULL"));
        Assert.Equal(1, await database.ScalarAsync<int>(
            $"""
             SELECT COUNT(*)
             FROM CaseRepairSpecifications
             WHERE CaseId = '{CaseId}'
               AND Version = 1
               AND Purpose = 'OrdinaryAssessment'
               AND Role = 'Ordinary'
               AND State = 'Draft'
               AND SourceRoute = 'LegacyUnresolved'
               AND AcceptedBy IS NULL
               AND AcceptedAtUtc IS NULL
             """));
    }

    private const string LegacyEstimateSql =
        """
        INSERT INTO IntakeReceipts
            (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel,
             ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey,
             SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Decision,
             DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson)
        VALUES
            ('93000000-0000-0000-0000-000000000010', 'legacy.eml', 'message/rfc822', 1,
             'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA', 'manual_upload',
             'repair-spec-migration', '2031-05-06T10:30:00+00:00', '2031-05-06T10:30:00+00:00',
             'migration-reader', '1', 'migration-policy', 1, 'case_created', 'accepted',
             '{"version":1,"data":[]}', '{"version":1,"data":[]}', '{"version":1,"data":[]}');

        INSERT INTO Organizations (Id, Name, Version)
        VALUES ('93000000-0000-0000-0000-000000000011', 'Repair migration provider', 0);

        INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc)
        VALUES ('93000000-0000-0000-0000-000000000012', '2031-05-06T10:30:00+00:00');

        INSERT INTO Principals
            (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version)
        VALUES
            ('93000000-0000-0000-0000-000000000013',
             '93000000-0000-0000-0000-000000000011', 'QDOS',
             '93000000-0000-0000-0000-000000000012', 1, 0);

        INSERT INTO Cases
            (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type,
             InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete,
             ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff,
             CreatedAtUtc, Version, ConcurrencyToken)
        VALUES
            ('93000000-0000-0000-0000-000000000001',
             '93000000-0000-0000-0000-000000000013',
             '93000000-0000-0000-0000-000000000012', 2031, 1, 'QDOS31001',
             'inspection', 'review', 'pending',
             '93000000-0000-0000-0000-000000000010', 1, 1, 1, 1,
             '2031-05-06T10:30:00+00:00', 0, NEWID());

        INSERT INTO CaseEstimateLines
            (Id, CaseId, Position, LineType, GuideCode, Description, WorkUnits, Price,
             Unpriced, PartNumber, Betterment, Status, EvidenceLabel, Justification,
             RecordedByKind, RecordedBy, RecordedAtUtc, ConfirmedBy, ConfirmedAtUtc)
        VALUES
            ('93000000-0000-0000-0000-000000000002',
             '93000000-0000-0000-0000-000000000001', 1, 'repair', '31',
             'Legacy front bumper repair', 2.5, 125.00, 0, NULL, NULL, 'confirmed',
             'case', 'Retained migration fixture', 'Staff', 'engineer-1',
             '2031-05-06T10:35:00+00:00', 'engineer-1', '2031-05-06T10:36:00+00:00');
        """;
}
