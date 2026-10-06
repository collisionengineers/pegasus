using System.Net;
using System.Text;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The scripted Glass's provider, and the one script both Glass's suites drive.
/// </summary>
/// <remarks>
/// <para>
/// The gateway's own tests and the Case record's web tests answer the same
/// stages from the same answers, in the shapes the supplied captures record —
/// the byte-order marked JSON, the login redirect, the candidate fragment, the
/// <c>start-ere</c> launch URL and the <c>ere_callback_xml</c> relay. A second
/// simulator would let one suite prove a route the other does not have.
/// </para>
/// <para>
/// Nothing here is a real credential, registration, VIN, name, address or
/// provider address: the origins are reserved <c>.test</c> names, the vehicle
/// is the documented estate registration AB12CDE, and the export is the parser
/// suite's own synthetic <c>&lt;Estimation&gt;</c> fixture.
/// </para>
/// </remarks>
internal static class GlassProviderFixture
{
    public const string Registration = GlassEstimateXmlParserTests.GlassExport.Registration;
    public const string NatCode = GlassEstimateXmlParserTests.GlassExport.TypeNumber;
    public const long MileageMiles = 33000;
    public const string VehicleId = "33584499";
    public const string EreId = "1954488";
    public const string EreSession = "me3d4aa4kg79prs0do2emalhc5";
    public const string ProfileId = "4063";

    /// <summary>
    /// The portal's answer for a plate its VRM supplier does not know, as
    /// captured on 2 October 2026: no stock, no lookup result and a type
    /// number that is the JSON <c>false</c>, not a string.
    /// </summary>
    public const string UnknownPlate = "{\"stockcount\":0,\"vehicle_id\":0,\"vrm_lookup\":0,\"natcode\":false}";

    /// <summary>The stock id and the unqualified type number the portal gave the captured placeholder.</summary>
    public const string PlaceholderVehicleId = "33638050";
    public const string PlaceholderNatCode = "49205";

    /// <summary>The insert's answer: an empty title and the new stock id.</summary>
    public static string PlaceholderInserted(string vehicleId = PlaceholderVehicleId) =>
        "{\"title\":\"\",\"message\":\"\",\"new_vehicle_id\":\"" + vehicleId + "\"}";

    /// <summary>The portal's own validation refusal, as captured for a one-digit month.</summary>
    public const string PlaceholderRefused =
        "{\"title\":\"error\",\"message\":{\"Month manufacturer\":{\"stringLengthTooShort\":\"'1' is less than 2 characters long\"}}}";

    /// <summary>The placeholder's detail form: no registration, no mileage, the unqualified type number.</summary>
    public static string PlaceholderDetail(
        string registration = "", long mileage = 0, string vehicleId = PlaceholderVehicleId,
        string natCode = PlaceholderNatCode, string profile = ProfileId, bool locked = false) =>
        VehicleDetail(registration, mileage, vehicleId, natCode, profile, locked);

    /// <summary>The operator's Save &amp; Exit, as the provider composes it.</summary>
    public const string SavedQuery =
        "?Total=0&DoSave=1&ErrMsg=D%3A%2Fvar%2Fdb%2Feremware%2Fresponse%2F1788356510_008376.xml";

    public static readonly Uri MvaBase = new("https://mva.test/");
    public static readonly Uri EstimatorBase = new("https://ere.test/");
    public static readonly Uri CallbackBase = new("https://pegasus.test/");

    public static string LaunchUrl(string? caller = null) =>
        "https://ere.test/ere/acolib/aco_call_xml.php?WorkTime=1788529734"
        + "&inURI=" + Uri.EscapeDataString("D:/in.xml")
        + "&outURI=" + Uri.EscapeDataString("D:/out.xml")
        + "&ucode=glassnet&scode=Test&EuComp=1005_1005_powered_by_eucomp&caller="
        + Uri.EscapeDataString(
            caller ?? $"https://mva.test/ere/ere-callback/ere_id/{EreId}/ere_session/{EreSession}");

    // Named fields and repeated id match the captured MVA detail form. Once an
    // estimate exists the portal renders the repair-profile control locked with
    // the profile that started it selected (glasses12.har entries 1826 and
    // 1840), which is what locked serves.
    public static string VehicleDetail(string registration = Registration, long mileage = MileageMiles,
        string vehicleId = VehicleId, string natCode = NatCode, string profile = ProfileId, bool locked = false) => $"""
        <form><input name="id" value="{vehicleId}" type="hidden" />
        <input name="natcode" value="{natCode}" type="hidden" />
        <input name="registration_number" value="{registration}" />
        <input name="mileage" value="{mileage}" /></form>
        <form><input name="id" value="{vehicleId}" type="hidden" />
        {ProfileSelect(profile, locked)}</form>
        """;

