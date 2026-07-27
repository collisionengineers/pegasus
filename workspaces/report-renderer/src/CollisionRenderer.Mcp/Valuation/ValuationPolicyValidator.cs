using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CollisionRenderer.Mcp.Valuation;

/// <summary>
/// Server-side defence-in-depth port of the high-value rules in the skill's
/// <c>validate_evidence_pack.py</c>. The skill still runs that Python validator
/// (its step 7) as the authoritative gate; this re-checks the same domain policy so a
/// payload reaching the renderer directly (e.g. via the MCP tool) cannot bypass it.
///
/// <para>Operates on the original <b>snake_case</b> payload — a 1:1 port that keeps the
/// field names and messages aligned with the Python source. The deep Pydantic schema
/// validation (<c>EvidencePackPayload.model_validate</c>) is intentionally NOT ported;
/// the structural presence/type/policy checks below cover the render-blocking cases.</para>
/// </summary>
public sealed class ValuationPolicyValidator
{
    internal static readonly string[] RequiredSubject =
    {
        "registration", "make", "model", "body_type", "fuel", "transmission", "engine",
        "first_registered", "mileage",
    };

    internal static readonly string[] RequiredAdvert =
    {
        "source", "url", "price", "make", "model", "derivative_or_engine", "registration_year",
        "mileage", "fuel", "transmission", "body_style", "seller_type", "location", "date_accessed",
        "comparability_note", "differences_note", "supports_assessed_value", "evidence_role",
        "is_materially_comparable",
    };

    private static readonly string[] OptionalWarn = { "advert_id", "screenshot_path" };
    private static readonly string[] CommercialWarn = { "vat_status", "admin_fee", "delivery_fee" };
    private static readonly string[] RequiredNarrative = { "market_research", "conclusion" };

    private static readonly HashSet<string> ValidEvidenceRoles =
        new(StringComparer.Ordinal) { "supportive", "limiting", "contextual", "excluded" };

    private static readonly HashSet<string> ValidValuationModes =
        new(StringComparer.Ordinal) { "guide_supported", "market_only" };

    private static readonly (string Label, Regex Pattern)[] ForbiddenExternalPatterns =
    {
        ("EVA", new Regex(@"\bEVA\b", RegexOptions.IgnoreCase)),
        ("uplift", new Regex(@"\buplift(?:s|ed|ing)?\b", RegexOptions.IgnoreCase)),
        ("guide value", new Regex(@"\bguide\s+value\b", RegexOptions.IgnoreCase)),
        ("guide valuation", new Regex(@"\bguide\s+valuation\b", RegexOptions.IgnoreCase)),
        ("guide price", new Regex(@"\bguide\s+price\b", RegexOptions.IgnoreCase)),
        ("Engineer Value", new Regex(@"\bEngineer\s+Value\b", RegexOptions.IgnoreCase)),
        ("Original Eng Value", new Regex(@"\bOriginal\s+Eng(?:ineer)?\s+Value\b", RegexOptions.IgnoreCase)),
    };

    private static readonly HashSet<string> MissingStrings = new(StringComparer.Ordinal)
    {
        "n/a", "na", "none", "not applicable", "not provided", "not supplied", "not stated",
        "not visible", "not known", "tbc", "to be confirmed", "unknown",
    };

    private static readonly string[] MissingPrefixes =
    {
        "not applicable", "not provided", "not supplied", "not stated", "not visible",
        "not known", "to be confirmed",
    };

    private static readonly HashSet<string> CommercialBodyTypes =
        new(StringComparer.Ordinal) { "van", "pickup", "commercial" };

    public (List<string> Errors, List<string> Warnings) Validate(JsonElement payload)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (payload.ValueKind != JsonValueKind.Object)
        {
            errors.Add("payload must be a JSON object");
            return (errors, warnings);
        }

