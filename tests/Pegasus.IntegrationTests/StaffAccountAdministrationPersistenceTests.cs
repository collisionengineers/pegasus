using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffAccountAdministrationPersistenceTests
{
    [Fact]
    public async Task StaffAccountQueryProjectsOneRoleAndItsVersion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var user = new PegasusIdentityUser { Id = Guid.NewGuid(), UserName = "single-role" };

        Assert.True((await userManager.CreateAsync(user, "Password-1")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.Engineer)).Succeeded);

        var account = await services.GetRequiredService<IStaffAccountQueries>().GetAsync(user.Id, default);

        Assert.NotNull(account);
        Assert.Equal(StaffRole.Engineer, account!.Role);
        Assert.Equal(0, account.Version);
    }

    [Fact]
    public async Task SignOffChoicesExcludeDisabledAndUnsignedAccounts()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var valid = await CreateSignOffAccountAsync(userManager, "valid-sign-off", enabled: true, signed: true);
        _ = await CreateSignOffAccountAsync(userManager, "disabled-sign-off", enabled: false, signed: true);
        _ = await CreateSignOffAccountAsync(userManager, "unsigned-sign-off", enabled: true, signed: false);

        var choices = await services.GetRequiredService<IStaffAccountQueries>()
            .ListSignOffEngineersAsync(default);

        Assert.Equal(valid.Id, Assert.Single(choices).StaffId);
    }

    [Fact]
    public async Task AdministratorAccountsAreEligibleForEngineerAssignment()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var administrator = await CreateStaffAccountAsync(
            userManager, "administrator-engineer-choice", StaffRole.Administrator);
        var engineer = await CreateStaffAccountAsync(
            userManager, "engineer-choice", StaffRole.Engineer);
        _ = await CreateStaffAccountAsync(userManager, "user-choice", StaffRole.User);

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var choices = await services.GetRequiredService<ICaseEngineerChoices>().GetAsync(actor, default);
        var administratorEligibility = await services.GetRequiredService<ICaseEngineerEligibility>()
            .GetAsync(administrator.Id, default);

        Assert.Equal([administrator.Id, engineer.Id], choices.Select(choice => choice.StaffId));
        Assert.True(administratorEligibility.HasEngineerRole);
    }

    [Fact]
    public async Task DeleteRefusesAnEngineerOnOpenCasesThenRemovesTheRowAndItsDependents()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var administrator = await CreateStaffAccountAsync(
            userManager, "delete-administrator", StaffRole.Administrator);
        var engineer = await CreateStaffAccountAsync(userManager, "delete-engineer", StaffRole.Engineer);
        var actor = ActionActor.Staff(administrator.Id, [StaffRole.Administrator]);
        var context = services.GetRequiredService<PegasusDbContext>();
        var caseId = await SeedCaseAsync(context, "DEL-1", engineer.Id);
        context.Set<StaffNotificationEntity>().Add(new StaffNotificationEntity
        {
            Id = Guid.NewGuid(),
            StaffId = engineer.Id,
            CaseId = caseId,
            Reference = "DEL-1",
            Cause = nameof(Pegasus.Core.Notifications.StaffNotificationCause.CaseAssigned),
            Route = "/Cases/" + caseId.ToString("D"),
            RaisedAtUtc = DateTimeOffset.UtcNow
        });
        await context.SaveChangesAsync();
        var version = (await context.Users.AsNoTracking().SingleAsync(item => item.Id == engineer.Id)).Version;
        var lease = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(EditScopeKind.StaffAccount, engineer.Id, version, actor, "delete-claim"),
            default);
        var delete = services.GetRequiredService<IDeleteStaffAccount>();

        // Open case: refused, and the account is still there.
        var refused = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            delete.ExecuteAsync(new(actor, engineer.Id, null, "delete-open", version, lease.Token), default));
        Assert.Equal(StaffAccountAdministrationError.AssignedToOpenCases, refused.Error);
        Assert.NotNull(await context.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == engineer.Id));

        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.State = nameof(CaseLifecycleState.PostReportComplete);
        await context.SaveChangesAsync();

        var result = await delete.ExecuteAsync(
            new(actor, engineer.Id, null, "delete-closed", version, lease.Token), default);

        Assert.False(result.WasReplay);
        Assert.Null(await context.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == engineer.Id));
        Assert.Empty(await context.UserRoles.AsNoTracking().Where(item => item.UserId == engineer.Id).ToListAsync());
        Assert.Empty(await context.Set<StaffNotificationEntity>().AsNoTracking()
            .Where(item => item.StaffId == engineer.Id).ToListAsync());
        var closed = await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == caseId);
        Assert.Equal(engineer.Id, closed.AssignedEngineerId);
        Assert.Single(await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateId == engineer.Id.ToString("D") && item.EventKind == "staff_account_deleted")
            .ToListAsync());

        // The same operation key is the same deletion, answered without a row to act on.
        var replay = await delete.ExecuteAsync(
            new(actor, engineer.Id, null, "delete-closed", version, lease.Token), default);
        Assert.True(replay.WasReplay);
    }

    private static async Task<Guid> SeedCaseAsync(PegasusDbContext context, string reference, Guid engineerId)
    {
        var occurredAtUtc = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        context.AddRange(
            new OrganizationEntity { Id = organizationId, Name = "Staff delete test " + reference, Version = 0 },
            new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = occurredAtUtc },
            new PrincipalEntity
            {
                Id = principalId,
                OrganizationId = organizationId,
                SequenceLineageId = lineageId,
                Code = reference,
                IsActive = true,
                Version = 0
            },
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "delete-origin.pdf",
                MediaType = "application/pdf",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = "delete:" + receiptId.ToString("N"),
                ReceivedAtUtc = occurredAtUtc,
                ProcessedAtUtc = occurredAtUtc,
                SourceReaderKey = "delete-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Staff delete test",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = principalId,
                SequenceLineageId = lineageId,
                Year = 2031,
                Sequence = 1,
                Reference = reference,
                Type = "Inspection",
                InitialState = "NotReady",
                CustodyState = "confirmed",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = occurredAtUtc,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = nameof(CaseLifecycleState.Review),
                AssignedEngineerId = engineerId,
                Version = 1,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }

    private static async Task<PegasusIdentityUser> CreateSignOffAccountAsync(
        UserManager<PegasusIdentityUser> userManager,
        string userName,
        bool enabled,
        bool signed)
    {
        var user = new PegasusIdentityUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsEnabled = enabled,
            IsSignOffEngineer = true,
            SignOffPrintedName = userName,
            SignOffSignature = signed ? [0x89, 0x50, 0x4e, 0x47] : null
        };
        Assert.True((await userManager.CreateAsync(user, "Password-1")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, StaffRoleNames.Engineer)).Succeeded);
        return user;
    }

    private static async Task<PegasusIdentityUser> CreateStaffAccountAsync(
        UserManager<PegasusIdentityUser> userManager,
        string userName,
        StaffRole role)
    {
        var user = new PegasusIdentityUser { Id = Guid.NewGuid(), UserName = userName };
        Assert.True((await userManager.CreateAsync(user, "Password-1")).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, role.ToString())).Succeeded);
        return user;
    }
}
