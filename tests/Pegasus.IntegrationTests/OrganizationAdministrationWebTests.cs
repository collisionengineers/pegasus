using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Cases;
using Pegasus.Core.Reports;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class OrganizationAdministrationWebTests
{
    [Fact]
    public async Task AdministratorRoutesAreDiscoverableAndPostThroughCoreEfCallers()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });

        using var landingResponse = await client.GetAsync("/Administration");
        var landingHtml = await landingResponse.Content.ReadAsStringAsync();
        landingResponse.EnsureSuccessStatusCode();
        Assert.DoesNotContain("/Administration/Organizations", landingHtml, StringComparison.Ordinal);
        Assert.Contains("/Administration/Contacts", landingHtml, StringComparison.Ordinal);

        var contactsHtml = await IntakeWebDriver.GetHtmlAsync(
            client,
            "/Administration/Contacts?createType=ClaimSource");
        Assert.Contains("New Claim Source", contactsHtml, StringComparison.Ordinal);
        Assert.Contains("Claim Source", contactsHtml, StringComparison.Ordinal);
        var contactForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                contactsHtml,
                "__RequestVerificationToken"),
            ["CreateType"] = "ClaimSource",
            ["OperationKey"] = InputValue(contactsHtml, "OperationKey"),
            ["ContactId"] = InputValue(contactsHtml, "ContactId"),
            ["Name"] = "Web Caller Claim Source"
        };
        using var contactPost = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(contactForm));
        Assert.True(contactPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {contactPost.StatusCode}. Validation errors: {await DescribeValidationErrorsAsync(contactPost)}");
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT Id FROM Organizations WHERE Name = 'Web Caller Claim Source';");
        Assert.Equal(1, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM Organizations WHERE Id = '{contactId:D}' AND Role = 'claim_source';"));
        var contactEditHtml = await IntakeWebDriver.GetHtmlAsync(
            client,
            $"/Administration/Contacts/Edit/{contactId:D}");
        Assert.Contains("Web Caller Claim Source</h2>", contactEditHtml, StringComparison.Ordinal);

        var principalHtml = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Contacts?createType=Principal");
        Assert.Contains("Name", principalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("OrganizationId", principalHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Select an organization", principalHtml, StringComparison.Ordinal);
        var principalForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                principalHtml,
                "__RequestVerificationToken"),
            ["CreateType"] = "Principal",
            ["OperationKey"] = InputValue(principalHtml, "OperationKey"),
            ["ContactId"] = InputValue(principalHtml, "ContactId"),
            ["Name"] = "pegasustest",
            ["PrincipalCode"] = "WEBP",
            ["PrincipalInspectionMode"] = "PhysicalAddress"
        };
        using var principalPost = await client.PostAsync(
            "/Administration/Contacts?handler=Create",
            new FormUrlEncodedContent(principalForm));
        Assert.Equal(HttpStatusCode.Redirect, principalPost.StatusCode);
        var principalId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'WEBP';");
        var principalContactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Id = '" + principalId + "';");

        var principalIndexHtml = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Contacts");
        Assert.Contains($"/Administration/Contacts/Edit/{principalContactId:D}", principalIndexHtml, StringComparison.Ordinal);
        Assert.Contains("pegasustest", principalIndexHtml, StringComparison.Ordinal);
        Assert.Contains("Open", principalIndexHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Provider", principalIndexHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Organization", principalIndexHtml, StringComparison.Ordinal);

        var reportSettingsPath =
            $"/Administration/Contacts/Edit/{principalContactId:D}";
        // An active Principal's page is tabs; the Contact tab is the default.
        var contactTabHtml = await EditContactAsync(client, reportSettingsPath);
        Assert.Contains("pegasustest</h2>", contactTabHtml, StringComparison.Ordinal);
        Assert.Matches(
            $"<a class=\"tab\"(?=[^>]*href=\"{Regex.Escape(reportSettingsPath)}\")(?=[^>]*aria-current=\"page\")[^>]*>Contact</a>",
            contactTabHtml);
        Assert.Matches(
            $"<a class=\"tab\"(?=[^>]*href=\"{Regex.Escape(reportSettingsPath + "?tab=report-generation")}\")[^>]*>Report generation</a>",
            contactTabHtml);
        Assert.DoesNotContain("principal-report-title", contactTabHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Principal settings", contactTabHtml, StringComparison.Ordinal);
        var reportSettingsHtml = await EditContactAsync(client, reportSettingsPath, "report-generation");
        Assert.Contains(">Report generation</h2>", reportSettingsHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("contact-title", reportSettingsHtml, StringComparison.Ordinal);
        // A new Principal starts with the default report sending rules.
        Assert.Equal(
            EfOrganizationAdministration.ToReportSendingJson(Pegasus.Core.Reports.PrincipalReportSendingRules.Default),
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT ReportSendingRulesJson FROM Principals WHERE Id = '{principalId:D}';"));
        var reportSettingsForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                reportSettingsHtml,
                "__RequestVerificationToken"),
            ["ReportSettingsOperationKey"] = InputValue(reportSettingsHtml, "ReportSettingsOperationKey"),
            ["PrincipalExpectedVersion"] = InputValue(reportSettingsHtml, "PrincipalExpectedVersion"),
            ["ExpectedVersion"] = InputValue(reportSettingsHtml, "ExpectedVersion"),
            ["FeeNoteVatNumber"] = "GB 123 4567 89",
            ["AuditWorkflow"] = nameof(AuditWorkflowKind.GarageAuthorisationAndInvoice),
            ["PrincipalInvoice"] = nameof(PrincipalInvoiceKind.UlaInvoice),
            ["InvoiceVatNumber"] = "GB 987 6543 21",
            ["InvoicePaymentTerms"] = "30 days",
            ["AuditReportNameFirst"] = "{REG}_{CASE REF}_AUDIT"
        };
        // The Audit workflow and the Principal's own invoice are chosen here,
        // with the two values the invoice prints (R1138, R1147, R1148, R1162);
        // a new Principal starts on the original engineer report and no invoice.
        Assert.Contains(
            $">{OperatorLabels.PrincipalAdministration.AuditWorkflow}</label><select id=\"principal-audit-workflow\" name=\"AuditWorkflow\">",
            reportSettingsHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<option value=\"OriginalEngineerReport\" selected=\"selected\">Original engineer report</option>",
            reportSettingsHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<option value=\"GarageAuthorisationAndInvoice\">Garage authorisation &amp; invoice</option>",
            reportSettingsHtml,
            StringComparison.Ordinal);
        Assert.Contains(
            ">Principal&#x27;s own invoice</label><select id=\"principal-own-invoice\" name=\"PrincipalInvoice\">",
            reportSettingsHtml,
            StringComparison.Ordinal);
        Assert.Contains("<option value=\"None\" selected=\"selected\">None</option>", reportSettingsHtml, StringComparison.Ordinal);
        Assert.Contains("<option value=\"UlaInvoice\">ULA Invoice</option>", reportSettingsHtml, StringComparison.Ordinal);
        Assert.Contains(">Invoice VAT number</label>", reportSettingsHtml, StringComparison.Ordinal);
        Assert.Contains(">Invoice payment terms</label>", reportSettingsHtml, StringComparison.Ordinal);
        // The fee note VAT number box greys the standard number it stands for (R1008).
        Assert.Contains(">Fee note VAT number</label>", reportSettingsHtml, StringComparison.Ordinal);
        Assert.Matches(
            "<input id=\"principal-fee-note-vat-number\" name=\"FeeNoteVatNumber\"(?: value=\"\")? maxlength=\"20\" placeholder=\"262 0937 10\"",
            reportSettingsHtml);
        Assert.Contains("Empty: the standard number.", reportSettingsHtml, StringComparison.Ordinal);
        // Each report name greys the standard name it stands for and offers its tokens (R740).
        Assert.Contains("placeholder=\"{CASE REF} {REG} {OUTCOME} report\"", reportSettingsHtml, StringComparison.Ordinal);
        Assert.Contains("data-insert-placeholder=\"{CASE REF}\" data-insert-target=\"report-name-audit-first report-name-audit-resend\"", reportSettingsHtml, StringComparison.Ordinal);
        using var reportSettingsPost = await client.PostAsync(
            $"{reportSettingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(reportSettingsForm));
        Assert.True(
            reportSettingsPost.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {reportSettingsPost.StatusCode}. " +
                $"Validation errors: {await DescribeValidationErrorsAsync(reportSettingsPost)}");
        Assert.Equal($"{reportSettingsPath}?tab=report-generation", reportSettingsPost.Headers.Location?.OriginalString);
        // The vehicle images tick was left clear, so the reports carry none (v55 item F).
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT CASE WHEN ReportImages = 0 THEN 1 ELSE 0 END FROM Principals WHERE Id = '{principalId:D}';"));
        Assert.Equal(
            "{REG}_{CASE REF}_AUDIT",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT AuditReportNameFirst FROM Principals WHERE Id = '{principalId:D}';"));
        Assert.Contains(
            "value=\"{REG}_{CASE REF}_AUDIT\"",
            await EditContactAsync(client, reportSettingsPath, "report-generation"),
            StringComparison.Ordinal);
        Assert.Equal(
            "GB 123 4567 89",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT FeeNoteVatNumber FROM Principals WHERE Id = '{principalId:D}';"));
        Assert.Contains(
            "name=\"FeeNoteVatNumber\" value=\"GB 123 4567 89\"",
            await EditContactAsync(client, reportSettingsPath, "report-generation"),
            StringComparison.Ordinal);
        Assert.Equal(
            "garage_authorisation_and_invoice|ula_invoice|GB 987 6543 21|30 days",
            await (await factory.DatabaseAsync).ScalarAsync<string>(
                $"SELECT AuditWorkflow + '|' + PrincipalInvoice + '|' + InvoiceVatNumber + '|' + InvoicePaymentTerms FROM Principals WHERE Id = '{principalId:D}';"));
        var savedSettingsHtml = await EditContactAsync(client, reportSettingsPath, "report-generation");
        Assert.Contains(
            "<option value=\"GarageAuthorisationAndInvoice\" selected=\"selected\">",
            savedSettingsHtml,
            StringComparison.Ordinal);
        Assert.Contains("<option value=\"UlaInvoice\" selected=\"selected\">", savedSettingsHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"InvoiceVatNumber\" value=\"GB 987 6543 21\"", savedSettingsHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"InvoicePaymentTerms\" value=\"30 days\"", savedSettingsHtml, StringComparison.Ordinal);

        // A name with a token the report cannot fill is refused, naming the report, and kept as typed.
        var refusedHtml = await EditContactAsync(client, reportSettingsPath, "report-generation");
        using var refusedPost = await client.PostAsync(
            $"{reportSettingsPath}?handler=UpdateReportSettings",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = InputValue(refusedHtml, "__RequestVerificationToken"),
                ["ReportSettingsOperationKey"] = InputValue(refusedHtml, "ReportSettingsOperationKey"),
                ["PrincipalExpectedVersion"] = InputValue(refusedHtml, "PrincipalExpectedVersion"),
                ["ExpectedVersion"] = InputValue(refusedHtml, "ExpectedVersion"),
                ["InspectionReportNameFirst"] = "{VRM} Initial"
            }));
        Assert.Equal(HttpStatusCode.OK, refusedPost.StatusCode);
        var refusedBody = WebUtility.HtmlDecode(await refusedPost.Content.ReadAsStringAsync());
        Assert.Contains("Inspection report name: use only {REG}, {CASE REF} and {OUTCOME}.", refusedBody, StringComparison.Ordinal);
        Assert.Contains("value=\"{VRM} Initial\"", refusedBody, StringComparison.Ordinal);

        await AssertCredentialControlsAsync(factory, client, principalId, reportSettingsPath);

        var replacePath = reportSettingsPath;
        var replaceHtml = await EditContactAsync(client, replacePath);
        Assert.Contains("allocated cases remain with their existing references", replaceHtml, StringComparison.Ordinal);
        var replacementOperationKey = InputValue(replaceHtml, "ReplacementOperationKey");
        var replaceForm = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = InputValue(
                replaceHtml,
                "__RequestVerificationToken"),
            ["ReplacementOperationKey"] = replacementOperationKey,
            ["ReplacementExpectedVersion"] = InputValue(replaceHtml, "ReplacementExpectedVersion"),
            ["ExpectedVersion"] = InputValue(replaceHtml, "ExpectedVersion"),
            ["SuccessorCode"] = "WEBN"
        };
        using var replacePost = await client.PostAsync(
            $"{replacePath}?handler=Replace",
            new FormUrlEncodedContent(replaceForm));
        Assert.Equal(HttpStatusCode.Redirect, replacePost.StatusCode);

        Assert.Equal(
            0,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT CASE WHEN IsActive = 0 AND Code = 'WEBP' THEN 0 ELSE 1 END FROM Principals WHERE Id = '{principalId:D}';"));
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals WHERE Code = 'WEBN' AND PredecessorId = '{principalId:D}' AND IsActive = 1;"));
        Assert.Equal(
            1,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM Principals p JOIN Principals previous ON previous.Id = p.PredecessorId WHERE p.Code = 'WEBN' AND p.OrganizationId = previous.OrganizationId;"));
        Assert.Equal(
            2,
            await (await factory.DatabaseAsync).ScalarAsync<int>(
                $"SELECT COUNT(*) FROM ActionHistory WHERE CorrelationId = '{replacementOperationKey}' AND ActorSubjectId = '{DevelopmentOfflineIdentity.AdministratorId:D}' AND Reason IS NULL;"));
    }

    /// <summary>
    /// An active Principal's tabs (operator, 25 November 2026; v55 item A):
    /// Contact, Report generation, Sending rules and Principal API, then Photo
    /// links while capture is on. There is no Default inspection location tab;
    /// Sending rules keeps the Report sending tab's address.
    /// </summary>
    [Fact]
    public async Task AnActivePrincipalHasTheFiveTabsInOrder()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, path, _) = await SendingRulesPrincipalAsync(factory, "SRTABS");

        var html = await EditContactAsync(client, path);
        var labels = Regex.Matches(html, "<a class=\"tab\"[^>]*>(?<label>[^<]+)</a>")
            .Select(match => WebUtility.HtmlDecode(match.Groups["label"].Value))
            .ToArray();
        Assert.True(labels.Length >= 4, string.Join(", ", labels));
        Assert.Equal(["Contact", "Report generation", "Sending rules", "Principal API"], labels[..4]);
        Assert.True(labels.Length == 4 || labels[4..] is ["Photo links"], string.Join(", ", labels));
        Assert.Matches(
            $"<a class=\"tab\"(?=[^>]*href=\"{Regex.Escape(path + "?tab=report-sending")}\")[^>]*>Sending rules</a>",
            html);
        Assert.DoesNotContain("default-inspection-location", html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Report sending<", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Sending rules tab (v55 items G, H): On every Case as one list, one
    /// row per action kind, and the numbered rules each read as an If line and
    /// a Then line, a contact by its name, "is not" for a negated condition and
    /// the dashed bracket round a group of several among several groups. Move
    /// up is hidden on the first rule and Move down on the last. The tab has
    /// one primary, Add rule, and no Save.
    /// </summary>
    [Fact]
    public async Task TheSendingRulesTabReadsEveryCaseAndEachRuleAsASentence()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, path, claimSourceId) = await SendingRulesPrincipalAsync(factory, "SRREAD");
        await StoreRulesAsync(factory, principalId, new(
            [
                new(ReportSendingActionKind.ReplyAll),
                new(ReportSendingActionKind.Attach, File: ReportSendingFile.FeeNoteSeparate),
                new(ReportSendingActionKind.Attach, File: ReportSendingFile.Estimate),
                new(ReportSendingActionKind.Hold, Text: "Check the report first.")
            ],
            [
                new([[new(ReportSendingFact.Outcome, ["repairable"])]],
                    [new(ReportSendingActionKind.Task, Text: ReportDispatchPolicy.GarageFiguresTask)]),
                new(
                    [
                        [new(ReportSendingFact.ClaimSource, [claimSourceId.ToString("D")], Not: true), new(ReportSendingFact.CaseType, ["Audit"])],
                        [new(ReportSendingFact.PreviousJob, [])]
                    ],
                    [new(ReportSendingActionKind.NeverCc, Addresses: ["never@example.com"])])
            ]));

        var html = await EditContactAsync(client, path, "report-sending");

        Assert.Contains(">Sending rules</h2>", html, StringComparison.Ordinal);
        Assert.Matches(
            $"<a class=\"btn btn--small btn--primary\" href=\"{Regex.Escape(path)}\\?tab=report-sending&amp;rule=new\">",
            html);
        Assert.Contains(">On every Case</h3>", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{path}?tab=report-sending&amp;every=1\">Edit</a>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Save report sending", html, StringComparison.Ordinal);
        Assert.Equal(
            ["Reply all", "Attach Separate fee note Estimate", "Hold “Check the report first.”"],
            Pieces(html, "data-every-case>", "<li>", "</ul>").Select(Words).ToArray());

        var cards = Pieces(html, "data-sending-rules>", "<li class=\"rule-card\"", "</ol>");
        Assert.Equal(2, cards.Length);
        Assert.StartsWith(
            $"1 If Outcome is Repairable Then Add a Case task “{ReportDispatchPolicy.GarageFiguresTask}”",
            Words("<x" + cards[0]),
            StringComparison.Ordinal);
        Assert.StartsWith(
            "2 If Claim Source is not Linked Source SRREAD and Case type is Audit or Previous job Then Never cc never@example.com",
            Words("<x" + cards[1]),
            StringComparison.Ordinal);
        Assert.Contains("<span class=\"rule-bracket\">", cards[1], StringComparison.Ordinal);
        Assert.Matches("<button(?=[^>]*aria-label=\"Move rule 1 up\")(?=[^>]*\\bhidden=\"hidden\")[^>]*>", cards[0]);
        Assert.DoesNotMatch("<button(?=[^>]*aria-label=\"Move rule 1 down\")(?=[^>]*\\bhidden=\"hidden\")[^>]*>", cards[0]);
        Assert.Matches("<button(?=[^>]*aria-label=\"Move rule 2 down\")(?=[^>]*\\bhidden=\"hidden\")[^>]*>", cards[1]);
        Assert.Contains($"href=\"{path}?tab=report-sending&amp;rule=2\">Edit</a>", cards[1], StringComparison.Ordinal);
        // No dialog is open on the tab itself.
        Assert.DoesNotContain("data-dialog=\"sending-rule-dialog\"", html, StringComparison.Ordinal);
    }

    /// <summary>A new Principal starts with reply all and the separate fee note, and no rule.</summary>
    [Fact]
    public async Task ANewPrincipalHasNoRules()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (_, path, _) = await SendingRulesPrincipalAsync(factory, "SRNONE");

        var html = await EditContactAsync(client, path, "report-sending");

        Assert.Contains($">{OperatorLabels.PrincipalAdministration.NoRules}</p>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"rule-card\"", html, StringComparison.Ordinal);
        Assert.Equal(
            ["Reply all", "Attach Separate fee note"],
            Pieces(html, "data-every-case>", "<li>", "</ul>").Select(Words).ToArray());
    }

    /// <summary>
    /// Each Sending rules dialog opens by address on the tab, drawn by the
    /// server so it works without script, and closes back to the tab: New
    /// rule, Rule n with Remove rule, On every Case with its actions only, and
    /// Remove rule n with the rule it removes.
    /// </summary>
    [Fact]
    public async Task EachSendingRulesDialogOpensByAddress()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, path, claimSourceId) = await SendingRulesPrincipalAsync(factory, "SRDLG");
        await StoreRulesAsync(factory, principalId, PrincipalReportSendingRules.Default with
        {
            Rules =
            [
                new([[new(ReportSendingFact.ClaimSource, [claimSourceId.ToString("D")])]],
                    [new(ReportSendingActionKind.Stop, Text: "Check with the handler.")])
            ]
        });

        var added = await EditContactAsync(client, path, "report-sending&rule=new");
        Assert.Contains(
            "<div class=\"dialog-backdrop\" data-dialog=\"sending-rule-dialog\" data-dialog-open-on-load=\"true\" data-dialog-cancel-form=\"sending-dialog-close\">",
            added,
            StringComparison.Ordinal);
        Assert.Contains(">New rule</h2>", added, StringComparison.Ordinal);
        Assert.Contains("<legend>If</legend>", added, StringComparison.Ordinal);
        Assert.Contains("<legend>Then</legend>", added, StringComparison.Ordinal);
        // A new rule starts with one Claim Source condition and one Cc action.
        Assert.Equal("ClaimSource", SelectedValue(added, "Cond.0.0.Fact"));
        Assert.Equal("Cc", SelectedValue(added, "Action.0.Kind"));
        Assert.DoesNotContain(">Remove rule</a>", added, StringComparison.Ordinal);
        Assert.Contains(">Save rule</button>", added, StringComparison.Ordinal);
        // Close and Cancel ask for the tab again.
        Assert.Matches(
            $"<form(?=[^>]*id=\"sending-dialog-close\")(?=[^>]*method=\"get\")(?=[^>]*action=\"{Regex.Escape(path)}\")[^>]*><input type=\"hidden\" name=\"tab\" value=\"report-sending\" /></form>",
            added);
        // The contacts linked to this Principal are listed first (v55 item U).
        Assert.Matches(
            $"<optgroup label=\"Linked to Sending Rules SRDLG\">\\s*<option value=\"{claimSourceId:D}\">Linked Source SRDLG</option>",
            added);
        Assert.Contains("<datalist id=\"sending-task-texts\">", added, StringComparison.Ordinal);

        var existing = await EditContactAsync(client, path, "report-sending&rule=1");
        Assert.Contains(">Rule 1</h2>", existing, StringComparison.Ordinal);
        Assert.Equal("ClaimSource", SelectedValue(existing, "Cond.0.0.Fact"));
        Assert.Equal(claimSourceId.ToString("D"), SelectedValue(existing, "Cond.0.0.ClaimSource"));
        Assert.Equal("Stop", SelectedValue(existing, "Action.0.Kind"));
        Assert.Equal("Check with the handler.", InputValue(existing, "Action.0.Text"));
        Assert.Contains($"href=\"{path}?tab=report-sending&amp;remove=1\">Remove rule</a>", existing, StringComparison.Ordinal);

        var every = await EditContactAsync(client, path, "report-sending&every=1");
        Assert.Contains(">On every Case</h2>", every, StringComparison.Ordinal);
        Assert.DoesNotContain("data-rule-if", every, StringComparison.Ordinal);
        Assert.Equal("ReplyAll", SelectedValue(every, "Action.0.Kind"));
        Assert.Equal("Attach", SelectedValue(every, "Action.1.Kind"));
        Assert.Equal("FeeNoteSeparate", SelectedValue(every, "Action.1.File"));
        Assert.Contains(">Save</button>", every, StringComparison.Ordinal);

        var remove = await EditContactAsync(client, path, "report-sending&remove=1");
        Assert.Contains(">Remove rule 1</h2>", remove, StringComparison.Ordinal);
        var removeDialog = remove[remove.IndexOf("data-dialog=\"sending-remove-dialog\"", StringComparison.Ordinal)..];
        Assert.Contains("If Claim Source is Linked Source SRDLG Then Stop “Check with the handler.”", Words(removeDialog), StringComparison.Ordinal);
        Assert.Contains("class=\"btn btn--danger\" data-busy-label=", removeDialog, StringComparison.Ordinal);

        // A number with no rule opens nothing.
        Assert.DoesNotContain("data-dialog=\"sending-rule-dialog\"", await EditContactAsync(client, path, "report-sending&rule=9"), StringComparison.Ordinal);
    }

    /// <summary>
    /// Save rule saves the whole rules value with the new rule added at the
    /// end and comes back to the tab with the notice; a refusal draws the
    /// dialog again with what was typed and the sentence at its top, and
    /// saves nothing.
    /// </summary>
    [Fact]
    public async Task ANewRuleIsSavedAndARefusalKeepsTheTypedDraft()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, path, _) = await SendingRulesPrincipalAsync(factory, "SRSAVE");
        var principalVersion = $"SELECT Version FROM Principals WHERE Id = '{principalId:D}';";
        var dialog = await EditContactAsync(client, path, "report-sending&rule=new");
        var versionBefore = await (await factory.DatabaseAsync).ScalarAsync<long>(principalVersion);

        using var refused = await client.PostAsync(
            $"{path}?handler=SaveRule&tab=report-sending&rule=new",
            SendingForm(dialog,
            [
                new("Cond.0.0.Fact", "Outcome"), new("Cond.0.0.Not", "false"),
                new("Action.0.Kind", "Cc"), new("Action.0.Addresses", "keep@example.com")
            ]));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        var refusedHtml = await refused.Content.ReadAsStringAsync();
        var title = refusedHtml.IndexOf(">New rule</h2>", StringComparison.Ordinal);
        var notice = refusedHtml.IndexOf("<div class=\"notice notice--danger\" role=\"alert\"><span>Rule 1: a condition needs a value.</span></div>", StringComparison.Ordinal);
        Assert.True(title > 0 && notice > title, "The refusal shows at the top of the dialog.");
        Assert.True(notice < refusedHtml.IndexOf("name=\"Cond.0.0.Fact\"", StringComparison.Ordinal), "The refusal shows above the rule.");
        Assert.Equal("Outcome", SelectedValue(refusedHtml, "Cond.0.0.Fact"));
        Assert.Equal("keep@example.com", InputValue(refusedHtml, "Action.0.Addresses"));
        Assert.Equal(versionBefore, await (await factory.DatabaseAsync).ScalarAsync<long>(principalVersion));

        dialog = await EditContactAsync(client, path, "report-sending&rule=new");
        using var saved = await client.PostAsync(
            $"{path}?handler=SaveRule&tab=report-sending&rule=new",
            SendingForm(dialog,
            [
                new("Cond.0.0.Fact", "Outcome"), new("Cond.0.0.Not", "false"), new("Cond.0.0.Outcome", "repairable"),
                new("Cond.0.1.Fact", "RefPrefix"), new("Cond.0.1.Not", "true"), new("Cond.0.1.Text", "a, b"),
                new("Cond.1.0.Fact", "PreviousJob"), new("Cond.1.0.Not", "false"),
                new("Action.0.Kind", "Cc"), new("Action.0.Addresses", "x@example.com, y@example.com"),
                new("Action.1.Kind", "Task"), new("Action.1.Text", ReportDispatchPolicy.RateCardTask)
            ]));
        Assert.True(
            saved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {saved.StatusCode}. Validation errors: {await DescribeValidationErrorsAsync(saved)}");
        Assert.Equal($"{path}?tab=report-sending", saved.Headers.Location?.OriginalString);
        Assert.Equal(versionBefore + 1, await (await factory.DatabaseAsync).ScalarAsync<long>(principalVersion));
        var rules = await StoredRulesAsync(factory, principalId);
        Assert.Equal(PrincipalReportSendingRules.Default.EveryCase, rules.EveryCase);
        var rule = Assert.Single(rules.Rules);
        Assert.Equal(2, rule.If.Count);
        Assert.Equal(ReportSendingFact.Outcome, rule.If[0][0].Fact);
        Assert.False(rule.If[0][0].Not);
        Assert.Equal(["repairable"], rule.If[0][0].Values);
        Assert.Equal(ReportSendingFact.RefPrefix, rule.If[0][1].Fact);
        Assert.True(rule.If[0][1].Not);
        Assert.Equal(["a", "b"], rule.If[0][1].Values);
        Assert.Equal(ReportSendingFact.PreviousJob, Assert.Single(rule.If[1]).Fact);
        Assert.Equal(["x@example.com", "y@example.com"], rule.Then[0].Addresses!);
        Assert.Equal(ReportDispatchPolicy.RateCardTask, rule.Then[1].Text);

        // The notice shows at the top of the panel.
        var reloaded = WebUtility.HtmlDecode(await EditContactAsync(client, path, "report-sending"));
        var updated = reloaded.IndexOf(OperatorLabels.PrincipalAdministration.ReportSendingUpdated, StringComparison.Ordinal);
        Assert.True(
            updated > reloaded.IndexOf("id=\"principal-sending-title\"", StringComparison.Ordinal)
                && updated < reloaded.IndexOf(">On every Case</h3>", StringComparison.Ordinal),
            "The notice must show at the top of the Sending rules panel.");
    }

    /// <summary>
    /// Without script, And, Or, Add action and a remove post the draft and
    /// the dialog is drawn again with a row more or fewer; nothing is saved.
    /// </summary>
    [Fact]
    public async Task TheDialogsRowButtonsRedrawTheDraftWithoutSaving()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, path, _) = await SendingRulesPrincipalAsync(factory, "SRDRAFT");
        var principalVersion = $"SELECT Version FROM Principals WHERE Id = '{principalId:D}';";
        var dialog = await EditContactAsync(client, path, "report-sending&rule=new");
        var versionBefore = await (await factory.DatabaseAsync).ScalarAsync<long>(principalVersion);
        KeyValuePair<string, string>[] draft =
        [
            new("Cond.0.0.Fact", "Sender"), new("Cond.0.0.Not", "true"), new("Cond.0.0.Text", "typed@example.com"),
            new("Action.0.Kind", "Hold"), new("Action.0.Text", "Typed hold.")
        ];

        async Task<string> ChangeAsync(string change)
        {
            using var response = await client.PostAsync(
                $"{path}?handler=RuleDraft&tab=report-sending&rule=new",
                SendingForm(dialog, [.. draft, new("DraftChange", change)]));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync();
        }

        var joined = await ChangeAsync("and:0");
        Assert.Equal("Sender", SelectedValue(joined, "Cond.0.0.Fact"));
        Assert.Equal("true", SelectedValue(joined, "Cond.0.0.Not"));
        Assert.Equal("typed@example.com", InputValue(joined, "Cond.0.0.Text"));
        Assert.Equal("ClaimSource", SelectedValue(joined, "Cond.0.1.Fact"));
        Assert.DoesNotContain("name=\"Cond.1.0.Fact\"", joined, StringComparison.Ordinal);
        Assert.Contains(">New rule</h2>", joined, StringComparison.Ordinal);

        var grouped = await ChangeAsync("or");
        Assert.Equal("ClaimSource", SelectedValue(grouped, "Cond.1.0.Fact"));
        Assert.Contains("<div class=\"rule-or-divider\" data-rule-or>or</div>", grouped, StringComparison.Ordinal);

        var withAction = await ChangeAsync("action");
        Assert.Equal("Typed hold.", InputValue(withAction, "Action.0.Text"));
        Assert.Equal("Task", SelectedValue(withAction, "Action.1.Kind"));

        var removed = await ChangeAsync("remove-condition:0:0");
        Assert.DoesNotContain("name=\"Cond.0.0.Fact\"", removed, StringComparison.Ordinal);
        Assert.Contains("data-rule-if", removed, StringComparison.Ordinal);

        Assert.Equal(versionBefore, await (await factory.DatabaseAsync).ScalarAsync<long>(principalVersion));
        Assert.Empty((await StoredRulesAsync(factory, principalId)).Rules);
    }

    /// <summary>
    /// Move up and Move down save at once (v55 item R); Remove rule asks
    /// first and then removes the rule (item Q); the On every Case dialog
    /// saves its list. Each comes back to the tab.
    /// </summary>
    [Fact]
    public async Task RulesMoveAndAreRemovedAndTheEveryCaseListIsSaved()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var (principalId, path, _) = await SendingRulesPrincipalAsync(factory, "SRMOVE");
        ReportSendingRule first = new([[new(ReportSendingFact.Outcome, ["repairable"])]], [new(ReportSendingActionKind.Task, Text: "First.")]);
        ReportSendingRule second = new([[new(ReportSendingFact.PreviousJob, [])]], [new(ReportSendingActionKind.Task, Text: "Second.")]);
        await StoreRulesAsync(factory, principalId, PrincipalReportSendingRules.Default with { Rules = [first, second] });

        var tab = await EditContactAsync(client, path, "report-sending");
        using var moved = await client.PostAsync(
            $"{path}?handler=MoveRule&tab=report-sending",
            SendingForm(tab, [new("RuleNumber", "1"), new("MoveBy", "1")]));
        Assert.True(
            moved.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {moved.StatusCode}. Validation errors: {await DescribeValidationErrorsAsync(moved)}");
        Assert.Equal($"{path}?tab=report-sending", moved.Headers.Location?.OriginalString);
        Assert.Equal(["Second.", "First."], (await StoredRulesAsync(factory, principalId)).Rules.Select(rule => rule.Then[0].Text!).ToArray());

        var remove = await EditContactAsync(client, path, "report-sending&remove=1");
        using var removed = await client.PostAsync(
            $"{path}?handler=RemoveRule&tab=report-sending&remove=1",
            SendingForm(remove, []));
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.Equal(["First."], (await StoredRulesAsync(factory, principalId)).Rules.Select(rule => rule.Then[0].Text!).ToArray());

        var every = await EditContactAsync(client, path, "report-sending&every=1");
        using var savedEvery = await client.PostAsync(
            $"{path}?handler=SaveEveryCase&tab=report-sending&every=1",
            SendingForm(every,
            [
                new("Action.0.Kind", "ReplyAll"),
                new("Action.1.Kind", "Hold"), new("Action.1.Text", "Check first."),
                new("Action.2.Kind", "Attach"), new("Action.2.File", "Estimate")
            ]));
        Assert.True(
            savedEvery.StatusCode == HttpStatusCode.Redirect,
            $"Expected a redirect but got {savedEvery.StatusCode}. Validation errors: {await DescribeValidationErrorsAsync(savedEvery)}");
        var stored = await StoredRulesAsync(factory, principalId);
        Assert.Equal(
            [
                new ReportSendingAction(ReportSendingActionKind.ReplyAll),
                new ReportSendingAction(ReportSendingActionKind.Hold, Text: "Check first."),
                new ReportSendingAction(ReportSendingActionKind.Attach, File: ReportSendingFile.Estimate)
            ],
            stored.EveryCase);
        Assert.Single(stored.Rules);

        // An every-Case refusal is named "On every Case".
        var refusedEvery = await EditContactAsync(client, path, "report-sending&every=1");
        using var refused = await client.PostAsync(
            $"{path}?handler=SaveEveryCase&tab=report-sending&every=1",
            SendingForm(refusedEvery, [new("Action.0.Kind", "Cc"), new("Action.0.Addresses", "not an address")]));
        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        Assert.Contains("On every Case: enter valid e-mail addresses.", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrincipalRoutesDenyNonAdministratorSession()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: false);
        _ = factory.Services;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<PegasusIdentityUser>>();
            var user = await userManager.FindByIdAsync(
                DevelopmentOfflineIdentity.AdministratorId.ToString("D"));
            Assert.NotNull(user);
            Assert.True((await userManager.RemoveFromRoleAsync(
                user,
                StaffRoleNames.Administrator)).Succeeded);
        }
        using var client = IntakeWebDriver.CreateClient(factory);

        var id = Guid.Parse("1eeea2b1-3e1d-4a0a-8205-0c25396206e8");
        string[] routes =
        [
            "/Administration/Contacts",
            "/Administration/Contacts/Edit?role=Principal",
            $"/Administration/Contacts/Edit/{id:D}"
        ];
        foreach (var route in routes)
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static async Task AssertCredentialControlsAsync(
        IntakeWebApplicationFactory factory, HttpClient client, Guid principalId, string settingsPath)
    {
        var settings = await EditContactAsync(client, settingsPath, "principal-api");
        var issueForm = CredentialForm(settings);
        using var issued = await client.PostAsync(
            $"{settingsPath}?handler=IssueCredential", new FormUrlEncodedContent(issueForm));
        issued.EnsureSuccessStatusCode();
        Assert.True(issued.Headers.CacheControl?.NoStore);
        var issuedHtml = await issued.Content.ReadAsStringAsync();
        var secretMatch = Regex.Match(issuedHtml, "id=\"issued-api-key\" value=\"([^\"]+)\"");
        Assert.True(secretMatch.Success);
        var secret = WebUtility.HtmlDecode(secretMatch.Groups[1].Value);
        var keyId = PrincipalCredentialPolicy.KeyIdOf(secret)!;
        Assert.False(string.IsNullOrWhiteSpace(secret));

        using var pauseFromIssuePage = await client.PostAsync(
            $"{settingsPath}?handler=PauseCredential",
            new FormUrlEncodedContent(CredentialForm(issuedHtml)));
        Assert.Equal(HttpStatusCode.Redirect, pauseFromIssuePage.StatusCode);

        using var replay = await client.PostAsync(
            $"{settingsPath}?handler=IssueCredential", new FormUrlEncodedContent(issueForm));
        replay.EnsureSuccessStatusCode();
        Assert.DoesNotContain(secret, await replay.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        settings = await EditContactAsync(client, settingsPath, "principal-api");
        Assert.DoesNotContain(secret, settings, StringComparison.Ordinal);
        Assert.DoesNotContain("issued-api-key", settings, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var authenticate = scope.ServiceProvider.GetRequiredService<IAuthenticatePrincipalCredential>();
        Assert.NotNull(await authenticate.ExecuteAsync(keyId, secret, default));

        foreach (var handler in new[] { "ResumeCredential", "PauseCredential", "ResumeCredential" })
        {
            using var response = await client.PostAsync(
                $"{settingsPath}?handler={handler}", new FormUrlEncodedContent(CredentialForm(settings)));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var authentication = await authenticate.ExecuteAsync(keyId, secret, default);
            Assert.NotNull(authentication);
            Assert.Equal(handler == "ResumeCredential", authentication.MaySubmit);
            settings = await EditContactAsync(client, settingsPath, "principal-api");
        }

        var stale = CredentialForm(settings);
        stale["CredentialVersion"] = "0";
        using var staleResponse = await client.PostAsync(
            $"{settingsPath}?handler=RevokeCredential", new FormUrlEncodedContent(stale));
        Assert.Equal(HttpStatusCode.OK, staleResponse.StatusCode);
        Assert.Contains("The API key changed.", await staleResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.NotNull(await authenticate.ExecuteAsync(keyId, secret, default));

        using var reset = await client.PostAsync(
            $"{settingsPath}?handler=IssueCredential", new FormUrlEncodedContent(CredentialForm(settings)));
        reset.EnsureSuccessStatusCode();
        var resetHtml = await reset.Content.ReadAsStringAsync();
        var resetMatch = Regex.Match(resetHtml, "id=\"issued-api-key\" value=\"([^\"]+)\"");
        Assert.True(resetMatch.Success);
        var resetSecret = WebUtility.HtmlDecode(resetMatch.Groups[1].Value);
        var resetKeyId = PrincipalCredentialPolicy.KeyIdOf(resetSecret)!;
        Assert.Null(await authenticate.ExecuteAsync(keyId, secret, default));
        Assert.NotNull(await authenticate.ExecuteAsync(resetKeyId, resetSecret, default));

        settings = await EditContactAsync(client, settingsPath, "principal-api");
        using var revoked = await client.PostAsync(
            $"{settingsPath}?handler=RevokeCredential", new FormUrlEncodedContent(CredentialForm(settings)));
        Assert.Equal(HttpStatusCode.Redirect, revoked.StatusCode);
        Assert.Null(await authenticate.ExecuteAsync(resetKeyId, resetSecret, default));
        Assert.Equal(7, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateId = '{principalId:D}' AND EventKind LIKE 'principal_credential_%';"));
    }

    [Theory]
    [InlineData("IssueCredential", false)]
    [InlineData("IssueCredential", true)]
    [InlineData("PauseCredential", true)]
    [InlineData("ResumeCredential", true)]
    [InlineData("RevokeCredential", true)]
    public async Task StaleContactCredentialActionsShowConflictAndPreserveCredential(
        string handler, bool credentialExists)
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var principalId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT Id FROM Principals WHERE Code = 'QDOS';");
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            $"SELECT OrganizationId FROM Principals WHERE Id = '{principalId:D}';");
        var path = $"/Administration/Contacts/Edit/{contactId:D}";
        var settings = await EditContactAsync(client, path, "principal-api");
        if (credentialExists)
        {
            using var issued = await client.PostAsync(
                $"{path}?handler=IssueCredential", new FormUrlEncodedContent(CredentialForm(settings)));
            Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
            settings = await EditContactAsync(client, path, "principal-api");
        }
        if (handler == "ResumeCredential")
        {
            using var paused = await client.PostAsync(
                $"{path}?handler=PauseCredential", new FormUrlEncodedContent(CredentialForm(settings)));
            Assert.Equal(HttpStatusCode.Redirect, paused.StatusCode);
            settings = await EditContactAsync(client, path, "principal-api");
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var getCredential = scope.ServiceProvider.GetRequiredService<IGetPrincipalCredential>();
        var actor = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
        var before = await getCredential.ExecuteAsync(actor, principalId, default);
        var historyBefore = await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateId = '{principalId:D}' AND EventKind LIKE 'principal_credential_%';");
        await (await factory.DatabaseAsync).ExecuteAsync(
            $"UPDATE Organizations SET Version = Version + 1 WHERE Id = '{contactId:D}';");

        using var refused = await client.PostAsync(
            $"{path}?handler={handler}", new FormUrlEncodedContent(CredentialForm(settings)));

        Assert.Equal(HttpStatusCode.OK, refused.StatusCode);
        var refusedHtml = await refused.Content.ReadAsStringAsync();
        Assert.Contains("This contact changed. Reload it before making further changes.", refusedHtml, StringComparison.Ordinal);
        Assert.Equal(InputValue(settings, "ExpectedVersion"), InputValue(refusedHtml, "ExpectedVersion"));
        Assert.DoesNotContain("id=\"issued-api-key\"", refusedHtml, StringComparison.Ordinal);
        Assert.Equal(before, await getCredential.ExecuteAsync(actor, principalId, default));
        Assert.Equal(historyBefore, await (await factory.DatabaseAsync).ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateId = '{principalId:D}' AND EventKind LIKE 'principal_credential_%';"));

        var reloaded = await EditContactAsync(client, path, "principal-api");
        using var retried = await client.PostAsync(
            $"{path}?handler={handler}", new FormUrlEncodedContent(CredentialForm(reloaded)));
        Assert.Equal(handler == "IssueCredential" ? HttpStatusCode.OK : HttpStatusCode.Redirect, retried.StatusCode);
        Assert.Equal((before?.Version ?? 0) + 1,
            (await getCredential.ExecuteAsync(actor, principalId, default))!.Version);
    }

    /// <summary>
    /// Only the Principal API tab draws the API key's state, so only its load
    /// reads the credential: the API tab sends one read of the credentials
    /// table and every other tab sends none.
    /// </summary>
    [Fact]
    public async Task OnlyThePrincipalApiTabReadsThePrincipalCredential()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var contactId = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Code = 'QDOS';");
        var path = $"/Administration/Contacts/Edit/{contactId:D}";
        var settings = await EditContactAsync(client, path, "principal-api");
        using var issued = await client.PostAsync(
            $"{path}?handler=IssueCredential", new FormUrlEncodedContent(CredentialForm(settings)));
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);

        counter.Reset();
        var apiTab = await EditContactAsync(client, path, "principal-api");
        Assert.Equal(1, counter.CountMentioning("PrincipalApiCredentials"));
        Assert.Equal("1", InputValue(apiTab, "CredentialVersion"));

        foreach (var tab in new string?[] { null, "report-generation", "report-sending" })
        {
            counter.Reset();
            _ = await EditContactAsync(client, path, tab);
            Assert.True(
                counter.CountMentioning("PrincipalApiCredentials") == 0,
                $"The {tab ?? "contact"} tab read the credential." + Environment.NewLine + counter.Describe());
        }
    }

    /// <summary>
    /// Each Contacts edit tab reads only the choices it draws (#1542): the
    /// Claim Source and Repairer lists only for an active Principal's Sending
    /// rules tab, the mailboxes Send from offers for a Principal's Contact tab
    /// (v55 item E), and the Principal choices only for a contact that is not
    /// a Principal. The lists are recognised by their query parameters,
    /// <c>roleCode</c> and <c>contactId</c>, and the mailboxes by their table.
    /// </summary>
    [Fact]
    public async Task EachContactTabReadsOnlyTheChoicesItDraws()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var principalContact = await (await factory.DatabaseAsync).ScalarAsync<Guid>(
            "SELECT OrganizationId FROM Principals WHERE Code = 'QDOS';");
        var repairer = Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            context.Organizations.Add(new OrganizationEntity
            {
                Id = repairer,
                Name = "Tab Reads Repairer",
                Role = "repairer",
                Active = true,
                Version = 0
            });
            await context.SaveChangesAsync();
        }

        var path = $"/Administration/Contacts/Edit/{principalContact:D}";
        counter.Reset();
        _ = await EditContactAsync(client, path, "report-sending");
        Assert.True(counter.CountMentioning("roleCode") > 0, "The Sending rules tab read no contact list." + Environment.NewLine + counter.Describe());
        Assert.True(counter.CountMentioning("contactId") == 0, "A Principal read the Principal choices." + Environment.NewLine + counter.Describe());

        // The Contact tab draws Send from, so it reads the mailboxes: no more
        // than once beyond what a tab that draws none reads.
        counter.Reset();
        _ = await EditContactAsync(client, path, "report-generation");
        var withoutSendFrom = counter.CountMentioning("ApprovedMailboxes");
        counter.Reset();
        var contactTab = await EditContactAsync(client, path);
        Assert.Contains("name=\"SendFromMailbox\"", contactTab, StringComparison.Ordinal);
        Assert.True(
            counter.CountMentioning("ApprovedMailboxes") <= withoutSendFrom + 1,
            "The Contact tab read the mailboxes more than once." + Environment.NewLine + counter.Describe());

        foreach (var tab in new string?[] { null, "report-generation", "principal-api" })
        {
            counter.Reset();
            _ = await EditContactAsync(client, path, tab);
            Assert.True(
                counter.CountMentioning("roleCode") + counter.CountMentioning("contactId") == 0,
                $"The {tab ?? "contact"} tab read a choice list it does not draw." + Environment.NewLine + counter.Describe());
        }

        counter.Reset();
        var repairerPage = await EditContactAsync(client, $"/Administration/Contacts/Edit/{repairer:D}");
        Assert.Contains("name=\"LinkedPrincipalIds\"", repairerPage, StringComparison.Ordinal);
        Assert.True(counter.CountMentioning("contactId") > 0, "The Repairer read no Principal choices." + Environment.NewLine + counter.Describe());
        Assert.True(counter.CountMentioning("roleCode") == 0, "The Repairer read a Sending rules list." + Environment.NewLine + counter.Describe());
    }

    private static Dictionary<string, string> CredentialForm(string html) => new()
    {
        ["__RequestVerificationToken"] = InputValue(html, "__RequestVerificationToken"),
        ["ExpectedVersion"] = InputValue(html, "ExpectedVersion"),
        ["CredentialOperationKey"] = InputValue(html, "CredentialOperationKey"),
        ["CredentialVersion"] = InputValue(html, "CredentialVersion")
    };

    private static FormUrlEncodedContent SendingForm(
        string html,
        IEnumerable<KeyValuePair<string, string>> fields) =>
        new(new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", InputValue(html, "__RequestVerificationToken")),
            new("ReportSendingOperationKey", InputValue(html, "ReportSendingOperationKey")),
            new("PrincipalExpectedVersion", InputValue(html, "PrincipalExpectedVersion")),
            new("ExpectedVersion", InputValue(html, "ExpectedVersion"))
        }.Concat(fields));

    private static readonly ActionActor Administrator = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    /// <summary>
    /// A new active Principal with the standard rules, and a Claim Source
    /// linked to it, so a rule can name a contact the picker lists first.
    /// </summary>
    private static async Task<(Guid PrincipalId, string Path, Guid ClaimSourceId)> SendingRulesPrincipalAsync(
        IntakeWebApplicationFactory factory, string code)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var administration = scope.ServiceProvider.GetRequiredService<IContactDirectoryAdministration>();
        var principal = await administration.SaveAsync(new(
            Administrator, Guid.NewGuid(), 0, $"Sending Rules {code}", null, null, null, null, true,
            ContactRole.Principal, code, CaseInspectionMode.PhysicalAddress, [], $"sending-rules:{code}"), default);
        var principalId = principal.OwnPrincipalIds!.Single();
        var claimSource = await administration.SaveAsync(new(
            Administrator, Guid.NewGuid(), 0, $"Linked Source {code}", null, null, null, null, true,
            ContactRole.ClaimSource, null, CaseInspectionMode.PhysicalAddress, [principalId], $"sending-rules:{code}:source"), default);
        return (principalId, $"/Administration/Contacts/Edit/{principal.OrganizationId:D}", claimSource.OrganizationId);
    }

    /// <summary>Stores <paramref name="rules"/> as the Principal's, as a migration or an earlier save left them.</summary>
    private static async Task StoreRulesAsync(IntakeWebApplicationFactory factory, Guid principalId, PrincipalReportSendingRules rules) =>
        await (await factory.DatabaseAsync).ExecuteAsync(
            $"UPDATE Principals SET ReportSendingRulesJson = N'{EfOrganizationAdministration.ToReportSendingJson(rules).Replace("'", "''", StringComparison.Ordinal)}' WHERE Id = '{principalId:D}';");

    private static async Task<PrincipalReportSendingRules> StoredRulesAsync(IntakeWebApplicationFactory factory, Guid principalId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var principal = await scope.ServiceProvider.GetRequiredService<IGetPrincipal>()
            .ExecuteAsync(Administrator, principalId, default);
        return principal!.Principal.ReportSending;
    }

    // The chosen option of a select the page draws.
    private static string SelectedValue(string html, string name)
    {
        var select = Regex.Match(html, $"<select\\b[^>]*\\bname=\"{Regex.Escape(name)}\"[^>]*>(?<options>.*?)</select>", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(select.Success, $"The form must render select '{name}'.");
        var chosen = Regex.Match(select.Groups["options"].Value, "<option value=\"(?<value>[^\"]*)\" selected=\"selected\">", RegexOptions.CultureInvariant);
        return chosen.Success ? WebUtility.HtmlDecode(chosen.Groups["value"].Value) : string.Empty;
    }

    // What a piece of the page reads, its tags dropped and its spaces folded.
    private static string Words(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), "\\s+", " ").Trim();

    // The pieces between the anchor and the end that open with the marker.
    private static string[] Pieces(string html, string anchor, string marker, string end)
    {
        var from = html.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(from >= 0, $"The page must draw '{anchor}'.");
        var to = html.IndexOf(end, from, StringComparison.Ordinal);
        return html[from..(to < 0 ? html.Length : to)].Split(marker)[1..];
    }

    private static Task<string> EditContactAsync(HttpClient client, string path, string? tab = null) =>
        IntakeWebDriver.GetHtmlAsync(client, tab is null ? path : $"{path}?tab={tab}");

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
