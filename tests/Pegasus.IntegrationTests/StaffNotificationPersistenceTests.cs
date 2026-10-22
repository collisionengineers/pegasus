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
        Assert.Equal(0, await store.CountUnreadAsync(person, DateTimeOffset.UtcNow.AddDays(1), default));

        await store.MarkAllReadAsync(person, DateTimeOffset.UtcNow, default);
        Assert.Equal(0, await store.CountUnreadAsync(person, window, default));
        Assert.Equal(1, await store.CountUnreadAsync(someoneElse, window, default));
    }

    private static NewStaffNotification Raised(Guid staffId, string reference) => new(
        staffId, null, reference, null, StaffNotificationCause.CaseAssigned, "/", null);
}
