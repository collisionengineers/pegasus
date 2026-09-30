using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffAccountAdministrationPersistenceTests
{
    [Fact]
    public async Task ActorDisplayNamesResolveManyStaffIdsInOneSqlCommand()
    {
        var commandCounter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(commandCounter),
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var enabled = await CreateStaffAccountAsync(
            userManager, "actor-display-enabled", StaffRole.Administrator);
        var disabled = await CreateStaffAccountAsync(
            userManager, "actor-display-disabled", StaffRole.Engineer);
        disabled.IsEnabled = false;
        Assert.True((await userManager.UpdateAsync(disabled)).Succeeded);
        var missingId = Guid.NewGuid();

        commandCounter.Reset();
        var names = await ActorDisplayNames.ResolveStaffNamesAsync(
            services.GetRequiredService<IStaffAccountQueries>(),
            [enabled.Id, disabled.Id, enabled.Id, missingId, Guid.Empty],
            default);

        Assert.Equal(1, commandCounter.Count);
        // A name lookup never reads the password hash, the security stamps or
        // the sign-off signature bytes of the accounts it resolves.
        var statement = Assert.Single(commandCounter.Statements);
        Assert.DoesNotContain("[PasswordHash]", statement, StringComparison.Ordinal);
        Assert.DoesNotContain("[SecurityStamp]", statement, StringComparison.Ordinal);
        Assert.DoesNotContain("[ConcurrencyStamp]", statement, StringComparison.Ordinal);
        Assert.Contains("DATALENGTH(", statement, StringComparison.Ordinal);
        AssertSignatureReadOnlyAsItsLength(statement);
        Assert.Equal("actor-display-enabled", names[enabled.Id]);
        Assert.Equal("actor-display-disabled", names[disabled.Id]);
        Assert.False(names.ContainsKey(missingId));
        Assert.Equal(
            ActorDisplayNames.FormerStaff,
            ActorDisplayNames.Resolve(ActorKind.Staff, missingId.ToString("D"), names));
    }

    /// <summary>
    /// The list, the single-account read and the batch read build the same
    /// summary: enabled state, role, version and the sign-off facts, with "has
    /// a signature" worked out in SQL from the stored length (an empty
    /// signature is none).
    /// </summary>
    [Fact]
    public async Task EveryAccountReadCarriesTheSameSummaryWithoutTheSignatureBytes()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter),
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var signed = await CreateSignOffAccountAsync(
            userManager, "summary-signed", enabled: true, signed: true, StaffRole.Engineer);
        var unsigned = await CreateSignOffAccountAsync(
            userManager, "summary-unsigned", enabled: false, signed: false, StaffRole.User);
        var plain = await CreateStaffAccountAsync(userManager, "summary-plain", StaffRole.Administrator);
        var queries = services.GetRequiredService<IStaffAccountQueries>();

        counter.Reset();
        var slice = await queries.ListAsync(0, 10, default);
        var listStatements = counter.Statements;
        var many = await queries.GetManyAsync([signed.Id, unsigned.Id, plain.Id], default);

        Assert.Equal(3, slice.Accounts.Count);
        foreach (var listed in slice.Accounts)
        {
            counter.Reset();
            var single = Assert.IsType<StaffAccountSummary>(await queries.GetAsync(listed.Id, default));
            // The single-account read carries neither the password hash nor
            // the signature bytes, and reads the role in the same statement.
            var singleStatements = counter.Statements;
            Assert.All(singleStatements, statement =>
            {
                Assert.DoesNotContain("[PasswordHash]", statement, StringComparison.Ordinal);
                Assert.DoesNotContain("[SecurityStamp]", statement, StringComparison.Ordinal);
                AssertSignatureReadOnlyAsItsLength(statement);
            });
            Assert.Contains("DATALENGTH(", Assert.Single(singleStatements), StringComparison.Ordinal);
            Assert.Equal(single, listed);
            Assert.Equal(single, Assert.Single(many, item => item.Id == listed.Id));
        }

        var signedSummary = Assert.Single(slice.Accounts, item => item.Id == signed.Id);
        Assert.True(signedSummary.SignOff.HasSignature);
        Assert.True(signedSummary.SignOff.IsSignOffEngineer);
        Assert.Equal(StaffRole.Engineer, signedSummary.Role);
        Assert.False(Assert.Single(slice.Accounts, item => item.Id == unsigned.Id).SignOff.HasSignature);
        Assert.False(Assert.Single(slice.Accounts, item => item.Id == unsigned.Id).IsEnabled);
        Assert.False(Assert.Single(slice.Accounts, item => item.Id == plain.Id).SignOff.HasSignature);
        Assert.All(listStatements, statement =>
        {
            Assert.DoesNotContain("[PasswordHash]", statement, StringComparison.Ordinal);
            Assert.DoesNotContain("[SecurityStamp]", statement, StringComparison.Ordinal);
            AssertSignatureReadOnlyAsItsLength(statement);
        });
        // The list is the users and then their roles.
        Assert.Equal(2, listStatements.Count);
    }

    [Fact]
    public async Task BatchStaffReadKeepsTheExactlyOneRoleInvariant()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureServices: IdentityPersistenceTestServices.Configure);
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<PegasusIdentityUser>>();
        var single = await CreateStaffAccountAsync(
            userManager, "batch-single-role", StaffRole.Engineer);
        // The unique index on AspNetUserRoles.UserId makes a second role
        // impossible; the reachable breach is an account with no role row.
        var roleless = new PegasusIdentityUser { Id = Guid.NewGuid(), UserName = "batch-no-role" };
        Assert.True((await userManager.CreateAsync(roleless, "Password-1")).Succeeded);
        var queries = services.GetRequiredService<IStaffAccountQueries>();

        var one = Assert.Single(await queries.GetManyAsync([single.Id], default));
        Assert.Equal(StaffRole.Engineer, one.Role);
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetAsync(roleless.Id, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetManyAsync([single.Id, roleless.Id], default));
    }

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
        var valid = await CreateSignOffAccountAsync(
            userManager, "valid-sign-off", enabled: true, signed: true, StaffRole.User);
        _ = await CreateSignOffAccountAsync(userManager, "disabled-sign-off", enabled: false, signed: true);
        _ = await CreateSignOffAccountAsync(userManager, "unsigned-sign-off", enabled: true, signed: false);

        var choices = await services.GetRequiredService<IStaffAccountQueries>()
            .ListSignOffEngineersAsync(default);

        Assert.Equal(valid.Id, Assert.Single(choices).StaffId);
    }

    [Fact]
    public async Task EveryEnabledStaffRoleIsEligibleForEngineerAssignment()
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
        var user = await CreateStaffAccountAsync(userManager, "user-choice", StaffRole.User);

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var choices = await services.GetRequiredService<ICaseEngineerChoices>().GetAsync(actor, default);
        var administratorEligibility = await services.GetRequiredService<ICaseEngineerEligibility>()
            .GetAsync(administrator.Id, default);

        Assert.Equal([administrator.Id, engineer.Id, user.Id], choices.Select(choice => choice.StaffId));
        Assert.True(administratorEligibility.AccountExists);
        Assert.True(administratorEligibility.IsEnabled);
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
        var delete = services.GetRequiredService<IDeleteStaffAccount>();

        // Open case: refused, and the account is still there.
        var refused = await Assert.ThrowsAsync<StaffAccountAdministrationException>(() =>
            delete.ExecuteAsync(new(actor, engineer.Id, null, "delete-open", version), default));
        Assert.Equal(StaffAccountAdministrationError.AssignedToOpenCases, refused.Error);
        Assert.NotNull(await context.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == engineer.Id));

        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.State = nameof(CaseLifecycleState.PostReportComplete);
        await context.SaveChangesAsync();

        var result = await delete.ExecuteAsync(
            new(actor, engineer.Id, null, "delete-closed", version), default);

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
            new(actor, engineer.Id, null, "delete-closed", version), default);
        Assert.True(replay.WasReplay);
    }

    /// <summary>
    /// The signature column appears only as its length, never as bytes.
    /// Asserting <c>DATALENGTH</c> alone would pass a query that read both.
    /// </summary>
    private static void AssertSignatureReadOnlyAsItsLength(string statement) =>
        Assert.DoesNotContain(
            "[SignOffSignature]",
            statement.Replace("DATALENGTH([a].[SignOffSignature])", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);

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
                Type = "inspection",
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
        bool signed,
        StaffRole role = StaffRole.Engineer)
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
        Assert.True((await userManager.AddToRoleAsync(user, role.ToString())).Succeeded);
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
