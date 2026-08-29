using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ENG-028: the assessment page's named-estimate import and editor, end to end
/// through the web with the real Audatex parser and the synthetic fixture —
/// a dropped PDF is parsed first, retained through the case-document custody
/// path, and landed through ENG-026's named-estimate use case with provenance
/// from the retained file. Only the stores are substituted, so the page's own
/// guards (Engineer-only, parse-before-retain, two-lease sequencing) are
/// exercised for real.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AssessmentEstimateImportWebTests
{
    [Fact]
    public async Task AnImportedEstimateIsRetainedAndLandsAsADraftWithProvenance()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var fixture = AudatexEstimateFixture.Build();

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Import estimate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=ImportEstimate",
            ImportForm(AntiforgeryValue(html), caseId, operationKey, fixture));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("section=", response.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var document = Assert.Single(store.AddedDocuments);
        Assert.Equal("estimate.pdf", document.FileName);
        Assert.Equal("application/pdf", document.MediaType);
        Assert.Equal(DocumentSemanticRole.Other, document.SemanticRole);
        Assert.Equal(DocumentSource.StaffUpload, document.Source);
        Assert.Equal($"estimate-import:{operationKey}", document.SourceOccurrenceIdentity);
        Assert.Equal(fixture, document.Content.ToArray());
        Assert.Equal(RecordingStores.CaseVersion, document.ExpectedCaseVersion);
        // The operator's own edit mode, not a lease this handler claimed for itself.
        Assert.Equal(RecordingStores.HeldLeaseToken, document.EditLeaseToken);

        var estimate = Assert.Single(store.SavedEstimates);
        Assert.Null(estimate.EstimateId);
        Assert.Equal("Imported estimate", estimate.Details.Name);
        Assert.Equal(RepairSpecificationSourceRoute.AudatexPdf, estimate.Source.Route);
        Assert.Equal($"estimate-import:{operationKey}", estimate.Source.ArtifactReference);
        Assert.Equal("TEST01 V1/1", estimate.Source.SourceVersion);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(fixture)), estimate.Source.Sha256);
        Assert.Equal(RecordingStores.CaseVersion + 1, estimate.ExpectedVersion);
        // Retaining the document was itself a case mutation, so it ended edit mode and moved the
        // version; the draft is the second half of one action and re-enters on the operator's
        // behalf. That single re-claim is the only one the import still makes.
        Assert.Equal("lease-1", estimate.EditLeaseToken);
        Assert.Equal(
            RecordingStores.CaseVersion + 1,
            Assert.Single(store.LeaseClaims).ExpectedVersion);
        Assert.Equal(operationKey, estimate.OperationKey);
        Assert.NotNull(estimate.Lines);
        Assert.Equal(6, estimate.Lines!.Count);
        Assert.Equal(620.20m, estimate.Lines.Single(line => line.Description == "FRONT BUMPER" && line.Type == "new_part").Price);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?estimate={store.LastCreatedEstimateId:D}");
        Assert.Contains("imported as a draft with 6 lines", afterHtml, StringComparison.Ordinal);
        Assert.Contains("The original document is kept on the case.", afterHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// CASE-024: a save submitted from a page that was never in edit mode is refused here rather
    /// than in Core, so nothing is retained and the operator is told what to do.
    /// </summary>
    [Fact]
    public async Task AnImportIsRefusedWhenEditModeWasNeverEntered()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=ImportEstimate",
            ImportForm(
                AntiforgeryValue(html),
                caseId,
                NewOperationKey(),
                AudatexEstimateFixture.Build(),
                editLeaseToken: null));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Empty(store.LeaseClaims);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Enter edit mode", afterHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// The assessment offers the same edit mode the workspace does, over the same lease, so an
    /// engineer working an assessment is visible to other staff as the case's editor.
    /// </summary>
    [Fact]
    public async Task TheAssessmentOffersEditModeAndClaimsTheCasesOwnLease()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Edit assessment", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=ClaimLease",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("expectedVersion", RecordingStores.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                ("operationKey", NewOperationKey())));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var claim = Assert.Single(store.LeaseClaims);
        Assert.Equal(caseId, claim.CaseId);
        Assert.Equal(RecordingStores.CaseVersion, claim.ExpectedVersion);
    }

    [Fact]
    public async Task ARejectedParseRetainsNothingAndNamesTheReason()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        // The document's own parts sub-total disagrees with its lines.
        var fixture = AudatexEstimateFixture.Build(partsSubTotal: "£999.99");

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=ImportEstimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), fixture));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Empty(store.LeaseClaims);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("do not add up to the document", afterHtml, StringComparison.Ordinal);
        Assert.Contains("nothing was imported", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyAnEngineerCanImport()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        // The default test identity is an Administrator, not an Engineer.
        using var client = CreateClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=ImportEstimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Only an Engineer can import an estimate.", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportDialogHasAStaticTargetWhenJavaScriptIsUnavailable()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains(
            $"href=\"/Cases/{caseId:D}/Assessment?dialog=import-estimate\"",
            html,
            StringComparison.Ordinal);

        var staticTarget = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}/Assessment?dialog=import-estimate");
        var dialog = Regex.Match(
            staticTarget,
            "<div class=\"dialog-backdrop\" data-dialog=\"import-estimate-dialog\"[^>]*>",
            RegexOptions.CultureInvariant);
        Assert.True(dialog.Success, "The static target must render the import dialog.");
        Assert.DoesNotContain("hidden=", dialog.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseEstimateRecordsTheEngineersAcceptance()
    {
        var caseId = Guid.NewGuid();
        var draft = DraftSpecification(caseId);
        var store = new RecordingStores(caseId) { CurrentDraft = draft };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?estimate={draft.SpecificationId:D}");
        Assert.Contains("Use estimate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=SetCurrentEstimate",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", RecordingStores.HeldLeaseToken),
                ("estimateId", draft.SpecificationId.ToString("D"))));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var use = Assert.Single(store.SetCurrentRequests);
        Assert.Equal(draft.SpecificationId, use.EstimateId);
        Assert.Equal(RecordingStores.CaseVersion, use.ExpectedVersion);
        Assert.Equal(RecordingStores.HeldLeaseToken, use.EditLeaseToken);
        Assert.Empty(store.LeaseClaims);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?estimate={draft.SpecificationId:D}");
        Assert.Contains(
            "The estimate is now the case's current estimate.",
            WebUtility.HtmlDecode(afterHtml),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheEditorSavesANamedEstimateWithTypedLines()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment?estimate=new");
        Assert.Contains("New estimate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=SaveEstimate",
            new FormUrlEncodedContent(
                NewEnumerable(
                    ("__RequestVerificationToken", AntiforgeryValue(html)),
                    ("id", caseId.ToString("D")),
                    ("operationKey", operationKey),
                    ("editLeaseToken", RecordingStores.HeldLeaseToken),
                    ("estimateName", "Repair alternative"),
                    ("estimateRepairDays", "3"),
                    ("estimateLabourRate", "48.50"),
                    ("estimatePaintLabourRate", "42.00"),
                    ("estimatePaintMaterials", "120.00"),
                    ("estimateOtherCosts", "75.00"),
                    ("estimateVatPercent", "20"),
                    ("estimateNotes", "Bumper and paint."),
                    ("lineOperation", "Replace"),
                    ("lineDescription", "Front bumper"),
                    ("linePartNumber", "51 11 8 067"),
                    ("lineQuantity", "1"),
                    ("lineLabourHours", "2.0"),
                    ("linePaintHours", "1.5"),
                    ("linePartPounds", "620.20"),
                    ("lineOperation", string.Empty),
                    ("lineDescription", string.Empty),
                    ("linePartNumber", string.Empty),
                    ("lineQuantity", string.Empty),
                    ("lineLabourHours", string.Empty),
                    ("linePaintHours", string.Empty),
                    ("linePartPounds", string.Empty))));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = Assert.Single(store.SavedEstimates);
        Assert.Null(saved.EstimateId);
        Assert.Equal("Repair alternative", saved.Details.Name);
        Assert.Equal(3, saved.Details.RepairDays);
        Assert.Equal(48.50m, saved.Details.LabourRate);
        Assert.Equal(42.00m, saved.Details.PaintLabourRate);
        Assert.Equal(120.00m, saved.Details.PaintMaterials);
        Assert.Equal(75.00m, saved.Details.OtherCosts);
        Assert.Equal(20m, saved.Details.VatPercent);
        Assert.Equal(RepairSpecificationSourceRoute.Manual, saved.Source.Route);
        var line = Assert.Single(saved.Lines!);
        Assert.Equal("new_part", line.Type);
        Assert.Equal("Front bumper", line.Description);
        Assert.Equal(2.0m, line.WorkUnits);
        Assert.Equal(1.5m, line.PaintWorkUnits);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(620.20m, line.Price);
    }

    [Fact]
    public async Task DuplicateEstimatePostsToTheNamedEstimateUseCase()
    {
        var caseId = Guid.NewGuid();
        var draft = DraftSpecification(caseId);
        var store = new RecordingStores(caseId) { CurrentDraft = draft };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Duplicate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=DuplicateEstimate",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", RecordingStores.HeldLeaseToken),
                ("estimateId", draft.SpecificationId.ToString("D"))));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var duplicate = Assert.Single(store.DuplicatedEstimates);
        Assert.Equal(draft.SpecificationId, duplicate.EstimateId);
        Assert.Equal(RecordingStores.CaseVersion, duplicate.ExpectedVersion);
        Assert.Equal(RecordingStores.HeldLeaseToken, duplicate.EditLeaseToken);
        Assert.Empty(store.LeaseClaims);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("The estimate was duplicated.", afterHtml, StringComparison.Ordinal);
    }

    private static RepairSpecificationVersion DraftSpecification(Guid caseId) => new(
        Guid.NewGuid(),
        caseId,
        1,
        RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.AudatexPdf, "estimate-import:abc", "TEST01 V1/1", new string('a', 64)),
        [
            new(
                Guid.NewGuid(), 1, "new_part", "283", "FRONT BUMPER", null, 620.20m, false,
                "51 11 8 067", "0%", "provisional", "case", null,
                ActorKind.Staff, "engineer-1", DateTimeOffset.UtcNow, "engineer-1", DateTimeOffset.UtcNow),
        ],
        null,
        "engineer-1",
        DateTimeOffset.UtcNow,
        null,
        null,
        null,
        null,
        new("Estimate 1", null, null, null, null, null, 20m, null));

    private static WebApplicationFactory<Program> Compose(
        IntakeWebApplicationFactory baseFactory, RecordingStores store) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<IRepairSpecificationStore>();
                services.RemoveAll<IAddCaseDocument>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IListCaseEstimates>();
                services.RemoveAll<ISaveEstimate>();
                services.RemoveAll<IDuplicateEstimate>();
                services.RemoveAll<IDiscardEstimate>();
                services.RemoveAll<ISetCurrentEstimate>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess());
                services.AddSingleton<IGetAssessmentWorkspace>(store);
                services.AddSingleton<IRepairSpecificationStore>(store);
                services.AddSingleton<IAddCaseDocument>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IListCaseEstimates>(store);
                services.AddSingleton<ISaveEstimate>(store);
                services.AddSingleton<IDuplicateEstimate>(store);
                services.AddSingleton<IDiscardEstimate>(store);
                services.AddSingleton<ISetCurrentEstimate>(store);
            }));

    private static HttpClient CreateEngineerClient(WebApplicationFactory<Program> factory)
    {
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "Engineer");
        return client;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139"),
        });

    /// <summary>
    /// CASE-024: the import runs under edit mode the operator entered, so the form carries the
    /// lease token the page was rendered with. Pass null for the token to submit as a page that
    /// was never in edit mode.
    /// </summary>
    private static MultipartFormDataContent ImportForm(
        string antiforgeryToken,
        Guid caseId,
        string operationKey,
        byte[] pdfBytes,
        string? editLeaseToken = RecordingStores.HeldLeaseToken)
    {
        var file = new ByteArrayContent(pdfBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent
        {
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" },
            { new StringContent(caseId.ToString("D")), "id" },
            { new StringContent(operationKey), "operationKey" },
            { new StringContent("Imported estimate"), "name" },
            { new StringContent("audatex-pdf"), "source" },
            { file, "estimateFile", "estimate.pdf" },
        };
        if (editLeaseToken is not null)
        {
            form.Add(new StringContent(editLeaseToken), "editLeaseToken");
        }

        return form;
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken, params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    /// <summary>A form that preserves repeated keys (the editor's line rows).</summary>
    private static IEnumerable<KeyValuePair<string, string>> NewEnumerable(
        params (string Name, string Value)[] values) =>
        values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value));

    private static string NewOperationKey() => Guid.NewGuid().ToString("N");

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The page must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    /// <summary>
    /// One recording fake for the four substituted seams, so the tests can
    /// assert exactly what the page handed to each store.
    /// </summary>
    private sealed class RecordingStores(Guid caseId)
        : IGetCase, IGetAssessmentWorkspace, IRepairSpecificationStore, IAddCaseDocument,
          IAcquireCaseEditLease, IListCaseEstimates, ISaveEstimate, IDuplicateEstimate,
          IDiscardEstimate, ISetCurrentEstimate
    {
        public const long CaseVersion = 7;

        /// <summary>
        /// The lease the operator entered edit mode with, which the assessment's forms carry into
        /// each save. Only the import's second half still claims one of its own.
        /// </summary>
        public const string HeldLeaseToken = "lease-held";

        private int leaseCounter;

        public RepairSpecificationVersion? CurrentDraft { get; set; }

        public RepairSpecificationVersion? CurrentAccepted { get; set; }

        public List<AddCaseDocumentCommand> AddedDocuments { get; } = [];

        public List<AcceptRepairSpecificationRequest> Acceptances { get; } = [];

        public List<ClaimCaseEditLeaseRequest> LeaseClaims { get; } = [];

        public List<SaveEstimateRequest> SavedEstimates { get; } = [];

        public List<DuplicateEstimateRequest> DuplicatedEstimates { get; } = [];

        public List<DiscardEstimateRequest> DiscardedEstimates { get; } = [];

        public List<SetCurrentEstimateRequest> SetCurrentRequests { get; } = [];

        public Guid LastCreatedEstimateId { get; private set; }

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (query.CaseId != caseId)
            {
                return Task.FromResult<CaseDetails?>(null);
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.Review, null, null,
                null, null, null, null, null, CaseVersion);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, new DateOnly(2026, 8, 1), "Email", DateTimeOffset.UtcNow);
            CaseDetails details = new(
                summary, workflow, null, [], null, CaseCustodyState.Pending, [], [], []);
            return Task.FromResult<CaseDetails?>(details);
        }

        public async Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query,
            CancellationToken cancellationToken = default)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            if (details is null)
            {
                return null;
            }
            var assessment = new CaseAssessmentProjection(
                caseId,
                details.Summary.Reference,
                CaseVersion,
                CaseLifecycleState.Review,
                null,
                [],
                [],
                new(null, null, null, null, null, null, null, null, null));
            return AssessmentWorkspaceTestData.Create(
                details, assessment, CurrentDraft, CurrentAccepted);
        }

        public Task<RepairSpecificationVersion> StartDraftAsync(
            StartRepairSpecificationDraftRequest request, CancellationToken cancellationToken)
        {
            var started = DraftSpecification(request.CaseId) with { Source = request.Source };
            CurrentDraft = started;
            return Task.FromResult(started);
        }

        public Task<RepairSpecificationVersion> AcceptAsync(
            AcceptRepairSpecificationRequest request, CancellationToken cancellationToken)
        {
            Acceptances.Add(request);
            var accepted = CurrentDraft! with
            {
                State = RepairSpecificationState.Accepted,
                CalculationBasis = request.CalculationBasis,
                AcceptedBy = request.Actor.SubjectId,
                AcceptedAtUtc = DateTimeOffset.UtcNow,
            };
            CurrentDraft = null;
            CurrentAccepted = accepted;
            return Task.FromResult(accepted);
        }

        public Task<RepairSpecificationVersion?> GetVersionAsync(
            Guid ownerCaseId, Guid specificationId, CancellationToken cancellationToken) =>
            Task.FromResult<RepairSpecificationVersion?>(null);

        public Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(
            Guid ownerCaseId, CancellationToken cancellationToken) =>
            Task.FromResult(CurrentAccepted);

        public Task<RepairSpecificationVersion?> GetCurrentDraftAsync(
            Guid ownerCaseId, CancellationToken cancellationToken) =>
            Task.FromResult(CurrentDraft);

        // The named-estimate use cases are the page's only estimate-mutation seam.
        public Task<RepairSpecificationVersion> SaveEstimateAsync(
            SaveEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DuplicateEstimateAsync(
            DuplicateEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DiscardEstimateAsync(
            DiscardEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> SetCurrentEstimateAsync(
            SetCurrentEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(
            Guid ownerCaseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>(
                new[] { CurrentAccepted, CurrentDraft }.Where(item => item is not null).ToArray()!);

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid ownerCaseId, CancellationToken cancellationToken) =>
            ListEstimatesAsync(ownerCaseId, cancellationToken);

        public Task<RepairSpecificationVersion> ExecuteAsync(
            SaveEstimateRequest request,
            CancellationToken cancellationToken = default)
        {
            SavedEstimates.Add(request);
            if (request.EstimateId is not null)
            {
                return Task.FromResult(CurrentDraft ?? DraftSpecification(caseId));
            }

            var created = DraftSpecification(caseId) with
            {
                Details = request.Details,
                Source = request.Source,
            };
            LastCreatedEstimateId = created.SpecificationId;
            CurrentDraft = created;
            return Task.FromResult(created);
        }

        public Task<RepairSpecificationVersion> ExecuteAsync(
            DuplicateEstimateRequest request,
            CancellationToken cancellationToken = default)
        {
            DuplicatedEstimates.Add(request);
            var copy = (CurrentDraft ?? CurrentAccepted ?? DraftSpecification(caseId)) with
            {
                SpecificationId = Guid.NewGuid(),
                State = RepairSpecificationState.Draft,
                IsCurrent = false,
            };
            CurrentDraft = copy;
            return Task.FromResult(copy);
        }

        public Task<RepairSpecificationVersion> ExecuteAsync(
            DiscardEstimateRequest request,
            CancellationToken cancellationToken = default)
        {
            DiscardedEstimates.Add(request);
            var discarded = (CurrentDraft ?? DraftSpecification(caseId)) with
            {
                State = RepairSpecificationState.Discarded,
            };
            CurrentDraft = null;
            return Task.FromResult(discarded);
        }

        public Task<RepairSpecificationVersion> ExecuteAsync(
            SetCurrentEstimateRequest request,
            CancellationToken cancellationToken = default)
        {
            SetCurrentRequests.Add(request);
            var madeCurrent = (CurrentDraft ?? CurrentAccepted ?? DraftSpecification(caseId)) with
            {
                State = RepairSpecificationState.Accepted,
                IsCurrent = true,
            };
            CurrentDraft = null;
            CurrentAccepted = madeCurrent;
            return Task.FromResult(madeCurrent);
        }

        public Task<AddCaseDocumentResult> ExecuteAsync(
            AddCaseDocumentCommand command, CancellationToken cancellationToken)
        {
            AddedDocuments.Add(command);
            var contentBytes = command.Content.ToArray();
            var version = new DocumentVersion(
                Guid.NewGuid(), Guid.NewGuid(), 1, command.FileName, command.MediaType,
                contentBytes.Length, Convert.ToHexStringLower(SHA256.HashData(contentBytes)),
                DocumentCustodyStatus.Pending, DateTimeOffset.UtcNow, command.Actor.SubjectId,
                true, false, null);
            var occurrence = new DocumentOccurrence(
                Guid.NewGuid(), command.CaseId, version.DocumentId, version.Id,
                command.SemanticRole, command.Source, command.SourceOccurrenceIdentity,
                DateTimeOffset.UtcNow, null, null);
            return Task.FromResult(new AddCaseDocumentResult(occurrence, version, false));
        }

        public Task<CaseEditLease> ExecuteAsync(
            ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken)
        {
            LeaseClaims.Add(request);
            leaseCounter++;
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                $"lease-{leaseCounter}",
                request.Actor.SubjectId,
                request.ExpectedVersion,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
}
