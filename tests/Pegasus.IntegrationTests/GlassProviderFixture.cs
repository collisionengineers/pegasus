using System.Net;
using System.Text;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The scripted Glass's provider, and the one script both Glass's suites drive
/// (CASE-047 B04).
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

    /// <summary>The provider answers this with a byte-order mark at both ends.</summary>
    public static string StartEre(string launchUrl) =>
        "\uFEFF{\"message\":\"\",\"status\":\"ok\",\"ere_url\":\"" + launchUrl.Replace("/", "\\/", StringComparison.Ordinal) + "\"}\uFEFF";

    public static string Relay(string arguments) =>
        "<html><body onLoad=\"b_load()\"><script>function b_load(){ window.opener.ere_callback_xml( "
        + arguments + "); window.close(); }</script></body></html>";

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
        mva.Set("GET /three-phase-vehicle/get-values", new(HttpStatusCode.OK, "<div></div>"));
        mva.Set("GET /three-phase-vehicle/refresh-vrm-count", new(HttpStatusCode.OK, string.Empty));
        mva.Set("GET /index/create-new-vehicle", new(
            HttpStatusCode.OK, "\uFEFF{\"vrm\":\"" + Registration + "\",\"id\":\"" + VehicleId + "\"}"));
        mva.Set("GET /index/vehicle-details/", new(HttpStatusCode.OK, "<div></div>"));
        mva.Set("GET /index/vehicle-detail-inline-fragment/", new(HttpStatusCode.OK, "<div></div>"));
        mva.Set("GET /index/vehicle-details-value/", new(
            HttpStatusCode.OK,
            "\uFEFF<script>var PROFILE_ID = '" + ProfileId + "'; var NATCODE = '" + NatCode + "';</script>"));
        mva.Set("GET /index/update-vehicle-select/", new(HttpStatusCode.OK, "{\"error\":false}"));
        mva.Set("GET /index/get-selected-vehicle-count/grid/stocklistGrid", new(
            HttpStatusCode.OK, "{\"grid\":\"stocklistGrid\",\"error\":false,\"count\":\"1\"}"));
        mva.Set("POST /ere/start-ere", new(HttpStatusCode.OK, StartEre(LaunchUrl())));
        mva.Set("GET /ere/ere-callback/", new(
            HttpStatusCode.OK,
            Relay("\"928.3\", \"773.58\", \"0\", \"0\", \"352\", \"421.58\", \"0\", " + EreId + ", 1, \"\"")));
        mva.Set("GET /ere/export-vehicle/", new(
            HttpStatusCode.OK, "<a href=\"/ndp_download/export_1.xml\">Download</a>"));
        mva.Set("GET /ndp_download/", new(
            HttpStatusCode.OK,
            GlassEstimateXmlParserTests.GlassExport.BuildXml(),
            ContentType: "application/xml"));
    }
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

    public List<Recorded> Requests { get; } = [];

    public void Set(string route, Reply reply) => standing[route] = reply;

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
