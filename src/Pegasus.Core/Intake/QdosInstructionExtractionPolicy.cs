using System.Globalization;
using System.Text.RegularExpressions;

namespace Pegasus.Core.Intake;

public sealed partial class QdosInstructionExtractionPolicy : IInstructionExtractionPolicy, IMailRoutePolicy
{
    public const string Key = "qdos_instruction";
    public const int Version = 1;
    public const string MailRouteKey = "qdos_mail_route";
    public const int MailRouteVersion = 1;
    private const string PrincipalCode = "QDOS";
    private const string AcceptedDirectDomain = "qdosassist.co.uk";

    private static readonly FieldDefinition[] FieldDefinitions =
    [
        new("Claimant name", ["Claimant Name", "Claimant"]),
        new("Claim number", ["Claim Number", "Claim No", "Claim Reference"]),
        new("Vehicle registration", ["Vehicle Registration", "Registration", "VRM"]),
        new("Vehicle make", ["Vehicle Make", "Make"]),
        new("Vehicle model", ["Vehicle Model", "Model"]),
        new("Vehicle mileage", ["Vehicle Mileage", "Mileage"]),
        new("Accident circumstances", ["Accident Circumstances", "Circumstances"]),
        new("Date of incident", ["Date of Incident", "Incident Date", "Accident Date"]),
        new("Instruction date", ["Instruction Date", "Date of Instruction"]),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"])
    ];

