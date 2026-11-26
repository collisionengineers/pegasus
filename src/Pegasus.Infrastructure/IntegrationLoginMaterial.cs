using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure;

/// <summary>
/// One integration's login as its provider reads it on each call (R1288,
/// ADR-0091): the decrypted values and the generation they were stored
/// under. Server-only secret material: never serialized to a browser, MCP,
/// history or log, and its <c>ToString</c> prints none of it.
/// </summary>
internal sealed class IntegrationLoginMaterial
{
    /// <summary>The payload name of Brego's stored Cognito refresh token.</summary>
    internal const string RefreshTokenName = "refreshToken";

    /// <summary>The payload name of Brego's stored <c>brego-session</c> cookie.</summary>
    internal const string SessionCookieName = "sessionCookie";

    private readonly IReadOnlyDictionary<string, string> values;

    internal IntegrationLoginMaterial(
        IntegrationLogin login,
        long generation,
        IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Login = login;
        Generation = generation;
        this.values = values;
    }

    public IntegrationLogin Login { get; }

    /// <summary>Moves on each replacement, never on a Brego token renewal.</summary>
    public long Generation { get; }

    /// <summary>Brego's stored Cognito refresh token, when the account has been linked.</summary>
    public string? RefreshToken => Named(RefreshTokenName);

    /// <summary>Brego's stored <c>brego-session</c> cookie, once one has been minted.</summary>
    public string? SessionCookie => Named(SessionCookieName);

    /// <summary>A login built from its fields, as a test or the store hands one over.</summary>
    internal static IntegrationLoginMaterial Create(
        IntegrationLogin login,
        long generation,
        IReadOnlyDictionary<IntegrationLoginField, string> fields,
        string? refreshToken = null,
        string? sessionCookie = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var named = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (field, value) in fields)
        {
            named[PayloadName(field)] = value;
        }

        if (refreshToken is not null)
        {
            named[RefreshTokenName] = refreshToken;
        }

        if (sessionCookie is not null)
        {
            named[SessionCookieName] = sessionCookie;
        }

        return new(login, generation, named);
    }

    /// <summary>The value of <paramref name="field"/>, or <see langword="null"/> when the login holds none.</summary>
    public string? Value(IntegrationLoginField field) => Named(PayloadName(field));

    /// <summary>A field's name in the protected JSON payload.</summary>
    internal static string PayloadName(IntegrationLoginField field) =>
        field switch
        {
            IntegrationLoginField.Username => "username",
            IntegrationLoginField.Email => "email",
            IntegrationLoginField.Password => "password",
            IntegrationLoginField.ClientId => "clientId",
            IntegrationLoginField.ClientSecret => "clientSecret",
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

    /// <summary>
    /// A usable value of <paramref name="field"/> for an account class: present,
    /// not blank and free of control characters. The refusal names the login
    /// and the field, never a value.
    /// </summary>
    internal string Require(IntegrationLoginField field, string loginName)
    {
        var value = Value(field);
        if (string.IsNullOrWhiteSpace(value) || HasControlCharacter(value))
        {
            throw new InvalidOperationException($"The {loginName} login has no usable {field}.");
        }

        return value;
    }

    public override string ToString() => nameof(IntegrationLoginMaterial);

    private string? Named(string name) => values.TryGetValue(name, out var value) ? value : null;

    /// <summary>A plain loop, run on each press: no enumerator and no delegate.</summary>
    internal static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}
