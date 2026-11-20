using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Assessment;
using Pegasus.Infrastructure.Glass;
using Pegasus.Web.Pages.Integrations.Glass;
using static Pegasus.IntegrationTests.GlassRepairEstimateCallbackWebTests;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Pegasus helper's key hand-off and poll (R1170) through the real
/// routes and the host's own composition: the launch is the Glass's suites'
/// scripted provider, the session store is the real one on LocalDB, and the
/// editor frame's address is built by the editor client's own address
/// builder. Every key and token is made up.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class GlassHelpWebTests
{
    private const string KeyRoute = HelpModel.BasePath + "/Key";
    private const string PollRoute = HelpModel.BasePath + "/Poll";
    private const string ExtensionOrigin = "chrome-extension://abcdefghijklmnopabcdefghijklmnop";
    private const string FirstKey = "TESTKEYD1000000000000000000001";
    private const string SecondKey = "TESTKEYD1000000000000000000002";

    /// <summary>
    /// A fresh holder has no key, so the first poll says one is needed. The
    /// owner's hand-off is held, a second one replaces it, and the poll says
    /// it is held. Nothing is asked of Glass's, the cookie-less poll sets no
    /// cookie, and no answer carries a CORS header.
    /// </summary>
    [Fact]
    public async Task TheOwnersKeyIsHeldAndReplacedAndThePollSaysSo()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        var session = Assert.Single(await workspace.SessionsAsync());
        var keys = workspace.Services.GetRequiredService<GlassHelpKeys>();
        var askedOfGlass = workspace.Mva.Requests.Count;

        using (var fresh = await workspace.Client.SendAsync(Poll(correlation)))
        {
            using var answer = await AnswerAsync(fresh, HttpStatusCode.OK);
            Assert.Equal("needed", answer.RootElement.GetProperty("key").GetString());
            Assert.Equal("off", answer.RootElement.GetProperty("help").GetString());
            Assert.Equal(0, answer.RootElement.GetProperty("refresh").GetInt64());
            Assert.Equal(10_000, answer.RootElement.GetProperty("next").GetInt32());
            Assert.False(fresh.Headers.Contains("Set-Cookie"));
            Assert.True(fresh.Headers.CacheControl?.NoStore);
        }

        using (var held = await workspace.Client.SendAsync(Key(FrameFor(correlation, FirstKey))))
        {
            using var answer = await AnswerAsync(held, HttpStatusCode.OK);
            Assert.Equal("held", answer.RootElement.GetProperty("key").GetString());
            Assert.Equal("off", answer.RootElement.GetProperty("help").GetString());
            Assert.Equal(10_000, answer.RootElement.GetProperty("next").GetInt32());
        }
        Assert.Equal(FirstKey, keys.Session(session.Id)?.Key);

        using (var replaced = await workspace.Client.SendAsync(Key(FrameFor(correlation, SecondKey))))
        {
            using var answer = await AnswerAsync(replaced, HttpStatusCode.OK);
        }
        var heldSession = keys.Session(session.Id);
        Assert.Equal(SecondKey, heldSession?.Key);
        Assert.Equal("https://ere.test/ere/aco/", heldSession?.BaseAddress);

        using (var polled = await workspace.Client.SendAsync(Poll(correlation)))
        {
            using var answer = await AnswerAsync(polled, HttpStatusCode.OK);
            Assert.Equal("held", answer.RootElement.GetProperty("key").GetString());
            Assert.False(polled.Headers.Contains("Set-Cookie"));
        }

        Assert.Equal(askedOfGlass, workspace.Mva.Requests.Count);
        Assert.Equal(GlassRepairEstimateSessionState.Active, Assert.Single(await workspace.SessionsAsync()).State);
    }

    /// <summary>
    /// With help turned on for the session (R1170) the poll says so and asks
    /// again every 2 seconds; when Pegasus asks the window to show its list
    /// again the poll carries the raised count, and the window's next poll
    /// reports it shown (seen), which a help act waits for.
    /// </summary>
    [Fact]
    public async Task WithHelpOnThePollCarriesTheRefreshAndRecordsWhatTheWindowShowed()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        var session = Assert.Single(await workspace.SessionsAsync());
        var keys = workspace.Services.GetRequiredService<GlassHelpKeys>();
        using (var held = await workspace.Client.SendAsync(Key(FrameFor(correlation, FirstKey))))
        {
            using var answer = await AnswerAsync(held, HttpStatusCode.OK);
        }
        await using (var scope = workspace.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IGlassSessionHelpStore>().SetHelpAsync(
                session.Id,
                Pegasus.Core.Identity.ActionActor.Staff(session.PegasusUserId, [Pegasus.Core.Identity.StaffRole.Engineer]),
                true,
                CancellationToken.None));
        }

        Assert.Equal(1, keys.Refresh(session.Id));
        using (var polled = await workspace.Client.SendAsync(Poll(correlation)))
        {
            using var answer = await AnswerAsync(polled, HttpStatusCode.OK);
            Assert.Equal("on", answer.RootElement.GetProperty("help").GetString());
            Assert.Equal(1, answer.RootElement.GetProperty("refresh").GetInt64());
            Assert.Equal(2_000, answer.RootElement.GetProperty("next").GetInt32());
        }
        using (var shown = await workspace.Client.SendAsync(Poll(correlation, seen: 1)))
        {
            using var answer = await AnswerAsync(shown, HttpStatusCode.OK);
        }

        Assert.True(await keys.ShownAsync(session.Id, 1, CancellationToken.None));
    }

    /// <summary>
    /// Each check refuses before anything is kept: no staff session is 401,
    /// another staff member, a page's origin, no origin and a form-shaped post
    /// are 403, an unknown token 404, a frame that is not this host's editor
    /// 400, and an unknown protocol 426. None answers with a CORS header.
    /// </summary>
    [Fact]
    public async Task EveryCheckRefusesBeforeAnythingIsKept()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        var session = Assert.Single(await workspace.SessionsAsync());
        var keys = workspace.Services.GetRequiredService<GlassHelpKeys>();
        var frame = FrameFor(correlation, FirstKey);

        var signedOut = Key(frame);
        signedOut.Headers.Add("X-Test-Anonymous", "true");
        var stranger = Key(frame);
        stranger.Headers.Add("X-Test-Subject", Guid.NewGuid().ToString("D"));
        var refusals = new (string Why, HttpRequestMessage Request, HttpStatusCode Status)[]
        {
            ("no staff session", signedOut, HttpStatusCode.Unauthorized),
            ("another staff member", stranger, HttpStatusCode.Forbidden),
            ("a page on Glass's origin", Key(frame, origin: "https://ere.test"), HttpStatusCode.Forbidden),
            ("no origin", Key(frame, origin: null), HttpStatusCode.Forbidden),
            ("a form-shaped post", Key(frame, mediaType: "application/x-www-form-urlencoded"), HttpStatusCode.Forbidden),
            ("a plain-text post", Key(frame, mediaType: "text/plain"), HttpStatusCode.Forbidden),
            ("an unknown token", Key(FrameFor(NewCorrelation(), FirstKey)), HttpStatusCode.NotFound),
            ("a frame on another host", Key(frame.Replace("https://ere.test/", "https://other.test/", StringComparison.Ordinal)), HttpStatusCode.BadRequest),
            ("no frame", Post(KeyRoute, new { v = HelpModel.Protocol }), HttpStatusCode.BadRequest),
            ("an unknown protocol", Post(KeyRoute, new { v = HelpModel.Protocol + 1, frame, version = "9.0.0" }), HttpStatusCode.UpgradeRequired),
            ("a poll for an unknown token", Poll(NewCorrelation()), HttpStatusCode.NotFound),
            ("a poll that names no token", Poll("not-a-token"), HttpStatusCode.BadRequest),
            ("a poll from a page", Poll(correlation, origin: "https://ere.test"), HttpStatusCode.Forbidden),
        };

        var wrong = new List<string>();
        foreach (var (why, request, status) in refusals)
        {
            using (request)
            using (var refused = await workspace.Client.SendAsync(request))
            {
                if (refused.StatusCode != status)
                {
                    wrong.Add($"{why}: {refused.StatusCode}, not {status}");
                }
                AssertNoCors(refused);
            }
        }

        Assert.Empty(wrong);
        Assert.Null(keys.Session(session.Id));
    }

    /// <summary>
    /// Once the session is not Active, Save &amp; Exit having claimed the
    /// return, the poll and the hand-off both answer 409 and the key is gone.
    /// </summary>
    [Fact]
    public async Task AnEndedSessionAnswersConflictAndDropsTheKey()
    {
        await using var workspace = await Workspace.CreateAsync();
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        var session = Assert.Single(await workspace.SessionsAsync());
        var keys = workspace.Services.GetRequiredService<GlassHelpKeys>();
        using (var held = await workspace.Client.SendAsync(Key(FrameFor(correlation, FirstKey))))
        {
            Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        }
        using (var returned = await workspace.ReturnAsync(correlation))
        {
            await AssertHandsBackToTheEstimateSectionAsync(returned, workspace.CaseId);
        }

        using (var polled = await workspace.Client.SendAsync(Poll(correlation)))
        {
            using var answer = await AnswerAsync(polled, HttpStatusCode.Conflict);
            Assert.Equal("ended", answer.RootElement.GetProperty("error").GetString());
        }
        Assert.Null(keys.Session(session.Id));
        using (var again = await workspace.Client.SendAsync(Key(FrameFor(correlation, SecondKey))))
        {
            using var answer = await AnswerAsync(again, HttpStatusCode.Conflict);
        }

        Assert.Null(keys.Session(session.Id));
    }

    /// <summary>A host that does not reach Glass's has neither route.</summary>
    [Fact]
    public async Task NeitherRouteExistsWhereGlassIsNotComposed()
    {
        await using var workspace = await Workspace.CreateAsync(liveGlass: false);
        var correlation = NewCorrelation();

        using var key = await workspace.Client.SendAsync(Key(FrameFor(correlation, FirstKey)));
        using var poll = await workspace.Client.SendAsync(Poll(correlation));

        Assert.Equal(HttpStatusCode.NotFound, key.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, poll.StatusCode);
    }

    /// <summary>
    /// The host log says a key was handed over, for which session, and that
    /// an unknown protocol was refused; it never carries the key, the token,
    /// the frame address or the return address.
    /// </summary>
    [Fact]
    public async Task TheLogNamesTheSessionAndNeverTheKeyOrTheToken()
    {
        var logs = new CapturingLoggerProvider();
        await using var workspace = await Workspace.CreateAsync(logs: logs);
        await workspace.ClaimLeaseAsync();
        var correlation = await workspace.LaunchAndReadCorrelationAsync();
        var session = Assert.Single(await workspace.SessionsAsync());
        var frame = FrameFor(correlation, FirstKey);
        // The launch itself is the gateway's; what follows is this page's.
        var fromHere = logs.Entries.Count;

        using (var held = await workspace.Client.SendAsync(Key(frame)))
        {
            Assert.Equal(HttpStatusCode.OK, held.StatusCode);
        }
        using (var polled = await workspace.Client.SendAsync(Poll(correlation)))
        {
            Assert.Equal(HttpStatusCode.OK, polled.StatusCode);
        }
        using (var unknown = await workspace.Client.SendAsync(
            Post(KeyRoute, new { v = HelpModel.Protocol + 1, frame, version = FirstKey })))
        {
            Assert.Equal(HttpStatusCode.UpgradeRequired, unknown.StatusCode);
        }

        var entries = logs.Entries;
        var help = entries.Where(entry => entry.Category == typeof(HelpModel).FullName).ToList();
        Assert.Contains(help, entry => entry.Message.Contains(session.Id.ToString("D"), StringComparison.Ordinal));
        Assert.Contains(help, entry => entry.Message.Contains("unreadable", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.Message.Contains(FirstKey, StringComparison.Ordinal));
        Assert.DoesNotContain(entries, entry => entry.Message.Contains(frame, StringComparison.Ordinal));
        Assert.DoesNotContain(entries.Skip(fromHere), entry => entry.Message.Contains(correlation, StringComparison.Ordinal));
    }

    /// <summary>The editor's main-frame address for the session's return token, as the browser holds it.</summary>
    private static string FrameFor(string correlation, string key) =>
        new GlassEditorAddresses(
            new GlassEditorSession(
                "https://ere.test/ere/aco/",
                "../acolib/aco_call_xml.php?caller=https://localhost:7139" + CallbackRoute + correlation,
                key,
                GlassEditorFixture.NoCookies),
            TimeProvider.System).Frame;

    private static HttpRequestMessage Key(
        string frame, string? origin = ExtensionOrigin, string mediaType = "application/json") =>
        Post(KeyRoute, new { v = HelpModel.Protocol, frame, version = "1.0.0" }, origin, mediaType);

    /// <summary>A poll as the helper sends it: without the staff cookie.</summary>
    private static HttpRequestMessage Poll(string token, string? origin = ExtensionOrigin, long seen = 0)
    {
        var request = Post(PollRoute, new { v = HelpModel.Protocol, token, seen, version = "1.0.0" }, origin);
        request.Headers.Add("X-Test-Anonymous", "true");
        return request;
    }

    private static HttpRequestMessage Post(
        string route, object body, string? origin = ExtensionOrigin, string mediaType = "application/json")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, mediaType),
        };
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return request;
    }

    private static async Task<JsonDocument> AnswerAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        AssertNoCors(response);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static void AssertNoCors(HttpResponseMessage response) =>
        Assert.DoesNotContain(
            response.Headers.Select(header => header.Key)
                .Concat(response.Content.Headers.Select(header => header.Key)),
            name => name.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase));

    private sealed record LogEntry(string Category, string Message);

    /// <summary>Every log entry the host writes, by category.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<LogEntry> entries = new();

        public IReadOnlyList<LogEntry> Entries => [.. entries];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, entries);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(new(category, formatter(state, exception) + " " + exception));
        }
    }
}
