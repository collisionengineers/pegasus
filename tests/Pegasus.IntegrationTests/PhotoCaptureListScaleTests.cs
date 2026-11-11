using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Xunit.Abstractions;
using Labels = Pegasus.Web.Presentation.OperatorLabels.PhotoCapture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Photo capture list measure runs apart from every parallel class, so
/// its timings are this runner's and not its neighbours' (xUnit runs a
/// collection that disables parallelization after, and apart from, every
/// parallel one).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PhotoCaptureListScaleSerialTests
{
    public const string Name = "Photo capture list scale";
}

/// <summary>
/// The Photo capture lists' speed at 500 and at 2,000 captures of 30 items
/// (#2020; R812): the captures are written straight to CI's own database, both
/// tabs are loaded 2 times to warm and 20 times timed at each size, and p50
/// and p95 are written to the test output and to the run's step summary. It
/// stays as the before-and-after measure for the fix (operator, 10 November
/// 2026; R881); the budget is 500 ms (R882). Nothing here asserts a time:
/// a request sends the same statements at 2,000 captures as at 500 (a
/// no-growth pair), the page's counts are the ones the seed was built with,
/// and a tab reads of its items only what the list uses, under a ceiling per
/// capture.
/// </summary>
[Trait("Category", "SqlServer")]
[Collection(PhotoCaptureListScaleSerialTests.Name)]
public sealed class PhotoCaptureListScaleTests(ITestOutputHelper output)
{
    /// <summary>The list's page size (<c>IndexModel.PageSize</c>).</summary>
    private const int PageSize = 25;

    private const int WarmUpRequests = 2;
    private const int TimedRequests = 20;
    private const int FirstSize = 500;
    private const int SecondSize = 2_000;

    /// <summary>The items of a capture with photos (<c>ScaleSeed.Shapes</c>).</summary>
    private const int ItemsPerCaptureWithPhotos = 30;

    /// <summary>
    /// The most item rows a list may read for one capture with photos: its 30
    /// items hold 20 distinct things the list uses (eight overview positions,
    /// seven damage area stages counting the extra, the number plate, the VIN,
    /// the dashboard, and one row each for the scene and the other vehicle's
    /// photos), and paperwork and video are not read (#2020).
    /// </summary>
    private const int ItemRowsReadPerCapture = 20;

    private const string ItemsTable = "[PhotoCaptureItems]";

    /// <summary>
    /// A column only a read of whole item rows selects: the preview read
    /// selects a row's content hash for its address, never its client id.
    /// </summary>
    private const string WholeItemRowColumn = "[ClientItemId]";

    /// <summary>The standing links the seed makes: one per contact and the website's.</summary>
    private const int StandingLinks = 21;

    private const string StandingUrl = $"/PhotoCapture?tab={Labels.StandingLinksKey}";
    private const string DirectUrl = "/PhotoCapture?scope=all";

    [Fact]
    public async Task BothPhotoCaptureListsSendTheSameStatementsAt2000CapturesAsAt500AndCountWhatWasSeeded()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var database = await factory.DatabaseAsync;
        await IntakeWebDriver.SeedUserAccountsAsync(factory, ScaleSeed.IssuerCount);
        var seed = await ScaleSeed.CreateAsync(database);

        var readings = new List<Reading>();
        foreach (var (size, added) in new[] { (FirstSize, FirstSize), (SecondSize, SecondSize - FirstSize) })
        {
            var seeding = Stopwatch.StartNew();
            await seed.AddCapturesAsync(added);
            seeding.Stop();
            Assert.Equal(size, seed.Captures);
            var expected = seed.Snapshot();
            readings.Add(await MeasureAsync(client, counter, StandingUrl, direct: false, expected, seeding.Elapsed));
            readings.Add(await MeasureAsync(client, counter, DirectUrl, direct: true, expected, seeding.Elapsed));
        }

        output.WriteLine(seed.DescribeRows());
        foreach (var reading in readings)
        {
            output.WriteLine(reading.Line);
        }

        await AppendStepSummaryAsync(readings);

        foreach (var reading in readings)
        {
            AssertPageCounts(reading);
            AssertItemReadsBounded(reading);
        }

