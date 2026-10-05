using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A new Case starts with its Principal's default fee as its agreed fee
/// (operator, 5 October 2026), so the fee note is not a report blocker. The
/// value is the Principal's, tagged as such, until staff change it.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PrincipalDefaultFeeCreationTests
{
    [Fact]
    public async Task EverySeededPrincipalStartsAtTheStandardFee()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();

        var fees = await context.Principals.AsNoTracking().Select(item => item.DefaultFee).ToListAsync();

        Assert.NotEmpty(fees);
        Assert.All(fees, fee => Assert.Equal(PrincipalDefaultFeePolicy.Standard, fee));
    }

    [Fact]
    public async Task AnAcceptedCaseStartsWithItsPrincipalsDefaultFee()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        var receipt = await AllocationTestData.StoreDefinitiveReceiptAsync(
            factory.Services, CaseType.Inspection, QdosPrincipal.Code);
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        await SetQdosFeeAsync(factory.Services, 212.50m);

        await using var scope = factory.Services.CreateAsyncScope();
        var outcome = await scope.ServiceProvider.GetRequiredService<IAcceptIntake>().ExecuteAsync(
            new(
                receipt.Id,
                0,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                $"case-accept:{Guid.NewGuid():N}",
                CaseType.Inspection,
                QdosPrincipal.Code,
                new(true, true)),
            CancellationToken.None);

        await AssertPrincipalFeeAsync(factory.Services, outcome.Identity.CaseId, "212.50");
    }

    [Fact]
    public async Task AManualCaseStartsWithItsPrincipalsDefaultFee()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        await SetQdosFeeAsync(factory.Services, 99.99m);

        await using var scope = factory.Services.CreateAsyncScope();
        var identity = await scope.ServiceProvider.GetRequiredService<ICreateManualCase>().ExecuteAsync(
            new(
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                $"manual-create:{Guid.NewGuid():N}",
                QdosPrincipal.Code,
                CaseType.Inspection,
                new(ClaimantName: "Jane Doe", ClaimNumber: "C-1", VehicleRegistration: "AB12CDE")),
            CancellationToken.None);

        await AssertPrincipalFeeAsync(factory.Services, identity.CaseId, "99.99");
    }

    private static async Task SetQdosFeeAsync(IServiceProvider services, decimal fee)
    {
        await using var scope = services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        await context.Principals
            .Where(item => item.Code == QdosPrincipal.Code)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DefaultFee, fee));
    }

    private static async Task AssertPrincipalFeeAsync(IServiceProvider services, Guid caseId, string expected)
    {
        await using var scope = services.CreateAsyncScope();
        await using var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var fee = await context.CaseAssessmentFields.AsNoTracking().SingleAsync(item =>
            item.WorkId == caseId && item.FieldPath == AssessmentVocabulary.AgreedFee);
        Assert.Equal(expected, fee.Value);
        Assert.Equal(nameof(ActorKind.Automation), fee.RecordedByKind);
        Assert.Equal(PrincipalDefaultFeePolicy.RecorderId, fee.RecordedBy);
    }
}
