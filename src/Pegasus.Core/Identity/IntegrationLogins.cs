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
            IntegrationLogin.Cazana or IntegrationLogin.Brego => EmailAndPassword,
            IntegrationLogin.ExperianHistory or IntegrationLogin.ExperianCap => ExperianAccount,
            _ => throw new ArgumentOutOfRangeException(nameof(login))
        };

    /// <summary>
    /// Whether <paramref name="field"/> must be given whenever
    /// <paramref name="login"/> is replaced. Every field is, except Brego's
    /// password: Brego signs its account in by e-mailed link, and with a
    /// password saved Pegasus signs in with it whenever it holds no usable
    /// sign-in (26 November 2026, R1290).
    /// </summary>
    public static bool IsRequired(IntegrationLogin login, IntegrationLoginField field) =>
        !(login == IntegrationLogin.Brego && field == IntegrationLoginField.Password);

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
    /// must hold the login's fields (<see cref="IntegrationLoginFields.Of"/>)
    /// and no other, every required one (<see cref="IntegrationLoginFields.IsRequired"/>)
    /// not blank; an optional field left blank is not stored.
    /// <paramref name="expectedVersion"/> is the version read (0 when the
    /// login was never set). Replacing Brego's account clears its stored
    /// sign-in and any sign-in waiting for a code: the account then signs in
    /// with its password, or is linked again (<see cref="IBregoAccountLink"/>).
    /// </summary>
    Task<IntegrationLoginStatus> ReplaceAsync(
        ActionActor actor,
        IntegrationLogin login,
        long expectedVersion,
        IReadOnlyDictionary<IntegrationLoginField, string> values,
        string operationKey,
        CancellationToken cancellationToken);
}

/// <summary>Where a Brego sign-in stands after a step on the Integrations page.</summary>
public enum BregoLinkStep
{
    /// <summary>The account is signed in and its sign-in is stored.</summary>
    Linked,

    /// <summary>Brego e-mailed a six-digit code to the account; the sign-in waits for it.</summary>
    CodeSent
}

/// <summary>Why a Brego sign-in step was refused. The page names each in its own approved sentence.</summary>
public enum BregoLinkRefusal
{
    /// <summary>No Brego account is saved, it can no longer be read, or a password sign-in was asked of an account with no password.</summary>
    NoAccount,

    /// <summary>Brego does not offer this sign-in for the account: it names no sign-in pool, or answers without a link or a password challenge.</summary>
    NotOffered,

    /// <summary>The pasted text holds no sign-in token.</summary>
    NoToken,

    /// <summary>The pasted link is for another e-mail address than the account's.</summary>
    AnotherAccount,

    /// <summary>Brego refused the link: it is used, expired or not the latest one sent.</summary>
    LinkRefused,

    /// <summary>Brego refused the stored password.</summary>
    PasswordRefused,

    /// <summary>The code entered is not six digits; Brego was not asked.</summary>
    CodeFormat,

    /// <summary>Brego refused the code; the sign-in still waits for one until it expires.</summary>
    CodeRefused,

    /// <summary>No sign-in waits for a code, or the one that did has expired.</summary>
    NoLinkWaiting,

    /// <summary>Brego could not be reached, or answered with a failure of its own.</summary>
    Unreachable,

    /// <summary>Brego's sign-in e-mail did not arrive in the account's mailbox in time (R1294).</summary>
    MailNotArrived,

    /// <summary>The account's mailbox could not be read on this host, so Brego was not asked (R1294).</summary>
    MailUnreadable
}

/// <summary>A Brego sign-in step Brego or the account refused. The message names the refusal, never a value.</summary>
public sealed class BregoLinkRefusedException : Exception
{
    public BregoLinkRefusedException(BregoLinkRefusal refusal)
        : base($"The Brego sign-in was refused: {refusal}.")
    {
        Refusal = refusal;
    }

    public BregoLinkRefusedException(BregoLinkRefusal refusal, Exception innerException)
        : base($"The Brego sign-in was refused: {refusal}.", innerException)
    {
        Refusal = refusal;
    }

    public BregoLinkRefusal Refusal { get; }
}

/// <summary>A Brego sign-in that waits for the six-digit code e-mailed to <paramref name="Email"/>.</summary>
public sealed record BregoPendingCode(string Email, DateTimeOffset ExpiresAtUtc);

/// <summary>
/// The Administrator's port for signing the Brego valuation account in on
/// the Integrations page (26 November 2026, R1290, issue #2180): asking Brego
/// for a sign-in link and redeeming the pasted link, signing in with the
/// saved password, and entering the six-digit code Brego e-mails when it
/// asks to approve Pegasus's device. A finished sign-in is stored with the
/// account and moves its generation; one that waits for a code is kept,
/// protected, until the code is entered or it expires. Every step requires
/// <see cref="StaffAccessRight.ManageAutomationClients"/>. A step that is
/// refused throws <see cref="BregoLinkRefusedException"/>; an
/// <c>expectedVersion</c> that is no longer the login's version throws the
/// store's concurrency exception, as
/// <see cref="IIntegrationLoginAdministration.ReplaceAsync"/> does.
/// </summary>
public interface IBregoAccountLink
{
    /// <summary>
    /// Asks Brego to e-mail a sign-in link to the saved account's address.
    /// Nothing is written.
    /// </summary>
    Task SendLinkAsync(
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>
    /// Redeems the pasted sign-in <paramref name="link"/> (the whole link, or
    /// its <c>#magic-link=</c> fragment) for the saved account.
    /// </summary>
    Task<BregoLinkStep> LinkAsync(
        ActionActor actor,
        string link,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>Enters the six-digit <paramref name="code"/> the waiting sign-in asked for.</summary>
    Task<BregoLinkStep> ConfirmCodeAsync(
        ActionActor actor,
        string code,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>Signs the saved account in with its saved password.</summary>
    Task<BregoLinkStep> SignInWithPasswordAsync(
        ActionActor actor,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken);

    /// <summary>The sign-in that waits for a code, or <see langword="null"/> when none waits.</summary>
    Task<BregoPendingCode?> ReadPendingAsync(
        ActionActor actor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether this host can read Brego's sign-in e-mail in the account's own
    /// mailbox (R1294): true when a mail reader is composed here. The mailbox
    /// may still refuse to be read; <see cref="RefreshAsync"/> then answers
    /// <see cref="BregoLinkRefusal.MailUnreadable"/>.
    /// </summary>
    bool ReadsSignInMail { get; }

    /// <summary>
    /// Refresh login (R1294): signs the saved account in with its password, or
    /// asks Brego for a sign-in link and reads it from the account's mailbox,
    /// and enters the code Brego e-mails when it asks to approve the device.
    /// Nothing is asked of Brego when the mailbox cannot be read
    /// (<see cref="BregoLinkRefusal.MailUnreadable"/>). A link that does not
    /// arrive is <see cref="BregoLinkRefusal.MailNotArrived"/> (it was sent, so
    /// it may still be pasted); a code that does not arrive leaves the sign-in
    /// waiting for it (<see cref="BregoLinkStep.CodeSent"/>).
    /// </summary>
    Task<BregoLinkStep> RefreshAsync(
        ActionActor actor,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken);
}
