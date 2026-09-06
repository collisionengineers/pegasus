using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The top-15 instruction profiles against their own immutable originals, read
/// through the real reader, selected by the real document-profile selector and
/// extracted by the real policy.
///
/// One row per original. A batch that adds a profile adds ROWS here — its
/// samples, their recorded hashes, the identity an independent labeller read
/// off the document, and the neighbouring values that must never be mistaken
/// for it. It does not add a test.
///
/// What this asserts, and deliberately no more:
///
/// <list type="number">
/// <item>Every cited original resolves under the reference pack and hashes to
/// what the pack records. An expectation about bytes nobody has is not
/// evidence.</item>
/// <item>The document, and nothing about how it arrived, selects the profile
/// the labeller assigned it.</item>
/// <item>Zero WRONG identity. Where the labeller read a claimant, a
/// reference, a registration or a date off the original, extraction either
/// agrees or has nothing to say; it never confidently says something else.
/// That is the acceptance gate each method file proposes.</item>
/// <item>No neighbouring party's, address's or date's value ever arrives as
/// the claimant's identity.</item>
/// </list>
///
/// What it deliberately does NOT assert is a coverage floor. Five samples per
/// principal prove examples, not production accuracy, and the implementation
/// plan is explicit that no accuracy threshold may be claimed without
/// operator-labelled holdouts. So recall, ambiguity and missing counts are
/// MEASURED and written to
/// <c>artifacts/evaluation/v1-intake/top15-instruction-corpus.md</c> as a
/// per-profile, per-field matrix for the owner to read, rather than being
/// turned into a number a passing test would imply had been accepted.
///
/// A sample that cannot be read completely is recorded INCONCLUSIVE with its
/// reason. Inconclusive is not a pass and is never counted as one.
/// </summary>
[Trait("Category", "Corpus")]
public sealed class Top15InstructionCorpusTests
{
    private static readonly DateTimeOffset ProcessedAtUtc =
        new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The least text a read must recover before its result means anything.
    /// Every original in this corpus is a page or more of correspondence; a
    /// couple of hundred characters is a floor no genuine one is near.
    /// </summary>
    private const int MinimumRecoveredCharacters = 200;

    /// <summary>
    /// The identity an independent labeller read off one original, in the
    /// canonical form the pipeline produces: a name and a reference as
    /// printed, a registration with its spacing removed, dates as dates.
    /// A null means the labeller recorded the field as absent or ambiguous —
    /// nothing is asserted about it beyond the negatives below.
    /// </summary>
    private sealed record ExpectedIdentity(
        string? ClaimantName,
        string? ClaimNumber,
        string? VehicleRegistration,
        DateOnly? DateOfIncident,
        DateOnly? InstructionDate);

    /// <summary>
    /// A value printed on the original under a DIFFERENT label, and the field
    /// it must never arrive as. <c>Field</c> null means it must not arrive as
    /// any of the identity fields.
    /// </summary>
    private sealed record NeighbouringValue(string? Field, string Value, string Why);

    private sealed record SampleExpectation(
        string Profile,
        string PackRelativePath,
        string Sha256,
        ExpectedIdentity Identity,
        NeighbouringValue[] Negatives);

    private const string CorpusRoot = "principal-docs/original-mapper-instruction-corpus";