    private static string ProfileSelect(string profile, bool locked) => locked
        ? "<select name=\"ere_profile\" id=\"ere_profile\" tabindex=\"6\" style=\"width:140px\" disabled=\"1\">"
            + $"<option value=\"{profile}\" selected=\"selected\">Repair profile</option></select>"
        : $"<select name=\"ere_profile\"><option value=\"{profile}\">Repair profile</option></select>";

    /// <summary>The provider answers this with a byte-order mark at both ends.</summary>
    public static string StartEre(string launchUrl) =>
        "\uFEFF{\"message\":\"\",\"status\":\"ok\",\"ere_url\":\"" + launchUrl.Replace("/", "\\/", StringComparison.Ordinal) + "\"}\uFEFF";

    public static string Relay(string arguments) =>
        "<html><body onLoad=\"b_load()\"><script>function b_load(){ window.opener.ere_callback_xml( "
        + arguments + "); window.close(); }</script></body></html>";

    /// <summary>The account's stock id for a valued vehicle, and its report's download path.</summary>
    public const string StockedVehicleId = "33636950";
    public const string ReportPath = "/ndp_download/18390/pdf_v34638_20261001152551.pdf";

    /// <summary>A valuation report: only its PDF signature is ever read.</summary>
    public const string ReportPdf = "%PDF-1.4\n% synthetic valuation report\n%%EOF\n";

    /// <summary>
    /// The valuation page in the captured shape: a script first, the Glass's
    /// Trade and Retail Transacted boxes, a commented-out repair-cost box that
    /// is never a figure, the mileage line, and the provider's trailing
    /// byte-order mark.
    /// </summary>
    public static string Values(string trade = "&#163;15,600", string retail = "£17,717") => $$"""
        <script type="text/javascript">
                hideDialog();
                    $('#banner3').attr('src', 'http://test.glassguide.co.uk/panelserver/Directory.asp');
        </script>
        <div id="three_phase_glass_trade" class="three_phase_value_box">
            <div class="three_phase_text">Glass's Trade</div>
                <div class="three_phase_icon_none" title=""></div>
            <div class="three_phase_value_text">{{trade}}</div>
            </div>
        <div id="three_phase_transacted" class="three_phase_value_box">
            <div class="three_phase_text">Retail Transacted</div>
                <div class="three_phase_icon_none" title=""></div>
            <div class="three_phase_value_text">{{retail}}</div>
        </div>
        <!--<div id="three_phase_repair_cost" class="three_phase_value_box">
            <div class="three_phase_text">Total Repair Cost</div>
                <div class="three_phase_icon_none" title=""></div>
            <div class="three_phase_value_text">99,999    </div>
            </div>-->
        <div id="three_phase_mileage" style="left: 190px">
        Mileage 33,000</div>
        """ + "﻿";

    /// <summary>The portal's "valuation not possible": JSON where the page would be.</summary>
    public const string ValuationNotPossible = "{\"success\":false,\"errormsg\":\"Valuation not possible\"}";

    /// <summary>The portal's refusal of a vehicle older than it values, worded as its dialog shows it.</summary>
    public const string ValuationVehicleAge = "{\"success\":false,\"errormsg\":\"Thank you for your valuation request, unfortunately"
        + " the vehicle you have requested is not valued due to the age you have specified. Glass's currently have a rolling"
        + " 20 year valuation period for Cars & Motorcycles and a 15 year rolling valuation period for Light Commercial Vehicles.\"}";

    /// <summary>The print's answer: one download link for the report.</summary>
    public static string ReportLink(string path = ReportPath) =>
        "﻿<h4>Please click on the link below to open or save the report.</h4>"
        + "<a style=\"text-decoration: underline;color: #023899;\" href=\"" + path
        + "\" target=\"_blank\">pdf_v34638_20261001152551.pdf</a>";

