using System.Globalization;

namespace Pegasus.Core.Eva;

/// <summary>
/// EVA's own accepted values for the instruction fields Pegasus does not hold
/// a case value for. Each is EVA's documented "not known" member, so sending
/// it states ignorance rather than inventing an answer.
/// </summary>
public static class EvaInstructionDefaults
{
    public const string NotKnown = "Not Known";

    /// <summary>
    /// EVA's cover-type enum member meaning "to be advised". Pegasus holds no
    /// cover type, and this is the one value that says so.
    /// </summary>
    public const string CoverTypeToBeAdvised = "TBA";
}

/// <summary>
/// Maps one case's thirteen exported values into EVA's instruction shape
/// (EXT-04).
///
/// This is deliberately a *second* mapping layered on the first, not a rival
/// to it. <see cref="CaseEvaMapping.MapForOperatorExport"/> stays the one
/// owner of what a case means — which value each field takes, how it is
/// normalised, and what provenance it carries. This type only renames those
/// settled values into EVA's field names and adds the fields EVA's own request
/// model requires. If the two ever disagree about a value, the export is
/// right.
///
/// Two decisions here came from the operator (2026-08-27) and are not
/// derivable from EVA's documentation:
///
/// 1. **The claimant name is sent as <c>InsName</c>.** EVA documents that
///    field as the insurer name; the operator's EVA instance uses it for the
///    claimant, and they own that answer. This displaces the work provider,
///    which has no other field of its own and so moves into the note.
/// 2. **The instruction date is not sent at all.** EVA sets it when the
///    instruction arrives, and for an API submission that instant *is* the
///    instruction date — sending the case's own value would overwrite a
///    truth with a guess at it.
///
/// Two values still have nowhere to go: EVA's instruction model documents no
/// inspection-date field and no mileage field. They travel as labelled lines
/// in <c>NotesStr</c>, where an assessor reads them and nothing is silently
/// dropped. Asking EVA for real fields is deferred to its own ticket — the
/// vendor's own documentation says the model "can be extended to include
/// additional fields on request" — rather than guessed at here.
/// </summary>
public static class CaseEvaApiMapping
{
    public const string MappingKey = "qdos-eva-api-instruction-mapping";
    public const int MappingVersion = 1;

    /// <summary>
    /// The exported values EVA's instruction model has no field for, in the
    /// order they are written into the note. Named once, here, so the note
    /// builder and its tests cannot drift.
    ///
    /// The work provider is here because the claimant name took
    /// <c>InsName</c>; inspection date and mileage are here because EVA has no
    /// field for either.
    /// </summary>
    private static readonly string[] NotedFields =
    [
        "Work Provider",
        "Inspection Date",
        "Mileage"
    ];

    public static EvaInstructionPayload Map(
        EvaReplayFields fields,
        string caseReference,
        string principalCode,
        EvaInstructionSettings settings,
        IReadOnlyList<EvaInstructionFile> files)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrWhiteSpace(caseReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(principalCode);

        // The export's own normalisation, reused rather than repeated: this is
        // what guarantees the API and the ZIP carry byte-identical values.
        var normalized = CaseEvaMapping.MapOfflineReplay(fields);

        return new(
            settings.RequestFrom,
            principalCode.Trim(),
            caseReference.Trim(),
            Text(normalized.Reference),
            Text(normalized.ClaimantName),
            Text(normalized.Vrm),
            Text(normalized.VehicleModel),
            ParseExportDate(normalized.IncidentDate),
            Text(normalized.AccidentCircumstances),
            Text(normalized.VatStatus),
            settings.InspectionType,
            EvaInstructionDefaults.CoverTypeToBeAdvised,
            EvaInstructionDefaults.NotKnown,
            EvaInstructionDefaults.NotKnown,
            settings.InstructionEmail,
            MapLocation(normalized.InspectionAddress),
            BuildNotes(normalized, principalCode),
            files);
    }

