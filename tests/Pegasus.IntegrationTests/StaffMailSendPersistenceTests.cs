using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Email;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffMailSendPersistenceTests
{
    [Fact]
    public async Task SameOperationAndPayloadReplaysButChangedPayloadConflicts()
    {
        await using var database = await CreateDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var command = Command();
        var now = new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);

        var first = await store.PrepareAsync(command, new string('A', 64), now, CancellationToken.None);
        var replay = await store.PrepareAsync(command, new string('A', 64), now, CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.PrepareAsync(command, new string('B', 64), now, CancellationToken.None));
    }

    [Fact]
    public async Task SubmittedOperationIsAvailableAfterAStoreRestartForReadOnlyReconciliation()
    {
        await using var database = await CreateDatabaseAsync();
        var command = Command();
        Guid operationId;
        await using (var scope = database.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
            var operation = await store.PrepareAsync(
                command, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft, null, null, null, null,
                CancellationToken.None);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.DraftReady, StaffMailAttemptStage.Attach, "draft-id", null, null, null,
                CancellationToken.None);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.Sending, StaffMailAttemptStage.Send, "draft-id", null, null, null,
                CancellationToken.None);
            operation = await store.TransitionAsync(command.Actor.SubjectId, operation.Id, operation.Version,
                StaffMailState.Submitted, StaffMailAttemptStage.ObserveSent, "draft-id",
                DateTimeOffset.UtcNow, null, null, CancellationToken.None);
            operationId = operation.Id;
        }

        var historyCount = await database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = 'StaffMailSend' AND AggregateId = '{operationId:D}'");
        Assert.Equal(5, historyCount);

        await using var restarted = database.CreateAsyncScope();
        var candidate = await restarted.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
            .GetExecutionAsync(command.Actor.SubjectId, operationId, CancellationToken.None);

        Assert.NotNull(candidate);
        Assert.Equal(operationId, candidate!.Operation.Id);
        Assert.Equal("draft-id", candidate.DraftImmutableId);
    }

    [Fact]
    public async Task ReplicaTransitionsCannotBothClaimTheSameSendStage()
    {
        await using var database = await CreateDatabaseAsync();
        var command = Command();
        StaffMailOperation prepared;
        await using (var prepareScope = database.CreateAsyncScope())
        {
            prepared = await prepareScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
                .PrepareAsync(command, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
        }
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();

        var attempts = await Task.WhenAll(
            ClaimAsync(first),
            ClaimAsync(second));

        Assert.Equal(1, attempts.Count(value => value));

        async Task<bool> ClaimAsync(IStaffMailSendStore store)
        {
            try
            {
                await store.TransitionAsync(
                    command.Actor.SubjectId, prepared.Id, prepared.Version,
                    StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft,
                    null, null, null, null, CancellationToken.None);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }

    [Fact]
    public async Task UploadProgressRestartsFromProtectedPerAttachmentState()
    {
        await using var database = await CreateDatabaseAsync();
        var command = Command();
        Guid operationId;
        var attachmentVersionId = Guid.NewGuid();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(10);
        await using (var firstScope = database.CreateAsyncScope())
        {
            var operation = await firstScope.ServiceProvider.GetRequiredService<IStaffMailSendStore>()
                .PrepareAsync(command, new string('A', 64), DateTimeOffset.UtcNow, CancellationToken.None);
            operationId = operation.Id;
            await firstScope.ServiceProvider.GetRequiredService<IStaffMailUploadProgress>().SaveAsync(
                operationId, attachmentVersionId,
                new(new Uri("https://upload.example.test/session-secret"), expiry, 327680),
                CancellationToken.None);
        }

        var protectedValue = await database.ScalarAsync<string>(
            $"SELECT ProtectedUploadSession FROM StaffMailSendOperations WHERE Id = '{operationId:D}'");
        Assert.DoesNotContain("session-secret", protectedValue, StringComparison.Ordinal);
        await using var restarted = database.CreateAsyncScope();
        var resumed = await restarted.ServiceProvider.GetRequiredService<IStaffMailUploadProgress>()
            .GetAsync(operationId, attachmentVersionId, CancellationToken.None);

        Assert.NotNull(resumed);
        Assert.Equal(327680, resumed.NextOffset);
        Assert.Equal(expiry, resumed.ExpiresAtUtc);
        Assert.Equal("https://upload.example.test/session-secret", resumed.UploadUrl!.AbsoluteUri);
    }

    [Fact]
    public async Task EnabledAccountWithoutCurrentCaseworkRoleCannotSend()
    {
        await using var database = await CreateDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var staffId = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Users.Add(new PegasusIdentityUser
            {
                Id = staffId, UserName = "mail.user", NormalizedUserName = "MAIL.USER",
                IsEnabled = true, SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });
            await db.SaveChangesAsync();
        }
        var store = scope.ServiceProvider.GetRequiredService<IStaffMailSendStore>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            store.RequireCurrentStaffAsync(staffId.ToString("D"), CancellationToken.None));

        await using (var db = await factory.CreateDbContextAsync())
        {
            var role = await db.Roles.SingleOrDefaultAsync(value => value.NormalizedName == "USER");
            if (role is null)
            {
                role = new IdentityRole<Guid>(StaffRoleNames.User)
                {
                    Id = Guid.NewGuid(), NormalizedName = "USER"
                };
                db.Roles.Add(role);
            }
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = role.Id });
            await db.SaveChangesAsync();
        }

        await store.RequireCurrentStaffAsync(staffId.ToString("D"), CancellationToken.None);
    }

    [Fact]
    public async Task ReplicaExecutionLockExcludesConcurrentAttachmentFlowAndReleases()
    {
        await using var database = await CreateDatabaseAsync();
        var operationId = Guid.NewGuid();
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IStaffMailExecutionLock>();
        var second = secondScope.ServiceProvider.GetRequiredService<IStaffMailExecutionLock>();
        await using (await first.AcquireAsync(operationId, CancellationToken.None))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                second.AcquireAsync(operationId, CancellationToken.None));
        }
        await using var reacquired = await second.AcquireAsync(operationId, CancellationToken.None);
    }

    private static StaffMailSendCommand Command() => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), Guid.NewGuid(), 1,
        StaffMailPurpose.GeneralCorrespondence, Guid.NewGuid(), 1,
        StaffMailComposeMode.New, null, [new("recipient@example.invalid", null)], [],
        "Subject", "Body", [], "operation-key");

    private static Task<LocalDbTestDatabase> CreateDatabaseAsync() =>
        LocalDbTestDatabase.CreateAsync(configureServices: services =>
        {
            services.AddScoped<EfStaffMailSendStore>();
            services.AddScoped<IStaffMailSendStore>(provider =>
                provider.GetRequiredService<EfStaffMailSendStore>());
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddScoped<IStaffMailUploadProgress, EfStaffMailUploadProgress>();
            services.AddScoped<IStaffMailExecutionLock, SqlStaffMailExecutionLock>();
        });
}