    /// <summary>
    /// The provider's own answers, in the shapes the supplied captures record:
    /// byte-order marked JSON, a same-origin login redirect, the candidate
    /// fragment naming its type number, and a launch URL whose caller is a
    /// Glass's ERE callback.
    /// </summary>
    public static void Script(ScriptedGlass mva)
    {
        ArgumentNullException.ThrowIfNull(mva);
        mva.Set("GET /login/index", new(
            HttpStatusCode.OK,
            "<form method=\"post\"><input type=\"hidden\" name=\"csrf_token\" "
            + "value=\"12f5cd3be5909a54ca82f3b3bc674e73\" id=\"csrf_token\" /></form>",
            SetCookie: "NDP=session-cookie; path=/; HttpOnly"));
        mva.Set("POST /login/index", new(
            HttpStatusCode.Found, string.Empty, Location: "https://mva.test/index"));
        mva.Set("GET /index", new(HttpStatusCode.OK, "<div id=\"stocklistGrid\"></div>"));
        mva.Set(
            "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000",
            new(HttpStatusCode.OK, "{\"stockcount\":3,\"vehicle_id\":\"33576604\",\"vrm_lookup\":0}"));
        mva.Set(
            "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000/nostocksearch/1",
            new(HttpStatusCode.OK,
                "\uFEFF{\"stockcount\":0,\"vehicle_id\":0,\"vrm_lookup\":1,\"natcode\":\"" + NatCode + "\"}"));
        mva.Set("GET /three-phase-vehicle/get-vehicles", new(
            HttpStatusCode.OK,
            "{\"success\":true,\"html\":\"<div class=\\\"three_phase_car_info car1\\\">"
            + "Test Make, Test Model, N\\/C: " + NatCode + "<\\/div>\"}"));
        mva.Set("GET /three-phase-vehicle/get-values", new(HttpStatusCode.OK, Values()));
        mva.Set("GET /three-phase-vehicle/refresh-vrm-count", new(HttpStatusCode.OK, string.Empty));
        mva.Set("GET /index/create-new-vehicle", new(
            HttpStatusCode.OK, "\uFEFF{\"vrm\":\"" + Registration + "\",\"id\":\"" + VehicleId + "\"}"));
        // Only a launch whose lookup found nothing asks for this.
        mva.Set("POST /index/unqualified-vehicle-insert", new(HttpStatusCode.OK, PlaceholderInserted()));
        mva.Set("GET /index/vehicle-details/", new(HttpStatusCode.OK, "<div></div>"));
        mva.Set("GET /index/vehicle-detail-inline-fragment/", new(HttpStatusCode.OK, "<div></div>"));
        mva.Set("GET /index/vehicle-details-value/", new(HttpStatusCode.OK, VehicleDetail()));
        mva.Set("GET /index/update-vehicle-select/", new(HttpStatusCode.OK, "{\"error\":false}"));
        mva.Set("GET /index/get-selected-vehicle-count/grid/stocklistGrid", new(
            HttpStatusCode.OK, "{\"grid\":\"stocklistGrid\",\"error\":false,\"count\":\"1\"}"));
        mva.Set("POST /ere/start-ere", new(HttpStatusCode.OK, StartEre(LaunchUrl())));
        // The portal locks the vehicle's repair-profile control as soon as
        // start-ere has allocated the estimate (issue 1026).
        LockProfileOnStart(mva, VehicleDetail(), VehicleDetail(locked: true));
        mva.Set("GET /ere/ere-callback/", new(
            HttpStatusCode.OK,
            Relay("\"928.3\", \"773.58\", \"0\", \"0\", \"352\", \"421.58\", \"0\", " + EreId + ", 1, \"\"")));
        mva.Set("GET /ere/export-vehicle/", new(
            HttpStatusCode.OK, "<a href=\"/ndp_download/export_1.xml\">Download</a>"));
        mva.Set("GET /ndp_download/", new(
            HttpStatusCode.OK,
            GlassEstimateXmlParserTests.GlassExport.BuildXml(),
            ContentType: "application/xml"));
        mva.Set("GET /pdf-print/storess/template/0/printaction/vehicle-valuation/vehicles/", new(
            HttpStatusCode.OK, ReportLink()));
        mva.Set("GET /ndp_download/18390/", new(HttpStatusCode.OK, ReportPdf, ContentType: "application/pdf"));
    }

    /// <summary>
    /// The script for a plate the provider does not know: the stock search
    /// answers nothing, and the vehicle the launch then opens is the
    /// placeholder the insert answered.
    /// </summary>
    public static void ScriptUnknownPlate(ScriptedGlass mva)
    {
        ArgumentNullException.ThrowIfNull(mva);
        mva.Set(
            "GET /index/search-vrm/vrms_reg_no/AB12CDE/valuate/1/vrms_mileage/33000",
            new(HttpStatusCode.OK, UnknownPlate));
        mva.Set("GET /index/vehicle-details-value/", new(HttpStatusCode.OK, PlaceholderDetail()));
        LockProfileOnStart(mva, PlaceholderDetail(), PlaceholderDetail(locked: true));
    }

