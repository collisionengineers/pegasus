using System.Buffers;
using System.Globalization;
using System.Net;
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

    // The five endpoints whose path is fixed, each parsed once; only the calls that carry a plate or a uuid build their address per call.
    private static readonly Uri BranchUri = new(BaseUri, "guest/onboard/branch");
    private static readonly Uri ConfirmUri = new(BaseUri, "guest/onboard/confirm");
    private static readonly Uri LookupUri = new(BaseUri, "app/companion");
    private static readonly Uri ListingsUri = new(BaseUri, "app/listings/similar");
    private static readonly Uri PdfUri = new(BaseUri, "app/pdf/create");

    /// <summary>How long one Companion call may take, from the request to the last byte of its answer.</summary>
    internal static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(30);

    private const string Environment = "production";
    private const string Client = "cazana";

    /// <summary>The constant headers are set as the strings they are: nothing parsed, validated or built per call.</summary>
    private const string JsonMediaType = "application/json";

    /// <summary>The Companion portal's own client identifier for guest requests.</summary>
    private const string Czn = "5ab3cc97a8d11a00017332a7a609b760ad0147e0a12295c85fabe703";

    /// <summary>
    /// The guest sign-in. It answers the session's Authorization header value,
    /// "Bearer" and the branch JWT, built once here: each later call adds that
    /// text as it stands, with no header object or text built per call.
    /// </summary>
    public async Task<string> SignInAsync(string email, string password, CancellationToken cancellationToken)
    {
        string token;
        long branchId;
        using (var answer = await ExchangeAsync(
            "sign_in", Guest(BranchUri, new { credentials = new { email, password } }), cancellationToken))
        {
            token = CazanaJson.String(answer.RootElement, "token")
                ?? throw new CazanaCompanionFailure("sign_in", null, "token=absent");
            branchId = ActiveBranchId(answer.RootElement)
                ?? throw new CazanaCompanionFailure("branch", null, "branches=none_active");
        }

        using var confirmed = await ExchangeAsync(
            "confirm", Guest(ConfirmUri, new { token, orgId = branchId }), cancellationToken);
        return "Bearer " + (CazanaJson.String(confirmed.RootElement, "jwt")
            ?? throw new CazanaCompanionFailure("confirm", null, "jwt=absent"));
    }

    /// <summary>The one metered call: a Companion lookup of the plate on the day at the mileage.</summary>
    public async Task<string> LookupAsync(string authorization, string vrm, DateOnly date, long miles, CancellationToken cancellationToken)
    {
        var search = new
        {
            search = new
            {
                vrm,
                date,
                mileage = miles,
                options = new { forceEstimate = false },
            },
        };
        using var answer = await ExchangeAsync(
            "lookup", App(HttpMethod.Post, LookupUri, authorization, search), cancellationToken);
        return CazanaJson.String(answer.RootElement, "uuid")
            ?? throw new CazanaCompanionFailure("lookup", null, "uuid=absent");
    }

    /// <summary>The lookup's <c>response</c>: valuation, unmasked VIN, specification and any listing.</summary>
    public async Task<JsonElement> ReadAsync(string authorization, string uuid, CancellationToken cancellationToken)
    {
        using var answer = await ExchangeAsync(
            "read",
            App(HttpMethod.Get, new Uri(BaseUri, "app/companion/" + Uri.EscapeDataString(uuid)), authorization),
            cancellationToken);
        if (CazanaJson.Get(answer.RootElement, "response") is { } response
            && CazanaJson.Get(response, "valuation") is { ValueKind: JsonValueKind.Object })
        {
            return response.Clone();
        }

        throw new CazanaCompanionFailure("read", null, "valuation=absent");
    }

    /// <summary>
    /// The vehicle's timeline answer, whose <c>response</c> is the timeline, or
    /// none when Cazana times out or holds no history for it, as Companion's
    /// own <c>getTimeline</c> treats them. The parsed answer itself is handed
    /// over, not a copy of its <c>response</c>: the caller reads it while it
    /// renders the page and disposes it straight after.
    /// </summary>
    public async Task<JsonDocument?> TimelineAsync(string authorization, string uuid, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await ExchangeAsync(
                "timeline",
                App(
                    HttpMethod.Get,
                    new Uri(BaseUri, "app/companion/" + Uri.EscapeDataString(uuid) + "/timeline"),
                    authorization),
                cancellationToken);
            if (CazanaJson.Get(answer.RootElement, "response", "vin") is { ValueKind: JsonValueKind.String } vin
                && !vin.ValueEquals(""u8))
            {
                return answer;
            }

            answer.Dispose();
            return null;
        }
        catch (CazanaCompanionFailure failure) when (failure.Status == HttpStatusCode.RequestTimeout)
        {
            return null;
        }
    }

    /// <summary>
    /// Comparable listings exactly as Companion asks for them, ten of them.
    /// The parsed answer itself is handed over, as the timeline's is: the
    /// caller reads the total and the listings it prints while it renders the
    /// page and disposes it straight after. The listing cid is the
    /// valuation's, not the specification's: Cazana answers 500 for the latter.
    /// </summary>
    public async Task<JsonDocument> SimilarListingsAsync(string authorization, JsonElement response, CancellationToken cancellationToken)
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
        return await ExchangeAsync(
            "listings", App(HttpMethod.Post, ListingsUri, authorization, request), cancellationToken);
    }

    /// <summary>
    /// The vehicle's first stock image. Companion shows its placeholder when
    /// this fails for any reason, so no failure here stops the report.
    /// </summary>
    public async Task<string?> ImageAsync(string authorization, string vrm, CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri, "cz/image/2.0?vrm=" + Uri.EscapeDataString(vrm)));
        message.Headers.TryAddWithoutValidation("Authorization", authorization);
        try
        {
            using var answer = await ExchangeAsync("image", message, cancellationToken);
            foreach (var image in CazanaJson.Items(answer.RootElement, "image"))
            {
                if (image.ValueKind == JsonValueKind.String)
                {
                    return image.GetString();
                }
            }

            return null;
        }
        catch (CazanaCompanionFailure)
        {
            return null;
        }
    }

    /// <summary>
    /// Cazana's PDF service: rendered HTML in, the PDF out. The page is
    /// encoded once, chunk by chunk, into a pooled buffer that the serializer
    /// writes as Base64 (no string and no exact-size array of the whole page),
    /// and the PDF is decoded from the answer's own bytes.
    /// </summary>
    public async Task<byte[]> CreatePdfAsync(string authorization, StringBuilder html, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(html.Length));
        try
        {
            var request = new { html = new ReadOnlyMemory<byte>(buffer, 0, Utf8(html, buffer)) };
            using var answer = await ExchangeAsync(
                "pdf", App(HttpMethod.Post, PdfUri, authorization, request), cancellationToken);
            return CazanaJson.Get(answer.RootElement, "data") is { ValueKind: JsonValueKind.String } data
                && data.TryGetBytesFromBase64(out var pdf)
                && pdf.Length > 0
                    ? pdf
                    : throw new CazanaCompanionFailure("pdf", null, "pdf=absent");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// The builder's chunks as UTF-8 through one encoder, so a surrogate pair
    /// split across two chunks still encodes as one character.
    /// </summary>
    private static int Utf8(StringBuilder text, byte[] destination)
    {
        var encoder = Encoding.UTF8.GetEncoder();
        var written = 0;
        foreach (var chunk in text.GetChunks())
        {
            written += encoder.GetBytes(chunk.Span, destination.AsSpan(written), flush: false);
        }

        return written + encoder.GetBytes(ReadOnlySpan<char>.Empty, destination.AsSpan(written), flush: true);
    }

    private static HttpRequestMessage Guest(Uri uri, object body)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body) };
        message.Headers.TryAddWithoutValidation("X-ENVIRONMENT", Environment);
        message.Headers.TryAddWithoutValidation("X-CLIENT", Client);
        message.Headers.TryAddWithoutValidation("X-CZN", Czn);
        message.Headers.TryAddWithoutValidation("Accept", JsonMediaType);
        return message;
    }

    private static HttpRequestMessage App(HttpMethod method, Uri uri, string authorization, object? body = null)
    {
        var message = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            message.Content = JsonContent.Create(body);
        }

        message.Headers.TryAddWithoutValidation("Authorization", authorization);
        message.Headers.TryAddWithoutValidation("X-ENVIRONMENT", Environment);
        message.Headers.TryAddWithoutValidation("X-CLIENT", Client);
        message.Headers.TryAddWithoutValidation("Accept", JsonMediaType);
        return message;
    }

    /// <summary>
    /// One call, named by its stage: the answer parsed from the network stream
    /// (the PDF answer is megabytes of Base64 and is never buffered whole
    /// first), or a stage-named failure. Only a refusal is read as text, for
    /// its detail. <see cref="CallTimeout"/> bounds the headers and the body
    /// together, as the one timer of the call: the named client sets no timeout
    /// of its own (<c>AddCazanaGuideValuation</c>). Whatever stops the call is
    /// that stage's <see cref="CazanaCompanionFailure"/>, so the host log can
    /// always name where it stopped: a connection that drops while an answer
    /// streams is an <see cref="IOException"/>; a request that fails or a
    /// refusal whose body drops is an <see cref="HttpRequestException"/>; the
    /// bound running out is an <see cref="OperationCanceledException"/> while
    /// the caller has not asked to stop (the caller's own cancellation passes
    /// through unchanged). The last two are the stage's transport failure and
    /// carry the exception.
    /// </summary>
    private async Task<JsonDocument> ExchangeAsync(
        string stage,
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
                if (!Succeeded(response.StatusCode))
                {
                    throw new CazanaCompanionFailure(
                        stage,
                        response.StatusCode,
                        $"status={(int)response.StatusCode} {ErrorDetail(await response.Content.ReadAsStringAsync(bounded.Token))}");
                }

                await using var content = await response.Content.ReadAsStreamAsync(bounded.Token);
                return await JsonDocument.ParseAsync(content, cancellationToken: bounded.Token);
            }
            catch (JsonException)
            {
                throw new CazanaCompanionFailure(stage, null, "json=invalid");
            }
            catch (IOException)
            {
                throw new CazanaCompanionFailure(stage, null, "body=unreadable");
            }
            catch (HttpRequestException exception)
            {
                throw new CazanaCompanionFailure(stage, exception);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new CazanaCompanionFailure(stage, exception);
            }
        }
    }

    private static bool Succeeded(HttpStatusCode status) => (int)status is >= 200 and <= 299;

    private static long? Whole(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } number && number.TryGetInt64(out var whole) ? whole : null;

    /// <summary>The id of the first branch whose <c>active</c> is 1, or none.</summary>
    private static long? ActiveBranchId(JsonElement answer)
    {
        foreach (var branch in CazanaJson.Items(answer, "branches"))
        {
            if (Whole(CazanaJson.Get(branch, "active")) == 1 && Whole(CazanaJson.Get(branch, "id")) is { } id)
            {
                return id;
            }
        }

        return null;
    }

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

/// <summary>A Companion call that did not answer as Companion answers, named by its stage.</summary>
internal sealed class CazanaCompanionFailure : Exception
{
    public CazanaCompanionFailure(string stage, HttpStatusCode? status, string detail)
        : base($"Cazana {stage} failed: {detail}")
    {
        Stage = stage;
        Status = status;
        Detail = detail;
    }

    /// <summary>
    /// A call that got no answer: the request failed or the call ran past
    /// <see cref="CazanaCompanionClient.CallTimeout"/>. The exception is kept
    /// for the host log.
    /// </summary>
    public CazanaCompanionFailure(string stage, Exception transport)
        : base($"Cazana {stage} failed: transport={transport.GetType().Name}", transport)
    {
        Stage = stage;
        Detail = "transport=" + transport.GetType().Name;
        Transport = true;
    }

    public string Stage { get; }

    public HttpStatusCode? Status { get; }

    public string Detail { get; }

    /// <summary>Whether the call got no answer at all, as against an answer that was refused or unreadable.</summary>
    public bool Transport { get; }
}
