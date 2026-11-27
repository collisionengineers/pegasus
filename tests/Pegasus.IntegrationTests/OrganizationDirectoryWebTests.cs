using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// EXT-18/S05 items 6-7, as the Contacts page holds them since 25 November
/// 2026 (v55; R1266, R1267): a Principal's default fee, its one Inspection
/// setting with the default location for a physical address, and its Send
/// from mailbox are the Contact tab's, saved by Save contact; Report
/// generation keeps the report's own settings.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class OrganizationDirectoryWebTests
{
    [Fact]
    public async Task TheContactTabSavesTheFeeInspectionAndSendFromAndReportGenerationTheImages()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Directory Web Caller Provider", "DIRW");

        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var editor = await GetContactSettingsAsync(client, settingsPath);
        Assert.Contains(">Report generation</a>", editor, StringComparison.Ordinal);
        Assert.DoesNotContain(">Default inspection location</a>", editor, StringComparison.Ordinal);
        // One form in three headed sections (v55 item B), Cases opening with the fee (item C).
        var organisation = editor.IndexOf(">Organisation</h3>", StringComparison.Ordinal);
        var cases = editor.IndexOf(">Cases</h3>", StringComparison.Ordinal);
        var mail = editor.IndexOf(">E-mail</h3>", StringComparison.Ordinal);
        Assert.True(organisation > 0 && cases > organisation && mail > cases, "The Contact tab's sections are Organisation, Cases and E-mail.");
        var fee = editor.IndexOf("name=\"DefaultFee\"", StringComparison.Ordinal);
        Assert.True(fee > cases && fee < mail, "Default fee is in Cases.");
        // A new Principal starts at the standard default fee.
        Assert.Equal("180.00", InputValue(editor, "DefaultFee"));
        Assert.Equal(nameof(Pegasus.Core.Cases.CaseInspectionMode.PhysicalAddress), SelectedValue(editor, "PrincipalInspectionMode"));
        Assert.Matches("""<fieldset class="field-wide contact-subsection" data-location-physical>\s*<legend>Default inspection location</legend>""", editor);
        Assert.DoesNotContain("LocationIsImageBasedAssessment", editor, StringComparison.Ordinal);
        var sendFrom = editor.IndexOf("<select id=\"principal-send-from\" name=\"SendFromMailbox\">", StringComparison.Ordinal);
        Assert.True(sendFrom > mail, "Send from is in E-mail.");

        using var saved = await client.PostAsync(
            $"{settingsPath}?handler=Save",
            new FormUrlEncodedContent(ContactForm(editor, new Dictionary<string, string>
            {
                ["DefaultFee"] = "225.50",
                ["PrincipalInspectionMode"] = "PhysicalAddress",
                ["LocationLabel"] = "Directory Web Caller Yard",
                ["LocationAddress"] = "1 Directory Way, DW1 2EF",
                ["LocationPostcode"] = "DW1 2EF",
                ["SendFromMailbox"] = "desk@collisionengineers.example"
            })));
        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(saved)}");
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND DefaultFee = 225.50 AND DefaultInspectionAddress = '1 Directory Way, DW1 2EF' AND DefaultInspectionLocationLabel = 'Directory Web Caller Yard' AND DefaultInspectionPostcode = 'DW1 2EF' AND SendFromMailbox = 'desk@collisionengineers.example';"));

        var savedEditor = await GetContactSettingsAsync(client, settingsPath);
        Assert.Equal("225.50", InputValue(savedEditor, "DefaultFee"));
        Assert.Equal("Directory Web Caller Yard", InputValue(savedEditor, "LocationLabel"));
        Assert.Equal("desk@collisionengineers.example", SelectedValue(savedEditor, "SendFromMailbox"));

        // Image Based Assessment has no default location: the save clears it (D4).
        using var imageBased = await client.PostAsync(
            $"{settingsPath}?handler=Save",
            new FormUrlEncodedContent(ContactForm(savedEditor, new Dictionary<string, string>
            {
                ["DefaultFee"] = "225.50",
                ["PrincipalInspectionMode"] = "ImageBasedAssessment",
                ["LocationLabel"] = "Directory Web Caller Yard",
                ["LocationAddress"] = "1 Directory Way, DW1 2EF",
                ["LocationPostcode"] = "DW1 2EF",
                ["SendFromMailbox"] = "desk@collisionengineers.example"
            })));
        Assert.Equal(HttpStatusCode.Redirect, imageBased.StatusCode);
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND InspectionMode = '{Pegasus.Core.Cases.PrincipalInspectionModePolicy.ImageBasedAssessmentCode}' AND DefaultInspectionAddress IS NULL AND DefaultInspectionLocationLabel IS NULL AND DefaultFee = 225.50;"));

        // Report generation has no Default fee; it carries the report's vehicle images tick (v55 item F).
        var reportSettingsHtml = await GetContactSettingsAsync(client, $"{settingsPath}?tab=report-generation");
        Assert.DoesNotContain("name=\"DefaultFee\"", reportSettingsHtml, StringComparison.Ordinal);
        Assert.Contains(
            $"<input type=\"checkbox\" name=\"ReportImages\" value=\"true\" checked=\"checked\" /> <span>{OperatorLabels.PrincipalAdministration.ReportImages}</span>",
            reportSettingsHtml,
            StringComparison.Ordinal);
        using var reportSettingsPost = await client.PostAsync(
            $"{settingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(reportSettingsHtml, "__RequestVerificationToken"),
                ["ReportSettingsOperationKey"] = InputValue(reportSettingsHtml, "ReportSettingsOperationKey"),
                ["PrincipalExpectedVersion"] = InputValue(reportSettingsHtml, "PrincipalExpectedVersion"),
                ["ExpectedVersion"] = InputValue(reportSettingsHtml, "ExpectedVersion")
            }));
        Assert.True(
            reportSettingsPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {reportSettingsPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(reportSettingsPost)}");
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Id = '{principalId:D}' AND ReportImages = 0 AND DefaultFee = 225.50;"));
        Assert.DoesNotMatch(
            "name=\"ReportImages\"[^>]*checked",
            await GetContactSettingsAsync(client, $"{settingsPath}?tab=report-generation"));
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
        var imagesBefore = $"SELECT CASE WHEN ReportImages = 1 THEN 1 ELSE 0 END FROM Principals WHERE Id = '{principalId:D}';";
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(imagesBefore));

        await (await factory.DatabaseAsync).ExecuteAsync(
            $"UPDATE Organizations SET Version = Version + 1 WHERE Id = '{contactId:D}';");
        // The tick is cleared: an unticked box posts nothing.
        var reportSettingsForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(settingsHtml, "__RequestVerificationToken"),
            ["ReportSettingsOperationKey"] = InputValue(settingsHtml, "ReportSettingsOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(settingsHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(settingsHtml, "ExpectedVersion")
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
                && refusal < refusedHtml.IndexOf("name=\"FeeNoteVatNumber\"", StringComparison.Ordinal),
            "The refusal must show at the top of the Report generation panel.");
        // What was typed stays: the tick is still clear.
        Assert.DoesNotMatch("name=\"ReportImages\"[^>]*checked", refusedHtml);
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(imagesBefore));
    }

    /// <summary>
    /// C06 review R-4: item 6's expected output — the handoff's "QDOS
    /// defaults to the Image Based Assessment location" — was otherwise
    /// unasserted anywhere. Image Based Assessment is represented as every
    /// <c>Default*</c> column being null, which is indistinguishable from
    /// "never set" by reading the schema alone; this asserts what the page
    /// actually renders for the seeded row: its Inspection mode is Image
    /// Based Assessment.
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
        var html = await GetContactSettingsAsync(client, $"/Administration/Contacts/Edit/{contactId:D}");
        Assert.Equal(nameof(Pegasus.Core.Cases.CaseInspectionMode.ImageBasedAssessment), SelectedValue(html, "PrincipalInspectionMode"));
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
    /// one "Notes on every Case" text, edited on its contact. Type and Principal code
    /// are greyed read-only boxes there (operator, 13 October 2026); Inspection mode is
    /// a choice since 25 November 2026 (v55 item D).
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
            """<div class="field field-wide" data-notes-on-every-case><label for="NotesOnEveryCase">Notes on every Case</label><textarea[^>]*name="NotesOnEveryCase"[^>]*>""",
            editor);
        // A Principal contact never links to a principal.
        Assert.DoesNotContain("name=\"LinkedPrincipalIds\"", editor, StringComparison.Ordinal);
        Assert.Matches("""<input id="contact-type" value="Principal" readonly""", editor);
        Assert.Matches("""<input(?=[^>]*name="PrincipalCode")(?=[^>]*value="QDOS")(?=[^>]*readonly)[^>]*>""", editor);
        Assert.Contains("<select id=\"principal-inspection-mode\" name=\"PrincipalInspectionMode\" data-inspection-mode>", editor, StringComparison.Ordinal);
        Assert.DoesNotContain("Guidance for new Cases", editor, StringComparison.Ordinal);

        // R118: the form shows the Principal's current mode, so QDOS stays Image Based Assessment.
        var inspectionMode = SelectedValue(editor, "PrincipalInspectionMode");
        Assert.Equal(nameof(Pegasus.Core.Cases.CaseInspectionMode.ImageBasedAssessment), inspectionMode);
        // Save posts only the contact form: the Replace dialog's fields are absent.
        using var saved = await client.PostAsync(
            $"{path}?handler=Save",
            new FormUrlEncodedContent(ContactForm(editor, new Dictionary<string, string>
            {
                ["PrincipalInspectionMode"] = inspectionMode,
                ["NotesOnEveryCase"] = "Ring the claimant before inspection."
            })));

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
    /// generation saves its own settings and leaves the notes as they are,
    /// even when a hand-built post carries a notes field.
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
                ["FeeNoteVatNumber"] = "GB 199 0000 00",
                ["NotesOnEveryCase"] = "Notes from the report settings form."
            }));

        Assert.True(
            post.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {post.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(post)}");
        Assert.Equal("GB 199 0000 00", await (await factory.DatabaseAsync).ScalarAsync<string>(
            $"SELECT FeeNoteVatNumber FROM Principals WHERE Id = '{principalId:D}';"));
        Assert.Equal(
            "Ring the claimant before inspection.",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT NotesOnEveryCase FROM Organizations WHERE Id = '{contactId:D}';"));
    }

    /// <summary>
    /// #1550: a default location with a label and no address is refused once,
    /// beside the Address field of the Contact tab, and what was typed stays.
    /// </summary>
    [Fact]
    public async Task ABlankDefaultLocationAddressIsRefusedOnceAtItsField()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Blank location address provider", "BLANKLOC");
        var settingsPath = $"/Administration/Contacts/Edit/{contactId:D}";
        var editor = await GetContactSettingsAsync(client, settingsPath);

        using var post = await client.PostAsync(
            $"{settingsPath}?handler=Save",
            new FormUrlEncodedContent(ContactForm(editor, new Dictionary<string, string>
            {
                ["DefaultFee"] = "195.00",
                ["PrincipalInspectionMode"] = "PhysicalAddress",
                ["LocationLabel"] = "Blank address yard",
                ["LocationAddress"] = string.Empty
            })));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Equal(1, Shown(body, OperatorLabels.PrincipalAdministration.AddressRequired));
        var refusal = body.IndexOf(">" + OperatorLabels.PrincipalAdministration.AddressRequired + "<", StringComparison.Ordinal);
        Assert.True(
            refusal > body.IndexOf("name=\"LocationAddress\"", StringComparison.Ordinal)
                && refusal < body.IndexOf("name=\"LocationPostcode\"", StringComparison.Ordinal),
            "The refusal must show at the Address field of the default inspection location.");
        Assert.Equal("Blank address yard", InputValue(body, "LocationLabel"));
        Assert.Equal("195.00", InputValue(body, "DefaultFee"));
        Assert.Equal(180.00m, await (await factory.DatabaseAsync).ScalarAsync<decimal>(
            $"SELECT DefaultFee FROM Principals WHERE Id = '{principalId:D}';"));
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

    /// <summary>
    /// #1547 (R539): Replace code with a code the principal code rule refuses
    /// names the rule, not the general notice; nothing is replaced.
    /// </summary>
    [Fact]
    public async Task ASuccessorCodeOutsideTheCodeRuleIsRefusedWithTheRule()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Hyphenated successor provider", "HYPHSUC");
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
                ["SuccessorCode"] = "ACME-2"
            }));

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var body = await post.Content.ReadAsStringAsync();
        Assert.Equal(1, Shown(body, "The new principal code must use letters and digits only."));
        Assert.DoesNotContain(OperatorLabels.PrincipalAdministration.ReplacementNotAccepted, body, StringComparison.Ordinal);
        Assert.Equal(0, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM Principals WHERE PredecessorId = '{principalId:D}';"));
    }

    /// <summary>
    /// #1551 (R539): the contact's Save refuses an empty and a 301-character
    /// Organisation name with the label-owned sentences, each once.
    /// </summary>
    [Fact]
    public async Task ContactSaveRefusesAnEmptyOrOverLongNameWithItsOwnWords()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, contactId) = await CreatePrincipalContactAsync(
            client, factory, "Save name words provider", "SAVENAME");
        var path = $"/Administration/Contacts/Edit/{contactId:D}";

        async Task<string> SaveAsync(string name)
        {
            var editor = await GetContactSettingsAsync(client, path);
            using var saved = await client.PostAsync(
                $"{path}?handler=Save",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = InputValue(editor, "__RequestVerificationToken"),
                    ["ContactId"] = InputValue(editor, "ContactId"),
                    ["ExpectedVersion"] = InputValue(editor, "ExpectedVersion"),
                    ["OperationKey"] = InputValue(editor, "OperationKey"),
                    ["Name"] = name,
                    ["PrincipalCode"] = InputValue(editor, "PrincipalCode"),
                    ["PrincipalInspectionMode"] = SelectedValue(editor, "PrincipalInspectionMode"),
                    ["Active"] = bool.TrueString
                }));
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            return await saved.Content.ReadAsStringAsync();
        }

        var empty = await SaveAsync(string.Empty);
        Assert.Equal(1, Shown(empty, OperatorLabels.Contacts.NameRequired));
        Assert.DoesNotContain("The Name field is required.", empty, StringComparison.Ordinal);

        var overLong = await SaveAsync(new string('n', Pegasus.Core.Cases.ContactDirectoryPolicy.MaximumNameLength + 1));
        Assert.Equal(1, Shown(overLong, "The organisation name must be 300 characters or fewer."));
        Assert.DoesNotContain("must be a string with a maximum length", overLong, StringComparison.Ordinal);

        Assert.Equal("Save name words provider", await (await factory.DatabaseAsync).ScalarAsync<string>(
            $"SELECT Name FROM Organizations WHERE Id = '{contactId:D}';"));
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

    // The contact form's own fields as the page drew them, with the changes made.
    private static Dictionary<string, string> ContactForm(string editor, Dictionary<string, string> changes)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(editor, "__RequestVerificationToken"),
            ["ContactId"] = InputValue(editor, "ContactId"),
            ["ExpectedVersion"] = InputValue(editor, "ExpectedVersion"),
            ["OperationKey"] = InputValue(editor, "OperationKey"),
            ["Name"] = InputValue(editor, "Name"),
            ["PrincipalCode"] = InputValue(editor, "PrincipalCode"),
            ["Active"] = bool.TrueString
        };
        foreach (var (name, value) in changes)
        {
            form[name] = value;
        }
        return form;
    }

    // The chosen option of a select the page draws.
    private static string SelectedValue(string html, string name)
    {
        var select = Regex.Match(html, $"<select\\b[^>]*\\bname=\"{Regex.Escape(name)}\"[^>]*>(?<options>.*?)</select>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(select.Success, $"The administration form must render select '{name}'.");
        var chosen = Regex.Match(select.Groups["options"].Value, "<option value=\"(?<value>[^\"]*)\" selected=\"selected\">", RegexOptions.CultureInvariant);
        return chosen.Success ? WebUtility.HtmlDecode(chosen.Groups["value"].Value) : string.Empty;
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
