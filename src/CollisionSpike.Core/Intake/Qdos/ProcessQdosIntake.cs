using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CollisionSpike.Core.Intake.Qdos;

public sealed partial class ProcessQdosIntake(
    IQdosIntakeSourceReader sourceReader,
    IQdosIntakeStore store,
    TimeProvider timeProvider,
    IIntakeArtifactStore? artifactStore = null)
{
    private const string PrincipalCode = "QDOS";

    private static readonly FieldDefinition[] FieldDefinitions =
    [
        new("Claimant name", ["Claimant Name", "Claimant"]),
        new("Claim number", ["Claim Number", "Claim No", "Claim Reference"]),
        new("Vehicle registration", ["Vehicle Registration", "Registration", "VRM"]),
        new("Vehicle make", ["Vehicle Make", "Make"]),
        new("Vehicle model", ["Vehicle Model", "Model"]),
        new("Vehicle mileage", ["Vehicle Mileage", "Mileage"]),
        new("Accident circumstances", ["Accident Circumstances", "Circumstances"]),
        new("Date of incident", ["Date of Incident", "Incident Date", "Accident Date"], IsDate: true),
        new("Instruction date", ["Instruction Date", "Date of Instruction"], IsDate: true),
        new("Inspection address", ["Inspection Address", "Vehicle Location", "Inspection Location"])
    ];

    public async Task<QdosIntakeRecord> ExecuteAsync(
        QdosIntakeSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceIdentity.ExternalReceiptToken);

        var safeFileName = Path.GetFileName(source.FileName);
        var sourceHash = Convert.ToHexString(SHA256.HashData(source.Content.Span));
        var existing = await store.FindBySourceIdentityAsync(source.SourceIdentity, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.SourceHash, sourceHash, StringComparison.Ordinal))
            {
                throw new IntakeSourceIdentityConflictException();
            }

            return existing with { IsDuplicate = true };
        }

        IntakeSourceReadResult readResult;

        try
        {
            readResult = await sourceReader.ReadAsync(source with { FileName = safeFileName }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            readResult = new(
                IntakeSourceReadStatus.TechnicalFailure,
                [],
                [],
                [],
                false,
                "source_reader_failure",
                "The uploaded source could not be read because of a technical failure.");
        }

        var currentTime = timeProvider.GetUtcNow();
        var assessment = Assess(readResult, currentTime);
        IReadOnlyList<IntakeAssetRecord> assets = [];

        if (artifactStore is not null)
        {
            try
            {
                assets = await StoreAssetsAsync(
                    source with { FileName = safeFileName },
                    readResult.AssetCandidates,
                    artifactStore,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                assessment = FailureAssessment(
                    QdosIntakeDecision.TechnicalFailure,
                    "The uploaded source could not be retained for review.",
                    "artifact_storage_failure",
                    "The source and its extracted assets could not be stored at this time.",
                    [.. assessment.Evidence, new(
                        QdosEvidenceSource.SystemDefault,
                        QdosEvidenceStrength.Strong,
                        QdosEvidenceFinding.Information,
                        "artifact-storage-failure",
                        "Intake stopped because evidence retention failed.")]);
            }
        }

        var draft = new QdosIntakeDraft(
            safeFileName,
            source.MediaType,
            source.Content.Length,
            sourceHash,
            source.SourceIdentity,
            source.ReceivedAtUtc,
            currentTime,
            source.Actor,
            assessment.Decision,
            assessment.DecisionReason,
            assessment.Evidence,
            assessment.Fields,
            CreateTypedDraft(assessment),
            assessment.MissingFields,
            assessment.FailureCode,
            assessment.FailureReason,
            assets,
            readResult.ScannedPdfPages);

        return await store.StoreAsync(draft, cancellationToken);
    }

    private static QdosTypedDraft? CreateTypedDraft(Assessment assessment)
    {
        if (assessment.Decision != QdosIntakeDecision.ConfirmedQdos)
        {
            return null;
        }

        var values = assessment.Fields.ToDictionary(
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

    private static string? TypedString(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength
            ? value
            : null;

    private static string? NormalizeRegistration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = Regex.Replace(value, @"[\s-]", string.Empty, RegexOptions.CultureInvariant)
            .ToUpperInvariant();
        return normalized.Length <= 20 && RegistrationRegex().IsMatch(normalized)
            ? normalized
            : null;
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
        if (string.IsNullOrWhiteSpace(value)
            || !MileageRegex().IsMatch(value))
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

    private static async Task<IReadOnlyList<IntakeAssetRecord>> StoreAssetsAsync(
        QdosIntakeSource source,
        IReadOnlyList<IntakeAssetCandidate> extractedAssets,
        IIntakeArtifactStore artifactStore,
        CancellationToken cancellationToken)
    {
        var sourceAsset = new IntakeAssetCandidate(
            "uploaded source",
            source.FileName,
            source.MediaType,
            source.Content,
            IntakeAssetKind.Source,
            IntakeAssetDisposition.Source);
        var candidates = new[] { sourceAsset }
            .Concat(extractedAssets)
            .ToArray();
        var stored = new List<IntakeAssetRecord>(candidates.Length);

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var contentHash = Convert.ToHexString(SHA256.HashData(candidate.Content.Span));
            var storageKey = await artifactStore.StoreAsync(contentHash, candidate.Content, cancellationToken);
            stored.Add(new(
                Guid.NewGuid(),
                candidate.SourceLabel,
                Path.GetFileName(candidate.FileName),
                candidate.MediaType,
                candidate.Kind,
                candidate.Disposition,
                candidate.Content.Length,
                contentHash,
                storageKey,
                candidate.PageNumber,
                candidate.Bounds,
                candidate.WidthPixels,
                candidate.HeightPixels));
        }

        return stored;
    }

    private static Assessment Assess(IntakeSourceReadResult readResult, DateTimeOffset receivedAtUtc)
    {
        var evidence = new List<QdosEvidence>();

        foreach (var issue in readResult.Issues)
        {
            evidence.Add(new(
                issue.Source,
                QdosEvidenceStrength.Strong,
                QdosEvidenceFinding.Information,
                issue.Code,
                issue.Reason));
        }

        if (readResult.Status == IntakeSourceReadStatus.Unsupported)
        {
            return FailureAssessment(
                QdosIntakeDecision.Unsupported,
                "The uploaded source is not readable as a supported email, document, PDF or image.",
                readResult.FailureCode ?? "unsupported_source",
                readResult.FailureReason ?? "The file is unsupported or corrupt.",
                evidence);
        }

        if (readResult.Status == IntakeSourceReadStatus.TechnicalFailure)
        {
            return FailureAssessment(
                QdosIntakeDecision.TechnicalFailure,
                "The uploaded source could not be assessed because of a technical failure.",
                readResult.FailureCode ?? "technical_failure",
                readResult.FailureReason ?? "The source could not be processed at this time.",
                evidence);
        }

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
                    QdosEvidenceStrength.Strong,
                    QdosEvidenceFinding.SupportsQdos,
                    "qdos-content-marker",
                    $"QDOS was identified in {fragment.SourceLabel}."));
            }

            if (hasQdos && labelsFound >= 2)
            {
                confirmingFragments.Add(fragment);
                evidence.Add(new(
                    fragment.Source,
                    QdosEvidenceStrength.Strong,
                    QdosEvidenceFinding.SupportsQdos,
                    "instruction-structure",
                    $"{fragment.SourceLabel} contains QDOS and {labelsFound} instruction field labels."));
            }
        }

        AddTransportEvidence(readResult.TransportEvidence, confirmingFragments.Count > 0, evidence);

        if (readResult.IsIncomplete)
        {
            return new(
                QdosIntakeDecision.NeedsSorting,
                "The source was retained, but processing could not be completed safely and requires manual sorting.",
                evidence,
                [],
                [],
                null,
                null);
        }

        if (confirmingFragments.Count > 0)
        {
            var (fields, missingFields, fieldEvidence) = ExtractFields(readResult.Content, receivedAtUtc);
            evidence.AddRange(fieldEvidence);

            if (readResult.RequiresOcr)
            {
                evidence.Add(new(
                    QdosEvidenceSource.PdfContent,
                    QdosEvidenceStrength.Strong,
                    QdosEvidenceFinding.Information,
                    "additional-scanned-content",
                    "QDOS is confirmed from readable content; additional scanned PDF content still requires review."));
            }

            return new(
                QdosIntakeDecision.ConfirmedQdos,
                "QDOS is confirmed from instruction content.",
                evidence,
                fields,
                missingFields,
                null,
                null);
        }

        if (readResult.RequiresOcr)
        {
            return FailureAssessment(
                QdosIntakeDecision.OcrRequired,
                "Readable content is insufficient to decide whether this is a QDOS instruction.",
                "ocr_required",
                "The PDF appears to contain scanned pages without enough embedded text for review.",
                evidence);
        }

        return new(
            QdosIntakeDecision.NeedsSorting,
            "The readable content does not provide enough evidence to confirm a QDOS instruction.",
            evidence,
            [],
            [],
            null,
            null);
    }

    private static Assessment FailureAssessment(
        QdosIntakeDecision decision,
        string decisionReason,
        string failureCode,
        string failureReason,
        IReadOnlyList<QdosEvidence> evidence) =>
        new(decision, decisionReason, evidence, [], [], failureCode, failureReason);

    private static void AddTransportEvidence(
        IReadOnlyList<IntakeTransportEvidence> transportEvidence,
        bool contentConfirmed,
        List<QdosEvidence> evidence)
    {
        foreach (var item in transportEvidence)
        {
            var hasQdos = QdosMarkerRegex().IsMatch(item.Value);
            if (hasQdos)
            {
                evidence.Add(new(
                    item.Source,
                    QdosEvidenceStrength.Weak,
                    QdosEvidenceFinding.SupportsQdos,
                    "qdos-transport-marker",
                    $"{DisplaySource(item.Source)} contains a QDOS marker."));
            }
            else if (contentConfirmed && item.Source == QdosEvidenceSource.Sender)
            {
                evidence.Add(new(
                    item.Source,
                    QdosEvidenceStrength.Weak,
                    QdosEvidenceFinding.ContradictsTransport,
                    "forwarded-sender",
                    "The sender does not identify QDOS; stronger instruction content takes precedence."));
            }
        }
    }

    private static (IReadOnlyList<QdosReviewField> Fields, IReadOnlyList<string> Missing, IReadOnlyList<QdosEvidence> Evidence)
        ExtractFields(IReadOnlyList<IntakeContentFragment> fragments, DateTimeOffset receivedAtUtc)
    {
        var fields = new List<QdosReviewField>();
        var missing = new List<string>();
        var evidence = new List<QdosEvidence>();

        foreach (var definition in FieldDefinitions)
        {
            var candidates = fragments
                .SelectMany(fragment => FindCandidates(fragment, definition))
                .DistinctBy(candidate => candidate.Value, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (candidates.Length == 0 && definition.Name == "Instruction date")
            {
                var defaultValue = DateOnly.FromDateTime(receivedAtUtc.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                var defaultCandidate = new QdosFieldCandidate(defaultValue, QdosEvidenceSource.SystemDefault, "Receipt date");
                fields.Add(new(definition.Name, defaultValue, [defaultCandidate], true, false));
                evidence.Add(new(
                    QdosEvidenceSource.SystemDefault,
                    QdosEvidenceStrength.Strong,
                    QdosEvidenceFinding.ExtractedField,
                    "instruction-date-defaulted",
                    "Instruction date was absent and was defaulted from the injected clock."));
                continue;
            }

            if (candidates.Length == 0)
            {
                fields.Add(new(definition.Name, null, [], false, false));
                missing.Add(definition.Name);
                evidence.Add(new(
                    QdosEvidenceSource.SystemDefault,
                    QdosEvidenceStrength.Strong,
                    QdosEvidenceFinding.MissingField,
                    definition.Name,
                    $"No {definition.Name.ToLowerInvariant()} suggestion was found."));
                continue;
            }

            if (candidates.Length > 1)
            {
                fields.Add(new(definition.Name, null, candidates, false, true));
                evidence.Add(new(
                    candidates[0].Source,
                    QdosEvidenceStrength.Strong,
                    QdosEvidenceFinding.ConflictingField,
                    definition.Name,
                    $"Conflicting {definition.Name.ToLowerInvariant()} candidates require operator review."));
                continue;
            }

            fields.Add(new(definition.Name, candidates[0].Value, candidates, false, false));
            evidence.Add(new(
                candidates[0].Source,
                QdosEvidenceStrength.Strong,
                QdosEvidenceFinding.ExtractedField,
                definition.Name,
                $"{definition.Name} was suggested from {candidates[0].SourceLabel}."));
        }

        return (fields, missing, evidence);
    }

    private static IEnumerable<QdosFieldCandidate> FindCandidates(
        IntakeContentFragment fragment,
        FieldDefinition definition)
    {
        var lines = fragment.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.TrimEntries);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            foreach (var label in definition.Labels)
            {
                var match = Regex.Match(
                    line,
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
                    value = lines.Skip(index + 1).FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)) ?? string.Empty;
                }

                value = NormalizeValue(definition, value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return new(value, fragment.Source, fragment.SourceLabel);
                }

                break;
            }
        }
    }

    private static string NormalizeValue(FieldDefinition definition, string value)
    {
        value = WhitespaceRegex().Replace(value, " ").Trim();
        return value;
    }

    private static bool ContainsLabel(string text, string label) =>
        Regex.IsMatch(
            text,
            $@"(?i)\b{Regex.Escape(label)}\b",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    private static string DisplaySource(QdosEvidenceSource source) => source switch
    {
        QdosEvidenceSource.Sender => "Sender",
        QdosEvidenceSource.Subject => "Subject",
        QdosEvidenceSource.FileName => "File name",
        QdosEvidenceSource.MimeType => "File type",
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

    private sealed record FieldDefinition(string Name, string[] Labels, bool IsDate = false);

    private sealed record Assessment(
        QdosIntakeDecision Decision,
        string DecisionReason,
        IReadOnlyList<QdosEvidence> Evidence,
        IReadOnlyList<QdosReviewField> Fields,
        IReadOnlyList<string> MissingFields,
        string? FailureCode,
        string? FailureReason);
}
