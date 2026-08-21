using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Per-file extraction assertions over the operator-supplied mapping corpus
/// (<c>corpus/qdosmapping</c> — local, git-ignored, immutable). Each mapped
/// instruction email must yield its documented core field set through the real
/// reader, route policy, and extraction policy; the values are pinned from the
/// approved mapping document. Runs only where that corpus folder exists.
/// </summary>
public sealed class QdosMappingExtractionTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset ReceivedAtUtc =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed record ExpectedInstruction(
        string FilePrefix,
        string? Claimant,
        string? ClaimNumber,
        string? Registration,
        string? Make,
        string? Model,
        long? Mileage,
        DateOnly? IncidentDate);

    // Keyed by the leading "(EREFn) …" prefix that uniquely names each mapped
    // file. Values come from the letters themselves (see the mapping document).
    private static readonly ExpectedInstruction[] Expectations =
    [
        new("(EREF10) RTA on 14_08_2026", "Mr Paul Larcombe", "AMA/47857/1", "PG18BTY",
            "FORD", "TRANSIT CUSTOM 290 SPORT", null, new(2026, 8, 14)),
        new("(EREF12) RTA on 25_06_2026", "Mr Liam Kinnear", "KAD//46384/1", "YD14VGJ",
            null, null, null, new(2026, 6, 25)),
        new("(EREF19) RTA on 02_08_2026", "Lookers", "JF/ND/47684/1", "DE23XKP",
            "AUDI", "A4 S LN BLACK ED 35TDI MHEV SA", 28000, new(2026, 8, 2)),
        new("(EREF5) RTA on 14_08_2026", "A B C Central", "JF//47847/1", "M555MJF",
            null, null, null, new(2026, 8, 14)),
        new("(EREF8) RTA on 19_08_2026", "Mr Derek King", "AKH/SBU/47856/1", "FC55DEL",
            null, null, null, new(2026, 8, 19)),
        new("(EREF9) RTA on 11_08_2026", "Mr Tomasz Mydlowski", "AKH/ND/47630/1", "MD22DDU",
            "FORD", "RANGER WILDTRAK ECOBLUE 4X4 A", null, new(2026, 8, 11)),
        new("(EREF9) RTA on 15_08_2026", "Miss Dionne Harvey", "AMA/47808/1", "SB71LSK",
            "PEUGEOT", "208 GT PURETECH S/S", null, new(2026, 8, 15))
    ];

    public static TheoryData<string> MappedFilePrefixes()
    {
        var data = new TheoryData<string>();
        foreach (var expectation in Expectations)
        {
            data.Add(expectation.FilePrefix);
        }

        return data;
    }

    [QdosMappingCorpusTheory]
    [MemberData(nameof(MappedFilePrefixes))]
    public async Task MappedInstructionEmailExtractsItsDocumentedFieldSet(string filePrefix)
    {
        var expected = Expectations.Single(item => item.FilePrefix == filePrefix);
        var path = Directory.EnumerateFiles(MappingRoot, "*.eml")
            .SingleOrDefault(item => Path.GetFileName(item)
                .StartsWith(filePrefix, StringComparison.Ordinal));
        Assert.True(path is not null, $"The local mapping corpus has no '{filePrefix}' email.");

        var reader = new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System);
        var readResult = await reader.ReadAsync(Source(path!, Array.IndexOf(Expectations, expected)), CancellationToken.None);
        Assert.Equal(IntakeSourceReadStatus.Readable, readResult.Status);

        var route = new QdosMailRoutePolicy().Evaluate(readResult);
        Assert.Equal(MailRouteDisposition.Accepted, route.Disposition);

        var result = new QdosInstructionExtractionPolicy().Extract(
            readResult,
            ReceivedAtUtc,
            new EstablishedPrincipalContext(
                QdosInstructionExtractionPolicy.SupportedPrincipalCode,
                QdosMailRoutePolicy.Key,
                QdosMailRoutePolicy.Version));
        var draft = Assert.IsType<InstructionDraft>(result.InstructionDraft);
        output.WriteLine(
            $"{Path.GetFileName(path)} => claimant='{draft.ClaimantName}' claim='{draft.ClaimNumber}' " +
            $"reg='{draft.VehicleRegistration}' make='{draft.VehicleMake}' model='{draft.VehicleModel}' " +
            $"mileage={draft.VehicleMileage} incident={draft.DateOfIncident:yyyy-MM-dd}");

        Assert.Equal(expected.Claimant, draft.ClaimantName);
        Assert.Equal(expected.ClaimNumber, draft.ClaimNumber);
        Assert.Equal(expected.Registration, draft.VehicleRegistration);
        if (expected.Make is not null)
        {
            Assert.Equal(expected.Make, draft.VehicleMake);
            Assert.Equal(expected.Model, draft.VehicleModel);
        }

        if (expected.Mileage is not null)
        {
            Assert.Equal(expected.Mileage, draft.VehicleMileage);
        }

        Assert.Equal(expected.IncidentDate, draft.DateOfIncident);
    }

    private static string MappingRoot => Path.Combine(QdosCorpus.Root, "qdosmapping");

    private static IntakeSource Source(string path, int index) =>
        new(
            Path.GetFileName(path),
            "message/rfc822",
            File.ReadAllBytes(path),
            ReceivedAtUtc,
            "qdos-mapping",
            new(IntakeSourceChannel.Mailbox, $"mapping-{index:00000}"));
}

internal sealed class QdosMappingCorpusTheoryAttribute : TheoryAttribute
{
    public QdosMappingCorpusTheoryAttribute()
    {
        if (!Directory.Exists(Path.Combine(QdosCorpus.Root, "qdosmapping")))
        {
            Skip = "This machine's ignored local corpus has no qdosmapping folder; corpora differ per system.";
        }
    }
}
