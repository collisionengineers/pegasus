namespace CollisionRenderer.Core;

/// <summary>Result of scanning a draft for un-overwritten starter placeholders.</summary>
public sealed record PlaceholderScan
{
    public int Count { get; init; }

    /// <summary>Up to a handful of distinct placeholder tokens, e.g. the make prompt.</summary>
    public IReadOnlyList<string> Samples { get; init; } = Array.Empty<string>();

    public bool Any => Count > 0;
}

/// <summary>
/// Detects starter-content placeholders that the user has not yet replaced. Starter
/// payloads wrap every prompt in guillemets (U+2039 / U+203A) — characters that never
/// occur in genuine document content, so a simple substring scan is reliable and
/// survives draft save/restore round-trips (unlike diffing against the starter).
/// </summary>
public static class PlaceholderScanner
{
    // Built from numeric code points so the source file stays ASCII and is read
    // identically regardless of the compiler's source-file encoding.
    public const char Open = (char)0x2039;  // single left-pointing angle quotation mark
    public const char Close = (char)0x203A; // single right-pointing angle quotation mark

    public static PlaceholderScan Scan(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return new PlaceholderScan();
        }

        // A JSON serializer may write the guillemets either literally or as \uXXXX
        // escapes; normalise to the literal characters so detection is encoder-agnostic.
        // Check both escapes — a hand-edited draft can carry one without the other.
        if (json.Contains("\\u2039", StringComparison.OrdinalIgnoreCase) ||
            json.Contains("\\u203a", StringComparison.OrdinalIgnoreCase))
        {
            json = json
                .Replace("\\u2039", Open.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("\\u203a", Close.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        if (json.IndexOf(Open) < 0)
        {
            return new PlaceholderScan();
        }

        var count = 0;
        var samples = new List<string>();
        var i = 0;

        while (i < json.Length)
        {
            var start = json.IndexOf(Open, i);
            if (start < 0)
            {
                break;
            }

            var end = json.IndexOf(Close, start + 1);
            if (end < 0)
            {
                break;
            }

            count++;
            if (samples.Count < 5)
            {
                var token = json.Substring(start, end - start + 1);
                if (!samples.Contains(token))
                {
                    samples.Add(token);
                }
            }

            i = end + 1;
        }

        return new PlaceholderScan { Count = count, Samples = samples };
    }

    public static bool ContainsPlaceholders(string? json) => Scan(json).Any;
}
