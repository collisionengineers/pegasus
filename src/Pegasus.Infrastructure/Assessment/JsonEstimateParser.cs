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
///   "provider": "string, optional — names the producing system",
///   "totals": {                // optional; the document's own printed totals
///     "parts": 299.80, "panelWorkUnits": 4.7, "paintWorkUnits": 3.2,
///     "materials": 75.60, "specialist": 245.00,
///     "net": 952.28, "vat": 190.46, "gross": 1142.74
///   },
///   "lines": [
///     {
///       "operation": "Replace | Repair | R&amp;I | Paint | Blend | Specialist | Other",
///                               // or "type": one of EstimateLineCodes.Types
///       "description": "string, required",
///       "partNumber": "string, optional",
///       "guideCode": "string, optional",
///       "rowId": "string, optional — the row's identity in the source system",
///       "quantity": 1,          // optional integer ≥ 1
///       "labourHours": 1.5,     // optional, the provider's own time precision
///       "paintHours": 0.5,      // optional, the provider's own time precision
///       "price": 120.00,        // optional, two decimal places; omit for an unpriced part
///       "materials": 45.60      // optional, two decimal places
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
/// confirms it. <c>totals</c> is reconciliation evidence only: Pegasus costs
/// the estimate from its own rows and retains a disagreeing document total
/// beside the calculation rather than adopting it.
/// </summary>
public sealed class JsonEstimateParser : IEstimateDocumentParser
{
    public const string Schema = "pegasus-estimate/1";

    /// <summary>Titles the Draft an import of this document lands as.</summary>
    public const string DefaultProviderName = "Estimate";

    private const int MaximumLines = AssessmentPolicy.MaximumEstimateLines;
    private const int MoneyDecimals = 2;

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
            return new ParsedEstimate(
                sourceVersion,
                parsed,
                OptionalText(root, "provider", 100) ?? DefaultProviderName,
                ParseTotals(root));
        }
    }

    /// <summary>
    /// The document's own printed totals, when it states them. They are
    /// evidence beside the calculation and never replace it, so an absent
    /// or partial block is normal rather than a rejection.
    /// </summary>
    private static EstimateSourceTotals? ParseTotals(JsonElement root)
    {
        if (!root.TryGetProperty("totals", out var totals) || totals.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return new(
            OptionalDecimal(totals, "parts", MoneyDecimals, 0),
            OptionalDecimal(totals, "panelWorkUnits", EstimatePolicy.WorkUnitDecimals, 0),
            OptionalDecimal(totals, "paintWorkUnits", EstimatePolicy.WorkUnitDecimals, 0),
            OptionalDecimal(totals, "materials", MoneyDecimals, 0),
            OptionalDecimal(totals, "specialist", MoneyDecimals, 0),
            OptionalDecimal(totals, "net", MoneyDecimals, 0),
            OptionalDecimal(totals, "vat", MoneyDecimals, 0),
            OptionalDecimal(totals, "gross", MoneyDecimals, 0));
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
        var price = OptionalDecimal(line, "price", MoneyDecimals, position);
        return new EstimateLineInput(
            type,
            OptionalText(line, "guideCode", 50),
            description,
            Hours(line, "labourHours", position),
            price,
            Unpriced: price is null && type == "new_part",
            OptionalText(line, "partNumber", 100),
            Betterment: null,
            Status: "estimated",
            EvidenceLabel: "reference",
            Justification: null,
            PaintWorkUnits: Hours(line, "paintHours", position),
            Quantity: OptionalQuantity(line, position),
            Materials: OptionalDecimal(line, "materials", MoneyDecimals, position),
            SourceRowIdentity: OptionalText(line, "rowId", 100));
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
            throw Reject(position, $"has an unreadable {name} (non-negative, {decimals} decimal places)");
        }
        return number;
    }

    /// <summary>
    /// Hours at the provider's own precision, bounded by the one estimate
    /// rule so a document can never state a figure the estimate cannot hold.
    /// </summary>
    private static decimal? Hours(JsonElement element, string name, int position)
    {
        var value = OptionalDecimal(element, name, EstimatePolicy.WorkUnitDecimals, position);
        return value is null || value <= EstimatePolicy.MaximumLineWorkUnits
            ? value
            : throw Reject(position, $"states a {name} beyond {EstimatePolicy.MaximumLineWorkUnits} hours");
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

    /// <summary>Position zero names the document's own totals block, which sits above the lines.</summary>
    private static EstimateParseRejectedException Reject(int position, string problem) => new(
        position > 0
            ? string.Create(CultureInfo.InvariantCulture, $"Line {position} {problem}, so nothing was imported.")
            : $"The document's printed totals block {problem}, so nothing was imported.");
}
