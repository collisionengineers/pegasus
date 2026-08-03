using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

/// <summary>
/// QDOS case-match key extraction and normalization (operator-accepted predicates,
/// decision 2026-08-03). Extraction is label-anchored with a required separator — free
/// text is never scraped, which is what excludes the predecessor's false registrations
/// (AND2 from an office-address footer, OCTOBER, postcode outward codes). The durable
/// claim identity is the NNNNN/N tail, so 'MFI/AKH/46553/1' and a bare '46553/1' hit
/// the same claim; qdoslaw.co.uk references keep their own letters-only grammar under
/// the same provider. Only the client vehicle is a key: TP-prefixed labels are skipped,
/// which keeps two claimants from one accident apart.
/// </summary>
public sealed partial class QdosCaseMatchPolicy : IProviderCaseMatchPolicy
{
    public const string Key = "qdos_case_match";
    public const int Version = 1;

    public string WorkProviderCode => "QDOS";
    public string PolicyKey => Key;
    public int PolicyVersion => Version;

    public CaseMatchKeys ExtractMatchKeys(IntakeSourceReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        var subject = readResult.TransportEvidence
            .FirstOrDefault(item => item.Source == IntakeEvidenceSource.Subject)?.Value ?? string.Empty;
        var texts = readResult.Content
            .Where(fragment =>
                fragment.Source is IntakeEvidenceSource.EmailBody
                    or IntakeEvidenceSource.DocumentContent
                    or IntakeEvidenceSource.PdfContent)
            .Select(fragment => fragment.Text)
            .Prepend(subject)
            .ToArray();

        var claimTokens = new List<string>();
        foreach (var text in texts)
        {
            claimTokens.AddRange(
                ClaimLabelRegex().Matches(text)
                    .Select(match => NormalizeClaimReference(match.Groups["value"].Value))
                    .OfType<string>());
        }

        claimTokens.AddRange(
            BareClaimTokenRegex().Matches(subject)
                .Select(match => WhitespaceRegex().Replace(match.Value, string.Empty)));

        var vrms = new List<string>();
        var names = new List<(string Surname, string Initial)>();
        var dates = new List<DateOnly>();
        foreach (var text in texts)
        {
            vrms.AddRange(
                VrmLabelRegex().Matches(text)
                    .Where(match => !match.Groups["tp"].Success)
                    .Select(match => NormalizeVrm(match.Groups["value"].Value))
                    .OfType<string>());
            names.AddRange(
                NameLabelRegex().Matches(text)
                    .Select(match => NormalizeName(match.Groups["value"].Value))
                    .OfType<(string, string)>());
            dates.AddRange(
                DateLabelRegex().Matches(text)
                    .Select(match => InstructionFieldEngine.ParseDate(match.Groups["value"].Value.Trim()))
                    .OfType<DateOnly>());
        }

        dates.AddRange(
            SubjectIncidentDateRegex().Matches(subject)
                .Select(match => InstructionFieldEngine.ParseDate(match.Groups["value"].Value))
                .OfType<DateOnly>());

        var claimToken = SingleDistinct(claimTokens);
        var vrm = SingleDistinct(vrms);
        var name = SingleDistinctName(names);
        var date = SingleDistinctDate(dates);
        return new(
            claimToken,
            vrm,
            name?.Surname,
            name?.Initial,
            date);
    }

    public CaseMatchIndexKeys DeriveIndexKeys(CaseMatchSourceData caseData)
    {
        ArgumentNullException.ThrowIfNull(caseData);
        var name = NormalizeName(caseData.ClaimantName ?? string.Empty);
        return new(
            NormalizeClaimReference(caseData.ClaimNumber ?? string.Empty),
            NormalizeVrm(caseData.VehicleRegistration ?? string.Empty),
            name?.Surname,
            name?.Initial,
            caseData.IncidentDate);
    }