        foreach (var direct in new[] { false, true })
        {
            var atFirst = Assert.Single(readings, reading => reading.Direct == direct && reading.Expected.Captures == FirstSize);
            var atSecond = Assert.Single(readings, reading => reading.Direct == direct && reading.Expected.Captures == SecondSize);
            Assert.True(atFirst.Statements > 0, "The interceptor observed no statement at all.");
            Assert.True(
                atFirst.Statements == atSecond.Statements,
                $"{atFirst.TabLabel} grew with its captures: {atFirst.Statements} statements at {FirstSize} and {atSecond.Statements} at {SecondSize}."
                + Environment.NewLine + $"At {FirstSize}:" + Environment.NewLine + atFirst.StatementText
                + Environment.NewLine + $"At {SecondSize}:" + Environment.NewLine + atSecond.StatementText);
        }
    }

    /// <summary>
    /// Two warm-up loads, then <see cref="TimedRequests"/> timed ones around
    /// the HTTP call. The statements of one request are the fewest any timed
    /// request sent, so a background read that lands beside one request does
    /// not count as the list's.
    /// </summary>
    private static async Task<Reading> MeasureAsync(
        HttpClient client,
        SqlStatementCounter counter,
        string url,
        bool direct,
        SeedCounts expected,
        TimeSpan seeding)
    {
        for (var index = 0; index < WarmUpRequests; index++)
        {
            _ = await IntakeWebDriver.GetHtmlAsync(client, url);
        }

        var times = new List<double>(TimedRequests);
        var statements = int.MaxValue;
        var statementText = string.Empty;
        var itemRowsRead = 0;
        var readsWholeItemRows = false;
        var html = string.Empty;
        for (var index = 0; index < TimedRequests; index++)
        {
            counter.Reset();
            var watch = Stopwatch.StartNew();
            using var response = await client.GetAsync(url);
            var body = await response.Content.ReadAsStringAsync();
            watch.Stop();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            times.Add(watch.Elapsed.TotalMilliseconds);
            if (counter.Count < statements)
            {
                statements = counter.Count;
                statementText = counter.Describe();
                var itemReads = counter.Reads
                    .Where(read => read.CommandText.Contains(ItemsTable, StringComparison.Ordinal))
                    .ToArray();
                itemRowsRead = itemReads.Sum(read => read.Rows);
                readsWholeItemRows = itemReads.Any(read => read.CommandText.Contains(WholeItemRowColumn, StringComparison.Ordinal));
            }

            html = body;
        }

        times.Sort();
        return new(
            direct,
            expected,
            NearestRank(times, 50),
            NearestRank(times, 95),
            statements,
            statementText,
            itemRowsRead,
            readsWholeItemRows,
            seeding.TotalSeconds,
            html);
    }

    /// <summary>The nearest-rank percentile of <paramref name="sorted"/>, which is in ascending order.</summary>
    private static double NearestRank(List<double> sorted, int percentile)
    {
        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Count);
        return sorted[Math.Clamp(rank, 1, sorted.Count) - 1];
    }

    /// <summary>
    /// The page's own counts, as it renders them (v51 N, P; R948): Direct
    /// links' Awaiting count and the standing links' count on the tabs; on
    /// Direct links each scope's count, Awaiting, Complete and Closed adding
    /// up to All; on Standing links the submissions received; and a full
    /// first page of rows.
    /// </summary>
    private static void AssertPageCounts(Reading reading)
    {
        var html = reading.Html;
        var expected = reading.Expected;
        var where = $"{reading.TabLabel} at {expected.Captures} captures";
        var direct = expected.LinksWeSentStates;
        int Seeded(params PhotoCaptureState[] states) => states.Sum(state => direct.GetValueOrDefault(state));
        // Every seeded link still waiting is live; Expired and Cancelled ones are closed.
        var awaiting = Seeded(
            PhotoCaptureState.SentNotOpened, PhotoCaptureState.Opened, PhotoCaptureState.PhotosComingIn,
            PhotoCaptureState.SomethingMissing, PhotoCaptureState.NeedsCall);
        var tabCounts = Regex.Matches(html, "<span class=\"tab-count\">(\\d+)</span>")
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .ToArray();
        Assert.True(tabCounts.Length == 2, $"{where}: the page rendered {tabCounts.Length} tab counts, not two.");
        Assert.Equal(awaiting, tabCounts[0]);
        Assert.Equal(StandingLinks, tabCounts[1]);

        if (reading.Direct)
        {
            Assert.True(ScopeCount(html, "all") == expected.LinksWeSent, $"{where}: All reads {ScopeCount(html, "all")}, not {expected.LinksWeSent}.");
            Assert.True(ScopeCount(html, "awaiting") == awaiting, $"{where}: Awaiting reads {ScopeCount(html, "awaiting")}, not {awaiting}.");
            Assert.Equal(Seeded(PhotoCaptureState.Complete), ScopeCount(html, "complete"));
            Assert.Equal(Seeded(PhotoCaptureState.Expired, PhotoCaptureState.Cancelled), ScopeCount(html, "closed"));
            var chase = ScopeCount(html, "due");
            Assert.True(
                chase >= Seeded(PhotoCaptureState.NeedsCall) && chase <= awaiting,
                $"{where}: Needs chase reads {chase}, outside {Seeded(PhotoCaptureState.NeedsCall)} to {awaiting}.");
        }
        else
        {
            Assert.Contains(Labels.ReceivedCount(expected.ComingIn), html, StringComparison.Ordinal);
        }

        Assert.Equal(PageSize, Regex.Count(html, "data-photo-capture-row=\""));
    }

    /// <summary>
    /// The list reads of the items only what the required items rule and
    /// the photograph count use, grouped in SQL (#2020), and the few each row
    /// previews: never whole item rows, and at most
    /// <see cref="ItemRowsReadPerCapture"/> rows for each capture with photos
    /// it evaluates, fewer than its <see cref="ItemsPerCaptureWithPhotos"/>
    /// items. Standing links also counts Direct links' Awaiting, so its
    /// ceiling is over both tabs. A ceiling, never an exact total.
    /// </summary>
    private static void AssertItemReadsBounded(Reading reading)
    {
        var where = $"{reading.TabLabel} at {reading.Expected.Captures} captures";
        Assert.True(reading.ItemRowsRead > 0, $"{where}: the interceptor observed no read of the items.");
        Assert.False(
            reading.ReadsWholeItemRows,
            $"{where}: a statement read whole item rows." + Environment.NewLine + reading.StatementText);
        var items = reading.Direct ? reading.TabItems : reading.Expected.ComingInItems + reading.Expected.LinksWeSentItems;
        var ceiling = items / ItemsPerCaptureWithPhotos * ItemRowsReadPerCapture;
        Assert.True(
            reading.ItemRowsRead <= ceiling,
            $"{where}: the list read {reading.ItemRowsRead} item rows, more than {ceiling} for {items / ItemsPerCaptureWithPhotos} captures with photos.");
    }

    /// <summary>The count on the Direct links scope button whose value is <paramref name="scopeKey"/>, or null when it has none.</summary>
    private static int? ScopeCount(string html, string scopeKey)
    {
        var match = Regex.Match(
            html,
            "name=\"scope\" value=\"" + Regex.Escape(scopeKey) + "\" class=\"pc-pill\"[\\s\\S]*?<span class=\"pc-pill-count\">(\\d+)</span>");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    /// <summary>
    /// The figures as a small Markdown table on the CI run's page, when the
    /// run has a step summary. A failure to write it is reported here and
    /// never fails the test.
    /// </summary>
    private async Task AppendStepSummaryAsync(List<Reading> readings)
    {
        var path = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var lines = new List<string>
        {
            string.Empty,
            "### Photo capture lists at scale (#2020)",
            string.Empty,
            string.Create(
                CultureInfo.InvariantCulture,
                $"In-process loads on this runner, {WarmUpRequests} warm-up then {TimedRequests} timed per tab and size; p50 and p95 by nearest rank."),
            string.Empty,
            "| Tab | Captures on the tab | Captures in all | Items on the tab | p50 ms | p95 ms | SQL statements per request | Seed seconds |",
            "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |"
        };
        lines.AddRange(readings.Select(reading => reading.TableRow));
        lines.Add(string.Empty);
        try
        {
            await File.AppendAllTextAsync(path, string.Join(Environment.NewLine, lines));
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException
            or System.Security.SecurityException)
        {
            output.WriteLine($"The step summary was not written: {exception.Message}");
        }
    }

    /// <summary>What the seed holds at one size: each tab's captures by state, and their items.</summary>
    private sealed record SeedCounts(
        int Captures,
        int ComingIn,
        int LinksWeSent,
        int ComingInItems,
        int LinksWeSentItems,
        Dictionary<PhotoCaptureState, int> ComingInStates,
        Dictionary<PhotoCaptureState, int> LinksWeSentStates);

    /// <summary>One tab at one size: its timings, its statements, the item rows they read and the last page it rendered.</summary>
    private sealed record Reading(
        bool Direct,
        SeedCounts Expected,
        double P50,
        double P95,
        int Statements,
        string StatementText,
        int ItemRowsRead,
        bool ReadsWholeItemRows,
        double SeedSeconds,
        string Html)
    {
        public string TabLabel => Direct ? Labels.DirectLinks : Labels.StandingLinks;

        private int TabCaptures => Direct ? Expected.LinksWeSent : Expected.ComingIn;

        public int TabItems => Direct ? Expected.LinksWeSentItems : Expected.ComingInItems;

        public string Line => string.Create(
            CultureInfo.InvariantCulture,
            $"{TabLabel}: {TabCaptures} captures on the tab, {Expected.Captures} in all, {TabItems} items on the tab; p50 {P50:F1} ms, p95 {P95:F1} ms; {Statements} SQL statements per request; seed {SeedSeconds:F1} s");

        public string TableRow => string.Create(
            CultureInfo.InvariantCulture,
            $"| {TabLabel} | {TabCaptures:N0} | {Expected.Captures:N0} | {TabItems:N0} | {P50:F1} | {P95:F1} | {Statements} | {SeedSeconds:F1} |");
    }

    /// <summary>
    /// The seed, written straight to the database with change tracking off, in
    /// batches of <see cref="CapturesPerBatch"/> captures. Captures alternate
    /// between the tabs, so each holds half; each tab's captures take the
    /// states of <see cref="StatePattern"/> in turn, and every capture is
    /// checked against Core's own status rule at the test's fixed clock as it
    /// is built, so the counts the page must show are the seed's tally.
    /// </summary>
    private sealed class ScaleSeed
    {
        public const int IssuerCount = 10;
        private const int ContactCount = 20;
        private const int CapturesPerBatch = 250;
        private const int LinkLifetimeDays = 7;
        private const string Registry = "PCLS";

        /// <summary>One cycle of a tab's states: twelve of twenty captures have photos.</summary>
        private static readonly PhotoCaptureState[] StatePattern =
        [
            PhotoCaptureState.Complete, PhotoCaptureState.PhotosComingIn, PhotoCaptureState.SentNotOpened,
            PhotoCaptureState.Complete, PhotoCaptureState.SomethingMissing, PhotoCaptureState.Opened,
            PhotoCaptureState.Complete, PhotoCaptureState.Expired, PhotoCaptureState.PhotosComingIn,
            PhotoCaptureState.Complete, PhotoCaptureState.SomethingMissing, PhotoCaptureState.NeedsCall,
            PhotoCaptureState.Complete, PhotoCaptureState.Cancelled, PhotoCaptureState.PhotosComingIn,
            PhotoCaptureState.SentNotOpened, PhotoCaptureState.Complete, PhotoCaptureState.SomethingMissing,
            PhotoCaptureState.Opened, PhotoCaptureState.Expired
        ];

        /// <summary>The pattern slot whose Links we sent capture is Expired because its Case is archived (R793).</summary>
        private const int ArchivedCaseSlot = 7;

        /// <summary>Two zones that do not touch, so the sender marked two damaged areas.</summary>
        private static readonly string[] MarkedZones = ["fd", "rp"];

        private static readonly DateTimeOffset Now = IntakeWebApplicationFactory.FixedUtcNow;

        private readonly LocalDbTestDatabase database;
        private readonly Guid principalId = Guid.NewGuid();
        private readonly Guid lineageId = Guid.NewGuid();
        private readonly List<Guid> issuers;
        private readonly List<(Guid OrganizationId, bool IsRepairer, Guid LinkId)> contacts = [];
        private readonly Guid websiteLinkId = Guid.NewGuid();
        private readonly List<ItemShape> fullShapes;
        private readonly List<ItemShape> missingPlateShapes;
        private readonly List<PhotoCaptureItem> fullItems;
        private readonly List<PhotoCaptureItem> missingPlateItems;
        private readonly string detailsWithAreas;
        private readonly string emptyDetails;
        private readonly string emptyEvidence = EfIntakeReceiptStore.SerializeEvidence([]);
        private readonly string emptyFields = EfIntakeReceiptStore.SerializeFields([]);
        private readonly string emptyOcr = EfIntakeReceiptStore.SerializeEnvelope<IReadOnlyList<ScannedPdfOcrCandidate>>([]);
        private readonly Dictionary<string, int> rows = new(StringComparer.Ordinal);
        private readonly Dictionary<PhotoCaptureState, int> comingInStates = [];
        private readonly Dictionary<PhotoCaptureState, int> linksWeSentStates = [];
        private int comingInItems;
        private int linksWeSentItems;
        private int nextCaseSequence = 1;

        private ScaleSeed(LocalDbTestDatabase database, List<Guid> issuers)
        {
            this.database = database;
            this.issuers = issuers;
            fullShapes = Shapes(withNumberPlate: true);
            missingPlateShapes = Shapes(withNumberPlate: false);
            fullItems = [.. fullShapes.Select((shape, ordinal) => EfPhotoCaptureStore.Map(Item(Guid.Empty, ordinal, shape, Now, null)))];
            missingPlateItems = [.. missingPlateShapes.Select((shape, ordinal) => EfPhotoCaptureStore.Map(Item(Guid.Empty, ordinal, shape, Now, null)))];
            detailsWithAreas = EfPhotoCaptureStore.SerializeDetails(new PhotoCaptureDetails(DamageAreas: MarkedZones));
            emptyDetails = EfPhotoCaptureStore.SerializeDetails(PhotoCaptureDetails.Empty);
        }

        public int Captures { get; private set; }

        /// <summary>
        /// The fixed rows: the issuers (the bulk staff accounts), a Principal
        /// with its lineage for the Cases, <see cref="ContactCount"/> contacts
        /// with a standing link each (every third a repairer) and the website
        /// link.
        /// </summary>
        public static async Task<ScaleSeed> CreateAsync(LocalDbTestDatabase database)
        {
            List<Guid> issuers;
            await using (var context = await database.CreateContextAsync())
            {
                var userNames = Enumerable.Range(0, IssuerCount).Select(index => $"bulk-{index:D3}").ToArray();
                issuers = await context.Users.AsNoTracking()
                    .Where(user => userNames.Contains(user.UserName!))
                    .Select(user => user.Id)
                    .ToListAsync();
            }

            Assert.Equal(IssuerCount, issuers.Count);
            var seed = new ScaleSeed(database, issuers);
            await seed.AddFixedRowsAsync();
            return seed;
        }

        public SeedCounts Snapshot() => new(
            Captures,
            comingInStates.Values.Sum(),
            linksWeSentStates.Values.Sum(),
            comingInItems,
            linksWeSentItems,
            new(comingInStates),
            new(linksWeSentStates));

        public string DescribeRows() =>
            "Seeded rows: " + string.Join(", ", rows.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => $"{row.Key} {row.Value}"));

        private async Task AddFixedRowsAsync()
        {
            await using var context = await database.CreateContextAsync();
            var principalOrganizationId = Guid.NewGuid();
            context.Organizations.Add(new OrganizationEntity
            {
                Id = principalOrganizationId, Name = "Photo capture scale Principal", Role = "principal", Version = 0
            });
            context.PrincipalSequenceLineages.Add(new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = Now.AddYears(-1) });
            context.Principals.Add(new PrincipalEntity
            {
                Id = principalId, OrganizationId = principalOrganizationId, SequenceLineageId = lineageId,
                Code = Registry, IsActive = true, Version = 0
            });
            for (var index = 0; index < ContactCount; index++)
            {
                var organizationId = Guid.NewGuid();
                var isRepairer = index % 3 == 0;
                var linkId = Guid.NewGuid();
                context.Organizations.Add(new OrganizationEntity
                {
                    Id = organizationId,
                    Name = isRepairer ? $"Scale Bodyshop {index:D2}" : $"Scale Claims {index:D2}",
                    Role = isRepairer ? "repairer" : "claim_source",
                    Version = 0
                });
                context.StandingPhotoLinks.Add(StandingLink(linkId, StandingPhotoLinkKind.Contact, organizationId, $"pcls-contact-{index:D2}"));
                contacts.Add((organizationId, isRepairer, linkId));
            }

            context.StandingPhotoLinks.Add(StandingLink(websiteLinkId, StandingPhotoLinkKind.Website, null, "pcls-website"));
            await context.SaveChangesAsync();
            Count("Organizations", ContactCount + 1);
            Count("Principals", 1);
            Count("StandingPhotoLinks", ContactCount + 1);
            Count("Staff accounts", IssuerCount);
        }

        /// <summary>
        /// <paramref name="count"/> more captures, half on each tab, with the
        /// Cases the Links we sent half are sent from: two links to a Case,
        /// and one archived Case to every twenty links.
        /// </summary>
        public async Task AddCapturesAsync(int count)
        {
            var linksWeSent = (count + 1) / 2;
            var liveCases = new List<(Guid Id, string Reference)>();
            var archivedCases = new List<(Guid Id, string Reference)>();
            await using (var context = await database.CreateContextAsync())
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                for (var index = 0; index < Math.Max(1, linksWeSent / 2); index++)
                {
                    liveCases.Add(AddCase(context, archived: false));
                }

                for (var index = 0; index < Math.Max(1, linksWeSent / 20); index++)
                {
                    archivedCases.Add(AddCase(context, archived: true));
                }

                await context.SaveChangesAsync();
            }

            Count("Cases", liveCases.Count + archivedCases.Count);
            Count("CaseWorkflows", liveCases.Count + archivedCases.Count);

            var sentInRun = 0;
            var comingInRun = 0;
            for (var start = 0; start < count; start += CapturesPerBatch)
            {
                await using var context = await database.CreateContextAsync();
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                for (var offset = 0; offset < Math.Min(CapturesPerBatch, count - start); offset++)
                {
                    var index = Captures++;
                    if (index % 2 == 0)
                    {
                        AddLinkWeSent(context, index / 2, sentInRun++, liveCases, archivedCases);
                    }
                    else
                    {
                        AddComingIn(context, index / 2, comingInRun++, liveCases);
                    }
                }

                await context.SaveChangesAsync();
            }
        }

        private void AddLinkWeSent(
            PegasusDbContext context,
            int tabIndex,
            int runIndex,
            List<(Guid Id, string Reference)> liveCases,
            List<(Guid Id, string Reference)> archivedCases)
        {
            var slot = tabIndex % StatePattern.Length;
            var intended = StatePattern[slot];
            var archived = slot == ArchivedCaseSlot;
            var target = archived ? archivedCases[runIndex % archivedCases.Count] : liveCases[runIndex / 2 % liveCases.Count];
            var quick = tabIndex % 5 == 4;
            var capture = NewCapture(
                tabIndex * 2,
                intended,
                PhotoCaptureOrigin.CaseLink,
                openedAsMade: false,
                expiredByTime: intended == PhotoCaptureState.Expired && !archived);
            capture.CaseId = target.Id;
            capture.CaseReference = target.Reference;
            capture.Destination = PhotoCaptureCodes.ToCode(PhotoCaptureDestination.Case);
            capture.Mode = PhotoCaptureCodes.ToCode(quick ? PhotoCaptureMode.Quick : PhotoCaptureMode.Guided);
            capture.QuickLinkFor = quick
                ? PhotoCaptureCodes.ToCode(tabIndex / 5 % 2 == 0 ? PhotoCaptureQuickLinkFor.Bodyshop : PhotoCaptureQuickLinkFor.AccidentManagement)
                : null;
            capture.IssuedByStaffId = issuers[tabIndex % issuers.Count];
            context.PhotoCaptureTokens.Add(new PhotoCaptureTokenEntity
            {
                TokenDigest = $"{capture.Id:N}{capture.Id:N}",
                CaptureId = capture.Id,
                Purpose = PhotoCaptureCodes.ToCode(PhotoLinkTokenPurpose.Email),
                IssuedAtUtc = capture.CreatedAtUtc,
                IssuedByStaffId = capture.IssuedByStaffId
            });
            Count("PhotoCaptureTokens", 1);
            var items = AddItemsAndBatch(context, capture, intended, submissionGroupId: null, repairer: false, archived);
            linksWeSentItems += items;
            Tally(linksWeSentStates, intended);
        }

        private void AddComingIn(
            PegasusDbContext context,
            int tabIndex,
            int runIndex,
            List<(Guid Id, string Reference)> liveCases)
        {
            var intended = StatePattern[tabIndex % StatePattern.Length];
            var photos = HasPhotos(intended);
            // Only a hand-off sent to the customer waits unopened or needs a
            // call; a staff or website capture is opened as it is made.
            var origin = intended is PhotoCaptureState.SentNotOpened or PhotoCaptureState.NeedsCall
                ? PhotoCaptureOrigin.StandingForwarded
                : (tabIndex % 4) switch
                {
                    0 or 1 => PhotoCaptureOrigin.StandingStaff,
                    2 => PhotoCaptureOrigin.StandingForwarded,
                    _ => PhotoCaptureOrigin.Website
                };
            var capture = NewCapture(
                tabIndex * 2 + 1,
                intended,
                origin,
                openedAsMade: origin != PhotoCaptureOrigin.StandingForwarded,
                expiredByTime: intended == PhotoCaptureState.Expired);
            var repairer = false;
            if (origin == PhotoCaptureOrigin.Website)
            {
                capture.StandingLinkId = websiteLinkId;
            }
            else
            {
                var contact = contacts[tabIndex % contacts.Count];
                capture.StandingLinkId = contact.LinkId;
                capture.OrganizationId = contact.OrganizationId;
                repairer = contact.IsRepairer;
            }

            Guid? submissionGroupId = null;
            if (photos && tabIndex % 3 == 0)
            {
                // Its registration was on a Case when it started: it files on the Case as it arrives.
                var target = liveCases[runIndex % liveCases.Count];
                capture.CaseId = target.Id;
                capture.CaseReference = target.Reference;
                capture.Destination = PhotoCaptureCodes.ToCode(PhotoCaptureDestination.Case);
            }
            else if (intended is PhotoCaptureState.Complete or PhotoCaptureState.SomethingMissing)
            {
                // Sent with no Case: its batch became a Vehicle images record.
                submissionGroupId = AddVehicleImagesRecord(context, capture, tabIndex);
                capture.SubmissionGroupId = submissionGroupId;
            }

            var items = AddItemsAndBatch(context, capture, intended, submissionGroupId, repairer, caseArchived: false);
            comingInItems += items;
            Tally(comingInStates, intended);
        }

        /// <summary>
        /// A capture in <paramref name="intended"/> at <see cref="Now"/>: its
        /// times follow the order a link lives through (made, opened, photos,
        /// sent), all before now; a link expired by time was made eight days
        /// ago and more.
        /// </summary>
        private PhotoCaptureEntity NewCapture(
            int index,
            PhotoCaptureState intended,
            PhotoCaptureOrigin origin,
            bool openedAsMade,
            bool expiredByTime)
        {
            var jitter = TimeSpan.FromMinutes(index % 2_880);
            var created = intended switch
            {
                PhotoCaptureState.Expired when expiredByTime => Now - TimeSpan.FromDays(8) - jitter,
                PhotoCaptureState.NeedsCall => Now - TimeSpan.FromDays(3) - jitter,
                _ => Now - TimeSpan.FromMinutes(30) - jitter
            };
            var opened = intended is PhotoCaptureState.SentNotOpened or PhotoCaptureState.NeedsCall
                || intended == PhotoCaptureState.Expired
                ? (DateTimeOffset?)null
                : openedAsMade ? created : created.AddMinutes(5);
            var letters = (char)('A' + index % 26);
            var second = (char)('A' + index / 26 % 26);
            var third = (char)('A' + index / 676 % 26);
            var capture = new PhotoCaptureEntity
            {
                Id = Guid.NewGuid(),
                Origin = PhotoCaptureCodes.ToCode(origin),
                Destination = PhotoCaptureCodes.ToCode(PhotoCaptureDestination.VehicleImages),
                NormalizedRegistration = $"P{letters}{index % 100:D2}{second}{third}X",
                IncidentDate = DateOnly.FromDateTime(created.UtcDateTime).AddDays(-10),
                CustomerName = $"Customer {index:D4}",
                Mobile = $"07700 9{index % 100_000:D5}",
                Email = index % 3 == 0 ? null : $"customer{index}@example.invalid",
                DetailsJson = HasPhotos(intended) ? detailsWithAreas : emptyDetails,
                Mode = PhotoCaptureCodes.ToCode(PhotoCaptureMode.Guided),
                LiveCameraOnly = true,
                OriginalsInBackground = true,
                CreatedAtUtc = created,
                ExpiresAtUtc = created.AddDays(LinkLifetimeDays),
                OpenedAtUtc = opened,
                Version = 1
            };
            if (intended == PhotoCaptureState.NeedsCall)
            {
                // Two reminders and nothing back since: the link needs a phone call.
                capture.ReminderCount = PhotoCaptureStatus.RemindersBeforeCall;
                capture.LastRemindedAtUtc = created.AddDays(2);
                capture.Version += PhotoCaptureStatus.RemindersBeforeCall;
            }
            else if (intended == PhotoCaptureState.Cancelled)
            {
                capture.RevokedAtUtc = Now.AddMinutes(-5);
                capture.Version++;
            }

            return capture;
        }

        /// <summary>
        /// The capture's 30 items when it has photos (the number plate left
        /// out for Something missing), its counters to match, its settled
        /// batch once sent, and the check that it reads as intended.
        /// </summary>
        private int AddItemsAndBatch(
            PegasusDbContext context,
            PhotoCaptureEntity capture,
            PhotoCaptureState intended,
            Guid? submissionGroupId,
            bool repairer,
            bool caseArchived)
        {
            List<ItemShape> shapes = !HasPhotos(intended) ? []
                : intended == PhotoCaptureState.SomethingMissing ? missingPlateShapes : fullShapes;
            if (shapes.Count > 0)
            {
                var first = (capture.OpenedAtUtc ?? capture.CreatedAtUtc).AddMinutes(1);
                var received = first;
                for (var ordinal = 0; ordinal < shapes.Count; ordinal++)
                {
                    received = first.AddSeconds(20 * ordinal);
                    context.PhotoCaptureItems.Add(Item(capture.Id, ordinal, shapes[ordinal], received, capture.CaseId));
                }

                capture.FirstItemAtUtc = first;
                capture.LastItemAtUtc = received;
                capture.AcceptedItemCount = shapes.Count;
                capture.AcceptedBytes = shapes.Sum(shape => shape.Length);
                capture.PhotoCount = shapes.Count(shape => PhotoCaptureStory.IsPhotographKind(shape.Kind));
                capture.DocumentCount = shapes.Count(shape => shape.Kind == PhotoCaptureItemKind.Paperwork);
                capture.VideoCount = shapes.Count(shape => shape.Kind == PhotoCaptureItemKind.Video);
                capture.Version += shapes.Count;
                Count("PhotoCaptureItems", shapes.Count);
            }

            if (intended is PhotoCaptureState.Complete or PhotoCaptureState.SomethingMissing)
            {
                var sent = capture.LastItemAtUtc!.Value.AddMinutes(2);
                var settled = sent.AddMinutes(1);
                capture.SentAtUtc = sent;
                capture.SettledAtUtc = settled;
                capture.SettledBatches = 1;
                capture.ImagesCompletedAtUtc = intended == PhotoCaptureState.Complete && capture.CaseId is not null ? settled : null;
                capture.Version += 2;
                context.PhotoCaptureBatches.Add(new PhotoCaptureBatchEntity
                {
                    CaptureId = capture.Id,
                    Batch = 1,
                    SubmissionGroupId = submissionGroupId,
                    CaseId = capture.CaseId,
                    ClosedAtUtc = settled,
                    ReachedAtUtc = settled
                });
                Count("PhotoCaptureBatches", 1);
            }

            context.PhotoCaptures.Add(capture);
            Count("PhotoCaptures", 1);

            // The seed is only a measure if each capture reads as intended
            // under the status rule the page itself evaluates.
            var mapped = EfPhotoCaptureStore.Map(capture);
            List<PhotoCaptureItem> items = shapes.Count == 0 ? []
                : intended == PhotoCaptureState.SomethingMissing ? missingPlateItems : fullItems;
            var missing = PhotoCaptureRequiredItems.Evaluate(
                mapped,
                PhotoCaptureItemPresence.Of(items),
                PhotoCaptureRequiredItems.VinRequired(mapped, repairer, handedOn: false));
            var photographs = items.Count(item => PhotoCaptureStory.IsPhotographKind(item.Kind));
            var state = PhotoCaptureStatus.Evaluate(mapped, missing, photographs, Now, caseArchived);
            if (state != intended)
            {
                throw new InvalidOperationException(
                    $"The seeded {capture.Origin} capture reads {state}, not the intended {intended}.");
            }

            return shapes.Count;
        }

        /// <summary>A sent standing capture's Vehicle images record: its origin receipt, its submission group and the record.</summary>
        private Guid AddVehicleImagesRecord(PegasusDbContext context, PhotoCaptureEntity capture, int tabIndex)
        {
            var channel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.PhotoCapture);
            var receiptId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var hash = $"{receiptId:N}{receiptId:N}";
            var at = capture.CreatedAtUtc.AddMinutes(20);
            context.IntakeReceipts.Add(new IntakeReceiptEntity
            {
                Id = receiptId, SourceFileName = fullShapes[0].FileName, MediaType = "image/jpeg", SourceLength = fullShapes[0].Length,
                SourceHash = hash, SourceChannel = channel, ExternalReceiptToken = $"pcls:{receiptId:N}", ReceivedAtUtc = at,
                ProcessedAtUtc = at, SourceReaderKey = "test", SourceReaderVersion = "1", Version = 1,
                Decision = "needs_sorting", DecisionReason = "test",
                EvidenceJson = emptyEvidence, FieldsJson = emptyFields, OcrCandidatesJson = emptyOcr
            });
            context.IntakeSubmissionGroups.Add(new IntakeSubmissionGroupEntity
            {
                Id = groupId, SourceChannel = channel, SubmissionToken = $"pcls:{groupId:N}",
                ExpectedMemberCount = fullShapes.Count, Actor = "photo-capture", ReceivedAtUtc = at, Version = 1,
                DeclaredRegistration = capture.NormalizedRegistration
            });
            context.ImageIntakes.Add(new ImageIntakeEntity
            {
                Id = Guid.NewGuid(), OriginReceiptId = receiptId, SourceChannel = channel,
                ExternalReceiptToken = $"pcls:{receiptId:N}", SourceHash = hash, EvaluationRevisionId = Guid.NewGuid(),
                SubmissionGroupId = groupId, NormalizedVehicleRegistration = capture.NormalizedRegistration!,
                ImageIntakeReference = $"{Registry}-{tabIndex:D5}", CreatedAtUtc = at, CreatedByActorKind = "SystemWorker",
                CreatedByActorSubjectId = "system-worker:image-intake", Reason = "Registered.",
                CreationOperationKey = $"pcls:{groupId:N}", RequestFingerprint = hash, LifecycleState = "registered",
                LifecycleVersion = 1
            });
            Count("IntakeReceipts", 1);
            Count("IntakeSubmissionGroups", 1);
            Count("ImageIntakes", 1);
            return groupId;
        }

        private (Guid Id, string Reference) AddCase(PegasusDbContext context, bool archived)
        {
            var caseId = Guid.NewGuid();
            var sequence = nextCaseSequence++;
            var reference = $"{Registry}31{sequence:D4}";
            context.Cases.Add(new CaseEntity
            {
                Id = caseId, PrincipalId = principalId, SequenceLineageId = lineageId, Year = 2031, Sequence = sequence,
                Reference = reference, Type = "inspection", InitialState = nameof(CaseLifecycleState.NotReady),
                CustodyState = "Pending", CreatedAtUtc = Now.AddDays(-10), Version = 1, ConcurrencyToken = Guid.NewGuid()
            });
            context.CaseWorkflows.Add(new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = archived ? nameof(CaseLifecycleState.PrincipalCancelled) : nameof(CaseLifecycleState.NotReady),
                Version = 1,
                ConcurrencyToken = Guid.NewGuid(),
                ArchivedAtUtc = archived ? Now.AddDays(-1) : null,
                ArchivedByKind = archived ? "Staff" : null,
                ArchivedBySubjectId = archived ? "staff:test" : null,
                ArchivedByRolesJson = archived ? "[]" : null,
                ArchiveReason = archived ? "Archived for the scale test." : null
            });
            return (caseId, reference);
        }

        private static StandingPhotoLinkEntity StandingLink(Guid id, StandingPhotoLinkKind kind, Guid? organizationId, string slug) => new()
        {
            Id = id,
            Kind = PhotoCaptureCodes.ToCode(kind),
            OrganizationId = organizationId,
            Slug = slug,
            ForwardedMode = PhotoCaptureCodes.ToCode(PhotoCaptureMode.Guided),
            LiveCameraOnly = true,
            LinkLifetimeDays = LinkLifetimeDays,
            OriginalsInBackground = true,
            CreatedAtUtc = Now.AddDays(-30),
            CreatedByKind = "Staff",
            CreatedBySubjectId = "staff:test",
            Version = 1
        };

        private static PhotoCaptureItemEntity Item(Guid captureId, int ordinal, ItemShape shape, DateTimeOffset receivedAtUtc, Guid? caseId) => new()
        {
            Id = Guid.NewGuid(),
            CaptureId = captureId,
            ClientItemId = Guid.NewGuid(),
            Kind = PhotoCaptureCodes.ToCode(shape.Kind),
            Area = shape.Area,
            Stage = PhotoCaptureCodes.ToCode(shape.Stage),
            Position = shape.Position,
            Batch = 1,
            Ordinal = ordinal,
            StoryGroup = shape.Group,
            StorySequence = shape.Sequence,
            FileName = shape.FileName,
            MediaType = shape.MediaType,
            TakenAtUtc = receivedAtUtc.AddSeconds(-10),
            LiveCamera = PhotoCaptureStory.IsPhotographKind(shape.Kind),
            InReportOnArrival = PhotoCaptureStory.StartsInReport(shape.Kind),
            ContentHash = $"{captureId:N}{ordinal:x32}",
            ContentLength = shape.Length,
            // Received: its upload reached intake, so the lists count it.
            StagedReceiptId = Guid.NewGuid(),
            ReceivedAtUtc = receivedAtUtc,
            DeclaredCaseId = caseId
        };

        /// <summary>
        /// The 30 items of a capture with photos: eight overviews; damage in
        /// two marked areas (overview, closer, two close-ups, and an extra in
        /// the first); the number plate (left out when
        /// <paramref name="withNumberPlate"/> is false, for a fourth scene
        /// photo); the VIN; the dashboard; three scene, three other-vehicle
        /// and three paperwork items; and a video.
        /// </summary>
        private static List<ItemShape> Shapes(bool withNumberPlate)
        {
            var areas = PhotoCaptureAreas.Derive(MarkedZones);
            if (areas.Count != 2)
            {
                throw new InvalidOperationException("The two marked zones must make two damaged areas.");
            }

            var shapes = new List<ItemShape>(30);
            for (var position = 1; position <= PhotoCaptureStory.OverviewCount; position++)
            {
                Add(PhotoCaptureItemKind.Overview, null, null, position);
            }

            Add(PhotoCaptureItemKind.Damage, areas[0].Key, PhotoCaptureStage.Overview, 1);
            Add(PhotoCaptureItemKind.Damage, areas[0].Key, PhotoCaptureStage.Closer, 1);
            Add(PhotoCaptureItemKind.Damage, areas[0].Key, PhotoCaptureStage.CloseUp, 1);
            Add(PhotoCaptureItemKind.Damage, areas[0].Key, PhotoCaptureStage.CloseUp, 2);
            Add(PhotoCaptureItemKind.Damage, areas[0].Key, PhotoCaptureStage.Extra, 1);
            Add(PhotoCaptureItemKind.Damage, areas[1].Key, PhotoCaptureStage.Overview, 1);
            Add(PhotoCaptureItemKind.Damage, areas[1].Key, PhotoCaptureStage.Closer, 1);
            Add(PhotoCaptureItemKind.Damage, areas[1].Key, PhotoCaptureStage.CloseUp, 1);
            Add(PhotoCaptureItemKind.Damage, areas[1].Key, PhotoCaptureStage.CloseUp, 2);
            if (withNumberPlate)
            {
                Add(PhotoCaptureItemKind.NumberPlate, null, null, 1);
            }

            Add(PhotoCaptureItemKind.Vin, null, null, 1);
            Add(PhotoCaptureItemKind.Dashboard, null, null, 1);
            for (var position = 1; position <= (withNumberPlate ? 3 : 4); position++)
            {
                Add(PhotoCaptureItemKind.Scene, null, null, position);
            }

            for (var position = 1; position <= 3; position++)
            {
                Add(PhotoCaptureItemKind.OtherVehicle, null, null, position);
                Add(PhotoCaptureItemKind.Paperwork, null, null, position);
            }

            Add(PhotoCaptureItemKind.Video, null, null, 1);
            if (shapes.Count != ItemsPerCaptureWithPhotos)
            {
                throw new InvalidOperationException($"A capture with photos holds {ItemsPerCaptureWithPhotos} items, not {shapes.Count}.");
            }

            return shapes;

            void Add(PhotoCaptureItemKind kind, string? area, PhotoCaptureStage? stage, int position)
            {
                var group = PhotoCaptureStory.Group(kind, area, MarkedZones);
                var sequence = PhotoCaptureStory.Sequence(kind, stage, position);
                var (mediaType, length) = kind switch
                {
                    PhotoCaptureItemKind.Paperwork => ("application/pdf", 120_000L),
                    PhotoCaptureItemKind.Video => (IntakeUploadFilePolicy.Mp4MediaType, 8_000_000L),
                    _ => ("image/jpeg", 250_000L)
                };
                var fileName = PhotoCaptureStory.FileName(
                    kind,
                    area,
                    stage,
                    position,
                    group,
                    sequence,
                    mediaType,
                    kind == PhotoCaptureItemKind.Paperwork ? $"estimate-{position}.pdf" : null);
                shapes.Add(new(kind, area, stage, position, group, sequence, fileName, mediaType, length));
            }
        }

        private static bool HasPhotos(PhotoCaptureState state) =>
            state is PhotoCaptureState.PhotosComingIn or PhotoCaptureState.SomethingMissing or PhotoCaptureState.Complete;

        private static void Tally(Dictionary<PhotoCaptureState, int> states, PhotoCaptureState state) =>
            states[state] = states.GetValueOrDefault(state) + 1;

        private void Count(string table, int added) => rows[table] = rows.GetValueOrDefault(table) + added;
    }

    /// <summary>One item of the 30-item template, with its story place and name worked out by <see cref="PhotoCaptureStory"/>.</summary>
    private sealed record ItemShape(
        PhotoCaptureItemKind Kind,
        string? Area,
        PhotoCaptureStage? Stage,
        int Position,
        int Group,
        int Sequence,
        string FileName,
        string MediaType,
        long Length);
}
