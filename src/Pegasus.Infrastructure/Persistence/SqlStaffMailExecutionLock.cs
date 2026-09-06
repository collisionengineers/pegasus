using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Operations;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class SqlStaffMailExecutionLock(
    IDbContextFactory<PegasusDbContext> contextFactory) : IStaffMailExecutionLock
{
    public async Task<IAsyncDisposable> AcquireAsync(
        Guid operationId, CancellationToken cancellationToken)
    {
        var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        try
        {
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource, @LockMode, @LockOwner, @LockTimeout; SELECT @result;";
            Add(command, "@Resource", $"staff-mail:{operationId:D}");
            Add(command, "@LockMode", "Exclusive");
            Add(command, "@LockOwner", "Session");
            Add(command, "@LockTimeout", 0);
            var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture);
            if (result < 0)
                throw new InvalidOperationException("The staff mail operation is already executing.");
            return new Held(db, $"staff-mail:{operationId:D}");
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }

    private static void Add(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name; parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed class Held(PegasusDbContext db, string resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "DECLARE @result int; EXEC @result = sp_releaseapplock @Resource, @LockOwner; SELECT @result;";
                Add(command, "@Resource", resource);
                Add(command, "@LockOwner", "Session");
                var result = Convert.ToInt32(await command.ExecuteScalarAsync(),
                    System.Globalization.CultureInfo.InvariantCulture);
                if (result < 0)
                    throw new InvalidOperationException("The staff mail execution lock could not be released.");
            }
            finally
            {
                await db.DisposeAsync();
            }
        }
    }
}