        var subject = GetObject(payload, "subject_vehicle");
        if (subject is null)
        {
            errors.Add("subject_vehicle missing or not an object");
        }
        else
        {
            var missingSubject = Missing(subject.Value, RequiredSubject);
            if (missingSubject.Count > 0)
            {
                errors.Add("subject_vehicle missing or placeholder: " + string.Join(", ", missingSubject));
            }

            if (subject.Value.TryGetProperty("vehicle_description", out var desc) && IsMissing(desc))
            {
                errors.Add("subject_vehicle.vehicle_description is placeholder: remove it or provide the exact visible Vehicle text");
            }
        }

        var meta = GetObject(payload, "meta");
        if (meta is null)
        {
            errors.Add("meta missing or not an object");
        }
        else
        {
            if (IsMissing(Prop(meta.Value, "your_ref")))
            {
                errors.Add("meta.your_ref missing or placeholder: ask for the claim or matter reference before rendering");
            }

            if (meta.Value.TryGetProperty("our_ref", out var ourRef) && IsMissing(ourRef))
            {
                errors.Add("meta.our_ref is placeholder: omit it (the registration is the fallback) or supply the internal reference");
            }
        }

        errors.AddRange(ValidateValuationMode(payload));

        foreach (var field in new[] { "assessed_retail_value", "adverts" })
        {
            if (IsMissing(Prop(payload, field)))
            {
                errors.Add($"{field} missing or placeholder");
            }
        }

        foreach (var field in RequiredNarrative)
        {
            if (IsMissing(Prop(payload, field)))
            {
                errors.Add($"{field} missing or placeholder");
            }
        }

        var commentary = Prop(payload, "valuation_commentary");
        if (commentary is not { ValueKind: JsonValueKind.Array } commentaryArr || commentaryArr.GetArrayLength() == 0)
        {
            errors.Add("valuation_commentary must be a non-empty list");
        }
        else
        {
            var placeholders = new List<string>();
            var idx = 1;
            foreach (var paragraph in commentaryArr.EnumerateArray())
            {
                if (IsMissing(paragraph))
                {
                    placeholders.Add(idx.ToString(CultureInfo.InvariantCulture));
                }

                idx++;
            }

            if (placeholders.Count > 0)
            {
                errors.Add("valuation_commentary contains placeholder/empty paragraphs: " + string.Join(", ", placeholders));
            }
        }

        var adverts = Prop(payload, "adverts");
        if (adverts is not { ValueKind: JsonValueKind.Array } advertsArr || advertsArr.GetArrayLength() == 0)
        {
            errors.Add("adverts missing or not a non-empty list");
            return (errors, warnings);
        }

        errors.AddRange(ValidateEvidenceAssessment(payload, advertsArr));

        var isCommercial = Truthy(Prop(payload, "is_commercial_vehicle"))
            || (subject is not null
                && CommercialBodyTypes.Contains((Prop(subject.Value, "body_type")?.GetString() ?? string.Empty).ToLowerInvariant()));

        var advertIndex = 1;
        foreach (var advert in advertsArr.EnumerateArray())
        {
            var advertPath = $"adverts[{advertIndex - 1}]";
            advertIndex++;
            if (advert.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{advertPath} must be an object");
                continue;
            }

            var missingAdvert = Missing(advert, RequiredAdvert);
            if (missingAdvert.Count > 0)
            {
                errors.Add($"{advertPath} missing or placeholder: " + string.Join(", ", missingAdvert));
            }

            if (advert.TryGetProperty("supports_uplift", out _))
            {
                errors.Add($"{advertPath}.supports_uplift is deprecated; use supports_assessed_value and evidence_role");
            }

            if (!IsBool(advert, "supports_assessed_value"))
            {
                errors.Add($"{advertPath}.supports_assessed_value must be boolean");
            }

            if (!IsBool(advert, "is_materially_comparable"))
            {
                errors.Add($"{advertPath}.is_materially_comparable must be boolean");
            }

            var role = Prop(advert, "evidence_role")?.GetString();
            if (role is null || !ValidEvidenceRoles.Contains(role))
            {
                errors.Add($"{advertPath}.evidence_role must be one of: " + string.Join(", ", ValidEvidenceRoles.OrderBy(x => x, StringComparer.Ordinal)));
            }

            var missingOptional = Missing(advert, OptionalWarn);
            if (missingOptional.Count > 0)
            {
                warnings.Add($"{advertPath} optional fields missing: " + string.Join(", ", missingOptional));
            }

            if (isCommercial)
            {
                var missingCommercial = Missing(advert, CommercialWarn);
                if (missingCommercial.Count > 0)
                {
                    warnings.Add($"{advertPath} commercial fields missing: " + string.Join(", ", missingCommercial));
                }
            }
        }

