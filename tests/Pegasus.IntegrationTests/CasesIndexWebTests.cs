using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CasesIndexWebTests
{
    [Fact]
    public async Task SearchUsesAuthorizedCoreQueryAndPreservesEveryFilterInPagingUrl()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);
        var engineerId = Guid.NewGuid();
        var path = "/Search?case=QDOS3100042&registration=AB12CDE&claimant=Claimant&claimNumber=CLM42"
            + $"&principal=QDOS&state=Review&engineerId={engineerId:D}"
            + "&receivedDate=2031-05-01&instructionDate=2031-05-02"
            + "&fromDate=2031-04-01&toDate=2031-05-31&origin=Email&query=needle&page=2";

        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        var query = Assert.IsType<SearchCasesQuery>(search.Query);
        Assert.Equal(ActorKind.Staff, query.Actor.Kind);
        Assert.NotEmpty(query.Actor.Roles);
        Assert.Equal(2, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Equal("QDOS3100042", query.Filters.CaseReference);
        Assert.Equal("AB12CDE", query.Filters.Registration);
        Assert.Equal("Claimant", query.Filters.Claimant);
        Assert.Equal("CLM42", query.Filters.ClaimNumber);
        Assert.Equal("QDOS", query.Filters.Principal);
        Assert.Equal(CaseLifecycleState.Review, query.Filters.State);
        Assert.Equal(engineerId, query.Filters.EngineerId);
        Assert.Equal(new DateOnly(2031, 5, 1), query.Filters.ReceivedDate);
        Assert.Equal(new DateOnly(2031, 5, 2), query.Filters.InstructionDate);
        Assert.Equal(new DateOnly(2031, 4, 1), query.Filters.FromDate);
        Assert.Equal(new DateOnly(2031, 5, 31), query.Filters.ToDate);
        Assert.Equal("Email", query.Filters.Origin);
        Assert.Equal("needle", query.Filters.Query);
        Assert.Contains($"href=\"/Cases/{search.CaseId:D}\"", html, StringComparison.Ordinal);

        // The ported page: selectable rows, a server-rendered Selected Case
        // pane, and the D3 terminal chip on the closed result.
        Assert.Contains("Selected Case", html, StringComparison.Ordinal);
        Assert.Contains("data-preview-target", html, StringComparison.Ordinal);
        Assert.Contains($"data-select-id=\"{search.CaseId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-select-id=\"{search.ClosedCaseId:D}\"", html, StringComparison.Ordinal);
        // The rendered bytes, not the source literal: the framework's HTML
        // encoder writes every non-ASCII character as a numeric reference, so
        // the settled "Closed · outcome" chip reaches the browser with its
        // separator encoded.
        Assert.Contains("Closed &#xB7; Created in error", html, StringComparison.Ordinal);

        var next = Regex.Match(
            html,
            "<a[^>]*href=\"(?<href>[^\"]+)\"[^>]*>Next</a>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(next.Success, "The bounded search result must render a next-page URL.");
        var href = Uri.UnescapeDataString(WebUtility.HtmlDecode(next.Groups["href"].Value));
        foreach (var expected in new[]
                 {
                     "case=QDOS3100042", "registration=AB12CDE", "claimant=Claimant",
                     "claimNumber=CLM42", "principal=QDOS", "state=Review",
                     $"engineerId={engineerId:D}", "receivedDate=2031-05-01",
                     "instructionDate=2031-05-02", "fromDate=2031-04-01", "toDate=2031-05-31",
                     "origin=Email", "query=needle", "page=3"
                 })
        {
            Assert.Contains(expected, href, StringComparison.OrdinalIgnoreCase);
        }
        Assert.DoesNotContain("total", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyAndUnavailableQueriesRenderDistinctNonLeakingStates()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { ReturnEmpty = true };
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        using var emptyResponse = await client.GetAsync("/Search?principal=QDOS");
        var emptyHtml = await emptyResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        Assert.Contains("No matching cases", emptyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("unauthorised", emptyHtml, StringComparison.OrdinalIgnoreCase);

        search.ThrowUnavailable = true;
        using var failedResponse = await client.GetAsync("/Search?principal=QDOS");
        var failedHtml = await failedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedResponse.StatusCode);
        Assert.Contains("Cases are unavailable", failedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), failedHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectedRowPreviewsItsFactsServerSide()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases();
        using var factory = Configure(baseFactory, search);
        using var client = CreateClient(factory);

        // ?selected= reads the named row server-side: its facts, its D3
        // terminal chip and its own Case link, with Copy Case/PO beside the
        // swapped region where the shell's copy binding can reach it.
        using var response = await client.GetAsync($"/Search?selected={search.ClosedCaseId:D}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = Regex.Match(
            html,
            "<tr[^>]*data-select-id=\"" + search.ClosedCaseId.ToString("D") + "\"[^>]*aria-selected=\"(?<selected>[^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(row.Success, "The selected row must carry its selection state.");
        Assert.Equal("true", row.Groups["selected"].Value);

        Assert.Contains("Selected Case", html, StringComparison.Ordinal);
        Assert.Contains("Inspection", html, StringComparison.Ordinal);
        Assert.Contains("QDOS3100043 &#xB7; AB12CDE", html, StringComparison.Ordinal);
        Assert.Contains("Rear-end impact at a roundabout", html, StringComparison.Ordinal);
        Assert.Contains("Provider reference", html, StringComparison.Ordinal);
        Assert.Contains("CLM43", html, StringComparison.Ordinal);
        Assert.Contains("Unassigned", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Cases/{search.ClosedCaseId:D}\"", html, StringComparison.Ordinal);
        Assert.Contains("data-copy-target=\"search-copy-reference\"", html, StringComparison.Ordinal);
        Assert.Contains("Copy Case/PO", html, StringComparison.Ordinal);
        // Copy Case/PO and the refresh form's selected row live outside the
        // region the shell swaps, so every row carries the reference the page
        // moves them to when the selection changes without a round trip.
        Assert.Contains("data-copy-reference=\"QDOS3100042\"", html, StringComparison.Ordinal);
        Assert.Contains("data-copy-reference=\"QDOS3100043\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"selected\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExactImageReferenceResultCarriesItsRecordedLifecycleState()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { ReturnEmpty = true };
        var images = new RecordingImageIntakeQueries
        {
            State = ImageInitiatedCaseState.MergedIntoInstructionCase
        };
        using var factory = Configure(baseFactory, search, images);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"/Search?query={RecordingImageIntakeQueries.Reference}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The exact-reference lookup used to drop the record's state and take
        // ImageIntakeSummary's AwaitingInstruction default, so a merged record
        // was listed as still awaiting its instruction.
        Assert.Contains("Merged into Instruction-initiated Case", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Awaiting definitive instruction", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableImageQueryRendersTheFailureNoticeNotAnEmptyList()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var search = new RecordingSearchCases { ReturnEmpty = true };
        var images = new RecordingImageIntakeQueries { ThrowUnavailable = true };
        using var factory = Configure(baseFactory, search, images);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/Search?kind=images&registration=AB12CDE");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("Cases are unavailable", html, StringComparison.Ordinal);
        // A failed image query answered "no vehicle images match these
        // filters", which is a claim about the estate the failed read cannot
        // make.
        Assert.DoesNotContain("No vehicle images match these filters.", html, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), html, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Configure(
        IntakeWebApplicationFactory baseFactory,
        RecordingSearchCases search,
        RecordingImageIntakeQueries? images = null) => baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISearchCases>();
                services.AddSingleton<ISearchCases>(search);
                if (images is not null)
                {
                    services.RemoveAll<IImageIntakeQueries>();
                    services.AddSingleton<IImageIntakeQueries>(images);
                }
            }));

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private sealed class RecordingSearchCases : ISearchCases
    {
        public Guid CaseId { get; } = Guid.NewGuid();

        /// <summary>A second result in a D3 terminal state, with the CASE-026 projection fields.</summary>
        public Guid ClosedCaseId { get; } = Guid.NewGuid();

        public SearchCasesQuery? Query { get; private set; }

        public bool ReturnEmpty { get; set; }

        public bool ThrowUnavailable { get; set; }

        public Task<SearchCasesResult> ExecuteAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            IReadOnlyList<CaseSearchItem> items = ReturnEmpty
                ? []
                :
                [
                    new(
                        CaseId,
                        "QDOS3100042",
                        null,
                        CaseType.Inspection,
                        "QDOS",
                        CaseLifecycleState.Review,
                        query.Filters.EngineerId,
                        "AB12CDE",
                        "Claimant",
                        "CLM42",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero),
                        new DateOnly(2031, 5, 2),
                        "Email",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero)),
                    new(
                        ClosedCaseId,
                        "QDOS3100043",
                        null,
                        CaseType.Inspection,
                        "QDOS",
                        CaseLifecycleState.CreatedInError,
                        null,
                        "AB12CDE",
                        "Claimant",
                        "CLM43",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero),
                        new DateOnly(2031, 5, 2),
                        "Email",
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero))
                    {
                        VehicleMake = "Ford",
                        VehicleModel = "Fiesta",
                        AccidentCircumstances = "Rear-end impact at a roundabout"
                    }
                ];
            return Task.FromResult(new SearchCasesResult(items, query.Page, query.PageSize, true, true));
        }
    }

    /// <summary>
    /// The Image-intake reads this page makes: an exact reference lookup and a
    /// registration search. Only those two are exercised here; the rest of the
    /// port is not reached by /Search and stays unimplemented rather than
    /// answering with invented records.
    /// </summary>
    private sealed class RecordingImageIntakeQueries : IImageIntakeQueries
    {
        public const string Reference = "AB12CDE-01";

        public Guid Id { get; } = Guid.NewGuid();

        public ImageInitiatedCaseState State { get; set; } =
            ImageInitiatedCaseState.AwaitingInstruction;

        public bool ThrowUnavailable { get; set; }

        public Task<ImageIntakeDetail?> GetByReferenceAsync(
            string imageIntakeReference,
            CancellationToken cancellationToken)
        {
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            return Task.FromResult<ImageIntakeDetail?>(
                string.Equals(imageIntakeReference, Reference, StringComparison.Ordinal)
                    ? new ImageIntakeDetail(
                        new ImageIntakeRecord(
                            Id,
                            new ImageIntakeOrigin(
                                Guid.NewGuid(),
                                new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, "token"),
                                "hash",
                                Guid.NewGuid()),
                            "AB12CDE",
                            Reference,
                            State),
                        new DateTimeOffset(2031, 5, 1, 10, 0, 0, TimeSpan.Zero),
                        null,
                        null)
                    : null);
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
            string normalizedVehicleRegistration,
            CancellationToken cancellationToken)
        {
            if (ThrowUnavailable)
            {
                throw new InvalidOperationException("sensitive store failure");
            }

            return Task.FromResult<IReadOnlyList<ImageIntakeSummary>>([]);
        }

        public Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
            bool? associated,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
            Guid intakeReceiptId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
            IReadOnlyCollection<Guid> intakeReceiptIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
