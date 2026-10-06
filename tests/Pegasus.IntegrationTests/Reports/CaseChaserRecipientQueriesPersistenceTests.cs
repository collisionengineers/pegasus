using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Cases;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The addresses a Case chaser opens addressed to, read as recorded: the
/// origin receipt's route sender, each paired image intake's route sender and
/// the accepted repairer's directory e-mail.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseChaserRecipientQueriesPersistenceTests
{
    private static readonly DateTimeOffset StartUtc = new(2031, 3, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecipientsAreTheOriginSenderPairedImageSendersAndTheRepairerEmail()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = Factory(database);
        var principalId = await SeedPrincipalAsync(factory);
        var originReceiptId = await SeedReceiptAsync(factory, "origin-sender@principal.example");
        var caseId = await SeedCaseAsync(factory, principalId, 1, originReceiptId);
        await SeedImageIntakeAsync(factory, caseId, "second-images@principal.example", StartUtc.AddHours(2), isActive: true);
        await SeedImageIntakeAsync(factory, caseId, "first-images@principal.example", StartUtc.AddHours(1), isActive: true);
        await SeedImageIntakeAsync(factory, caseId, "first-images@principal.example", StartUtc.AddHours(3), isActive: true);
        await SeedImageIntakeAsync(factory, caseId, "unpaired-images@principal.example", StartUtc.AddHours(4), isActive: false);
        await SeedImageIntakeAsync(factory, caseId, sender: null, StartUtc.AddHours(5), isActive: true);
        var repairerId = await SeedOrganizationAsync(factory, "Chaser repairer", "repairer@repairs.example");
        await SeedAcceptedRepairerAsync(factory, caseId, repairerId);

        var recipients = await new EfCaseChaserRecipientQueries(factory)
            .GetAsync(caseId, CancellationToken.None);

        Assert.NotNull(recipients);
        Assert.Equal("origin-sender@principal.example", recipients!.OriginalInstructionSender);
        Assert.Equal(
            ["first-images@principal.example", "second-images@principal.example"],
            recipients.ImageSourceSenders);
        Assert.Equal("repairer@repairs.example", recipients.RepairerEmail);
    }

    [Fact]
    public async Task ACaseWithNothingRecordedHasNoRecipients()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = Factory(database);
        var principalId = await SeedPrincipalAsync(factory);
        var caseId = await SeedCaseAsync(factory, principalId, 1, originReceiptId: null);

        var recipients = await new EfCaseChaserRecipientQueries(factory)
            .GetAsync(caseId, CancellationToken.None);

        Assert.NotNull(recipients);
        Assert.Null(recipients!.OriginalInstructionSender);
        Assert.Empty(recipients.ImageSourceSenders);
        Assert.Null(recipients.RepairerEmail);
    }

    [Fact]
    public async Task AnUnknownCaseHasNoRecipientRecord()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var recipients = await new EfCaseChaserRecipientQueries(Factory(database))
            .GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(recipients);
    }

    private static PooledDbContextFactory<PegasusDbContext> Factory(LocalDbTestDatabase database) =>
        new(new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(database.ConnectionString)
            .Options);

    private static async Task<Guid> SeedOrganizationAsync(
        PooledDbContextFactory<PegasusDbContext> factory,
        string name,
        string? email)
    {
        await using var context = await factory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        context.Organizations.Add(new OrganizationEntity
        {
            Id = organizationId,
            Name = name,
            Email = email,
            Version = 0
        });
        await context.SaveChangesAsync();
        return organizationId;
    }

    private static async Task<Guid> SeedPrincipalAsync(PooledDbContextFactory<PegasusDbContext> factory)
    {
        var organizationId = await SeedOrganizationAsync(factory, "Chaser recipient test", email: null);
        await using var context = await factory.CreateDbContextAsync();
        var principalId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        context.AddRange(
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = StartUtc },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = "CHSR",
                IsActive = true,
                Version = 0
            });
        await context.SaveChangesAsync();
        return principalId;
    }

    private static async Task<Guid> SeedReceiptAsync(
        PooledDbContextFactory<PegasusDbContext> factory,
        string? sender)
    {
        await using var context = await factory.CreateDbContextAsync();
        var receiptId = Guid.NewGuid();
        context.Add(new IntakeReceiptEntity
        {
            Id = receiptId,
            SourceFileName = "chaser-recipient.eml",
            MediaType = "message/rfc822",
            SourceLength = 1,
            SourceHash = new string('0', 64),
            SourceChannel = "mailbox",
            ExternalReceiptToken = $"chaser:{receiptId:N}",
            ReceivedAtUtc = StartUtc,
            ProcessedAtUtc = StartUtc,
            SourceReaderKey = "chaser-test",
            SourceReaderVersion = "1",
            Version = 0,
            Decision = "case_created",
            DecisionReason = "Chaser recipient test",
            EvidenceJson = "[]",
            FieldsJson = "[]",
            OcrCandidatesJson = "[]"
        });
        if (sender is not null)
        {
            context.Add(new IntakeMailRouteDecisionEntity
            {
                IntakeReceiptId = receiptId,
                Disposition = "accepted",
                PredicatesJson = "[]",
                Reason = "Chaser recipient sender",
                PolicyKey = "chaser-test",
                PolicyVersion = 1,
                TransportIdentitiesJson = "[]",
                OriginalIdentitiesJson = "[]",
                EffectiveSenderAddress = sender,
                EffectiveSenderSourceLabel = "original-header"
            });
        }
        await context.SaveChangesAsync();
        return receiptId;
    }

    private static async Task<Guid> SeedCaseAsync(
        PooledDbContextFactory<PegasusDbContext> factory,
        Guid principalId,
        int sequence,
        Guid? originReceiptId)
    {
        await using var context = await factory.CreateDbContextAsync();
        var lineageId = await context.Principals
            .Where(item => item.Id == principalId)
            .Select(item => item.SequenceLineageId)
            .SingleAsync();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2031,
                Sequence = sequence,
                Reference = $"CHSR3100{sequence}",
                Type = "inspection",
                InitialState = "NotReady",
                CustodyState = "confirmed",
                OriginIntakeReceiptId = originReceiptId,
                CreatedAtUtc = StartUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "NotReady",
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        return caseId;
    }

    private static async Task SeedImageIntakeAsync(
        PooledDbContextFactory<PegasusDbContext> factory,
        Guid caseId,
        string? sender,
        DateTimeOffset createdAtUtc,
        bool isActive)
    {
        var receiptId = await SeedReceiptAsync(factory, sender);
        await using var context = await factory.CreateDbContextAsync();
        context.AddRange(
            new ImageIntakeEntity
            {
                Id = Guid.NewGuid(),
                OriginReceiptId = receiptId,
                SourceChannel = "mailbox",
                ExternalReceiptToken = $"chaser:{receiptId:N}",
                SourceHash = new string('0', 64),
                EvaluationRevisionId = Guid.NewGuid(),
                NormalizedVehicleRegistration = "AB12CDE",
                ImageIntakeReference = $"IMG-{receiptId.ToString("N")[..20]}",
                CreatedAtUtc = createdAtUtc,
                CreatedByActorKind = "Staff",
                CreatedByActorSubjectId = "chaser-test",
                Reason = "seed",
                CreationOperationKey = $"image:{receiptId:N}",
                RequestFingerprint = new string('a', 64),
                LifecycleState = "registered",
                LifecycleVersion = 1
            },
            new IntakeManualAssociationEntity
            {
                IntakeReceiptId = receiptId,
                CaseId = caseId,
                IsActive = isActive,
                Version = 1,
                LinkedAtUtc = createdAtUtc,
                ActorKind = "Staff",
                ActorSubjectId = "chaser-test",
                ActorRolesJson = "[\"Engineer\"]",
                LastOperationKey = $"link:{receiptId:N}"
            });
        await context.SaveChangesAsync();
    }

    private static async Task SeedAcceptedRepairerAsync(
        PooledDbContextFactory<PegasusDbContext> factory,
        Guid caseId,
        Guid repairerId)
    {
        await using var context = await factory.CreateDbContextAsync();
        var snapshot = new CaseDataSnapshotEntity
        {
            WorkId = caseId,
            CompletenessPolicyKey = "case-completeness",
            CompletenessPolicyVersion = 1,
            CompletenessPolicySatisfied = true,
            AcceptedAtUtc = StartUtc
        };
        snapshot.Fields.Add(new CaseDataFieldEntity
        {
            WorkId = caseId,
            Snapshot = snapshot,
            FieldName = CaseDataFieldNames.RepairerId,
            ValueKind = "confirmed",
            ValueType = "text",
            Value = repairerId.ToString("D"),
            SourceKind = "staff_correction",
            SourceIdentity = "chaser-test",
            SourceLabel = "Staff",
            PolicyKey = "case-data",
            PolicyVersion = 1,
            ConfirmedByActor = "chaser-test",
            ConfirmedAtUtc = StartUtc
        });
        context.CaseDataSnapshots.Add(snapshot);
        await context.SaveChangesAsync();
    }
}
