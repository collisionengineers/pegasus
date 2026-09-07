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
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Cases;
using Pegasus.Web.Presentation;

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
    /// <summary>The engineer who amended the seeded estimate's lines before this save.</summary>
    private const string SeededAmendedBy = "engineer-before";

    /// <summary>The one line amount this save changes.</summary>
    private const decimal AmendedAmount = 700.25m;

    private static readonly DateTimeOffset SeededAmendedAtUtc =
        new(2031, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The clock the host runs on, so the stamp is the server's own time.</summary>
    private static readonly DateTimeOffset SavedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnImportedEstimateIsRetainedAndLandsAsADraftWithProvenance()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var fixture = AudatexEstimateFixture.Build();

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("Import estimate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, operationKey, fixture));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("section=estimate", response.Headers.Location?.OriginalString, StringComparison.Ordinal);

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

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate={store.LastCreatedEstimateId:D}");
        Assert.Contains("imported as a draft with 6 lines", afterHtml, StringComparison.Ordinal);
        Assert.Contains("The original document is kept on the case.", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnImportedEstimatePreservesItsLineEvidenceWhenEditedAndSaved()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var importHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var importResponse = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(
                AntiforgeryValue(importHtml),
                caseId,
                NewOperationKey(),
                AudatexEstimateFixture.Build()));

        Assert.Equal(HttpStatusCode.Redirect, importResponse.StatusCode);
        var imported = Assert.Single(store.SavedEstimates);
        var draft = Assert.IsType<RepairSpecificationVersion>(store.CurrentDraft);
        var editorHtml = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", AntiforgeryValue(editorHtml)),
            new("id", caseId.ToString("D")),
            new("operationKey", NewOperationKey()),
            new("editLeaseToken", RecordingStores.HeldLeaseToken),
            new("estimateId", draft.SpecificationId.ToString("D")),
            new("estimateName", draft.Details.Name),
            new("estimateVatPercent", draft.Details.VatPercent.ToString(CultureInfo.InvariantCulture)),
        };
        foreach (var line in draft.Lines.OrderBy(line => line.Position))
        {
            fields.Add(new("lineId", line.Id.ToString("D")));
            fields.Add(new("lineOperation", EstimateOperations.FromLineType(line.Type).ToString()));
            fields.Add(new("lineDescription", line.Description ?? string.Empty));
            fields.Add(new("linePartNumber", line.PartNumber ?? string.Empty));
            fields.Add(new("lineQuantity", line.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new(
                "lineLabourHours",
                line.Position == 1
                    ? "9.9"
                    : line.WorkUnits?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("linePaintHours", line.PaintWorkUnits?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("linePartPounds", line.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        using var saveResponse = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SaveEstimate&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, saveResponse.StatusCode);
        Assert.Equal(2, store.SavedEstimates.Count);
        var edited = store.SavedEstimates[1];
        Assert.Equal(imported.Source, edited.Source);
        Assert.Equal(9.9m, edited.Lines[0].WorkUnits);
        Assert.Equal(imported.Lines.Count, edited.Lines.Count);
        foreach (var (before, after) in imported.Lines.Zip(edited.Lines))
        {
            Assert.Equal(before.GuideCode, after.GuideCode);
            Assert.Equal(before.Unpriced, after.Unpriced);
            Assert.Equal(before.Betterment, after.Betterment);
            Assert.Equal(before.Status, after.Status);
            Assert.Equal(before.EvidenceLabel, after.EvidenceLabel);
            Assert.Equal(before.Justification, after.Justification);
        }
        var unpriced = Assert.Single(edited.Lines, line => line.Description == "GRILLE BADGE");
        Assert.True(unpriced.Unpriced);
        Assert.Null(unpriced.Price);
        Assert.Contains(
            $"name=\"lineId\" value=\"{draft.Lines[0].Id:D}\"",
            editorHtml,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// An imported line with no value arrives <c>Unpriced</c> — "To be
    /// confirmed". Pricing it is the point of the editor, and
    /// <c>AssessmentPolicy</c> refuses a line that is both marked To be
    /// confirmed and priced ("A line marked To be confirmed cannot also carry a
    /// price."), so the flag must clear when a price is entered.
    ///
    /// Carrying every evidence field forward unconditionally — the first shape
    /// of the fix for the evidence-destroying save — made this save impossible.
    /// </summary>
    [Fact]
    public async Task PricingAnImportedUnpricedLineClearsItsToBeConfirmedFlag()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var importHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var importResponse = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(
                AntiforgeryValue(importHtml),
                caseId,
                NewOperationKey(),
                AudatexEstimateFixture.Build()));
        Assert.Equal(HttpStatusCode.Redirect, importResponse.StatusCode);

        var draft = Assert.IsType<RepairSpecificationVersion>(store.CurrentDraft);
        var unpricedBefore = Assert.Single(draft.Lines, line => line.Description == "GRILLE BADGE");
        Assert.True(unpricedBefore.Unpriced);
        Assert.Null(unpricedBefore.Price);

        var editorHtml = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", AntiforgeryValue(editorHtml)),
            new("id", caseId.ToString("D")),
            new("operationKey", NewOperationKey()),
            new("editLeaseToken", RecordingStores.HeldLeaseToken),
            new("estimateId", draft.SpecificationId.ToString("D")),
            new("estimateName", draft.Details.Name),
            new("estimateVatPercent", draft.Details.VatPercent.ToString(CultureInfo.InvariantCulture)),
        };
        foreach (var line in draft.Lines.OrderBy(line => line.Position))
        {
            // The operator prices the one line the import left To be confirmed.
            var price = line.Id == unpricedBefore.Id
                ? "125.00"
                : line.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            fields.Add(new("lineId", line.Id.ToString("D")));
            fields.Add(new("lineOperation", EstimateOperations.FromLineType(line.Type).ToString()));
            fields.Add(new("lineDescription", line.Description ?? string.Empty));
            fields.Add(new("linePartNumber", line.PartNumber ?? string.Empty));
            fields.Add(new("lineQuantity", line.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("lineLabourHours", line.WorkUnits?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("linePaintHours", line.PaintWorkUnits?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("linePartPounds", price));
        }

        using var saveResponse = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SaveEstimate&section=estimate",
            new FormUrlEncodedContent(fields));

        // A refusal here is the regression: Core rejects Unpriced with a price.
        Assert.Equal(HttpStatusCode.Redirect, saveResponse.StatusCode);
        Assert.Equal(2, store.SavedEstimates.Count);

        var edited = store.SavedEstimates[1];
        var priced = Assert.Single(edited.Lines, line => line.Description == "GRILLE BADGE");
        Assert.Equal(125.00m, priced.Price);
        Assert.False(priced.Unpriced);

        // The rest of that line's imported evidence still survives the save.
        Assert.Equal(unpricedBefore.GuideCode, priced.GuideCode);
        Assert.Equal(unpricedBefore.Betterment, priced.Betterment);
        Assert.Equal(unpricedBefore.EvidenceLabel, priced.EvidenceLabel);
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

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
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

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("Enter edit mode", afterHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Estimate section uses the Case record's one edit mode and lease.
    /// </summary>
    [Fact]
    public async Task TheEstimateSectionUsesTheCasesOwnEditLease()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("Edit Case", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease&section=estimate",
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

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), fixture));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Empty(store.LeaseClaims);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("do not add up to the document", afterHtml, StringComparison.Ordinal);
        Assert.Contains("nothing was imported", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASourceTheFormDoesNotOfferIsRefusedBeforeAnythingIsRead()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        // A valid document under a source the select never posts: the disabled option's value.
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build(), source: "other"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Empty(store.LeaseClaims);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("The form has expired. Retry the operation.", afterHtml, StringComparison.Ordinal);
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

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
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

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains(
            $"href=\"/Cases/{caseId:D}?section=estimate&amp;dialog=import-estimate\"",
            html,
            StringComparison.Ordinal);

        var staticTarget = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=estimate&dialog=import-estimate");
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

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}");
        Assert.Contains("Use estimate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SetCurrentEstimate&section=estimate",
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

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}");
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

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate=new");
        Assert.Contains("New estimate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SaveEstimate&section=estimate",
            new FormUrlEncodedContent(
                NewEnumerable(
                    ("__RequestVerificationToken", AntiforgeryValue(html)),
                    ("id", caseId.ToString("D")),
                    ("operationKey", operationKey),
                    ("editLeaseToken", RecordingStores.HeldLeaseToken),
                    ("estimateName", "Repair alternative"),
                    ("estimateRepairDays", "3"),
                    ("estimateLabourRate", "48.50"),
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
        // B04: one hourly rate prices panel and paint hours alike, so the
        // editor neither offers nor carries a second paint rate.
        Assert.DoesNotContain("estimatePaintLabourRate", html, StringComparison.Ordinal);
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

    /// <summary>
    /// B08: the estimate header's VAT status, the categories its percentage is
    /// charged on and its four discounts are the editor's own controls. A save
    /// records exactly what was posted, and the reload renders it back — the
    /// status selected, the category boxes checked, the discounts as the
    /// percentages they were typed as.
    /// </summary>
    [Fact]
    public async Task TheEditorPostsAndRendersTheVatPolicyAndTheDiscounts()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate=new");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", AntiforgeryValue(html)),
            new("id", caseId.ToString("D")),
            new("operationKey", NewOperationKey()),
            new("editLeaseToken", RecordingStores.HeldLeaseToken),
            new("estimateName", "Repairer"),
            new("estimateLabourRate", "52.50"),
            new("estimateVatPercent", "20"),
            new("lineOperation", "Replace"),
            new("lineDescription", "Front bumper"),
            new("lineQuantity", "1"),
            new("linePartPounds", "620.20"),
        };
        // A registered repairer charging VAT on all four categories: the
        // status's own defaults, so nothing is recorded as an override.
        fields.AddRange(HeaderFields(
            EstimateVatPolicy.For(RepairerVatStatus.Registered),
            new EstimateDiscounts(0.125m, 0.05m, 0.1m, 0.025m)));

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SaveEstimate&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = Assert.Single(store.SavedEstimates);
        Assert.Equal(RepairerVatStatus.Registered, saved.Details.Vat!.RepairerStatus);
        Assert.Equal(EstimateVatCategories.All, saved.Details.Vat.Categories);
        Assert.False(saved.Details.Vat.CategoriesOverridden);
        Assert.Equal(new EstimateDiscounts(0.125m, 0.05m, 0.1m, 0.025m), saved.Details.Discounts);

        var reloaded = await GetHtmlAsync(
            client, $"/Cases/{caseId:D}?section=estimate&estimate={store.LastCreatedEstimateId:D}");
        Assert.Contains(
            "<option value=\"Registered\" selected=\"selected\">Registered</option>",
            reloaded,
            StringComparison.Ordinal);
        foreach (var category in CaseWorkspaceLabels.EstimateVat.Categories)
        {
            var field = DetailsModel.VatCategoryField(category);
            Assert.Contains(
                $"id=\"{field}\" name=\"{field}\" type=\"checkbox\" value=\"true\"",
                reloaded,
                StringComparison.Ordinal);
            Assert.Contains(
                $"<input type=\"hidden\" name=\"{field}\" value=\"false\" />",
                reloaded,
                StringComparison.Ordinal);
        }

        // The percentages round-trip as percentages, not as the fractions Core
        // validates.
        Assert.Contains(
            "name=\"estimateDiscountParts\" type=\"number\" min=\"0\" max=\"100\" step=\"0.01\" inputmode=\"decimal\" class=\"tabular\" value=\"12.5\"",
            reloaded,
            StringComparison.Ordinal);
        Assert.Contains(
            "name=\"estimateDiscountOverall\" type=\"number\" min=\"0\" max=\"100\" step=\"0.01\" inputmode=\"decimal\" class=\"tabular\" value=\"2.5\"",
            reloaded,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// B08: an Unknown repairer status charges VAT on nothing and blocks Use
    /// estimate, so the Engineer who selects the categories by hand is making
    /// the override that unblocks it. The screen records that override without
    /// a control of its own — the checked set differing from the status's own
    /// default is what the override means.
    /// </summary>
    [Fact]
    public async Task PostingUnknownWithCategoriesRecordsTheOverrideThatUnblocksUseEstimate()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate=new");
        // A new estimate opens on Unknown, charging VAT on nothing.
        Assert.Contains(
            "<option value=\"Unknown\" selected=\"selected\">Unknown</option>",
            html,
            StringComparison.Ordinal);

        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", AntiforgeryValue(html)),
            new("id", caseId.ToString("D")),
            new("operationKey", NewOperationKey()),
            new("editLeaseToken", RecordingStores.HeldLeaseToken),
            new("estimateName", "Repairer"),
            new("estimateLabourRate", "52.50"),
            new("estimateVatPercent", "20"),
            new("lineOperation", "Replace"),
            new("lineDescription", "Front bumper"),
            new("lineQuantity", "1"),
            new("linePartPounds", "620.20"),
        };
        fields.AddRange(HeaderFields(
            new EstimateVatPolicy(
                RepairerVatStatus.Unknown,
                EstimateVatCategories.Parts | EstimateVatCategories.Materials,
                true),
            EstimateDiscounts.None));

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SaveEstimate&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = Assert.Single(store.SavedEstimates);
        Assert.Equal(RepairerVatStatus.Unknown, saved.Details.Vat!.RepairerStatus);
        Assert.Equal(
            EstimateVatCategories.Parts | EstimateVatCategories.Materials,
            saved.Details.Vat.Categories);
        Assert.True(saved.Details.Vat.CategoriesOverridden);
        Assert.False(saved.Details.Vat.BlocksAcceptance);
        Assert.Equal(EstimateDiscounts.None, saved.Details.Discounts);

        // Unblocked, so the reload offers the live control rather than the
        // gated one.
        var reloaded = await GetHtmlAsync(
            client, $"/Cases/{caseId:D}?section=estimate&estimate={store.LastCreatedEstimateId:D}");
        Assert.Contains("handler=SetCurrentEstimate", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain(
            CaseWorkspaceLabels.EstimateVat.UnknownStatusCondition,
            reloaded,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// B08: Core refuses to make an estimate Current while its repairer VAT
    /// status is unrecorded, so the screen never offers the control that would
    /// be refused — it renders the workspace's gated shape naming the
    /// condition.
    /// </summary>
    [Fact]
    public async Task AnUnrecordedRepairerVatStatusGatesUseEstimateWithItsCondition()
    {
        var caseId = Guid.NewGuid();
        var seeded = DraftSpecification(caseId);
        var draft = seeded with { Details = seeded.Details with { Vat = null } };
        var store = new RecordingStores(caseId) { CurrentDraft = draft };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        Assert.True(draft.Details.VatPolicy.BlocksAcceptance);
        var html = await GetHtmlAsync(
            client, $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}");

        Assert.Contains(
            $"<span class=\"gated\" data-condition=\"{CaseWorkspaceLabels.EstimateVat.UnknownStatusCondition}\">",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain("handler=SetCurrentEstimate", html, StringComparison.Ordinal);

        // The same estimate with its status recorded offers the live control.
        store.CurrentDraft = seeded;
        var recordedHtml = await GetHtmlAsync(
            client, $"/Cases/{caseId:D}?section=estimate&estimate={seeded.SpecificationId:D}");
        Assert.Contains("handler=SetCurrentEstimate", recordedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(
            CaseWorkspaceLabels.EstimateVat.UnknownStatusCondition,
            recordedHtml,
            StringComparison.Ordinal);
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

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("Duplicate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=DuplicateEstimate&section=estimate",
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

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("The estimate was duplicated.", afterHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// CASE-047 B04 review defect 2, through the real editor: the save used to
    /// carry every existing line's amendment attribution forward
    /// unconditionally, so a line the operator had just changed stayed
    /// credited to whoever last touched it. Posting the real editor form with
    /// one line's amount changed must stamp that line with this Engineer and
    /// the host's clock, leave the untouched line's own stamp alone, and land
    /// the header the editor renders - the discounts and the VAT policy it now
    /// posts (B08), and the rate card it does not show - across the reload.
    /// </summary>
    [Fact]
    public async Task SavingTheEditorStampsTheChangedLineAndKeepsTheUntouchedOnes()
    {
        var caseId = Guid.NewGuid();
        var seeded = ProvenancedDraft(caseId);
        var store = new RecordingStores(caseId) { CurrentDraft = seeded };
        using var baseFactory = new IntakeWebApplicationFactory(
            "Development",
            true,
            new FixedTimeProvider(SavedAtUtc),
            useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var editorHtml = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=estimate&estimate={seeded.SpecificationId:D}");
        var details = seeded.Details;
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", AntiforgeryValue(editorHtml)),
            new("id", caseId.ToString("D")),
            new("operationKey", NewOperationKey()),
            new("editLeaseToken", RecordingStores.HeldLeaseToken),
            new("estimateId", seeded.SpecificationId.ToString("D")),
            new("estimateName", details.Name),
            new("estimateRepairDays", details.RepairDays!.Value.ToString(CultureInfo.InvariantCulture)),
            new("estimateLabourRate", details.LabourRate!.Value.ToString(CultureInfo.InvariantCulture)),
            new("estimatePaintMaterials", details.PaintMaterials!.Value.ToString(CultureInfo.InvariantCulture)),
            new("estimateOtherCosts", details.OtherCosts!.Value.ToString(CultureInfo.InvariantCulture)),
            new("estimateVatPercent", details.VatPercent.ToString(CultureInfo.InvariantCulture)),
            new("estimateNotes", details.Notes ?? string.Empty),
        };
        fields.AddRange(HeaderFields(details.VatPolicy, details.AppliedDiscounts));
        foreach (var line in seeded.Lines.OrderBy(line => line.Position))
        {
            // Only the first line's amount moves; every other posted value is
            // exactly the one the editor rendered.
            var amount = line.Position == 1
                ? AmendedAmount.ToString(CultureInfo.InvariantCulture)
                : line.Price?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            fields.Add(new("lineId", line.Id.ToString("D")));
            fields.Add(new("lineOperation", EstimateOperations.FromLineType(line.Type).ToString()));
            fields.Add(new("lineDescription", line.Description ?? string.Empty));
            fields.Add(new("linePartNumber", line.PartNumber ?? string.Empty));
            fields.Add(new("lineQuantity", line.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("lineLabourHours", line.WorkUnits?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("linePaintHours", line.PaintWorkUnits?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
            fields.Add(new("linePartPounds", amount));
        }

        using var saveResponse = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=SaveEstimate&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, saveResponse.StatusCode);
        var request = Assert.Single(store.SavedEstimates);
        Assert.Equal(seeded.SpecificationId, request.EstimateId);

        // The reload goes back through the page's own read path.
        var reloadedHtml = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=estimate&estimate={seeded.SpecificationId:D}");
        Assert.Contains(
            $"value=\"{AmendedAmount.ToString(CultureInfo.InvariantCulture)}\"",
            reloadedHtml,
            StringComparison.Ordinal);

        var reloaded = Assert.IsType<RepairSpecificationVersion>(store.CurrentDraft);
        var amended = Assert.Single(reloaded.Lines, line => line.Description == "FRONT BUMPER");
        Assert.Equal(AmendedAmount, amended.Price);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId.ToString("D"), amended.AmendedBy);
        Assert.Equal(SavedAtUtc, amended.AmendedAtUtc);
        // Stamping the amendment does not cost the line its imported evidence.
        Assert.Equal(seeded.Lines[0].Origin, amended.Origin);
        Assert.Equal(seeded.Lines[0].Materials, amended.Materials);
        Assert.Equal(seeded.Lines[0].SourceRowIdentity, amended.SourceRowIdentity);

        var untouched = Assert.Single(reloaded.Lines, line => line.Description == "REPAIR NEARSIDE DOOR");
        Assert.Equal(SeededAmendedBy, untouched.AmendedBy);
        Assert.Equal(SeededAmendedAtUtc, untouched.AmendedAtUtc);

        // The header facts survive the round trip: the discounts and the VAT
        // policy the editor posted back unchanged, and the rate card it never
        // renders at all.
        Assert.Equal(details.Discounts, reloaded.Details.Discounts);
        Assert.Equal(details.Vat, reloaded.Details.Vat);
        Assert.Equal(details.Rate, reloaded.Details.Rate);
        Assert.Equal(details.LabourRate, reloaded.Details.LabourRate);
        Assert.Equal(details.HourlyRate, reloaded.Details.HourlyRate);
    }

    /// <summary>
    /// One imported estimate as the store holds it: a priced part line and a
    /// labour line, both carrying the source document's provenance and an
    /// earlier engineer's amendment stamp, under a header with discounts,
    /// explicit VAT categories and the rate card it was priced at.
    /// </summary>
    private static RepairSpecificationVersion ProvenancedDraft(Guid caseId)
    {
        var documentVersionId = Guid.NewGuid();
        var documentSha = new string('d', 64);
        return new(
            Guid.NewGuid(),
            caseId,
            1,
            RepairSpecificationState.Draft,
            new(RepairSpecificationSourceRoute.AudatexPdf, "estimate-import:seeded", "TEST01 V1/1", documentSha),
            [
                new(
                    Guid.NewGuid(), 1, "new_part", "283", "FRONT BUMPER", null, 620.20m, false,
                    "51 11 8 067", "0%", "confirmed", "official", null,
                    ActorKind.Staff, "engineer-recorded", SeededAmendedAtUtc, null, null,
                    PaintWorkUnits: null,
                    Quantity: 1,
                    Materials: 12.50m,
                    Origin: new("new_part", "FRONT BUMPER", "51 11 8 067", 1, null, null, 620.20m, 12.50m),
                    SourceDocumentIdentity: "estimate-import:seeded",
                    SourceDocumentVersionId: documentVersionId,
                    SourceDocumentSha256: documentSha,
                    SourceRowIdentity: "parts:1",
                    AmendedBy: SeededAmendedBy,
                    AmendedAtUtc: SeededAmendedAtUtc),
                new(
                    Guid.NewGuid(), 2, "repair", null, "REPAIR NEARSIDE DOOR", 2.5m, null, false,
                    null, null, "confirmed", "judgement", null,
                    ActorKind.Staff, "engineer-recorded", SeededAmendedAtUtc, null, null,
                    PaintWorkUnits: 1.5m,
                    Quantity: null,
                    Materials: null,
                    Origin: new("repair", "REPAIR NEARSIDE DOOR", null, null, 2.5m, 1.5m, null, null),
                    SourceDocumentIdentity: "estimate-import:seeded",
                    SourceDocumentVersionId: documentVersionId,
                    SourceDocumentSha256: documentSha,
                    SourceRowIdentity: "labour:2",
                    AmendedBy: SeededAmendedBy,
                    AmendedAtUtc: SeededAmendedAtUtc),
            ],
            null,
            "engineer-1",
            SeededAmendedAtUtc,
            null,
            null,
            null,
            null,
            new(
                "Imported estimate", 3, 52.50m, 25m, 110m, 20m, "Typed from the repairer's e-mail.",
                new EstimateDiscounts(0.125m, 0.05m, 0.1m, 0.025m),
                new EstimateVatPolicy(
                    RepairerVatStatus.NotRegistered,
                    EstimateVatCategories.Parts | EstimateVatCategories.Materials,
                    false),
                new EstimateRateSnapshot(Guid.NewGuid(), 7L, 52.50m)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
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
        new("Estimate 1", null, null, null, null, 20m, null,
            Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)));

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
        string? editLeaseToken = RecordingStores.HeldLeaseToken,
        string source = "audatex-pdf")
    {
        var file = new ByteArrayContent(pdfBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        var form = new MultipartFormDataContent
        {
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" },
            { new StringContent(caseId.ToString("D")), "id" },
            { new StringContent(operationKey), "operationKey" },
            { new StringContent("Imported estimate"), "name" },
            { new StringContent(source), "source" },
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

    /// <summary>
    /// The estimate header's VAT policy and discounts as the browser posts
    /// them (B08): each category box carries the hidden false companion that
    /// makes an unchecked box submit, and a discount is a percentage.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> HeaderFields(
        EstimateVatPolicy vat, EstimateDiscounts discounts)
    {
        yield return new("estimateVatStatus", vat.RepairerStatus.ToString());
        foreach (var category in CaseWorkspaceLabels.EstimateVat.Categories)
        {
            if (vat.Charges(category))
            {
                yield return new(DetailsModel.VatCategoryField(category), "true");
            }

            yield return new(DetailsModel.VatCategoryField(category), "false");
        }

        yield return new("estimateDiscountParts", PercentField(discounts.Parts));
        yield return new("estimateDiscountMaterials", PercentField(discounts.Materials));
        yield return new("estimateDiscountSpecialist", PercentField(discounts.Specialist));
        yield return new("estimateDiscountOverall", PercentField(discounts.Overall));
    }

    private static string PercentField(decimal fraction) =>
        (fraction * 100m).ToString(CultureInfo.InvariantCulture);

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
            Task.FromResult(
                new[] { CurrentDraft, CurrentAccepted }
                    .FirstOrDefault(item => item?.SpecificationId == specificationId));

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

        public Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
            Guid ownerCaseId, int? afterVersion, Guid? afterId, int fetchCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid ownerCaseId, CancellationToken cancellationToken) =>
            ListEstimatesAsync(ownerCaseId, cancellationToken);

        public Task<RepairSpecificationVersion> ExecuteAsync(
            SaveEstimateRequest request,
            CancellationToken cancellationToken = default)
        {
            SavedEstimates.Add(request);
            // A save replaces the estimate's whole header and line collection,
            // the way the real store does, so reading the estimate back after
            // one shows exactly what the page mapped and nothing else.
            if (request.EstimateId is not null)
            {
                var replaced = (CurrentDraft ?? DraftSpecification(caseId)) with
                {
                    Details = request.Details,
                    Lines = Recorded(request),
                };
                CurrentDraft = replaced;
                return Task.FromResult(replaced);
            }

            var created = DraftSpecification(caseId) with
            {
                Details = request.Details,
                Source = request.Source,
                Lines = Recorded(request),
            };
            LastCreatedEstimateId = created.SpecificationId;
            CurrentDraft = created;
            return Task.FromResult(created);
        }

        /// <summary>
        /// The saved lines as a read returns them, every recorded fact
        /// included — the materials, source provenance and amendment
        /// attribution the editor never shows are read back from here.
        /// </summary>
        private static CaseEstimateLineRecord[] Recorded(SaveEstimateRequest request) =>
            request.Lines.Select((line, index) => new CaseEstimateLineRecord(
                Guid.NewGuid(),
                index + 1,
                line.Type,
                line.GuideCode,
                line.Description,
                line.WorkUnits,
                line.Price,
                line.Unpriced,
                line.PartNumber,
                line.Betterment,
                line.Status,
                line.EvidenceLabel,
                line.Justification,
                ActorKind.Staff,
                request.Actor.SubjectId,
                DateTimeOffset.UtcNow,
                null,
                null,
                line.PaintWorkUnits,
                line.Quantity,
                line.Materials,
                line.Origin,
                line.SourceDocumentIdentity,
                line.SourceDocumentVersionId,
                line.SourceDocumentSha256,
                line.SourceRowIdentity,
                line.AmendedBy,
                line.AmendedAtUtc)).ToArray();

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
