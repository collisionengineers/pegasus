using System.Net;
using System.Text;
using Pegasus.Infrastructure.Glass;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

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

    /// <summary>A synthetic VIN in ISO 3779's shape, never a captured one.</summary>
    public const string Vin = "TESTVEH0A1B2C3D45";

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

    /// <summary>The placeholder's detail form: no registration, no mileage, no VIN, the unqualified type number.</summary>
    public static string PlaceholderDetail(
        string registration = "", long mileage = 0, string vehicleId = PlaceholderVehicleId,
        string natCode = PlaceholderNatCode, string profile = ProfileId, bool locked = false) =>
        VehicleDetail(registration, mileage, vehicleId, natCode, profile, locked, vin: string.Empty);

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

    // Named fields and repeated id match the captured MVA detail form, whose
    // vin control carries the VIN Glass's looked up from the plate. Once an
    // estimate exists the portal renders the repair-profile control locked with
    // the profile that started it selected (glasses12.har entries 1826 and
    // 1840), which is what locked serves.
    public static string VehicleDetail(string registration = Registration, long mileage = MileageMiles,
        string vehicleId = VehicleId, string natCode = NatCode, string profile = ProfileId, bool locked = false,
        string vin = Vin) => $"""
        <form><input name="id" value="{vehicleId}" type="hidden" />
        <input name="natcode" value="{natCode}" type="hidden" />
        <input name="registration_number" value="{registration}" />
        <input name="mileage" value="{mileage}" />
        <input type="text" name="vin" id="vin" value="{vin}" maxlength="17" /></form>
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

    /// <summary>The valuation report for the fixture's own vehicle: a real PDF that names its plate.</summary>
    public static byte[] ReportPdf { get; } = ValuationReport(Registration);

    /// <summary>
    /// A one-page "Values Only" report in the captured shape, whose text names
    /// the vehicle's plate as Glass's prints it, so the filing's registration
    /// check reads it as it reads the portal's own.
    /// </summary>
    public static byte[] ValuationReport(string registration)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Vehicle Valuation Report", 12, new PdfPoint(36, 780), font);
        page.AddText("Synthetic Sportage 1.6 Station Wagon 5d", 10, new PdfPoint(36, 760), font);
        page.AddText("- " + registration, 10, new PdfPoint(36, 745), font);
        return builder.Build();
    }

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
        mva.Set("GET /ndp_download/18390/", new(
            HttpStatusCode.OK, string.Empty, ContentType: "application/pdf", Bytes: ReportPdf));
        ScriptEditor(mva);
    }

    /// <summary>
    /// The return address the editor's captured pages carry. Each page is
    /// served with it replaced by the session's own, read from the launch
    /// address the editor is opened with, as Glass's would serve it.
    /// </summary>
    private const string CapturedCallback =
        "https://pegasus.test/Integrations/Glass/Callback/TESTTOKEN0000000000000000000000000000000000";

    /// <summary>
    /// Glass's editor on the estimator's host, answering from the pages cut
    /// from browser sessions (<c>GlassEditorPages/</c>): the launch page, the
    /// two opening pages, the estimate with its totals (empty until a
    /// position is posted), the records of the positions the fixtures hold
    /// (<c>mcode.php</c>; any other is not found), the calculation report,
    /// the special list, the dialogs and answers of two special operations
    /// (<c>AC0000073</c> and <c>AC0000075</c>), the part list
    /// (<see cref="BuildPartList"/>), the captured equipment maps of rows 0,
    /// 274, 682 and 821 and one with no variants for any other row, the
    /// answer to a stored equipment answer (<c>globcrit</c>), the left front
    /// door's dialog for its rows <c>771|772</c> and for the right door's
    /// <c>790|791</c> (so that one serves the wrong side), cut with the
    /// script values a changed operation reads, its "Mandatory parts:" page
    /// and answer, the front bumper lining's dialog for any pair of its row 0
    /// (captured for <c>0|5</c>, so any other is the wrong variant) with its
    /// "Mandatory parts:" page and answer, the left mirror's dialog for
    /// <c>822|829</c> and its answer, the front bumper side reinforcement's
    /// left dialog (<see cref="ReinforcementLeft"/>), whose answer to a post
    /// with "Both" ticked sends the list to the right one
    /// (<see cref="ReinforcementRight"/>), each with its captured answer
    /// (NEWHAR of 18 November 2026), and Save and Exit's three
    /// pages. One scripted editor for the editor client's own tests is
    /// <see cref="ScriptedEditor"/>; this one is the gateway's, on the same
    /// provider as the portal, so a build runs end to end.
    /// </summary>
    public static void ScriptEditor(ScriptedGlass glass)
    {
        ArgumentNullException.ThrowIfNull(glass);
        string? callback = null;
        glass.Respond("GET /ere/acolib/aco_call_xml.php", request =>
        {
            callback = QueryValue(request.Query, "caller") ?? callback;
            return EditorPage(
                request.Query.Contains("DoSave=", StringComparison.Ordinal) ? "return.html" : "launch.html", callback);
        });
        glass.Set("GET /ere/aco/aco.php", Editor("<html><body></body></html>"));
        glass.Respond("GET /ere/aco/aco2.php", _ => EditorPage("aco2.html", null));
        glass.Respond("GET /ere/aco/selected.php", _ => glass.Count("POST /ere/aco/compproc.php") == 0
            ? EditorPage("selected_empty.html", null)
            : EditorPage("selected.html", null));
        glass.Respond("GET /ere/aco/mcode.php", request => PositionRecord(QueryValue(request.Query, "partid")));
        glass.Respond("GET /ere/aco/report.php", _ => EditorPage("report.html", null));
        glass.Respond("GET /ere/aco/list_1G.php", _ => EditorPage("special_list.html", null));
        glass.Respond("GET /ere/aco/list.php", _ => Editor(BuildPartList()));
        glass.Respond("GET /ere/aco/part.php", request => QueryValue(request.Query, "mcode") switch
        {
            "822|829" => EditorPage("mirror_left.dialog.html", null),
            ReinforcementLeft => EditorPage("reinforcement_left_both.dialog.html", null),
            ReinforcementRight => EditorPage("reinforcement_right.dialog.html", null),
            _ => SpecialOperation(request.Query, "dialog"),
        });
        glass.Respond("GET /ere/aco/comp.php", request => QueryValue(request.Query, "mcode") switch
        {
            "771|772" or "790|791" => EditorPage("door_left_operations.dialog.html", null),
            { } rows when rows.StartsWith("0|", StringComparison.Ordinal) => EditorPage("bumper_part.dialog.html", null),
            _ => new Reply(HttpStatusCode.NotFound, string.Empty),
        });
        glass.Respond("POST /ere/aco/compproc.php", request => QueryValue(request.Query, "mcode") switch
        {
            "771|772" => EditorPage(
                QueryValue(request.Query, "BX") is null ? "door_left_part.answer.html" : "door_left_part.confirm_answer.html", null),
            "0|5" => EditorPage(
                QueryValue(request.Query, "BX") is null ? "bumper_part.answer.html" : "bumper_part.confirm_answer.html", null),
            "822|829" => EditorPage("mirror_left.answer.html", null),
            ReinforcementLeft => EditorPage("reinforcement_left_both.answer.html", null),
            ReinforcementRight => EditorPage("reinforcement_right.answer.html", null),
            _ => SpecialOperation(request.Query, "answer"),
        });
        glass.Respond("GET /ere/aco/service.php", request =>
            QueryValue(request.Query, "globcrit") is not null ? EditorPage("globcrit.answer.html", null)
            : QueryValue(request.Query, "rowid") is { } row ? EquipmentMap(row)
            : EditorPage("save.answer.html", null));
        glass.Respond("GET /ere/aco/close.php", _ => EditorPage("close.html", callback));
    }

    /// <summary>
    /// The part list the scripted editor serves: the blocks cut from the list
    /// of 7 September 2026 (<c>part_list.html</c>, with that page's criteria),
    /// then three blocks made for this script in the same row format: the
    /// left and right front door shells, whose dialog the captures hold for
    /// the left one only (rows <c>771|772</c>, code 2020001, 18 November
    /// 2026), the BMW's left mirror (<see cref="MirrorRows"/>) and its roof
    /// panel (<see cref="RoofRows"/>). Each
    /// door's block needs no equipment, and the door can be painted (marked
    /// <c>L</c>), as the browser's use of <c>comp.php</c> for it shows.
    /// </summary>
    public static string BuildPartList() =>
        GlassEditorFixture.Text("part_list.html").Replace(
            "\n</script>",
            "\n" + string.Join('\n', DoorRows.Concat(MirrorRows).Concat(ReinforcementRows).Concat(RoofRows).Concat(ShapeRows).Select(row => "partlist.addPart(\"" + row + "\");"))
            + "\n" + string.Join('\n', MirrorCriteria.Select(line => "partlist.addCrit(\"" + line + "\");")) + "\n</script>",
            StringComparison.Ordinal);

    /// <summary>
    /// The BMW's left mirror (code 2027021), made for this script in the
    /// list's row format, as no BMW part list is captured (design 10 section
    /// 5): the head 821, its part variants 822 to 828 as its captured map
    /// (<c>equipment_821.html</c>) names them, and its Replace and refit
    /// time rows. Row 822's number and price and row 829's hours are those
    /// its captured dialog (<c>822|829</c>, 18 November 2026) serves; every
    /// other number and figure is made up. None can be painted, as the
    /// browser's use of <c>part.php</c> for it shows.
    /// </summary>
    private static readonly string[] MirrorRows =
    [
        "821|pbclTId|HTB3-202_809|701300250101|40|2027021||51167459045|350.00|ERAEI |||||||Front Door Ext Mirror (Elec/Htd)|||||||||||KS|||||830|0|0.0.0.10",
        "822|plTI|||||21115|51167459047|383.48|ERAEI |||||||||||||||||||||||831|0|0.0.0.10",
        "823|plTI|||||21115_21117|51167459048|390.00|ERAEI |||||||||||||||||||||||832|0|0.0.0.10",
        "824|plTI|||||21115_21117_85142|51167459049|395.00|ERAEI |||||||||||||||||||||||833|0|0.0.0.10",
        "825|plTI|||||21115_85142|51167459050|385.00|ERAEI |||||||||||||||||||||||834|0|0.0.0.10",
        "826|plTI|||||21117|51167459051|360.00|ERAEI |||||||||||||||||||||||835|0|0.0.0.10",
        "827|plTI|||||21117_85142|51167459052|365.00|ERAEI |||||||||||||||||||||||836|0|0.0.0.10",
        "828|plTI|||||85142|51167459053|355.00|ERAEI |||||||||||||||||||||||837|0|0.0.0.10",
        "829|tlTI|||||||0.80|ER|||||||||||||||||||||||838|0|0.0.0.10",
        "830|tlTI|||||||0.60|AE|||||||||||||||||||||||839|0|0.0.0.10",
    ];

    /// <summary>The rows the left reinforcement's dialog is asked for: its head and its Replace time row.</summary>
    public const string ReinforcementLeft = "193|196";

    /// <summary>The rows the left dialog's answer names for the right side, as the editor's list asks for them (NEWHAR #1248).</summary>
    public const string ReinforcementRight = "1783|ER|1786|AE|1792";

    /// <summary>
    /// The BMW's front bumper side reinforcement (codes 1012401 left and
    /// 1012402 right), made for this script in the list's row format: each
    /// side's head and its Replace time row. The row ids, codes, numbers,
    /// price and hours are those its captured dialogs serve (NEWHAR #1224 and
    /// #1248, 18 November 2026); the rest is made up. Neither side can be
    /// painted, as the browser's use of <c>part.php</c> for them shows.
    /// </summary>
    private static readonly string[] ReinforcementRows =
    [
        "193|pbclTId|HTB3-202_809|701300250101|41|1012401||51117266193|38.68|ERAEI |||||||Front Bumper Side Reinforcement|||||||||||KS|||||1783|0|0.0.0.11",
        "196|tlTI|||||||2.20|ER|||||||||||||||||||||||1786|0|0.0.0.11",
        "1783|pbcrTId|HTB3-202_809|701300250203|41|1012402||51117266194|38.68|ERAEI |||||||Front Bumper Side Reinforcement|||||||||||KS|||||193|0|0.0.0.11",
        "1786|trTI|||||||2.20|ER|||||||||||||||||||||||196|0|0.0.0.11",
    ];

    /// <summary>The BMW's criteria the mirror's block names, as its <c>reqglobcrit2g</c> served them (r18 #745).</summary>
    /// <summary>
    /// The BMW's roof panel as its part list served it on 19 November 2026
    /// (operatorwalkglass.har #369), byte for byte: two heads of one part
    /// (rows 525 and 2697), each hidden by the panoramic roof (20011, field
    /// 11) and asking no question, with their Replace and refit time rows.
    /// The editor's own click asks for <c>525|526</c> and <c>2697|2698</c>.
    /// </summary>
    internal static readonly string[] RoofRows =
    [
        "525|pbcdLg|STA5-201_204||105|2010000||41007494007|1179.04|ERAEI ||20011|||||Roof Panel|Part made of steel||||||||||ST|E2341BSI2341BS|||272|2697||0.0.0.42",
        "526|t|||||||12.50|ER|||||||||||||||||||||||2698||0.0.0.42",
        "527|t|||||||12.50|AE|||||||||||||||||||||||2699||0.0.0.42",
        "2697|pbcdLg|STA5-201_204||2|2010000||41007494007|1179.04|ERAEI ||20011|||||Roof Panel|Part made of steel||||||||||ST|E2341BSI2341BS|||272|525||0.3.0.3",
        "2698|t|||||||12.50|ER|||||||||||||||||||||||526||0.3.0.3",
        "2699|t|||||||12.50|AE|||||||||||||||||||||||527||0.3.0.3",
    ];

    /// <summary>
    /// The two shapes no captured list holds, made by altering real blocks of
    /// the list of 19 November 2026 (glasss2missing.har #373), as the offline
    /// run of the editor's own code altered them (shape-dump.mjs): the front
    /// door lower seal (row 177, code 2021981) marked both left and right
    /// (<c>pbclTId</c> to <c>pbclrTId</c>, its time rows 178 and 179
    /// <c>tlTI</c> to <c>tlrTI</c>, its right twin's rows 180 to 182 left
    /// out), one row serving both sides, which the editor clicks to
    /// <c>177|178</c> on either side; and the front bumper lower air guide
    /// (row 51, code 1017150) with its two time rows (52 and 53) left out,
    /// which the editor asks its dialog for alone (<c>51</c>).
    /// </summary>
    internal static readonly string[] ShapeRows =
    [
        "177|pbclrTId|HTB3-202_809||51|2021981||72328TV0E01|103.77|ERAEI |||||||Front Door Lower Seal|Part made of plastic||||||||||KS|||||1911|180|0.0.0.36",
        "178|tlrTI|||||||0.10|ER|||||||||||||||||||||||1912|181|0.0.0.36",
        "179|tlrTI|||||||0.10|AE|||||||||||||||||||||||1913|182|0.0.0.36",
        "51|pbcd|HTB3-107_108_109||82|1017150||71181TV0E10|72.70|ERAEI |||||||Front Bumper Lower Air Guide|Part made of plastic||||||||||KS|||||633||0.0.0.9",
    ];

    /// <summary>The panoramic roof as that list served it: the vehicle does not have it, so the roof panel is shown.</summary>
    internal const string NoPanoramicRoof = "20011|IgNtp|0|_|0|Panoramic Roof|0|0|140|340|0|";

    /// <summary>The same line for a vehicle that has the panoramic roof, which hides the roof panel.</summary>
    internal const string PanoramicRoof = "20011|IgNtp|1|_|0|Panoramic Roof|0|0|140|340|0|";

    /// <summary>
    /// The panoramic roof as the SEAT's list (<c>part_list.html</c>) already
    /// carries it, not set. The scripted list keeps that line: a vehicle's
    /// criteria are read first line first, so a second line for 20011 would
    /// never be read.
    /// </summary>
    internal const string SeatNoPanoramicRoof = "20011|IgNpt|0|_|0|Panoramic Roof|0|0|123|123|0|";

    /// <summary>The SEAT's line set: a vehicle with the panoramic roof, which hides the roof panel.</summary>
    internal const string SeatPanoramicRoof = "20011|IgNpt|1|_|0|Panoramic Roof|0|0|123|123|0|";

    private static readonly string[] MirrorCriteria =
    [
        "21115|pNg|0|_|0|Outside Mirrors Automatic Dimming|0|0|240|239|0|",
        "21117|pNg|0|_|0|Outside Mirrors with Memory|0|0|240|240|0|",
        "85142|pNg|0|_|0|Window Mouldings Gloss Black|0|0|160|160|1|",
    ];

    /// <summary>The rows whose captured equipment map the scripted editor serves, by row.</summary>
    private static readonly Dictionary<string, string> CapturedMaps = new(StringComparer.Ordinal)
    {
        ["0"] = "equipment_0.html",
        ["274"] = "equipment_274.html",
        ["682"] = "equipment_682.html",
        ["821"] = "equipment_821.html",
    };

    /// <summary>A row's equipment map: its captured one, or one with no variants.</summary>
    private static Reply EquipmentMap(string row) =>
        CapturedMaps.TryGetValue(row, out var file) ? EditorPage(file, null) : Editor(EquipmentMapWithNoVariants(row));

    /// <summary>The two door blocks of <see cref="BuildPartList"/>: the head row, its Replace time row, its refit time row.</summary>
    private static readonly string[] DoorRows =
    [
        "771|pbclTIdLg|HTB3-202_809|701300250101|36|2020001||41009628759|545.24|ERAEI |||||||Front Door Shell|||||||||||KS|||||790|0|0.0.0.9",
        "772|tlTI|||||||2.90|ER|||||||||||||||||||||||791|0|0.0.0.9",
        "773|tlTI|||||||2.40|AE|||||||||||||||||||||||792|0|0.0.0.9",
        "790|pbcrTIdLg|HTB3-202_809|701300250101|36|2020002||41009628760|545.24|ERAEI |||||||Front Door Shell|||||||||||KS|||||771|0|0.0.0.9",
        "791|trTI|||||||2.90|ER|||||||||||||||||||||||772|0|0.0.0.9",
        "792|trTI|||||||2.40|AE|||||||||||||||||||||||773|0|0.0.0.9",
    ];

    /// <summary>
    /// The equipment map Glass's serves for a row with no variants, for the
    /// row's own code (the shape the program read for row 753 on 18 November
    /// 2026: <c>[ ["I","2020001"],[],[],[] ]</c>).
    /// </summary>
    private static string EquipmentMapWithNoVariants(string row)
    {
        var code = BuildPartList().Split('\n')
            .Select(line => line.Split('|'))
            .FirstOrDefault(fields => fields.Length > 5 && fields[0] == "partlist.addPart(\"" + row)?[5] ?? string.Empty;
        return "<script>\nvar aCritMap = [ [\"I\",\"" + code + "\"],[],[],[] ];\n</script>\n";
    }

    /// <summary>A captured editor page, its return address replaced by the session's own.</summary>
    public static Reply EditorPage(string file, string? callback)
    {
        var text = GlassEditorFixture.Text(file);
        if (callback is not null)
        {
            text = text
                .Replace(CapturedCallback, callback, StringComparison.Ordinal)
                .Replace(CapturedCallback.Replace("/", "\\/", StringComparison.Ordinal),
                    callback.Replace("/", "\\/", StringComparison.Ordinal), StringComparison.Ordinal);
        }

        return Editor(text);
    }

    /// <summary>An editor answer, sent as windows-1252 as the editor sends it.</summary>
    public static Reply Editor(string body) =>
        new(HttpStatusCode.OK, body, Bytes: GlassEditorText.Encode(body));

    /// <summary>The fixtures' position records by their ids (<c>position</c>, <c>positionLeft</c>, …).</summary>
    private static readonly string[] PositionPages = ["position", "positionLeft", "positionPaint", "positionSpecial"];

    /// <summary>The record of a position the fixtures hold, as <c>mcode.php</c> answers it, or a 404 for any other.</summary>
    private static Reply PositionRecord(string? positionId)
    {
        var page = PositionPages
            .Select(GlassEditorFixture.Page)
            .FirstOrDefault(candidate => candidate.GetProperty("id").GetString() == positionId);
        return page.ValueKind == System.Text.Json.JsonValueKind.Object
            ? new(HttpStatusCode.OK, GlassEditorFixture.Text(page.GetProperty("file").GetString()!), ContentType: "application/json")
            : new(HttpStatusCode.NotFound, string.Empty);
    }

    /// <summary>The dialog or the answer of the special operation a request names, or a 404 for any other.</summary>
    private static Reply SpecialOperation(string query, string page) =>
        QueryValue(query, "mcode") switch
        {
            "AC0000073" => EditorPage("special_fixed_price." + page + ".html", null),
            "AC0000075" => EditorPage("special_hours." + page + ".html", null),
            _ => new Reply(HttpStatusCode.NotFound, string.Empty),
        };

    /// <summary>One parameter of a request's query, unescaped.</summary>
    private static string? QueryValue(string query, string name)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && string.Equals(part[..separator], name, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(part[(separator + 1)..]);
            }
        }

        return null;
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
    string ContentType = "text/html",
    byte[]? Bytes = null);

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
    private readonly Dictionary<string, Func<Recorded, Reply>> responders = new(StringComparer.Ordinal);

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

    /// <summary>
    /// Answers the route from the request itself, for a page that must name
    /// what the request named (the editor's pages name the session's return
    /// address). A reply queued for the route is still served first; a
    /// standing one is not served while a responder is set.
    /// </summary>
    public void Respond(string route, Func<Recorded, Reply> responder) => responders[route] = responder;

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
        var recorded = new Recorded(
            request.Method.Method,
            uri.AbsolutePath,
            uri.Query,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken),
            Header(request, "Cookie"),
            Header(request, "X-Requested-With"),
            request.Headers.Referrer?.AbsoluteUri);
        Requests.Add(recorded);

        var route = queued.Keys.Concat(standing.Keys).Concat(responders.Keys)
            .Where(candidate => key.StartsWith(candidate, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.Length)
            .FirstOrDefault();
        var reply = route is not null && queued.TryGetValue(route, out var queue) && queue.Count > 0
            ? queue.Dequeue()
            : route is not null && responders.TryGetValue(route, out var responder)
                ? responder(recorded)
                : route is not null && standing.TryGetValue(route, out var standingReply)
                    ? standingReply
                    : new Reply(HttpStatusCode.NotFound, string.Empty);
        if (route is not null && hooks.TryGetValue(route, out var served))
        {
            served(this);
        }

        var response = new HttpResponseMessage(reply.Status)
        {
            Content = new ByteArrayContent(reply.Bytes ?? Encoding.UTF8.GetBytes(reply.Body)),
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
