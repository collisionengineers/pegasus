using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Persistence.Migrations;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class ContactDirectoryPersistenceTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("4fa2d0a6-2f1c-4b6e-8f9a-3d4a9e8a7c11"),
        [StaffRole.Administrator]);

    private static readonly JsonSerializerOptions HistoryJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SaveKeepsTheLinkedPrincipalsAndTheOneFixedType()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var firstPrincipal = await SaveAsync(database, NewPrincipal("First principal", "CP01"));
        var secondPrincipal = await SaveAsync(database, NewPrincipal("Second principal", "CP02"));
        var contactId = Guid.NewGuid();
        Guid[] linked = [.. new[] { firstPrincipal.OwnPrincipalIds!.Single(), secondPrincipal.OwnPrincipalIds!.Single() }.Order()];

        var request = new SaveContactRequest(
            Administrator,
            contactId,
            0,
            "Linked repairer",
            null,
            null,
            "1 Contact Street",
            "AB1 2CD",
            true,
            ContactRole.Repairer,
            null,
            CaseInspectionMode.PhysicalAddress,
            linked,
            Guid.NewGuid().ToString("N"));
        var saved = await SaveAsync(database, request);

        Assert.Equal("AB1 2CD", saved.Postcode);
        Assert.Equal(ContactRole.Repairer, saved.Role);
        Assert.Equal(linked, saved.LinkedPrincipalIds);

        await using var scope = database.CreateAsyncScope();
        var contacts = scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        var reread = await contacts.GetAsync(Administrator, contactId, CancellationToken.None);
        Assert.NotNull(reread);
        Assert.Equal(ContactRole.Repairer, reread.Role);
        Assert.Equal(linked, reread.LinkedPrincipalIds);

        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(
            linked,
            (await context.Set<ContactPrincipalLinkEntity>()
                .Where(item => item.OrganizationId == contactId)
                .Select(item => item.PrincipalId)
                .ToArrayAsync()).Order());

        // The type is chosen when the contact is added and never changes.
        await Assert.ThrowsAsync<ArgumentException>(() => SaveAsync(database, request with
        {
            ExpectedVersion = saved.Version,
            Role = ContactRole.ClaimSource,
            OperationKey = Guid.NewGuid().ToString("N")
        }));
    }

    [Fact]
    public async Task ExistingVersionZeroContactCanSaveDirectlyAndRejectsStaleVersion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var contactId = Guid.NewGuid();
        await using (var scope = database.CreateAsyncScope())
        {
            var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await using (context)
            {
                context.Organizations.Add(new OrganizationEntity
                {
                    Id = contactId,
                    Name = "Migrated contact",
                    Role = "claim_source",
                    Active = true,
                    Version = 0
                });
                await context.SaveChangesAsync();
            }
        }

        var request = NewContact(contactId, expectedVersion: 0) with
        {
            Name = "Migrated contact renamed"
        };
        var saved = await SaveAsync(database, request);
        Assert.Equal(1, saved.Version);
        Assert.Equal("Migrated contact renamed", saved.Name);
        var stale = await Assert.ThrowsAsync<ContactDirectoryException>(() =>
            SaveAsync(database, request with { OperationKey = Guid.NewGuid().ToString("N") }));
        Assert.Equal(ContactDirectoryError.StaleVersion, stale.Error);
    }

    [Fact]
    public async Task DisablingAContactDisablesOnlyItsCurrentPrincipal()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var request = NewPrincipal("Current principal", "CP03");
        var created = await SaveAsync(database, request);

        await using var scope = database.CreateAsyncScope();
        _ = await SaveAsync(database, request with
        {
            ExpectedVersion = created.Version,
            Active = false,
            OperationKey = Guid.NewGuid().ToString("N")
        });

        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.False(await context.Principals
            .Where(item => item.OrganizationId == request.OrganizationId && item.SuccessorId == null)
            .Select(item => item.IsActive)
            .SingleAsync());
    }

    /// <summary>
    /// R118: a save sets an existing Principal's inspection mode; the same mode
    /// changes nothing and leaves the version, and a new one is the contact's
    /// history event with the mode before and after.
    /// </summary>
    [Fact]
    public async Task SaveSetsAnExistingPrincipalsInspectionModeOnlyWhenItDiffers()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var request = NewPrincipal("Mode principal", "CPM01");
        var created = await SaveAsync(database, request);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, created.PrincipalInspectionMode);

        var unchanged = await SaveAsync(database, request with
        {
            ExpectedVersion = created.Version,
            OperationKey = Guid.NewGuid().ToString("N")
        });
        Assert.Equal(created.Version, unchanged.Version);
        Assert.Equal(CaseInspectionMode.PhysicalAddress, unchanged.PrincipalInspectionMode);

        var changedKey = Guid.NewGuid().ToString("N");
        var changed = await SaveAsync(database, request with
        {
            ExpectedVersion = unchanged.Version,
            PrincipalInspectionMode = CaseInspectionMode.ImageBasedAssessment,
            OperationKey = changedKey
        });
        Assert.Equal(unchanged.Version + 1, changed.Version);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, changed.PrincipalInspectionMode);

        await using var scope = database.CreateAsyncScope();
        var reread = await scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>()
            .GetAsync(Administrator, request.OrganizationId, CancellationToken.None);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, reread!.PrincipalInspectionMode);
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        Assert.Equal(
            PrincipalInspectionModePolicy.ImageBasedAssessmentCode,
            await context.Principals
                .Where(item => item.OrganizationId == request.OrganizationId)
                .Select(item => item.InspectionMode)
                .SingleAsync());
        var history = await context.ActionHistory.SingleAsync(item => item.CorrelationId == changedKey);
        Assert.Equal(("contact", request.OrganizationId.ToString("D"), "contact_saved"), (history.AggregateType, history.AggregateId, history.EventKind));
        Assert.Equal(CaseInspectionMode.PhysicalAddress, HistoryRecord(history.BeforeJson).PrincipalInspectionMode);
        Assert.Equal(CaseInspectionMode.ImageBasedAssessment, HistoryRecord(history.AfterJson).PrincipalInspectionMode);
    }

    /// <summary>
    /// R119: a contact may link an inactive Principal, and the Principal choices
    /// offer an inactive one only to the contact that already links it.
    /// </summary>
    [Fact]
    public async Task ALinkMayNameAnInactivePrincipalWhichOnlyItsContactIsOffered()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var activePrincipal = await SaveAsync(database, NewPrincipal("Linked active principal", "CPL01"));
        var inactiveRequest = NewPrincipal("Linked inactive principal", "CPL02");
        var inactivePrincipal = await SaveAsync(database, inactiveRequest);
        _ = await SaveAsync(database, inactiveRequest with
        {
            ExpectedVersion = inactivePrincipal.Version,
            Active = false,
            OperationKey = Guid.NewGuid().ToString("N")
        });
        var activeId = activePrincipal.OwnPrincipalIds!.Single();
        var inactiveId = inactivePrincipal.OwnPrincipalIds!.Single();
        Guid[] linked = [.. new[] { activeId, inactiveId }.Order()];

        var linking = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Inactive link repairer",
            Role = ContactRole.Repairer,
            LinkedPrincipalIds = linked
        });
        var other = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Unlinked repairer",
            Role = ContactRole.Repairer
        });

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        Assert.Equal(linked, (await queries.GetAsync(Administrator, linking.OrganizationId, CancellationToken.None))!.LinkedPrincipalIds);
        async Task<Guid[]> FixtureChoices(Guid? contactId) =>
            [.. (await queries.ListPrincipalChoicesAsync(Administrator, contactId, CancellationToken.None))
                .Select(choice => choice.Principal.Id)
                .Where(id => linked.Contains(id))
                .Order()];
        Assert.Equal(linked, await FixtureChoices(linking.OrganizationId));
        Assert.Equal([activeId], await FixtureChoices(other.OrganizationId));
        Assert.Equal([activeId], await FixtureChoices(null));
    }

    /// <summary>
    /// R35, R17: an intermediary is an active Claim Source with an e-mail, once
    /// for each active Principal it is linked to. No other contact type, and no
    /// unlinked, inactive or e-mail-less Claim Source, is one.
    /// </summary>
    [Fact]
    public async Task TheIntermediaryRegistryReadsLinkedActiveClaimSourcesOnly()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var first = (await SaveAsync(database, NewPrincipal("Intermediary principal one", "CPI01"))).OwnPrincipalIds!.Single();
        var second = (await SaveAsync(database, NewPrincipal("Intermediary principal two", "CPI02"))).OwnPrincipalIds!.Single();
        var inactiveRequest = NewPrincipal("Intermediary principal inactive", "CPI03");
        var inactiveSaved = await SaveAsync(database, inactiveRequest);
        _ = await SaveAsync(database, inactiveRequest with
        {
            ExpectedVersion = inactiveSaved.Version,
            Active = false,
            OperationKey = Guid.NewGuid().ToString("N")
        });
        var inactive = inactiveSaved.OwnPrincipalIds!.Single();
        async Task Contact(string name, ContactRole role, string? email, bool active, params Guid[] principals) =>
            _ = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
            {
                Name = name,
                Role = role,
                Email = email,
                Active = active,
                LinkedPrincipalIds = principals
            });
        await Contact("Linked claim source", ContactRole.ClaimSource, "desk@linked-claims.example", true, first, second);
        await Contact("Inactive claim source", ContactRole.ClaimSource, "desk@inactive-claims.example", false, first);
        await Contact("Inactive Principal claim source", ContactRole.ClaimSource, "desk@inactive-principal.example", true, inactive);
        await Contact("Blank e-mail claim source", ContactRole.ClaimSource, null, true, first);
        await Contact("Unlinked claim source", ContactRole.ClaimSource, "desk@unlinked-claims.example", true);
        await Contact("Linked repairer", ContactRole.Repairer, "desk@linked-repairer.example", true, first);

        await using var scope = database.CreateAsyncScope();
        var registry = scope.ServiceProvider.GetRequiredService<IIntermediaryRegistry>();
        Assert.IsType<EfIntermediaryRegistry>(registry);
        var intermediaries = await registry.LoadAsync(CancellationToken.None);

        Assert.True(intermediaries.IsIntermediaryFor("claims@linked-claims.example", "CPI01"));
        Assert.True(intermediaries.IsIntermediaryFor("claims@linked-claims.example", "CPI02"));
        Assert.False(intermediaries.IsIntermediaryFor("claims@linked-claims.example", "CPI03"));
        Assert.False(intermediaries.IsIntermediaryFor("claims@inactive-claims.example", "CPI01"));
        Assert.False(intermediaries.IsIntermediaryFor("claims@inactive-principal.example", "CPI03"));
        Assert.False(intermediaries.IsIntermediaryFor("claims@unlinked-claims.example", "CPI01"));
        Assert.False(intermediaries.IsIntermediaryFor("claims@linked-repairer.example", "CPI01"));
    }

    [Fact]
    public async Task ListOrdersByNameAndRetainsContactsWithoutCases()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        const string fixturePrefix = "List-sort fixture ";
        var claimSource = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = fixturePrefix + "Zulu claim source"
        });
        var firstPrincipal = await SaveAsync(database, NewPrincipal(fixturePrefix + "Alpha principal", "LCD01"));
        var secondPrincipal = await SaveAsync(database, NewPrincipal(fixturePrefix + "Bravo principal", "LCD02"));
        var repairer = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = fixturePrefix + "Mike repairer",
            Role = ContactRole.Repairer
        });
        var receivedAtUtc = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        await using (var scope = database.CreateAsyncScope())
        {
            var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await using (context)
            {
                var principalIds = new[]
                {
                    firstPrincipal.OwnPrincipalIds!.Single(),
                    secondPrincipal.OwnPrincipalIds!.Single()
                };
                var principals = await context.Principals
                    .Where(item => principalIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id);
                var first = principals[firstPrincipal.OwnPrincipalIds!.Single()];
                var second = principals[secondPrincipal.OwnPrincipalIds!.Single()];
                var repaired = Case(second, 1, "CASE-C", receivedAtUtc);
                context.Cases.AddRange(
                    Case(first, 1, "CASE-B", receivedAtUtc),
                    Case(first, 2, "CASE-A", receivedAtUtc),
                    repaired);
                await context.SaveChangesAsync();
                // CASE-C names the Repairer, as a link on the Case records it.
                context.CaseDataSnapshots.Add(new CaseDataSnapshotEntity
                {
                    WorkId = repaired.Id,
                    OriginReceivedAtUtc = receivedAtUtc,
                    CompletenessPolicyKey = "test",
                    CompletenessPolicyVersion = 1,
                    AcceptedAtUtc = receivedAtUtc,
                    Fields =
                    [
                        new CaseDataFieldEntity
                        {
                            WorkId = repaired.Id,
                            FieldName = CaseDataFieldNames.RepairerId,
                            ValueKind = "fact",
                            ValueType = "text",
                            Value = repairer.OrganizationId.ToString("D"),
                            SourceKind = "intake_evidence",
                            SourceIdentity = "test",
                            SourceLabel = "test",
                            PolicyKey = "test",
                            PolicyVersion = 1
                        }
                    ]
                });
                await context.SaveChangesAsync();
            }
        }

        await using var queryScope = database.CreateAsyncScope();
        var queries = queryScope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        async Task<IReadOnlyList<ContactDirectoryRecord>> ListOfType(ContactRole role) =>
            (await queries.ListAsync(new(Administrator, role, fixturePrefix), CancellationToken.None)).Items;
        var listedPrincipals = await ListOfType(ContactRole.Principal);
        var listedRepairers = await ListOfType(ContactRole.Repairer);
        var listedClaimSources = await ListOfType(ContactRole.ClaimSource);

        Assert.Equal(
            [firstPrincipal.OrganizationId, secondPrincipal.OrganizationId],
            listedPrincipals.Select(item => item.OrganizationId));
        Assert.Equal([repairer.OrganizationId], listedRepairers.Select(item => item.OrganizationId));
        Assert.Equal([claimSource.OrganizationId], listedClaimSources.Select(item => item.OrganizationId));

        var latest = Assert.Single(listedPrincipals, item => item.OrganizationId == firstPrincipal.OrganizationId);
        Assert.Equal(receivedAtUtc, latest.LastCaseReceivedAtUtc);
        Assert.Equal("CASE-A", latest.LastCaseReference);
        Assert.Equal("LCD01", latest.PrincipalCode);
        // A Repairer's last Case is the latest Case that names it (FRD-04).
        var listedRepairer = Assert.Single(listedRepairers);
        Assert.Equal(receivedAtUtc, listedRepairer.LastCaseReceivedAtUtc);
        Assert.Equal("CASE-C", listedRepairer.LastCaseReference);
        Assert.Null(listedRepairer.PrincipalCode);
        Assert.All(
            listedClaimSources,
            item =>
            {
                Assert.Null(item.LastCaseReceivedAtUtc);
                Assert.Null(item.LastCaseReference);
                Assert.Null(item.PrincipalCode);
            });

    }

    /// <summary>
    /// R373: the role lists and the possible matches read one statement over the
    /// organisation's own columns, never its Principals or latest Case, and the
    /// choice holds the same facts the full record holds. A role list carries, in
    /// that same statement, the codes of the Principals each contact is linked
    /// to, which order a Case's list (R1013, R1015); a possible match does not.
    /// </summary>
    [Fact]
    public async Task ChoicesAreOneStatementOverTheOrganisationsOwnColumns()
    {
        var statements = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(statements));
        const string prefix = "Choice fixture ";
        var principal = await SaveAsync(database, NewPrincipal(prefix + "Principal", "CHC01"));
        var zulu = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = prefix + "Zulu repairer",
            Role = ContactRole.Repairer,
            Email = "zulu@example.test",
            Telephone = "0113 000 0001",
            Address = "1 Zulu Road",
            Postcode = "ZU1 1ZU",
            LinkedPrincipalIds = principal.OwnPrincipalIds!
        });
        var alpha = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = prefix + "Alpha repairer",
            Role = ContactRole.Repairer
        });
        var inactive = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = prefix + "Inactive repairer",
            Role = ContactRole.Repairer,
            Active = false
        });
        var claimSource = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = prefix + "Claim source",
            NotesOnEveryCase = "Quote the reference."
        });

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        statements.Reset();
        var repairers = await queries.ListByRoleAsync(Administrator, ContactRole.Repairer, CancellationToken.None);
        Assert.Equal(1, statements.Count);
        var listed = repairers.Where(item => item.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal([alpha.OrganizationId, zulu.OrganizationId], listed.Select(item => item.OrganizationId));
        Assert.Equal(
            new ContactChoice(
                zulu.OrganizationId, zulu.Name, ContactRole.Repairer, zulu.Version, "zulu@example.test",
                "0113 000 0001", "1 Zulu Road", "ZU1 1ZU", null),
            listed[1] with { LinkedPrincipalCodes = null });
        Assert.Equal("CHC01", Assert.Single(listed[1].LinkedPrincipalCodes!));
        Assert.True(listed[1].IsLinkedTo("chc01"));
        Assert.Empty(listed[0].LinkedPrincipalCodes!);
        Assert.False(listed[0].IsLinkedTo("CHC01"));

        statements.Reset();
        var sources = await queries.ListByRoleAsync(Administrator, ContactRole.ClaimSource, CancellationToken.None);
        Assert.Equal(1, statements.Count);
        Assert.Equal(
            "Quote the reference.",
            Assert.Single(sources, item => item.OrganizationId == claimSource.OrganizationId).NotesOnEveryCase);

        // A possible match is any organisation by name, an inactive one and any type included.
        statements.Reset();
        var matches = await queries.FindPossibleMatchesAsync(Administrator, prefix, CancellationToken.None);
        Assert.Equal(1, statements.Count);
        Assert.Equal(
            [
                (alpha.OrganizationId, ContactRole.Repairer),
                (claimSource.OrganizationId, ContactRole.ClaimSource),
                (inactive.OrganizationId, ContactRole.Repairer),
                (principal.OrganizationId, ContactRole.Principal),
                (zulu.OrganizationId, ContactRole.Repairer)
            ],
            matches.Select(item => (item.OrganizationId, item.Role)));
    }

    /// <summary>
    /// R1014, R1017: a received item's Claim Source is read from the directory
    /// in one statement, and only for an item that names one or came through
    /// an intermediary. The instruction's named Claim Source comes before the
    /// sender's.
    /// </summary>
    [Fact]
    public async Task AReceivedItemEstablishesItsClaimSourceFromTheDirectory()
    {
        var statements = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(statements));
        var principal = await SaveAsync(database, NewPrincipal("Fill fixture principal", "CSF01"));
        var sender = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Fill fixture sender",
            Email = "desk@fill-claims.example",
            LinkedPrincipalIds = principal.OwnPrincipalIds!
        });
        var named = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with { Name = "Fill fixture named" });
        _ = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Fill fixture inactive",
            Email = "desk@fill-claims.example",
            Active = false,
            LinkedPrincipalIds = principal.OwnPrincipalIds!
        });
        const string emptyEnvelope = """{"version":1,"data":[]}""";
        IntakeReceiptEntity Receipt(string routeKind, params InstructionReviewField[] fields) => new()
        {
            Id = Guid.NewGuid(), SourceFileName = "fill-probe", MediaType = "message/rfc822",
            SourceHash = "source-hash", SourceChannel = "mailbox", ExternalReceiptToken = "fill-probe",
            SourceReaderKey = "structural-probe", SourceReaderVersion = "1",
            Decision = "case_created", DecisionReason = "fill-probe",
            EvidenceJson = emptyEnvelope, OcrCandidatesJson = emptyEnvelope,
            FieldsJson = EfIntakeReceiptStore.SerializeFields(fields),
            InstructionDraft = new() { SuggestedPrincipalCode = "CSF01" },
            MailRouteDecision = new()
            {
                Disposition = "accepted", RouteOwnerCode = "fill-claims.example", RouteKind = routeKind,
                PrincipalCode = "CSF01", PredicatesJson = emptyEnvelope, Reason = "fill-probe",
                PolicyKey = "principal_mail_route", PolicyVersion = 1,
                TransportIdentitiesJson = emptyEnvelope, OriginalIdentitiesJson = emptyEnvelope,
                EffectiveSenderAddress = "reports@fill-claims.example", EffectiveSenderSourceLabel = "fill-probe"
            }
        };
        var introducer = new InstructionReviewField(
            EstablishedIntakeClaimSource.IntroducerField, "FILL FIXTURE NAMED",
            [new("FILL FIXTURE NAMED", IntakeEvidenceSource.PdfContent, "introducer-line")], false, false);

        await using var scope = database.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();

        statements.Reset();
        var fromSender = await CaseDataSnapshotFactory.EstablishClaimSourceAsync(
            context, Receipt("intermediary"), CancellationToken.None);
        Assert.InRange(statements.Count, 0, 1);
        Assert.NotNull(fromSender);
        Assert.Equal(IntakeClaimSourceOrigin.Sender, fromSender.Origin);
        Assert.Equal(sender.OrganizationId, fromSender.ClaimSource.OrganizationId);

        var fromInstruction = await CaseDataSnapshotFactory.EstablishClaimSourceAsync(
            context, Receipt("intermediary", introducer), CancellationToken.None);
        Assert.NotNull(fromInstruction);
        Assert.Equal(IntakeClaimSourceOrigin.Instruction, fromInstruction.Origin);
        Assert.Equal(named.OrganizationId, fromInstruction.ClaimSource.OrganizationId);

        // A direct Principal's own mail names no Claim Source and reads nothing.
        statements.Reset();
        Assert.Null(await CaseDataSnapshotFactory.EstablishClaimSourceAsync(
            context, Receipt("direct_principal"), CancellationToken.None));
        Assert.Empty(statements.Statements);
    }

    /// <summary>
    /// R1027: a received item's Repairer is read from the directory in one
    /// statement, and only for an item whose instruction names a repairer. An
    /// inactive Repairer of the name, and a Claim Source of it, are not taken.
    /// </summary>
    [Fact]
    public async Task AReceivedItemEstablishesItsRepairerFromTheDirectory()
    {
        var statements = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(statements));
        var named = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Fill fixture bodyshop",
            Role = ContactRole.Repairer
        });
        _ = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Fill fixture closed bodyshop",
            Role = ContactRole.Repairer,
            Active = false
        });
        _ = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with { Name = "Fill fixture source only" });
        const string emptyEnvelope = """{"version":1,"data":[]}""";
        IntakeReceiptEntity Receipt(params InstructionReviewField[] fields) => new()
        {
            Id = Guid.NewGuid(), SourceFileName = "fill-probe", MediaType = "message/rfc822",
            SourceHash = "source-hash", SourceChannel = "mailbox", ExternalReceiptToken = "fill-probe",
            SourceReaderKey = "structural-probe", SourceReaderVersion = "1",
            Decision = "case_created", DecisionReason = "fill-probe",
            EvidenceJson = emptyEnvelope, OcrCandidatesJson = emptyEnvelope,
            FieldsJson = EfIntakeReceiptStore.SerializeFields(fields),
            InstructionDraft = new() { SuggestedPrincipalCode = "AX" }
        };
        InstructionReviewField RepairerName(string printed) => new(
            "Repairer name", printed, [new(printed, IntakeEvidenceSource.PdfContent, "bodyshop-name-line")], false, false);

        await using var scope = database.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();

        statements.Reset();
        var established = await CaseDataSnapshotFactory.EstablishRepairerAsync(
            context, Receipt(RepairerName("FILL FIXTURE BODYSHOP")), CancellationToken.None);
        Assert.InRange(statements.Count, 0, 1);
        Assert.NotNull(established);
        Assert.Equal(named.OrganizationId, established.Repairer.OrganizationId);

        Assert.Null(await CaseDataSnapshotFactory.EstablishRepairerAsync(
            context, Receipt(RepairerName("Fill fixture closed bodyshop")), CancellationToken.None));
        Assert.Null(await CaseDataSnapshotFactory.EstablishRepairerAsync(
            context, Receipt(RepairerName("Fill fixture source only")), CancellationToken.None));

        // An instruction that names no repairer reads nothing.
        statements.Reset();
        Assert.Null(await CaseDataSnapshotFactory.EstablishRepairerAsync(
            context, Receipt(), CancellationToken.None));
        Assert.Empty(statements.Statements);
    }

    /// <summary>
    /// #1950: a role's choices are kept until a contact save commits, so a new
    /// contact and a deactivated one show on the next read.
    /// </summary>
    [Fact]
    public async Task TheRoleChoicesAreKeptUntilASaveEvictsThem()
    {
        var statements = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(statements));
        var repairerA = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Kept choice repairer A",
            Role = ContactRole.Repairer
        });

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        var cold = await queries.ListByRoleAsync(Administrator, ContactRole.Repairer, CancellationToken.None);
        Assert.Contains(cold, item => item.OrganizationId == repairerA.OrganizationId);
        statements.Reset();
        Assert.Equal(cold, await queries.ListByRoleAsync(Administrator, ContactRole.Repairer, CancellationToken.None));
        Assert.Equal(0, statements.Count);

        var repairerB = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Kept choice repairer B",
            Role = ContactRole.Repairer
        });
        await SaveAsync(database, NewContact(repairerA.OrganizationId, repairerA.Version) with
        {
            Name = repairerA.Name,
            Role = ContactRole.Repairer,
            Active = false
        });
        var afterSaves = await queries.ListByRoleAsync(Administrator, ContactRole.Repairer, CancellationToken.None);
        Assert.Contains(afterSaves, item => item.OrganizationId == repairerB.OrganizationId);
        Assert.DoesNotContain(afterSaves, item => item.OrganizationId == repairerA.OrganizationId);
    }

    [Fact]
    public async Task ListPagesATypeByNameWithoutSkippingOrRepeatingARow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        const string repairerPrefix = "Paged repairer ";
        // Zero-padded, so name order is number order; added newest-first in one save.
        string[] repairerNames = [.. Enumerable.Range(0, 120).Select(number => $"{repairerPrefix}{number:D3}")];
        await using (var scope = database.CreateAsyncScope())
        {
            var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            await using (context)
            {
                context.Organizations.AddRange(repairerNames.OrderDescending().Select(name => new OrganizationEntity
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Role = "repairer",
                    Active = true,
                    Version = 0
                }));
                await context.SaveChangesAsync();
            }
        }
        const string principalPrefix = "Paged principal ";
        string[] principalNames = [.. Enumerable.Range(1, 3).Select(number => $"{principalPrefix}{number}")];
        foreach (var number in Enumerable.Range(1, 3))
        {
            await SaveAsync(database, NewPrincipal($"{principalPrefix}{number}", $"PGP{number}"));
        }

        await using var queryScope = database.CreateAsyncScope();
        var queries = queryScope.ServiceProvider.GetRequiredService<IContactDirectoryQueries>();
        Task<ContactDirectoryPage> ReadPage(ContactRole role, int pageNumber, string search) =>
            queries.ListAsync(new(Administrator, role, search, pageNumber), CancellationToken.None);

        var pages = new[]
        {
            await ReadPage(ContactRole.Repairer, 1, repairerPrefix),
            await ReadPage(ContactRole.Repairer, 2, repairerPrefix),
            await ReadPage(ContactRole.Repairer, 3, repairerPrefix)
        };
        Assert.Equal([1, 2, 3], pages.Select(page => page.PageNumber));
        Assert.Equal([50, 50, 20], pages.Select(page => page.Items.Count));
        Assert.Equal([true, true, false], pages.Select(page => page.HasNextPage));
        Assert.Equal(repairerNames, pages.SelectMany(page => page.Items).Select(item => item.Name));

        var beyond = await ReadPage(ContactRole.Repairer, 4, repairerPrefix);
        Assert.Empty(beyond.Items);
        Assert.False(beyond.HasNextPage);

        var principals = await ReadPage(ContactRole.Principal, 1, principalPrefix);
        Assert.Equal(principalNames, principals.Items.Select(item => item.Name));
        Assert.False(principals.HasNextPage);

        // A prefix narrows to 100 rows, so the second page holds exactly a full page and no more.
        var narrowedFirst = await ReadPage(ContactRole.Repairer, 1, repairerPrefix + "0");
        var narrowedSecond = await ReadPage(ContactRole.Repairer, 2, repairerPrefix + "0");
        Assert.Equal(
            repairerNames.Take(100),
            narrowedFirst.Items.Concat(narrowedSecond.Items).Select(item => item.Name));
        Assert.True(narrowedFirst.HasNextPage);
        Assert.False(narrowedSecond.HasNextPage);
        Assert.Empty((await ReadPage(ContactRole.Repairer, 3, repairerPrefix + "0")).Items);
    }

    /// <summary>
    /// The removal of the Storage and Third Party Engineer types (operator,
    /// 13 November 2026; R1049 to R1051): a Storage contact holding an address
    /// and a postcode becomes an inspection location with its Principal links,
    /// both types' contacts and links go, every other contact stays, and a
    /// second run finds nothing left to do.
    /// </summary>
    [Fact]
    public async Task StorageYardsBecomeInspectionLocationsAndTheTwoRemovedTypesGo()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var principal = (await SaveAsync(database, NewPrincipal("Yard fixture principal", "YRD01"))).OwnPrincipalIds!.Single();
        var repairer = await SaveAsync(database, NewContact(Guid.NewGuid(), 0) with
        {
            Name = "Yard fixture repairer",
            Role = ContactRole.Repairer,
            LinkedPrincipalIds = [principal]
        });
        var appended = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var noPostcode = Guid.NewGuid();
        var engineer = Guid.NewGuid();
        await using var scope = database.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
        // The schema no longer admits the two types, so the rows are seeded as they stood before it changed.
        await context.Database.ExecuteSqlRawAsync(
            "ALTER TABLE [dbo].[Organizations] DROP CONSTRAINT [CK_Organizations_Role];");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO [dbo].[Organizations] ([Id], [Name], [Role], [Address], [Postcode], [Telephone], [Active], [Version]) VALUES
                ({appended}, N'Yard fixture appended', N'storage', N' 1 Yard Road, Leeds ', N'ls1 1aa', N'0113 000 0003', 1, 2),
                ({kept}, N'Yard fixture kept', N'storage', N'2 Yard Road, Leeds, LS2 2BB', N'LS2 2BB', NULL, 0, 0),
                ({noPostcode}, N'Yard fixture no postcode', N'storage', N'3 Yard Road', NULL, NULL, 1, 0),
                ({engineer}, N'Yard fixture engineer', N'third_party_engineer', N'4 Yard Road', N'LS4 4DD', NULL, 1, 0);
            INSERT INTO [dbo].[ContactPrincipalLinks] ([PrincipalId], [OrganizationId]) VALUES
                ({principal}, {appended}), ({principal}, {noPostcode}), ({principal}, {engineer});
            """);

        await context.Database.ExecuteSqlRawAsync(ContactsThreeTypes.RemoveTypesSql);
        await context.Database.ExecuteSqlRawAsync(ContactsThreeTypes.RemoveTypesSql);

        var yards = await context.Set<InspectionLocationEntity>().AsNoTracking()
            .Where(item => item.Source == ContactsThreeTypes.YardSource)
            .OrderBy(item => item.Name)
            .Select(item => new
            {
                item.Id,
                item.Address,
                item.Postcode,
                item.Active,
                item.SourceRecordId,
                Links = item.PrincipalLinks.Select(link => link.PrincipalId).ToList()
            })
            .ToArrayAsync();
        Assert.Equal([appended, kept], yards.Select(item => item.Id));
        Assert.Equal(
            ("1 Yard Road, Leeds, LS1 1AA", "LS1 1AA", true, appended.ToString("D")),
            (yards[0].Address, yards[0].Postcode, yards[0].Active, yards[0].SourceRecordId));
        Assert.Equal(principal, Assert.Single(yards[0].Links));
        Assert.Equal(
            ("2 Yard Road, Leeds, LS2 2BB", "LS2 2BB", false),
            (yards[1].Address, yards[1].Postcode, yards[1].Active));
        Assert.Empty(yards[1].Links);

        Assert.Equal(0, await database.ScalarAsync<int>(
            "SELECT COUNT(*) FROM Organizations WHERE Role IN (N'storage', N'third_party_engineer')"));
        Assert.Equal(
            repairer.OrganizationId,
            Assert.Single(await context.Set<ContactPrincipalLinkEntity>().AsNoTracking()
                .Where(item => item.PrincipalId == principal)
                .Select(item => item.OrganizationId)
                .ToArrayAsync()));
    }

    private static SaveContactRequest NewPrincipal(string name, string code) => new(
        Administrator,
        Guid.NewGuid(),
        0,
        name,
        null,
        null,
        null,
        null,
        true,
        ContactRole.Principal,
        code,
        CaseInspectionMode.PhysicalAddress,
        [],
        Guid.NewGuid().ToString("N"));

    private static CaseEntity Case(
        PrincipalEntity principal,
        int sequence,
        string reference,
        DateTimeOffset receivedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        PrincipalId = principal.Id,
        SequenceLineageId = principal.SequenceLineageId,
        Year = receivedAtUtc.Year,
        Sequence = sequence,
        Reference = reference,
        Type = "inspection",
        InitialState = "NotReady",
        CustodyState = "Pending",
        CreatedAtUtc = receivedAtUtc,
        Version = 0,
        ConcurrencyToken = Guid.NewGuid()
    };

    private static SaveContactRequest NewContact(Guid contactId, long expectedVersion) => new(
        Administrator,
        contactId,
        expectedVersion,
        "Migrated contact",
        null,
        null,
        null,
        null,
        true,
        ContactRole.ClaimSource,
        null,
        CaseInspectionMode.PhysicalAddress,
        [],
        Guid.NewGuid().ToString("N"),
        string.Empty);

    private static ContactDirectoryRecord HistoryRecord(string? json) =>
        JsonSerializer.Deserialize<ContactDirectoryRecord>(json!, HistoryJson)!;

    private static async Task<ContactDirectoryRecord> SaveAsync(
        LocalDbTestDatabase database,
        SaveContactRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>()
            .SaveAsync(request, CancellationToken.None);
    }
}
