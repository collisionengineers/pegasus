using System.Globalization;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Assessment;

/// <summary>
/// Single owner of assessment validation. The closed field vocabulary fails
/// closed on unknown or case-owned paths; values are canonicalized before
/// persistence; the required-when pairings from the screen's own hints are
/// enforced against the merged state; and the actor rules implement the
/// operator-decided direct-write model: a staff or Automation save records
/// the Case's value with its provenance, professional findings included
/// (operator, 7 October 2026: the Automation actor does anything a staff
/// member can). There is no per-field
/// review (operator, 25 September 2026): Review is a Case stage, and Hand to
/// Engineer is the review. Estimate derivation (totals, worklists) is
/// deliberately absent until its formulas hold accepted authority (EXT-09,
/// open decision D2).
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
        if (request.Fields.Count == 0)
        {
            throw new ArgumentException(
                "An assessment save requires at least one field.",
                nameof(request));
        }
        if (request.Fields.Count > MaximumFieldsPerSave)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                $"An assessment save is bounded to {MaximumFieldsPerSave} fields.");
        }

        var normalizedFields = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (path, rawValue) in request.Fields)
        {
            normalizedFields[path] = NormalizeWritableField(path, rawValue, request.Actor);
        }

        return request with { Fields = normalizedFields };
    }

    /// <summary>
    /// The one gate every generic field save passes: the path must be part of
    /// the vocabulary, must not be recorded by the vehicle lookup or the
    /// chosen guide card, and must not be owned by the accepted case
    /// record. The value is then canonicalized
    /// against its own definition. Both the assessment save and the Case
    /// workspace save call it, so an unwritable path fails the same way on
    /// either route.
    /// </summary>
    public static string? NormalizeWritableField(string path, string? rawValue, ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(actor);
        if (AssessmentVocabulary.LookupDerivedPaths.Contains(path))
        {
            throw new InvalidOperationException(
                $"The field '{path}' is filled by the DVLA/DVSA vehicle lookup and cannot be written directly.");
        }
        if (AssessmentVocabulary.GuideCardDerivedPaths.Contains(path))
        {
            throw new InvalidOperationException(
                $"The field '{path}' is the chosen guide card's figure and cannot be written directly; "
                + "choose the card on the Valuation section.");
        }
        if (AssessmentVocabulary.CaseOwnedPaths.Contains(path))
        {
            throw new InvalidOperationException(
                $"The field '{path}' is owned by the accepted case record; "
                + "save it through the case-detail edit path instead.");
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
    /// Whether an automated fill (the original-report extraction, the vehicle
    /// lookup's Vehicle type) lands on a cell: only where no one has recorded
    /// a value or another fill recorded it. A value staff or the Automation
    /// actor recorded on purpose is never overwritten (operator, 7 October
    /// 2026); a value a fill recorded takes the newer reading.
    /// </summary>
    public static bool FillLands(ActorKind? recordedByKind, string? recordedBy) =>
        recordedByKind is null
        || (recordedByKind == ActorKind.Automation
            && recordedBy is not null
            && SystemFillRecorders.Contains(recordedBy));

    private static readonly HashSet<string> SystemFillRecorders = new(StringComparer.Ordinal)
    {
        OriginalReportPrefillPolicy.RecorderId,
        VehicleLookupFillPolicy.RecorderId,
        GlassVinFillPolicy.RecorderId,
        Intake.ThirdPartyReports.VehicleCaptureFill.RecorderId,
        PrincipalDefaultFeePolicy.RecorderId,
    };

    public static void RequireOriginalReportScope(IEnumerable<string> paths, CaseType caseType)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (caseType != CaseType.Audit
            && paths.Any(path => path.StartsWith("original_report.", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Original report fields can be saved only on an Audit Case.");
        }
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

    /// <summary>The impacts staff entered (v46), in their order; nothing stored is none.</summary>
    public static IReadOnlyList<AssessmentImpact> ParseImpacts(string? value)
    {
        if (value is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(value);
        return ReadImpacts(document.RootElement);
    }

    /// <summary>
    /// Skip impacts stands only while no impact row exists (v47 items B and D,
    /// operator, 3 November 2026; R649): a save that would leave the skip
    /// ticked beside any row, complete or not, is refused, for staff and the
    /// Automation actor alike. <paramref name="merged"/> is the work's values
    /// after the save.
    /// </summary>
    public static void RequireSkipOnlyWithoutImpacts(IReadOnlyDictionary<string, string> merged)
    {
        ArgumentNullException.ThrowIfNull(merged);
        if (string.Equals(merged.GetValueOrDefault(AssessmentVocabulary.ImpactsSkipped), "true", StringComparison.Ordinal)
            && ParseImpacts(merged.GetValueOrDefault(AssessmentVocabulary.Impacts)).Count > 0)
        {
            throw new InvalidOperationException("Skip impacts can be ticked only while no impact is entered.");
        }
    }

    /// <summary>The damage markers drawn on the plan (v46: positions only); nothing stored is none.</summary>
    public static IReadOnlyList<DamageDisc> ParseMarkers(string? value)
    {
        if (value is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(value);
        return ReadMarkers(document.RootElement);
    }

    /// <summary>
    /// Complete the coupled decision writes before the merged-state guard and
    /// attributed field history run. An explicit outcome change takes priority
    /// over a stale sum still present in the form.
    /// </summary>
    public static void CompleteCoupledWrites(
        Dictionary<string, string?> writes,
        IReadOnlyDictionary<string, string> current)
    {
        ArgumentNullException.ThrowIfNull(writes);
        ArgumentNullException.ThrowIfNull(current);
        var leavingContract = string.Equals(
                current.GetValueOrDefault(AssessmentVocabulary.Outcome),
                "contract_repair", StringComparison.Ordinal)
            && writes.TryGetValue(AssessmentVocabulary.Outcome, out var requestedOutcome)
            && !string.Equals(requestedOutcome, "contract_repair", StringComparison.Ordinal);
        if (!leavingContract
            && writes.TryGetValue(AssessmentVocabulary.SettlementContractSum, out var sum)
            && decimal.TryParse(sum, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            && amount > 0)
        {
            writes[AssessmentVocabulary.Outcome] = "contract_repair";
        }

        if (writes.TryGetValue(AssessmentVocabulary.Outcome, out var outcome))
        {
            if (!string.Equals(outcome, "contract_repair", StringComparison.Ordinal)
                && (current.ContainsKey(AssessmentVocabulary.SettlementContractSum)
                    || writes.ContainsKey(AssessmentVocabulary.SettlementContractSum)))
            {
                writes[AssessmentVocabulary.SettlementContractSum] = null;
            }
            if (!string.Equals(outcome, "total_loss", StringComparison.Ordinal))
            {
                foreach (var path in SalvageOnlyPaths)
                {
                    if (current.ContainsKey(path) || writes.ContainsKey(path))
                    {
                        writes[path] = null;
                    }
                }
            }
        }
    }

    private static readonly string[] SalvageOnlyPaths =
    [
        AssessmentVocabulary.SalvageCategory,
        AssessmentVocabulary.SalvageValue,
    ];

    public static bool IsWritableState(CaseLifecycleState state) =>
        state is CaseLifecycleState.NotReady
            or CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation
            or CaseLifecycleState.ReportSent;

    /// <summary>
    /// The readiness rail: every requirement the screen names, with its
    /// source and resolution. A derived read model only — nothing here is a
    /// save-blocker, and no value is ever guessed.
    /// </summary>
    public static IReadOnlyList<AssessmentReadinessItem> EvaluateReadiness(
        CaseAssessmentProjection projection) => Evaluate(projection, includeReviewEntryRequirements: true);

    /// <summary>
    /// Assessment and report work still required after entry to Review:
    /// everything the report prints, including every Case fact it prints;
    /// entry to Review proves only instruction and image completeness. Only
    /// the vehicle make, model and year are left to <see cref="EvaluateReadiness"/>.
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

        void RequireField(string path, string requirement, string section, string? howToResolve = null)
        {
            if (!fields.ContainsKey(path))
            {
                items.Add(new(
                    requirement,
                    "Assessment record",
                    "No value is recorded.",
                    howToResolve ?? $"Record it on the {section} section.",
                    Field: path));
            }
        }

        void RequireCaseFact(
            bool recorded, string requirement, string field, string whyOutstanding, string howToResolve)
        {
            if (!recorded)
            {
                items.Add(new(requirement, "Case record", whyOutstanding, howToResolve, Field: field));
            }
        }

        var owned = projection.CaseOwned;
        if (includeReviewEntryRequirements)
        {
            RequireCaseFact(
                owned.Make is not null, "Vehicle make", CaseDataFieldNames.VehicleMake,
                "No confirmed make is recorded.",
                "Record it on the Vehicle section.");
            RequireCaseFact(
                owned.Model is not null, "Vehicle model", CaseDataFieldNames.VehicleModel,
                "No confirmed model is recorded.",
                "Record it on the Vehicle section.");
            RequireCaseFact(
                owned.Year is not null, "Vehicle year", CaseDataFieldNames.VehicleYear,
                "No confirmed year is recorded.",
                "Record it on the Vehicle section.");
        }

        // The Case facts the report prints (operator, 24 September 2026).
        // Entry to Review proves only instruction and image completeness, so
        // each is named here, where the preview, the Case page and generation
        // all look. The date instructions were received is the Case's received
        // date, which every Case has, so it is never named.
        RequireCaseFact(
            !string.IsNullOrWhiteSpace(owned.ClaimantName), "Claimant name", CaseDataFieldNames.ClaimantName,
            "The report prints the claimant's name and none is recorded.",
            "Record it on the Claim section.");
        // A one-off for a private customer has no claim number to print (R578).
        if (!owned.PrivateCustomer)
        {
            RequireCaseFact(
                !string.IsNullOrWhiteSpace(owned.ClaimNumber), "Claim reference", CaseDataFieldNames.ClaimNumber,
                "The report prints the claim reference as Your Ref and none is recorded.",
                "Record it on the Case details section.");
        }
        RequireCaseFact(
            owned.IncidentDate is not null, "Incident date", CaseDataFieldNames.IncidentDate,
            "The report prints the incident date and none is recorded.",
            "Record it on the Case details section.");
        RequireCaseFact(
            !string.IsNullOrWhiteSpace(owned.Registration), "Vehicle registration", CaseDataFieldNames.VehicleRegistration,
            "The report prints the registration and none is recorded.",
            "Record it on the Vehicle section.");
        if (owned.InspectionMode is null)
        {
            items.Add(new(
                "Inspection type", "Case record",
                "The report says how the vehicle was assessed and no inspection type is recorded.",
                "Choose Inspect at on the Inspection details section.",
                Field: CaseDataFieldNames.InspectionMode));
        }
        else if (owned.InspectionMode == nameof(CaseInspectionMode.PhysicalAddress)
            && string.IsNullOrWhiteSpace(owned.InspectionAddress))
        {
            items.Add(new(
                "Inspection address", "Case record",
                "The vehicle is inspected at a physical location and no address is recorded for the report to print.",
                "Record the address on the Inspection details section.",
                Field: CaseDataFieldNames.InspectionAddress));
        }
        RequireCaseFact(
            owned.InspectionDate is not null, "Inspection date", CaseDataFieldNames.InspectionDate,
            "The report says the damage was assessed on the Inspection date and none is recorded.",
            "Record it on the Inspection details section.");

        RequireField(AssessmentVocabulary.VehicleType, "Vehicle type", "Vehicle");
        RequireField(AssessmentVocabulary.VehicleCondition, "Pre-incident condition", "Vehicle");
        // An impact, or the deliberate Skip impacts (v47, operator, 3 November
        // 2026; R647–R649): the Incident narrative is not required, but leaving
        // it out is a choice, never an oversight. A row missing its severity or
        // location is not an impact.
        if (!ParseImpacts(fields.GetValueOrDefault(AssessmentVocabulary.Impacts)).Any(impact => impact.IsComplete)
            && !string.Equals(
                fields.GetValueOrDefault(AssessmentVocabulary.ImpactsSkipped), "true", StringComparison.Ordinal))
        {
            items.Add(new(
                "Impacts",
                "Assessment record",
                "No impact is entered.",
                "Enter an impact, or tick Skip impacts, on the Damage section.",
                Field: AssessmentVocabulary.Impacts));
        }
        // The report prints the retail, trade and Engineer's values side by
        // side, so each is named until entered. Retail and trade are the
        // chosen guide card's (operator, 8 October 2026), and an AI market
        // research card has no trade (R311); the Engineer's Value is its own
        // box on Valuation.
        RequireField(
            AssessmentVocabulary.ValueRetail, "Retail value", "Valuation",
            "Enter it on the Valuation section.");
        if (!string.Equals(
                fields.GetValueOrDefault(AssessmentVocabulary.ValueFromMarketResearch),
                "true",
                StringComparison.Ordinal))
        {
            RequireField(
                AssessmentVocabulary.ValueTrade, "Trade value", "Valuation",
                "Enter it on the Valuation section.");
        }
        RequireField(
            AssessmentVocabulary.ValueEngineer, "Engineer's Value", "Valuation",
            "Enter it on the Valuation section.");
        RequireField(AssessmentVocabulary.Outcome, "Assessment outcome", "Decisions");
        RequireField(AssessmentVocabulary.LegalStatus, "Roadworthiness", "Decisions");
        RequireField(
            AssessmentVocabulary.HistoryCheck, "Vehicle history check", "Vehicle",
            "Record the vehicle history on the Vehicle section.");
        // The Engineer name, qualifications and signature readiness
        // items are retired (D18). The signing Engineer is the selected
        // sign-off account, whose printed name, qualifications and signature
        // come from that account, so typed copies of them were three ways to
        // record the same three facts.
        RequireField(
            AssessmentVocabulary.AgreedFee, "Agreed fee", "Report",
            "Record it on the Fee tab of the Report section.");

        // No odometer readiness item: the report's mileage-source code is
        // derived from the mileage's own provenance, so a case with no mileage
        // reads "tbc" by construction and the report says it is to be
        // confirmed. The former item could only fire when staff recorded a
        // source for a mileage they had not entered, which is no longer
        // possible.

        if (fields.TryGetValue(AssessmentVocabulary.LegalStatus, out var legalStatus)
            && string.Equals(legalStatus, "unroadworthy", StringComparison.Ordinal)
            && !fields.ContainsKey(AssessmentVocabulary.UnroadworthyReason))
        {
            items.Add(new(
                "Unroadworthy reason",
                "Assessment record",
                "The vehicle is recorded as unroadworthy without a reason.",
                "Record the reason on the Decisions section.",
                Field: AssessmentVocabulary.UnroadworthyReason));
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
                    "Record it on the Decisions section.",
                    Field: AssessmentVocabulary.SalvageCategory));
            }
            if (!fields.ContainsKey(AssessmentVocabulary.SalvageValue))
            {
                items.Add(new(
                    "Salvage value",
                    "Assessment record",
                    "The outcome is Total loss without a salvage value.",
                    "Record it on the Decisions section.",
                    Field: AssessmentVocabulary.SalvageValue));
            }
        }

        if (string.Equals(outcome, "contract_repair", StringComparison.Ordinal)
            && !fields.ContainsKey(AssessmentVocabulary.SettlementContractSum))
        {
            items.Add(new(
                "Agreed contract sum",
                "Assessment record",
                "The outcome is Contract repair without an agreed contract sum.",
                "Record the agreed contract sum on the Decisions section and save it.",
                Field: AssessmentVocabulary.SettlementContractSum));
        }

        // A recorded value is the Case's value whoever recorded it (operator,
        // 25 September 2026): nothing here names a field because of who
        // recorded it. Each requirement above identifies its own material,
        // reason and permitted resolution; a single aggregate count is
        // prohibited.
        return items;
    }

    /// <summary>
    /// A vehicle identification number as ISO 3779 writes it: seventeen
    /// characters, digits and the letters A–Z except I, O and Q (never used,
    /// to avoid confusion with 1 and 0). Spaces are removed and letters
    /// upper-cased before the check, so a VIN typed in groups is accepted.
    /// </summary>
    public static string Vin(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var canonical = string.Concat(value.Where(character => !char.IsWhiteSpace(character)))
            .ToUpperInvariant();
        if (canonical.Length != 17
            || canonical.Any(character => !char.IsAsciiDigit(character)
                && !(char.IsAsciiLetterUpper(character) && character is not ('I' or 'O' or 'Q'))))
        {
            throw new ArgumentException(
                "A VIN is 17 characters: digits and the letters A–Z except I, O and Q.",
                nameof(value));
        }

        return canonical;
    }

    /// <summary>
    /// A derived field's value as its own definition canonicalizes it: the
    /// section that owns the derivation writes it, so it skips the field
    /// gate's derived-path refusal but not the value checks.
    /// </summary>
    public static string? NormalizeDerivedField(string path, string? rawValue) =>
        NormalizeValue(AssessmentVocabulary.Definitions[path], rawValue);

    private static string? NormalizeValue(AssessmentFieldDefinition definition, string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var value = rawValue.Trim();
        if (definition.Path == AssessmentVocabulary.VehicleVin)
        {
            return Vin(value);
        }

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
                        $"The value for '{definition.Path}' is not one of its accepted codes: "
                        + string.Join(", ", definition.Codes ?? []) + ".",
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
                if (!DateOnly.TryParseExact(
                        value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    || date == DateOnly.MinValue)
                {
                    throw new ArgumentException(
                        $"The value for '{definition.Path}' must be a yyyy-MM-dd date.",
                        nameof(rawValue));
                }
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            case AssessmentFieldType.Json:
                return NormalizeJson(definition, value);

            default:
                throw new InvalidOperationException(
                    $"The field type for '{definition.Path}' is not supported.");
        }
    }

    /// <summary>
    /// The canonical wire shape of the impacts staff enter (v46). A row with
    /// nothing chosen or written is dropped; a row naming only some of its
    /// members is kept as entered.
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
                if (impact.Severity.Length == 0 && impact.Location.Length == 0 && impact.Note.Length == 0)
                {
                    continue;
                }
                writer.WriteStartObject();
                writer.WriteString("severity", impact.Severity);
                writer.WriteString("location", impact.Location);
                writer.WriteString("note", impact.Note);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// The canonical wire shape of the damage markers. A caller holding typed
    /// markers (the Case workspace save) writes them through here so the
    /// stored JSON has exactly one owner.
    /// </summary>
    public static string SerializeMarkers(IReadOnlyList<DamageDisc> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var marker in markers)
            {
                ArgumentNullException.ThrowIfNull(marker);
                writer.WriteStartObject();
                writer.WriteNumber("x", RoundDisc(marker.CentreX));
                writer.WriteNumber("y", RoundDisc(marker.CentreY));
                writer.WriteNumber("r", RoundDisc(marker.Radius));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string NormalizeJson(AssessmentFieldDefinition definition, string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var normalized = definition.Path switch
            {
                AssessmentVocabulary.Impacts => SerializeImpacts(ReadImpacts(document.RootElement)),
                AssessmentVocabulary.DamageMarkers => SerializeMarkers(ReadMarkers(document.RootElement)),
                _ => throw new InvalidOperationException($"The field '{definition.Path}' has no JSON reader."),
            };
            if (normalized.Length > definition.MaximumLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), $"The canonical value of '{definition.Path}' cannot exceed {definition.MaximumLength} characters.");
            }
            return normalized;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"The value for '{definition.Path}' must be a valid JSON array.", nameof(value), exception);
        }
    }

    private static List<AssessmentImpact> ReadImpacts(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("The impacts must be a JSON array.", nameof(root));
        }
        var result = new List<AssessmentImpact>();
        foreach (var element in root.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                    .SequenceEqual(["location", "note", "severity"])
                || element.GetProperty("severity").ValueKind != JsonValueKind.String
                || element.GetProperty("location").ValueKind != JsonValueKind.String
                || element.GetProperty("note").ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Each impact must contain exactly string severity, location and note members.", nameof(root));
            }
            var severity = element.GetProperty("severity").GetString()!.Trim();
            var location = element.GetProperty("location").GetString()!.Trim();
            var note = element.GetProperty("note").GetString()!.Trim();
            if (severity.Length > 0 && !AssessmentVocabulary.DamageSeverities.ContainsKey(severity))
            {
                throw new ArgumentException("An impact severity is not accepted.", nameof(root));
            }
            if (location.Length > 0 && !AssessmentVocabulary.DamageAreas.ContainsKey(location))
            {
                throw new ArgumentException("An impact location is not accepted.", nameof(root));
            }
            if (note.Length > 200 || note.Any(char.IsControl))
            {
                throw new ArgumentException("An impact note cannot exceed 200 characters or contain control characters.", nameof(root));
            }
            if (severity.Length == 0 && location.Length == 0 && note.Length == 0)
            {
                continue;
            }
            result.Add(new(severity, location, note));
        }
        return result;
    }

    private static List<DamageDisc> ReadMarkers(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("The damage markers must be a JSON array.", nameof(root));
        }
        return [.. root.EnumerateArray().Select(ReadDisc)];
    }

    /// <summary>
    /// A drawn disc in unit-plan terms, rounded as it is stored: the centre
    /// on the plan and the radius between the smallest and the largest disc
    /// the plan allows.
    /// </summary>
    private static DamageDisc ReadDisc(JsonElement element)
    {
        static bool Number(JsonElement disc, string name, out double value)
        {
            value = 0;
            return disc.GetProperty(name).ValueKind == JsonValueKind.Number
                && disc.GetProperty(name).TryGetDouble(out value);
        }

        if (element.ValueKind != JsonValueKind.Object
            || element.EnumerateObject().Select(property => property.Name).Order().SequenceEqual(["r", "x", "y"]) is false
            || !Number(element, "x", out var x)
            || !Number(element, "y", out var y)
            || !Number(element, "r", out var r))
        {
            throw new ArgumentException("A damage disc must contain exactly numeric x, y and r members.", nameof(element));
        }
        (x, y, r) = (RoundDisc(x), RoundDisc(y), RoundDisc(r));
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(r)
            || x is < 0 or > 1 || y is < 0 or > 1
            || r < RoundDisc(DamageAreaGeometry.MinRadius) || r > DamageAreaGeometry.MaxRadius)
        {
            throw new ArgumentException("A damage disc must lie on the plan and be no smaller or larger than the plan allows.", nameof(element));
        }
        return new(x, y, r);
    }

    private static double RoundDisc(double value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

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
            var evidence = NormalizeEvidenceLabel(line.EvidenceLabel);
            normalized.Add(line with
            {
                Type = line.Type!.Trim(),
                // A priced line is not To be confirmed: the price wins, as
                // pricing a To be confirmed line on the Case does.
                Unpriced = line.Unpriced && line.Price is null,
                GuideCode = NormalizeText(line.GuideCode, 50, "guide code"),
                Description = NormalizeText(line.Description, 300, "description"),
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

    private static string? NormalizeEvidenceLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return EstimateLineCodes.EvidenceLabels.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : throw new ArgumentException(
                $"An estimate line carries an unrecognized evidence label '{normalized}'; use one of: "
                + string.Join(", ", EstimateLineCodes.EvidenceLabels)
                + ". Source text belongs in the line's justification.",
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
