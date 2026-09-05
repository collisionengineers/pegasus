using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class InspectionAddressChoicesPersistenceTests
{
    [Fact]
    public async Task StoragePersistsAndPrincipalHistoryIsDistinctNewestFirst()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = AutomationMcpTestSupport.WithAutomationMcp(baseFactory);
        var currentId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        var priorIds = await CloneCasesAsync(factory, currentId, 4);
        var duplicateOlderId = priorIds[0];
        var duplicateNewerId = priorIds[1];
        var newestId = priorIds[2];
        var imageBasedId = priorIds[3];

        await SaveInspectionAsync(factory, currentId, "99 Current Road", "14 Storage Lane");
        await SaveInspectionAsync(factory, duplicateOlderId, "1 Previous Street");
        await SaveInspectionAsync(factory, duplicateNewerId, "1 previous street");
        await SaveInspectionAsync(factory, newestId, "2 Newer Avenue");
        await SaveInspectionAsync(
            factory,
            imageBasedId,
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            mode: CaseInspectionMode.ImageBasedAssessment);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await SetConfirmationTimeAsync(context, duplicateOlderId, new(2031, 5, 1, 9, 0, 0, TimeSpan.Zero));
            await SetConfirmationTimeAsync(context, duplicateNewerId, new(2031, 5, 2, 9, 0, 0, TimeSpan.Zero));
            await SetConfirmationTimeAsync(context, newestId, new(2031, 5, 3, 9, 0, 0, TimeSpan.Zero));
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var services = verificationScope.ServiceProvider;
        var projection = await services.GetRequiredService<ICaseDataQueries>()
            .GetAsync(currentId, CancellationToken.None);
        var choices = await services.GetRequiredService<IInspectionAddressChoicesQueries>()
            .GetAsync(currentId, CancellationToken.None);

        Assert.Equal("14 Storage Lane", projection?.Inspection.StorageLocation?.Confirmed?.Value);
        Assert.Equal(
            CaseDataSourceKind.StaffCorrection,
            projection?.Inspection.StorageLocation?.Confirmed?.Source.Kind);
        Assert.NotNull(choices);
        Assert.Null(choices.RepairerAddress);
        Assert.Equal(["2 Newer Avenue", "1 previous street"], choices.PreviousAddresses);
        Assert.DoesNotContain("99 Current Road", choices.PreviousAddresses);
        Assert.DoesNotContain(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            choices.PreviousAddresses);
    }

    [Fact]
    public async Task AutomationDetailsUpdatePreservesStorageLocation()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = AutomationMcpTestSupport.WithAutomationMcp(baseFactory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://localhost")
        });
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        await SaveInspectionAsync(factory, caseId, "99 Current Road", "14 Storage Lane");
        var version = await AutomationMcpTestSupport.GetWorkflowVersionAsync(factory, caseId);
        var token = await AutomationMcpTestSupport.RequestTokenAsync(client, "automation.cases");
        var lease = await AutomationMcpTestSupport.BeginEditAsync(client, token, caseId, version, 41);

        using var response = await AutomationMcpTestSupport.PostMcpAsync(
            client,
            token,
            AutomationMcpTestSupport.ToolCallPayload(
                42,
                "pegasus_case_update_details",
                new
                {
                    caseId,
                    expectedVersion = lease.CaseVersion,
                    editLeaseToken = lease.LeaseToken,
                    operationKey = $"mcp:case-041-{Guid.NewGuid():N}",
                    reason = "Updated an unrelated case detail",
                    claimantName = "Updated claimant"
                }));
        _ = await AutomationMcpTestSupport.ReadStructuredContentAsync(response);

        await using var scope = factory.Services.CreateAsyncScope();
        var projection = await scope.ServiceProvider.GetRequiredService<ICaseDataQueries>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.Equal("14 Storage Lane", projection?.Inspection.StorageLocation?.Confirmed?.Value);
    }

    private static async Task SaveInspectionAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        string address,
        string? storageLocation = null,
        CaseInspectionMode mode = CaseInspectionMode.PhysicalAddress)
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
                "Confirmed inspection choice fixture",
                lease.Token,
                new(
                    InspectionAddress: address,
                    InspectionMode: mode,
                    StorageLocation: storageLocation)),
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
            var sequence = sourceCase.Sequence + index + 1;
            var clone = new CaseEntity
            {
                Id = caseId,
                PrincipalId = sourceCase.PrincipalId,
                SequenceLineageId = sourceCase.SequenceLineageId,
                Year = sourceCase.Year,
                Sequence = sequence,
                Reference = $"{sourceCase.Reference}-H{index + 1}",
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

    private static Task<int> SetConfirmationTimeAsync(
        PegasusDbContext context,
        Guid caseId,
        DateTimeOffset confirmedAtUtc) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseDataFields SET ConfirmedAtUtc = {confirmedAtUtc} WHERE CaseId = {caseId} AND FieldName = {"inspection_address"} AND ValueKind = {"confirmed"}");
}
