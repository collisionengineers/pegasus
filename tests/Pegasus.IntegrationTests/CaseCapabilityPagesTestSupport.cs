using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The harness the capability-page tests share: a workspace client that has already entered edit
/// mode, the recording store substituted for every port the page under test calls, and the two
/// refusal paths every page inherits from <c>CaseMutationPageModel</c>.
/// </summary>
internal static partial class CaseWebTestSupport
{
    internal const string CloseUpFileName = "front-nearside.jpg";
    internal const string OverviewFileName = "vehicle-overview.jpg";
    internal const string FirstSupportingFileName = "rear-offside.jpg";
    internal const string SecondSupportingFileName = "interior.jpg";
    internal const string UnusedFileName = "plate.jpg";

    internal static async Task<LeasedWorkspace> EnterEditModeAsync(
        RecordingCaseDetailsStore store,
        Action<IServiceCollection> substitutePorts)
    {
        var baseFactory = new IntakeWebApplicationFactory();
        var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IValidateCaseRenderLease>(services, store);
                Substitute<IAcquireCaseEditLease>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                substitutePorts(services);
            }));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claim = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey"))));
        AssertPrg(claim, store.CaseId);
        var leased = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Equal(store.LeaseToken, InputValue(leased, "editLeaseToken"));
        return new(baseFactory, factory, client, store, AntiforgeryValue(leased));
    }

    internal static void Substitute<T>(IServiceCollection services, T instance)
        where T : class
    {
        services.RemoveAll<T>();
        services.AddSingleton(instance);
    }

    /// <summary>
    /// A command the case refuses for a reason other than a lost lease or a stale version reports
    /// the refusal and keeps this browser in edit mode, so the editor can correct and resubmit.
    /// </summary>
    internal static async Task AssertRefusalKeepsEditModeAsync(
        LeasedWorkspace workspace,
        string route,
        HttpContent form)
    {
        workspace.Store.NextFailure = new InvalidOperationException("The case refused the command.");
        using var refused = await workspace.PostAsync(route, form);
        AssertPrg(refused, workspace.Store.CaseId);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.Equal(workspace.Store.LeaseToken, InputValue(html, "editLeaseToken"));
    }

    /// <summary>
    /// A lost lease or a stale version makes the editor reacquire rather than resubmit: the page
    /// forgets this browser's edit mode and reports the refusal.
    /// </summary>
    internal static async Task AssertLostLeaseClearsEditModeAsync(
        LeasedWorkspace workspace,
        string route,
        HttpContent form)
    {
        workspace.Store.NextFailure =
            new CaseEditLeaseExpiredException(workspace.Store.CaseId, workspace.Store.CaseVersion);
        using var refused = await workspace.PostAsync(route, form);
        AssertPrg(refused, workspace.Store.CaseId);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"editLeaseToken\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// The port received the leased workspace's envelope: the claimant, the case and its version,
    /// the lease token, and the operation key and reason the form carried.
    /// </summary>
    internal static void AssertLeasedMutation(
        LeasedWorkspace workspace,
        CaseMutationRequest request,
        string operationKey,
        string reason,
        long? expectedVersion = null)
    {
        AssertClaimant(workspace, request.Actor);
        Assert.Equal(workspace.Store.CaseId, request.CaseId);
        Assert.Equal(expectedVersion ?? workspace.Store.CaseVersion, request.ExpectedVersion);
        Assert.Equal(workspace.Store.LeaseToken, request.EditLeaseToken);
        Assert.Equal(operationKey, request.OperationKey);
        Assert.Equal(reason, request.Reason);
    }

    /// <summary>The actor a port recorded is the staff member who entered edit mode.</summary>
    internal static void AssertClaimant(LeasedWorkspace workspace, ActionActor recordedActor)
    {
        var claimant = workspace.Claimant;
        Assert.Equal(claimant.Kind, recordedActor.Kind);
        Assert.Equal(claimant.SubjectId, recordedActor.SubjectId);
        Assert.Equal(claimant.Roles.OrderBy(role => role), recordedActor.Roles.OrderBy(role => role));
    }

    internal sealed class LeasedWorkspace(
        IntakeWebApplicationFactory baseFactory,
        WebApplicationFactory<Program> factory,
        HttpClient client,
        RecordingCaseDetailsStore store,
        string antiforgeryToken) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public RecordingCaseDetailsStore Store { get; } = store;

        public string AntiforgeryToken { get; } = antiforgeryToken;

        /// <summary>
        /// The staff member who entered edit mode. v25 decision F: a post made
        /// inside the session re-acquires the lease the store's mutation
        /// cleared, so the store may hold more than one claim by the time a
        /// test reads this; the claimant is the first.
        /// </summary>
        public ActionActor Claimant => Store.Claims[0].Actor;

        public Task<HttpResponseMessage> PostAsync(string route, HttpContent content) =>
            Client.PostAsync($"/Cases/{Store.CaseId:D}/{route}", content);

        public Task<string> GetWorkspaceAsync() => GetHtmlAsync(Client, $"/Cases/{Store.CaseId:D}");

        public FormUrlEncodedContent MutationForm(
            string operationKey,
            string reason,
            params (string Name, string Value)[] fields) =>
            LifecycleForm(AntiforgeryToken, Store, operationKey, reason, fields);

        public void Dispose()
        {
            Client.Dispose();
            factory.Dispose();
            baseFactory.Dispose();
        }
    }

    internal sealed partial class RecordingCaseDetailsStore
    {
        /// <summary>Armed by a test so the next capability-page command is refused once.</summary>
        public Exception? NextFailure { get; set; }

        private void ThrowNextFailure()
        {
            if (NextFailure is { } failure)
            {
                NextFailure = null;
                throw failure;
            }
        }
    }

    /// <summary>
    /// The Case as an operator who holds no edit lease reads it, with
    /// <paramref name="substitutePorts"/> replacing further ports after the
    /// store's. Without a lease a lazy section (Vehicle among them) is a
    /// placeholder unless <paramref name="section"/> addresses it.
    /// </summary>
    internal static async Task<string> ReadCaseAsync(
        RecordingCaseDetailsStore store,
        Action<IServiceCollection>? substitutePorts = null,
        string? section = null)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IGetAssessmentWorkspace>(services, store);
                substitutePorts?.Invoke(services);
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        return await GetHtmlAsync(
            client,
            section is null ? $"/Cases/{store.CaseId:D}" : $"/Cases/{store.CaseId:D}?section={section}");
    }

    /// <summary>
    /// The Case data of a Principal whose inspection setting is image-based:
    /// the address and the mode both reach the Case from that setting, and
    /// <paramref name="recordedAddress"/> is what staff recorded instead.
    /// </summary>

    internal static string StickyActionRow(string html) => RecordBar(html);

    /// <summary>The record's Refresh control, with the section it reruns.</summary>

    internal static FormUrlEncodedContent LifecycleForm(
        string antiforgeryToken,
        RecordingCaseDetailsStore store,
        string operationKey,
        string reason,
        params (string Name, string Value)[] fields) => Form(
            antiforgeryToken,
            [
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", operationKey),
                ("editLeaseToken", store.LeaseToken),
                ("reason", reason),
                .. fields
            ]);


    internal static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }


    internal static FormUrlEncodedContent Form(
        string antiforgeryToken,
        params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    internal static void AssertEditorCommit(
        string html,
        string editor,
        string operationKey,
        long expectedVersion)
    {
        var attribute = Regex.Match(html, "data-editor-commit=\"(?<value>[^\"]+)\"");
        Assert.True(attribute.Success);
        using var json = System.Text.Json.JsonDocument.Parse(
            WebUtility.HtmlDecode(attribute.Groups["value"].Value));
        var commit = json.RootElement;
        Assert.Equal(editor, commit.GetProperty("editor").GetString());
        Assert.Equal(operationKey, commit.GetProperty("operationKey").GetString());
        Assert.Equal(expectedVersion, commit.GetProperty("expectedVersion").GetInt64());
        Assert.Equal(expectedVersion + 1, commit.GetProperty("version").GetInt64());
    }

    /// <summary>
    /// The valuation redirect lands on the Valuation section, not the record's
    /// top, so the editor reads the outcome where they acted.
    /// </summary>
    internal static void AssertValuationPrg(HttpResponseMessage response, Guid caseId)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            $"/Cases/{caseId:D}?section=valuation",
            response.Headers.Location?.OriginalString);
    }

    internal static string OverviewPanel(string html)
    {
        var host = html.IndexOf("id=\"section-overview\"", StringComparison.Ordinal);
        Assert.True(host >= 0, "The Case overview panel must render.");
        var start = html.LastIndexOf("<section", host, StringComparison.Ordinal);
        Assert.True(start >= 0, "The Case overview panel must be a section.");

        var depth = 0;
        var index = start;
        while (true)
        {
            var open = html.IndexOf("<section", index, StringComparison.Ordinal);
            var close = html.IndexOf("</section>", index, StringComparison.Ordinal);
            Assert.True(close >= 0, "The Case overview panel must close.");

            if (open >= 0 && open < close)
            {
                depth++;
                index = open + "<section".Length;
                continue;
            }

            if (--depth == 0)
            {
                return html[start..(close + "</section>".Length)];
            }

            index = close + "</section>".Length;
        }
    }

    /// <summary>
    /// The Files body mounts after the page's first response. Match the
    /// browser request: send the rendered lease token only as fragment
    /// rendering data, so the server can render the existing edit controls.
    /// </summary>
    internal static async Task<string> GetFilesFragmentAsync(
        LeasedWorkspace workspace,
        string renderedWorkspace)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{workspace.Store.CaseId:D}/Section?section=files");
        request.Headers.Add(
            "X-Pegasus-Edit-Lease",
            InputValue(renderedWorkspace, "editLeaseToken"));

        using var response = await workspace.Client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// One current case file: a current, unremoved version, custody-confirmed
    /// unless the test names another custody state.
    /// </summary>
    internal static CaseDocument Document(
        Guid occurrenceId,
        Guid versionId,
        string fileName,
        string mediaType,
        DocumentSemanticRole role = DocumentSemanticRole.Instruction,
        IReadOnlyList<ImageTagAssignment>? tags = null,
        DocumentCustodyStatus custody = DocumentCustodyStatus.Confirmed)
    {
        var documentId = Guid.NewGuid();
        var recordedAtUtc = new DateTimeOffset(2031, 5, 5, 9, 0, 0, TimeSpan.Zero);
        return new(
            documentId,
            Guid.Empty,
            [
                new(
                    occurrenceId,
                    Guid.Empty,
                    documentId,
                    versionId,
                    role,
                    DocumentSource.Intake,
                    "source-1",
                    recordedAtUtc,
                    tags ?? [])
            ],
            [
                new(
                    versionId,
                    documentId,
                    1,
                    fileName,
                    mediaType,
                    24_576,
                    new string('c', 64),
                    custody,
                    recordedAtUtc,
                    "staff",
                    IsCurrent: true,
                    IsLogicallyRemoved: false,
                    RemovalReason: null)
            ]);
    }

    /// <summary>
    /// One case's image occurrences and the preparation each carries: a
    /// Close-up turned a quarter turn, a cropped Overview, two ordered
    /// Supporting images and one the report does not use.
    /// </summary>
    internal sealed class PreparedImages
    {
        public Guid CloseUpOccurrenceId { get; } = Guid.NewGuid();

        public Guid OverviewOccurrenceId { get; } = Guid.NewGuid();

        public Guid FirstSupportingOccurrenceId { get; } = Guid.NewGuid();

        public Guid SecondSupportingOccurrenceId { get; } = Guid.NewGuid();

        public Guid UnusedOccurrenceId { get; } = Guid.NewGuid();

        public CaseAssetCrop OverviewCrop { get; } = new(0.1m, 0.1m, 0.8m, 0.8m);

        public RecordingCaseDetailsStore Store(
            CaseLifecycleState state = CaseLifecycleState.NotReady)
        {
            var store = new RecordingCaseDetailsStore
            {
                State = state,
                CaseState = state,
                CaseDocuments =
                [
                    Document(CloseUpOccurrenceId, VersionOf(CloseUpOccurrenceId), CloseUpFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(OverviewOccurrenceId, VersionOf(OverviewOccurrenceId), OverviewFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(FirstSupportingOccurrenceId, VersionOf(FirstSupportingOccurrenceId), FirstSupportingFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(SecondSupportingOccurrenceId, VersionOf(SecondSupportingOccurrenceId), SecondSupportingFileName, "image/jpeg", DocumentSemanticRole.Image),
                    Document(UnusedOccurrenceId, VersionOf(UnusedOccurrenceId), UnusedFileName, "image/jpeg", DocumentSemanticRole.Image)
                ]
            };
            store.Preparations =
            [
                Preparation(store.CaseId, CloseUpOccurrenceId, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full, 2),
                Preparation(store.CaseId, OverviewOccurrenceId, CaseAssetReportRole.Overview, null, CaseAssetRotation.None, OverviewCrop, 4),
                Preparation(store.CaseId, FirstSupportingOccurrenceId, CaseAssetReportRole.Supporting, 1, CaseAssetRotation.None, CaseAssetCrop.Full, 1),
                Preparation(store.CaseId, SecondSupportingOccurrenceId, CaseAssetReportRole.Supporting, 2, CaseAssetRotation.None, CaseAssetCrop.Full, 1),
                Preparation(store.CaseId, UnusedOccurrenceId, CaseAssetReportRole.NotUsed, null, CaseAssetRotation.None, CaseAssetCrop.Full, 0)
            ];
            return store;
        }

        /// <summary>
        /// The pinned version of an occurrence. It is derived from the
        /// occurrence identity so the document fixture and the preparation
        /// name the same version without a second table to keep in step.
        /// </summary>
        private static Guid VersionOf(Guid occurrenceId)
        {
            var bytes = occurrenceId.ToByteArray();
            bytes[0] ^= 0xFF;
            return new(bytes);
        }

        private static CaseAssetPreparation Preparation(
            Guid caseId,
            Guid occurrenceId,
            CaseAssetReportRole role,
            int? order,
            CaseAssetRotation rotation,
            CaseAssetCrop crop,
            long preparationVersion) =>
            new(
                caseId,
                occurrenceId,
                Guid.NewGuid(),
                VersionOf(occurrenceId),
                1,
                new string('a', 64),
                "image/jpeg",
                role,
                order,
                rotation,
                crop,
                preparationVersion,
                preparationVersion == 0 ? null : "staff",
                preparationVersion == 0 ? null : new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    /// A form post that may repeat a field name, which a checkbox and its hidden false companion
    /// always do. <see cref="Form"/> cannot express that because it keys by name.
    /// </summary>

    internal static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The case action must render '{name}'.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The case field '{name}' must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }


    internal static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The case action must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The case antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    /// <summary>
    /// The original Case-contact regression dropped claimant contact/address.
    /// Keep its actual caller assertion when moving the single Save to the
    /// workspace command, whose submitted sections replace all their members.
    /// </summary>

    internal const string DetailsModelOperationKey = "3f2504e04f8911d39a0c0305e82c3301";

    /// <summary>
    /// v26: the record's actions live in the ribbon's own actions cluster —
    /// the primary (Edit Case / Cancel + Save) and the one Actions menu —
    /// which the section row closes. Nothing below the sticky block is an
    /// action of the record itself.
    /// </summary>

    internal static string RecordBar(string html)
    {
        var start = html.IndexOf("data-case-ribbon-actions", StringComparison.Ordinal);
        Assert.True(start >= 0, "The record bar is not rendered.");
        var end = html.IndexOf("class=\"section-row\"", start, StringComparison.Ordinal);
        Assert.True(end > start, "The record bar is not closed before the section row.");
        return html[start..end];
    }

    /// <summary>
    /// The ten Case sections in the order D30 fixes, from the frame's one
    /// section list.
    /// </summary>

    internal static string[] CaseSectionKeys =>
        [.. Pegasus.Web.Presentation.OperatorLabels.CaseWorkspace.Sections
            .Select(section => section.Key)
            .Where(key => key != "original-report")];

    /// <summary>The section links an Inspection Case shows: no Original report, and Damage and Valuation read inside Vehicle (v28 P26).</summary>
    internal static string[] CaseSectionLinkKeys =>
        [.. CaseSectionKeys.Where(key => key is not ("damage" or "valuation"))];

    /// <summary>The hosts the first response leaves for the frame to fetch.</summary>

    internal static string[] DeferredSections(string html) =>
        [.. DeferredSectionRegex().Matches(html).Select(match => match.Groups[1].Value)];

    /// <summary>The record's section hosts, in the order they render.</summary>

    internal static string[] HostOrder(string html) =>
        [.. SectionHostRegex().Matches(html).Select(match => match.Groups[1].Value)];

    internal static int Occurrences(string html, string value) =>
        html.Split(value, StringSplitOptions.None).Length - 1;

    /// <summary>
    /// Razor compiles a stray <c>}</c> or closing tag as markup and the browser repairs it
    /// silently, moving whatever follows out of its column (issue 816). Every container opened
    /// is closed, and no brace stands alone as text.
    /// </summary>
    internal static void AssertBalancedMarkup(string html)
    {
        var markup = UnparsedContentRegex().Replace(html, string.Empty);
        foreach (var tag in new[] { "div", "section", "details", "form", "aside", "nav", "ul", "table" })
        {
            Assert.True(
                Regex.Count(markup, $"<{tag}[\\s>]") == Occurrences(markup, $"</{tag}>"),
                $"Every <{tag}> is not closed exactly once.");
        }
        Assert.DoesNotMatch(StrayBraceRegex(), markup);
    }

    /// <summary>The record's main column, from its opening tag to the aside that follows it.</summary>
    internal static string MainColumn(string html)
    {
        var start = html.IndexOf("<div class=\"workspace-main\" id=\"case-main\">", StringComparison.Ordinal);
        var end = html.IndexOf("<aside class=\"workspace-aside\"", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "The record's main column and aside are not rendered.");
        return html[start..end];
    }

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex UnparsedContentRegex();

    [GeneratedRegex(">\\s*[{}]\\s*<", RegexOptions.CultureInvariant)]
    private static partial Regex StrayBraceRegex();

    [GeneratedRegex(
        "<section class=\"record-section[^\"]*\" id=\"section-([a-z-]+)\"",
        RegexOptions.CultureInvariant)]
    internal static partial Regex SectionHostRegex();

    [GeneratedRegex(
        "data-section-link=\"([a-z-]+)\"",
        RegexOptions.CultureInvariant)]
    internal static partial Regex JumpLinkRegex();

    [GeneratedRegex(
        "data-lazy=\"([a-z-]+)\"",
        RegexOptions.CultureInvariant)]
    internal static partial Regex DeferredSectionRegex();

    [GeneratedRegex(
        "data-section-link=\"([a-z-]+)\"\\s+aria-current=\"true\"",
        RegexOptions.CultureInvariant)]
    internal static partial Regex CurrentSectionRegex();

    internal static string Section(string html, string labelledBy)
    {
        var start = html.IndexOf($"aria-labelledby=\"{labelledBy}\"", StringComparison.Ordinal);
        Assert.True(start >= 0, $"The '{labelledBy}' section is not rendered.");
        var open = html.LastIndexOf("<section", start, StringComparison.Ordinal);
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > open, $"The '{labelledBy}' section is not closed.");
        return html[open..(end + "</section>".Length)];
    }

    /// <summary>
    /// Operator copy only: markup, attribute values, and script are removed so the banned-vocabulary
    /// assertion reads what a member of staff reads.
    /// </summary>

    internal static string VisibleText(string html) =>
        MarkupRegex().Replace(html, " ");

    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>|<[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]

    internal static partial Regex MarkupRegex();

    [GeneratedRegex("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant)]
    internal static partial Regex GuidRegex();

    internal static void AssertPrg(HttpResponseMessage response, Guid caseId, string? expectedQuery = null)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        var path = location.Split('?', '#')[0];
        Assert.Equal($"/Cases/{caseId:D}", path);
        var query = location.Contains('?', StringComparison.Ordinal) ? location.Split('?')[1].Split('#')[0] : string.Empty;
        Assert.True(
            query.Length == 0 || Regex.IsMatch(query, "^section=[a-z]+$"),
            $"The redirect carries more than a section: {location}");
        if (expectedQuery is not null)
        {
            Assert.Equal(expectedQuery.TrimStart('?'), query);
        }
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]

    internal static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]

    internal static partial Regex ValueRegex();


    internal static void SubstituteDetailsPageReaders(
        IServiceCollection services,
        RecordingCaseDetailsStore store)
    {
        Substitute<IGetCasePageFrame>(services, store);
        Substitute<IGetCaseVehicleSection>(services, store);
        Substitute<IGetCaseValuationSection>(services, store);
        Substitute<IGetCaseNotesSection>(services, store);
        Substitute<IGetCaseFilesSection>(services, store);
        Substitute<IGetAssessmentWorkspace>(services, store);
    }

    /// <summary>
    /// The refusal test visits a distinct record between its failed save and
    /// return. Both focused reads dispatch by Case ID, so its visit cannot be
    /// satisfied by the first record's page frame or assessment workspace.
    /// </summary>

    internal sealed class StubStaffAccounts(Guid staffId, string userName, StaffRole role = StaffRole.User)
        : IStaffAccountQueries, IStaffHeldCaseEditLeaseQueries
    {
        private readonly StaffAccountSummary account =
            new(staffId, userName, true, false, role);

        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StaffAccountQuerySlice([account], false));

        public Task<StaffAccountSummary?> GetAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StaffAccountSummary?>(requestedStaffId == staffId ? account : null);

        public Task<IReadOnlyList<StaffAccountSummary>> GetManyAsync(
            IReadOnlyCollection<Guid> staffIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffAccountSummary>>(
                staffIds.Contains(staffId) ? [account] : []);

        public Task<IReadOnlyList<StaffHeldCaseEditLease>> ListHeldCaseEditLeasesAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffHeldCaseEditLease>>([]);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SignOffEngineerProfile>>([]);

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid requestedStaffId,
            CancellationToken cancellationToken) =>
            Task.FromResult<SignOffEngineerProfile?>(null);
    }

    internal sealed class StubEvaSubmissionStores(EvaSubmissionModes modes) :
        IEvaSubmissionQueries,
        IEvaSubmissionModeStore
    {
        Task<EvaSubmissionRecord?> IEvaSubmissionQueries.GetLatestAsync(
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult<EvaSubmissionRecord?>(null);

        Task<IReadOnlyList<EvaSubmissionFailure>> IEvaSubmissionQueries.GetRecentFailuresAsync(
            DateTimeOffset sinceUtc,
            int maximumResults,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EvaSubmissionFailure>>([]);

        Task<EvaSubmissionActivity> IEvaSubmissionQueries.GetActivityAsync(
            CancellationToken cancellationToken) => Task.FromResult(new EvaSubmissionActivity(null));

        Task<EvaSubmissionModes> IEvaSubmissionModeStore.GetForPrincipalAsync(
            string principalCode,
            CancellationToken cancellationToken) => Task.FromResult(modes);
    }

    /// <summary>
    /// In-memory stand-in so the page sees a composed transport and applies
    /// the principal's manual toggle. No request is ever sent anywhere: the
    /// send-page test is a GET, and a POST would only record here and read
    /// back as "nothing was submitted".
    /// </summary>
    internal sealed class StubSubmitCaseToEva : ISubmitCaseToEva
    {
        public List<SubmitCaseToEvaRequest> Requests { get; } = [];

        public Task<SubmitCaseToEvaResult?> ExecuteAsync(
            SubmitCaseToEvaRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<SubmitCaseToEvaResult?>(null);
        }
    }

    /// <summary>
    /// The post-redirect-get lands on the Case record, carrying at most the
    /// section it returns to (v26: <c>?section=</c> plus a fragment). Name the
    /// expected query to pin the section.
    /// </summary>

    internal sealed partial class RecordingCaseDetailsStore :
        IGetCase,
        IGetCasePageFrame,
        ICaseDataQueries,
        IInspectionAddressChoicesQueries,
        IAcquireCaseEditLease,
        IRecordManualCaseChase,
        IHoldCase,
        IReleaseCase,
        ITransitionCase,
        ICaseWorkflowQueries,
        ISaveCaseWorkspace,
        IGetCaseVehicleSection,
        IGetCaseValuationSection,
        IGetCaseNotesSection,
        IGetCaseFilesSection,
        IValidateCaseRenderLease
    {
        private readonly DateTimeOffset _now = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private CaseDueWork _dueWork;
        private string? _leaseHolder;
        private ActorKind? _leaseHolderKind = ActorKind.Staff;
        private string? _leaseOperationKey;

        public RecordingCaseDetailsStore()
        {
            _dueWork = new(
                CaseId,
                "QDOS26001",
                "Vehicle images",
                new DateOnly(2031, 5, 10),
                CaseDueWorkState.Scheduled,
                _now.AddDays(1),
                null,
                null,
                null,
                null,
                null,
                3);
        }

        public Guid CaseId { get; } = Guid.NewGuid();

        public long CaseVersion { get; private set; } = 7;
        public bool AcceptWorkspaceSaves { get; init; }

        /// <summary>The workflow state the projection reports; Not ready unless a test says otherwise.</summary>
        public CaseLifecycleState State { get; set; } = CaseLifecycleState.NotReady;

        public bool ExposeCustody { get; init; }

        /// <summary>
        /// The lifecycle state the store's case data reports. The default keeps the
        /// workflow surface's NotReady answer; a page that acts on a particular
        /// state sets the state it needs.
        /// </summary>
        public CaseLifecycleState CaseState { get; init; } = CaseLifecycleState.NotReady;

        /// <summary>The detected Sent evidence the projection offers for confirmation (D10).</summary>
        public IReadOnlyList<RetainedApprovedMailboxReportSentEvidence> AvailableReportSentEvidence
        {
            get;
            init;
        } = [];

        public IReadOnlyList<CaseHistoryEntry> HistoryEntries { get; init; } = [];

        public IReadOnlyList<CaseCorrespondenceEmail> CorrespondenceEmails { get; init; } = [];

        public string LeaseToken { get; } = new('a', CaseEditAuthority.LeaseTokenLength);

        public bool RenderLeaseIsCurrent { get; set; } = true;

        /// <summary>Fails a test if a focused page path falls back to the legacy full Case read.</summary>
        public bool ThrowOnBroadCaseRead { get; init; }

        public CaseAssessmentProjection? FocusedAssessment { get; set; }
        public List<GetCaseSectionQuery> VehicleSectionQueries { get; } = [];
        public List<GetCaseSectionQuery> ValuationSectionQueries { get; } = [];
        public List<CaseAssessmentProjection?> VehicleSectionAssessments { get; } = [];
        public List<CaseAssessmentProjection?> ValuationSectionAssessments { get; } = [];

        public List<ClaimCaseEditLeaseRequest> Claims { get; } = [];
        public string? LeaseHolder
        {
            get => _leaseHolder;
            set => _leaseHolder = value;
        }

        public ActorKind? LeaseHolderKind
        {
            get => _leaseHolderKind;
            set => _leaseHolderKind = value;
        }

        public List<SaveCaseWorkspaceRequest> Saves { get; } = [];
        public CaseDataProjection? DataOverride { get; set; }
        public CaseWorkspaceClaimSource? ClaimSource { get; set; }
        public List<ManualChaseRecord> ManualChases { get; } = [];
        public List<PutCaseOnHoldRequest> Holds { get; } = [];
        public List<CaseMutationRequest> Releases { get; } = [];
        public List<TransitionCaseRequest> Transitions { get; } = [];
        public InspectionAddressChoicesData InspectionChoices { get; init; } = new(
            "8 Claimant Street",
            RepairerAddress: null,
            "14 Storage Lane",
            ["2 Previous Street", "1 Older Avenue"]);

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (ThrowOnBroadCaseRead)
            {
                throw new InvalidOperationException("A focused Case page read used IGetCase.");
            }

            var workflow = CreateWorkflow();
            var summary = CreateSummary(workflow);
            CaseDetails details = new(
                summary,
                workflow,
                ActiveLease(),
                CaseDocuments,
                null,
                CaseCustodyState.Pending,
                AvailableReportSentEvidence,
                HistoryEntries)
            {
                Data = DataOverride ?? CreateData(),
                VehicleEvidence = VehicleLookupEvidence,
                CorrespondenceEmails = CorrespondenceEmails,
                RecordNotes = RecordNotes,
                Custody = ExposeCustody
                    ? [new(CaseId, CaseVersion, CustodyTargetKind.CaseSource, "Failed", "Provider storage was unavailable.", 1, true)]
                    : []
            };
            return Task.FromResult<CaseDetails?>(details);
        }

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            PageFrameQueries.Add(query);
            if (query.CaseId != CaseId)
            {
                return Task.FromResult<CasePageFrame?>(null);
            }

            return Task.FromResult<CasePageFrame?>(new(
                FocusedFrame(),
                CaseDocuments,
                AvailableReportSentEvidence,
                RecordNotes,
                DataOverride ?? CreateData()));
        }

        Task<CaseVehicleSection?> IGetCaseVehicleSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            VehicleSectionQueries.Add(query);
            if (query.CaseId != CaseId)
            {
                return Task.FromResult<CaseVehicleSection?>(null);
            }

            var sectionAssessment = query.AssessmentWorkspace?.Assessment ?? FocusedAssessment ?? EngineeringAssessment();
            VehicleSectionAssessments.Add(sectionAssessment);
            return Task.FromResult<CaseVehicleSection?>(new(
                query.Frame ?? FocusedFrame(),
                query.AssessmentWorkspace?.Data ?? query.Data ?? DataOverride ?? CreateData(),
                query.AssessmentWorkspace?.LatestVehicleObservation ?? VehicleLookupEvidence?.LatestObservation,
                sectionAssessment));
        }

        Task<CaseValuationSection?> IGetCaseValuationSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            ValuationSectionQueries.Add(query);
            if (query.CaseId != CaseId)
            {
                return Task.FromResult<CaseValuationSection?>(null);
            }

            var sectionAssessment = query.AssessmentWorkspace?.Assessment ?? FocusedAssessment ?? EngineeringAssessment();
            ValuationSectionAssessments.Add(sectionAssessment);
            return Task.FromResult<CaseValuationSection?>(new(
                query.Frame ?? FocusedFrame(),
                query.AssessmentWorkspace?.Data ?? query.Data ?? DataOverride ?? CreateData(),
                sectionAssessment));
        }

        Task<CaseNotesSection?> IGetCaseNotesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<CaseNotesSection?>(query.CaseId == CaseId
                ? new(query.Frame ?? FocusedFrame(), HistoryEntries)
                : null);
        }

        Task<CaseFilesSection?> IGetCaseFilesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<CaseFilesSection?>(query.CaseId == CaseId
                ? new(
                    query.Frame ?? FocusedFrame(),
                    query.Documents ?? CaseDocuments,
                    null,
                    CaseCustodyState.Pending,
                    CorrespondenceEmails,
                    StandaloneAuditEvidenceId,
                    AuditCustodyState: AuditCustodyState,
                    AuditCustodyFolderRemoteId: AuditCustodyFolderRemoteId)
                : null);
        }

        Task<bool> IValidateCaseRenderLease.ExecuteAsync(
            ValidateCaseRenderLeaseQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                RenderLeaseIsCurrent
                && string.Equals(query.Token, LeaseToken, StringComparison.Ordinal)
                && string.Equals(query.Actor.SubjectId, LeaseHolder, StringComparison.Ordinal));

        private CaseSearchItem CreateSummary(CaseWorkflowRecord workflow) => new(
            CaseId,
            workflow.Identity.Reference,
            null,
            SummaryCaseType,
            workflow.Identity.PrincipalCode,
            workflow.State,
            null,
            OmitVehicleValues ? null : "AB12CDE",
            "Case claimant",
            "CLM-42",
            _now.AddDays(-2),
            "Email",
            _now.AddDays(-2));

        private CaseEditLeaseSnapshot? ActiveLease() => _leaseHolder is null
            ? null
            : new(_leaseHolder, _leaseHolderKind, _now.AddMinutes(5), _leaseOperationKey!);

        private CaseSectionFrame FocusedFrame()
        {
            var workflow = CreateWorkflow();
            return new(CreateSummary(workflow), workflow, ActiveLease(), Works: Works);
        }

        /// <summary>
        /// The same case the details surface serves, through the port the data-reading
        /// case pages (the EVA send page) use.
        /// </summary>
        public Task<CaseDataProjection?> GetAsync(Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<CaseDataProjection?>(caseId == CaseId ? DataOverride ?? CreateData() : null);

        Task<CaseWorkflowRecord?> ICaseWorkflowQueries.GetAsync(
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult<CaseWorkflowRecord?>(
                caseId == CaseId ? CreateWorkflow() : null);

        Task<bool> ICaseWorkflowQueries.HasOperationAsync(
            Guid caseId,
            string operationKey,
            CancellationToken cancellationToken) => Task.FromResult(false);

        Task<InspectionAddressChoicesData?> IInspectionAddressChoicesQueries.GetAsync(
            Guid caseId,
            CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<InspectionAddressChoicesData?>(
                caseId == CaseId ? InspectionChoices : null);

        /// <summary>
        /// The case as it currently stands, so a refused editor's proposed values have something to
        /// be compared against rather than an empty "the case now holds" column.
        /// </summary>
        private CaseDataProjection CreateData() =>
            new(
                new(CaseId, "QDOS", 2031, 42, "QDOS3100042"),
                new(
                    Guid.NewGuid(),
                    IntakeSourceChannel.Mailbox,
                    "receipt-token",
                    "source-hash",
                    _now.AddDays(-2),
                    "reader",
                    "1",
                    null,
                    null),
                _now.AddDays(-2),
                CaseVersion,
                CaseState,
                new(
                    new(
                        InstructionComplete: true,
                        ImagesComplete: true),
                    new(false, "case-completeness", 1)),
                new(Confirmed("QDOS")),
                new(Confirmed("Case claimant"), Empty<string>(), Empty<string>()),
                new(Confirmed("CLM-42")),
                VehicleFields(),
                new(Empty<DateOnly>(), Confirmed("Rear impact")),
                new(Confirmed("Case contact"), Empty<string>(), Empty<string>()),
                new(CaseDataPolicy.ReceivedDate(_now.AddDays(-2), _now.AddDays(-2)), Confirmed("Standard")),
                new(
                    Empty<DateOnly>(),
                    Empty<DateOnly>(),
                    Confirmed("1 Depot Road"),
                    Confirmed(CaseInspectionMode.PhysicalAddress),
                    Confirmed("14 Storage Lane"),
                    Empty<string>()),
                Workspace: ClaimSource is null
                    ? null
                    : new(ClaimSource, null, null, null, null, null, null, null, null, null, null),
                StandaloneAuditEvidenceId: StandaloneAuditEvidenceId);

        /// <summary>
        /// The vehicle as the case holds it. A lookup now fills empty fields as
        /// working values rather than suggesting beside them, so there is no
        /// suggestion shape to build.
        /// </summary>
        private CaseVehicleData VehicleFields() => OmitVehicleValues
            ? new(Empty<string>(), Empty<string>(), Empty<string>(), Empty<string>(), Empty<long>(), Empty<string>())
            : new(
                Confirmed("AB12CDE"),
                Confirmed("Ford"),
                Confirmed("Transit"),
                Confirmed("2019"),
                Confirmed(42_000L),
                Confirmed("miles"));

        private static readonly CaseDataSource StaffCorrection =
            new(CaseDataSourceKind.StaffCorrection, "staff", "Staff correction", "case-edit", 1);

        private CaseField<T> Confirmed<T>(T value)
            where T : notnull =>
            new(
                null,
                null,
                new(value, CaseDataValueKind.Confirmed, StaffCorrection, "staff", _now));

        private static CaseField<T> Empty<T>()
            where T : notnull =>
            new(null, null, null);

        Task<CaseEditLease> IAcquireCaseEditLease.ExecuteAsync(
            ClaimCaseEditLeaseRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            _leaseHolder = request.Actor.SubjectId;
            _leaseHolderKind = request.Actor.Kind;
            _leaseOperationKey = request.OperationKey;
            Claims.Add(request);
            return Task.FromResult(
                new CaseEditLease(
                    request.CaseId,
                    LeaseToken,
                    request.Actor.SubjectId,
                    request.ExpectedVersion,
                    _now.AddMinutes(5)));
        }


        Task<SaveCaseWorkspaceResult> ISaveCaseWorkspace.ExecuteAsync(
            SaveCaseWorkspaceRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Saves.Add(request);
            if (AcceptWorkspaceSaves)
            {
                CaseVersion++;
                return Task.FromResult(new SaveCaseWorkspaceResult(CreateData(), EngineeringAssessment(), null, false));
            }
            throw new CaseVersionConflictException(CaseId, request.ExpectedVersion, CaseVersion + 1);
        }

        Task<CaseWorkflowRecord> IHoldCase.ExecuteAsync(
            PutCaseOnHoldRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Holds.Add(request);
            return Task.FromResult(CreateWorkflow() with { State = CaseLifecycleState.Held });
        }

        Task<CaseWorkflowRecord> IReleaseCase.ExecuteAsync(
            CaseMutationRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Releases.Add(request);
            return Task.FromResult(CreateWorkflow() with { State = CaseLifecycleState.Review });
        }

        Task<CaseWorkflowRecord> ITransitionCase.ExecuteAsync(
            TransitionCaseRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowNextFailure();
            Transitions.Add(request);
            return Task.FromResult(CreateWorkflow() with
            {
                State = request.Destination == CaseTransitionDestination.ReportPreparation
                    ? CaseLifecycleState.ReportPreparation
                    : CaseLifecycleState.Review
            });
        }
        private CaseWorkflowRecord CreateWorkflow() =>
            new CaseWorkflowRecord(
                CaseId,
                new(CaseId, "QDOS", 2031, 42, "QDOS3100042"),
                State,
                AssignedEngineerId,
                null,
                ReportSentEvidence,
                _dueWork,
                null,
                null,
                null,
                CaseVersion) with
            {
                HoldReviewOn = HoldReviewOn,
                AssignedEngineerId = AssignedEngineerId,
                ReportSentEvidence = ReportSentEvidence
            };

        Task<CaseDueWork> IRecordManualCaseChase.ExecuteAsync(
            ManualChaseRecord request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            ManualChases.Add(request);
            _dueWork = _dueWork with
            {
                NextChaseAtUtc = _now.AddDays(7),
                MostRecentChannel = request.Channel,
                MostRecentOutcome = request.Outcome,
                MostRecentNote = request.Note,
                Version = _dueWork.Version + 1
            };
            _leaseHolder = null;
            _leaseOperationKey = null;
            return Task.FromResult(_dueWork);
        }
    }

    internal static async Task<LeasedWorkspace> EnterEngineerEditModeAsync(
        RecordingCaseDetailsStore store,
        Action<IServiceCollection> substitutePorts,
        StaffRole role = StaffRole.Engineer)
    {
        var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                Substitute<IGetCase>(services, store);
                Substitute<IGetCasePageFrame>(services, store);
                Substitute<IGetCaseVehicleSection>(services, store);
                Substitute<IGetCaseValuationSection>(services, store);
                Substitute<IGetCaseNotesSection>(services, store);
                Substitute<IGetCaseFilesSection>(services, store);
                Substitute<IValidateCaseRenderLease>(services, store);
                Substitute<IAcquireCaseEditLease>(services, store);
                Substitute<IGetAssessmentAccess>(services, new FakeGetAssessmentAccess(canOpen: true));
                Substitute<IGetAssessmentWorkspace>(services, store);
                substitutePorts(services);
            }));
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", role.ToString());
        var initial = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        using var claim = await client.PostAsync(
            $"/Cases/{store.CaseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", store.CaseId.ToString("D")),
                ("expectedVersion", store.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", InputValue(initial, "operationKey"))));
        AssertPrg(claim, store.CaseId);
        var leased = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}");
        Assert.Equal(store.LeaseToken, InputValue(leased, "editLeaseToken"));
        return new(baseFactory, factory, client, store, AntiforgeryValue(leased));
    }

}
