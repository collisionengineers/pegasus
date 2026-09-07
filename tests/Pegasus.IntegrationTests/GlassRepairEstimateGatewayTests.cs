using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Glass;
using Pegasus.Infrastructure.Persistence;
using static Pegasus.IntegrationTests.GlassProviderFixture;

namespace Pegasus.IntegrationTests;

/// <summary>
/// CASE-047 B04: the Glass's repair-estimate gateway against a scripted
/// transport. Every stage the provider answers is played back from the shapes
/// the supplied captures record — the byte-order marked JSON, the login
/// redirect, the candidate fragment, the <c>start-ere</c> launch URL and the
/// <c>ere_callback_xml</c> relay — so the checks that matter (an application
/// failure inside an HTTP 200, an off-origin redirect, an ambiguous export) are
/// proven without a single live call.
///
/// <para>
/// Nothing here uses a real credential, registration, VIN, name or address: the
/// account is a synthetic name, the vehicle is the documented estate
/// registration AB12CDE, and the export is the parser suite's own synthetic
/// <c>&lt;Estimation&gt;</c> fixture.
/// </para>
/// </summary>
public sealed class GlassRepairEstimateGatewayTests
{
    private static readonly DateTimeOffset StartUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    /// <summary>The export and its embedded calculation sheet, in that order.</summary>
    private static readonly string[] RetainedMediaTypes = ["application/xml", "application/pdf"];

    // ---------------------------------------------------------------- launch

