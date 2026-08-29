using System.Globalization;
using System.Text.Json;
using Pegasus.Core.Assessment;

namespace Pegasus.Infrastructure.Assessment;

/// <summary>
/// Deterministic parser for the Pegasus-owned JSON estimate document (ENG-026,
/// route <see cref="RepairSpecificationSourceRoute.Json"/>), the import
/// format beside the Audatex PDF for estimates produced outside Pegasus.
///
/// Schema <c>pegasus-estimate/1</c> (UTF-8 JSON object):
///
/// <code>
/// {
///   "schema": "pegasus-estimate/1",
///   "sourceVersion": "string, required — the producing system's own version or reference",
///   "lines": [
///     {
///       "operation": "Replace | Repair | R&amp;I | Paint | Other",   // or "type": one of EstimateLineCodes.Types
///       "description": "string, required",
///       "partNumber": "string, optional",
///       "guideCode": "string, optional",
///       "quantity": 1,          // optional integer ≥ 1
///       "labourHours": 1.5,     // optional, steps of 0.1
///       "paintHours": 0.5,      // optional, steps of 0.1
///       "price": 120.00         // optional, two decimal places; omit for an unpriced part
///     }
///   ]
/// }
/// </code>
///
/// Values are read from the document, never derived; an unknown schema,
/// operation, type, or an unreadable number rejects the whole import with
/// <see cref="EstimateParseRejectedException"/>. Line status and evidence
/// label are not part of the document — an imported line is
/// <c>estimated</c> from a <c>reference</c> source until an Engineer
/// confirms it.
/// </summary>
public sealed class JsonEstimateParser : IEstimateDocumentParser
{
    public const string Schema = "pegasus-estimate/1";
    private const int MaximumLines = AssessmentPolicy.MaximumEstimateLines;

    public RepairSpecificationSourceRoute Route => RepairSpecificationSourceRoute.Json;

    public bool CanParse(string fileName, string mediaType) =>
        string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);

    public ParsedEstimate Parse(ReadOnlyMemory<byte> content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            throw new EstimateParseRejectedException("The file is not valid JSON, so nothing was imported.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schema", out var schema)
                || schema.ValueKind != JsonValueKind.String
                || !string.Equals(schema.GetString(), Schema, StringComparison.Ordinal))
            {
                throw new EstimateParseRejectedException(
                    $"The document does not declare the '{Schema}' schema, so nothing was imported.");
            }
            var sourceVersion = OptionalText(root, "sourceVersion", 100)
                ?? throw new EstimateParseRejectedException("The document names no sourceVersion.");
            if (!root.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array)
            {
                throw new EstimateParseRejectedException("The document carries no lines array.");
            }
            if (lines.GetArrayLength() is 0 or > MaximumLines)
            {
                throw new EstimateParseRejectedException(
                    $"The document must carry between 1 and {MaximumLines} lines.");
            }

            var parsed = new List<EstimateLineInput>(lines.GetArrayLength());
            var position = 0;
            foreach (var line in lines.EnumerateArray())
            {
                position++;
                parsed.Add(ParseLine(line, position));
            }
            return new ParsedEstimate(sourceVersion, parsed);
        }
    }

    private static EstimateLineInput ParseLine(JsonElement line, int position)
    {
        if (line.ValueKind != JsonValueKind.Object)
        {
            throw Reject(position, "is not an object");
        }
        var type = LineType(line, position);
        var description = OptionalText(line, "description", 300)
            ?? throw Reject(position, "has no description");
        var price = OptionalDecimal(line, "price", 2, position);
        return new EstimateLineInput(
            type,
            OptionalText(line, "guideCode", 50),
            description,
            OptionalDecimal(line, "labourHours", 1, position),
            price,
            Unpriced: price is null && type == "new_part",
            OptionalText(line, "partNumber", 100),
            Betterment: null,
            Status: "estimated",
            EvidenceLabel: "reference",
            Justification: null,
            PaintWorkUnits: OptionalDecimal(line, "paintHours", 1, position),
            Quantity: OptionalQuantity(line, position));
    }

    private static string LineType(JsonElement line, int position)
    {
        if (line.TryGetProperty("type", out var typeElement))
        {
            var type = typeElement.ValueKind == JsonValueKind.String ? typeElement.GetString()?.Trim() : null;
            return type is not null && EstimateLineCodes.Types.Contains(type, StringComparer.Ordinal)
                ? type
                : throw Reject(position, "carries an unrecognized line type");
        }
        if (line.TryGetProperty("operation", out var operationElement)
            && operationElement.ValueKind == JsonValueKind.String
            && EstimateOperations.TryParse(operationElement.GetString(), out var operation))
        {
            return EstimateOperations.ToLineType(operation);
        }
        throw Reject(position, "names neither a known operation nor a line type");
    }

    private static string? OptionalText(JsonElement element, string name, int maximumLength)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        return text.Length <= maximumLength
            ? text
            : throw new EstimateParseRejectedException(
                $"The value of '{name}' exceeds {maximumLength} characters, so nothing was imported.");
    }

    private static decimal? OptionalDecimal(JsonElement element, string name, int decimals, int position)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number)
            || number < 0 || decimal.Round(number, decimals) != number)
        {
            throw Reject(position, $"has an unreadable {name} (non-negative, {decimals} decimal place{(decimals == 1 ? "" : "s")})");
        }
        return number;
    }

    private static int? OptionalQuantity(JsonElement element, int position)
    {
        if (!element.TryGetProperty("quantity", out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var quantity) && quantity >= 1
            ? quantity
            : throw Reject(position, "has a quantity that is not a whole number of at least one");
    }

    private static EstimateParseRejectedException Reject(int position, string problem) => new(
        string.Create(CultureInfo.InvariantCulture, $"Line {position} {problem}, so nothing was imported."));
}
