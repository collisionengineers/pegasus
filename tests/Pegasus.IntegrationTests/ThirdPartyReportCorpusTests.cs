using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Infrastructure.Intake;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The 29 third-party report originals, read through the real reader (INTK-031).
///
/// This is the test the rules are actually answerable to. The Core tests read
/// text the test itself wrote; this one reads the retained PDFs through
/// <see cref="MimeKitPdfPigOpenXmlIntakeSourceReader"/> — the production reader,
/// whose text engine is not the one that produced the pack's extracted text —
/// and asserts the family, the negative roles and the worked source values the
/// extraction review recorded.
///
/// It reads bytes, hashes and text. It copies no original, writes no corpus
/// content into any report, and never edits the pack.
/// </summary>
[Trait("Category", "Corpus")]
public sealed class ThirdPartyReportCorpusTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset ReceivedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// The recorded classification of every original: a family, an explicit
    /// negative role, or — for the two scan-only PDFs — no verdict at all. Taken
    /// from the extraction review, and asserted from the document's own printed
    /// evidence rather than from the folder each file happens to sit in.
    /// </summary>
    private static readonly Dictionary<string, string> Expected =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Report 00077570.pdf"] = "Connexus",
            ["Report 00077930.pdf"] = "Connexus",
            ["Report 00079220.pdf"] = "Connexus",
            ["2041739499656__EREHR91037_.pdf"] = "ExclusiveErehr",
            ["2063880554396__EREHR92682_.pdf"] = "ExclusiveErehr",
            ["2063917430326__EREHR92502_.pdf"] = "ExclusiveErehr",
            ["2065134549155__EREHR93004_.pdf"] = "ExclusiveErehr",
            ["2139090762105__EREHR96454_.pdf"] = "ExclusiveErehr",
            ["2158880476495__EREHR97577_.pdf"] = "ExclusiveErehr",
            ["2159872700713__EREHR97500_.pdf"] = "ExclusiveErehr",
            ["Bodyshopreport139273-V1.pdf"] = "EvaBodyshop",
            ["Bodyshopreport236502-V1-EVA-repairable.pdf"] = "EvaBodyshop",
            ["Bodyshopreport236502-V1-EVA-repairable2.pdf"] = "EvaBodyshop",
            ["Bodyshopreport236502-V1-EVA-repairable3.pdf"] = "EvaBodyshop",
            ["Bodyshopreport236502-V1-EVA-repairable4.pdf"] = "EvaBodyshop",
            ["Bodyshopreport236502-V1-EVA-repairableSupp1.pdf"] = "EvaBodyshop",
            ["1_Bodyshopreport1064150-V1-Laird-Repairable1.pdf"] = "Laird",
            ["2336321682865__Bodyshopreport-V1.pdf"] = "Laird",
            ["LairdRepairable1.pdf"] = "Laird",
            ["tpreportexample.pdf"] = "Laird",
            ["Bodyshopreport-V1.pdf"] = "Montgomery",
            ["MontgomeryRepairable1.pdf"] = "Montgomery",
            ["Bodyshopreport236502-V1-sPrintAssessors-repairable2.pdf"] = "SPrint",
            ["Bodyshopreport236502-V1-sPrintAssessors-repairable3.pdf"] = "SPrint",
            ["GGEstimate1.pdf"] = "Estimate",
            ["MotorCheck1.pdf"] = "VehicleHistory",
            ["eva-just-images.pdf"] = "ImageEvidence",
            ["JohnRBell1.pdf"] = "TextUnavailableRequiresOcr",
            ["TonBridgeAccidentRepair1.pdf"] = "TextUnavailableRequiresOcr"
        };

    /// <summary>
    /// The values the extraction review worked through by hand, each with the
    /// printed amount role it belongs to. These are the assertions that would
    /// catch a parser that classified a document correctly and then read the
    /// wrong number out of it.
    /// </summary>
    private static readonly (string File, string Field, string Role, string Value)[] WorkedValues =
    [
        ("Report 00077570.pdf", ThirdPartyReportFields.LabourAmount, "initial", "2394.25"),
        ("Report 00077570.pdf", ThirdPartyReportFields.LabourAmount, "agreed", "3351.95"),
        ("Report 00077570.pdf", ThirdPartyReportFields.LabourHours, "agreed", "35"),
        ("Report 00077570.pdf", ThirdPartyReportFields.LabourRate, "agreed", "95.77"),
        ("Report 00077570.pdf", ThirdPartyReportFields.VatAmount, "agreed", "1023.98"),
        ("Report 00077570.pdf", ThirdPartyReportFields.Gross, "agreed", "6143.90"),
        ("Report 00077570.pdf", ThirdPartyReportFields.Trade, "", "7233.00"),
        ("Report 00077570.pdf", ThirdPartyReportFields.Mid, "", "8250.00"),
        ("Report 00077570.pdf", ThirdPartyReportFields.Reserve, "", "6150.00"),
        ("2041739499656__EREHR91037_.pdf", ThirdPartyReportFields.LabourAmount, "agreed", "1464.00"),
        ("2041739499656__EREHR91037_.pdf", ThirdPartyReportFields.Net, "agreed", "5695.62"),
        ("2041739499656__EREHR91037_.pdf", ThirdPartyReportFields.Gross, "agreed", "6834.74"),
        ("LairdRepairable1.pdf", ThirdPartyReportFields.Revision, "", "Supplementary Report"),
        ("LairdRepairable1.pdf", ThirdPartyReportFields.LabourAmount, "supplement", "4064.06"),
        ("LairdRepairable1.pdf", ThirdPartyReportFields.Net, "supplement", "9891.39"),
        ("LairdRepairable1.pdf", ThirdPartyReportFields.Gross, "supplement", "11869.67"),
        ("MontgomeryRepairable1.pdf", ThirdPartyReportFields.LabourHours, "assessed", "26.20"),
        ("MontgomeryRepairable1.pdf", ThirdPartyReportFields.LabourRate, "assessed", "90.00"),
        ("MontgomeryRepairable1.pdf", ThirdPartyReportFields.LabourAmount, "assessed", "1582.20"),
        ("MontgomeryRepairable1.pdf", ThirdPartyReportFields.Net, "assessed", "16064.52"),
        ("MontgomeryRepairable1.pdf", ThirdPartyReportFields.Gross, "assessed", "19277.42"),
        ("MontgomeryRepairable1.pdf", ThirdPartyReportFields.FinalValue, "", "30000.00"),
        ("Bodyshopreport236502-V1-sPrintAssessors-repairable2.pdf", ThirdPartyReportFields.Net, "assessed", "0.00"),
        ("Bodyshopreport236502-V1-sPrintAssessors-repairable2.pdf", ThirdPartyReportFields.Net, "contract-repair", "8250.00"),
        ("Bodyshopreport236502-V1-sPrintAssessors-repairable2.pdf", ThirdPartyReportFields.PreAccidentValue, "", "11790.00")
    ];

    /// <summary>
    /// The findings the extraction review named, each against the original it
    /// belongs to. Every one of them must reach storage as its own source row:
    /// a reconciliation that is computed and discarded is the same as one that
    /// was never run.
    /// </summary>
    private static readonly (string File, string Code)[] RecordedFindings =
    [
        ("MontgomeryRepairable1.pdf", ThirdPartyFindingCodes.LabourHoursRateMismatch),
        ("MontgomeryRepairable1.pdf", ThirdPartyFindingCodes.ComponentSumReconciles),
        ("MontgomeryRepairable1.pdf", ThirdPartyFindingCodes.NetVatGrossReconciles),
        ("MontgomeryRepairable1.pdf", ThirdPartyFindingCodes.ModelOdometerConflict),
        ("Bodyshopreport236502-V1-sPrintAssessors-repairable2.pdf",
            ThirdPartyFindingCodes.ZeroTotalsWithContractRepair),
        ("Bodyshopreport236502-V1-sPrintAssessors-repairable2.pdf",
            ThirdPartyFindingCodes.ContractRepairBasisNotPrinted),
        ("Report 00077570.pdf", ThirdPartyFindingCodes.NetNotPrinted),
        ("Report 00077570.pdf", ThirdPartyFindingCodes.InitialAndAgreedDiffer),
        ("LairdRepairable1.pdf", ThirdPartyFindingCodes.SupplementWithoutProvedBase),
        ("JohnRBell1.pdf", ThirdPartyFindingCodes.SourceRequiresOcr),
        ("JohnRBell1.pdf", ThirdPartyFindingCodes.PageRequiresHumanVerification)
    ];

    /// <summary>
    /// The corpus is read once for the whole class. Twenty-nine PDFs through
    /// the real reader is not work to repeat per assertion, and every test here
    /// asks a different question of the same reading. The determinism test
    /// deliberately reads again rather than sharing this.
    /// </summary>
    private static readonly Lazy<Task<Dictionary<string, ThirdPartyReportExtractionResult>>> Corpus =
        new(ReadCorpusAsync);

    [ReferencePackFact]
    public async Task EveryOriginalClassifiesToItsRecordedFamilyOrNegativeRole()
    {
        var read = await Corpus.Value;
        var actual = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, result) in read)
        {
            actual[name] = Classification(result.Selection);
        }

        Report("classification", actual.Select(entry => $"{entry.Key} = {entry.Value}"));

        Assert.Equal(29, actual.Count);
        var wrong = actual
            .Where(entry => Expected[entry.Key] != entry.Value)
            .Select(entry => $"{entry.Key}: recorded {Expected[entry.Key]}, read {entry.Value}")
            .ToList();
        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    [ReferencePackFact]
    public async Task NoNegativeOriginalIsGivenAReportVerdict()
    {
        var read = await Corpus.Value;

        foreach (var (name, result) in read)
        {
            if (Expected[name] is "Connexus" or "ExclusiveErehr" or "EvaBodyshop"
                or "Laird" or "Montgomery" or "SPrint")
            {
                continue;
            }

            // An estimate, a vehicle-history check, an invoice, an image page
            // and a scan-only PDF each get no candidate and no outcome,
            // repairability or amount asserted anywhere.
            Assert.Null(result.Candidate);
            Assert.DoesNotContain(
                result.Candidates,
                row => (row.Field == ThirdPartyReportFields.Outcome
                        || row.Field == ThirdPartyReportFields.Repairability
                        || row.Field == ThirdPartyReportFields.Gross
                        || row.Field == ThirdPartyReportFields.Net)
                    && row.Disposition != SourceCandidateDisposition.Missing);
        }
    }

    [ReferencePackFact]
    public async Task TheWorkedSourceValuesAreReadExactlyAsTheReviewRecordedThem()
    {
        var read = await Corpus.Value;
        var wrong = new List<string>();

        foreach (var (file, field, role, expected) in WorkedValues)
        {
            var result = read[file];
            var row = result.Candidates.FirstOrDefault(candidate =>
                candidate.Field == field && candidate.ReferenceRole == role);
            var actual = row is null || row.Disposition == SourceCandidateDisposition.Missing
                ? "(not read)"
                : row.NormalizedValue;
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                wrong.Add($"{file} {field} ({role}): recorded {expected}, read {actual}");
            }
        }

        Report("worked values", wrong.Count == 0 ? ["all as recorded"] : wrong);
        Assert.True(wrong.Count == 0, string.Join("; ", wrong));
    }

    /// <summary>
    /// The contradictions the review names must survive the real reader: the
    /// printed hours times rate that does not make the printed labour, beside
    /// the component total and the gross that both do reconcile.
    /// </summary>
    [ReferencePackFact]
    public async Task TheMontgomeryContradictionAndTheFiguresThatReconcileAreBothPreserved()
    {
        var read = await Corpus.Value;
        var result = read["MontgomeryRepairable1.pdf"];

        Assert.Equal(ThirdPartyReportFamily.Montgomery, result.Selection.Family);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.LabourHoursRateMismatch);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ComponentSumReconciles);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.NetVatGrossReconciles);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ModelOdometerConflict);

        // Nothing was repaired: every printed figure is still on the record.
        Assert.Equal("1582.20", Value(result, ThirdPartyReportFields.LabourAmount, "assessed"));
        Assert.Equal("16064.52", Value(result, ThirdPartyReportFields.Net, "assessed"));
    }

    [ReferencePackFact]
    public async Task TheSPrintZeroTotalsAreNotTakenAsTheAgreedRepairCost()
    {
        var read = await Corpus.Value;
        var result = read["Bodyshopreport236502-V1-sPrintAssessors-repairable2.pdf"];

        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.ZeroTotalsWithContractRepair);
        Assert.Equal("0.00", Value(result, ThirdPartyReportFields.Net, "assessed"));
        Assert.Equal("8250.00", Value(result, ThirdPartyReportFields.Net, "contract-repair"));
    }

    [ReferencePackFact]
    public async Task TheLairdSupplementLinksNoBaseReportItCannotProve()
    {
        var read = await Corpus.Value;
        var result = read["LairdRepairable1.pdf"];

        Assert.Equal("Supplementary Report", Value(result, ThirdPartyReportFields.Revision, ""));
        Assert.Null(result.Candidate!.Identity.BaseReportDocumentId);
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.SupplementWithoutProvedBase);

        // The base report's assessed figures are not in this document and are
        // not borrowed from the full Laird report that is also in the corpus.
        Assert.Null(Value(result, ThirdPartyReportFields.Net, "assessed"));
        Assert.Null(Value(result, ThirdPartyReportFields.LabourAmount, "assessed"));
    }

    [ReferencePackFact]
    public async Task AScanOnlyOriginalNamesThePagesAPersonMustCheck()
    {
        var read = await Corpus.Value;
        var result = read["JohnRBell1.pdf"];

        Assert.Equal(
            ThirdPartySelectionReason.TextUnavailableRequiresOcr,
            result.Selection.Reason);
        Assert.Null(result.Selection.Family);

        var pages = result.Candidates
            .Where(row => row.Field == ThirdPartyReportFields.PageRequiresHumanVerification)
            .ToList();
        Assert.NotEmpty(pages);
        Assert.All(pages, row => Assert.NotNull(row.Page));
        Assert.All(
            pages,
            row => Assert.Equal(SourceCandidateDisposition.Missing, row.Disposition));
        Assert.Contains(
            result.Findings,
            finding => finding.Code == ThirdPartyFindingCodes.PageRequiresHumanVerification);
    }

    /// <summary>
    /// Every finding the review named is persisted as its own source row, with
    /// the finding rules' version, the locator of the rows it compares and the
    /// same document identity they carry. A finding is an observation about the
    /// printed values, so it is recorded beside them rather than instead of one
    /// and never as a value anything may accept.
    /// </summary>
    [ReferencePackFact]
    public async Task EveryRecordedFindingIsPersistedAsItsOwnSourceRow()
    {
        var read = await Corpus.Value;
        var missing = new List<string>();

        foreach (var (file, code) in RecordedFindings)
        {
            var result = read[file];
            var row = result.Candidates.FirstOrDefault(candidate =>
                candidate.Field == ThirdPartyReportFields.Finding(code));
            if (row is null)
            {
                missing.Add($"{file}: no persisted row for {code}");
                continue;
            }

            Assert.Equal(code, row.NormalizedValue);
            Assert.False(string.IsNullOrWhiteSpace(row.RawValue));
            Assert.False(string.IsNullOrWhiteSpace(row.SourceLabel));
            Assert.Equal(ThirdPartyReportValidation.PolicyVersion, row.PolicyVersion);

            // Never Usable and never Missing: a finding is not a value, and it
            // is not an unstated one either.
            Assert.Contains(
                row.Disposition,
                new[]
                {
                    SourceCandidateDisposition.Conflicting,
                    SourceCandidateDisposition.Ambiguous
                });

            // It carries the source identity of the rows it is about, so it is
            // read back with them rather than floating free of the document.
            var value = result.Candidates.First(candidate =>
                !ThirdPartyReportFields.IsFinding(candidate.Field));
            Assert.Equal(value.Sha256, row.Sha256);
            Assert.Equal(value.Occurrence, row.Occurrence);
            Assert.Equal(value.IntakeAssetId, row.IntakeAssetId);
            Assert.Equal(value.DocumentRole, row.DocumentRole);
            Assert.Equal(value.ReaderVersion, row.ReaderVersion);
        }

        Report("findings", missing.Count == 0 ? ["all persisted"] : missing);
        Assert.True(missing.Count == 0, string.Join("; ", missing));
    }

    /// <summary>
    /// The persisted finding rows are exactly the findings that were raised,
    /// one for one and in the same order. Neither half can drift from the other
    /// without this failing.
    /// </summary>
    [ReferencePackFact]
    public async Task ThePersistedFindingRowsAreExactlyTheFindingsRaised()
    {
        var read = await Corpus.Value;

        foreach (var (_, result) in read)
        {
            Assert.Equal(
                result.Findings.Select(finding => finding.Code),
                result.Candidates
                    .Where(row => ThirdPartyReportFields.IsFinding(row.Field))
                    .Select(row => row.NormalizedValue!));
        }
    }

    /// <summary>
    /// A negative original states no contradiction about figures it does not
    /// print. The three that carry text raise no finding at all; the two
    /// scan-only ones raise only the OCR and page-verification findings, which
    /// are the record that a person must read those pages against the original.
    /// </summary>
    [ReferencePackFact]
    public async Task NoNegativeOriginalCarriesAFindingAboutRepairFigures()
    {
        var read = await Corpus.Value;
        var permitted = new[]
        {
            ThirdPartyFindingCodes.SourceRequiresOcr,
            ThirdPartyFindingCodes.PageRequiresHumanVerification
        };

        foreach (var (name, result) in read)
        {
            if (Expected[name] is "Connexus" or "ExclusiveErehr" or "EvaBodyshop"
                or "Laird" or "Montgomery" or "SPrint")
            {
                continue;
            }

            var codes = result.Candidates
                .Where(row => ThirdPartyReportFields.IsFinding(row.Field))
                .Select(row => row.NormalizedValue!)
                .ToList();
            var isScanOnly =
                name.Equals("JohnRBell1.pdf", StringComparison.OrdinalIgnoreCase)
                || name.Equals("TonBridgeAccidentRepair1.pdf", StringComparison.OrdinalIgnoreCase);
            if (isScanOnly)
            {
                Assert.NotEmpty(codes);
                Assert.All(codes, code => Assert.Contains(code, permitted));
                continue;
            }

            Assert.Empty(codes);
        }
    }

    [ReferencePackFact]
    public async Task ReadingTheWholeCorpusTwiceProducesTheIdenticalRecord()
    {
        var first = await ReadCorpusAsync();
        var second = await ReadCorpusAsync();

        foreach (var (name, result) in first)
        {
            Assert.Equal(
                result.Candidates.Select(Describe),
                second[name].Candidates.Select(Describe));
            Assert.Equal(
                result.Findings.Select(finding => finding.Code),
                second[name].Findings.Select(finding => finding.Code));
        }
    }

    private static string Classification(ThirdPartyReportSelection selection) => selection switch
    {
        { Family: { } family } => family.ToString(),
        { Reason: ThirdPartySelectionReason.TextUnavailableRequiresOcr } =>
            nameof(ThirdPartySelectionReason.TextUnavailableRequiresOcr),
        { DocumentRole: { } role } => role.ToString(),
        _ => selection.Reason.ToString()
    };

    private static string? Value(
        ThirdPartyReportExtractionResult result,
        string field,
        string role)
    {
        var row = result.Candidates.FirstOrDefault(candidate =>
            candidate.Field == field && candidate.ReferenceRole == role);
        return row is null || row.Disposition == SourceCandidateDisposition.Missing
            ? null
            : row.NormalizedValue;
    }

    private static string Describe(SourceFieldCandidate row) =>
        string.Join(
            '|',
            row.Field,
            row.PartyRole,
            row.ReferenceRole,
            row.RawValue,
            row.NormalizedValue,
            row.Page?.ToString(CultureInfo.InvariantCulture),
            row.Disposition.ToString());

    /// <summary>
    /// Reads every original in the inventory through the production reader and
    /// the report extractor. Each file's recorded hash is verified first: a
    /// reading of different bytes than the review examined would prove nothing.
    /// </summary>
    private static async Task<Dictionary<string, ThirdPartyReportExtractionResult>> ReadCorpusAsync()
    {
        var root = PrincipalSourceManifestTests.ConfiguredPackRoot()
            ?? throw new InvalidOperationException("This test should have been skipped.");
        var inventory = Path.Combine(
            root, "astra_output", "reports", "third-party-source-inventory.json");
        using var document = JsonDocument.Parse(await File.ReadAllBytesAsync(inventory));
        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var results = new Dictionary<string, ThirdPartyReportExtractionResult>(
            StringComparer.OrdinalIgnoreCase);
        var ordinal = 0;

        foreach (var entry in document.RootElement.EnumerateArray())
        {
            var relative = entry.GetProperty("source").GetString()!;
            var expectedHash = entry.GetProperty("sha256").GetString()!;
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"The pack has no original at {relative}.");

            var bytes = await File.ReadAllBytesAsync(path);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            Assert.Equal(expectedHash, hash);

            var name = Path.GetFileName(path);
            var readResult = await reader.ReadAsync(
                new IntakeSource(
                    name,
                    "application/pdf",
                    bytes,
                    ReceivedAtUtc,
                    "third-party-report-corpus",
                    new(IntakeSourceChannel.ManualUpload, $"third-party-report-{ordinal:00000}")),
                CancellationToken.None);
            Assert.Equal(IntakeSourceReadStatus.Readable, readResult.Status);

            results[name] = ThirdPartyReportExtraction.Extract(
                readResult,
                new(
                    Guid.NewGuid(),
                    hash.ToUpperInvariant(),
                    Occurrence: 0,
                    IntakeAssetId: Guid.NewGuid(),
                    ReaderVersion: readResult.ReaderVersion));
            ordinal++;
        }

        Assert.Equal(29, results.Count);
        return results;
    }

    private void Report(string heading, IEnumerable<string> lines)
    {
        var text = new StringBuilder().AppendLine(
            CultureInfo.InvariantCulture,
            $"third-party report corpus — {heading}:");
        foreach (var line in lines)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {line}");
        }

        output.WriteLine(text.ToString());
    }
}
