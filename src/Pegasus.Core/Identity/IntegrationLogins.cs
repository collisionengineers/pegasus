namespace Pegasus.Core.Identity;

/// <summary>
/// The logins Collision Engineers holds for its connected integrations, one
/// each, kept in the database and replaced by an Administrator (R1288,
/// ADR-0091). Each is kept encrypted as a member of staff's own Glass's login
/// is (ADR-0043) and is never shown back.
/// </summary>
public enum IntegrationLogin { GlassValuation, Cazana, Brego, ExperianHistory, ExperianCap }

/// <summary>One value a login is made of.</summary>
public enum IntegrationLoginField { Username, Email, Password, ClientId, ClientSecret }

/// <summary>Which values each login is made of, and which of them names the account.</summary>
public static class IntegrationLoginFields
{
    private static readonly IntegrationLoginField[] UsernameAndPassword =
        [IntegrationLoginField.Username, IntegrationLoginField.Password];

    private static readonly IntegrationLoginField[] EmailAndPassword =
        [IntegrationLoginField.Email, IntegrationLoginField.Password];

    // Brego signs in by e-mailed link: the account is named by its e-mail
    // address and its stored sign-in is kept beside it, never typed.
    private static readonly IntegrationLoginField[] EmailOnly =
        [IntegrationLoginField.Email];

    private static readonly IntegrationLoginField[] ExperianAccount =
    [
        IntegrationLoginField.ClientId,
        IntegrationLoginField.ClientSecret,
        IntegrationLoginField.Username,
        IntegrationLoginField.Password
    ];

    /// <summary>The values <paramref name="login"/> is made of, in the order they are entered.</summary>
    public static IReadOnlyList<IntegrationLoginField> Of(IntegrationLogin login) =>
        login switch
        {
            IntegrationLogin.GlassValuation => UsernameAndPassword,
            IntegrationLogin.Cazana => EmailAndPassword,
            IntegrationLogin.Brego => EmailOnly,
            IntegrationLogin.ExperianHistory or IntegrationLogin.ExperianCap => ExperianAccount,
            _ => throw new ArgumentOutOfRangeException(nameof(login))
        };

    /// <summary>
    /// The value that names the account and may be shown: the username or the
    /// e-mail address. No other value is ever shown, an Experian client id
    /// included.
    /// </summary>
    public static IntegrationLoginField AccountNameField(IntegrationLogin login) =>
        login switch
        {
            IntegrationLogin.GlassValuation
                or IntegrationLogin.ExperianHistory
                or IntegrationLogin.ExperianCap => IntegrationLoginField.Username,
            IntegrationLogin.Cazana or IntegrationLogin.Brego => IntegrationLoginField.Email,
            _ => throw new ArgumentOutOfRangeException(nameof(login))
        };
}

/// <summary>
/// What may be shown of one login: the account's name, when it was last
/// replaced and its version. <see cref="Readable"/> is false when a stored
/// login can no longer be decrypted (the key ring that protected it is gone);
/// it is then typed again.
/// </summary>
public sealed record IntegrationLoginStatus(
    IntegrationLogin Login,
    string? AccountName,
    DateTimeOffset? UpdatedAtUtc,
    long Version,
    bool Readable);

/// <summary>The Administrator's port over the integrations' logins.</summary>
public interface IIntegrationLoginAdministration
{
    /// <summary>Every login, set or not, in <see cref="IntegrationLogin"/> order.</summary>
    Task<IReadOnlyList<IntegrationLoginStatus>> ListAsync(
        ActionActor actor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replaces <paramref name="login"/> with <paramref name="values"/>, which
    /// must hold exactly the login's fields (<see cref="IntegrationLoginFields.Of"/>),
    /// none blank. <paramref name="expectedVersion"/> is the version read
    /// (0 when the login was never set). Replacing Brego's account clears its
    /// stored sign-in: a changed account is linked again.
    /// </summary>
    Task<IntegrationLoginStatus> ReplaceAsync(
        ActionActor actor,
        IntegrationLogin login,
        long expectedVersion,
        IReadOnlyDictionary<IntegrationLoginField, string> values,
        string operationKey,
        CancellationToken cancellationToken);
}
