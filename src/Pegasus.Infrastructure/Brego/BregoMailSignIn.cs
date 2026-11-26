namespace Pegasus.Infrastructure.Brego;

/// <summary>
/// Signing the Brego account in from Brego's own e-mail (26 November 2026,
/// R1294, R1295), the one flow behind Refresh login on the Integrations page
/// and a Get valuation that repairs a lost sign-in by itself: the saved
/// password signs in, or Brego is asked for a sign-in link and the link is
/// read from the account's mailbox and redeemed; when Brego then asks to
/// approve Pegasus's device, the six-digit code it e-mails is read and
/// entered. Each wait polls the mailbox every <see cref="PollInterval"/> for
/// at most <see cref="PollLimit"/> on the host's <see cref="TimeProvider"/>.
/// It stores nothing: its callers store what it signs in.
/// </summary>
internal sealed class BregoMailSignIn(IBregoSignInMailReader reader, TimeProvider timeProvider)
{
    /// <summary>The stage of a failure to read the sign-in link from the mailbox.</summary>
    internal const string LinkStage = "mail_link";

    /// <summary>The stage of a failure to read or enter the device code from the mailbox.</summary>
    internal const string CodeStage = "mail_code";

    internal const string NotArrived = "mail=not_arrived";
    internal const string Unreadable = "mail=unreadable";
    internal const string CodeNotConfirmed = "code=not_confirmed";

    internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan PollLimit = TimeSpan.FromSeconds(30);

    // Brego's mail arrives 2 to 6 seconds after it is sent; the mailbox's
    // received time and this host's clock may differ by a little.
    private static readonly TimeSpan Slack = TimeSpan.FromSeconds(5);

    /// <summary>The earliest receipt time of mail Brego sends from now on.</summary>
    public DateTimeOffset Since() => timeProvider.GetUtcNow() - Slack;

    /// <summary>Whether the account's mailbox can be read on this host; any failure is "no".</summary>
    public async Task<bool> CanReadAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.CanReadAsync(email, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// The account's Cognito tokens: from its saved <paramref name="password"/>
    /// when it has one, otherwise from a sign-in link Brego is asked to send
    /// and that is read from the mailbox. A link that does not arrive, or a
    /// mailbox that stops being readable, fails at <see cref="LinkStage"/>.
    /// </summary>
    public async Task<BregoTokens> SignInAsync(
        BregoClient client,
        string email,
        string? password,
        CancellationToken cancellationToken)
    {
        if (password is not null)
        {
            return await client.PasswordSignInAsync(email, password, timeProvider, cancellationToken);
        }

        var since = Since();
        await client.RequestLinkAsync(email, cancellationToken);
        string? link;
        try
        {
            link = await PollAsync(token => reader.ReadLinkAsync(email, since, token), cancellationToken);
        }
        catch (BregoSignInMailUnreadableException)
        {
            throw new BregoFailure(LinkStage, null, Unreadable);
        }

        if (link is null || !BregoClient.TryReadLinkToken(link, email, out var linkToken, out _))
        {
            throw new BregoFailure(LinkStage, null, NotArrived);
        }

        return await client.RedeemLinkAsync(email, linkToken, cancellationToken);
    }

    /// <summary>
    /// Enters the code Brego e-mailed after it answered that the device needs
    /// approval, reading the mailbox for mail received since
    /// <paramref name="sinceUtc"/>, and reads the approval again. False when
    /// no code arrived, the mailbox could not be read, Brego refused the code
    /// or the device is still not approved; Brego not answering throws.
    /// </summary>
    public async Task<bool> ApproveByMailAsync(
        BregoClient client,
        string email,
        BregoTokens tokens,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        string? code;
        try
        {
            code = await PollAsync(token => reader.ReadCodeAsync(email, sinceUtc, token), cancellationToken);
        }
        catch (BregoSignInMailUnreadableException)
        {
            return false;
        }

        if (code is null)
        {
            return false;
        }

        try
        {
            await client.VerifyDeviceAsync(tokens.AccessToken, code, cancellationToken);
        }
        catch (BregoFailure failure) when (!failure.Transport
            && (failure.Status is null || (int)failure.Status.Value < 500))
        {
            return false;
        }

        return await client.IsDeviceApprovedAsync(tokens.AccessToken, cancellationToken);
    }

    private async Task<string?> PollAsync(
        Func<CancellationToken, Task<string?>> read,
        CancellationToken cancellationToken)
    {
        var deadline = timeProvider.GetUtcNow() + PollLimit;
        while (true)
        {
            if (await read(cancellationToken) is { } found)
            {
                return found;
            }

            if (timeProvider.GetUtcNow() + PollInterval > deadline)
            {
                return null;
            }

            await Task.Delay(PollInterval, timeProvider, cancellationToken);
        }
    }
}
