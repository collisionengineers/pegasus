using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// The Brego valuation account signed in by an Administrator on the
/// Integrations page (26 November 2026, R1290, issue #2180; ADR-0084): Brego
/// e-mails a sign-in link, the pasted link is redeemed, or the saved password
/// signs in; when Brego asks to approve Pegasus's device it e-mails a
/// six-digit code, and the sign-in waits, protected in the login's row, until
/// the code is entered or the access token it holds expires. A finished
/// sign-in mints the one-year <c>brego-session</c> and is stored with the
/// account under its next generation, which the valuation provider then
/// renews from. Where this host can read the account's own mailbox
/// (R1294), Refresh login does all of it from Brego's e-mail
/// (<see cref="BregoMailSignIn"/>), and every sign-in reads the code from the
/// mailbox before it falls back to waiting for it. No token, password, link or
/// code reaches an exception message, a log line or a history row.
/// </summary>
internal sealed partial class BregoAccountLink(
    IHttpClientFactory httpClientFactory,
    EfIntegrationLoginStore store,
    TimeProvider timeProvider,
    ILogger<BregoAccountLink> logger,
    BregoMailSignIn? mail = null) : IBregoAccountLink
{

    private const string PendingClientId = "clientId";
    private const string PendingAccessToken = "accessToken";
    private const string PendingIdToken = "idToken";
    private const string PendingRefreshToken = "refreshToken";
    private const string PendingExpiresIn = "expiresIn";

    public async Task SendLinkAsync(
        ActionActor actor,
        string operationKey,
        CancellationToken cancellationToken)
    {
        Require(actor, operationKey);
        var account = await ReadAccountAsync(cancellationToken);
        await CallAsync(
            "send_link",
            async client =>
            {
                await client.RequestLinkAsync(account.Email, cancellationToken);
                return true;
            },
            BregoLinkRefusal.NotOffered);
    }

    public async Task<BregoLinkStep> LinkAsync(
        ActionActor actor,
        string link,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        Require(actor, operationKey);
        var account = await ReadAccountAsync(cancellationToken);
        RequireVersion(account, expectedVersion);
        if (!BregoClient.TryReadLinkToken(link, account.Email, out var token, out var anotherAccount))
        {
            throw new BregoLinkRefusedException(
                anotherAccount ? BregoLinkRefusal.AnotherAccount : BregoLinkRefusal.NoToken);
        }

        string linkToken = token;
        var tokens = await CallAsync(
            "link",
            client => client.RedeemLinkAsync(account.Email, linkToken, cancellationToken),
            BregoLinkRefusal.LinkRefused);
        return await ApproveOrWaitAsync(
            "link", actor, account, tokens, expectedVersion, operationKey, BregoLinkRefusal.LinkRefused, cancellationToken);
    }

    public async Task<BregoLinkStep> ConfirmCodeAsync(
        ActionActor actor,
        string code,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        Require(actor, operationKey);
        var entered = code?.Trim() ?? "";
        if (!IsSixDigits(entered))
        {
            throw new BregoLinkRefusedException(BregoLinkRefusal.CodeFormat);
        }

        var account = await ReadAccountAsync(cancellationToken);
        RequireVersion(account, expectedVersion);
        if (account.Pending is not { } pending || !TryReadTokens(pending, out var tokens))
        {
            throw new BregoLinkRefusedException(BregoLinkRefusal.NoLinkWaiting);
        }

        // The code is submitted before the approval is read again, so Brego
        // sends no second code; the sign-in keeps waiting when it is refused.
        await CallAsync(
            "code",
            async client =>
            {
                await client.VerifyDeviceAsync(tokens.AccessToken, entered, cancellationToken);
                return true;
            },
            BregoLinkRefusal.CodeRefused);
        var approved = await CallAsync(
            "code",
            client => client.IsDeviceApprovedAsync(tokens.AccessToken, cancellationToken),
            BregoLinkRefusal.CodeRefused);
        if (!approved)
        {
            throw new BregoLinkRefusedException(BregoLinkRefusal.CodeRefused);
        }

        await FinishAsync("code", actor, account, tokens, expectedVersion, operationKey, BregoLinkRefusal.CodeRefused, cancellationToken);
        return BregoLinkStep.Linked;
    }

    public async Task<BregoLinkStep> SignInWithPasswordAsync(
        ActionActor actor,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        Require(actor, operationKey);
        var account = await ReadAccountAsync(cancellationToken);
        RequireVersion(account, expectedVersion);
        if (account.Password is not { } password)
        {
            throw new BregoLinkRefusedException(BregoLinkRefusal.NoAccount);
        }

        var tokens = await CallAsync(
            "password",
            client => client.PasswordSignInAsync(account.Email, password, timeProvider, cancellationToken),
            BregoLinkRefusal.NotOffered);
        return await ApproveOrWaitAsync(
            "password", actor, account, tokens, expectedVersion, operationKey, BregoLinkRefusal.NotOffered, cancellationToken);
    }

    public bool ReadsSignInMail => mail is not null;

    public async Task<BregoLinkStep> RefreshAsync(
        ActionActor actor,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken)
    {
        Require(actor, operationKey);
        var account = await ReadAccountAsync(cancellationToken);
        RequireVersion(account, expectedVersion);

        // A host that cannot read the mailbox asks nothing of Brego, so Brego
        // sends no e-mail that nobody will read.
        if (mail is not { } mailSignIn || !await mailSignIn.CanReadAsync(account.Email, cancellationToken))
        {
            throw new BregoLinkRefusedException(BregoLinkRefusal.MailUnreadable);
        }

        var otherwise = account.Password is null ? BregoLinkRefusal.LinkRefused : BregoLinkRefusal.NotOffered;
        var tokens = await CallAsync(
            "refresh",
            client => mailSignIn.SignInAsync(client, account.Email, account.Password, cancellationToken),
            otherwise);
        return await ApproveOrWaitAsync(
            "refresh", actor, account, tokens, expectedVersion, operationKey, otherwise, cancellationToken);
    }

    public async Task<BregoPendingCode?> ReadPendingAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        var account = await store.ReadBregoLinkAsync(cancellationToken);
        return account is { Pending: not null, PendingExpiresAtUtc: { } expiresAt }
            ? new BregoPendingCode(account.Email, expiresAt)
            : null;
    }

    /// <summary>
    /// Finishes the sign-in on an approved device. On one Brego wants a code
    /// for, the code is read from the account's mailbox where this host can
    /// read it (R1294); otherwise, or when it does not arrive or is refused,
    /// the sign-in waits for the code until its access token expires.
    /// </summary>
    private async Task<BregoLinkStep> ApproveOrWaitAsync(
        string act,
        ActionActor actor,
        BregoLinkState account,
        BregoTokens tokens,
        long expectedVersion,
        string operationKey,
        BregoLinkRefusal otherwise,
        CancellationToken cancellationToken)
    {
        var since = mail?.Since() ?? timeProvider.GetUtcNow();
        var approved = await CallAsync(
            act,
            client => client.IsDeviceApprovedAsync(tokens.AccessToken, cancellationToken),
            otherwise);
        if (!approved && mail is { } mailSignIn && await mailSignIn.CanReadAsync(account.Email, cancellationToken))
        {
            approved = await CallAsync(
                act,
                client => mailSignIn.ApproveByMailAsync(client, account.Email, tokens, since, cancellationToken),
                otherwise);
        }

        if (!approved)
        {
            await store.WriteBregoPendingAsync(
                expectedVersion,
                PendingPayload(tokens),
                timeProvider.GetUtcNow().AddSeconds(tokens.ExpiresIn),
                cancellationToken);
            return BregoLinkStep.CodeSent;
        }

        await FinishAsync(act, actor, account, tokens, expectedVersion, operationKey, otherwise, cancellationToken);
        return BregoLinkStep.Linked;
    }

    private async Task FinishAsync(
        string act,
        ActionActor actor,
        BregoLinkState account,
        BregoTokens tokens,
        long expectedVersion,
        string operationKey,
        BregoLinkRefusal otherwise,
        CancellationToken cancellationToken)
    {
        var session = await CallAsync(
            act,
            client => client.MintSessionAsync(account.Email, tokens, timeProvider, cancellationToken),
            otherwise);
        await store.WriteBregoSignInAsync(
            actor,
            expectedVersion,
            new BregoStoredSignIn(session.BregoSessionCookie, session.RefreshToken),
            operationKey,
            cancellationToken);
    }

    private async Task<BregoLinkState> ReadAccountAsync(CancellationToken cancellationToken) =>
        await store.ReadBregoLinkAsync(cancellationToken)
            ?? throw new BregoLinkRefusedException(BregoLinkRefusal.NoAccount);

    /// <summary>
    /// A version that moved since the page read it is refused before Brego is
    /// asked, so a link, which redeems once, is not used up.
    /// </summary>
    private static void RequireVersion(BregoLinkState account, long expectedVersion)
    {
        if (account.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException();
        }
    }

    private static void Require(ActionActor actor, string operationKey)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
    }

    /// <summary>
    /// One Brego call. A refusal is named by the stage it came from; no
    /// answer, a throttle or a failure of Brego's own is
    /// <see cref="BregoLinkRefusal.Unreachable"/>. The log names the act, the
    /// refusal and the stage, never a value.
    /// </summary>
    private async Task<T> CallAsync<T>(
        string act,
        Func<BregoClient, Task<T>> call,
        BregoLinkRefusal otherwise)
    {
        try
        {
            return await call(new BregoClient(httpClientFactory.CreateClient(BregoGuideValuationProvider.HttpClientName)));
        }
        catch (BregoFailure failure)
        {
            var refusal = Refusal(failure, otherwise);
            LogRefused(
                logger,
                act,
                refusal,
                failure.Stage,
                failure.Detail,
                refusal == BregoLinkRefusal.Unreachable && failure.Transport ? failure : null);
            throw new BregoLinkRefusedException(refusal, failure);
        }
    }

    internal static BregoLinkRefusal Refusal(BregoFailure failure, BregoLinkRefusal otherwise)
    {
        if (failure.Transport
            || failure.Status is { } status
                && ((int)status >= 500 || status == HttpStatusCode.TooManyRequests))
        {
            return BregoLinkRefusal.Unreachable;
        }

        return failure.Stage switch
        {
            "authorizer" or "link_start" or "link_request" => BregoLinkRefusal.NotOffered,
            "link_redeem" => BregoLinkRefusal.LinkRefused,
            "password" => failure.Forbidden ? BregoLinkRefusal.PasswordRefused : BregoLinkRefusal.NotOffered,
            "device_verify" => BregoLinkRefusal.CodeRefused,
            BregoMailSignIn.LinkStage => failure.Detail == BregoMailSignIn.Unreadable
                ? BregoLinkRefusal.MailUnreadable
                : BregoLinkRefusal.MailNotArrived,
            _ => otherwise,
        };
    }

    private static bool IsSixDigits(string code)
    {
        if (code.Length != 6)
        {
            return false;
        }

        foreach (var character in code)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, string> PendingPayload(BregoTokens tokens) =>
        new(StringComparer.Ordinal)
        {
            [PendingClientId] = tokens.ClientId,
            [PendingAccessToken] = tokens.AccessToken,
            [PendingIdToken] = tokens.IdToken,
            [PendingRefreshToken] = tokens.RefreshToken,
            [PendingExpiresIn] = tokens.ExpiresIn.ToString(CultureInfo.InvariantCulture)
        };

    private static bool TryReadTokens(Dictionary<string, string> pending, out BregoTokens tokens)
    {
        tokens = null!;
        if (!pending.TryGetValue(PendingClientId, out var clientId)
            || !pending.TryGetValue(PendingAccessToken, out var accessToken)
            || !pending.TryGetValue(PendingIdToken, out var idToken)
            || !pending.TryGetValue(PendingRefreshToken, out var refreshToken)
            || !pending.TryGetValue(PendingExpiresIn, out var expiresIn)
            || !int.TryParse(expiresIn, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(accessToken)
            || string.IsNullOrWhiteSpace(idToken)
            || string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        tokens = new BregoTokens(clientId, accessToken, idToken, refreshToken, seconds);
        return true;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Brego {Act} on the Integrations page was refused as {Refusal} at stage={Stage} {Detail}")]
    private static partial void LogRefused(
        ILogger logger, string act, BregoLinkRefusal refusal, string stage, string detail, Exception? exception);
}

/// <summary>
/// Brego's login as the Integrations page signs it in: its version and
/// generation, e-mail address, saved password, and the sign-in that waits for
/// a code. Its <c>ToString</c> prints none of it.
/// </summary>
internal sealed class BregoLinkState(
    long version,
    long generation,
    string email,
    string? password,
    Dictionary<string, string>? pending,
    DateTimeOffset? pendingExpiresAtUtc)
{
    public long Version { get; } = version;

    public long Generation { get; } = generation;

    public string Email { get; } = email;

    public string? Password { get; } = password;

    public Dictionary<string, string>? Pending { get; } = pending;

    public DateTimeOffset? PendingExpiresAtUtc { get; } = pendingExpiresAtUtc;

    public override string ToString() => nameof(BregoLinkState);
}
