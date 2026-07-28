using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CollisionRenderer.Mcp.Valuation;

/// <summary>
/// Maps the skill's <b>snake_case</b> valuation payload onto the renderer's
/// <b>camelCase</b> document JSON, plus the one structural rename the .NET models
/// make: top-level <c>subject_vehicle</c> → <c>subject</c>.
///
/// <para>Why a deterministic key transform rather than a hand-maintained field list:
/// the .NET document models (<c>MarketValuationEvidenceDocument</c>,
/// <c>AdvertEvidencePackDocument</c>, <c>Advert</c>, <c>SubjectVehicle</c>) are a
/// faithful camelCase port of the snake_case payload, so a recursive
/// snake→camel key rename reproduces every property name exactly. Crucially it
/// <i>carries every input key over</i> — it cannot silently drop a field, which is the
/// exact failure mode a hand-written map invites (forget a field → the renderer
/// produces a plausible-but-wrong PDF). The single intentional rename
/// (<c>subject_vehicle</c> → <c>subject</c>) is applied explicitly below, and the
/// round-trip unit test asserts a fully-populated fixture lands on every model
/// property.</para>
///
/// <para><c>System.Text.Json</c> ignores unknown properties, so passing the whole
/// transformed object to each document model is safe — the model binds the fields it
/// knows and skips the rest (e.g. <c>valuationMode</c>, <c>searchSummary</c>).</para>
/// </summary>
public static class ValuationPayloadMapper
{
    /// <summary>Build the camelCase JSON for the <c>market-valuation-evidence</c> template (pure mapping).</summary>
    public static string ToReportJson(JsonElement payload) => ToReportDocument(payload).ToJsonString();

    /// <summary>Build the camelCase document object for the <c>market-valuation-evidence</c> template (pure mapping).</summary>
    public static JsonObject ToReportDocument(JsonElement payload) => CamelizeObjectWithSubjectRename(payload);

    /// <summary>Build the camelCase JSON for the <c>advert-evidence-pack</c> template (pure mapping + captures).</summary>
    public static string ToEvidencePackJson(JsonElement payload, IReadOnlyList<Capture> captures, out List<string> errors) =>
        ToEvidencePackDocument(payload, captures, out errors).ToJsonString();

    /// <summary>
    /// Build the camelCase document object for the <c>advert-evidence-pack</c> template,
    /// resolving captured advert PDFs onto <c>capturedPdfPath</c> (as <c>data:</c> URIs) so the
    /// renderer appends them after the evidence table.
    /// </summary>
    /// <param name="errors">
    /// Populated with the same capture-completeness errors the Python engine raised:
    /// every non-excluded advert needs a captured PDF, and at least one must be present.
    /// </param>
    public static JsonObject ToEvidencePackDocument(
        JsonElement payload,
        IReadOnlyList<Capture> captures,
        out List<string> errors)
    {
        errors = new List<string>();
        var doc = CamelizeObjectWithSubjectRename(payload);

        // Resolve file-handoff captures ({evidence_path, sha256} from the connector's file
        // delivery mode) into inline bytes up front, so the matching below only ever deals
        // in PdfBase64. Resolution failures become actionable per-capture errors.
        captures = ResolveEvidencePaths(captures, errors);

        // Match captures to adverts on a NORMALISED url, not the raw string. The capture
        // url (echoed by the connector's capture_advert_pages) and adverts[i].url (built by
        // the skill) come from the same source but can drift cosmetically — a trailing
        // slash, a #fragment, host casing, an explicit :443. Exact-ordinal matching would
        // then drop a supplied PDF and fail the render as "missing captured advert PDFs".
        var byUrl = new Dictionary<string, Capture>(StringComparer.Ordinal);
        foreach (var capture in captures)
        {
            if (!string.IsNullOrEmpty(capture.Url))
            {
                byUrl[NormalizeUrl(capture.Url)] = capture;
            }
        }

        var missingCaptureUrls = new List<string>();
        var appended = 0;

        if (doc["adverts"] is JsonArray adverts)
        {
            for (var i = 0; i < adverts.Count; i++)
            {
                if (adverts[i] is not JsonObject advert)
                {
                    continue;
                }

                var url = advert["url"]?.GetValue<string>();
                var role = advert["evidenceRole"]?.GetValue<string>();
                var excluded = string.Equals(role, "excluded", StringComparison.Ordinal);

                // Honour an inline capturedPdfPath if the payload already carried one.
                var hasCaptured = advert["capturedPdfPath"] is { } existing
                    && !string.IsNullOrWhiteSpace(existing.GetValue<string>());

                if (!hasCaptured && url is not null && byUrl.TryGetValue(NormalizeUrl(url), out var capture)
                    && capture.IsSuccess && !string.IsNullOrEmpty(capture.PdfBase64))
                {
                    advert["capturedPdfPath"] = "data:application/pdf;base64," + capture.PdfBase64;
                    hasCaptured = true;
                }

                if (hasCaptured)
                {
                    appended++;
                }
                else if (!excluded)
                {
                    missingCaptureUrls.Add(url ?? $"advert {i + 1}");
                }
            }
        }

        // Mirror render_evidence_pack_pdf: a complete evidence pack needs a captured PDF
        // behind every non-excluded advert, and at least one overall.
        if (missingCaptureUrls.Count > 0)
        {
            errors.Add("missing captured advert PDFs for: " + string.Join(", ", missingCaptureUrls));
        }

        if (appended == 0)
        {
            errors.Add("no captured advert PDFs were supplied for the evidence pack");
        }

        return doc;
    }

