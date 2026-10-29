using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-18/S05 items 6-7: the web-facing surface for a principal's default
/// inspection-location choice and report settings. A physical default
/// change is retained alongside its reason without ever implying CE attendance
/// or changing B's separate assessment method.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class OrganizationDirectoryWebTests
{
    [Fact]
    public async Task PrincipalSettingsPageSavesDefaultLocationAndReportSettingsIndependently()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Directory Web Caller Provider", "DIRW");

        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var settingsHtml = await GetContactSettingsAsync(client, $"{settingsPath}?tab=default-inspection-location");
        Assert.Contains(">Report generation</a>", settingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Report settings", settingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("ReportGenerationPolicy", settingsHtml, StringComparison.Ordinal);

        var locationForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
            ["LocationOperationKey"] = InputValue(settingsHtml, "LocationOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(settingsHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
            ["LocationIsImageBasedAssessment"] = bool.FalseString,
            ["LocationLabel"] = "Directory Web Caller Yard",
            ["LocationAddress"] = "1 Directory Way, DW1 2EF",
            ["LocationPostcode"] = "DW1 2EF"
        };
        using var locationPost = await client.PostAsync(
            $"{settingsPath}?handler=UpdateLocation",
            new FormUrlEncodedContent(locationForm));
        Assert.True(
            locationPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {locationPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(locationPost)}");
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND DefaultInspectionAddress = '1 Directory Way, DW1 2EF' AND DefaultInspectionLocationLabel = 'Directory Web Caller Yard';"));

        // The save comes back to its panel, with the notice inside it.
        Assert.Equal($"{settingsPath}?tab=default-inspection-location", locationPost.Headers.Location?.OriginalString);
        var locationSavedHtml = await GetContactSettingsAsync(client, locationPost.Headers.Location!.OriginalString);
        var notice = locationSavedHtml.IndexOf("The principal&#x27;s default inspection location was updated.", StringComparison.Ordinal);
        Assert.True(
            notice > locationSavedHtml.IndexOf("id=\"principal-location-title\"", StringComparison.Ordinal)
                && notice < locationSavedHtml.IndexOf("name=\"LocationLabel\"", StringComparison.Ordinal),
            "The notice must show at the top of the Default inspection location panel.");

        var settingsAfterLocationHtml = await GetContactSettingsAsync(client, $"{settingsPath}?tab=report-generation");
        var reportSettingsForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsAfterLocationHtml, "__RequestVerificationToken"),
            ["ReportSettingsOperationKey"] = InputValue(settingsAfterLocationHtml, "ReportSettingsOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(settingsAfterLocationHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(settingsAfterLocationHtml, "ExpectedVersion"),
            ["DefaultFee"] = "225.50"
        };
        // A new Principal starts at the standard default fee.
        Assert.Equal("180.00", InputValue(settingsAfterLocationHtml, "DefaultFee"));
        using var reportSettingsPost = await client.PostAsync(
            $"{settingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(reportSettingsForm));
        Assert.True(
            reportSettingsPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {reportSettingsPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(reportSettingsPost)}");
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND DefaultInspectionAddress = '1 Directory Way, DW1 2EF' AND DefaultFee = 225.50;"));

        using var indexGet = await client.GetAsync("/Administration/Contacts");
        var indexHtml = await indexGet.Content.ReadAsStringAsync();
        indexGet.EnsureSuccessStatusCode();
        Assert.Contains("Directory Web Caller Provider", indexHtml, StringComparison.Ordinal);
        using var settingsAfterReportSettingsGet = await client.GetAsync($"{settingsPath}?tab=default-inspection-location");
        settingsAfterReportSettingsGet.EnsureSuccessStatusCode();
        Assert.Contains("Directory Web Caller Yard", await settingsAfterReportSettingsGet.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaleContactVersionOnNestedReportSettingsReturnsTheConflictWithoutChangingThePrincipal()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Stale contact version provider", "STALEW");
        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var settingsHtml = await GetContactSettingsAsync(client, $"{settingsPath}?tab=report-generation");
        var feeBefore = await (await factory.DatabaseAsync).ScalarAsync<decimal>(
            $"SELECT DefaultFee FROM Principals WHERE Id = '{principalId:D}';");

        await (await factory.DatabaseAsync).ExecuteAsync(
            $"UPDATE Organizations SET Version = Version + 1 WHERE Id = '{contactId:D}';");
        var reportSettingsForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
            ["ReportSettingsOperationKey"] = InputValue(settingsHtml, "ReportSettingsOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(settingsHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
            ["DefaultFee"] = "250.00"
        };

        using var response = await client.PostAsync(
            $"{settingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(reportSettingsForm));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refusedHtml = await response.Content.ReadAsStringAsync();
        var refusal = refusedHtml.IndexOf("This contact changed. Reload it before making further changes.", StringComparison.Ordinal);
        var panel = refusedHtml.IndexOf("id=\"principal-report-title\"", StringComparison.Ordinal);
        // The refusal shows at the top of the Report generation panel, which stays open.
        Assert.True(
            panel >= 0 && refusal > panel
                && refusal < refusedHtml.IndexOf("name=\"DefaultFee\"", StringComparison.Ordinal),
            "The refusal must show at the top of the Report generation panel.");
        Assert.Contains("value=\"250.00\"", refusedHtml, StringComparison.Ordinal);
        Assert.Equal(
            feeBefore,
            await (await factory.DatabaseAsync).ScalarAsync<decimal>(
                $"SELECT DefaultFee FROM Principals WHERE Id = '{principalId:D}';"));
    }

    /// <summary>
    /// C06 review R-4: item 6's expected output — the handoff's "QDOS
    /// defaults to the Image Based Assessment location" — was otherwise
    /// unasserted anywhere. Image Based Assessment is represented as every
    /// <c>Default*</c> column being null, which is indistinguishable from
    /// "never set" by reading the schema alone; this asserts what the page
    /// actually renders for the seeded row.
    /// </summary>
    [Fact]
    public async Task QdosPrincipalSettingsDefaultToImageBasedAssessmentAndShowAcceptedDomains()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var principalId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'QDOS';");
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Id = '" + principalId + "';");
        var locationHtml = await GetContactSettingsAsync(
            client, $"/Administration/Contacts/Edit/{contactId:D}?tab=default-inspection-location");
        Assert.Matches(
            """<input\b(?=[^>]*name="LocationIsImageBasedAssessment")(?=[^>]*checked="checked")[^>]*>""",
            locationHtml);
        var html = await GetContactSettingsAsync(client, $"/Administration/Contacts/Edit/{contactId:D}");
        // Accepted e-mail identities is a greyed box in the contact form.
        Assert.Matches(
            """<input id="principal-accepted-identities" value="[^"]+" readonly[^>]*>""",
            html);
        foreach (var domain in Pegasus.Core.Intake.PrincipalMailRoutePolicy.AcceptedIdentities["QDOS"])
        {
            Assert.Contains(domain, html, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A Principal with no accepted e-mail route is a normal state (18 of 33 at
    /// 13 October 2026); its box says so rather than standing empty.
    /// </summary>
    [Fact]
    public async Task APrincipalWithNoAcceptedRouteSaysSo()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        Assert.False(Pegasus.Core.Intake.PrincipalMailRoutePolicy.AcceptedIdentities.ContainsKey("WALKER"));
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Code = 'WALKER';");
        var html = await GetContactSettingsAsync(client, $"/Administration/Contacts/Edit/{contactId:D}");

        Assert.Matches(
            $"""<input id="principal-accepted-identities" value="{Regex.Escape(OperatorLabels.PrincipalAdministration.NoAcceptedRoute)}" readonly[^>]*>""",
            html);
    }

    /// <summary>
    /// Record notes (Contacts, 13 September): a Principal (and a Claim source) carries
    /// one "Notes on every Case" text, edited on its contact. Type, Principal code and
    /// Inspection mode are greyed read-only boxes there (operator, 13 October 2026).
    /// </summary>
    [Fact]
    public async Task APrincipalContactEditsItsNotesOnEveryCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var principalId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'QDOS';");
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Id = '" + principalId + "';");
        var path = $"/Administration/Contacts/Edit/{contactId:D}";

        var editor = await GetContactSettingsAsync(client, path);

        Assert.Matches(
            """<div class="field" data-notes-on-every-case><label for="NotesOnEveryCase">Notes on every Case</label><textarea[^>]*name="NotesOnEveryCase"[^>]*>""",
            editor);
        // A Principal contact never links to a principal.
        Assert.DoesNotContain("linked-principals-title", editor, StringComparison.Ordinal);
        Assert.Matches("""<input id="contact-type" value="Principal" readonly""", editor);
        Assert.Matches("""<input(?=[^>]*name="PrincipalCode")(?=[^>]*value="QDOS")(?=[^>]*readonly)[^>]*>""", editor);
        Assert.Matches("""<input id="principal-inspection-mode" value="[^"]+" readonly""", editor);
        Assert.DoesNotContain("Guidance for new Cases", editor, StringComparison.Ordinal);

        // R118: the form posts the Principal's current mode, so QDOS stays Image Based Assessment.
        var inspectionMode = InputValue(editor, "PrincipalInspectionMode");
        Assert.Equal(nameof(Pegasus.Core.Cases.CaseInspectionMode.ImageBasedAssessment), inspectionMode);
        // Save posts only the contact form: the Replace dialog's fields are absent.
        using var saved = await client.PostAsync(
            $"{path}?handler=Save",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(editor, "__RequestVerificationToken"),
                ["ContactId"] = InputValue(editor, "ContactId"),
                ["ExpectedVersion"] = InputValue(editor, "ExpectedVersion"),
                ["OperationKey"] = InputValue(editor, "OperationKey"),
                ["Name"] = InputValue(editor, "Name"),
                ["PrincipalCode"] = InputValue(editor, "PrincipalCode"),
                ["PrincipalInspectionMode"] = inspectionMode,
                ["NotesOnEveryCase"] = "Ring the claimant before inspection.",
                ["Active"] = bool.TrueString
            }));

        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(saved)}");
        Assert.Equal(
            "Ring the claimant before inspection.",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT NotesOnEveryCase FROM Organizations WHERE Id = '{contactId:D}';"));
        Assert.Equal(
            Pegasus.Core.Cases.PrincipalInspectionModePolicy.ImageBasedAssessmentCode,
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT InspectionMode FROM Principals WHERE Id = '{principalId:D}';"));
    }

    /// <summary>
    /// #1552: Notes on every Case have one writer, the contact's Save. Report
    /// generation saves the Default fee and leaves the notes as they are, even
    /// when a hand-built post carries a notes field.
    /// </summary>
    [Fact]
    public async Task ReportSettingsLeaveTheNotesOnEveryCaseAlone()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Report settings notes provider", "RSNOTE");
        await (await factory.DatabaseAsync).ExecuteAsync(
            $"UPDATE Organizations SET NotesOnEveryCase = N'Ring the claimant before inspection.' WHERE Id = '{contactId:D}';");
        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var settingsHtml = await GetContactSettingsAsync(client, $"{settingsPath}?tab=report-generation");

        using var post = await client.PostAsync(
            $"{settingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
                ["ReportSettingsOperationKey"] = InputValue(settingsHtml, "ReportSettingsOperationKey"),
                ["PrincipalExpectedVersion"] = InputValue(settingsHtml, "PrincipalExpectedVersion"),
                ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
                ["DefaultFee"] = "199.00",
                ["NotesOnEveryCase"] = "Notes from the report settings form."
            }));

        Assert.True(
            post.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {post.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(post)}");
        Assert.Equal(199.00m, await (await factory.DatabaseAsync).ScalarAsync<decimal>(
            $"SELECT DefaultFee FROM Principals WHERE Id = '{principalId:D}';"));
        Assert.Equal(
            "Ring the claimant before inspection.",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT NotesOnEveryCase FROM Organizations WHERE Id = '{contactId:D}';"));
    }

    /// <summary>
    /// #1550: the Default inspection location panel shows a refused save's
    /// sentence once, at the top of the panel, and keeps what was typed.
    /// </summary>
    [Fact]
    public async Task ABlankDefaultLocationAddressIsRefusedOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Blank location address provider", "BLANKLOC");
        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var settingsHtml = await GetContactSettingsAsync(client, $"{settingsPath}?tab=default-inspection-location");

        using var post = await client.PostAsync(
            $"{settingsPath}?handler=UpdateLocation",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
                ["LocationOperationKey"] = InputValue(settingsHtml, "LocationOperationKey"),
                ["PrincipalExpectedVersion"] = InputValue(settingsHtml, "PrincipalExpectedVersion"),
                ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
                ["LocationIsImageBasedAssessment"] = bool.FalseString,
                ["LocationLabel"] = "Blank address yard",
                ["LocationAddress"] = string.Empty
            }));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Equal(1, Shown(body, OperatorLabels.PrincipalAdministration.AddressRequired));
        var refusal = body.IndexOf(">" + OperatorLabels.PrincipalAdministration.AddressRequired + "<", StringComparison.Ordinal);
        Assert.True(
            refusal > body.IndexOf("id=\"principal-location-title\"", StringComparison.Ordinal)
                && refusal < body.IndexOf("name=\"LocationLabel\"", StringComparison.Ordinal),
            "The refusal must show at the top of the Default inspection location panel.");
        Assert.Equal("Blank address yard", InputValue(body, "LocationLabel"));
    }

    /// <summary>
    /// #1550 and #1551: Replace code with no new code shows the label-owned
    /// sentence once and never the framework's wording; nothing is replaced.
    /// </summary>
    [Fact]
    public async Task ABlankSuccessorCodeIsRefusedOnce()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Blank successor code provider", "BLANKSUC");
        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var settingsHtml = await GetContactSettingsAsync(client, settingsPath);

        using var post = await client.PostAsync(
            $"{settingsPath}?handler=Replace",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
                ["ReplacementOperationKey"] = InputValue(settingsHtml, "ReplacementOperationKey"),
                ["ReplacementExpectedVersion"] = InputValue(settingsHtml, "ReplacementExpectedVersion"),
                ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion"),
                ["SuccessorCode"] = string.Empty
            }));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Equal(1, Shown(body, OperatorLabels.PrincipalAdministration.NewCodeRequired));
        Assert.DoesNotContain("The SuccessorCode field is required.", body, StringComparison.Ordinal);
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND IsActive = 1;"));
        Assert.Equal(0, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM Principals WHERE PredecessorId = '{principalId:D}';"));
    }

    /// <summary>How often a sentence is shown as text; an attribute that carries it is not shown.</summary>
    private static int Shown(string html, string text) =>
        Regex.Count(html, ">" + Regex.Escape(text) + "<", RegexOptions.CultureInvariant);

    [Fact]
    public async Task YmlSettingsShowTheExactMailboxNotASharedDomain()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var principalId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'YML';");
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Id = '" + principalId + "';");
        using var response = await client.GetAsync($"/Administration/Contacts/Edit/{contactId:D}");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accepted e-mail identities", html, StringComparison.Ordinal);
        Assert.Contains("networkhduk@gmail.com", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Accepted e-mail domains", html, StringComparison.Ordinal);
    }

    private static Task<string> GetContactSettingsAsync(HttpClient client, string path) =>
        IntakeWebDriver.GetHtmlAsync(client, path);

    private static async Task<(Guid PrincipalId, Guid ContactId)> CreatePrincipalContactAsync(
        HttpClient client,
        IntakeWebApplicationFactory factory,
        string name,
        string code)
    {
        using var principalGet = await client.GetAsync("/Administration/Contacts?createType=Principal");
        var principalHtml = await principalGet.Content.ReadAsStringAsync();
        principalGet.EnsureSuccessStatusCode();
        var principalForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(principalHtml, "__RequestVerificationToken"),
            ["OperationKey"] = InputValue(principalHtml, "OperationKey"),
            ["ContactId"] = InputValue(principalHtml, "ContactId"),
            ["CreateType"] = "Principal",
            ["Name"] = name,
            ["PrincipalCode"] = code,
            ["PrincipalInspectionMode"] = "PhysicalAddress"
        };
        using var principalPost = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(principalForm));
        Assert.Equal(HttpStatusCode.Redirect, principalPost.StatusCode);
        var principalId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            $"SELECT Id FROM Principals WHERE Code = '{code}';");
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            $"SELECT OrganizationId FROM Principals WHERE Id = '{principalId:D}';");
        return (principalId, contactId);
    }

    private static string InputValue(string html, string name)
    {
        var match = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                WebUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The administration form must render input '{name}'.");
        return WebUtility.HtmlDecode(match!.Groups["value"].Value);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    // C06 review R-20: when a POST unexpectedly redisplays the page instead
    // of redirecting, name the cause instead of leaving only a status-code
    // mismatch behind.
    private static async Task<string> DescribeValidationErrorsAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var texts = ValidationSummaryRegex().Matches(html)
            .Cast<Match>()
            .Concat(FieldValidationErrorRegex().Matches(html).Cast<Match>())
            .Select(match => WebUtility.HtmlDecode(
                Regex.Replace(match.Groups["text"].Value, "<[^>]+>", string.Empty)).Trim())
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal);
        var joined = string.Join(" | ", texts);
        return joined.Length > 0 ? joined : "(none found in response body)";
    }

    [GeneratedRegex(
        "<div[^>]*class=\"[^\"]*status-card--error[^\"]*\"[^>]*>(?<text>[\\s\\S]*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValidationSummaryRegex();

    [GeneratedRegex(
        "<span[^>]*class=\"[^\"]*field-validation-error[^\"]*\"[^>]*>(?<text>[\\s\\S]*?)</span>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FieldValidationErrorRegex();
}