    public MailRouteEvaluationResult Evaluate(IntakeSourceReadResult readResult)
    {
        EnsureReadable(readResult);

        var senders = readResult.TransportEvidence
            .Where(item => item.Source == IntakeEvidenceSource.Sender)
            .Select(item => item.Value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasOneConsistentSender = senders.Length == 1;
        var senderDomain = string.Empty;
        var hasValidSender = hasOneConsistentSender
            && TryGetMailboxDomain(senders[0], out senderDomain);
        var matchesDirectQdosDomain = hasValidSender
            && string.Equals(senderDomain, AcceptedDirectDomain, StringComparison.OrdinalIgnoreCase);

        MailRoutePredicateResult[] predicates =
        [
            new(
                "direct.sender-exactly-one",
                hasOneConsistentSender,
                hasOneConsistentSender
                    ? "Exactly one consistent sender address was supplied."
                    : $"Expected one consistent sender address; found {senders.Length}."),
            new(
                "direct.qdos-domain",
                matchesDirectQdosDomain,
                matchesDirectQdosDomain
                    ? "The sender uses the accepted direct QDOS domain."
                    : "The sender does not use the accepted direct QDOS domain."),
            new(
                "intermediary.accepted-policy",
                false,
                "No QDOS intermediary mail route has accepted evidence in this policy version.")
        ];

        if (!hasOneConsistentSender)
        {
            return new(
                MailRouteDisposition.NeedsSorting,
                null,
                predicates,
                "Mail route evaluation requires exactly one consistent sender address.",
                MailRouteKey,
                MailRouteVersion);
        }

        if (!hasValidSender)
        {
            return new(
                MailRouteDisposition.NeedsSorting,
                null,
                predicates,
                "The sender address is malformed.",
                MailRouteKey,
                MailRouteVersion);
        }

        if (!matchesDirectQdosDomain)
        {
            return new(
                MailRouteDisposition.NoMatch,
                null,
                predicates,
                "The message does not match an accepted QDOS mail route.",
                MailRouteKey,
                MailRouteVersion);
        }

        return new(
            MailRouteDisposition.Accepted,
            new(PrincipalCode, MailRouteKind.DirectProvider, PrincipalCode),
            predicates,
            "The message matches the accepted direct QDOS mail route.",
            MailRouteKey,
            MailRouteVersion);
    }

    public InstructionExtractionResult Extract(
        IntakeSourceReadResult readResult,
        DateTimeOffset processedAtUtc)
    {
        var route = Evaluate(readResult);
        var evidence = new List<IntakeEvidence>();
        var confirmingFragments = new List<IntakeContentFragment>();
        foreach (var fragment in readResult.Content)
        {
            var labelsFound = FieldDefinitions.Count(definition =>
                definition.Labels.Any(label => ContainsLabel(fragment.Text, label)));
            var hasQdos = QdosMarkerRegex().IsMatch(fragment.Text);

            if (hasQdos)
            {
                evidence.Add(new(
                    fragment.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "qdos-content-marker",
                    $"QDOS was identified in {fragment.SourceLabel}."));
            }

            if (hasQdos && labelsFound >= 2)
            {
                confirmingFragments.Add(fragment);
                evidence.Add(new(
                    fragment.Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "instruction-structure",
                    $"{fragment.SourceLabel} contains QDOS and {labelsFound} instruction field labels."));
            }
        }

        AddTransportEvidence(
            readResult.TransportEvidence,
            route,
            confirmingFragments.Count > 0,
            evidence);

        if (confirmingFragments.Count > 0)
        {
            var (fields, missingFields, fieldEvidence) = ExtractFields(readResult.Content, processedAtUtc);
            evidence.AddRange(fieldEvidence);
            if (readResult.RequiresOcr)
            {
                evidence.Add(new(
                    IntakeEvidenceSource.PdfContent,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.Information,
                    "additional-scanned-content",
                    "A QDOS-shaped draft was extracted from readable content; additional scanned PDF content still requires review."));
            }

            return new(
                InstructionPolicyApplicability.Applicable,
                evidence,
                fields,
                CreateInstructionDraft(fields),
                missingFields,
                Key,
                Version);
        }

        return new(
            evidence.Count == 0 && !readResult.RequiresOcr
                ? InstructionPolicyApplicability.NotApplicable
                : InstructionPolicyApplicability.Indeterminate,
            evidence,
            [],
            null,
            [],
            Key,
            Version);
    }

    private static InstructionDraft CreateInstructionDraft(IReadOnlyList<InstructionReviewField> fields)
    {
        var values = fields.ToDictionary(
            field => field.Name,
            field => field.SuggestedValue,
            StringComparer.Ordinal);
        return new(
            PrincipalCode,
            TypedString(values["Claimant name"], 300),
            TypedString(values["Claim number"], 100),
            NormalizeRegistration(values["Vehicle registration"]),
            TypedString(values["Vehicle make"], 100),
            TypedString(values["Vehicle model"], 100),
            ParseMileage(values["Vehicle mileage"]),
            TypedString(values["Accident circumstances"], 2000),
            ParseDate(values["Date of incident"]),
            ParseDate(values["Instruction date"]),
            TypedString(values["Inspection address"], 1000));
    }

    private static void AddTransportEvidence(
        IReadOnlyList<IntakeTransportEvidence> transportEvidence,
        MailRouteEvaluationResult route,
        bool contentConfirmed,
        List<IntakeEvidence> evidence)
    {
        if (route.Disposition == MailRouteDisposition.Accepted)
        {
            evidence.Add(new(
                IntakeEvidenceSource.Sender,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.Information,
                "qdos-direct-mail-route",
                "The sender uses the accepted direct QDOS domain."));
        }

        foreach (var item in transportEvidence)
        {
            if (QdosMarkerRegex().IsMatch(item.Value))
            {
                evidence.Add(new(
                    item.Source,
                    IntakeEvidenceStrength.Weak,
                    IntakeEvidenceFinding.SupportsPrincipal,
                    "qdos-transport-marker",
                    $"{DisplaySource(item.Source)} contains a QDOS marker."));
            }
            else if (contentConfirmed && item.Source == IntakeEvidenceSource.Sender)
            {
                evidence.Add(new(
                    item.Source,
                    IntakeEvidenceStrength.Weak,
                    IntakeEvidenceFinding.ContradictsTransport,
                    "forwarded-sender",
                    "The sender does not identify QDOS; stronger instruction content takes precedence."));
            }
        }
    }

    private static void EnsureReadable(IntakeSourceReadResult readResult)
    {
        ArgumentNullException.ThrowIfNull(readResult);
        if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
        {
            throw new ArgumentException(
                "The QDOS extraction policy accepts only fully readable, complete reader results.",
                nameof(readResult));
        }
    }

    private static bool TryGetMailboxDomain(string address, out string domain)
    {
        domain = string.Empty;
        var separator = address.IndexOf('@');
        if (separator <= 0
            || separator != address.LastIndexOf('@')
            || separator == address.Length - 1
            || address.Any(char.IsWhiteSpace)
            || address.Contains('<')
            || address.Contains('>'))
        {
            return false;
        }

        domain = address[(separator + 1)..];
        return true;
    }

    private static (IReadOnlyList<InstructionReviewField> Fields, IReadOnlyList<string> Missing, IReadOnlyList<IntakeEvidence> Evidence)
        ExtractFields(IReadOnlyList<IntakeContentFragment> fragments, DateTimeOffset processedAtUtc)
    {
        var fields = new List<InstructionReviewField>();
        var missing = new List<string>();
        var evidence = new List<IntakeEvidence>();

        foreach (var definition in FieldDefinitions)
        {
            var candidates = fragments
                .SelectMany(fragment => FindCandidates(fragment, definition))
                .DistinctBy(candidate => candidate.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (candidates.Length == 0 && definition.Name == "Instruction date")
            {
                var defaultValue = DateOnly.FromDateTime(processedAtUtc.UtcDateTime)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var defaultCandidate = new InstructionFieldCandidate(
                    defaultValue,
                    IntakeEvidenceSource.SystemDefault,
                    "Receipt date");
                fields.Add(new(definition.Name, defaultValue, [defaultCandidate], true, false));
                evidence.Add(new(
                    IntakeEvidenceSource.SystemDefault,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.ExtractedField,
                    "instruction-date-defaulted",
                    "Instruction date was absent and was defaulted from the injected clock."));
                continue;
            }

            if (candidates.Length == 0)
            {
                fields.Add(new(definition.Name, null, [], false, false));
                missing.Add(definition.Name);
                evidence.Add(new(
                    IntakeEvidenceSource.SystemDefault,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.MissingField,
                    definition.Name,
                    $"No {definition.Name.ToLowerInvariant()} suggestion was found."));
                continue;
            }

            if (candidates.Length > 1)
            {
                fields.Add(new(definition.Name, null, candidates, false, true));
                evidence.Add(new(
                    candidates[0].Source,
                    IntakeEvidenceStrength.Strong,
                    IntakeEvidenceFinding.ConflictingField,
                    definition.Name,
                    $"Conflicting {definition.Name.ToLowerInvariant()} candidates require operator review."));
                continue;
            }

            fields.Add(new(definition.Name, candidates[0].Value, candidates, false, false));
            evidence.Add(new(
                candidates[0].Source,
                IntakeEvidenceStrength.Strong,
                IntakeEvidenceFinding.ExtractedField,
                definition.Name,
                $"{definition.Name} was suggested from {candidates[0].SourceLabel}."));
        }

        return (fields, missing, evidence);
    }

    private static IEnumerable<InstructionFieldCandidate> FindCandidates(
        IntakeContentFragment fragment,
        FieldDefinition definition)
    {
        var lines = fragment.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var label in definition.Labels)
            {
                var match = Regex.Match(
                    lines[index],
                    $@"(?i)(?:^|\s){Regex.Escape(label)}\s*(?::|-)?\s*(?<value>.*)$",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
                if (!match.Success)
                {
                    continue;
                }

                var value = match.Groups["value"].Value.Trim(' ', ':', '-', '|');
                if (string.IsNullOrWhiteSpace(value))
                {
                    var nextLine = lines
                        .Skip(index + 1)
                        .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
                    value = nextLine is not null && !StartsWithKnownFieldLabel(nextLine)
                        ? nextLine
                        : string.Empty;
                }

                value = WhitespaceRegex().Replace(value, " ").Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return new(value, fragment.Source, fragment.SourceLabel);
                }

                break;
            }
        }
    }