    /// <summary>
    /// Batch 1: QDOS and PCH. The remaining thirteen profiles' rows are added
    /// by their own batches, against the same four assertions.
    ///
    /// The QDOS identity comes from each letter's clean page-one header. Three
    /// of the five originals have genuine source-level row shift in their
    /// details table — labels and values zippered from different rows,
    /// confirmed against the rendered pages — so the labeller used the header
    /// where it gave an unambiguous corroborating value and recorded the rest
    /// ambiguous rather than reassigning it. The scrambled rows are the
    /// negatives.
    /// </summary>
    private static readonly SampleExpectation[] Expectations =
    [
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 01.pdf",
            "21ad661ea450a7d05a082da8742f5ea0d6bb6917db5b5b290ab2dabe78c04ede",
            new("Ms Angela Feetham", "LR/ND/45143/1", "NG22FVH", new(2026, 5, 2), new(2026, 5, 6)),
            [
                new("Vehicle registration", "NJ63YOF", "TP Registration: the third party's."),
                new("Claimant name", "Ageas Insurance Limited", "TP Representative Name."),
                new(
                    "Vehicle description",
                    "NISSAN X-TRAIL TEKNA DCI",
                    "TP Vehicle: the third party's vehicle."),
                new(
                    "Accident circumstances",
                    "Damage Area",
                    "Damage is not an account of how the accident happened.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 02.pdf",
            "854ad91f463010780fc91e9c08961546d05c5c6f29cdf8320ae0fdad52d94d91",
            new("Mr Timothy Lewis", "AKH/ND/45078/1", "GO13UCS", new(2026, 5, 1), new(2026, 5, 6)),
            [
                new("Vehicle registration", "RA75OZP", "TP Registration."),
                new(
                    "Vehicle description",
                    "VOLVO XC40 PLUS PRO B4 MHEV AUTO",
                    "TP Vehicle."),
                new("Claimant name", "AIG UK LTD", "TP Representative Name."),
                new(
                    "Accident circumstances",
                    "Damage Area",
                    "Damage is not an account of how the accident happened.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 03.pdf",
            "f6961bc33ec46f3e6312f9818f12b1b116a1645407f7e342c0d41806be88efef",
            // The reference row prints "MW/45101/1"; the details table is row
            // shifted and the header is the ground truth.
            new("Mr Andrew Adams", "MW/45101/1", "PY07FWD", new(2026, 5, 2), new(2026, 5, 6)),
            [
                new(
                    "Vehicle registration",
                    "Wear and tear",
                    "The row-shifted TP Registration slot; not a registration despite the label."),
                new(
                    "Vehicle description",
                    "KIA SPORTAGE KX-1 CRDI",
                    "A TP vehicle description in the TP Representative slot."),
                new(null, "Undriveable", "Vehicle status, not an identity or a circumstance.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 04.pdf",
            "c78c2dcf87c3f2949cbe446840cbaa17cd54098b89b9972a43a84309d2cdc56b",
            new("Mr Thomas Wilson", "MW/45117/1", "CK62TXA", new(2026, 5, 3), new(2026, 5, 6)),
            [
                new("Vehicle description", "VAUXHALL CORSA SPORT", "A TP vehicle description."),
                new(null, "Undriveable", "Vehicle status, not an identity or a circumstance.")
            ]),
        new(
            "QDOS",
            $"{CorpusRoot}/QDOS 05.pdf",
            "080bec20fd211188ca8e19404ff518ed8396c348e381c6d2a2fda53fd0f0af94",
            // "Our Ref" prints as "/45160/1" with no initials prefix, unlike
            // every other original. Flagged by the labeller rather than
            // corrected, so nothing is asserted about the reference here.
            new("Mr Jamie Elder", null, "FD70ONU", new(2026, 5, 2), new(2026, 5, 6)),
            [
                new(
                    "Vehicle description",
                    "PEUGEOT EXPERT S STANDARD BLUE HDI",
                    "A TP vehicle description."),
                new(
                    "Inspection address",
                    "Gordon Marshall Coachworks",
                    "A repairer address does not prove a physical inspection.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 01.DOC",
            "87181b81f0fd3c59001178be782bcdfdb0efd504bb311491329d50896dbb94a4",
            new("Mrs Adam Bielecka", "573942", "VN20XFC", new(2026, 3, 31), new(2026, 5, 6)),
            [
                new("Claim number", "MRPC0103479703-LS", "Insurer Policy No, a different party's."),
                new("Claimant name", "Hannah Hammill", "The sender of the instruction message."),
                new(null, "01/04/2026", "Hire Out Date: when a replacement car was supplied."),
                new(
                    "Claimant address",
                    "1210 Centre Park Square",
                    "The supplier's footer address.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 02.DOC",
            "66f29af7613f63cc6c8ce13286db9905e6456574ebdb8aa9308806ca494746bd",
            new("Ms Angela Abdallah", "573425", "XS02ANG", new(2026, 3, 20), new(2026, 5, 6)),
            [
                new("Claim number", "MS1000743098Y0", "Insurer Policy No."),
                new("Claimant name", "Hannah Hammill", "The sender of the instruction message."),
                new(null, "23/03/2026", "Hire Out Date.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 03.DOC",
            "e9242909d2e4be91e35a4c90ada904f2d832fca3e126d2ea224bc1d0cc4d6a27",
            new("Mr Daniel Broome", "572566", "BD69NJY", new(2026, 3, 4), new(2026, 5, 6)),
            [
                // The clearest evidence in the corpus that driver and claimant
                // are two roles: two different people, one surname.
                new("Claimant name", "Mrs Nicky Broome", "Driver: a separate labelled role."),
                new("Claim number", "P68716723-1", "Insurer Policy No."),
                new(
                    "Inspection address",
                    "in use",
                    "A statement about whether the car is driven, not a place.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 04.DOC",
            "a2fa3a75cb7aee3fcb634692dc8abc3cdebd56fa7be7fb5471f62439d1aeb80c",
            // A corporate claimant: the field must accept a company name.
            new("Westons Group Ltd", "574289", "BD22GZW", new(2026, 3, 3), new(2026, 5, 6)),
            [
                new(
                    "Claimant name",
                    "Miss Carolann Hughes",
                    "The driver of the corporate claimant's vehicle."),
                new("Claim number", "NM050028493", "Insurer Policy No.")
            ]),
        new(
            "PCH",
            $"{CorpusRoot}/PCH 05.DOC",
            "e5e6a84abe062fc5130d77b950a7fb5465f46560eb99879fc9171f0e4bafca05",
            // One day earlier than the other four: a genuine variable field.
            new("Mr Junior Cover", "573923", "JR07CVR", new(2026, 3, 31), new(2026, 5, 5)),
            [
                new("Claim number", "LN92101512821", "Insurer Policy No."),
                new(null, "02/04/2026", "Hire Out Date.")
            ])
    ];

    private static IInstructionExtractionPolicy[] Policies() =>
        [new QdosInstructionExtractionPolicy(), new PchInstructionExtractionPolicy()];

    [ReferencePackFact]
    public async Task EveryLabelledOriginalSelectsItsProfileAndMisidentifiesNothing()
    {
        var root = PackRoot();
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var selector = new InstructionExtractionPolicySelector(Policies());
        var report = new StringBuilder()
            .AppendLine("# Top-15 instruction corpus: per-profile, per-field matrix")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"Pack root read from `{PackRootVariable}`.")
            .AppendLine(
                "Recall and ambiguity are MEASURED here, not asserted: five samples per "
                + "principal prove examples, not production accuracy, and no accuracy "
                + "threshold is claimed without operator-labelled holdouts.")
            .AppendLine();

        var readable = 0;
        var inconclusive = new List<string>();
        var failures = new List<string>();
        var counts = new Dictionary<(string Profile, string Field, string Disposition), int>();

        foreach (var expectation in Expectations)
        {
            var absolute = Path.Combine(
                root, expectation.PackRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var name = Path.GetFileName(expectation.PackRelativePath);
            if (!File.Exists(absolute))
            {
                failures.Add($"{name}: the pack does not carry this original.");
                continue;
            }

            var sha256 = Sha256Of(absolute);
            if (!string.Equals(sha256, expectation.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(
                    $"{name}: hashes to {sha256}, and the pack records {expectation.Sha256}.");
                continue;
            }

            var readResult = await reader.ReadAsync(
                Source(absolute, name, sha256), CancellationToken.None);
            if (readResult.Status != IntakeSourceReadStatus.Readable || readResult.IsIncomplete)
            {
                inconclusive.Add(
                    $"{name}: {readResult.FailureReason ?? "the reader returned incomplete content"}"
                    + " - INCONCLUSIVE, which is not a pass.");
                continue;
            }

            // A read that recovered almost nothing has given the profile
            // nothing to identify and the policy nothing to extract. That is a
            // reader gap, and calling it a misidentification would blame the
            // wrong thing - so it is recorded as inconclusive, by which nobody
            // is reassured.
            var recovered = readResult.Content.Sum(fragment => fragment.Text.Length);
            if (recovered < MinimumRecoveredCharacters)
            {
                inconclusive.Add(
                    $"{name}: the reader recovered {recovered} characters, below the "
                    + $"{MinimumRecoveredCharacters} this measurement needs - INCONCLUSIVE, "
                    + "which is not a pass.");
                continue;
            }

            readable++;
            var selection = selector.Select(
                readResult, InstructionDocumentSignature.InstructionRole);
            if (selection.Outcome != InstructionPolicySelectionOutcome.Selected
                || !string.Equals(
                    selection.Policy!.PrincipalCode, expectation.Profile, StringComparison.Ordinal))
            {
                failures.Add(
                    $"{name}: the document selected {Describe(selection)}, and the labeller "
                    + $"assigned it {expectation.Profile}.");
                continue;
            }

            var policy = selection.Policy!;
            var profile = (IInstructionDocumentProfile)policy;
            var result = policy.Extract(
                readResult,
                ProcessedAtUtc,
                new(policy.PrincipalCode, profile.DocumentProfileKey, profile.DocumentProfileVersion));

            AppendSample(report, name, sha256, expectation, selection, result, profile, policy);
            Count(counts, expectation.Profile, result);
            failures.AddRange(WrongIdentity(name, expectation, result));
            failures.AddRange(NeighbouringValuesThatArrived(name, expectation, result));
        }

        AppendMatrix(report, counts);
        if (inconclusive.Count > 0)
        {
            report.AppendLine().AppendLine("## Inconclusive").AppendLine();
            foreach (var line in inconclusive)
            {
                report.AppendLine(CultureInfo.InvariantCulture, $"- {line}");
            }
        }

        WriteReport(report.ToString());

        Assert.True(
            readable > 0,
            "No labelled original could be read completely, so nothing was measured.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The gate each method file proposes: zero WRONG accepted identity. A
    /// value the labeller did not read is a miss and is measured above; a
    /// DIFFERENT value confidently accepted is a failure.
    /// </summary>
    private static IEnumerable<string> WrongIdentity(
        string name,
        SampleExpectation expectation,
        InstructionExtractionResult result)
    {
        var draft = result.InstructionDraft;
        if (draft is null)
        {
            yield return $"{name}: the policy produced no draft at all.";
            yield break;
        }

        foreach (var wrong in new[]
        {
            Compare(name, "claimant", expectation.Identity.ClaimantName, draft.ClaimantName),
            Compare(name, "reference", expectation.Identity.ClaimNumber, draft.ClaimNumber),
            Compare(
                name,
                "registration",
                expectation.Identity.VehicleRegistration,
                draft.VehicleRegistration),
            Compare(
                name,
                "incident date",
                expectation.Identity.DateOfIncident?.ToString("O", CultureInfo.InvariantCulture),
                draft.DateOfIncident?.ToString("O", CultureInfo.InvariantCulture)),
            Compare(
                name,
                "instruction date",
                expectation.Identity.InstructionDate?.ToString("O", CultureInfo.InvariantCulture),
                draft.InstructionDate?.ToString("O", CultureInfo.InvariantCulture))
        })
        {
            if (wrong is not null)
            {
                yield return wrong;
            }
        }
    }

    private static string? Compare(string name, string field, string? expected, string? actual) =>
        expected is null || actual is null || string.Equals(expected, actual, StringComparison.Ordinal)
            ? null
            : $"{name}: the {field} extracted as '{actual}', and the labelled value is '{expected}'.";

    private static IEnumerable<string> NeighbouringValuesThatArrived(
        string name,
        SampleExpectation expectation,
        InstructionExtractionResult result)
    {
        foreach (var negative in expectation.Negatives)
        {
            foreach (var field in result.Fields)
            {
                if (negative.Field is not null
                    && !string.Equals(field.Name, negative.Field, StringComparison.Ordinal))
                {
                    continue;
                }

                if (negative.Field is null && !IdentityFields.Contains(field.Name))
                {
                    continue;
                }

                if (field.SuggestedValue is { } value
                    && value.Contains(negative.Value, StringComparison.OrdinalIgnoreCase))
                {
                    yield return
                        $"{name}: '{field.Name}' carries '{negative.Value}'. {negative.Why}";
                }
            }
        }
    }

    private static readonly HashSet<string> IdentityFields = new(StringComparer.Ordinal)
    {
        "Claimant name",
        "Claim number",
        "Vehicle registration",
        "Vehicle description",
        "Vehicle make",
        "Vehicle model",
        "Date of incident",
        "Instruction date",
        "Accident circumstances"
    };

    /// <summary>
    /// The serialized candidates for one original: field, normalized value,
    /// raw value, party and reference role, document role, source hash,
    /// occurrence, the page, cell, form field or region the reader reported,
    /// the policy key and version, and the disposition.
    /// </summary>
    private static void AppendSample(
        StringBuilder report,
        string name,
        string sha256,
        SampleExpectation expectation,
        InstructionPolicySelection selection,
        InstructionExtractionResult result,
        IInstructionDocumentProfile profile,
        IInstructionExtractionPolicy policy)
    {
        var roles = policy as IInstructionFieldRoles;
        report.AppendLine(CultureInfo.InvariantCulture, $"## {expectation.Profile} — {name}")
            .AppendLine()
            .AppendLine(CultureInfo.InvariantCulture, $"SHA-256 `{sha256}`.")
            .AppendLine(
                CultureInfo.InvariantCulture,
                $"Selected `{profile.DocumentProfileKey}` v{profile.DocumentProfileVersion}; "
                + $"matched template variants: {Variants(selection)}.")
            .AppendLine()
            .AppendLine(
                "| Field | Normalized | Raw | Party role | Reference role | Document role "
                + "| Occurrence | Locator | Policy | Disposition |")
            .AppendLine("| --- | --- | --- | --- | --- | --- | ---: | --- | --- | --- |");

        foreach (var field in result.Fields)
        {
            var role = roles is not null && roles.FieldRoles.TryGetValue(field.Name, out var found)
                ? found
                : new InstructionFieldRole(null, null);
            if (field.Candidates.Count == 0)
            {
                report.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| {field.Name} | | | {role.PartyRole} | {role.ReferenceRole} "
                    + $"| {profile.Signature.DocumentRole} | 0 | not stated "
                    + $"| {result.PolicyKey} v{result.PolicyVersion} | Missing |");
                continue;
            }

            var disposition = field.HasConflict
                ? nameof(SourceCandidateDisposition.Ambiguous)
                : nameof(SourceCandidateDisposition.Usable);
            var occurrence = 0;
            foreach (var candidate in field.Candidates)
            {
                report.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"| {field.Name} | {Cell(field.HasConflict ? null : field.SuggestedValue)} "
                    + $"| {Cell(candidate.SourceValue)} | {role.PartyRole} | {role.ReferenceRole} "
                    + $"| {profile.Signature.DocumentRole} | {occurrence++} "
                    + $"| {Locator(candidate)} "
                    + $"| {result.PolicyKey} v{result.PolicyVersion} | {disposition} |");
            }
        }

        report.AppendLine();
    }

    /// <summary>
    /// The smallest useful layout locator the reader reported: its page, table
    /// cell, PDF form field, bounded region and message part where it has
    /// them, and the source label it always has.
    /// </summary>
    private static string Locator(InstructionFieldCandidate candidate)
    {
        var parts = new List<string> { Cell(candidate.SourceLabel) };
        var page = candidate.Locator?.Page
            ?? AnalyzeRetainedInstruction.PageFrom(candidate.SourceLabel);
        if (page is { } value)
        {
            parts.Add($"page {value}");
        }

        if (candidate.Locator is { } locator)
        {
            parts.Add($"kind {locator.Kind}");
            if (locator.Cell is { } cell)
            {
                parts.Add($"cell {cell}");
            }

            if (locator.FormField is { } formField)
            {
                parts.Add($"form field {formField}");
            }

            if (locator.Region is { } region)
            {
                parts.Add($"region {region}");
            }

            if (locator.MessagePart != IntakeMessagePart.None)
            {
                parts.Add($"message part {locator.MessagePart}");
            }
        }

        return string.Join("; ", parts);
    }

    private static void Count(
        Dictionary<(string, string, string), int> counts,
        string profile,
        InstructionExtractionResult result)
    {
        foreach (var field in result.Fields)
        {
            var disposition = field.Candidates.Count == 0
                ? nameof(SourceCandidateDisposition.Missing)
                : field.HasConflict
                    ? nameof(SourceCandidateDisposition.Ambiguous)
                    : nameof(SourceCandidateDisposition.Usable);
            var key = (profile, field.Name, disposition);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
    }

    private static void AppendMatrix(
        StringBuilder report,
        Dictionary<(string Profile, string Field, string Disposition), int> counts)
    {
        report.AppendLine("## Measured coverage")
            .AppendLine()
            .AppendLine("| Profile | Field | Usable | Ambiguous | Missing |")
            .AppendLine("| --- | --- | ---: | ---: | ---: |");
        foreach (var group in counts.Keys
            .Select(key => (key.Profile, key.Field))
            .Distinct()
            .OrderBy(key => key.Profile, StringComparer.Ordinal)
            .ThenBy(key => key.Field, StringComparer.Ordinal))
        {
            report.AppendLine(
                CultureInfo.InvariantCulture,
                $"| {group.Profile} | {group.Field} "
                + $"| {counts.GetValueOrDefault((group.Profile, group.Field, nameof(SourceCandidateDisposition.Usable)))} "
                + $"| {counts.GetValueOrDefault((group.Profile, group.Field, nameof(SourceCandidateDisposition.Ambiguous)))} "
                + $"| {counts.GetValueOrDefault((group.Profile, group.Field, nameof(SourceCandidateDisposition.Missing)))} |");
        }
    }

    private static string Describe(InstructionPolicySelection selection) => selection.Outcome switch
    {
        InstructionPolicySelectionOutcome.Selected => selection.Policy!.PrincipalCode,
        InstructionPolicySelectionOutcome.Ambiguous =>
            $"ambiguously {string.Join(", ", selection.Matches.Select(item => item.PrincipalCode))}",
        _ => "no profile"
    };

    private static string Variants(InstructionPolicySelection selection) =>
        selection.MatchedVariantKeys.Count == 0
            ? "none recorded"
            : string.Join(", ", selection.MatchedVariantKeys)
                + (selection.HasAmbiguousVariant ? " (ambiguous)" : string.Empty);

    /// <summary>Table cells: the pipes and newlines a value may carry cannot break the row.</summary>
    private static string Cell(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private static IntakeSource Source(string path, string name, string sha256) =>
        new(
            name,
            MediaType(name),
            File.ReadAllBytes(path),
            ProcessedAtUtc,
            "top15-instruction-corpus",
            new(IntakeSourceChannel.ManualUpload, $"corpus-{sha256[..12]}"));

    private static string MediaType(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".msg" => "application/vnd.ms-outlook",
            ".eml" => "message/rfc822",
            var other => throw new InvalidOperationException(
                $"The corpus carries an original this test has no media type for: '{other}'.")
        };

    private const string PackRootVariable = PrincipalSourceManifestTests.PackRootVariable;

    private static string PackRoot() =>
        PrincipalSourceManifestTests.ConfiguredPackRoot()
        ?? throw new InvalidOperationException(
            $"{PackRootVariable} is not set; this test should have been skipped.");

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void WriteReport(string content)
    {
        var directory = Path.Combine(
            FindRepositoryRoot(), "artifacts", "evaluation", "v1-intake");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "top15-instruction-corpus.md"),
            content,
            new UTF8Encoding(false));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Repository root not found.");
    }
}
