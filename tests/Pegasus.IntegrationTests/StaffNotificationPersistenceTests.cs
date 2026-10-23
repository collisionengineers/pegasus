using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Notifications;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class StaffNotificationPersistenceTests
{
    /// <summary>
    /// Every page carries the bell's unread figure, so it is counted in SQL in
    /// one command without reading a row, over the person's own unread
    /// notifications inside the window.
    /// </summary>
    [Fact]
    public async Task TheUnreadCountIsOneCountCommandOverThePersonsUnreadRowsInTheWindow()
    {
        var commandCounter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(commandCounter));
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffNotificationStore>();
        var person = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var first = await store.AddAsync(Raised(person, "QDOS26001"), default);
        await store.AddAsync(Raised(person, "QDOS26002"), default);
        await store.AddAsync(Raised(person, "QDOS26003"), default);
        await store.AddAsync(Raised(someoneElse, "QDOS26004"), default);
        await store.MarkReadAsync(person, first.Id, DateTimeOffset.UtcNow, default);
        var window = DateTimeOffset.UtcNow.AddDays(-30);

        commandCounter.Reset();
        var unread = await store.CountUnreadAsync(person, window, default);

        Assert.Equal(2, unread);
        Assert.Equal(1, commandCounter.Count);
        var statement = Assert.Single(commandCounter.Statements);
        Assert.Contains("COUNT(", statement, StringComparison.Ordinal);
        Assert.DoesNotContain("[Route]", statement, StringComparison.Ordinal);
        Assert.Equal(1, await store.CountUnreadAsync(someoneElse, window, default));

        var later = Guid.NewGuid();
        await store.AddAsync(Raised(later, "QDOS26005"), default);
        Assert.Equal(0, await store.CountUnreadAsync(later, DateTimeOffset.UtcNow.AddDays(1), default));
    }

    /// <summary>
    /// The unread figure is kept per person for 30 seconds (R409): a repeat
    /// read sends no command, and each write Web makes to the person's rows
    /// evicts it, so the next read counts again.
    /// </summary>
    [Fact]
    public async Task TheUnreadCountIsKeptPerPersonAndEveryWriteEvictsIt()
    {
        var commandCounter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(commandCounter));
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStaffNotificationStore>();
        var person = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var window = DateTimeOffset.UtcNow.AddDays(-30);
        var first = await store.AddAsync(Raised(person, "QDOS26011"), default);
        await store.AddAsync(Raised(someoneElse, "QDOS26012"), default);

        Assert.Equal(1, await store.CountUnreadAsync(person, window, default));
        commandCounter.Reset();
        Assert.Equal(1, await store.CountUnreadAsync(person, window, default));
        Assert.Equal(0, commandCounter.CountContaining("COUNT("));

        await store.AddAsync(Raised(person, "QDOS26013"), default);
        Assert.Equal(2, await store.CountUnreadAsync(person, window, default));
        Assert.Equal(1, commandCounter.CountContaining("COUNT("));

        await store.MarkReadAsync(person, first.Id, DateTimeOffset.UtcNow, default);
        Assert.Equal(1, await store.CountUnreadAsync(person, window, default));
        Assert.Equal(2, commandCounter.CountContaining("COUNT("));

        await store.MarkAllReadAsync(person, DateTimeOffset.UtcNow, default);
        Assert.Equal(0, await store.CountUnreadAsync(person, window, default));
        Assert.Equal(3, commandCounter.CountContaining("COUNT("));

        // Another person's entry is neither read nor evicted by these writes.
        Assert.Equal(1, await store.CountUnreadAsync(someoneElse, window, default));
        Assert.Equal(4, commandCounter.CountContaining("COUNT("));
        Assert.Equal(1, await store.CountUnreadAsync(someoneElse, window, default));
        Assert.Equal(4, commandCounter.CountContaining("COUNT("));
    }

    private static NewStaffNotification Raised(Guid staffId, string reference) => new(
        staffId, null, reference, null, StaffNotificationCause.CaseAssigned, "/", null);
}