    [Fact]
    public async Task ALaunchOpensTheEstimatorAtPegasusOwnCallbackAndKeepsTheProviderSessionToItself()
    {
        var harness = Harness.Create();

        var session = await harness.LaunchAsync();

        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Equal(VehicleId, session.ProviderVehicleId);
        Assert.Equal(EreId, session.ProviderEstimateId);
        Assert.Null(session.FailureCode);

        var estimator = await harness.Gateway.GetEstimatorUrlAsync(
            harness.Engineer, session.Id, CancellationToken.None);
        Assert.NotNull(estimator);
        Assert.Equal(EstimatorBase.Host, estimator.Host);
        var query = QueryOf(estimator);
        Assert.Equal(CallbackBase.Host, new Uri(query["caller"]).Host);
        Assert.StartsWith(
            CallbackBase.AbsoluteUri + GlassRepairEstimateOptions.CallbackPath,
            query["caller"],
            StringComparison.Ordinal);
        // Only the caller moved: every other launch segment is the provider's.
        Assert.Equal("1788529734", query["WorkTime"]);
        Assert.Equal("D:/in.xml", query["inURI"]);
        Assert.Equal("D:/out.xml", query["outURI"]);
        Assert.Equal("glassnet", query["ucode"]);
        Assert.Equal("Test", query["scode"]);
        Assert.Equal("1005_1005_powered_by_eucomp", query["EuComp"]);
        Assert.DoesNotContain(EreSession, estimator.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSessionExistsBeforeAnythingIsCreatedInsideTheGlassAccount()
    {
        var harness = Harness.Create();

        await harness.LaunchAsync();

        Assert.Equal(
            new[]
            {
                GlassRepairEstimateSessionState.Prepared,
                GlassRepairEstimateSessionState.Launching,
                GlassRepairEstimateSessionState.Active,
            },
            harness.Store.History.Select(entry => entry.State));
        // Prepared is written before a single request reaches the provider, and
        // Launching before the stage that first creates state inside it.
        Assert.Equal(0, harness.Store.History[0].RequestsSoFar);
        Assert.Equal(harness.Mva.IndexOf("create-new-vehicle"), harness.Store.History[1].RequestsSoFar);
    }

    [Fact]
    public async Task TheAjaxStagesAnnounceThemselvesAndTheLookupAsksForTheLondonMonth()
    {
        var harness = Harness.Create();

        await harness.LaunchAsync();

        var lookup = harness.Mva.Requests.Single(
            request => request.Path.Contains("get-vehicles", StringComparison.Ordinal));
        var month = TimeZoneInfo.ConvertTime(
                harness.Clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Europe/London"))
            .ToString("yyyyMM", CultureInfo.InvariantCulture);
        Assert.EndsWith(month, lookup.Path, StringComparison.Ordinal);
        Assert.Equal("XMLHttpRequest", lookup.Requested);
        Assert.Equal(MvaBase.AbsoluteUri + "index", lookup.Referer);
        // The session's own cookie jar is presented, never a shared one.
        Assert.Contains("NDP=session-cookie", lookup.Cookie ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADoubleClickOnLaunchGetsTheSessionTheFirstClickCreated()
    {
        var harness = Harness.Create();
        var first = await harness.LaunchAsync(operationKey: "launch-1");

        var second = await harness.LaunchAsync(operationKey: "launch-1");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, harness.Mva.Count("POST /ere/start-ere"));
        Assert.Single(harness.Store.Sessions);
    }

    [Fact]
    public async Task TwoEngineersOnDifferentGlassAccountsRunSideBySide()
    {
        var harness = Harness.Create();
        await harness.LaunchAsync(operationKey: "launch-1");

        var second = await harness.LaunchAsync(
            actor: harness.OtherEngineer, operationKey: "launch-2");

        Assert.Equal(GlassRepairEstimateSessionState.Active, second.State);
        Assert.Equal(2, harness.Store.Sessions.Count);
    }

    [Fact]
    public async Task TheSameGlassAccountUnderAnotherEngineerIsRefused()
    {
        var harness = Harness.Create();
        harness.Credentials.Give(harness.OtherEngineer, harness.OtherEngineerId, account: Harness.Account);
        await harness.LaunchAsync(operationKey: "launch-1");

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.LaunchAsync(actor: harness.OtherEngineer, operationKey: "launch-2"));

        Assert.Equal(GlassRepairEstimateSessionConflict.ActiveAccount, conflict.Conflict);
    }

    [Fact]
    public async Task AnEngineerWithNoEnabledGlassAccountCannotLaunch()
    {
        var harness = Harness.Create();
        harness.Credentials.Revoke(harness.Engineer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.LaunchAsync());
        Assert.Empty(harness.Store.Sessions);
        Assert.Empty(harness.Mva.Requests);
    }

    [Fact]
    public async Task AStaleCaseVersionStopsTheLaunchBeforeTheProviderIsTouched()
    {
        var harness = Harness.Create();
        harness.CaseAuthority.Refusal = new CaseVersionConflictException(harness.CaseId, 4, 5);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.LaunchAsync());
        Assert.Empty(harness.Store.Sessions);
        Assert.Empty(harness.Mva.Requests);
    }

    // -------------------------------------------- launch stage refusals (200)

    public static TheoryData<string, Reply, string, GlassRepairEstimateSessionState> RefusedStages() => new()
    {
        {
            "GET /login/index",
            new(HttpStatusCode.OK, "<form><input name=\"csrf_token\" value=\"nope\" /></form>"),
            GlassFailure.LoginCsrf,
            GlassRepairEstimateSessionState.Failed
        },
        {
            // A re-rendered login form inside an HTTP 200 is a refused sign-in.
            "POST /login/index",
            new(HttpStatusCode.OK, "<form name=\"Form_Login\"></form>"),
            GlassFailure.LoginRedirect,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "POST /login/index",
            new(HttpStatusCode.Found, string.Empty, Location: "https://attacker.test/index"),
            GlassFailure.LoginRedirect,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "GET /index",
            new(HttpStatusCode.OK, "<form name=\"Form_Login\">stocklistGrid</form>"),
            GlassFailure.LoginLanding,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000/nostocksearch/1",
            new(HttpStatusCode.OK, "\uFEFF{\"stockcount\":0,\"vehicle_id\":0,\"vrm_lookup\":0}"),
            GlassFailure.LookupUnavailable,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "GET /three-phase-vehicle/get-vehicles",
            new(HttpStatusCode.OK, "{\"success\":false,\"html\":\"\"}"),
            GlassFailure.CandidatesRefused,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "GET /three-phase-vehicle/get-vehicles",
            new(HttpStatusCode.OK, "{\"success\":true,\"html\":\"<div class=\\\"three_phase_car_info car1\\\">N/C: 999999999</div>\"}"),
            GlassFailure.CandidatesNone,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "GET /three-phase-vehicle/get-vehicles",
            new(HttpStatusCode.OK,
                "{\"success\":true,\"html\":\"<div class=\\\"three_phase_car_info car1\\\">N/C: "
                + NatCode + "</div><div class=\\\"three_phase_car_info car2\\\">N/C: " + NatCode + "</div>\"}"),
            GlassFailure.CandidatesAmbiguous,
            GlassRepairEstimateSessionState.Failed
        },
        {
            // Created state may exist at Glass's, so this is uncertain and keeps
            // the account's live slot rather than being replaced.
            "GET /index/create-new-vehicle",
            new(HttpStatusCode.OK, "\uFEFF{\"vrm\":\"ZZ99ZZZ\",\"id\":\"33584499\"}"),
            GlassFailure.VehicleIdentity,
            GlassRepairEstimateSessionState.Unknown
        },
        {
            "GET /index/create-new-vehicle",
            new(HttpStatusCode.OK, "\uFEFF{\"vrm\":\"" + Registration + "\",\"id\":\"0\"}"),
            GlassFailure.VehicleIdentity,
            GlassRepairEstimateSessionState.Unknown
        },
        {
            "GET /index/vehicle-details-value/",
            new(HttpStatusCode.OK, "<div>profile 9999 natcode " + NatCode + "</div>"),
            GlassFailure.DetailsProfile,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "GET /index/get-selected-vehicle-count/grid/stocklistGrid",
            new(HttpStatusCode.OK, "{\"grid\":\"stocklistGrid\",\"error\":false,\"count\":\"2\"}"),
            GlassFailure.SelectCount,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "POST /ere/start-ere",
            new(HttpStatusCode.OK, "{\"message\":\"Profile not available\",\"status\":\"error\",\"ere_url\":\"\"}"),
            GlassFailure.StartStatus,
            GlassRepairEstimateSessionState.Failed
        },
        {
            // A launch URL missing one of its own segments is not rewritten.
            "POST /ere/start-ere",
            new(HttpStatusCode.OK, StartEre(LaunchUrl().Replace(
                "&EuComp=1005_1005_powered_by_eucomp", string.Empty, StringComparison.Ordinal))),
            GlassFailure.StartUrl,
            GlassRepairEstimateSessionState.Unknown
        },
        {
            // Two callers is ambiguous; picking one would be a guess.
            "POST /ere/start-ere",
            new(HttpStatusCode.OK, StartEre(LaunchUrl() + "&caller=" + Uri.EscapeDataString(
                "https://mva.test/ere/ere-callback/ere_id/9/ere_session/other"))),
            GlassFailure.StartUrl,
            GlassRepairEstimateSessionState.Unknown
        },
        {
            "POST /ere/start-ere",
            new(HttpStatusCode.OK, StartEre(LaunchUrl(caller:
                "https://attacker.test/ere/ere-callback/ere_id/1954488/ere_session/" + EreSession))),
            GlassFailure.StartCaller,
            GlassRepairEstimateSessionState.Unknown
        },
        {
            "POST /ere/start-ere",
            new(HttpStatusCode.OK, StartEre(LaunchUrl(caller:
                "https://mva.test/ere/ere-callback/ere_id/1954488/ere_session/" + EreSession + "?x=1"))),
            GlassFailure.StartCaller,
            GlassRepairEstimateSessionState.Unknown
        },
    };

    [Theory]
    [MemberData(nameof(RefusedStages))]
    public async Task AStageThatRefusesInsideAnHttp200StopsTheLaunchWhereItStopped(
        string route, Reply reply, string expectedFailure, GlassRepairEstimateSessionState expectedState)
    {
        var harness = Harness.Create();
        harness.Mva.Set(route, reply);

        var session = await harness.LaunchAsync();

        Assert.Equal(expectedState, session.State);
        Assert.Equal(expectedFailure, session.FailureCode);
        Assert.Null(session.ProviderEstimateId);
    }

    [Fact]
    public async Task TheFreshLookupIsRetriedExactlyOnce()
    {
        var harness = Harness.Create();
        harness.Mva.Enqueue(
            "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000/nostocksearch/1",
            new Reply(HttpStatusCode.OK, "\uFEFF{\"stockcount\":0,\"vehicle_id\":0,\"vrm_lookup\":0}"));

        var session = await harness.LaunchAsync();

        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.Equal(
            2,
            harness.Mva.Count(
                "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000/nostocksearch/1"));
    }

    [Fact]
    public async Task ALookupThatNeverSucceedsIsTriedTwiceAndNoMore()
    {
        var harness = Harness.Create();
        harness.Mva.Set(
            "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000/nostocksearch/1",
            new(HttpStatusCode.OK, "\uFEFF{\"stockcount\":0,\"vehicle_id\":0,\"vrm_lookup\":0}"));

        await harness.LaunchAsync();

        Assert.Equal(
            2,
            harness.Mva.Count(
                "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000/nostocksearch/1"));
    }

    [Theory]
    [InlineData("GET /index/create-new-vehicle")]
    [InlineData("POST /ere/start-ere")]
    public async Task TheStagesThatChangeTheGlassAccountAreNeverRetried(string route)
    {
        var harness = Harness.Create();
        harness.Mva.Set(route, new(HttpStatusCode.InternalServerError, string.Empty));

        var session = await harness.LaunchAsync();

        Assert.Equal(GlassRepairEstimateSessionState.Unknown, session.State);
        Assert.Equal(1, harness.Mva.Count(route));
    }

    // -------------------------------------------------------------- callback

    [Fact]
    public async Task ACompletedCallbackRetainsBothArtifactsAndLandsOneDraft()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        var completed = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, completed.State);
        Assert.Null(completed.FailureCode);
        Assert.Equal(
            new[]
            {
                GlassRepairEstimateGateway.XmlOccurrenceIdentity(session.Id),
                GlassRepairEstimateGateway.PdfOccurrenceIdentity(session.Id),
            },
            harness.Custody.Retained.Select(item => item.OccurrenceIdentity));
        Assert.Equal(RetainedMediaTypes, harness.Custody.Retained.Select(item => item.MediaType));

        var import = Assert.Single(harness.Import.Requests);
        Assert.Equal(RepairSpecificationSourceRoute.Glasses, import.Route);
        Assert.Equal(harness.CaseId, import.CaseId);
        Assert.Equal(Harness.CaseVersion, import.ExpectedVersion);
        Assert.Equal(Harness.LeaseToken, import.EditLeaseToken);
        Assert.Equal(harness.Custody.Retained[0].Sha256, import.Sha256);

        var results = harness.Store.ResultsOf(session.Id)!;
        Assert.Contains($"\"importedEstimateId\":\"{harness.Import.EstimateId:D}\"", results, StringComparison.Ordinal);
        Assert.Contains("\"xml\":", results, StringComparison.Ordinal);
        Assert.Contains("\"pdf\":", results, StringComparison.Ordinal);
        Assert.NotNull(harness.Store.Material(session.Id).Session.CallbackConsumedAtUtc);
    }

    [Fact]
    public async Task TheRelayCarriesTheProvidersOwnQueryBackToItUntouched()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        await harness.CompleteAsync(session);

