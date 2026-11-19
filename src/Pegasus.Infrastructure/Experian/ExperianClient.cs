using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Pegasus.Infrastructure.Experian;

/// <summary>
/// The one way Pegasus calls Experian's Velocity API (ADR-0089): the sign-in
/// with the Experian account (OAuth2 password grant), the bearer token it
/// keeps, and <c>POST /v2/vehicle/</c>. Run Experian check and CAP's Get
/// valuation share it, so both use one token.
///
/// <para>
/// The account is read on each query, never when the client is built; neither
/// the credentials nor the token ever reaches a URL or log, and the
/// registration travels in the body. A call that stops is an
/// <see cref="ExperianFailure"/> naming its stage; a missing or unresolved
/// setting is the account's own <see cref="InvalidOperationException"/>.
/// </para>
/// </summary>
public sealed class ExperianClient(
    IHttpClientFactory httpClientFactory,
    Func<ExperianAccount> account,
    TimeProvider timeProvider) : IDisposable
{
    public const string HttpClientName = "Experian";

    /// <summary>Each call's one bound, headers and body together (the named client sets none).</summary>
    internal static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? token;
    private DateTimeOffset tokenExpiresAtUtc;

    /// <summary>
    /// One vehicle query, as its <c>VehicleQueryV2</c> JSON: the status and
    /// the parsed body (none when the body is empty or not JSON on a refusal).
    /// A token Experian has withdrawn early is renewed once.
    /// </summary>
    internal async Task<(HttpStatusCode Status, JsonDocument? Body)> QueryVehicleAsync(
        byte[] query,
        CancellationToken cancellationToken)
    {
        var signIn = account();
        var http = httpClientFactory.CreateClient(HttpClientName);
        var bearer = await TokenAsync(http, signIn, refresh: false, cancellationToken);
        var (status, body) = await QueryAsync(http, signIn, bearer, query, cancellationToken);
        if (status == HttpStatusCode.Unauthorized)
        {
            body?.Dispose();
            bearer = await TokenAsync(http, signIn, refresh: true, cancellationToken);
            (status, body) = await QueryAsync(http, signIn, bearer, query, cancellationToken);
        }

        return (status, body);
    }

    public void Dispose() => tokenLock.Dispose();

    /// <summary>
    /// Experian's own "not found" answer (<c>VehicleNotFoundResponse</c>), not a
    /// gateway's 404 for a wrong address.
    /// </summary>
    internal static bool IsVehicleNotFound(JsonDocument? body) =>
        body?.RootElement is { ValueKind: JsonValueKind.Object } root
        && root.TryGetProperty("errors", out var errors)
        && errors.ValueKind == JsonValueKind.Object;

    /// <summary>Experian's message ids, as " messages=13,100", for the log; nothing else of the answer.</summary>
    internal static string MessageIds(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("messages", out var messages)
            || messages.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var ids = new List<string>();
        foreach (var message in messages.EnumerateArray())
        {
            if (message.ValueKind == JsonValueKind.Object
                && message.TryGetProperty("id", out var id)
                && id.ValueKind is JsonValueKind.String or JsonValueKind.Number
                && id.ToString() is { Length: > 0 and <= 10 } text
                && text.All(char.IsAsciiDigit))
            {
                ids.Add(text);
            }
        }

        return ids.Count == 0 ? string.Empty : " messages=" + string.Join(',', ids);
    }

    /// <summary>
    /// The bearer token, from the in-process cache while it has a minute left,
    /// else a fresh sign-in. Experian's gateway takes the password grant as a
    /// JSON body with a <c>Grant_type</c> header.
    /// </summary>
    private async Task<string> TokenAsync(
        HttpClient http,
        ExperianAccount signIn,
        bool refresh,
        CancellationToken cancellationToken)
    {
        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (!refresh && token is not null && tokenExpiresAtUtc > timeProvider.GetUtcNow().AddMinutes(1))
            {
                return token;
            }

            var message = new HttpRequestMessage(HttpMethod.Post, signIn.TokenUri)
            {
                Content = JsonContent(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string>
                {
                    ["username"] = signIn.Username,
                    ["password"] = signIn.Password,
                    ["client_id"] = signIn.ClientId,
                    ["client_secret"] = signIn.ClientSecret,
                })),
            };
            message.Headers.TryAddWithoutValidation("Grant_type", "password");
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var (status, body) = await SendAsync("token", http, message, cancellationToken);
            using (body)
            {
                if ((int)status is < 200 or > 299)
                {
                    throw new ExperianFailure("token", status, null);
                }

                if (body?.RootElement is not { ValueKind: JsonValueKind.Object } root
                    || !root.TryGetProperty("access_token", out var accessToken)
                    || accessToken.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(accessToken.GetString())
                    || ExpiresInSeconds(root) is not { } seconds)
                {
                    throw new ExperianFailure("token", null, "token=malformed");
                }

                token = accessToken.GetString()!;
                tokenExpiresAtUtc = timeProvider.GetUtcNow().AddSeconds(seconds);
                return token;
            }
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private static Task<(HttpStatusCode Status, JsonDocument? Body)> QueryAsync(
        HttpClient http,
        ExperianAccount signIn,
        string bearer,
        byte[] query,
        CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, VehicleUri(signIn.BaseUri))
        {
            Content = JsonContent(query),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return SendAsync("vehicle", http, message, cancellationToken);
    }

    /// <summary>
    /// One call, named by its stage: the status and the parsed body (none when
    /// the body is empty or not JSON on a refusal). <see cref="CallTimeout"/>
    /// bounds the call; the caller's own cancellation passes through.
    /// </summary>
    private static async Task<(HttpStatusCode Status, JsonDocument? Body)> SendAsync(
        string stage,
        HttpClient http,
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using (message)
        using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            bounded.CancelAfter(CallTimeout);
            try
            {
                using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded.Token);
                var bytes = await response.Content.ReadAsByteArrayAsync(bounded.Token);
                var succeeded = (int)response.StatusCode is >= 200 and <= 299;
                JsonDocument? body = null;
                if (bytes.Length > 0)
                {
                    try
                    {
                        body = JsonDocument.Parse(bytes);
                    }
                    catch (JsonException) when (!succeeded)
                    {
                        // A refusal's body is read only for the not-found answer.
                    }
                }

                return (response.StatusCode, body);
            }
            catch (JsonException)
            {
                throw new ExperianFailure(stage, null, "json=invalid");
            }
            catch (IOException exception)
            {
                throw new ExperianFailure(stage, null, "body=unreadable", exception);
            }
            catch (HttpRequestException exception)
            {
                throw new ExperianFailure(stage, null, "transport", exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ExperianFailure(stage, null, "transport", exception);
            }
        }
    }

    private static ByteArrayContent JsonContent(byte[] body)
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static Uri VehicleUri(Uri baseUri)
    {
        var root = baseUri.AbsoluteUri.EndsWith('/') ? baseUri.AbsoluteUri : baseUri.AbsoluteUri + "/";
        return new Uri(new Uri(root), "v2/vehicle/");
    }

    /// <summary><c>expires_in</c> as Experian sends it: a number, or a number in a string.</summary>
    private static double? ExpiresInSeconds(JsonElement root) =>
        root.TryGetProperty("expires_in", out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetDouble(out var seconds) && seconds > 0 => seconds,
                JsonValueKind.String when double.TryParse(
                    value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    && seconds > 0 => seconds,
                _ => null,
            }
            : null;
}

/// <summary>Where and how one Experian call stopped. Its message carries no secret.</summary>
internal sealed class ExperianFailure(
    string stage,
    HttpStatusCode? status,
    string? detail,
    Exception? innerException = null)
    : Exception($"Experian {stage} failed.", innerException)
{
    public string Stage { get; } = stage;

    public HttpStatusCode? Status { get; } = status;

    public string? Detail { get; } = detail;
}
