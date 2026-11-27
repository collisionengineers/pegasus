using System.Security.Cryptography;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Tests.Documents;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The rules of a submission of market research from Pegasus Desktop (issue
/// 2166; R1243, R1309 to R1315): only a member of staff submits, the record
/// carries a proposed retail value, there is at most one report, and every
/// screenshot an advert names is exactly one of the files.
/// </summary>
public sealed class MarketResearchSubmissionTests
{
    private const string Owner = "staff:desktop";
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly DateOnly Month = new(2026, 11, 1);
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 10, 0, 0, TimeSpan.Zero);
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly MarketResearchSubmissionFile Report =
        new(Guid.NewGuid(), "report.pdf", "application/pdf", MarketResearchFileKind.Report);
    private static readonly MarketResearchSubmissionFile Pack =
        new(Guid.NewGuid(), "pack.pdf", "application/pdf", MarketResearchFileKind.EvidencePack);
    private static readonly MarketResearchSubmissionFile First =
        new(Guid.NewGuid(), "advert-1.jpg", "image/jpeg", MarketResearchFileKind.Screenshot);
    private static readonly MarketResearchSubmissionFile Second =
        new(Guid.NewGuid(), "advert-2.jpg", "image/jpeg", MarketResearchFileKind.Screenshot);

    [Fact]
    public void AWholeSubmissionAndOneWithNoFilesAreBothAdmitted()
    {
        MarketResearchSubmissionPolicy.Validate(Whole());
        // With no AI to write the report, research is filed without one (R1311).
        MarketResearchSubmissionPolicy.Validate(Command(Record(null) with { Adverts = [] }, []));
        MarketResearchSubmissionPolicy.Validate(Whole() with { Review = MarketResearchReview.NotReviewed });
    }

    [Fact]
    public void OnlyAMemberOfStaffSubmits()
    {
        Assert.Throws<StaffAuthorizationException>(() => MarketResearchSubmissionPolicy.Validate(
            Whole() with { Actor = ActionActor.Automation("pegasus-automation") }));
        Assert.Throws<StaffAuthorizationException>(() => MarketResearchSubmissionPolicy.Validate(
            Whole() with { Actor = ActionActor.SystemWorker("worker") }));
    }

    [Fact]
    public void TheRecordCarriesAProposedRetailValueAboveZero()
    {
        foreach (var proposed in new decimal?[] { null, 0m, -1m })
        {
            var whole = Whole();
            var refused = Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
                whole with { Record = whole.Record with { Value = whole.Record.Value! with { Proposed = proposed } } }));
            Assert.StartsWith("A market research valuation carries its retail value.", refused.Message, StringComparison.Ordinal);
        }

        var noValue = Whole();
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            noValue with { Record = noValue.Record with { Value = null } }));
    }

    [Fact]
    public void TheCardIsRetailOnlyForTheGuideMonthAndCarriesTheRecordsMileage()
    {
        var card = MarketResearchSubmissionPolicy.Card(Whole(), new DateOnly(2026, 11, 26), new TimeOnly(10, 0));

        Assert.Equal(ValuationSource.AiMarketResearch, card.Source);
        Assert.Equal(9900m, card.RetailValue);
        Assert.Null(card.TradeValue);
        Assert.Equal(Month, card.GuideMonth);
        Assert.Equal(42000L, card.Mileage);
    }

    [Fact]
    public void TheGuideMonthIsTheFirstDayOfItsMonthAndTheSchemaVersionIsOne()
    {
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Whole() with { GuideMonth = new DateOnly(2026, 11, 2) }));
        var whole = Whole();
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            whole with { Record = whole.Record with { SchemaVersion = 2 } }));
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Whole() with { Review = (MarketResearchReview)9 }));
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Whole() with { CaseId = Guid.Empty }));
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Whole() with { OperationKey = " " }));
    }

    [Fact]
    public void ThereIsAtMostOneReportAndOnePackAndThePackIsTheOneTheRecordNames()
    {
        var secondReport = Report with { UploadId = Guid.NewGuid(), FileName = "report-2.pdf" };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record("pack.pdf"), [Report, secondReport, Pack, First, Second])));
        var secondPack = Pack with { UploadId = Guid.NewGuid(), FileName = "pack-2.pdf" };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record("pack.pdf"), [Report, Pack, secondPack, First, Second])));
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record("another.pdf"), [Report, Pack, First, Second])));
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record(null), [Report, Pack, First, Second])));
    }

    [Fact]
    public void EveryScreenshotAnAdvertNamesIsOneFileAndNoFileIsLeftUnnamed()
    {
        // A screenshot the record names and no file carries.
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record("pack.pdf"), [Report, Pack, First])));
        // A screenshot file no advert names.
        var unnamed = First with { UploadId = Guid.NewGuid(), FileName = "advert-9.jpg" };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record("pack.pdf"), [Report, Pack, First, Second, unnamed])));
        // Two files of one name.
        var duplicate = Second with { UploadId = Guid.NewGuid(), FileName = "ADVERT-1.JPG" };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record("pack.pdf"), [Report, Pack, First, duplicate])));
        // One screenshot named by two adverts.
        var record = Record("pack.pdf");
        var twice = record with
        {
            Adverts = [record.Adverts![0], record.Adverts[1] with { Screenshots = record.Adverts[0].Screenshots }]
        };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(twice, [Report, Pack, First, Second])));

        Assert.Equal(1, MarketResearchSubmissionPolicy.AdvertNumberOf(record, First));
        Assert.Equal(2, MarketResearchSubmissionPolicy.AdvertNumberOf(record, Second));
        Assert.Null(MarketResearchSubmissionPolicy.AdvertNumberOf(record, Pack));
    }

    [Fact]
    public void TheCountsAndLengthsAreBounded()
    {
        var record = Record("pack.pdf");
        var manyAdverts = record with
        {
            Adverts = [.. Enumerable.Range(1, MarketResearchSubmissionPolicy.MaximumAdverts + 1)
                .Select(number => record.Adverts![0] with { Number = number, Screenshots = [] })]
        };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(Command(manyAdverts, [])));

        var longName = First with { FileName = new string('a', MarketResearchSubmissionPolicy.MaximumFileNameLength + 1) };
        Assert.Throws<ArgumentOutOfRangeException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record(null), [longName])));
        var noUpload = First with { UploadId = Guid.Empty };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.Validate(
            Command(Record("pack.pdf"), [Report, Pack, noUpload, Second])));

        var longComment = record with
        {
            Adverts = [record.Adverts![0] with { Comment = new string('c', MarketResearchSubmissionPolicy.MaximumRecordLength) }, record.Adverts[1]]
        };
        Assert.Throws<ArgumentException>(() => MarketResearchSubmissionPolicy.RecordJson(longComment));
    }

    [Fact]
    public void TheInputHashTellsTheSameSubmissionFromAnother()
    {
        var whole = Whole();
        var json = MarketResearchSubmissionPolicy.RecordJson(whole.Record);
        var hash = MarketResearchSubmissionPolicy.InputHash(whole, json);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, MarketResearchSubmissionPolicy.InputHash(Whole(), MarketResearchSubmissionPolicy.RecordJson(Whole().Record)));
        Assert.NotEqual(hash, MarketResearchSubmissionPolicy.InputHash(
            whole with { Review = MarketResearchReview.NotReviewed }, json));
        Assert.NotEqual(hash, MarketResearchSubmissionPolicy.InputHash(
            whole with { Files = [Report with { UploadId = Guid.NewGuid() }, Pack, First, Second] }, json));
        var other = whole.Record with { Value = whole.Record.Value! with { Proposed = 9950m } };
        Assert.NotEqual(hash, MarketResearchSubmissionPolicy.InputHash(
            whole with { Record = other }, MarketResearchSubmissionPolicy.RecordJson(other)));
        Assert.Contains("\"comment\":\"Close match.\"", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// A submission completes a market research request that is queued or
    /// taken (R1312), and nothing else: not another kind of job, and not one
    /// already closed.
    /// </summary>
    [Fact]
    public void ASubmissionAnswersOnlyAWaitingMarketResearchRequest()
    {
        Assert.True(AiJobPolicy.IsAnsweredByMarketResearchSubmission(AiJobKind.MarketResearch, AiJobState.Queued));
        Assert.True(AiJobPolicy.IsAnsweredByMarketResearchSubmission(AiJobKind.MarketResearch, AiJobState.Taken));
        Assert.All(
            Enum.GetValues<AiJobState>().Where(state => state is not (AiJobState.Queued or AiJobState.Taken)),
            state => Assert.False(AiJobPolicy.IsAnsweredByMarketResearchSubmission(AiJobKind.MarketResearch, state)));
        Assert.All(
            Enum.GetValues<AiJobKind>().Where(kind => kind != AiJobKind.MarketResearch),
            kind => Assert.False(AiJobPolicy.IsAnsweredByMarketResearchSubmission(kind, AiJobState.Queued)));
        // No transition a client asks for completes a queued job.
        Assert.False(AiJobPolicy.IsLegalTransition(AiJobState.Queued, AiJobState.Completed));
    }

    /// <summary>
    /// A call under a key already used answers with the first submission
    /// before any staged upload is opened, so it still answers once the
    /// uploads have expired; a new one opens each file whole first.
    /// </summary>
    [Fact]
    public async Task AReplayAnswersWithoutOpeningAnUploadAndANewSubmissionOpensEachFile()
    {
        var staging = new InMemoryDocumentUploadStaging();
        var uploads = new DocumentUploads(staging, new SettableClock(Now));
        var store = new RecordingStore();
        var submit = new SubmitMarketResearch(store, uploads);
        var content = new byte[] { 1, 2, 3 };
        var staged = new MarketResearchSubmissionFile(
            Guid.NewGuid(), "advert-1.jpg", "image/jpeg", MarketResearchFileKind.Screenshot);
        await uploads.ExecuteAsync(
            new BeginDocumentUploadRequest(staged.UploadId, Owner, content.Length, Convert.ToHexString(SHA256.HashData(content))),
            default);
        await uploads.ExecuteAsync(
            new AppendDocumentUploadPartRequest(
                staged.UploadId, Owner, 1, content, Convert.ToHexString(SHA256.HashData(content))),
            default);
        var record = Record(null);
        var command = Command(record with { Adverts = [record.Adverts![0]] }, [staged]);

        var filed = await submit.ExecuteAsync(command, default);

        Assert.False(filed.IsReplay);
        var filing = Assert.Single(store.Filings);
        Assert.Equal(content, Assert.Single(filing.Contents).ToArray());
        Assert.Equal(MarketResearchSubmissionPolicy.RecordJson(command.Record), filing.RecordJson);

        // The uploads have gone; the replay still answers, and nothing more is filed.
        staging.Manifests.Clear();
        var replay = await submit.ExecuteAsync(command, default);

        Assert.True(replay.IsReplay);
        Assert.Equal(filed.SubmissionId, replay.SubmissionId);
        Assert.Single(store.Filings);

        // A new submission whose upload is not there is refused before the store is asked to file.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            submit.ExecuteAsync(command with { OperationKey = "mcp:another" }, default));
        Assert.Single(store.Filings);
    }

    private static SubmitMarketResearchCommand Whole() =>
        Command(Record("pack.pdf"), [Report, Pack, First, Second]);

    private static SubmitMarketResearchCommand Command(
        MarketResearchRecord record,
        IReadOnlyList<MarketResearchSubmissionFile> files) =>
        new(CaseId, Engineer, "mcp:research-1", MarketResearchReview.Accepted, Month, record, files, Owner);

    private static MarketResearchRecord Record(string? evidencePack) =>
        new(
            MarketResearchSubmissionPolicy.SchemaVersion,
            new("Pegasus Desktop", "0.13.0"),
            "2026-11-26T10:00:00.000Z",
            "DK12TOP",
            "Ford Focus 1.0 Zetec",
            42000,
            "odometer",
            new(9500m),
            null,
            [Advert(1, "advert-1.jpg"), Advert(2, "advert-2.jpg")],
            new(2, 9900m, 9900m, 9800m, 10000m, 9900m, 9900m, "average", null),
            evidencePack);

    private static MarketResearchRecordAdvert Advert(int number, string screenshot) =>
        new(
            number,
            "search",
            "Auto Trader",
            $"https://adverts.example/{number}",
            "Ford Focus 1.0 Zetec",
            2019,
            40000,
            9800m + (number * 100),
            "Trade, Leeds",
            "Close match.",
            true,
            90,
            "2026-11-26T09:55:00.000Z",
            [screenshot],
            null);

    private sealed class RecordingStore : IMarketResearchSubmissionStore
    {
        private readonly Dictionary<string, (string InputHash, MarketResearchSubmission Submission)> submissions =
            new(StringComparer.Ordinal);

        public List<MarketResearchSubmissionFiling> Filings { get; } = [];

        public Task<MarketResearchSubmission?> FindAsync(
            Guid caseId,
            string operationKey,
            string inputHash,
            CancellationToken cancellationToken) =>
            Task.FromResult<MarketResearchSubmission?>(submissions.TryGetValue(operationKey, out var found)
                ? found.Submission with { IsReplay = true }
                : null);

        public Task<MarketResearchSubmission> SubmitAsync(
            MarketResearchSubmissionFiling filing,
            CancellationToken cancellationToken)
        {
            Filings.Add(filing);
            var submission = new MarketResearchSubmission(
                Guid.NewGuid(),
                filing.Command.CaseId,
                Now,
                filing.Command.Actor.SubjectId,
                Guid.NewGuid(),
                [],
                IsReplay: false);
            submissions[filing.Command.OperationKey] = (filing.InputHash, submission);
            return Task.FromResult(submission);
        }
    }
}
