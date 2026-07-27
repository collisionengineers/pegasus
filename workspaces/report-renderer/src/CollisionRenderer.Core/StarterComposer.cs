using System.Text.Json.Nodes;

namespace CollisionRenderer.Core;

/// <summary>
/// Turns a blank draft into a "starter" draft: every form-exposed text field is filled
/// with a guillemet-wrapped prompt (and example lorem-ipsum for prose), so the user sees
/// the shape and overwrites it. Only fields the form can edit are touched, so the user
/// can always clear a placeholder from the form — and <see cref="PlaceholderScanner"/>
/// can later warn if any remain at render time.
/// </summary>
public static class StarterComposer
{
    private static readonly string O = PlaceholderScanner.Open.ToString();
    private static readonly string C = PlaceholderScanner.Close.ToString();

    public static string Wash(string blankJson, DocumentFormDefinition form)
    {
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(blankJson);
        }
        catch
        {
            return blankJson;
        }

        if (root is null)
        {
            return blankJson;
        }

        foreach (var section in form.Sections)
        {
            foreach (var field in section.Fields)
            {
                Fill(root, field, field.Path);
            }
        }

        return root.ToJsonString(CrJson.Relaxed);
    }

    private static void Fill(JsonNode root, DocumentFormField field, string path)
    {
        switch (field.Kind)
        {
            case FormFieldKind.Text:
            case FormFieldKind.Date:
            case FormFieldKind.MultilineText:
                SetIfEmptyString(root, path, Token(field));
                break;

            case FormFieldKind.Repeater:
                FillRepeater(root, field, path);
                break;

            case FormFieldKind.Table:
                FillTable(root, path);
                break;

            case FormFieldKind.QuestionAnswer:
                FillQuestionAnswer(root, path);
                break;

            // Money / Number / Checkbox / Select / SignatureSelect / image + pdf upload
            // are left at their blank defaults — a guillemet string would break the typed
            // model (decimals/bools) or has no sensible textual prompt.
        }
    }

    private static void FillRepeater(JsonNode root, DocumentFormField field, string path)
    {
        if (JsonPath.Navigate(root, path) is not JsonArray { Count: > 0 })
        {
            return;
        }

        // Only seed the first (default) row so the starter shows one filled example.
        if (field.Fields.Count == 1 && field.Fields[0].Path == "$")
        {
            SetIfEmptyString(root, $"{path}[0]", Prose(field.Label));
            return;
        }

        foreach (var child in field.Fields)
        {
            Fill(root, child, JsonPath.Combine($"{path}[0]", child.Path));
        }
    }

    private static void FillTable(JsonNode root, string path)
    {
        if (JsonPath.Navigate(root, path) is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            var label = (row["label"] as JsonValue)?.GetValue<string>();
            if (IsEmptyString(row["value"]))
            {
                row["value"] = string.IsNullOrWhiteSpace(label) ? Wrap("Value") : Wrap(label!);
            }
        }
    }

    private static void FillQuestionAnswer(JsonNode root, string path)
    {
        if (JsonPath.Navigate(root, path) is not JsonArray rows)
        {
            return;
        }

        foreach (var pair in rows.OfType<JsonArray>())
        {
            if (pair.Count >= 1 && IsEmptyString(pair[0]))
            {
                pair[0] = Prose("Question");
            }

            if (pair.Count >= 2 && IsEmptyString(pair[1]))
            {
                pair[1] = Prose("Response");
            }
        }
    }

    // ----------------------------------------------------------------- tokens

    private static string Token(DocumentFormField field) => field.Kind switch
    {
        FormFieldKind.Date => Wrap("DD/MM/YYYY"),
        FormFieldKind.MultilineText => Prose(field.Label),
        _ => Wrap(field.Label),
    };

    private static string Wrap(string label) => O + label + C;

    private static string Prose(string label) =>
        O + label + " - replace this example. Lorem ipsum dolor sit amet, consectetur adipiscing elit." + C;

    // ----------------------------------------------------------------- json paths

    private static void SetIfEmptyString(JsonNode root, string path, string value)
    {
        // Fill only blanks: a field that is missing/null, or an explicit empty string.
        // Real defaults (e.g. "Dear Sirs,", "guide_supported") are left untouched.
        var existing = JsonPath.Navigate(root, path);
        if (existing is not null && !IsEmptyString(existing))
        {
            return;
        }

        JsonPath.Set(root, path, value);
    }

    private static bool IsEmptyString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && string.IsNullOrEmpty(s);
}