    private static string? TypedString(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength ? value : null;

    private static string? NormalizeRegistration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Regex.Replace(value, @"[\s-]", string.Empty, RegexOptions.CultureInvariant)
            .ToUpperInvariant();
        return normalized.Length <= 20 && RegistrationRegex().IsMatch(normalized) ? normalized : null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exactDate))
        {
            return exactDate;
        }

        return DateOnly.TryParse(
            value,
            CultureInfo.GetCultureInfo("en-GB"),
            DateTimeStyles.AllowWhiteSpaces,
            out var date)
            ? date
            : null;
    }

    private static long? ParseMileage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !MileageRegex().IsMatch(value))
        {
            return null;
        }

        var normalized = Regex.Replace(
            value,
            @"(?i)\s*(?:miles?|mi)\s*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        return long.TryParse(
            normalized,
            NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out var mileage)
            ? mileage
            : null;
    }

    private static bool StartsWithKnownFieldLabel(string line) =>
        FieldDefinitions.Any(definition => definition.Labels.Any(label =>
            Regex.IsMatch(
                line,
                $@"(?i)^{Regex.Escape(label)}(?:\s*(?::|-|\|)\s*|\s+|$)",
                RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100))));

    private static bool ContainsLabel(string text, string label) =>
        Regex.IsMatch(
            text,
            $@"(?i)\b{Regex.Escape(label)}\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static string DisplaySource(IntakeEvidenceSource source) => source switch
    {
        IntakeEvidenceSource.Sender => "Sender",
        IntakeEvidenceSource.Subject => "Subject",
        IntakeEvidenceSource.FileName => "File name",
        IntakeEvidenceSource.MimeType => "File type",
        _ => "Transport metadata"
    };

    [GeneratedRegex(@"\bQDOS\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QdosMarkerRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\s*(?:\d+|\d{1,3}(?:,\d{3})+)\s*(?:miles?|mi)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MileageRegex();

    [GeneratedRegex("^[A-Z0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RegistrationRegex();

    private sealed record FieldDefinition(string Name, string[] Labels);
}