        errors.AddRange(ExternalWordingErrors(payload));
        return (errors, warnings);
    }

    private static List<string> ValidateValuationMode(JsonElement payload)
    {
        var errors = new List<string>();
        var mode = Prop(payload, "valuation_mode")?.GetString() ?? "guide_supported";
        if (!ValidValuationModes.Contains(mode))
        {
            errors.Add("valuation_mode must be one of: " + string.Join(", ", ValidValuationModes.OrderBy(x => x, StringComparer.Ordinal)));
            return errors;
        }

        if (mode == "guide_supported")
        {
            if (IsMissing(Prop(payload, "guide_value")))
            {
                errors.Add("guide_value missing or placeholder for guide_supported valuation_mode");
            }
            else
            {
                var guide = ParseMoney(Prop(payload, "guide_value"));
                var assessed = ParseMoney(Prop(payload, "assessed_retail_value"));
                if (guide is not null && assessed is not null && assessed < guide)
                {
                    errors.Add(
                        $"guide_supported mode: assessed_retail_value (£{assessed.Value:N2}) is below " +
                        $"guide_value (£{guide.Value:N2}). Broaden the search per " +
                        "references/marketplace-search.md, or switch to market_only with a documented " +
                        "guide_value_unavailable_reason before rendering.");
                }
            }
        }
        else
        {
            if (IsMissing(Prop(payload, "guide_value_unavailable_reason")))
            {
                errors.Add("guide_value_unavailable_reason missing or placeholder for market_only valuation_mode");
            }
        }

        return errors;
    }

    private static List<string> ValidateEvidenceAssessment(JsonElement payload, JsonElement adverts)
    {
        var errors = new List<string>();
        var assessment = GetObject(payload, "evidence_assessment");
        if (assessment is null)
        {
            errors.Add("evidence_assessment missing or not an object");
            return errors;
        }

        if (!(assessment.Value.TryGetProperty("sufficient_for_pdf", out var sufficient) && sufficient.ValueKind == JsonValueKind.True))
        {
            errors.Add("evidence_assessment.sufficient_for_pdf must be true before rendering PDFs");
        }

        if (IsMissing(Prop(assessment.Value, "basis")))
        {
            errors.Add("evidence_assessment.basis missing or placeholder");
        }

        var suitable = 0;
        var supportiveComparable = 0;
        foreach (var advert in adverts.EnumerateArray())
        {
            if (advert.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var role = Prop(advert, "evidence_role")?.GetString();
            if (string.Equals(role, "excluded", StringComparison.Ordinal))
            {
                continue;
            }

            suitable++;
            if (BoolValue(advert, "supports_assessed_value")
                && BoolValue(advert, "is_materially_comparable")
                && string.Equals(role, "supportive", StringComparison.Ordinal))
            {
                supportiveComparable++;
            }
        }

        if (suitable < 3)
        {
            errors.Add("adverts requires at least three suitable live adverts before rendering PDFs");
        }

        if (supportiveComparable < 2)
        {
            errors.Add("adverts requires at least two materially comparable adverts supporting the assessed retail value before rendering PDFs");
        }

        return errors;
    }

    private static List<string> ExternalWordingErrors(JsonElement payload)
    {
        var errors = new List<string>();
        foreach (var (path, value) in PdfBoundTextFields(payload))
        {
            foreach (var (label, pattern) in ForbiddenExternalPatterns)
            {
                if (pattern.IsMatch(value))
                {
                    errors.Add($"External PDF wording contains forbidden term '{label}' in {path}");
                }
            }
        }

        return errors;
    }

    private static IEnumerable<(string Path, string Value)> PdfBoundTextFields(JsonElement payload)
    {
        var fields = new List<(string, string)>();

        var subject = GetObject(payload, "subject_vehicle");
        if (subject is not null)
        {
            foreach (var key in new[]
                     {
                         "registration", "vehicle_description", "make", "model", "derivative",
                         "body_type", "fuel", "transmission", "engine", "first_registered",
                         "mileage", "colour", "vehicle_history", "vin",
                     })
            {
                AppendText(fields, $"subject_vehicle.{key}", Prop(subject.Value, key));
            }
        }

        foreach (var key in new[] { "intro", "market_research", "conclusion", "vat_note", "search_summary" })
        {
            AppendText(fields, key, Prop(payload, key));
        }

        if (Prop(payload, "valuation_commentary") is { ValueKind: JsonValueKind.Array } commentary)
        {
            var idx = 1;
            foreach (var paragraph in commentary.EnumerateArray())
            {
                AppendText(fields, $"valuation_commentary[{idx}]", paragraph);
                idx++;
            }
        }

        if (Prop(payload, "adverts") is { ValueKind: JsonValueKind.Array } adverts)
        {
            var idx = 1;
            foreach (var advert in adverts.EnumerateArray())
            {
                if (advert.ValueKind == JsonValueKind.Object)
                {
                    foreach (var key in new[]
                             {
                                 "source", "price", "make", "model", "derivative_or_engine",
                                 "registration_year", "mileage", "fuel", "transmission", "body_style",
                                 "seller_type", "location", "report_comment",
                             })
                    {
                        AppendText(fields, $"adverts[{idx}].{key}", Prop(advert, key));
                    }
                }

                idx++;
            }
        }

        return fields;
    }

    private static void AppendText(List<(string, string)> fields, string path, JsonElement? value)
    {
        if (IsMissing(value))
        {
            return;
        }

        fields.Add((path, RawString(value!.Value)));
    }

    // --- helpers -----------------------------------------------------------

    private static JsonElement? GetObject(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Object ? v : null;

    private static JsonElement? Prop(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var v) ? v : null;

    private static List<string> Missing(JsonElement mapping, IEnumerable<string> fields)
    {
        var missing = new List<string>();
        foreach (var field in fields)
        {
            if (IsMissing(Prop(mapping, field)))
            {
                missing.Add(field);
            }
        }

        return missing;
    }

    /// <summary>Port of Python <c>_is_missing</c>: None/""/placeholder strings are missing; numbers/bools/objects are not.</summary>
    private static bool IsMissing(JsonElement? value)
    {
        if (value is null)
        {
            return true;
        }

        switch (value.Value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;
            case JsonValueKind.String:
                var cleaned = CollapseWhitespace(value.Value.GetString() ?? string.Empty).ToLowerInvariant();
                if (cleaned.Length == 0)
                {
                    return true;
                }

                return MissingStrings.Contains(cleaned)
                    || MissingPrefixes.Any(token => cleaned.StartsWith(token + " ", StringComparison.Ordinal));
            default:
                return false;
        }
    }

    private static bool IsBool(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False);

    private static bool BoolValue(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    private static bool Truthy(JsonElement? value)
    {
        if (value is null)
        {
            return false;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => value.Value.GetString()?.Length > 0,
            JsonValueKind.Number => value.Value.TryGetDouble(out var d) && d != 0,
            JsonValueKind.Array => value.Value.GetArrayLength() > 0,
            JsonValueKind.Object => value.Value.EnumerateObject().Any(),
            _ => false,
        };
    }

    /// <summary>Port of <c>_parse_money</c>: strip £/GBP/commas and parse, or null.</summary>
    private static double? ParseMoney(JsonElement? value)
    {
        if (value is null)
        {
            return null;
        }

        switch (value.Value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.Value.GetDouble();
            case JsonValueKind.String:
                var raw = value.Value.GetString();
                if (string.IsNullOrEmpty(raw))
                {
                    return null;
                }

                var cleaned = Regex.Replace(raw, "(?i)\\bgbp\\s*", string.Empty)
                    .Replace("£", string.Empty)
                    .Replace(",", string.Empty)
                    .Trim();
                return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            default:
                return null;
        }
    }

    private static string RawString(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
