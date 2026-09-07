using System.Globalization;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Single owner of assessment validation. The closed field vocabulary fails
/// closed on unknown or case-owned paths; values are canonicalized before
/// persistence; the required-when pairings from the screen's own hints are
/// enforced against the merged state; and the actor rules implement the
/// operator-decided direct-write model: staff saves record confirmed values,
/// Automation saves record unconfirmed values, and a professional-finding
/// field is confirmable only by a staff Engineer (the EngineerFindingPolicy
/// precedent). Estimate derivation (totals, worklists) is deliberately absent
/// until its formulas hold accepted authority (EXT-09, open decision D2).
/// </summary>
public static class AssessmentPolicy
{
    public const string PolicyKey = "case-assessment-edit";
    public const int PolicyVersion = 1;
    public const int MaximumFieldsPerSave = 120;
    public const int MaximumEstimateLines = 200;

    public static SaveAssessmentRequest ValidateAndNormalize(SaveAssessmentRequest request)
    {
        CaseLifecycleRules.ValidateMutation(request);
        ArgumentNullException.ThrowIfNull(request.Fields);
        if (request.Actor.Kind is not (ActorKind.Staff or ActorKind.Automation))
        {
            throw new InvalidOperationException(
                "Only a staff member or the Automation actor can save an assessment.");
        }
        if (request.Fields.Count == 0 && request.EstimateLines is null)
        {
            throw new ArgumentException(
                "An assessment save requires at least one field or the estimate-line collection.",
                nameof(request));
        }
        if (request.Fields.Count > MaximumFieldsPerSave)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"An assessment save is bounded to {MaximumFieldsPerSave} fields.");
        }
        if (request.AiWorkRequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "The optional work-request binding cannot be an empty identifier.",
                nameof(request));
        }

        var normalizedFields = new Dictionary<string, string?>(StringComparer.Ordinal);
        var touchesFinding = false;
        foreach (var (path, rawValue) in request.Fields)
        {
            normalizedFields[path] = NormalizeWritableField(path, rawValue);
            touchesFinding |= AssessmentVocabulary.Definitions[path].IsFinding;
        }

        if (touchesFinding && request.Actor.Kind == ActorKind.Staff)
        {
            RequireFindingConfirmationAuthority(request.Actor);
        }

        var normalizedLines = request.EstimateLines is null
            ? null
            : NormalizeLines(request.EstimateLines);
        return request with { Fields = normalizedFields, EstimateLines = normalizedLines };
    }

    /// <summary>
    /// The one owner of who may confirm a professional finding: a staff
    /// member only when that member is an authenticated Engineer. The
    /// assessment save applies it to its staff branch (the Automation actor
    /// records unconfirmed working data instead); a caller that writes a
    /// finding field as a confirmed value outside that save - the Engineer's
    /// Value valuation - applies it on its own.
    /// </summary>
    public static void RequireFindingConfirmationAuthority(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.Staff || !actor.IsInRole(StaffRole.Engineer))
        {
            throw new InvalidOperationException(
                "A professional finding can be recorded by staff only when the staff member "
                + "is an authenticated Engineer.");
        }
    }

    /// <summary>
    /// The one gate every generic field save passes: the path must be part of
    /// the vocabulary, must not be derived from the damage impacts, must not
    /// be owned by the accepted case record, and must not be a finding a named
    /// command adopts (AUTO-015). The value is then canonicalized against its
    /// own definition. Both the assessment save and the Case workspace save
    /// call it, so an unwritable path fails the same way on either route.
    /// </summary>
    public static string? NormalizeWritableField(string path, string? rawValue)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (AssessmentVocabulary.DerivedPaths.Contains(path))
        {
            throw new InvalidOperationException(
                $"The field '{path}' is derived from damage.impacts and cannot be written directly.");
        }
        if (AssessmentVocabulary.CaseOwnedPaths.Contains(path))
        {
            throw new InvalidOperationException(
                $"The field '{path}' is owned by the accepted case record; "
                + "save it through the case-detail edit path instead.");
        }
        if (AssessmentVocabulary.AdoptedFindingPaths.Contains(path))
        {
            throw new InvalidOperationException(
                $"The field '{path}' is adopted only by the valuation Apply command; "
                + "a field save can neither record nor clear it.");
        }
        if (!AssessmentVocabulary.Definitions.TryGetValue(path, out var definition))
        {
            throw new ArgumentException(
                $"The field path '{path}' is not part of the assessment vocabulary.",
                nameof(path));
        }

        return NormalizeValue(definition, rawValue);
    }

    /// <summary>
    /// Canonicalizes one value against its own vocabulary definition. The
    /// assessment save normalizes through the same rules; a caller that
    /// writes a single field outside that save canonicalizes it here rather
    /// than keeping a second copy of the format.
    /// </summary>
    public static string? NormalizeFieldValue(string path, string? rawValue)
    {
        if (!AssessmentVocabulary.Definitions.TryGetValue(path, out var definition))
        {
            throw new ArgumentException(
                $"The field path '{path}' is not part of the assessment vocabulary.",
                nameof(path));
        }

        return NormalizeValue(definition, rawValue);
    }

    public static IReadOnlyList<AssessmentImpact> ParseImpacts(string? value)
    {
        if (value is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(value);
        return ReadImpacts(document.RootElement);
    }

    public static (string? Location, string? Severity) DeriveImpactValues(string? value)
    {
        var impacts = ParseImpacts(value);
        if (impacts.Count == 0)
        {
            return (null, null);
        }

        var location = impacts.Count > 1
            ? "multiple"
            : AssessmentVocabulary.DamageZones[impacts[0].Zone].ImpactLocation;
        var severity = impacts.MaxBy(
            impact => AssessmentVocabulary.DamageSeverities[impact.Severity].Rank)!.Severity;
        return (location, severity);
    }

    /// <summary>
    /// Cross-field pairings from the screen hints, applied to the merged
    /// state whenever a save writes the governing value. Plain "required"
    /// rules stay readiness items so section-by-section saves remain possible.
    /// </summary>
    public static void ValidateMergedState(
        IReadOnlyDictionary<string, string?> savedFields,
        IReadOnlyDictionary<string, string> mergedState)
    {
        ArgumentNullException.ThrowIfNull(savedFields);
        ArgumentNullException.ThrowIfNull(mergedState);
        if (savedFields.TryGetValue(AssessmentVocabulary.LegalStatus, out var legalStatus)
            && string.Equals(legalStatus, "unroadworthy", StringComparison.Ordinal)
            && !mergedState.ContainsKey(AssessmentVocabulary.UnroadworthyReason))
        {
            throw new InvalidOperationException(
                "Recording the vehicle as unroadworthy requires the reason it is unroadworthy.");
        }

        if (savedFields.TryGetValue(AssessmentVocabulary.Outcome, out var outcome)
            && string.Equals(outcome, "total_loss", StringComparison.Ordinal))
        {
            if (!mergedState.ContainsKey(AssessmentVocabulary.SalvageCategory))
            {
                throw new InvalidOperationException(
                    "A total-loss outcome requires the salvage category.");
            }
            if (!mergedState.ContainsKey(AssessmentVocabulary.SalvageValue))
            {
                throw new InvalidOperationException(
                    "A total-loss outcome requires the salvage value.");
            }
        }
    }

    public static bool IsWritableState(CaseLifecycleState state) =>
        state is CaseLifecycleState.NotReady
            or CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation;

    /// <summary>
    /// The readiness rail: every requirement the screen names, with its
    /// source and resolution. A derived read model only — nothing here is a
    /// save-blocker, and no value is ever guessed.
    /// </summary>
    public static IReadOnlyList<AssessmentReadinessItem> EvaluateReadiness(
        CaseAssessmentProjection projection) => Evaluate(projection, includeReviewEntryRequirements: true);

    /// <summary>
    /// Assessment and report work still required after entry to Review. Case
    /// facts already proved by that transition are deliberately excluded.
    /// </summary>
    public static IReadOnlyList<AssessmentReadinessItem> EvaluatePostReviewReadiness(
        CaseAssessmentProjection projection) => Evaluate(projection, includeReviewEntryRequirements: false);

    private static List<AssessmentReadinessItem> Evaluate(
        CaseAssessmentProjection projection,
        bool includeReviewEntryRequirements)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var items = new List<AssessmentReadinessItem>();
        var fields = projection.Fields.ToDictionary(
            field => field.Path,
            field => field.Value,
            StringComparer.Ordinal);

        void RequireField(string path, string requirement, string section)
        {
            if (!fields.ContainsKey(path))
            {
                items.Add(new(
                    requirement,
                    "Assessment record",
                    "No value is recorded.",
                    $"Record it on the {section} section."));
            }
        }

        if (includeReviewEntryRequirements)
        {
            if (projection.CaseOwned.Registration is null)
            {
                items.Add(new(
                    "Vehicle registration", "Case record",
                    "No confirmed registration is recorded.",
                    "Confirm it on the case details."));
            }
            if (projection.CaseOwned.Make is null)
            {
                items.Add(new(
                    "Vehicle make", "Case record",
                    "No confirmed make is recorded.",
                    "Confirm it on the case details."));
            }
            if (projection.CaseOwned.Model is null)
            {
                items.Add(new(
                    "Vehicle model", "Case record",
                    "No confirmed model is recorded.",
                    "Confirm it on the case details."));
            }
            if (projection.CaseOwned.InstructionDate is null)
            {
                items.Add(new(
                    "Instructions received date", "Case record",
                    "No confirmed instruction date is recorded.",
                    "Confirm it on the case details."));
            }
        }

        RequireField(AssessmentVocabulary.VehicleType, "Vehicle type", "Vehicle");
        RequireField(AssessmentVocabulary.VehicleYear, "Vehicle year", "Vehicle");
        RequireField(AssessmentVocabulary.VehicleMileageSource, "Mileage source", "Vehicle");
        RequireField(AssessmentVocabulary.VehicleCondition, "Pre-incident condition", "Vehicle");
        RequireField(AssessmentVocabulary.IncidentAssessed, "Assessed date", "Incident and impact");
        RequireField(AssessmentVocabulary.ImpactSeverity, "Impact severity", "Incident and impact");
        RequireField(AssessmentVocabulary.ImpactLocation, "Impact location", "Incident and impact");
        RequireField(AssessmentVocabulary.ValueRetail, "Retail value", "Valuation");
        RequireField(AssessmentVocabulary.ValueTrade, "Trade value", "Valuation");
        RequireField(AssessmentVocabulary.ValueEngineer, "Engineer's value", "Valuation");
        RequireField(
            AssessmentVocabulary.CostRepairerVatRegistered,
            "Repairer VAT answer",
            "Estimate");
        RequireField(AssessmentVocabulary.Outcome, "Assessment outcome", "Findings");
        RequireField(AssessmentVocabulary.LegalStatus, "Roadworthiness", "Findings");
        RequireField(AssessmentVocabulary.HistoryCheck, "Vehicle history check", "Report content");
        // ENG-038: the Engineer name, qualifications and signature readiness
        // items are retired (D18). The signing Engineer is the selected
        // sign-off account, whose printed name, qualifications and signature
        // come from that account, so typed copies of them were three ways to
        // record the same three facts.
        RequireField(AssessmentVocabulary.AgreedFee, "Agreed fee", "Report content");

        if (includeReviewEntryRequirements)
        {
            var mileageSourceIsTbc = fields.TryGetValue(
                    AssessmentVocabulary.VehicleMileageSource,
                    out var mileageSource)
                && string.Equals(mileageSource, "tbc", StringComparison.Ordinal);
            if (!mileageSourceIsTbc && projection.CaseOwned.Mileage is null)
            {
                items.Add(new(
                    "Odometer reading", "Case record",
                    "No confirmed mileage is recorded and the mileage source is not To be confirmed.",
                    "Confirm the mileage on the case details, or record the mileage source as To be confirmed."));
            }
        }

        if (fields.TryGetValue(AssessmentVocabulary.LegalStatus, out var legalStatus)
            && string.Equals(legalStatus, "unroadworthy", StringComparison.Ordinal)
            && !fields.ContainsKey(AssessmentVocabulary.UnroadworthyReason))
        {
            items.Add(new(
                "Unroadworthy reason",
                "Assessment record",
                "The vehicle is recorded as unroadworthy without a reason.",
                "Record the reason on the Findings section."));
        }

        if (fields.TryGetValue(AssessmentVocabulary.Outcome, out var outcome)
            && string.Equals(outcome, "total_loss", StringComparison.Ordinal))
        {
            if (!fields.ContainsKey(AssessmentVocabulary.SalvageCategory))
            {
                items.Add(new(
                    "Salvage category",
                    "Assessment record",
                    "The outcome is Total loss without a salvage category.",
                    "Record it on the Findings section."));
            }
            if (!fields.ContainsKey(AssessmentVocabulary.SalvageValue))
            {
                items.Add(new(
                    "Salvage value",
                    "Assessment record",
                    "The outcome is Total loss without a salvage value.",
                    "Record it on the Findings section."));
            }
        }

        if (includeReviewEntryRequirements
            && string.Equals(
                projection.CaseOwned.InspectionMode,
                "PhysicalAddress",
                StringComparison.Ordinal)
            && projection.CaseOwned.InspectionAddress is null)
        {
            items.Add(new(
                "Inspection address", "Case record",
                "The method is Physical without an inspection address.",
                "Confirm the address on the case details."));
        }

        // One actionable blocker per unconfirmed value, naming the exact
        // field or line and who recorded it. A single aggregate count is
        // prohibited: an unmet requirement has to identify its own material,
        // provenance, reason, and permitted resolution.
        foreach (var field in projection.Fields.Where(field => !field.IsConfirmed))
        {
            items.Add(new(
                $"{field.Path} awaits review",
                $"Recorded by {field.RecordedByKind} ({field.RecordedBy})",
                "The value is unconfirmed working data until an Engineer confirms it.",
                "Review the value and re-save it as the assigned Engineer to confirm it."));
        }

        foreach (var line in projection.EstimateLines.Where(line => !line.IsConfirmed))
        {
            items.Add(new(
                $"Estimate line {line.Position} ({line.Type}) awaits review",
                $"Recorded by {line.RecordedByKind} ({line.RecordedBy})",
                "The line is unconfirmed working data until an Engineer confirms it.",
                "Review the line and re-save the estimate as the assigned Engineer to confirm it."));
        }

        return items;
    }

    private static string? NormalizeValue(AssessmentFieldDefinition definition, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var value = rawValue.Trim();
        if (value.Length > definition.MaximumLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rawValue),
                $"The value for '{definition.Path}' cannot exceed {definition.MaximumLength} characters.");
        }

        switch (definition.Type)
        {
            case AssessmentFieldType.Text:
                // Only the supported line breaks are permitted. Testing the
                // whole value for "contains a newline" would have let NUL,
                // escape, and form-feed through any multi-line narrative.
                if (value.Any(character =>
                    char.IsControl(character) && character is not ('\n' or '\r')))
                {
                    throw new ArgumentException(
                        $"The value for '{definition.Path}' contains control characters.",
                        nameof(rawValue));
                }
                return string.Join(
                    '\n',
                    value.Split('\n').Select(line => line.TrimEnd('\r').TrimEnd()));

            case AssessmentFieldType.Enumerated:
                if (definition.Codes is null
                    || !definition.Codes.Contains(value, StringComparer.Ordinal))
                {
                    throw new ArgumentException(
                        $"The value for '{definition.Path}' is not one of its accepted codes.",
                        nameof(rawValue));
                }
                return value;

            case AssessmentFieldType.WholeNumber:
                if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)
                    || integer < 0)
                {
                    throw new ArgumentException(
                        $"The value for '{definition.Path}' must be a non-negative whole number.",
                        nameof(rawValue));
                }
                return integer.ToString(CultureInfo.InvariantCulture);

            case AssessmentFieldType.Money:
                if (!decimal.TryParse(
                        value,
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var money)
                    || money < 0
                    || decimal.Round(money, 2) != money)
                {
                    throw new ArgumentException(
                        $"The value for '{definition.Path}' must be a non-negative amount with at most two decimal places.",
                        nameof(rawValue));
                }
                if (definition.MustBePositive && money <= 0)
                {
                    throw new ArgumentException(
                        $"The value for '{definition.Path}' must be greater than zero.",
                        nameof(rawValue));
                }
                return money.ToString("0.00", CultureInfo.InvariantCulture);

            case AssessmentFieldType.Flag:
                return value.ToLowerInvariant() switch
                {
                    "true" => "true",
                    "false" => "false",
                    _ => throw new ArgumentException(
                        $"The value for '{definition.Path}' must be 'true' or 'false'.",
                        nameof(rawValue))
                };

            case AssessmentFieldType.Date:
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out var date)
                    || date == DateOnly.MinValue)
                {
                    throw new ArgumentException(
                        $"The value for '{definition.Path}' must be a yyyy-MM-dd date.",
                        nameof(rawValue));
                }
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case AssessmentFieldType.Json:
                return NormalizeImpacts(value);

            default:
                throw new InvalidOperationException(
                    $"The field type for '{definition.Path}' is not supported.");
        }
    }

    /// <summary>
    /// The canonical wire shape of the damage impacts. A caller holding typed
    /// impacts (the Case workspace save) writes them through here so the
    /// stored JSON has exactly one owner.
    /// </summary>
    public static string SerializeImpacts(IReadOnlyList<AssessmentImpact> impacts)
    {
        ArgumentNullException.ThrowIfNull(impacts);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var impact in impacts)
            {
                ArgumentNullException.ThrowIfNull(impact);
                writer.WriteStartObject();
                writer.WriteString("zone", impact.Zone);
                writer.WriteString("severity", impact.Severity);
                writer.WriteString("note", impact.Note);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string NormalizeImpacts(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var normalized = SerializeImpacts(ReadImpacts(document.RootElement));
            if (normalized.Length > AssessmentVocabulary.Definitions[AssessmentVocabulary.DamageImpacts].MaximumLength)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The canonical damage impacts cannot exceed 4000 characters.");
            }
            return normalized;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The damage impacts must be a valid JSON array.", nameof(value), exception);
        }
    }

    private static List<AssessmentImpact> ReadImpacts(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("The damage impacts must be a JSON array.", nameof(root));
        }
        var result = new List<AssessmentImpact>();
        var zones = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || element.EnumerateObject().Select(property => property.Name).Order().SequenceEqual(["note", "severity", "zone"]) is false
                || !element.TryGetProperty("zone", out var zoneElement)
                || !element.TryGetProperty("severity", out var severityElement)
                || !element.TryGetProperty("note", out var noteElement)
                || zoneElement.ValueKind != JsonValueKind.String
                || severityElement.ValueKind != JsonValueKind.String
                || noteElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Each damage impact must contain exactly string zone, severity, and note members.", nameof(root));
            }
            var zone = zoneElement.GetString()!;
            var severity = severityElement.GetString()!;
            var note = noteElement.GetString()!.Trim();
            if (!AssessmentVocabulary.DamageZones.ContainsKey(zone) || !zones.Add(zone))
            {
                throw new ArgumentException("Damage impact zones must be accepted and unique.", nameof(root));
            }
            if (!AssessmentVocabulary.DamageSeverities.ContainsKey(severity))
            {
                throw new ArgumentException("A damage impact severity is not accepted.", nameof(root));
            }
            if (note.Length > 200 || note.Any(char.IsControl))
            {
                throw new ArgumentException("A damage impact note cannot exceed 200 characters or contain control characters.", nameof(root));
            }
            result.Add(new(zone, severity, note));
        }
        return result;
    }

    private static List<EstimateLineInput> NormalizeLines(
        IReadOnlyList<EstimateLineInput> lines)
    {
        if (lines.Count > MaximumEstimateLines)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lines),
                $"The estimate is bounded to {MaximumEstimateLines} lines.");
        }

        var normalized = new List<EstimateLineInput>(lines.Count);
        foreach (var line in lines)
        {
            ArgumentNullException.ThrowIfNull(line);
            if (!EstimateLineCodes.Types.Contains(line.Type?.Trim() ?? string.Empty, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    "Every estimate line requires a recognized line type.",
                    nameof(lines));
            }
            // Hours and row materials are estimate money: EstimatePolicy owns
            // that rule so a provider's own time precision is kept, not
            // rounded to the editor's step (B04).
            EstimatePolicy.ValidateLineAmounts(line);
            if (line.Quantity is { } quantity && quantity < 1)
            {
                throw new ArgumentException(
                    "An estimate line quantity must be at least one.",
                    nameof(lines));
            }
            if (line.Price is { } price && (price < 0 || decimal.Round(price, 2) != price))
            {
                throw new ArgumentException(
                    "An estimate line price must be a non-negative amount with at most two decimal places.",
                    nameof(lines));
            }
            if (line.Unpriced && line.Price is not null)
            {
                throw new ArgumentException(
                    "A line marked To be confirmed cannot also carry a price.",
                    nameof(lines));
            }

            var status = NormalizeCode(line.Status, EstimateLineCodes.Statuses, "status");
            var evidence = NormalizeCode(
                line.EvidenceLabel,
                EstimateLineCodes.EvidenceLabels,
                "evidence label");
            normalized.Add(line with
            {
                Type = line.Type!.Trim(),
                GuideCode = NormalizeText(line.GuideCode, 50, "guide code"),
                Description = NormalizeText(line.Description, 300, "description"),
                Status = status,
                EvidenceLabel = evidence,
                PartNumber = NormalizeText(line.PartNumber, 100, "part number"),
                Betterment = NormalizeText(line.Betterment, 100, "betterment"),
                Justification = NormalizeText(line.Justification, 500, "justification")
            });
        }

        return normalized;
    }

    public static IReadOnlyList<EstimateLineInput> NormalizeRepairSpecificationLines(
        IReadOnlyList<EstimateLineInput> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return NormalizeLines(lines);
    }

    private static string? NormalizeCode(
        string? value,
        IReadOnlyList<string> codes,
        string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return codes.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : throw new ArgumentException(
                $"An estimate line carries an unrecognized {description}.",
                nameof(value));
    }

    private static string? NormalizeText(string? value, int maximumLength, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentOutOfRangeException(
                nameof(value),
                $"An estimate line {description} cannot exceed {maximumLength} characters.");
    }
}
