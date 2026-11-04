using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Notifications;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Persistence.Migrations;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The temporary <c>-p</c> Case/PO suffix (operator, 4 November 2026; R656):
/// while the Administrator's switch is on every minted Case/PO ends in it,
/// off it mints without, and the migration that shipped it gave every
/// existing Case the suffix once. The suite's databases start with the switch
/// off (<see cref="LocalDbTestDatabase.SetCaseReferenceSuffixAsync"/>), so
/// these tests switch it on themselves. The host clock is in 2031.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReferenceSuffixTests
{
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    [Fact]
    public async Task TheSwitchEndsEveryNewCasePoInTheSuffixAndOffMintsWithout()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        await using var scope = factory.Services.CreateAsyncScope();
        var control = scope.ServiceProvider.GetRequiredService<ICaseReferenceSuffixControl>();
        Assert.False(await control.IsEnabledAsync(CancellationToken.None));

        await control.SetEnabledAsync(true, Administrator, $"suffix-on:{Guid.NewGuid():N}", CancellationToken.None);

        Assert.True(await control.IsEnabledAsync(CancellationToken.None));
        Assert.Equal("a.QDOS31001-p", (await CreateAsync(factory.Services, QdosPrincipal.Code, CaseType.Audit)).Reference);
        Assert.Equal("t.QDOS31002-p", (await CreateAsync(factory.Services, QdosPrincipal.Code, CaseType.Triage)).Reference);
        Assert.Equal("PV12ATE-p", (await CreateAsync(factory.Services, null, CaseType.Inspection)).Reference);
        Assert.Equal("a.PV12ATE.2-p", (await CreateAsync(factory.Services, null, CaseType.Audit)).Reference);

        await control.SetEnabledAsync(false, Administrator, $"suffix-off:{Guid.NewGuid():N}", CancellationToken.None);

        Assert.Equal("a.QDOS31003", (await CreateAsync(factory.Services, QdosPrincipal.Code, CaseType.Audit)).Reference);
        Assert.Equal("PV12ATE.3", (await CreateAsync(factory.Services, null, CaseType.Inspection)).Reference);
        await using var context = await ContextAsync(factory.Services);
        Assert.Equal(
            ["case_reference_suffix_disabled", "case_reference_suffix_enabled"],
            await context.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "case_reference_suffix")
                .Select(item => item.EventKind)
                .OrderBy(kind => kind)
                .ToArrayAsync());
    }

    [Fact]
    public async Task OnlyAnAdministratorCanSwitchTheSuffix()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var control = scope.ServiceProvider.GetRequiredService<ICaseReferenceSuffixControl>();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => control.SetEnabledAsync(
            true,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            $"suffix-on:{Guid.NewGuid():N}",
            CancellationToken.None));

        Assert.False(await control.IsEnabledAsync(CancellationToken.None));
    }

    /// <summary>
    /// The shipping migration's rewrite: every Case's Case/PO and Audit
    /// reference take the suffix, a notification's shown reference and reply
    /// link follow, and a second run changes nothing.
    /// </summary>
    [Fact]
    public async Task TheMigrationGivesEveryCaseTheSuffixOnce()
    {
        using var factory = new IntakeWebApplicationFactory(initializeDevelopmentOffline: false);
        await AllocationTestData.SeedPrincipalAsync(factory.Services, QdosPrincipal.Code);
        var inspection = await CreateAsync(factory.Services, QdosPrincipal.Code, CaseType.InspectionAndAudit);
        var oneOff = await CreateAsync(factory.Services, null, CaseType.Inspection);
        Assert.Equal("QDOS31001", inspection.Reference);
        var messageId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        await using var context = await ContextAsync(factory.Services);
        await context.Cases
            .Where(item => item.Id == inspection.CaseId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AuditReference, "a.QDOS31001"));
        var notificationId = Guid.NewGuid();
        context.Set<StaffNotificationEntity>().Add(new()
        {
            Id = notificationId,
            StaffId = Guid.NewGuid(),
            CaseId = inspection.CaseId,
            Reference = "QDOS31001",
            Cause = "AiDraftReady",
            Route = StaffNotificationPolicy.MessageReplyRoute(messageId, "QDOS31001", jobId),
            RaisedAtUtc = IntakeWebApplicationFactory.FixedUtcNow
        });
        await context.SaveChangesAsync();

        await context.Database.ExecuteSqlRawAsync(CaseReferenceSuffix.RewriteSql);
        await context.Database.ExecuteSqlRawAsync(CaseReferenceSuffix.RewriteSql);

        var cases = await context.Cases.AsNoTracking()
            .Where(item => item.Id == inspection.CaseId || item.Id == oneOff.CaseId)
            .ToDictionaryAsync(item => item.Id);
        Assert.Equal("QDOS31001-p", cases[inspection.CaseId].Reference);
        Assert.Equal("a.QDOS31001-p", cases[inspection.CaseId].AuditReference);
        Assert.Equal("PV12ATE-p", cases[oneOff.CaseId].Reference);
        Assert.Null(cases[oneOff.CaseId].AuditReference);
        var notification = await context.Set<StaffNotificationEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == notificationId);
        Assert.Equal("QDOS31001-p", notification.Reference);
        Assert.Equal(StaffNotificationPolicy.MessageReplyRoute(messageId, "QDOS31001-p", jobId), notification.Route);
    }

    private static async Task<CaseIdentity> CreateAsync(
        IServiceProvider services,
        string? principalCode,
        CaseType caseType)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICreateManualCase>().ExecuteAsync(
            new(
                Administrator,
                $"suffix-create:{Guid.NewGuid():N}",
                principalCode,
                caseType,
                principalCode is null
                    ? new(ClaimantName: "Pat Private", VehicleRegistration: "PV12ATE")
                    : new(ClaimantName: "Jane Doe", ClaimNumber: "C-1", VehicleRegistration: "AB12CDE")),
            CancellationToken.None);
    }

    private static async Task<PegasusDbContext> ContextAsync(IServiceProvider services) =>
        await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();
}
