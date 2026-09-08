using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Pegasus.Core;

namespace Pegasus.Web.Mcp;

internal sealed class DataProtectionCursorProtector(IDataProtectionProvider provider) : ICursorProtector
{
    private const int Version = 1;
    private const int MaximumCursorLength = 4096;
    private const int MaximumKeyLength = 1024;
    private const int MaximumScopeLength = 4096;
    private const string Purpose = "Pegasus.CursorPaging.v1";

    public string Protect(string scope, string sortKey, Guid id)
    {
        ValidateScope(scope);
        if (string.IsNullOrWhiteSpace(sortKey) || sortKey.Length > MaximumKeyLength)
            throw new ArgumentException("A bounded cursor sort key is required.", nameof(sortKey));
        if (id == Guid.Empty)
            throw new ArgumentException("A cursor identifier is required.", nameof(id));
        var cursor = provider.CreateProtector(Purpose, scope).Protect(
            JsonSerializer.Serialize(new Payload(Version, sortKey, id)));
        if (cursor.Length > MaximumCursorLength)
            throw new ArgumentException("The cursor payload is too large.", nameof(sortKey));
        return cursor;
    }

    public (string SortKey, Guid Id) Unprotect(string cursor, string scope)
    {
        ValidateScope(scope);
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > MaximumCursorLength)
            throw new CursorRejectedException();
        try
        {
            var json = provider.CreateProtector(Purpose, scope).Unprotect(cursor);
            var payload = JsonSerializer.Deserialize<Payload>(json);
            if (payload is null || payload.Version != Version || payload.Id == Guid.Empty
                || string.IsNullOrWhiteSpace(payload.SortKey)
                || payload.SortKey.Length > MaximumKeyLength)
                throw new CursorRejectedException();
            return (payload.SortKey, payload.Id);
        }
        catch (CursorRejectedException) { throw; }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            throw new CursorRejectedException(exception);
        }
    }

    private static void ValidateScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope) || scope.Length > MaximumScopeLength)
            throw new ArgumentException("A bounded cursor query scope is required.", nameof(scope));
    }

    private sealed record Payload(int Version, string SortKey, Guid Id);
}