    /// <summary>
    /// The inspection address across EVA's named location fields.
    ///
    /// The export already resolves this to exactly six lines - five body
    /// lines then a postcode - so that resolution is reused verbatim and
    /// simply distributed across EVA's location fields.
    ///
    /// Five body lines into four fields means one field takes two. The
    /// fourth and fifth body lines are joined into County, because a fifth
    /// body line is rare and dropping it would silently lose part of an
    /// address; EVA is told slightly more than it asked for rather than
    /// less than the case holds.
    ///
    /// Name repeats the first line. EVA wants a descriptive location name
    /// and the case holds none, so the first line of the address is the
    /// most truthful thing available - better than inventing one, and
    /// better than leaving a required field empty. An image-based
    /// assessment arrives here as EVA's own literal, exactly as the
    /// drag-and-drop bundle sends it.
    /// </summary>
    private static EvaInspectionLocation MapLocation(string? inspectionAddress)
    {
        var lines = (inspectionAddress ?? string.Empty).Split('\n');
        return new(
            Line(lines, 0),
            Line(lines, 0),
            Line(lines, 1),
            Line(lines, 2),
            Join(Line(lines, 3), Line(lines, 4)),
            Line(lines, 5));
    }

    /// <summary>Two address lines as one, skipping whichever is absent.</summary>
    private static string Join(string first, string second) =>
        string.Join(
            ' ',
            new[] { first, second }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static string Line(string[] lines, int index) =>
        index < lines.Length ? lines[index].Trim() : string.Empty;

    /// <summary>
    /// The values EVA has no field for, written as one labelled line each.
    ///
    /// A value the case does not hold is omitted rather than written as an
    /// empty label, because a note reading "Mileage:" with nothing after it
    /// tells an assessor less than saying nothing. When the case holds none of
    /// them the note is empty and EVA receives an empty string, not a heading
    /// with no content.
    /// </summary>
    private static string BuildNotes(EvaReplayFields fields, string principalCode)
    {
        var values = new[]
        {
            NotableWorkProvider(fields.WorkProvider, principalCode),
            fields.InspectionDate,
            Mileage(fields)
        };

        return string.Join(
            '\n',
            NotedFields
                .Select((name, index) => (Name: name, Value: values[index]))
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => $"{item.Name}: {item.Value!.Trim()}"));
    }

    /// <summary>
    /// Mileage and its unit as one value, because they are meaningless apart
    /// and the case is required to save them together.
    /// </summary>
    /// <summary>
    /// The work provider, but only where it says something the Agent code
    /// does not.
    ///
    /// Since the claimant name took InsName, the work provider has no field of
    /// its own. Agent now carries the Principal the case was allocated to, and
    /// repeating that same value as a note line is noise, so it is named only
    /// where the case's own work-provider code differs from that Principal -
    /// which is the case actually worth an assessor reading.
    /// </summary>
    private static string? NotableWorkProvider(string? workProvider, string principalCode) =>
        string.IsNullOrWhiteSpace(workProvider)
        || workProvider.Trim().Equals(principalCode.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : workProvider;

    private static string? Mileage(EvaReplayFields fields)
    {
        if (string.IsNullOrWhiteSpace(fields.Mileage))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(fields.MileageUnit)
            ? fields.Mileage
            : $"{fields.Mileage} {fields.MileageUnit}";
    }

    /// <summary>
    /// An exported date back into a <see cref="DateOnly"/>. The export writes
    /// <c>dd/MM/yyyy</c>, so that is the only format accepted — parsing
    /// loosely here would let a differently-shaped value through to EVA and
    /// silently change what day it means.
    /// </summary>
    private static DateOnly? ParseExportDate(string? value) =>
        DateOnly.TryParseExact(
            value,
            "dd/MM/yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;

    private static string Text(string? value) => value?.Trim() ?? string.Empty;
}

/// <summary>
/// The three instruction values that are configuration rather than case data:
/// who Pegasus is to EVA, what kind of inspection this deployment requests,
/// and where EVA should send the instruction. None is derivable from a case,
/// and none may be guessed — a wrong <see cref="RequestFrom"/> is refused by
/// EVA with a 400.
/// </summary>
public sealed record EvaInstructionSettings(
    string RequestFrom,
    string InspectionType,
    string InstructionEmail);
