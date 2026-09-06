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
        using var host = factory.WithC06Adapters();
        var currentId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(host);
        var priorIds = await CloneCasesAsync(host, currentId, 1);
        var priorId = priorIds[0];

        await SaveClaimantAddressAsync(host, currentId, "Riverside House, AB1 2CD");
        await SaveInspectionAddressAsync(host, priorId, "Riverside Garage, AB1 9ZZ");
        await using (var scope = host.Services.CreateAsyncScope())
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

        await using var verificationScope = host.Services.CreateAsyncScope();
        var choices = await verificationScope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, currentId, "Riverside"), CancellationToken.None);

        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.Claimant
            && choice.Address == "Riverside House, AB1 2CD");
        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.PriorPrincipalLocation
            && choice.Address == "Riverside Garage, AB1 9ZZ");
        Assert.Contains(choices, choice => choice.SourceKind == InspectionLocationSourceKind.Directory
            && choice.Label == "Riverside Repairs Ltd");
        Assert.True(choices.Count <= 20);
        Assert.Equal(choices.Count, choices.Select(choice => choice.Id).Distinct().Count());
    }

    [Fact]
    public async Task SearchExcludesTheCurrentCaseFromItsOwnPriorPrincipalLocations()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        using var host = factory.WithC06Adapters();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(host);
        await SaveInspectionAddressAsync(host, caseId, "Meadow Lane Depot, CD3 4EF");

        await using var scope = host.Services.CreateAsyncScope();
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
        using var host = factory.WithC06Adapters();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(host);
        await using var scope = host.Services.CreateAsyncScope();
        var choices = await scope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "R"), CancellationToken.None);

        Assert.Empty(choices);
    }

    [Fact]
    public async Task SearchCapsAtTwentyEvenWithManyMatchingDirectoryEntries()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        using var host = factory.WithC06Adapters();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(host);

        await using (var scope = host.Services.CreateAsyncScope())
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

        await using var verificationScope = host.Services.CreateAsyncScope();
        var choices = await verificationScope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "Bounded"), CancellationToken.None);

        Assert.Equal(20, choices.Count);
    }

    [Fact]
    public async Task SearchNeverReturnsAnInactiveDirectoryEntry()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        using var host = factory.WithC06Adapters();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(host);

        await using (var scope = host.Services.CreateAsyncScope())
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

        await using var verificationScope = host.Services.CreateAsyncScope();
        var choices = await verificationScope.ServiceProvider
            .GetRequiredService<IInspectionLocationChoices>()
            .SearchAsync(new(Administrator, caseId, "Withdrawn"), CancellationToken.None);

        Assert.Empty(choices);
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
