using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Pegasus.Core.Eva;

namespace Pegasus.Infrastructure.Eva;

/// <summary>
/// The EVA API client, and the only place in Pegasus that talks to EVA
/// (EXT-04).
///
/// Four things about EVA's API drive almost every decision in this file, and
/// all four are departures from what its own documentation says:
///
/// 1. **The token endpoint is not OAuth2.** It is form-urlencoded with
///    <c>Client_Id</c> and <c>Client_Secret</c> — PascalCase, underscored, no
///    <c>grant_type</c> — and its <c>expires_in</c> is measured in **minutes**.
///    Reading it as seconds yields a token treated as expiring in five
///    seconds, which turns every submission into two round trips.
/// 2. **The response envelope is camelCase**, not the PascalCase the
///    documentation specifies, so deserialization is case-insensitive.
/// 3. **A rejection can arrive inside an HTTP 200**, carrying its own
///    <c>statusCode</c> in the body.
/// 4. **A 500 arrives as <c>text/plain</c>**, so no response body may be
///    assumed to be JSON.
///
/// Failures are returned, never thrown: the four-outcome model is the contract
/// FRD-07 requires, and an exception cannot express "we do not know whether
/// this was delivered".
/// </summary>
internal sealed partial class EvaApiTransport(
    EvaApiOptions options,
    HttpClient httpClient,
    TimeProvider timeProvider) : IEvaApiTransport, IDisposable
{
    /// <summary>
    /// How early a cached token is abandoned. EVA's default token life is five
    /// minutes and a submission carries every image of a case, so it can take
    /// a while; renewing this far ahead keeps a long upload from outliving the
    /// token it started with.
    /// </summary>
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Case-insensitive because the live API answers <c>statusCode</c> where
    /// the documentation promises <c>StatusCode</c>, and both must read.
    /// </summary>
    private static readonly JsonSerializerOptions CaseInsensitive =
        new() { PropertyNameCaseInsensitive = true };

    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? token;
    private DateTimeOffset tokenExpiresAtUtc;

    public void Dispose() => tokenLock.Dispose();

    public async Task<EvaSubmissionResult> SubmitInstructionAsync(
        EvaInstructionPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var authorization = await GetTokenAsync(forceRefresh: false, cancellationToken);
        if (authorization.Failure is { } failure)
        {
            return failure;
        }

        var response = await PostInstructionAsync(
            payload,
            authorization.Token!,
            cancellationToken);

        // One retry, and only on 401. A token can expire between the margin
        // check and the request arriving; re-minting once costs a round trip
        // and turns a spurious failure into a success. It is not a general
        // retry: anything else that fails, fails, because EVA has no
        // idempotency and a blind resend can duplicate the claim.
        if (response.Status == HttpStatusCode.Unauthorized)
        {
            var refreshed = await GetTokenAsync(forceRefresh: true, cancellationToken);
            if (refreshed.Failure is { } refreshFailure)
            {
                return refreshFailure;
            }

            response = await PostInstructionAsync(
                payload,
                refreshed.Token!,
                cancellationToken);
        }

        return Interpret(response, payload.Files.Count);
    }

    private async Task<EvaResponse> PostInstructionAsync(
        EvaInstructionPayload payload,
        string authorization,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.InstructionUri)
            {
                Content = EvaInstructionSerializer.CreateContent(payload)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorization);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new(response.StatusCode, body, null);
        }
        // A request that never produced a response leaves delivery genuinely
        // unknown — EVA may have created the claim before the connection died,
        // and there is no way to ask. Caught rather than thrown so the caller
        // records Unknown and the retry policy, not an exception handler,
        // decides what happens next.
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            return new(null, null, exception.Message);
        }
    }

    /// <summary>
    /// EVA's answer read in the four terms FRD-07 requires stay distinct. The
    /// classification itself belongs to Core; this only supplies the facts.
    /// </summary>
    private static EvaSubmissionResult Interpret(EvaResponse response, int fileCount)
    {
        var envelope = ReadEnvelope(response.Body);
        var identifier = Trimmed(envelope?.Id);
        var outcome = EvaSubmissionPolicy.Classify(
            response.Status,
            envelope?.StatusCode,
            !string.IsNullOrEmpty(identifier));

        return new(
            outcome,
            identifier,
            ReadFileReference(envelope?.Message),
            EvaSubmissionPolicy.FailureCode(outcome, response.Status),
            outcome == EvaSubmissionOutcome.Succeeded
                ? null
                : Detail(response, envelope),
            outcome is EvaSubmissionOutcome.Succeeded or EvaSubmissionOutcome.Partial
                ? fileCount
                : 0);
    }

    /// <summary>
    /// Why it failed, in EVA's own words where it gave any.
    ///
    /// Truncated, because a <c>text/plain</c> 500 or an HTML error page can be
    /// arbitrarily long and this is written to a database column and shown to
    /// an operator.
    /// </summary>
    private static string? Detail(EvaResponse response, EvaEnvelope? envelope)
    {
        var text = Trimmed(envelope?.Message)
            ?? Trimmed(response.TransportError)
            ?? Trimmed(response.Body);
        return text is null || text.Length <= 500 ? text : text[..500];
    }

    /// <summary>
    /// The response envelope, when there is one.
    ///
    /// Case-insensitive because the live API answers <c>statusCode</c> while
    /// the documentation promises <c>StatusCode</c>. A body that is not JSON
    /// at all — EVA's <c>text/plain</c> 500 — yields null rather than
    /// throwing, and the HTTP status then decides the outcome alone.
    /// </summary>
    private static EvaEnvelope? ReadEnvelope(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvaEnvelope>(
                body,
                CaseInsensitive);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// EVA returns two identifiers and only one of them is a field. The other
    /// — the File Reference an operator actually quotes — is embedded in the
    /// human-readable message: "Inspection Request has been processed. File
    /// Reference: 61239". Absent or unparseable, the submission is still a
    /// success; the envelope id remains the durable link.
    /// </summary>
    private static string? ReadFileReference(string? message) =>
        message is not null && FileReferenceRegex().Match(message) is { Success: true } match
            ? match.Groups[1].Value
            : null;

    [GeneratedRegex(
        @"File Reference:\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        100)]
    private static partial Regex FileReferenceRegex();

    /// <summary>
    /// A bearer token, cached until shortly before it expires.
    ///
    /// The lock is what stops a burst of submissions each minting their own
    /// token: the first through does the round trip and the rest wait and find
    /// it already there, which is why the expiry is re-checked after the wait.
    /// </summary>
    private async Task<TokenResult> GetTokenAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (!forceRefresh && TryUseCachedToken(out var cached))
        {
            return new(cached, null);
        }

        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (forceRefresh)
            {
                token = null;
            }
            else if (TryUseCachedToken(out var waited))
            {
                return new(waited, null);
            }

            return await MintTokenAsync(cancellationToken);
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private bool TryUseCachedToken(out string? value)
    {
        value = token;
        return value is not null
            && tokenExpiresAtUtc > timeProvider.GetUtcNow() + RenewalMargin;
    }

    private async Task<TokenResult> MintTokenAsync(CancellationToken cancellationToken)
    {
        EvaResponse response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.TokenUri)
            {
                // EVA's own field names, which are neither the OAuth2 ones nor
                // camelCase. They are exactly as documented and as the working
                // reference client sends them.
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Client_Id"] = options.ClientId,
                    ["Client_Secret"] = options.ClientSecret
                })
            };
            using var message = await httpClient.SendAsync(request, cancellationToken);
            var body = await message.Content.ReadAsStringAsync(cancellationToken);
            response = new(message.StatusCode, body, null);
        }
        catch (Exception exception) when (exception is HttpRequestException
            or TaskCanceledException
            or IOException)
        {
            return Failed("eva_auth_unreachable", exception.Message);
        }

        if (response.Status != HttpStatusCode.OK)
        {
            // Only a refusal is terminal. A 4xx means EVA read the credentials
            // and said no, and the same pair will be refused again. Anything
            // else — a 500, a gateway timeout, a throttle — is EVA being
            // unavailable, and marking that terminal would strand every case
            // the sweep happened to touch during it, with no route back for a
            // principal that has no manual button.
            var outcome = EvaSubmissionPolicy.Classify(response.Status, null, false)
                == EvaSubmissionOutcome.Rejected
                ? EvaSubmissionOutcome.Rejected
                : EvaSubmissionOutcome.Unknown;
            return new(
                null,
                new(
                    outcome,
                    null,
                    null,
                    $"eva_auth_{(int)response.Status!}",
                    Detail(response, ReadEnvelope(response.Body)),
                    0));
        }

        EvaToken? minted;
        try
        {
            minted = JsonSerializer.Deserialize<EvaToken>(
                response.Body!,
                CaseInsensitive);
        }
        catch (JsonException)
        {
            return Failed("eva_auth_malformed", null);
        }

        if (minted is null
            || string.IsNullOrWhiteSpace(minted.AccessToken)
            || minted.ExpiresIn <= 0)
        {
            return Failed("eva_auth_malformed", null);
        }

        // MINUTES. EVA documents expires_in in minutes and returns 5 by
        // default; treating it as seconds would expire the token before the
        // submission it was minted for finished uploading.
        token = minted.AccessToken;
        tokenExpiresAtUtc = timeProvider.GetUtcNow()
            .AddMinutes(minted.ExpiresIn);
        return new(token, null);
    }

    /// <summary>
    /// An authentication failure that leaves delivery unknown rather than
    /// refused — we never reached the point of submitting anything, but we
    /// also cannot say EVA said no.
    /// </summary>
    private static TokenResult Failed(string code, string? detail) => new(
        null,
        new(EvaSubmissionOutcome.Unknown, null, null, code, detail, 0));

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record EvaResponse(
        HttpStatusCode? Status,
        string? Body,
        string? TransportError);

    private sealed record TokenResult(string? Token, EvaSubmissionResult? Failure);

    /// <summary>
    /// EVA's write-response envelope. Every member is optional because the
    /// live API and the documentation disagree about all three, and a missing
    /// one is a fact about the answer rather than a parse error.
    /// </summary>
    private sealed record EvaEnvelope(int? StatusCode, string? Message, string? Id);

    /// <summary>
    /// The token response, whose members are snake_case.
    ///
    /// Named explicitly rather than left to case-insensitive matching, which
    /// does not bridge an underscore: <c>access_token</c> would not have bound
    /// to <c>AccessToken</c>, every token would have read as malformed, and
    /// every submission would have failed as Unknown without ever reaching
    /// EVA.
    /// </summary>
    private sealed record EvaToken(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}