    /// <summary>
    /// After <c>start-ere</c> is answered the vehicle's details page is the
    /// locked one, as the portal renders a vehicle that has an estimate. A
    /// vehicle created or inserted afterwards has none yet, so a later launch
    /// on the same script gets the open page back. Registering again replaces
    /// the earlier hooks.
    /// </summary>
    private static void LockProfileOnStart(ScriptedGlass mva, string openPage, string lockedPage)
    {
        const string Details = "GET /index/vehicle-details-value/";
        var locked = false;
        mva.OnServed("POST /ere/start-ere", glass =>
        {
            glass.Set(Details, new(HttpStatusCode.OK, lockedPage));
            locked = true;
        });
        foreach (var newVehicle in new[] { "GET /index/create-new-vehicle", "POST /index/unqualified-vehicle-insert" })
        {
            mva.OnServed(newVehicle, glass =>
            {
                if (locked)
                {
                    glass.Set(Details, new(HttpStatusCode.OK, openPage));
                    locked = false;
                }
            });
        }
    }
}

/// <summary>Hands every named client the scripted provider.</summary>
internal sealed class ScriptedClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>
/// One scripted provider answer. Public because a theory names it in its own
/// signature, which xUnit reads from outside this assembly.
/// </summary>
public sealed record Reply(
    HttpStatusCode Status,
    string Body,
    string? Location = null,
    string? SetCookie = null,
    string ContentType = "text/html");

/// <summary>What one request carried, for the assertions that read it back.</summary>
internal sealed record Recorded(
    string Method, string Path, string Query, string? Body, string? Cookie, string? Requested, string? Referer);

/// <summary>
/// The scripted Market Value Assessor. Routes are matched by the longest
/// "METHOD path" prefix, so a stage can be replaced by registering the exact
/// path it uses without restating the rest of the script.
/// </summary>
internal sealed class ScriptedGlass : HttpMessageHandler
{
    private readonly Dictionary<string, Reply> standing = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<Reply>> queued = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Action<ScriptedGlass>> hooks = new(StringComparer.Ordinal);

    public List<Recorded> Requests { get; } = [];

    public void Set(string route, Reply reply) => standing[route] = reply;

    /// <summary>
    /// Runs the action each time the route is chosen for a request, after its
    /// reply is picked, so the provider's own state can move on as a result of
    /// the call (a started estimate locks its vehicle's profile). One action
    /// per route; registering again replaces it. Not on <see cref="Reply"/>,
    /// which is theory data.
    /// </summary>
    public void OnServed(string route, Action<ScriptedGlass> served) => hooks[route] = served;

    public void Enqueue(string route, params Reply[] replies)
    {
        var queue = queued.TryGetValue(route, out var existing) ? existing : queued[route] = new();
        foreach (var reply in replies)
        {
            queue.Enqueue(reply);
        }
    }

    public int Count(string route) => Requests.Count(
        request => $"{request.Method} {request.Path}".StartsWith(route, StringComparison.Ordinal));

    public int IndexOf(string pathFragment) => Requests.FindIndex(
        request => request.Path.Contains(pathFragment, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        var key = $"{request.Method.Method} {uri.AbsolutePath}";
        Requests.Add(new(
            request.Method.Method,
            uri.AbsolutePath,
            uri.Query,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            Header(request, "Cookie"),
            Header(request, "X-Requested-With"),
            request.Headers.Referrer?.AbsoluteUri));

        var route = queued.Keys.Concat(standing.Keys)
            .Where(candidate => key.StartsWith(candidate, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        var reply = route is not null && queued.TryGetValue(route, out var queue) && queue.Count > 0
            ? queue.Dequeue()
            : route is not null && standing.TryGetValue(route, out var standingReply)
                ? standingReply
                : new Reply(HttpStatusCode.NotFound, string.Empty);
        if (route is not null && hooks.TryGetValue(route, out var served))
        {
            served(this);
        }

        var response = new HttpResponseMessage(reply.Status)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(reply.Body)),
        };
        response.Content.Headers.TryAddWithoutValidation("Content-Type", reply.ContentType);
        if (reply.Location is not null)
        {
            response.Headers.TryAddWithoutValidation("Location", reply.Location);
        }
        if (reply.SetCookie is not null)
        {
            response.Headers.TryAddWithoutValidation("Set-Cookie", reply.SetCookie);
        }

        return response;
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join("; ", values) : null;
}
