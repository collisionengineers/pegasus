using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-18/S05 items 3-5: the bounded local inspection-location suggestion
/// search — the union of a case's own current claimant/repairer/storage
/// addresses, the principal's prior accepted locations and the active
/// Administrator-maintained directory, with a required 2-character minimum
/// prefix, exact-before-prefix ordering and an internal 20-row cap that no
/// caller can raise. No external address provider is installed or called.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class InspectionAddressSuggestionTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        Guid.Parse("6a2b6a0e-0a1a-4a2a-8a2a-6a2b6a0e0a1a"),
        [StaffRole.Administrator]);

    [Fact]
    public async Task SearchUnionsCaseClaimantPriorPrincipalLocationAndDirectory()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var currentId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        var priorIds = await CloneCasesAsync(factory, currentId, 1);
        var priorId = priorIds[0];

        // C06 review R-22: SaveEditableDataAsync posts a partial
        // CaseEditableData, and EfCaseDataStore.SetConfirmed deletes a
        // confirmed field whose incoming value is null — a second save
        // naming only StorageLocation would wipe the ClaimantAddress the
        // line above it just confirmed. Seed both confirmed fields this
        // case needs in one save so neither is destroyed by the other.
        await SaveEditableDataAsync(
            factory,
            currentId,
            new(ClaimantAddress: "Riverside House, AB1 2CD", StorageLocation: "Riverside Yard, AB1 5GH"));
        await SaveInspectionAddressAsync(factory, priorId, "Riverside Garage, AB1 9ZZ");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.Set<OrganizationDirectoryEntryEntity>().Add(new()
            {
                Id = Guid.NewGuid(),
                Role = "repairer",
                Name = "Riverside Repairs Ltd",
                NormalizedName = "RIVERSIDE REPAIRS LTD",
                Address = "1 Riverside Way",
                Postcode = "AB1 3EE",
                NormalizedPostcode = "AB13EE",
                SourceKind = "manual",
                SourceRecordId = null,
                SourceVersion = 1,
                UpdatedBy = Administrator.SubjectId,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Active = true,
                Version = 0
            });
            await context.SaveChangesAsync();
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var choices = await verificationScope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, currentId, "Riverside"), CancellationToken.None);

        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.Claimant
            && choice.Address == "Riverside House, AB1 2CD");
        // C06 review R-19: the union's Storage source had no coverage at
        // all — unlike Repairer, EfCaseDataStore does write and map back
        // CaseDataFieldNames.StorageLocation, so it is seedable here.
        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.Storage
            && choice.Address == "Riverside Yard, AB1 5GH");
        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.PriorPrincipalLocation
            && choice.Address == "Riverside Garage, AB1 9ZZ");
        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.Directory
            && choice.Label == "Riverside Repairs Ltd");
        Assert.True(choices.Count <= 20);
        Assert.Equal(choices.Count, choices.Select(choice => choice.Id).Distinct().Count());
    }

    /// <summary>
    /// C06 review R-17: the SQL-level pre-filter on
    /// <c>CaseDataFields.Value</c> must not be narrower than the exact,
    /// whitespace-collapsing <c>NormalizeNamePrefix</c> rule the union
    /// re-applies afterwards — a prior address stored with irregular (here,
    /// doubled) whitespace must still prefix-match a query whose whitespace
    /// is regular.
    /// </summary>
    /// <remarks>
    /// C06 review R-21: the fixture cannot seed through <c>ISaveCase</c> —
    /// <c>CaseDataOperations.Text</c> collapses whitespace on write, so the
    /// doubled space would never reach the row. The intake-acceptance path
    /// (<c>Ext18InspectionAddressPolicy.Evaluate</c>, which only
    /// <c>Trim()</c>s, followed by
    /// <c>CaseDataSnapshotFactory.UpsertConfirmed</c>) is the production
    /// source of this irregular interior whitespace, so this seeds the
    /// confirmed field row directly the same way that path leaves it.
    /// </remarks>
    [Fact]
    public async Task SearchMatchesAPriorLocationWhoseStoredWhitespaceIsIrregular()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var currentId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        var priorIds = await CloneCasesAsync(factory, currentId, 1);
        var priorId = priorIds[0];

        await SeedConfirmedInspectionAddressAsync(factory, priorId, "12  High Street, AB1 2CD");

        await using var scope = factory.Services.CreateAsyncScope();
        var choices = await scope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, currentId, "12 High"), CancellationToken.None);

        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.PriorPrincipalLocation
            && choice.Address == "12  High Street, AB1 2CD");
    }

    [Fact]
    public async Task SearchExcludesTheCurrentCaseFromItsOwnPriorPrincipalLocations()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        await SaveInspectionAddressAsync(factory, caseId, "Meadow Lane Depot, CD3 4EF");

        await using var scope = factory.Services.CreateAsyncScope();
        var choices = await scope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "Meadow"), CancellationToken.None);

        Assert.DoesNotContain(
            choices,
            choice => choice.SourceKind == InspectionLocationSourceKind.PriorPrincipalLocation);
    }

    [Fact]
    public async Task SearchRequiresAtLeastTwoNormalizedCharacters()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var choices = await scope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "R"), CancellationToken.None);

        Assert.Empty(choices);
    }

    [Fact]
    public async Task SearchCapsAtTwentyEvenWithManyMatchingDirectoryEntries()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            for (var index = 0; index < 30; index++)
            {
                context.Set<OrganizationDirectoryEntryEntity>().Add(new()
                {
                    Id = Guid.NewGuid(),
                    Role = "storage",
                    Name = $"Bounded Storage {index:D2}",
                    NormalizedName = $"BOUNDED STORAGE {index:D2}",
                    Address = $"{index} Bounded Way",
                    Postcode = null,
                    NormalizedPostcode = null,
                    SourceKind = "manual",
                    SourceRecordId = null,
                    SourceVersion = 1,
                    UpdatedBy = Administrator.SubjectId,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    Active = true,
                    Version = 0
                });
            }
            await context.SaveChangesAsync();
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var choices = await verificationScope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "Bounded"), CancellationToken.None);

        Assert.Equal(20, choices.Count);
    }

    [Fact]
    public async Task SearchNeverReturnsAnInactiveDirectoryEntry()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.Set<OrganizationDirectoryEntryEntity>().Add(new()
            {
                Id = Guid.NewGuid(),
                Role = "storage",
                Name = "Withdrawn Storage Site",
                NormalizedName = "WITHDRAWN STORAGE SITE",
                Address = "9 Withdrawn Road",
                Postcode = null,
                NormalizedPostcode = null,
                SourceKind = "manual",
                SourceRecordId = null,
                SourceVersion = 1,
                UpdatedBy = Administrator.SubjectId,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Active = false,
                Version = 0
            });
            await context.SaveChangesAsync();
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var choices = await verificationScope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "Withdrawn"), CancellationToken.None);

        Assert.Empty(choices);
    }

    /// <summary>
    /// C06 review R-8: exact-before-prefix ordering and the source-record
    /// identity (item 5's <see cref="InspectionLocationChoice.SourceRecordId"/>/
    /// <see cref="InspectionLocationChoice.SourceVersion"/>) were asserted
    /// only at <see cref="EfOrganizationDirectory"/>, never through
    /// <see cref="IInspectionLocationChoices"/> itself, where the union's own
    /// re-ranking runs. (Repairer-source coverage is deferred: nothing in
    /// this codebase writes <c>CaseDataProjection.Inspection.RepairerAddress</c>
    /// today — <see cref="EfCaseDataStore"/>'s map never populates it — so
    /// there is no path to seed it through, a pre-existing gap outside C06.)
    /// </summary>
    [Fact]
    public async Task SearchRanksAnExactMatchBeforeAPrefixMatchAndCarriesTheSourceRecordIdentity()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        await SaveClaimantAddressAsync(factory, caseId, "Ash House, AB1 2CD");

        var directoryId = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            context.Set<OrganizationDirectoryEntryEntity>().Add(new()
            {
                Id = directoryId,
                Role = "repairer",
                Name = "Ash",
                NormalizedName = "ASH",
                Address = "2 Ash Lane",
                Postcode = "AB1 9ZZ",
                NormalizedPostcode = "AB19ZZ",
                SourceKind = "manual",
                SourceRecordId = null,
                SourceVersion = 3,
                UpdatedBy = Administrator.SubjectId,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Active = true,
                Version = 0
            });
            await context.SaveChangesAsync();
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var services = verificationScope.ServiceProvider;
        var projection = await services.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CancellationToken.None);
        var choices = await services.GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "Ash"), CancellationToken.None);

        Assert.NotNull(projection);
        // The directory entry's name "Ash" is an exact match for the search
        // prefix; the claimant address "Ash House, AB1 2CD" only matches as
        // a prefix (a claimant/repairer/storage/prior choice never carries a
        // postcode, so it can only ever rank as exact via a whole-address
        // equality that a real address will not hit) — so the exact match
        // must sort first regardless of either candidate's alphabetical name.
        Assert.Equal(InspectionLocationSourceKind.Directory, choices[0].SourceKind);
        Assert.Equal(directoryId, choices[0].Id);
        // The entry's own SourceRecordId is null, so the directory adapter's
        // fallback (SourceRecordId ?? Id) must surface as the entry's id.
        Assert.Equal(directoryId, choices[0].SourceRecordId);
        Assert.Equal(3, choices[0].SourceVersion);

        var claimantChoice = Assert.Single(
            choices, choice => choice.SourceKind == InspectionLocationSourceKind.Claimant);
        Assert.Equal(caseId, claimantChoice.SourceRecordId);
        Assert.Equal(projection!.Version, claimantChoice.SourceVersion);
    }

    private static async Task SaveClaimantAddressAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        string claimantAddress) =>
        await SaveEditableDataAsync(factory, caseId, new(ClaimantAddress: claimantAddress));

    private static async Task SaveInspectionAddressAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        string inspectionAddress) =>
        await SaveEditableDataAsync(
            factory,
            caseId,
            new(InspectionAddress: inspectionAddress, InspectionMode: CaseInspectionMode.PhysicalAddress));

    /// <summary>
    /// C06 review R-21: seeds a confirmed <c>inspection_address</c> field
    /// directly at the row level, bypassing <c>ISaveCase</c> /
    /// <c>CaseDataPolicy.Normalize</c> — whose <c>Text(...)</c> helper
    /// collapses every whitespace run on write — the same way the
    /// intake-acceptance path (<c>Trim()</c> only) can leave a stored value
    /// with irregular interior whitespace. Not reachable through
    /// <c>ISaveCase</c> at all, so this is the only route to that state.
    /// </summary>
    private static async Task SeedConfirmedInspectionAddressAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        string inspectionAddress)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var originIntakeReceiptId = await context.CaseDataSnapshots.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => item.OriginIntakeReceiptId)
            .SingleAsync();
        context.Set<CaseDataFieldEntity>().Add(new()
        {
            CaseId = caseId,
            FieldName = CaseDataFieldNames.InspectionAddress,
            ValueKind = CaseDataCodes.Confirmed,
            ValueType = CaseDataCodes.Text,
            Value = inspectionAddress,
            SourceKind = CaseDataCodes.CaseAcceptance,
            SourceIdentity = originIntakeReceiptId.ToString("D"),
            SourceLabel = "accepted inspection address",
            PolicyKey = Ext18InspectionAddressPolicy.PolicyKey,
            PolicyVersion = Ext18InspectionAddressPolicy.PolicyVersion,
            ConfirmedByActor = Administrator.SubjectId,
            ConfirmedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
    }

    private static async Task SaveEditableDataAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        CaseEditableData editableData)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var current = await services.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The accepted case was not found.");
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var lease = await services.GetRequiredService<IAcquireCaseEditLease>()
            .ExecuteAsync(
                new(caseId, current.Version, actor, $"lease-{Guid.NewGuid():N}"),
                CancellationToken.None);
        await services.GetRequiredService<ISaveCase>().ExecuteAsync(
            new(
                caseId,
                current.Version,
                actor,
                $"save-{Guid.NewGuid():N}",
                "Suggestion search fixture",
                lease.Token,
                editableData),
            CancellationToken.None);
    }

    private static async Task<Guid[]> CloneCasesAsync(
        WebApplicationFactory<Program> factory,
        Guid sourceCaseId,
        int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var sourceCase = await context.Cases.AsNoTracking()
            .SingleAsync(item => item.Id == sourceCaseId);
        var sourceSnapshot = await context.CaseDataSnapshots.AsNoTracking()
            .SingleAsync(item => item.CaseId == sourceCaseId);
        var ids = new Guid[count];
        for (var index = 0; index < count; index++)
        {
            var caseId = Guid.NewGuid();
            ids[index] = caseId;
            var clone = new CaseEntity
            {
                Id = caseId,
                PrincipalId = sourceCase.PrincipalId,
                SequenceLineageId = sourceCase.SequenceLineageId,
                Year = sourceCase.Year,
                Sequence = sourceCase.Sequence + index + 1,
                Reference = $"{sourceCase.Reference}-S{index + 1}",
                Type = sourceCase.Type,
                InitialState = sourceCase.InitialState,
                CustodyState = sourceCase.CustodyState,
                OriginIntakeReceiptId = sourceCase.OriginIntakeReceiptId,
                InstructionComplete = sourceCase.InstructionComplete,
                ImagesComplete = sourceCase.ImagesComplete,
                CreatedAtUtc = sourceCase.CreatedAtUtc.AddMinutes(index + 1),
                ConcurrencyToken = Guid.NewGuid()
            };
            context.Cases.Add(clone);
            context.CaseDataSnapshots.Add(new()
            {
                CaseId = caseId,
                Case = clone,
                OriginIntakeReceiptId = sourceSnapshot.OriginIntakeReceiptId,
                OriginSourceChannel = sourceSnapshot.OriginSourceChannel,
                OriginExternalReceiptToken = sourceSnapshot.OriginExternalReceiptToken,
                OriginSourceHash = sourceSnapshot.OriginSourceHash,
                OriginReceivedAtUtc = sourceSnapshot.OriginReceivedAtUtc,
                SourceReaderKey = sourceSnapshot.SourceReaderKey,
                SourceReaderVersion = sourceSnapshot.SourceReaderVersion,
                ExtractionPolicyKey = sourceSnapshot.ExtractionPolicyKey,
                ExtractionPolicyVersion = sourceSnapshot.ExtractionPolicyVersion,
                CompletenessPolicyKey = sourceSnapshot.CompletenessPolicyKey,
                CompletenessPolicyVersion = sourceSnapshot.CompletenessPolicyVersion,
                CompletenessPolicySatisfied = sourceSnapshot.CompletenessPolicySatisfied,
                AcceptedAtUtc = sourceSnapshot.AcceptedAtUtc.AddMinutes(index + 1)
            });
            context.CaseWorkflows.Add(new()
            {
                CaseId = caseId,
                Case = clone,
                State = nameof(CaseLifecycleState.NotReady),
                ConcurrencyToken = Guid.NewGuid()
            });
        }
        await context.SaveChangesAsync();
        return ids;
    }
}