    internal static string? NormalizeClaimReference(string value)
    {
        var trimmed = CompactWhitespace(CutAtDashSegment(value).Trim().Trim('.', ',', ';', ')', '('));
        if (trimmed.Length == 0)
        {
            return null;
        }

        var token = ClaimTokenRegex().Match(trimmed);
        if (token.Success)
        {
            return WhitespaceRegex().Replace(token.Groups["token"].Value, string.Empty);
        }

        var compact = WhitespaceRegex().Replace(trimmed, string.Empty).ToUpperInvariant();
        return QdosLawReferenceRegex().IsMatch(compact) ? compact : null;
    }

    internal static string? NormalizeVrm(string value)
    {
        var compact = VrmCompactionRegex()
            .Replace(value.Trim(), string.Empty)
            .ToUpperInvariant();
        return compact.Length is >= 5 and <= 10
            && compact.All(char.IsAsciiLetterOrDigit)
            && compact.Any(char.IsAsciiDigit)
            && compact.Any(char.IsAsciiLetter)
            ? compact
            : null;
    }

    internal static (string Surname, string Initial)? NormalizeName(string value)
    {
        var tokens = CutAtDashSegment(value)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim('.', ',', ';', ':'))
            .Where(token => token.Length > 0)
            .SkipWhile(token => Titles.Contains(token, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (tokens.Length < 2
            || !tokens.All(token => token.All(character =>
                char.IsAsciiLetter(character) || character is '-' or '\'')))
        {
            return null;
        }

        return (
            tokens[^1].ToUpperInvariant(),
            tokens[0][..1].ToUpperInvariant());
    }

    private static string? SingleDistinct(IReadOnlyList<string> values)
    {
        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static (string Surname, string Initial)? SingleDistinctName(
        IReadOnlyList<(string Surname, string Initial)> values)
    {
        var distinct = values.Distinct().ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static DateOnly? SingleDistinctDate(IReadOnlyList<DateOnly> values)
    {
        var distinct = values.Distinct().ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static string CompactWhitespace(string value) =>
        WhitespaceRegex().Replace(value, " ").Trim();

    private static string CutAtDashSegment(string value)
    {
        var separator = value.IndexOf(" - ", StringComparison.Ordinal);
        return separator < 0 ? value : value[..separator];
    }

    private static readonly string[] Titles =
        ["Mr", "Mrs", "Ms", "Miss", "Dr", "Mx", "Master", "Mstr", "Sir", "Rev"];

    [GeneratedRegex(
        @"(?:^|[\s(])(?:Our\s+Ref(?:erence)?|Our\s+claim\s+Reference|Claim\s+(?:Number|No|Reference))\s*[:;-]\s*(?<value>[^,)\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex ClaimLabelRegex();

    [GeneratedRegex(
        @"\b\d{4,6}\s*/\s*\d{1,2}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex BareClaimTokenRegex();

    [GeneratedRegex(
        @"\b(?<token>\d{4,6}\s*/\s*\d{1,2})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex ClaimTokenRegex();

    [GeneratedRegex(
        @"^[A-Z]{2,5}/[A-Z]{2,5}\d{3,6}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex QdosLawReferenceRegex();

    [GeneratedRegex(
        @"(?:^|[\s(])(?<tp>TP\s+)?(?:Vehicle\s+Registration|Vehicle\s+Reg|Registration|VRM)\s*[:;-]\s*(?<value>[^,)\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex VrmLabelRegex();

    [GeneratedRegex(
        @"(?:^|[\s(])(?:Claimant\s+Name|Claimant|Our\s+Client|Mutual\s+Client)\s*[:;-]\s*(?<value>[^,)\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex NameLabelRegex();

    [GeneratedRegex(
        @"(?:^|[\s(])(?:Date\s+of\s+Incident|Incident\s+Date|Accident\s+Date|Date\s+of\s+Accident)\s*[:;-]\s*(?<value>[^,)\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex DateLabelRegex();

    [GeneratedRegex(
        @"\bon\s+(?<value>\d{2}/\d{2}/\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubjectIncidentDateRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[\s-]", RegexOptions.CultureInvariant)]
    private static partial Regex VrmCompactionRegex();
}
