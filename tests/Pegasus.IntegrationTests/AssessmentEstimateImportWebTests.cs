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
/// ENG-002: the assessment page's estimate import and acceptance, end to end
/// through the web with the real Audatex parser and the synthetic fixture —
/// a dropped PDF is parsed first, retained through the case-document custody
/// path, and landed as a draft specification whose provenance carries the
/// retained file's hash; a rejected parse retains nothing; acceptance records
/// the Engineer-typed calculation basis. Only the stores are substituted, so
/// the page's own guards (Engineer-only, parse-before-retain, two-lease
/// sequencing) are exercised for real.
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

        var draft = Assert.Single(store.StartedDrafts);
        Assert.Equal(RepairSpecificationSourceRoute.AudatexPdf, draft.Source.Route);
        Assert.Equal($"estimate-import:{operationKey}", draft.Source.ArtifactReference);
        Assert.Equal("TEST01 V1/1", draft.Source.SourceVersion);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(fixture)), draft.Source.Sha256);
        Assert.Equal(RecordingStores.CaseVersion + 1, draft.ExpectedCaseVersion);
        // Retaining the document was itself a case mutation, so it ended edit mode and moved the
        // version; the draft is the second half of one action and re-enters on the operator's
        // behalf. That single re-claim is the only one the import still makes.
        Assert.Equal("lease-1", draft.EditLeaseToken);
        Assert.Equal(
            RecordingStores.CaseVersion + 1,
            Assert.Single(store.LeaseClaims).ExpectedVersion);
        Assert.Equal(operationKey, draft.OperationKey);
        Assert.Null(draft.SupersedesSpecificationId);
        Assert.NotNull(draft.Lines);
        Assert.Equal(6, draft.Lines!.Count);
        Assert.Equal(620.20m, draft.Lines.Single(line => line.Description == "FRONT BUMPER" && line.Type == "new_part").Price);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
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
        Assert.Empty(store.StartedDrafts);
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
        Assert.Empty(store.StartedDrafts);
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
        Assert.Empty(store.StartedDrafts);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Only an Engineer can import an estimate.", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnExistingDraftRefusesASecondImport()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId) { CurrentDraft = DraftSpecification(caseId) };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Awaiting an Engineer", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dialog=\"import-estimate-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("A draft estimate is awaiting acceptance", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=ImportEstimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.StartedDrafts);
    }

    [Fact]
    public async Task AcceptanceRecordsTheTypedCalculationBasis()
    {
        var caseId = Guid.NewGuid();
        var draft = DraftSpecification(caseId);
        var store = new RecordingStores(caseId) { CurrentDraft = draft };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("Accept this specification", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}/Assessment?handler=AcceptSpecification",
            Form(
                AntiforgeryValue(html),
                ("id", caseId.ToString("D")),
                ("operationKey", operationKey),
                ("editLeaseToken", RecordingStores.HeldLeaseToken),
                ("specificationId", draft.SpecificationId.ToString("D")),
                ("specificationVersion", "1"),
                ("labour", "1193.34"),
                ("parts", "1880.36"),
                ("paintMaterials", "836.85"),
                ("specialistOther", "429.00"),
                ("vat", "867.91"),
                ("repairerVatRegistered", "true"),
                ("reason", "Checked against the original document")));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var acceptance = Assert.Single(store.Acceptances);
        Assert.Equal(draft.SpecificationId, acceptance.SpecificationId);
        Assert.Equal(1, acceptance.ExpectedSpecificationVersion);
        Assert.Equal(draft.Source, acceptance.Source);
        Assert.Equal(1193.34m, acceptance.CalculationBasis.Labour);
        Assert.Equal(1880.36m, acceptance.CalculationBasis.Parts);
        Assert.Equal(836.85m, acceptance.CalculationBasis.PaintMaterials);
        Assert.Equal(429.00m, acceptance.CalculationBasis.SpecialistOther);
        Assert.True(acceptance.CalculationBasis.RepairerVatRegistered);
        Assert.Equal(867.91m, acceptance.CalculationBasis.Vat);
        Assert.Equal(5207.46m, acceptance.CalculationBasis.Total);
        Assert.Equal("Checked against the original document", acceptance.Reason);
        // Accepting runs under the operator's edit mode and claims no lease of its own.
        Assert.Equal(RecordingStores.HeldLeaseToken, acceptance.EditLeaseToken);
        Assert.Empty(store.LeaseClaims);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}/Assessment");
        Assert.Contains("The repair specification was accepted.", afterHtml, StringComparison.Ordinal);
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
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess());
                services.AddSingleton<IGetAssessmentWorkspace>(store);
                services.AddSingleton<IRepairSpecificationStore>(store);
                services.AddSingleton<IAddCaseDocument>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
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
        : IGetCase, IGetAssessmentWorkspace, IRepairSpecificationStore, IAddCaseDocument, IAcquireCaseEditLease
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

        public List<StartRepairSpecificationDraftRequest> StartedDrafts { get; } = [];

        public List<AcceptRepairSpecificationRequest> Acceptances { get; } = [];

        public List<ClaimCaseEditLeaseRequest> LeaseClaims { get; } = [];

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
            StartedDrafts.Add(request);
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

        // The named-estimate path (ENG-026) has no caller on this page yet.
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
