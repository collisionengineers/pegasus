using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Pegasus.Infrastructure.Cazana;

/// <summary>
/// Cazana Companion's own API as the Companion portal calls it (ADR-0079):
/// the guest sign-in that exchanges the account for a branch JWT, the one
/// metered lookup, and the unmetered reads and PDF service the Companion
/// Report is built from. The account, onboarding token and JWT travel only in
/// request bodies and the Authorization header, never in a URL, and nothing
/// here logs them.
/// </summary>
internal sealed class CazanaCompanionClient(HttpClient http)
{
    internal static readonly Uri BaseUri = new("https://api.cazana.com/");

    private const string Environment = "production";
    private const string Client = "cazana";

    /// <summary>How many of the ten comparable listings Companion's market overview prints.</summary>
    private const int ListingsShown = 8;

    /// <summary>The Companion portal's own client identifier for guest requests.</summary>
    private const string Czn = "5ab3cc97a8d11a00017332a7a609b760ad0147e0a12295c85fabe703";

    public async Task<string> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        string token;
        long branchId;
        using (var answer = await ExchangeAsync(
            "sign_in", Guest("guest/onboard/branch", new { credentials = new { email, password } }), cancellationToken))
        {
            token = CazanaJson.String(answer.RootElement, "token")
                ?? throw new CazanaCompanionFailure("sign_in", null, "token=absent");
            branchId = CazanaJson.Items(answer.RootElement, "branches")
                .Select(branch => (Active: Whole(CazanaJson.Get(branch, "active")), Id: Whole(CazanaJson.Get(branch, "id"))))
                .FirstOrDefault(branch => branch.Active == 1 && branch.Id is not null).Id
                ?? throw new CazanaCompanionFailure("branch", null, "branches=none_active");
        }