    /// <summary>
    /// Swap each successful <c>evidence_path</c>-only capture for an inline-bytes copy read
    /// via <see cref="EvidencePathResolver"/> (allowlisted root + mandatory sha256). Inline
    /// <c>pdf_base64</c> always wins when both are present. Failures are reported once per
    /// capture and the capture is left byte-less, so the advert it backs surfaces in the
    /// standard "missing captured advert PDFs" error alongside the specific cause.
    /// </summary>
    private static IReadOnlyList<Capture> ResolveEvidencePaths(IReadOnlyList<Capture> captures, List<string> errors)
    {
        if (!captures.Any(c => string.IsNullOrEmpty(c.PdfBase64) && !string.IsNullOrEmpty(c.EvidencePath)))
        {
            return captures;
        }

        var resolved = new List<Capture>(captures.Count);
        foreach (var capture in captures)
        {
            if (!capture.IsSuccess
                || !string.IsNullOrEmpty(capture.PdfBase64)
                || string.IsNullOrEmpty(capture.EvidencePath))
            {
                resolved.Add(capture);
                continue;
            }

            if (EvidencePathResolver.TryResolve(capture.EvidencePath, capture.Sha256, out var base64, out var error))
            {
                resolved.Add(capture with { PdfBase64 = base64 });
            }
            else
            {
                errors.Add($"capture for {capture.Url ?? capture.Filename ?? "(unknown url)"}: {error}");
                resolved.Add(capture);
            }
        }

        return resolved;
    }

    /// <summary>
    /// Canonicalise a URL for capture↔advert matching: lower-case scheme + host, drop a
    /// default port, trim a trailing slash, and drop the #fragment (query is preserved, as
    /// it can distinguish adverts). Cosmetic-only; never collapses genuinely distinct URLs.
    /// </summary>
    internal static string NormalizeUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var s = raw.Trim();
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
        {
            return s.TrimEnd('/');
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port;
        var path = uri.AbsolutePath;
        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
        }

        return $"{scheme}://{host}{port}{path}{uri.Query}";
    }

    /// <summary>Transform a snake_case payload object to camelCase, then rename <c>subjectVehicle</c> → <c>subject</c>.</summary>
    private static JsonObject CamelizeObjectWithSubjectRename(JsonElement payload)
    {
        if (Camelize(payload) is not JsonObject doc)
        {
            throw new ArgumentException("valuation payload must be a JSON object", nameof(payload));
        }

        // After camelisation `subject_vehicle` becomes `subjectVehicle`; the .NET models call it `subject`.
        if (doc.Remove("subjectVehicle", out var subject))
        {
            doc["subject"] = subject;
        }

        // The contract (valuation/v1) and the skill emit `meta.report_date`, which camelises to
        // `reportDate`; DocumentMeta exposes the field as `date`. Without this alias the report date
        // binds to nothing and silently defaults to today. Alias it, without clobbering an explicit `date`.
        if (doc["meta"] is JsonObject meta && meta.Remove("reportDate", out var reportDate) && reportDate is not null)
        {
            meta["date"] ??= reportDate;
        }

        return doc;
    }

    /// <summary>Recursively rename every object key snake_case → camelCase; values are deep-cloned, never dropped.</summary>
    private static JsonNode? Camelize(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var prop in element.EnumerateObject())
                {
                    obj[ToCamelCase(prop.Name)] = Camelize(prop.Value);
                }

                return obj;

            case JsonValueKind.Array:
                var arr = new JsonArray();
                foreach (var item in element.EnumerateArray())
                {
                    arr.Add(Camelize(item));
                }

                return arr;

            default:
                // Leaf (string/number/bool/null): preserve the value verbatim.
                return JsonNode.Parse(element.GetRawText());
        }
    }

    public static string ToCamelCase(string key)
    {
        if (key.IndexOf('_') < 0)
        {
            return key;
        }

        var sb = new StringBuilder(key.Length);
        var upcomingUpper = false;
        var emitted = false;
        foreach (var ch in key)
        {
            if (ch == '_')
            {
                // Leading underscores are preserved only until the first real char; thereafter
                // an underscore just marks the next letter for upper-casing.
                upcomingUpper = emitted;
                continue;
            }

            if (upcomingUpper)
            {
                sb.Append(char.ToUpperInvariant(ch));
                upcomingUpper = false;
            }
            else
            {
                sb.Append(ch);
            }

            emitted = true;
        }

        return sb.ToString();
    }
}

/// <summary>
/// A normalised advert capture from the render request. The connector's file delivery mode
/// sends <c>{evidence_path, sha256}</c> (a PDF in the shared evidence directory — resolved via
/// <see cref="EvidencePathResolver"/>); its inline mode sends <c>pdf_base64</c>. Inline bytes
/// win when both are present. A legacy <c>artifact_id</c>-only capture is treated as missing
/// (and surfaced as such) — there is no remote artifact store locally.
/// </summary>
public sealed record Capture(
    string? Url,
    string? Status,
    string? PdfBase64,
    string? ArtifactId,
    string? Filename,
    string? EvidencePath = null,
    string? Sha256 = null)
{
    public bool IsSuccess => Status is null || string.Equals(Status, "success", StringComparison.Ordinal);

    public static IReadOnlyList<Capture> Parse(JsonElement capturesElement)
    {
        var list = new List<Capture>();
        if (capturesElement.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in capturesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            list.Add(new Capture(
                Url: Str(item, "url"),
                Status: Str(item, "status"),
                PdfBase64: Str(item, "pdf_base64"),
                ArtifactId: Str(item, "artifact_id"),
                Filename: Str(item, "filename"),
                EvidencePath: Str(item, "evidence_path"),
                Sha256: Str(item, "sha256")));
        }

        return list;
    }

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
