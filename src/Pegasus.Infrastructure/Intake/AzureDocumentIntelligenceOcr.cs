using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Azure.Core;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// Configuration for the one approved OCR boundary. The endpoint must be an
/// absolute HTTPS Azure AI Services endpoint; no key is configured, because the
/// service is reached with the host's own managed identity.
/// </summary>
public sealed record AzureDocumentIntelligenceOptions(Uri Endpoint, TimeSpan PollInterval)
{
    public const string CredentialScope = "https://cognitiveservices.azure.com/.default";

    public static AzureDocumentIntelligenceOptions Create(Uri endpoint) =>
        new(Validate(endpoint), TimeSpan.FromSeconds(2));

    private static Uri Validate(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "The Document Intelligence endpoint must be an absolute HTTPS URI.");
        }

        return endpoint;
    }
}

/// <summary>
/// Azure Document Intelligence <c>prebuilt-layout</c> over the existing
/// <see cref="HttpClient"/> and <c>Azure.Identity</c>, against REST
/// <c>api-version=2024-11-30</c>. No SDK package, no second vendor, no OCR
/// runtime of our own.
///
/// Only the qualified pages are submitted, through the documented <c>pages</c>
/// parameter, so a document whose other pages carry embedded text is neither
/// re-read nor charged for. The operation the service returns is polled at the
/// location it named, and that location is checked to belong to the configured
/// endpoint and to this model before it is followed — a redirected
/// operation-location is refused rather than trusted.
///
/// Nothing here decides what a value MEANS. Confidence is carried through as the
/// provider reported it and is never used to accept a field, and coordinates are
/// mapped in the provider's own unit rather than converted into an assumed one.
/// </summary>
public sealed class AzureDocumentIntelligenceOcr(
    AzureDocumentIntelligenceOptions options,
    HttpClient httpClient,
    TokenCredential credential,
    TimeProvider timeProvider) : IIntakeOcrProvider
{
    private const string AnalyzePath =
        "documentintelligence/documentModels/" + IntakeOcrProviderIdentity.ModelId + ":analyze";

    public async Task<IntakeOcrResult> AnalyzeAsync(
        IntakeOcrRequest request,
        Stream content,
        CancellationToken cancellationToken)
    {
        IntakeOcrRequest.Validate(request);
        ArgumentNullException.ThrowIfNull(content);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (!string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                request.SourceSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            // The bytes are not the bytes the operation names. Nothing is sent:
            // a result read from the wrong source could never be attributed.
            return Failed(
                "ocr_source_hash_mismatch",
                "The opened source does not match the hash the OCR operation records.",
                retryable: false);
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, AnalyzeUri(request));
        using var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        message.Content = body;
        await AuthorizeAsync(message, cancellationToken);

        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (response.StatusCode is HttpStatusCode.TooManyRequests
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            or HttpStatusCode.BadGateway)
        {
            return Failed(
                "ocr_provider_unavailable",
                $"The provider refused the submission with {(int)response.StatusCode}.",
                retryable: true,
                retryAfter: response.Headers.RetryAfter?.Delta);
        }

        if (!response.IsSuccessStatusCode)
        {
            return Failed(
                "ocr_submission_rejected",
                $"The provider rejected the submission with {(int)response.StatusCode}.",
                retryable: false);
        }

        if (!TryReadOperationLocation(response, out var operationLocation, out var providerOperationId))
        {
            // Accepted, but we cannot say what was accepted. The operation may
            // have a side effect, so this is Unknown rather than a failure — and
            // it is never repeated on that basis.
            return new(
                IntakeOcrState.Unknown,
                IntakeOcrProviderIdentity.Provider,
                IntakeOcrProviderIdentity.ModelId,
                IntakeOcrProviderIdentity.ApiVersion,
                Failure: new(
                    "ocr_operation_location_invalid",
                    "The provider accepted the submission but returned no usable operation location.",
                    Retryable: false));
        }

        return await PollAsync(request, operationLocation, providerOperationId, cancellationToken);
    }

    public async Task<IntakeOcrResult> ReconcileAsync(
        IntakeOcrRequest request,
        string providerOperationId,
        CancellationToken cancellationToken)
    {
        IntakeOcrRequest.Validate(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerOperationId);
        var location = new Uri(
            options.Endpoint,
            $"documentintelligence/documentModels/{IntakeOcrProviderIdentity.ModelId}/analyzeResults/"
            + $"{Uri.EscapeDataString(providerOperationId)}?api-version={IntakeOcrProviderIdentity.ApiVersion}");
        return await PollAsync(request, location, providerOperationId, cancellationToken);
    }

    /// <summary>
    /// Follows the operation until the provider answers or the caller's bounded
    /// attempt runs out. A wait that runs out leaves the operation Unknown and
    /// names the provider's identity for it, which is what makes it reconcilable
    /// instead of repeatable.
    /// </summary>
    private async Task<IntakeOcrResult> PollAsync(
        IntakeOcrRequest request,
        Uri operationLocation,
        string providerOperationId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            HttpResponseMessage response;
            using (var message = new HttpRequestMessage(HttpMethod.Get, operationLocation))
            {
                await AuthorizeAsync(message, cancellationToken);
                response = await httpClient.SendAsync(message, cancellationToken);
            }

            using (response)
            {
                if (response.StatusCode is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout
                    or HttpStatusCode.BadGateway)
                {
                    return Unknown(
                        providerOperationId,
                        "ocr_provider_unavailable",
                        $"The operation could not be read: {(int)response.StatusCode}.",
                        response.Headers.RetryAfter?.Delta);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return Unknown(
                        providerOperationId,
                        "ocr_operation_unreadable",
                        $"The operation was rejected with {(int)response.StatusCode}.",
                        retryAfter: null);
                }

                var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                var mapped = Map(request, providerOperationId, payload);
                if (mapped is not null)
                {
                    return mapped;
                }
            }

            try
            {
                await Task.Delay(options.PollInterval, timeProvider, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The bounded attempt ended while the provider was still
                // working. The operation exists and is named, so the next
                // attempt asks about it rather than sending the pages again.
                return Unknown(
                    providerOperationId,
                    "ocr_operation_pending",
                    "The attempt ended before the provider finished the operation.",
                    retryAfter: null);
            }
        }
    }

    /// <summary>
    /// One provider response as an OCR result, or null while the operation is
    /// still running. Everything the response claims about itself is checked
    /// before any of it is believed: its model, its API version and the pages it
    /// names.
    /// </summary>
    private static IntakeOcrResult? Map(
        IntakeOcrRequest request,
        string providerOperationId,
        byte[] payload)
    {
        var responseSha256 = Convert.ToHexStringLower(SHA256.HashData(payload));
        JsonElement root;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return Failed(
                "ocr_response_malformed",
                "The provider response is not readable JSON.",
                retryable: false,
                providerOperationId: providerOperationId,
                responseSha256: responseSha256);
        }

        using (document)
        {
            root = document.RootElement;
            var status = Text(root, "status");
            switch (status)
            {
                case "notStarted" or "running":
                    return null;
                case "failed":
                    return Failed(
                        "ocr_operation_failed",
                        "The provider reported the operation as failed.",
                        retryable: false,
                        providerOperationId: providerOperationId,
                        responseSha256: responseSha256);
                case "succeeded":
                    break;
                default:
                    return Failed(
                        "ocr_response_malformed",
                        $"The provider reported an unrecognized operation status '{status}'.",
                        retryable: false,
                        providerOperationId: providerOperationId,
                        responseSha256: responseSha256);
            }

            if (!root.TryGetProperty("analyzeResult", out var analyze)
                || analyze.ValueKind != JsonValueKind.Object)
            {
                return Failed(
                    "ocr_response_malformed",
                    "The provider reported success without an analysis result.",
                    retryable: false,
                    providerOperationId: providerOperationId,
                    responseSha256: responseSha256);
            }

            var modelId = Text(analyze, "modelId");
            if (!string.Equals(modelId, IntakeOcrProviderIdentity.ModelId, StringComparison.Ordinal))
            {
                return Failed(
                    "ocr_model_unexpected",
                    $"The result was produced by model '{modelId}', not {IntakeOcrProviderIdentity.ModelId}.",
                    retryable: false,
                    providerOperationId: providerOperationId,
                    responseSha256: responseSha256);
            }

            return new(
                IntakeOcrState.Completed,
                IntakeOcrProviderIdentity.Provider,
                IntakeOcrProviderIdentity.ModelId,
                // Carried through as the provider reported it. Core refuses a
                // version it did not pin, rather than mapping on the assumption
                // that the shape is unchanged.
                Text(analyze, "apiVersion") ?? string.Empty,
                providerOperationId,
                responseSha256,
                Pages(analyze, request));
        }
    }

    private static IReadOnlyList<IntakeOcrPage> Pages(JsonElement analyze, IntakeOcrRequest request)
    {
        if (!analyze.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var tablesByPage = TablesByPage(analyze);
        var results = new List<IntakeOcrPage>();
        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("pageNumber", out var number)
                || number.ValueKind != JsonValueKind.Number
                || !number.TryGetInt32(out var pageNumber))
            {
                continue;
            }

            var unit = Text(page, "unit") ?? "unknown";
            var words = page.TryGetProperty("words", out var wordArray) && wordArray.ValueKind == JsonValueKind.Array
                ? wordArray.EnumerateArray()
                    .Select(word => new IntakeOcrWord(
                        Text(word, "content") ?? string.Empty,
                        word.TryGetProperty("confidence", out var confidence)
                            && confidence.ValueKind == JsonValueKind.Number
                                ? confidence.GetDouble()
                                : null,
                        Bounds(word, unit)))
                    .ToArray()
                : [];
            var lines = page.TryGetProperty("lines", out var lineArray) && lineArray.ValueKind == JsonValueKind.Array
                ? lineArray.EnumerateArray()
                    .Select(line => new IntakeOcrLine(
                        Text(line, "content") ?? string.Empty,
                        Bounds(line, unit),
                        []))
                    .ToArray()
                : [];

            results.Add(new(
                pageNumber,
                string.Join(Environment.NewLine, lines.Select(line => line.Text)),
                lines.Length == 0 && words.Length > 0
                    ? [new(string.Join(' ', words.Select(word => word.Text)), null, words)]
                    : lines,
                tablesByPage.TryGetValue(pageNumber, out var tables) ? tables : []));
        }

        // The pages come back in the provider's order; the request's order is
        // ascending, and a stable ascending order is what makes a replay
        // byte-identical.
        return [.. results.OrderBy(page => page.Number)];
    }

    /// <summary>
    /// The layout model reports tables for the whole analysis, each cell naming
    /// the page it sits on. They are grouped back onto their pages here so a
    /// caller never has to reason about a table that spans one.
    /// </summary>
    private static Dictionary<int, IntakeOcrTable[]> TablesByPage(JsonElement analyze)
    {
        if (!analyze.TryGetProperty("tables", out var tables) || tables.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var grouped = new Dictionary<int, List<IntakeOcrTable>>();
        var ordinal = 0;
        foreach (var table in tables.EnumerateArray())
        {
            ordinal++;
            var page = table.TryGetProperty("boundingRegions", out var regions)
                && regions.ValueKind == JsonValueKind.Array
                && regions.EnumerateArray().FirstOrDefault() is { ValueKind: JsonValueKind.Object } region
                && region.TryGetProperty("pageNumber", out var pageNumber)
                && pageNumber.TryGetInt32(out var value)
                    ? value
                    : 0;
            if (page < 1)
            {
                continue;
            }

            var cells = table.TryGetProperty("cells", out var cellArray)
                && cellArray.ValueKind == JsonValueKind.Array
                    ? cellArray.EnumerateArray()
                        .Where(cell => cell.TryGetProperty("rowIndex", out _)
                            && cell.TryGetProperty("columnIndex", out _))
                        .Select(cell => new IntakeOcrCell(
                            // The provider indexes from zero; the intake locator
                            // counts rows and columns from one, as a person does.
                            cell.GetProperty("rowIndex").GetInt32() + 1,
                            cell.GetProperty("columnIndex").GetInt32() + 1,
                            Text(cell, "content") ?? string.Empty,
                            null))
                        .ToArray()
                    : [];
            if (!grouped.TryGetValue(page, out var list))
            {
                list = [];
                grouped.Add(page, list);
            }

            list.Add(new(
                ordinal,
                Int(table, "rowCount") ?? 0,
                Int(table, "columnCount") ?? 0,
                cells));
        }

        return grouped.ToDictionary(entry => entry.Key, entry => entry.Value.ToArray());
    }

    /// <summary>
    /// The provider states a shape as a flat polygon of four points in the
    /// page's own unit. The enclosing rectangle is recorded in that same unit;
    /// nothing is converted into an assumed one.
    /// </summary>
    private static IntakeOcrBounds? Bounds(JsonElement element, string unit)
    {
        if (!element.TryGetProperty("polygon", out var polygon)
            || polygon.ValueKind != JsonValueKind.Array
            || polygon.GetArrayLength() < 8)
        {
            return null;
        }

        var values = polygon.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Number)
            .Select(value => value.GetDouble())
            .ToArray();
        if (values.Length < 8)
        {
            return null;
        }

        var xs = values.Where((_, index) => index % 2 == 0).ToArray();
        var ys = values.Where((_, index) => index % 2 == 1).ToArray();
        return new(xs.Min(), ys.Min(), xs.Max(), ys.Max(), unit);
    }

    /// <summary>
    /// The submission URI: this model, this pinned API version, and only the
    /// qualified pages, written as the documented <c>pages</c> parameter.
    /// </summary>
    private Uri AnalyzeUri(IntakeOcrRequest request) =>
        new(
            options.Endpoint,
            $"{AnalyzePath}?api-version={IntakeOcrProviderIdentity.ApiVersion}"
            + $"&pages={string.Join(',', request.QualifiedPages.Order().Select(page => page.ToString(CultureInfo.InvariantCulture)))}");

    /// <summary>
    /// An operation location is followed only when it belongs to the configured
    /// endpoint and names this model's analyze results. Anything else is a
    /// redirect we did not agree to, and the identity we would record for the
    /// operation would not be the provider's.
    /// </summary>
    private bool TryReadOperationLocation(
        HttpResponseMessage response,
        out Uri location,
        out string providerOperationId)
    {
        location = options.Endpoint;
        providerOperationId = string.Empty;
        if (!response.Headers.TryGetValues("Operation-Location", out var values)
            || values.FirstOrDefault() is not { } raw
            || !Uri.TryCreate(raw, UriKind.Absolute, out var candidate)
            || candidate.Scheme != Uri.UriSchemeHttps
            || !string.Equals(candidate.Host, options.Endpoint.Host, StringComparison.OrdinalIgnoreCase)
            || candidate.Port != options.Endpoint.Port)
        {
            return false;
        }

        var marker = $"/documentModels/{IntakeOcrProviderIdentity.ModelId}/analyzeResults/";
        var index = candidate.AbsolutePath.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return false;
        }

        var id = candidate.AbsolutePath[(index + marker.Length)..].Trim('/');
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        location = candidate;
        providerOperationId = id;
        return true;
    }

    private async Task AuthorizeAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([AzureDocumentIntelligenceOptions.CredentialScope]),
            cancellationToken);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }

    private static IntakeOcrResult Failed(
        string code,
        string reason,
        bool retryable,
        TimeSpan? retryAfter = null,
        string? providerOperationId = null,
        string? responseSha256 = null) =>
        new(
            IntakeOcrState.Failed,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            providerOperationId,
            responseSha256,
            Failure: new(code, reason, retryable, retryAfter));

    private static IntakeOcrResult Unknown(
        string providerOperationId,
        string code,
        string reason,
        TimeSpan? retryAfter) =>
        new(
            IntakeOcrState.Unknown,
            IntakeOcrProviderIdentity.Provider,
            IntakeOcrProviderIdentity.ModelId,
            IntakeOcrProviderIdentity.ApiVersion,
            providerOperationId,
            Failure: new(code, reason, Retryable: true, retryAfter));

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Int(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var parsed)
            ? parsed
            : null;
}