        using var confirmed = await ExchangeAsync(
            "confirm", Guest("guest/onboard/confirm", new { token, orgId = branchId }), cancellationToken);
        return CazanaJson.String(confirmed.RootElement, "jwt")
            ?? throw new CazanaCompanionFailure("confirm", null, "jwt=absent");
    }

    /// <summary>The one metered call: a Companion lookup of the plate on the day at the mileage.</summary>
    public async Task<string> LookupAsync(string jwt, string vrm, DateOnly date, long miles, CancellationToken cancellationToken)
    {
        var search = new
        {
            search = new
            {
                vrm,
                date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                mileage = miles,
                options = new { forceEstimate = false },
            },
        };
        using var answer = await ExchangeAsync(
            "lookup", App(HttpMethod.Post, "app/companion", jwt, search), cancellationToken);
        return CazanaJson.String(answer.RootElement, "uuid")
            ?? throw new CazanaCompanionFailure("lookup", null, "uuid=absent");
    }

    /// <summary>The lookup's <c>response</c>: valuation, unmasked VIN, specification and any listing.</summary>
    public async Task<JsonElement> ReadAsync(string jwt, string uuid, CancellationToken cancellationToken)
    {
        using var answer = await ExchangeAsync(
            "read", App(HttpMethod.Get, "app/companion/" + Uri.EscapeDataString(uuid), jwt), cancellationToken);
        if (CazanaJson.Get(answer.RootElement, "response") is { } response
            && CazanaJson.Get(response, "valuation") is { ValueKind: JsonValueKind.Object })
        {
            return response.Clone();
        }

        throw new CazanaCompanionFailure("read", null, "valuation=absent");
    }

    /// <summary>
    /// The vehicle's timeline, or none when Cazana times out or holds no
    /// history for it, as Companion's own <c>getTimeline</c> treats them.
    /// </summary>
    public async Task<JsonElement?> TimelineAsync(string jwt, string uuid, CancellationToken cancellationToken)
    {
        try
        {
            using var answer = await ExchangeAsync(
                "timeline",
                App(HttpMethod.Get, "app/companion/" + Uri.EscapeDataString(uuid) + "/timeline", jwt),
                cancellationToken);
            var response = CazanaJson.Get(answer.RootElement, "response");
            return string.IsNullOrEmpty(CazanaJson.String(response, "vin")) ? null : response!.Value.Clone();
        }
        catch (CazanaCompanionFailure failure) when (failure.Status == HttpStatusCode.RequestTimeout)
        {
            return null;
        }
    }

    /// <summary>
    /// Comparable listings exactly as Companion asks for them, ten of them,
    /// of which the report prints the first eight with the total: only those
    /// leave the answer. The listing cid is the valuation's, not the
    /// specification's: Cazana answers 500 for the latter.
    /// </summary>
    public async Task<SimilarListings> SimilarListingsAsync(string jwt, JsonElement response, CancellationToken cancellationToken)
    {
        var valuation = CazanaJson.Get(response, "valuation");
        var request = new
        {
            cid = CazanaJson.Get(valuation, "cid"),
            location = new { method = "", postcode = "", distance = 5, coords = new { lat = "", lon = "" } },
            mileage = CazanaJson.Get(valuation, "mileage", "value"),
            order = "desc",
            sort = "daysOnSale",
            filter = new
            {
                colour = Array.Empty<object>(),
                year = Array.Empty<object>(),
                dealer = Array.Empty<object>(),
                listing = Array.Empty<object>(),
                mileage = Array.Empty<object>(),
                keyword = Array.Empty<object>(),
                category = Array.Empty<object>(),
            },
            value = CazanaJson.Get(valuation, "retail"),
            vrm = CazanaJson.Get(valuation, "searchMethod", "identifier"),
            year = Year(CazanaJson.Get(response, "specs")),
            vin = CazanaJson.Get(valuation, "vin"),
            dealerIds = Array.Empty<object>(),
            page = 1,
            limit = 10,
        };
        using var answer = await ExchangeAsync(
            "listings", App(HttpMethod.Post, "app/listings/similar", jwt, request), cancellationToken);
        return new SimilarListings(
            CazanaJson.Get(answer.RootElement, "total")?.Clone(),
            [.. CazanaJson.Items(answer.RootElement, "listings").Take(ListingsShown).Select(listing => listing.Clone())]);
    }

    /// <summary>
    /// The vehicle's first stock image. Companion shows its placeholder when
    /// this fails for any reason, so no failure here stops the report.
    /// </summary>
    public async Task<string?> ImageAsync(string jwt, string vrm, CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, "cz/image/2.0?vrm=" + Uri.EscapeDataString(vrm)));
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        try
        {
            using var answer = await ExchangeAsync("image", message, cancellationToken);
            return CazanaJson.Items(answer.RootElement, "image")
                .Where(image => image.ValueKind == JsonValueKind.String)
                .Select(image => image.GetString())
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is HttpRequestException or CazanaCompanionFailure
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>
    /// Cazana's PDF service: rendered HTML in, the PDF out. The HTML travels
    /// as bytes, which the serializer writes as Base64, and the PDF is decoded
    /// from the answer's own bytes.
    /// </summary>
    public async Task<byte[]> CreatePdfAsync(string jwt, string html, CancellationToken cancellationToken)
    {
        var request = new { html = Encoding.UTF8.GetBytes(html) };
        using var answer = await ExchangeAsync(
            "pdf", App(HttpMethod.Post, "app/pdf/create", jwt, request), cancellationToken);
        return CazanaJson.Get(answer.RootElement, "data") is { ValueKind: JsonValueKind.String } data
            && data.TryGetBytesFromBase64(out var pdf)
            && pdf.Length > 0
                ? pdf
                : throw new CazanaCompanionFailure("pdf", null, "pdf=absent");
    }

    private static HttpRequestMessage Guest(string path, object body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri, path)) { Content = JsonContent.Create(body) };
        message.Headers.Add("X-ENVIRONMENT", Environment);
        message.Headers.Add("X-CLIENT", Client);
        message.Headers.Add("X-CZN", Czn);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return message;
    }

    private static HttpRequestMessage App(HttpMethod method, string path, string jwt, object? body = null)
    {
        var message = new HttpRequestMessage(method, new Uri(BaseUri, path));
        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        message.Headers.Add("X-ENVIRONMENT", Environment);
        message.Headers.Add("X-CLIENT", Client);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return message;
    }

    /// <summary>
    /// One call, named by its stage: the answer parsed from the network stream
    /// (the PDF answer is megabytes of Base64 and is never buffered whole
    /// first), or a stage-named failure. Only a refusal is read as text, for
    /// its detail. <see cref="HttpClient.Timeout"/> ends at the response
    /// headers once the body streams, so the same bound is linked over the
    /// body read.
    /// </summary>
    private async Task<JsonDocument> ExchangeAsync(
        string stage,
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        using (message)
        using (var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            bounded.CancelAfter(http.Timeout);
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, bounded.Token);
            if (!Succeeded(response.StatusCode))
            {
                throw new CazanaCompanionFailure(
                    stage,
                    response.StatusCode,
                    $"status={(int)response.StatusCode} {ErrorDetail(await response.Content.ReadAsStringAsync(bounded.Token))}");
            }

            try
            {
                await using var content = await response.Content.ReadAsStreamAsync(bounded.Token);
                return await JsonDocument.ParseAsync(content, cancellationToken: bounded.Token);
            }
            catch (JsonException)
            {
                throw new CazanaCompanionFailure(stage, null, "json=invalid");
            }
        }
    }

    private static bool Succeeded(HttpStatusCode status) => (int)status is >= 200 and <= 299;

    private static long? Whole(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } number && number.TryGetInt64(out var whole) ? whole : null;

    /// <summary>The year Companion sends: of first registration, else the specification's year.</summary>
    private static object? Year(JsonElement? specs) =>
        DateTimeOffset.TryParse(
            CazanaJson.String(specs, "firstRegistration"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var registered)
            ? registered.Year
            : CazanaJson.Get(specs, "year");

    /// <summary>
    /// Cazana's own words for a refusal: its message and any detail strings
    /// (a 403 answers a bare string). Never more than a short line.
    /// </summary>
    private static string ErrorDetail(string body)
    {
        var words = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    words.Add(error.GetString()!);
                }
                else if (error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("message", out var text) && text.ValueKind == JsonValueKind.String)
                    {
                        words.Add(text.GetString()!);
                    }

                    if (error.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        words.AddRange(data.EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Select(item => item.GetString()!));
                    }
                }
            }
        }
        catch (JsonException)
        {
            return "error=unreadable";
        }

        var detail = "error=" + string.Join("; ", words);
        return detail.Length <= 200 ? detail : detail[..200];
    }
}

/// <summary>The comparable listings the report prints: the answer's total and its first listings.</summary>
internal sealed record SimilarListings(JsonElement? Total, IReadOnlyList<JsonElement> Listings);

/// <summary>A Companion call that did not answer as Companion answers, named by its stage.</summary>
internal sealed class CazanaCompanionFailure(string stage, HttpStatusCode? status, string detail)
    : Exception($"Cazana {stage} failed: {detail}")
{
    public string Stage { get; } = stage;

    public HttpStatusCode? Status { get; } = status;

    public string Detail { get; } = detail;
}