        var relay = harness.Mva.Requests.Single(
            request => request.Path.StartsWith("/ere/ere-callback/", StringComparison.Ordinal));
        Assert.Equal($"/ere/ere-callback/ere_id/{EreId}/ere_session/{EreSession}", relay.Path);
        Assert.Equal(SavedQuery, relay.Query);
        Assert.Equal(EstimatorBase.AbsoluteUri, relay.Referer);
    }

    [Fact]
    public async Task AnUnknownCorrelationIsRefusedAndChangesNothing()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.CompleteAsync(session, correlation: new string('f', 64)));

        Assert.Equal(GlassRepairEstimateSessionConflict.Callback, conflict.Conflict);
        Assert.Equal(
            GlassRepairEstimateSessionState.Active, harness.Store.Material(session.Id).Session.State);
        Assert.Empty(harness.Import.Requests);
    }

    [Fact]
    public async Task AnIdenticalCallbackReplayReturnsWhatTheFirstOneProduced()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        var completed = await harness.CompleteAsync(session);

        var replayed = await harness.CompleteAsync(session, expectedVersion: completed.Version);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, replayed.State);
        Assert.Single(harness.Import.Requests);
        Assert.Equal(2, harness.Custody.Retained.Count);
    }

    [Fact]
    public async Task ADifferentCallbackQueryForTheSameSessionIsRefusedAndChangesNothing()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        var completed = await harness.CompleteAsync(session);

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.CompleteAsync(
                session, expectedVersion: completed.Version, rawQuery: "?Total=1&DoSave=1&ErrMsg="));

        Assert.Equal(GlassRepairEstimateSessionConflict.Callback, conflict.Conflict);
        Assert.Equal(
            GlassRepairEstimateSessionState.Completed, harness.Store.Material(session.Id).Session.State);
        Assert.Single(harness.Import.Requests);
    }

    [Fact]
    public async Task ACallbackPresentedByAnotherEngineerIsRefused()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.CompleteAsync(session, actor: harness.OtherEngineer));

        Assert.Equal(GlassRepairEstimateSessionConflict.Callback, conflict.Conflict);
        Assert.Empty(harness.Import.Requests);
    }

    [Fact]
    public async Task ACallbackAtTheWrongSessionVersionIsRefused()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        var conflict = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.CompleteAsync(session, expectedVersion: session.Version + 5));

        Assert.Equal(GlassRepairEstimateSessionConflict.Version, conflict.Conflict);
    }

    [Fact]
    public async Task ACallbackAfterTheSessionExpiredIsRefusedAndTheSessionSaysSo()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Clock.Offset = TimeSpan.FromHours(24);

        var settled = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Expired, settled.State);
        Assert.Equal(GlassFailure.CallbackExpired, settled.FailureCode);
        Assert.Empty(harness.Import.Requests);
    }

    [Fact]
    public async Task ACallbackAfterTheGlassCredentialWasReplacedExpiresTheSession()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Credentials.Give(harness.Engineer, harness.EngineerId, generation: 9);

        var settled = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Expired, settled.State);
        Assert.Empty(harness.Import.Requests);
    }

    [Fact]
    public async Task ACancelledCalculationIsNotImported()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        var settled = await harness.CompleteAsync(session, rawQuery: "?Total=0&DoSave=0&ErrMsg=");

        Assert.Equal(GlassRepairEstimateSessionState.Failed, settled.State);
        Assert.Equal(GlassFailure.CallbackNotSaved, settled.FailureCode);
        Assert.Empty(harness.Import.Requests);
        Assert.Empty(harness.Custody.Retained);
    }

    public static TheoryData<Reply, string> RefusedRelays() => new()
    {
        {
            new(HttpStatusCode.OK, "<html><body>Session expired</body></html>"),
            GlassFailure.RelayShape
        },
        {
            new(HttpStatusCode.OK, Relay("\"1\", \"2\", 1954488, 1, \"\"")),
            GlassFailure.RelayShape
        },
        {
            new(HttpStatusCode.OK, Relay("\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\", 999, 1, \"\"")),
            GlassFailure.RelayEstimate
        },
        {
            new(HttpStatusCode.OK, Relay("\"1\",\"2\",\"3\",\"4\",\"5\",\"6\",\"7\", 1954488, 0, \"\"")),
            GlassFailure.RelayOutcome
        },
    };

    [Theory]
    [MemberData(nameof(RefusedRelays))]
    public async Task ARelayThatDoesNotConfirmTheSaveStopsTheSession(Reply reply, string expectedFailure)
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Mva.Set("GET /ere/ere-callback/", reply);

        var settled = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Failed, settled.State);
        Assert.Equal(expectedFailure, settled.FailureCode);
        Assert.Empty(harness.Import.Requests);
    }

    public static TheoryData<string, string, GlassRepairEstimateSessionState> RefusedExports() => new()
    {
        {
            "<div>no exports yet</div>",
            GlassFailure.ExportNone,
            GlassRepairEstimateSessionState.Unknown
        },
        {
            "<a href=\"/ndp_download/one.xml\">a</a><a href=\"/ndp_download/two.xml\">b</a>",
            GlassFailure.ExportAmbiguous,
            GlassRepairEstimateSessionState.Failed
        },
        {
            "<a href=\"https://attacker.test/ndp_download/one.xml\">a</a>",
            GlassFailure.ExportOffOrigin,
            GlassRepairEstimateSessionState.Failed
        },
    };

    [Theory]
    [MemberData(nameof(RefusedExports))]
    public async Task AnExportThatIsNotExactlyOneSameOriginDocumentIsRefused(
        string grid, string expectedFailure, GlassRepairEstimateSessionState expectedState)
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Mva.Set("GET /ere/export-vehicle/", new(HttpStatusCode.OK, grid));

        var settled = await harness.CompleteAsync(session);

        Assert.Equal(expectedState, settled.State);
        Assert.Equal(expectedFailure, settled.FailureCode);
        Assert.Empty(harness.Import.Requests);
    }

    [Fact]
    public async Task AnOversizeExportIsRefusedRatherThanBuffered()
    {
        var harness = Harness.Create(maximumExportBytes: 512);
        var session = await harness.LaunchAsync();

        var settled = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Failed, settled.State);
        Assert.Equal(GlassFailure.DownloadOversize, settled.FailureCode);
        Assert.Empty(harness.Import.Requests);
    }

    public static TheoryData<string, string> MismatchedExports() => new()
    {
        { GlassEstimateXmlParserTests.GlassExport.BuildXml(registration: "ZZ99ZZZ"), GlassFailure.IdentityRegistration },
        { GlassEstimateXmlParserTests.GlassExport.BuildXml(mileage: "12345"), GlassFailure.IdentityMileage },
        { GlassEstimateXmlParserTests.GlassExport.BuildXml(typeNumber: "999999999"), GlassFailure.IdentityNatCode },
        {
            // A real, well-formed Glass's document that costs nothing: valid to
            // parse, not an estimate to import.
            GlassEstimateXmlParserTests.GlassExport.BuildXml(
                positions: string.Empty,
                attachment: string.Empty,
                partsTotal: "0.00",
                labourTotal: "0.00",
                paintTotal: "0.00",
                netTotal: "0.00",
                vatMaterial: "0.00",
                grossTotal: "0.00"),
            GlassFailure.ExportEmpty
        },
        { "<Estimation><GlobalSetting /></Estimation>", GlassFailure.ExportUnreadable },
    };

    [Theory]
    [MemberData(nameof(MismatchedExports))]
    public async Task AnExportThatIsNotThisSessionsVehicleIsNeverImported(string xml, string expectedFailure)
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Mva.Set("GET /ndp_download/", new(HttpStatusCode.OK, xml, ContentType: "application/xml"));

        var settled = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Failed, settled.State);
        Assert.Equal(expectedFailure, settled.FailureCode);
        Assert.Empty(harness.Import.Requests);
        Assert.Empty(harness.Custody.Retained);
    }

    // ------------------------------------------------- waiting for the import

    [Fact]
    public async Task AStaleCaseLeaseKeepsTheArtifactsAndWaitsForTheEngineerToComeBack()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Import.Refusal = new CaseEditLeaseExpiredException(harness.CaseId, Harness.CaseVersion);

        var waiting = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.AwaitingImport, waiting.State);
        Assert.Equal(2, harness.Custody.Retained.Count);
        Assert.Single(harness.Import.Requests);

        harness.Import.Refusal = null;
        var completed = await harness.Gateway.ResumeAsync(
            new GlassRepairEstimateResumeRequest(
                harness.Engineer, session.Id, waiting.Version, ExpectedCaseVersion: 12, LeaseToken: new string('b', 64)),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, completed.State);
        // Only the import runs again: nothing is offered to custody a second time.
        Assert.Equal(2, harness.Custody.Retained.Count);
        Assert.Equal(2, harness.Import.Requests.Count);
        Assert.Equal(12, harness.Import.Requests[1].ExpectedVersion);
        Assert.Equal(new string('b', 64), harness.Import.Requests[1].EditLeaseToken);
    }

    [Fact]
    public async Task ResumingAWaitingSessionWithoutTheCaseLeaseIsRefused()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Import.Refusal = new CaseEditLeaseExpiredException(harness.CaseId, Harness.CaseVersion);
        var waiting = await harness.CompleteAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Gateway.ResumeAsync(
                new GlassRepairEstimateResumeRequest(harness.Engineer, session.Id, waiting.Version),
                CancellationToken.None));
    }

    [Fact]
    public async Task APendingRetentionKeepsItsIdentitiesAndIsResolvedThroughCustodyLater()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Custody.Disposition = CaseArtifactCustodyDisposition.Pending;

        var waiting = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.AwaitingImport, waiting.State);
        Assert.Empty(harness.Import.Requests);
        var results = harness.Store.ResultsOf(session.Id)!;
        Assert.Contains("\"status\":\"Pending\"", results, StringComparison.Ordinal);
        Assert.Contains(harness.Custody.DocumentIdOf("xml").ToString("D"), results, StringComparison.Ordinal);

        var completed = await harness.Gateway.ResumeAsync(
            new GlassRepairEstimateResumeRequest(
                harness.Engineer, session.Id, waiting.Version, Harness.CaseVersion, Harness.LeaseToken),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, completed.State);
        // Resolved by asking custody what happened, never by offering the bytes again.
        Assert.Equal(2, harness.Custody.Retained.Count);
        Assert.Equal(2, harness.Custody.StatusQueries.Count);
        Assert.Single(harness.Import.Requests);
    }

    [Fact]
    public async Task ARetentionThatFailedStopsTheSessionWithNothingImported()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        harness.Custody.Disposition = CaseArtifactCustodyDisposition.Failed;

        var settled = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Failed, settled.State);
        Assert.Equal(GlassFailure.CustodyFailed, settled.FailureCode);
        Assert.Empty(harness.Import.Requests);
    }

    // ---------------------------------------------------------------- resume

    [Fact]
    public async Task ResumingALiveSessionReopensItUnderTheCallbackItAlreadyMinted()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        var estimator = await harness.Gateway.GetEstimatorUrlAsync(
            harness.Engineer, session.Id, CancellationToken.None);

        var resumed = await harness.Gateway.ResumeAsync(
            new GlassRepairEstimateResumeRequest(harness.Engineer, session.Id, session.Version),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Active, resumed.State);
        var reopened = await harness.Gateway.GetEstimatorUrlAsync(
            harness.Engineer, session.Id, CancellationToken.None);
        Assert.Equal(QueryOf(estimator!)["caller"], QueryOf(reopened!)["caller"]);
        // The grid selection is server-side state, so it is re-asserted, and the
        // existing estimate is resumed rather than a second one started.
        Assert.Equal(2, harness.Mva.Count("GET /index/get-selected-vehicle-count/grid/stocklistGrid"));
        Assert.Equal(
            EreId,
            QueryOf(harness.Mva.Requests.Last(request => request.Path == "/ere/start-ere").Body!)["ere_id"]);
    }

    [Fact]
    public async Task AnotherEngineerCannotReadOrResumeThisEngineersSession()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Gateway.GetEstimatorUrlAsync(
                harness.OtherEngineer, session.Id, CancellationToken.None));
        await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => harness.Gateway.ResumeAsync(
                new GlassRepairEstimateResumeRequest(harness.OtherEngineer, session.Id, session.Version),
                CancellationToken.None));
    }

    // --------------------------------------------------------------- secrets

    [Fact]
    public async Task NothingTheProviderHandedOverIsReadableOnTheSession()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();
        var completed = await harness.CompleteAsync(session);

        var material = harness.Store.Material(session.Id);
        foreach (var secret in new[] { EreSession, Harness.Password, Harness.LeaseToken, "session-cookie" })
        {
            Assert.DoesNotContain(secret, material.ProtectedProviderState, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, material.ResultArtifactsJson ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, completed.FailureCode ?? string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, material.Session.ToString(), StringComparison.Ordinal);
        }

        Assert.DoesNotContain(Harness.Account, material.Session.NormalizedExternalAccountKey, StringComparison.Ordinal);
    }

    // -------------------------------------------------- the real store, once

    /// <summary>
    /// The same launch → callback → import walk against the real store on
    /// LocalDB, because the stages this gateway relies on being durable — one
    /// live session per account, the immutable callback fingerprint consumed
    /// once, per-row version — are the database's rules and not this suite's
    /// substitute's.
    /// </summary>
    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task ThePersistedStagesSurviveTheRealStore()
    {
        await using var database = await GlassRepairEstimatePersistenceTests.Harness.CreateAsync();
        var harness = Harness.Create(
            store: new EfGlassRepairEstimateSessionStore(database.Factory, TimeProvider.System),
            caseId: database.CaseId,
            engineerId: database.UserId,
            otherEngineerId: database.OtherUserId);

        var session = await harness.LaunchAsync();
        Assert.Equal(GlassRepairEstimateSessionState.Active, session.State);
        Assert.NotNull(await database.ActiveAccountKeyAsync(session.Id));

        var completed = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, completed.State);
        Assert.NotNull(await database.CallbackConsumedAtAsync(session.Id));
        // Completed releases the account's one live slot.
        Assert.Null(await database.ActiveAccountKeyAsync(session.Id));
        var results = await database.ResultArtifactsJsonAsync(session.Id);
        Assert.Contains($"\"importedEstimateId\":\"{harness.Import.EstimateId:D}\"", results!, StringComparison.Ordinal);
        var protectedSession = await database.ProtectedSessionAsync(session.Id);
        Assert.DoesNotContain(EreSession, protectedSession, StringComparison.Ordinal);
        Assert.DoesNotContain(Harness.Password, protectedSession, StringComparison.Ordinal);
    }

    /// <summary>
    /// The claim race on the persisted store: two deliveries of the same
    /// return read the session at one version, the database's version check
    /// admits one write, the other reads the record, and the provider hears
    /// the relay once. A gateway built afterwards over the same database, as a
    /// restarted host is, answers the replay from the durable record.
    /// </summary>
    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task TwoDeliveriesOfTheSameReturnRaceOnThePersistedStoreAndActOnce()
    {
        await using var database = await GlassRepairEstimatePersistenceTests.Harness.CreateAsync();
        using var gate = new GatedStore(new EfGlassRepairEstimateSessionStore(database.Factory, TimeProvider.System));
        var harness = Harness.Create(
            store: gate,
            caseId: database.CaseId,
            engineerId: database.UserId,
            otherEngineerId: database.OtherUserId);
        var session = await harness.LaunchAsync();
        gate.HoldNext(2);

        var outcomes = await Task.WhenAll(harness.CompleteAsync(session), harness.CompleteAsync(session));

        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        Assert.Single(harness.Import.Requests);
        Assert.All(outcomes, outcome => Assert.Equal(session.Id, outcome.Id));
        Assert.All(outcomes, outcome => Assert.True(
            outcome.State is GlassRepairEstimateSessionState.Importing or GlassRepairEstimateSessionState.Completed,
            outcome.State.ToString()));
        var recorded = (await database.Store.GetAsync(session.Id, CancellationToken.None))!;
        Assert.Equal(GlassRepairEstimateSessionState.Completed, recorded.Session.State);
        Assert.NotNull(await database.CallbackConsumedAtAsync(session.Id));
        Assert.Contains(
            $"\"callbackQueryDigest\":\"{GlassRepairEstimateGateway.CallbackDigestOf(SavedQuery)}\"",
            recorded.ResultArtifactsJson,
            StringComparison.Ordinal);

        var restarted = Restarted(
            harness, new EfGlassRepairEstimateSessionStore(database.Factory, TimeProvider.System));
        var replayed = await restarted.CompleteAsync(
            recorded.Session, correlation: harness.CorrelationOf(session.Id));

        Assert.Equal(GlassRepairEstimateSessionState.Completed, replayed.State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        Assert.Single(harness.Import.Requests);
    }

    /// <summary>
    /// Two different returns racing on the persisted store: the one the
    /// database's version check admits is acted on and its fingerprint is the
    /// record's; the other is refused as a contradictory callback, before and
    /// after a restart.
    /// </summary>
    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task TwoDifferentReturnsRacingOnThePersistedStoreLeaveOneRecordAndOneRefusal()
    {
        const string otherQuery = "?Total=0&DoSave=1&ErrMsg=D%3A%2Fvar%2Fdb%2Feremware%2Fresponse%2Fother.xml";
        await using var database = await GlassRepairEstimatePersistenceTests.Harness.CreateAsync();
        using var gate = new GatedStore(new EfGlassRepairEstimateSessionStore(database.Factory, TimeProvider.System));
        var harness = Harness.Create(
            store: gate,
            caseId: database.CaseId,
            engineerId: database.UserId,
            otherEngineerId: database.OtherUserId);
        var session = await harness.LaunchAsync();
        gate.HoldNext(2);

        var first = harness.CompleteAsync(session);
        var second = harness.CompleteAsync(session, rawQuery: otherQuery);
        var refusals = new List<GlassRepairEstimateSessionConflictException>();
        var landed = new List<GlassRepairEstimateSession>();
        foreach (var delivery in new[] { first, second })
        {
            try
            {
                landed.Add(await delivery);
            }
            catch (GlassRepairEstimateSessionConflictException refusal)
            {
                refusals.Add(refusal);
            }
        }

        Assert.Equal(GlassRepairEstimateSessionConflict.Callback, Assert.Single(refusals).Conflict);
        Assert.Equal(GlassRepairEstimateSessionState.Completed, Assert.Single(landed).State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        var winner = first.IsCompletedSuccessfully ? SavedQuery : otherQuery;
        var loser = first.IsCompletedSuccessfully ? otherQuery : SavedQuery;
        Assert.Contains(
            $"\"callbackQueryDigest\":\"{GlassRepairEstimateGateway.CallbackDigestOf(winner)}\"",
            await database.ResultArtifactsJsonAsync(session.Id),
            StringComparison.Ordinal);

        var restarted = Restarted(
            harness, new EfGlassRepairEstimateSessionStore(database.Factory, TimeProvider.System));
        var current = (await database.Store.GetAsync(session.Id, CancellationToken.None))!.Session;
        var refused = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => restarted.CompleteAsync(current, correlation: harness.CorrelationOf(session.Id), rawQuery: loser));
        Assert.Equal(GlassRepairEstimateSessionConflict.Callback, refused.Conflict);
        var replayed = await restarted.CompleteAsync(
            current, correlation: harness.CorrelationOf(session.Id), rawQuery: winner);
        Assert.Equal(GlassRepairEstimateSessionState.Completed, replayed.State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
    }

    // ---------------------------------------------------------------- support

    private static Dictionary<string, string> QueryOf(Uri uri) => QueryOf(uri.Query);

    private static Dictionary<string, string> QueryOf(string query)
    {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            parsed[Uri.UnescapeDataString(part[..separator])] =
                Uri.UnescapeDataString(part[(separator + 1)..]);
        }

        return parsed;
    }

    /// <summary>
    /// A clock that starts at a fixed moment and then runs, so a bounded poll
    /// finishes, while <see cref="Offset"/> jumps it forward for the expiry
    /// checks.
    /// </summary>
    internal sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private readonly long origin = Stopwatch.GetTimestamp();

        public TimeSpan Offset { get; set; }

        public override DateTimeOffset GetUtcNow() =>
            start + Offset + Stopwatch.GetElapsedTime(origin);
    }

    // -------------------------------------------------------- callback claim

    /// <summary>
    /// Two deliveries of the same Save &amp; Exit that both read the session at
    /// the same version: the claim admits one, the other reads its record, and
    /// the provider hears the relay once.
    /// </summary>
    [Fact]
    public async Task TwoDeliveriesOfTheSameReturnActOnTheProviderOnce()
    {
        using var gate = new GatedStore(new MemorySessionStore(new TestClock(StartUtc)));
        var harness = Harness.Create(store: gate);
        var session = await harness.LaunchAsync();
        gate.HoldNext(2);

        var outcomes = await Task.WhenAll(harness.CompleteAsync(session), harness.CompleteAsync(session));

        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        Assert.All(outcomes, outcome => Assert.Equal(session.Id, outcome.Id));
        // The loser reads the record as it stands: still importing, or done.
        Assert.All(outcomes, outcome => Assert.True(
            outcome.State is GlassRepairEstimateSessionState.Importing or GlassRepairEstimateSessionState.Completed,
            outcome.State.ToString()));
        Assert.Single(harness.Import.Requests);
        var recorded = (await harness.Sessions.GetAsync(session.Id, CancellationToken.None))!.Session;
        Assert.Equal(GlassRepairEstimateSessionState.Completed, recorded.State);
        Assert.NotNull(recorded.CallbackConsumedAtUtc);
    }

    /// <summary>
    /// Two different messages racing for one session: the one the claim admits
    /// is acted on, the other is refused as a contradictory callback, and the
    /// record carries the fingerprint of the one that acted.
    /// </summary>
    [Fact]
    public async Task TwoDifferentReturnsRacingForOneSessionLeaveOneRecordAndOneRefusal()
    {
        const string otherQuery = "?Total=0&DoSave=1&ErrMsg=D%3A%2Fvar%2Fdb%2Feremware%2Fresponse%2Fother.xml";
        using var gate = new GatedStore(new MemorySessionStore(new TestClock(StartUtc)));
        var harness = Harness.Create(store: gate);
        var session = await harness.LaunchAsync();
        gate.HoldNext(2);

        var first = harness.CompleteAsync(session);
        var second = harness.CompleteAsync(session, rawQuery: otherQuery);
        var refusals = new List<Exception>();
        var landed = new List<GlassRepairEstimateSession>();
        foreach (var delivery in new[] { first, second })
        {
            try
            {
                landed.Add(await delivery);
            }
            catch (GlassRepairEstimateSessionConflictException refusal)
            {
                refusals.Add(refusal);
            }
        }

        var refused = Assert.Single(refusals);
        Assert.Equal(
            GlassRepairEstimateSessionConflict.Callback,
            ((GlassRepairEstimateSessionConflictException)refused).Conflict);
        Assert.Equal(GlassRepairEstimateSessionState.Completed, Assert.Single(landed).State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        var relayed = harness.Mva.Requests.Single(
            request => request.Path.StartsWith("/ere/ere-callback/", StringComparison.Ordinal));
        var winner = first.IsCompletedSuccessfully ? SavedQuery : otherQuery;
        Assert.Equal(winner, relayed.Query);
        Assert.Contains(
            $"\"callbackQueryDigest\":\"{GlassRepairEstimateGateway.CallbackDigestOf(winner)}\"",
            (await harness.Sessions.GetAsync(session.Id, CancellationToken.None))!.ResultArtifactsJson,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The store refuses the claim: the fault surfaces, the provider was never
    /// touched, and the callback is still the Engineer's to deliver — through
    /// a fresh gateway, as after a restart.
    /// </summary>
    [Fact]
    public async Task AClaimTheStoreRefusesLeavesTheCallbackUnspentAndTheProviderUntouched()
    {
        using var gate = new GatedStore(new MemorySessionStore(new TestClock(StartUtc)));
        var harness = Harness.Create(store: gate);
        var session = await harness.LaunchAsync();
        gate.Refuse = material => material.Session.State == GlassRepairEstimateSessionState.Importing
            ? new IOException("The session store is unavailable.")
            : null;

        await Assert.ThrowsAsync<IOException>(() => harness.CompleteAsync(session));

        Assert.Equal(0, harness.Mva.Count("GET /ere/ere-callback/"));
        var untouched = (await harness.Sessions.GetAsync(session.Id, CancellationToken.None))!.Session;
        Assert.Equal(GlassRepairEstimateSessionState.Active, untouched.State);
        Assert.Null(untouched.CallbackConsumedAtUtc);

        gate.Refuse = null;
        var restarted = Restarted(harness, gate);
        var completed = await restarted.CompleteAsync(
            untouched, correlation: harness.CorrelationOf(session.Id));

        Assert.Equal(GlassRepairEstimateSessionState.Completed, completed.State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
    }

    /// <summary>
    /// The provider fails right after the claim: the outcome is on the record
    /// with the delivery's fingerprint, and a fresh gateway given the same
    /// message reads that record — it never relays again — while a different
    /// message is refused.
    /// </summary>
    [Fact]
    public async Task AFailureAfterTheClaimIsRecordedAndNeverRepeatedByARestartedHost()
    {
        using var gate = new GatedStore(new MemorySessionStore(new TestClock(StartUtc)));
        var harness = Harness.Create(store: gate);
        var session = await harness.LaunchAsync();
        harness.Mva.Enqueue("GET /ere/ere-callback/", new Reply(HttpStatusCode.InternalServerError, string.Empty));

        var settled = await harness.CompleteAsync(session);

        Assert.True(
            settled.State is GlassRepairEstimateSessionState.Failed or GlassRepairEstimateSessionState.Unknown,
            settled.State.ToString());
        Assert.NotNull(settled.FailureCode);
        var recorded = (await harness.Sessions.GetAsync(session.Id, CancellationToken.None))!;
        Assert.NotNull(recorded.Session.CallbackConsumedAtUtc);
        Assert.Contains(
            $"\"callbackQueryDigest\":\"{GlassRepairEstimateGateway.CallbackDigestOf(SavedQuery)}\"",
            recorded.ResultArtifactsJson,
            StringComparison.Ordinal);

        var restarted = Restarted(harness, gate);
        var replayed = await restarted.CompleteAsync(
            recorded.Session, correlation: harness.CorrelationOf(session.Id));
        Assert.Equal(settled.State, replayed.State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        var refused = await Assert.ThrowsAsync<GlassRepairEstimateSessionConflictException>(
            () => restarted.CompleteAsync(
                recorded.Session,
                correlation: harness.CorrelationOf(session.Id),
                rawQuery: "?Total=0&DoSave=1&ErrMsg=D%3A%2Fvar%2Fdb%2Feremware%2Fresponse%2Fother.xml"));
        Assert.Equal(GlassRepairEstimateSessionConflict.Callback, refused.Conflict);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
    }

    /// <summary>
    /// The export never appears within the wait: the session is Unknown with
    /// its claim, a restarted host answers the replay from the record, and the
    /// Engineer's resume looks the export up again — signing in, selecting the
    /// vehicle, reading — without a second relay or a second estimate.
    /// </summary>
    [Fact]
    public async Task AnUncertainExportStaysUnknownUntilAResumeLooksItUpAgain()
    {
        using var gate = new GatedStore(new MemorySessionStore(new TestClock(StartUtc)));
        var harness = Harness.Create(store: gate);
        var session = await harness.LaunchAsync();
        harness.Mva.Set("GET /ere/export-vehicle/", new(HttpStatusCode.OK, "<div>nothing published yet</div>"));

        var uncertain = await harness.CompleteAsync(session);

        Assert.Equal(GlassRepairEstimateSessionState.Unknown, uncertain.State);
        Assert.NotNull((await harness.Sessions.GetAsync(session.Id, CancellationToken.None))!.Session.CallbackConsumedAtUtc);
        var relaysBefore = harness.Mva.Count("GET /ere/ere-callback/");
        Assert.Equal(1, relaysBefore);

        var restarted = Restarted(harness, gate);
        var replayed = await restarted.CompleteAsync(uncertain, correlation: harness.CorrelationOf(session.Id));
        Assert.Equal(GlassRepairEstimateSessionState.Unknown, replayed.State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));

        harness.Mva.Set("GET /ere/export-vehicle/", new(
            HttpStatusCode.OK, "<a href=\"/ndp_download/export_1.xml\">Download</a>"));
        var completed = await restarted.Gateway.ResumeAsync(
            new GlassRepairEstimateResumeRequest(
                restarted.Engineer, session.Id, uncertain.Version, Harness.CaseVersion, Harness.LeaseToken),
            CancellationToken.None);

        Assert.Equal(GlassRepairEstimateSessionState.Completed, completed.State);
        Assert.Equal(1, harness.Mva.Count("GET /ere/ere-callback/"));
        Assert.Equal(1, harness.Mva.Count("POST /ere/start-ere"));
        Assert.Single(restarted.Import.Requests);
        Assert.Equal(
            new[]
            {
                GlassRepairEstimateGateway.XmlOccurrenceIdentity(session.Id),
                GlassRepairEstimateGateway.PdfOccurrenceIdentity(session.Id),
            },
            restarted.Custody.Retained.Select(item => item.OccurrenceIdentity));
    }

    /// <summary>The import is named by the occurrence custody minted, not the document (G23).</summary>
    [Fact]
    public async Task TheImportIsNamedByTheOccurrenceCustodyMinted()
    {
        var harness = Harness.Create();
        var session = await harness.LaunchAsync();

        await harness.CompleteAsync(session);

        var import = Assert.Single(harness.Import.Requests);
        Assert.Equal(harness.Custody.OccurrenceIdOf("xml"), import.OccurrenceId);
        Assert.NotEqual(harness.Custody.DocumentIdOf("xml"), import.OccurrenceId);
    }

    /// <summary>A second gateway over the same store, protector and provider: the host after a restart.</summary>
    private static Harness Restarted(Harness before, IGlassRepairEstimateSessionStore store) =>
        Harness.Create(
            store: store,
            caseId: before.CaseId,
            engineerId: before.EngineerId,
            otherEngineerId: before.OtherEngineerId,
            protection: before.Protection,
            provider: before.Mva);

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CaseAuthorityDouble(GlassRepairEstimateCaseFacts facts) : IGlassRepairEstimateCaseAuthority
    {
        public Exception? Refusal { get; set; }

        public Task<GlassRepairEstimateCaseFacts> RequireEditAuthorityAsync(
            ActionActor actor,
            Guid caseId,
            long expectedCaseVersion,
            string editLeaseToken,
            CancellationToken cancellationToken) =>
            Refusal is null
                ? Task.FromResult(facts)
                : Task.FromException<GlassRepairEstimateCaseFacts>(Refusal);
    }

    private sealed class CredentialDouble : IPerUserExternalCredentialReader
    {
        private readonly Dictionary<string, PerUserExternalCredentialMaterial> held = new(StringComparer.Ordinal);

        /// <summary>
        /// The reference carries the canonical account key the credential
        /// store mints, never the account name. This double hands out an
        /// opaque 64-hex key per account so the gateway can be seen passing it
        /// through untouched; the real derivation is Stream A's and is proved
        /// against A's store in the shared host, not here.
        /// </summary>
        public void Give(ActionActor actor, Guid userId, string account = Harness.Account, long generation = 1) =>
            held[actor.SubjectId] = new(
                new(userId, ExternalCredentialProvider.GlassRepairEstimate, generation, OpaqueKeyFor(account), true, 1),
                account,
                Harness.Password);

        private static string OpaqueKeyFor(string account) =>
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("fixture-key:" + account)));

        public void Revoke(ActionActor actor) => held.Remove(actor.SubjectId);

        public Task<PerUserExternalCredentialMaterial?> GetEnabledAsync(
            ActionActor actor, ExternalCredentialProvider provider, CancellationToken cancellationToken) =>
            Task.FromResult(held.GetValueOrDefault(actor.SubjectId));
    }

    private sealed class CustodyDouble : ICaseArtifactCustody, ICaseArtifactCustodyStatus
    {
        private readonly Dictionary<string, (Guid Document, Guid Version, Guid Occurrence)> identities =
            new(StringComparer.Ordinal);

        public CaseArtifactCustodyDisposition Disposition { get; set; } = CaseArtifactCustodyDisposition.Confirmed;

        public List<Retention> Retained { get; } = [];

        public List<(Guid Document, Guid Version, Guid Occurrence)> StatusQueries { get; } = [];

        public Guid DocumentIdOf(string kind) => identities[kind].Document;

        public Guid OccurrenceIdOf(string kind) => identities[kind].Occurrence;

        public async Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            Retained.Add(new(
                request.OccurrenceIdentity,
                request.OperationKey,
                request.FileName,
                request.MediaType,
                request.Sha256,
                request.ContentLength,
                buffer.ToArray()));

            var kind = request.OccurrenceIdentity[(request.OccurrenceIdentity.LastIndexOf(':') + 1)..];
            var identity = identities.TryGetValue(kind, out var existing)
                ? existing
                : identities[kind] = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            return Result(identity, request.Sha256, request.ContentLength, request.MediaType, Disposition);
        }

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor, Guid caseId, Guid documentId, Guid versionId, Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            StatusQueries.Add((documentId, versionId, occurrenceId));
            var retained = Retained.Single(
                item => identities[item.OccurrenceIdentity[(item.OccurrenceIdentity.LastIndexOf(':') + 1)..]]
                    .Occurrence == occurrenceId);
            return Task.FromResult(Result(
                (documentId, versionId, occurrenceId),
                retained.Sha256,
                retained.ContentLength,
                retained.MediaType,
                CaseArtifactCustodyDisposition.Confirmed));
        }

        public Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
            ActionActor actor, Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult<CaseArtifactCustodyResult?>(null);

        private static CaseArtifactCustodyResult Result(
            (Guid Document, Guid Version, Guid Occurrence) identity,
            string sha256,
            long contentLength,
            string mediaType,
            CaseArtifactCustodyDisposition disposition) =>
            new(
                disposition,
                identity.Document,
                identity.Version,
                identity.Occurrence,
                BoxFileId: null,
                BoxVersionId: null,
                disposition == CaseArtifactCustodyDisposition.Confirmed ? sha256 : null,
                contentLength,
                mediaType,
                disposition == CaseArtifactCustodyDisposition.Failed ? "custody_refused" : null,
                disposition == CaseArtifactCustodyDisposition.Pending ? "staged/glass" : null);

        internal sealed record Retention(
            string OccurrenceIdentity,
            string OperationKey,
            string FileName,
            string MediaType,
            string Sha256,
            long ContentLength,
            byte[] Content);
    }

    private sealed class ImportDouble : IImportRawEstimate
    {
        public Guid EstimateId { get; } = Guid.NewGuid();

        public Exception? Refusal { get; set; }

        public List<ImportRawEstimateRequest> Requests { get; } = [];

        public Task<Guid> ExecuteAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Refusal is null ? Task.FromResult(EstimateId) : Task.FromException<Guid>(Refusal);
        }
    }

    /// <summary>
    /// An in-memory stand-in for <see cref="EfGlassRepairEstimateSessionStore"/>
    /// that keeps exactly the rules the gateway leans on: one live session per
    /// external account, replay by operation key, an immutable callback
    /// fingerprint consumed once, and per-row optimistic concurrency. The real
    /// store proves those rules against the database in
    /// <see cref="ThePersistedStagesSurviveTheRealStore"/> and in
    /// <see cref="GlassRepairEstimatePersistenceTests"/>; this one is here so
    /// the transport tests do not each need a database.
    /// </summary>
    internal sealed class MemorySessionStore(TimeProvider timeProvider) : IGlassRepairEstimateSessionStore
    {
        public Dictionary<Guid, GlassRepairEstimateSessionMaterial> Sessions { get; } = [];

        public List<(GlassRepairEstimateSessionState State, int RequestsSoFar)> History { get; } = [];

        public Func<int> RequestCount { get; set; } = () => 0;

        public GlassRepairEstimateSessionMaterial Material(Guid sessionId) => Sessions[sessionId];

        public string? ResultsOf(Guid sessionId) => Sessions[sessionId].ResultArtifactsJson;

        public Task<GlassRepairEstimateSessionMaterial?> GetAsync(
            Guid sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(Sessions.GetValueOrDefault(sessionId));

        public Task<GlassRepairEstimateSessionMaterial> CreateAsync(
            GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken)
        {
            var session = material.Session;
            // The canonical key arrives minted by the credential store and is
            // kept unchanged, exactly as the real session store keeps it.
            var accountKey = session.NormalizedExternalAccountKey;
            var replay = Sessions.Values.SingleOrDefault(
                item => item.Session.OperationKey == session.OperationKey);
            if (replay is not null)
            {
                return replay.Session.CaseId == session.CaseId
                    && replay.Session.PegasusUserId == session.PegasusUserId
                    && replay.Session.CredentialGeneration == session.CredentialGeneration
                    && replay.Session.NormalizedExternalAccountKey == accountKey
                    ? Task.FromResult(replay)
                    : throw new GlassRepairEstimateSessionConflictException(
                        GlassRepairEstimateSessionConflict.OperationKey,
                        replay.Session.Id,
                        "Operation key already names another Glass's session.");
            }
            if (Sessions.Values.Any(item =>
                    item.Session.NormalizedExternalAccountKey == accountKey && Occupies(item.Session.State)))
            {
                throw new GlassRepairEstimateSessionConflictException(
                    GlassRepairEstimateSessionConflict.ActiveAccount,
                    session.Id,
                    "The Glass's account already holds a live session.");
            }

            var stored = new GlassRepairEstimateSessionMaterial(
                session with { NormalizedExternalAccountKey = accountKey },
                material.ProtectedProviderState,
                material.CallbackDigest,
                material.ResultArtifactsJson);
            Sessions[session.Id] = stored;
            History.Add((session.State, RequestCount()));
            return Task.FromResult(stored);
        }

        public Task SaveAsync(
            GlassRepairEstimateSessionMaterial material, long expectedVersion, CancellationToken cancellationToken)
        {
            var session = material.Session;
            var current = Sessions[session.Id];
            if (current.CallbackDigest != material.CallbackDigest)
            {
                throw new GlassRepairEstimateSessionConflictException(
                    GlassRepairEstimateSessionConflict.Callback, session.Id, "A different callback.");
            }
            if (current.Session.Version != expectedVersion)
            {
                throw new GlassRepairEstimateSessionConflictException(
                    GlassRepairEstimateSessionConflict.Version, session.Id, "A different version.");
            }

            var consumed = current.Session.CallbackConsumedAtUtc
                ?? (Awaiting(current.Session.State) && !Awaiting(session.State)
                    ? timeProvider.GetUtcNow()
                    : null);
            Sessions[session.Id] = new(
                session with
                {
                    NormalizedExternalAccountKey = current.Session.NormalizedExternalAccountKey,
                    Version = expectedVersion + 1,
                    CallbackConsumedAtUtc = consumed,
                },
                material.ProtectedProviderState,
                material.CallbackDigest,
                material.ResultArtifactsJson);
            History.Add((session.State, RequestCount()));
            return Task.CompletedTask;
        }

        private static bool Occupies(GlassRepairEstimateSessionState state) =>
            state is GlassRepairEstimateSessionState.Prepared
                or GlassRepairEstimateSessionState.Launching
                or GlassRepairEstimateSessionState.Active
                or GlassRepairEstimateSessionState.Unknown
                or GlassRepairEstimateSessionState.AwaitingImport
                or GlassRepairEstimateSessionState.Importing;

        private static bool Awaiting(GlassRepairEstimateSessionState state) =>
            state is GlassRepairEstimateSessionState.Prepared
                or GlassRepairEstimateSessionState.Launching
                or GlassRepairEstimateSessionState.Active
                or GlassRepairEstimateSessionState.Unknown;
    }

    /// <summary>
    /// The real store's shape with two things a test can do to it: hold the
    /// next N saves until all N have arrived, so two deliveries reach the claim
    /// having read the same version, and refuse a save outright.
    /// </summary>
    private sealed class GatedStore(IGlassRepairEstimateSessionStore inner) : IGlassRepairEstimateSessionStore, IDisposable
    {
        private readonly SemaphoreSlim oneWriter = new(1, 1);
        private TaskCompletionSource? barrier;
        private int expected;
        private int arrived;

        public Func<GlassRepairEstimateSessionMaterial, Exception?>? Refuse { get; set; }

        public void HoldNext(int saves)
        {
            expected = saves;
            arrived = 0;
            barrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task<GlassRepairEstimateSessionMaterial?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
            inner.GetAsync(sessionId, cancellationToken);

        public Task<GlassRepairEstimateSessionMaterial> CreateAsync(
            GlassRepairEstimateSessionMaterial material, CancellationToken cancellationToken) =>
            inner.CreateAsync(material, cancellationToken);

        public async Task SaveAsync(
            GlassRepairEstimateSessionMaterial material, long expectedVersion, CancellationToken cancellationToken)
        {
            if (Refuse?.Invoke(material) is { } refusal)
            {
                throw refusal;
            }
            if (barrier is { } wait)
            {
                if (Interlocked.Increment(ref arrived) >= expected)
                {
                    wait.TrySetResult();
                }
                await wait.Task;
            }

            await oneWriter.WaitAsync(cancellationToken);
            try
            {
                await inner.SaveAsync(material, expectedVersion, cancellationToken);
            }
            finally
            {
                oneWriter.Release();
            }
        }

        public void Dispose() => oneWriter.Dispose();
    }

    private sealed class Harness
    {
        internal const string Account = "a.engineer";
        internal const string Password = "not-a-real-password";
        internal const long CaseVersion = 11;
        internal static readonly string LeaseToken = new('a', 64);

        private readonly Dictionary<Guid, string> correlations = [];

        private Harness(
            GlassRepairEstimateGateway gateway,
            IGlassRepairEstimateSessionStore store,
            MemorySessionStore? memory,
            ScriptedGlass mva,
            CaseAuthorityDouble caseAuthority,
            CredentialDouble credentials,
            CustodyDouble custody,
            ImportDouble import,
            TestClock clock,
            IDataProtectionProvider protection,
            Guid caseId,
            Guid engineerId,
            Guid otherEngineerId)
        {
            Gateway = gateway;
            Protection = protection;
            Sessions = store;
            Memory = memory;
            Mva = mva;
            CaseAuthority = caseAuthority;
            Credentials = credentials;
            Custody = custody;
            Import = import;
            Clock = clock;
            CaseId = caseId;
            EngineerId = engineerId;
            OtherEngineerId = otherEngineerId;
            Engineer = ActionActor.Staff(engineerId, [StaffRole.Engineer]);
            OtherEngineer = ActionActor.Staff(otherEngineerId, [StaffRole.Engineer]);
            Credentials.Give(Engineer, engineerId);
            Credentials.Give(OtherEngineer, otherEngineerId, account: "b.engineer");
        }

        public GlassRepairEstimateGateway Gateway { get; }

        public IGlassRepairEstimateSessionStore Sessions { get; }

        public MemorySessionStore Store => Memory
            ?? throw new InvalidOperationException("This harness drives the real store.");

        public ScriptedGlass Mva { get; }

        public CaseAuthorityDouble CaseAuthority { get; }

        public CredentialDouble Credentials { get; }

        public CustodyDouble Custody { get; }

        public ImportDouble Import { get; }

        public TestClock Clock { get; }

        /// <summary>Shared with a second harness that stands in for a restarted host.</summary>
        public IDataProtectionProvider Protection { get; }

        public string CorrelationOf(Guid sessionId) => correlations[sessionId];

        public Guid CaseId { get; }

        public Guid EngineerId { get; }

        public Guid OtherEngineerId { get; }

        public ActionActor Engineer { get; }

        public ActionActor OtherEngineer { get; }

        private MemorySessionStore? Memory { get; }

        public static Harness Create(
            int maximumExportBytes = 16 * 1024 * 1024,
            IGlassRepairEstimateSessionStore? store = null,
            Guid? caseId = null,
            Guid? engineerId = null,
            Guid? otherEngineerId = null,
            IDataProtectionProvider? protection = null,
            ScriptedGlass? provider = null)
        {
            var clock = new TestClock(StartUtc);
            var mva = provider ?? new ScriptedGlass();
            if (provider is null)
            {
                Script(mva);
            }
            var protector = protection ?? new EphemeralDataProtectionProvider();
            var memory = store is null ? new MemorySessionStore(clock) : null;
            if (memory is not null)
            {
                memory.RequestCount = () => mva.Requests.Count;
            }

            var options = new GlassRepairEstimateOptions(
                MvaBase,
                EstimatorBase,
                CallbackBase,
                ProfileId,
                SessionLifetime: TimeSpan.FromHours(8),
                ExportPollInterval: TimeSpan.FromMilliseconds(5),
                ExportTimeout: TimeSpan.FromMilliseconds(50),
                maximumExportBytes);
            var caseAuthority = new CaseAuthorityDouble(new(Registration, MileageMiles));
            var credentials = new CredentialDouble();
            var custody = new CustodyDouble();
            var import = new ImportDouble();
            var sessions = store ?? memory!;
            return new(
                new GlassRepairEstimateGateway(
                    sessions,
                    caseAuthority,
                    credentials,
                    custody,
                    custody,
                    import,
                    new ClientFactory(mva),
                    protector,
                    options,
                    clock),
                sessions,
                memory,
                mva,
                caseAuthority,
                credentials,
                custody,
                import,
                clock,
                protector,
                caseId ?? Guid.NewGuid(),
                engineerId ?? Guid.NewGuid(),
                otherEngineerId ?? Guid.NewGuid());
        }

        public async Task<GlassRepairEstimateSession> LaunchAsync(
            ActionActor? actor = null, string operationKey = "glass-launch-1")
        {
            var launched = await Gateway.LaunchAsync(
                new GlassRepairEstimateLaunchRequest(
                    actor ?? Engineer, CaseId, CaseVersion, LeaseToken, operationKey),
                CancellationToken.None);
            if (launched.State == GlassRepairEstimateSessionState.Active)
            {
                // The one-use token the provider will hand back, read where the
                // operator's browser would read it: out of the launch URL.
                var estimator = await Gateway.GetEstimatorUrlAsync(
                    actor ?? Engineer, launched.Id, CancellationToken.None);
                correlations[launched.Id] = new Uri(QueryOf(estimator!)["caller"]).Segments[^1];
            }

            return launched;
        }

        public Task<GlassRepairEstimateSession> CompleteAsync(
            GlassRepairEstimateSession session,
            ActionActor? actor = null,
            long? expectedVersion = null,
            string? correlation = null,
            string? rawQuery = null) =>
            Gateway.CompleteAsync(
                new GlassRepairEstimateCallback(
                    actor ?? Engineer,
                    session.Id,
                    expectedVersion ?? session.Version,
                    correlation ?? correlations[session.Id],
                    rawQuery ?? SavedQuery),
                CancellationToken.None);
    }
}