/// <summary>
/// Writes the instruction body in EVA's field names.
///
/// Hand-written rather than attribute-mapped because EVA's names
/// (<c>VehReg</c>, <c>ClmNo</c>, <c>InspType</c>) are a wire contract, not a
/// serialization convenience — and because "a field the case does not hold" is
/// answered here once, as an empty string, the same way the drag-and-drop
/// bundle answers it. EVA's request model reads every key it knows and ignores
/// the rest; it does not accept a null where it expects text.
/// </summary>
internal static class EvaInstructionSerializer
{
    public static HttpContent CreateContent(EvaInstructionPayload payload)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["RequestFrom"] = payload.RequestFrom,
            ["Agent"] = payload.Agent,
            ["ExternalRef"] = payload.ExternalRef,
            ["ClmNo"] = payload.ClaimNumber,
            ["InsName"] = payload.ClaimantName,
            ["VehReg"] = payload.VehicleRegistration,
            ["VehDesc"] = payload.VehicleDescription,
            ["DtIncident"] = FormatDate(payload.IncidentDate),
            ["Cause"] = payload.Cause,
            ["VatStat"] = payload.VatStatus,
            ["InspType"] = payload.InspectionType,
            ["CoverType"] = payload.CoverType,
            ["VehDriveable"] = payload.VehicleDriveable,
            ["InUse"] = payload.InUse,
            ["InstEmail"] = payload.InstructionEmail,
            ["InspLocName"] = payload.Location.Name,
            ["InspLocAdd"] = payload.Location.Address,
            ["InspLocTown"] = payload.Location.Town,
            ["InspLocCity"] = payload.Location.City,
            ["InspLocCounty"] = payload.Location.County,
            ["InspLocPCode"] = payload.Location.Postcode,
            ["NotesStr"] = payload.Notes,
            ["Files"] = payload.Files.Select(file => new Dictionary<string, string>
            {
                ["Name"] = file.Name,
                ["Extension"] = file.Extension,
                ["Data"] = Convert.ToBase64String(file.Content.Span)
            }).ToArray()
        };

        return JsonContent.Create(body);
    }

    /// <summary>
    /// EVA's dates are full instants with a trailing Z, at midnight UTC. A
    /// date the case does not hold is empty rather than a placeholder day.
    /// </summary>
    private static string FormatDate(DateOnly? value) => value is { } date
        ? date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
        : string.Empty;
}
