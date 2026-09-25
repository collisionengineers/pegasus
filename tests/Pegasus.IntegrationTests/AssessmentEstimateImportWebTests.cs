using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Web.Authentication;
using Pegasus.Web.Pages.Cases;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The assessment page's named-estimate import and editor, end to end
/// through the web with the real Audatex parser and synthetic fixture —
/// a selected or dropped PDF is retained through the Case document path and
/// immediately parsed through the canonical Core import. Only stores are
/// substituted, so the page's own guards (human-staff authority, not a role;
/// retain-before-parse; lease sequencing) are exercised for real.
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
        using var client = CreateEngineerClient(factory, StaffRole.User);
        var fixture = AudatexEstimateFixture.Build();

        // v26: the section's controls render inside the page-wide edit session.
        var html = await EnterEditModeAsync(client, caseId);
        Assert.Contains("data-estimate-import", html, StringComparison.Ordinal);
        Assert.Contains("Import estimate", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, operationKey, fixture,
                editLeaseToken: InputValue(html, "editLeaseToken")));

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
        Assert.Equal("Audatex 1", estimate.Details.Name);
        Assert.Equal(RepairSpecificationSourceRoute.AudatexPdf, estimate.Source.Route);
        Assert.Equal($"estimate-import:{store.RetainedDocument!.Occurrence.Id:D}", estimate.Source.ArtifactReference);
        Assert.Equal("TEST01 V1/1", estimate.Source.SourceVersion);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(fixture)), estimate.Source.Sha256);
        Assert.Equal(RecordingStores.CaseVersion + 1, estimate.ExpectedVersion);
        // Retaining the document was itself a case mutation, so it ended edit mode and moved the
        // version; the draft is the second half of one action and re-enters on the operator's
        // behalf (lease-2). v25 decision F: the import is an immediate post inside the
        // session, so after the draft consumed that lease the record reclaims once more
        // (lease-3) and the operator is still editing.
        Assert.Equal("lease-2", estimate.EditLeaseToken);
        Assert.Equal(3, store.LeaseClaims.Count);
        Assert.Equal(RecordingStores.CaseVersion + 1, store.LeaseClaims[1].ExpectedVersion);
        Assert.Equal(operationKey, estimate.OperationKey);
        Assert.NotNull(estimate.Lines);
        Assert.Equal(6, estimate.Lines!.Count);
        Assert.Equal(620.20m, estimate.Lines.Single(line => line.Description == "FRONT BUMPER" && line.Type == "new_part").Price);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate={store.LastCreatedEstimateId:D}");
        Assert.Contains(CaseWorkspaceLabels.EstimateImport.Imported, afterHtml, StringComparison.Ordinal);
        Assert.All(store.CurrentDraft!.Lines, line => Assert.Null(line.ConfirmedBy));
        Assert.NotNull(store.ActiveLease);
        Assert.Contains("value=\"lease-3\"", afterHtml, StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectingTheSameEstimateAgainReusesTheStoredSourceAndDraft()
    {
        var caseId = Guid.NewGuid();
        var fixture = AudatexEstimateFixture.Build();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var imported = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), fixture));
        Assert.Equal(HttpStatusCode.Redirect, imported.StatusCode);
        var estimateId = store.LastCreatedEstimateId;
        var importedVersion = store.WorkflowVersion;
        html = await GetHtmlAsync(client, imported.Headers.Location!.OriginalString);
        Assert.Contains("value=\"lease-3\"", html, StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", html, StringComparison.Ordinal);
        Assert.Equal(3, store.LeaseClaims.Count); // Read mode claimed automatically, then both writes renewed authority.
        var activeLease = store.ActiveLease;

        using var replay = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), fixture,
                editLeaseToken: InputValue(html, "editLeaseToken"),
                expectedVersion: long.Parse(InputValue(html, "expectedVersion"), CultureInfo.InvariantCulture)));

        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Contains(estimateId.ToString("D"), replay.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Single(store.AddedDocuments);
        Assert.Single(store.DocumentCalls);
        Assert.Single(store.SavedEstimates); // No second writer or its history side effects.
        Assert.Equal(importedVersion, store.WorkflowVersion);
        Assert.Same(activeLease, store.ActiveLease);
        Assert.Equal(3, store.LeaseClaims.Count);
        var reloaded = await GetHtmlAsync(client, replay.Headers.Location.OriginalString);
        Assert.Contains("value=\"lease-3\"", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("Recover editing", reloaded, StringComparison.Ordinal);
        Assert.Equal(estimateId, store.CurrentDraft!.SpecificationId);
        Assert.Null(store.CurrentAccepted);
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
        // The import consumed the session; the editor renders inside a new one (v26).
        var editorHtml = await EnterEditModeAsync(
            client, caseId, $"?section=estimate&estimate={draft.SpecificationId:D}");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", AntiforgeryValue(editorHtml)),
            new("id", caseId.ToString("D")),
            new("operationKey", NewOperationKey()),
            new("editLeaseToken", RecordingStores.HeldLeaseToken),
            new("estimateId", draft.SpecificationId.ToString("D")),
            new("expectedVersion", store.WorkflowVersion.ToString(CultureInfo.InvariantCulture)),
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
            $"/Cases/{caseId:D}?handler=Save&section=estimate",
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
            new("expectedVersion", store.WorkflowVersion.ToString(CultureInfo.InvariantCulture)),
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
            $"/Cases/{caseId:D}?handler=Save&section=estimate",
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

    [Fact]
    public async Task ImportFromReadModeAcquiresTheCaseLeaseAndCreatesTheDraftImmediately()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        var operationKey = NewOperationKey();
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, operationKey, AudatexEstimateFixture.Build()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(store.AddedDocuments);
        Assert.Single(store.SavedEstimates);
        Assert.Equal(3, store.LeaseClaims.Count);
        Assert.Equal(RecordingStores.CaseVersion, store.LeaseClaims[0].ExpectedVersion);
        Assert.Equal(RecordingStores.CaseVersion + 1, store.LeaseClaims[1].ExpectedVersion);
        Assert.Equal(RecordingStores.CaseVersion + 2, store.LeaseClaims[2].ExpectedVersion);
        Assert.Equal("lease-1", store.AddedDocuments[0].EditLeaseToken);
        Assert.Equal("lease-2", store.SavedEstimates[0].EditLeaseToken);

        var afterHtml = await GetHtmlAsync(client, response.Headers.Location!.OriginalString);
        Assert.Contains(CaseWorkspaceLabels.EstimateImport.Imported, afterHtml, StringComparison.Ordinal);
        Assert.Contains("FRONT BUMPER", afterHtml, StringComparison.Ordinal);
        Assert.Contains("data-estimate-line", afterHtml, StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", afterHtml, StringComparison.Ordinal);
        Assert.Contains("value=\"lease-3\"", afterHtml, StringComparison.Ordinal);
        var commitAttribute = Regex.Match(afterHtml, "data-editor-commit=\"(?<value>[^\"]*)\"", RegexOptions.CultureInvariant);
        Assert.True(commitAttribute.Success, "The successful in-place import should acknowledge its Case command.");
        using var commit = JsonDocument.Parse(WebUtility.HtmlDecode(commitAttribute.Groups["value"].Value));
        Assert.Equal("case-estimate-import-form", commit.RootElement.GetProperty("editor").GetString());
        Assert.Equal(operationKey, commit.RootElement.GetProperty("operationKey").GetString());
        Assert.Equal(RecordingStores.CaseVersion, commit.RootElement.GetProperty("expectedVersion").GetInt64());
        Assert.Equal(RecordingStores.CaseVersion + 2, commit.RootElement.GetProperty("version").GetInt64());
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
    public async Task ARejectedParseKeepsItsRetainedSourceWithoutPartialRowsAndNamesTheReason()
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
        Assert.Single(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Equal(2, store.LeaseClaims.Count); // The stored source consumes the first lease before parsing.
        Assert.Contains(store.RetainedDocuments, file =>
            file.Version.FileName == "estimate.pdf"
            && file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("do not add up to the document", afterHtml, StringComparison.Ordinal);
        Assert.Contains("nothing was imported", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedSourceStorageDoesNotReachTheCanonicalImporter()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId) { FailDocumentStorage = true };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await EnterEditModeAsync(client, caseId);
        var form = ImportForm(
            AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build(),
            editLeaseToken: InputValue(html, "editLeaseToken"));

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(store.DocumentCalls);
        Assert.Empty(store.AddedDocuments);
        Assert.Equal(0, store.DocumentMetadataReads);
        Assert.Empty(store.SavedEstimates);
        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("source could not be retained", afterHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AReplacementLeaseConflictAfterStorageLeavesNoDraft()
    {
        var caseId = Guid.NewGuid();
        var fixture = AudatexEstimateFixture.Build();
        var store = new RecordingStores(caseId) { FailLeaseClaimAt = 2 };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await EnterEditModeAsync(client, caseId);
        var form = ImportForm(
            AntiforgeryValue(html), caseId, NewOperationKey(), fixture,
            editLeaseToken: InputValue(html, "editLeaseToken"));

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(store.AddedDocuments);
        Assert.Equal(2, store.LeaseClaims.Count);
        Assert.Equal(0, store.DocumentMetadataReads);
        Assert.Empty(store.SavedEstimates);
        Assert.Contains(store.RetainedDocuments, file =>
            file.Version.FileName == "estimate.pdf"
            && file.Version.CustodyStatus == DocumentCustodyStatus.Confirmed);
        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("source was retained", afterHtml, StringComparison.OrdinalIgnoreCase);

        using var retryForm = ImportForm(AntiforgeryValue(afterHtml), caseId, NewOperationKey(),
            fixture,
            expectedVersion: long.Parse(InputValue(afterHtml, "expectedVersion"), CultureInfo.InvariantCulture));
        using var retry = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate", retryForm);

        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        Assert.Single(store.DocumentCalls); // The retained source is reused instead of another Box write.
        Assert.Single(store.AddedDocuments);
        Assert.Single(store.SavedEstimates);
        Assert.Equal(RecordingStores.CaseVersion + 2, store.WorkflowVersion);
    }

    [Fact]
    public async Task AValidJsonEstimateAboveTenMibImportsWithinTheCoreBound()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        var content = Encoding.UTF8.GetBytes(
            "{\"schema\":\"pegasus-estimate/1\",\"sourceVersion\":\"large-json\"," +
            "\"lines\":[{\"operation\":\"Replace\",\"description\":\"Front bumper\",\"price\":120.00}]}"
            .PadRight(10 * 1024 * 1024 + 1024, ' '));
        Assert.InRange(content.Length, 10 * 1024 * 1024 + 1, ImportRawEstimate.MaximumDocumentBytes);

        using var form = ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), content,
            fileName: "estimate.json", mediaType: "application/json");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(content.Length, Assert.Single(store.AddedDocuments).Content.Length);
        Assert.Equal(RepairSpecificationSourceRoute.Json, Assert.Single(store.SavedEstimates).Source.Route);
    }

    [Fact]
    public async Task UnsupportedEmptyOversizedAndMultipleFilesAreRejectedBeforeLeaseOrStorage()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        var antiforgery = AntiforgeryValue(html);

        using (var unsupported = ImportForm(antiforgery, caseId, NewOperationKey(), [1, 2, 3], fileName: "estimate.exe"))
        using (var response = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", unsupported))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("PDF, XML or JSON", html, StringComparison.Ordinal);

        using (var empty = ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), [], fileName: "empty.pdf"))
        using (var response = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", empty))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("non-empty", html, StringComparison.OrdinalIgnoreCase);

        var tooLarge = new byte[ImportRawEstimate.MaximumDocumentBytes + 1];
        using (var oversized = ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), tooLarge))
        using (var response = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", oversized))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("32 MiB", html, StringComparison.Ordinal);

        using (var multiple = ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), [1, 2, 3]))
        {
            var second = new ByteArrayContent([4, 5, 6]);
            second.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            multiple.Add(second, "anotherFile", "second.pdf");
            using var response = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", multiple);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        Assert.Empty(store.DocumentCalls);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Empty(store.LeaseClaims);
    }

    [Fact]
    public async Task AStaleVersionOrSuppliedInvalidLeaseDoesNotAcquireAuthority()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");

        using (var stale = ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build(),
            expectedVersion: RecordingStores.CaseVersion - 1))
        using (var response = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", stale))
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }
        Assert.Empty(store.DocumentCalls);
        Assert.Empty(store.LeaseClaims);

        using var invalidToken = ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build(),
            editLeaseToken: "expired-token");
        using var refused = await client.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", invalidToken);

        Assert.Equal(HttpStatusCode.Redirect, refused.StatusCode);
        // The import now takes its authority before it retains anything, so an
        // expired token writes nothing to the Case at all, as a stale version does.
        Assert.Empty(store.DocumentCalls);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Empty(store.LeaseClaims);
    }

    [Fact]
    public async Task ArchivedAndReadOnlyCasesCannotStoreAnEstimate()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var engineerClient = CreateEngineerClient(factory);
        store.CaseState = CaseLifecycleState.PostReportComplete;
        store.AssessmentState = CaseLifecycleState.PostReportComplete;
        var readOnlyHtml = await GetHtmlAsync(engineerClient, $"/Cases/{caseId:D}?section=estimate");
        using (var readOnlyForm = ImportForm(AntiforgeryValue(readOnlyHtml), caseId, NewOperationKey(), AudatexEstimateFixture.Build()))
        using (var readOnlyResponse = await engineerClient.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", readOnlyForm))
        {
            Assert.Equal(HttpStatusCode.Redirect, readOnlyResponse.StatusCode);
        }
        Assert.Empty(store.DocumentCalls);

        store.CaseState = CaseLifecycleState.Review;
        store.AssessmentState = CaseLifecycleState.ReportPreparation;
        store.CaseArchive = new(DateTimeOffset.UtcNow,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]), "Test archive");
        var archivedHtml = await GetHtmlAsync(engineerClient, $"/Cases/{caseId:D}?section=estimate");
        using var archivedForm = ImportForm(AntiforgeryValue(archivedHtml), caseId, NewOperationKey(), AudatexEstimateFixture.Build());
        using var archivedResponse = await engineerClient.PostAsync($"/Cases/{caseId:D}?handler=ImportEstimate", archivedForm);

        Assert.Equal(HttpStatusCode.Redirect, archivedResponse.StatusCode);
        Assert.Empty(store.DocumentCalls);
        Assert.Empty(store.AddedDocuments);
        Assert.Empty(store.SavedEstimates);
        Assert.Empty(store.LeaseClaims);
    }

    [Fact]
    public async Task ReplayingAnUploadOperationContinuesImportWithoutDuplicatingItsSource()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await EnterEditModeAsync(client, caseId);
        var operationKey = NewOperationKey();
        var token = InputValue(html, "editLeaseToken");
        var fixture = AudatexEstimateFixture.Build();

        using var firstForm = ImportForm(
            AntiforgeryValue(html), caseId, operationKey, fixture,
            editLeaseToken: token);
        using var first = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate", firstForm);
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var estimateId = store.LastCreatedEstimateId;
        var completedVersion = store.WorkflowVersion;
        html = await GetHtmlAsync(client, first.Headers.Location!.OriginalString);

        using var replayForm = ImportForm(
            AntiforgeryValue(html), caseId, operationKey, fixture,
            editLeaseToken: InputValue(html, "editLeaseToken"),
            expectedVersion: long.Parse(InputValue(html, "expectedVersion"), CultureInfo.InvariantCulture));
        using var replay = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate", replayForm);

        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Contains(estimateId.ToString("D"), replay.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(2, store.DocumentCalls.Count);
        Assert.Single(store.AddedDocuments);
        Assert.Single(store.SavedEstimates);
        Assert.Equal(2, store.DocumentMetadataReads);
        Assert.Equal(completedVersion, store.WorkflowVersion);
        Assert.Equal(3, store.LeaseClaims.Count);
        Assert.Equal("lease-3", store.CurrentLeaseToken);
    }

    [Fact]
    public async Task ASourceReplayOperationKeyRejectsDifferentBytesBeforeRetainingAnotherDocument()
    {
        var caseId = Guid.NewGuid();
        var fixture = AudatexEstimateFixture.Build();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var first = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), fixture));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        html = await GetHtmlAsync(client, first.Headers.Location!.OriginalString);
        var replayOperationKey = NewOperationKey();
        using var replay = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, replayOperationKey, fixture,
                editLeaseToken: InputValue(html, "editLeaseToken"),
                expectedVersion: long.Parse(InputValue(html, "expectedVersion"), CultureInfo.InvariantCulture)));
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Single(store.SourceReplayBindings);
        var documentCallCount = store.DocumentCalls.Count;
        var documentCount = store.AddedDocuments.Count;
        var savedCount = store.SavedEstimates.Count;
        var workflowVersion = store.WorkflowVersion;

        html = await GetHtmlAsync(client, replay.Headers.Location!.OriginalString);
        var changed = fixture.Concat(new byte[] { 0x01 }).ToArray();
        using var conflict = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, replayOperationKey, changed,
                editLeaseToken: InputValue(html, "editLeaseToken"),
                expectedVersion: long.Parse(InputValue(html, "expectedVersion"), CultureInfo.InvariantCulture)));

        Assert.Equal(HttpStatusCode.Redirect, conflict.StatusCode);
        Assert.Equal(documentCallCount, store.DocumentCalls.Count);
        Assert.Equal(documentCount, store.AddedDocuments.Count);
        Assert.Equal(savedCount, store.SavedEstimates.Count);
        Assert.Equal(workflowVersion, store.WorkflowVersion);
        Assert.Single(store.SourceReplayBindings);
    }

    [Fact]
    public async Task ASourceReplayRequiresCurrentEditAuthorityBeforeAcknowledgingReplay()
    {
        var caseId = Guid.NewGuid();
        var fixture = AudatexEstimateFixture.Build();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var first = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), fixture));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);

        html = await GetHtmlAsync(client, first.Headers.Location!.OriginalString);
        var replayOperationKey = NewOperationKey();
        using var replay = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, replayOperationKey, fixture,
                editLeaseToken: InputValue(html, "editLeaseToken"),
                expectedVersion: long.Parse(InputValue(html, "expectedVersion"), CultureInfo.InvariantCulture)));
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Single(store.SourceReplayBindings);

        var estimateId = store.LastCreatedEstimateId;
        var documentCalls = store.DocumentCalls.Count;
        var savedEstimates = store.SavedEstimates.Count;
        var workflowVersion = store.WorkflowVersion;
        var leaseClaims = store.LeaseClaims.Count;

        html = await GetHtmlAsync(client, replay.Headers.Location!.OriginalString);
        using var stale = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, replayOperationKey, fixture,
                editLeaseToken: "stale-lease-token",
                expectedVersion: workflowVersion));
        Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);

        var refusedHtml = await GetHtmlAsync(client, stale.Headers.Location!.OriginalString);
        Assert.Contains("The Case cannot be edited right now.", refusedHtml, StringComparison.Ordinal);
        Assert.DoesNotContain(CaseWorkspaceLabels.EstimateImport.Imported, refusedHtml, StringComparison.Ordinal);
        Assert.Equal(documentCalls, store.DocumentCalls.Count);
        Assert.Equal(savedEstimates, store.SavedEstimates.Count);
        Assert.Equal(workflowVersion, store.WorkflowVersion);
        Assert.Single(store.SourceReplayBindings);

        using var reacquired = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(refusedHtml), caseId, replayOperationKey, fixture,
                expectedVersion: workflowVersion));
        Assert.Equal(HttpStatusCode.Redirect, reacquired.StatusCode);
        Assert.Contains(estimateId.ToString("D"), reacquired.Headers.Location!.OriginalString, StringComparison.Ordinal);
        Assert.Equal(leaseClaims + 1, store.LeaseClaims.Count);
        Assert.Equal("lease-4", store.CurrentLeaseToken);
        Assert.Equal(documentCalls, store.DocumentCalls.Count);
        Assert.Equal(savedEstimates, store.SavedEstimates.Count);
        Assert.Equal(workflowVersion, store.WorkflowVersion);
    }

    [Fact]
    public async Task ACallerSuppliedSourceLabelCannotOverrideTheDetectedDocumentFormat()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.DoesNotContain("name=\"source\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"import-name\"", html, StringComparison.Ordinal);
        // Unbound old form data is not format authority.
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build(), source: "other"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(store.AddedDocuments);
        Assert.Equal(RepairSpecificationSourceRoute.AudatexPdf, Assert.Single(store.SavedEstimates).Source.Route);
    }

    [Fact]
    public async Task AUserCanImport()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory, StaffRole.User);

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ImportEstimate&section=estimate",
            ImportForm(AntiforgeryValue(html), caseId, NewOperationKey(), AudatexEstimateFixture.Build()));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Single(store.AddedDocuments);
        Assert.True(Assert.Single(store.SavedEstimates).Actor.IsInRole(StaffRole.User));
    }

    [Fact]
    public async Task ImportFormIsAvailableInReadAndEditModeWithAccessibleNativeFallback()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var readHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        var readForm = Regex.Match(readHtml,
            "<form[^>]*id=\"case-estimate-import-form\"[^>]*>(?<body>.*?)</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(readForm.Success, "Read mode should render the direct upload form.");
        Assert.Contains("handler=ImportEstimate", readForm.Value, StringComparison.Ordinal);
        Assert.Contains("enctype=\"multipart/form-data\"", readForm.Value, StringComparison.Ordinal);
        Assert.Contains("name=\"estimateFile\"", readForm.Value, StringComparison.Ordinal);
        Assert.Contains("name=\"editLeaseToken\" value=\"\"", readForm.Value, StringComparison.Ordinal);
        Assert.Contains("data-estimate-import-fallback", readForm.Value, StringComparison.Ordinal);
        Assert.Contains("data-estimate-import-picker", readForm.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("Complete import", readHtml, StringComparison.Ordinal);
        Assert.Contains("Drop estimate to import", readHtml, StringComparison.Ordinal);

        var html = await EnterEditModeAsync(client, caseId);
        var editForm = Regex.Match(html,
            "<form[^>]*id=\"case-estimate-import-form\"[^>]*>(?<body>.*?)</form>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(editForm.Success, "Edit mode should render the direct upload form.");
        Assert.Contains("handler=ImportEstimate", editForm.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"\"", editForm.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UseEstimateRecordsTheUsersAcceptance()
    {
        var caseId = Guid.NewGuid();
        var draft = DraftSpecification(caseId);
        var store = new RecordingStores(caseId) { CurrentDraft = draft };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory, StaffRole.User);

        var html = await EnterEditModeAsync(client, caseId, $"?section=estimate&estimate={draft.SpecificationId:D}");
        Assert.Contains("data-estimate-use", html, StringComparison.Ordinal);
        Assert.Contains("Use repair spec", html, StringComparison.Ordinal);
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
        Assert.True(use.Actor.IsInRole(StaffRole.User));
        Assert.Equal(draft.SpecificationId, use.EstimateId);
        Assert.Equal(RecordingStores.CaseVersion, use.ExpectedVersion);
        Assert.Equal(RecordingStores.HeldLeaseToken, use.EditLeaseToken);
        // v25 decision F: an immediate post keeps the session — the page's own
        // entry claim, then the reclaim after the acceptance consumed it.
        Assert.Equal(2, store.LeaseClaims.Count);
        Assert.NotNull(store.ActiveLease);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}");
        Assert.Contains(
            "The repair spec is now the case's current repair spec.",
            WebUtility.HtmlDecode(afterHtml),
            StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheCaseSaveKeepsSubmittedVersionAndIntentAcrossIdenticalPosts()
    {
        var caseId = Guid.NewGuid();
        var store = new RecordingStores(caseId);
        var existing = DraftSpecification(caseId);
        store.CurrentDraft = existing;
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate&estimate={existing.SpecificationId:D}");
        Assert.Contains($"name=\"expectedVersion\" value=\"{RecordingStores.CaseVersion}\"", html, StringComparison.Ordinal);
        var fields = NewEnumerable(
            ("__RequestVerificationToken", AntiforgeryValue(html)),
            ("operationKey", NewOperationKey()), ("editLeaseToken", RecordingStores.HeldLeaseToken),
            ("estimateId", existing.SpecificationId.ToString("D")), ("lineId", existing.Lines[0].Id.ToString("D")),
            ("expectedVersion", "3"), ("estimateName", "Repairer"),
            ("lineOperation", "Repair"), ("lineDescription", "Repair door"), ("lineLabourHours", "2")).ToArray();
        using var first = await client.PostAsync($"/Cases/{caseId:D}?handler=Save&section=estimate", new FormUrlEncodedContent(fields));
        using var second = await client.PostAsync($"/Cases/{caseId:D}?handler=Save&section=estimate", new FormUrlEncodedContent(fields));
        // The stale version is refused both times, and the save is the same
        // request both times: the page never rewrites what the form posted.
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, second.StatusCode);
        Assert.Equal(2, store.SubmittedEstimates.Count);
        Assert.All(store.SubmittedEstimates, request => Assert.Equal(3, request.ExpectedVersion));
        Assert.Equal(JsonSerializer.Serialize(store.SubmittedEstimates[0]), JsonSerializer.Serialize(store.SubmittedEstimates[1]));
        Assert.Empty(store.SavedEstimates);
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
        Assert.Contains("New repair spec", html, StringComparison.Ordinal);
        var operationKey = NewOperationKey();

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save&section=estimate",
            new FormUrlEncodedContent(
                NewEnumerable(
                    ("__RequestVerificationToken", AntiforgeryValue(html)),
                    ("id", caseId.ToString("D")),
                    ("operationKey", operationKey),
                    ("editLeaseToken", RecordingStores.HeldLeaseToken),
                    ("expectedVersion", RecordingStores.CaseVersion.ToString(CultureInfo.InvariantCulture)),
                    ("estimateName", "Repair alternative"),
                    ("estimateLabourRate", "48.50"),
                    ("estimateRegionalUplift", "true"),
                    ("estimateRegionalUplift", "false"),
                    ("estimateOtherCosts", "75.00"),
                    ("estimateVatPercent", "20"),
                    ("lineOperation", "Replace"),
                    ("lineDescription", "Front bumper"),
                    ("linePartNumber", "51 11 8 067"),
                    ("lineQuantity", "1"),
                    ("lineLabourHours", "2.0"),
                    ("linePaintHours", "1.5"),
                    ("linePartPounds", "620.20"),
                    ("lineMaterials", "120.00"),
                    ("lineOperation", string.Empty),
                    ("lineDescription", string.Empty),
                    ("linePartNumber", string.Empty),
                    ("lineQuantity", string.Empty),
                    ("lineLabourHours", string.Empty),
                    ("linePaintHours", string.Empty),
                    ("linePartPounds", string.Empty),
                    ("lineMaterials", string.Empty))));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = Assert.Single(store.SavedEstimates);
        Assert.Null(saved.EstimateId);
        Assert.Equal("Repair alternative", saved.Details.Name);
        Assert.True(saved.Details.RegionalUplift);
        Assert.Equal(48.50m, saved.Details.LabourRate);
        // B04: one hourly rate prices panel and paint hours alike, so the
        // editor neither offers nor carries a second paint rate.
        Assert.DoesNotContain("estimatePaintLabourRate", html, StringComparison.Ordinal);
        Assert.Equal(75.00m, saved.Details.OtherCosts);
        Assert.Equal(20m, saved.Details.VatPercent);
        Assert.Equal(RepairSpecificationSourceRoute.Manual, saved.Source.Route);
        var line = Assert.Single(saved.Lines!);
        Assert.Equal("new_part", line.Type);
        Assert.Equal("Front bumper", line.Description);
        // Materials sit on the line (v28 P48).
        Assert.Equal(120.00m, line.Materials);
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
            new("expectedVersion", RecordingStores.CaseVersion.ToString(CultureInfo.InvariantCulture)),
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
            $"/Cases/{caseId:D}?handler=Save&section=estimate",
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
                $"id=\"{field}\" name=\"{field}\" form=\"case-edit-form\" type=\"checkbox\" value=\"true\"",
                reloaded,
                StringComparison.Ordinal);
            Assert.Contains(
                $"<input type=\"hidden\" name=\"{field}\" value=\"false\" form=\"case-edit-form\" />",
                reloaded,
                StringComparison.Ordinal);
        }

        // The percentages round-trip as percentages, not as the fractions Core
        // validates.
        Assert.Contains(
            "name=\"estimateDiscountParts\" form=\"case-edit-form\" type=\"number\" min=\"0\" max=\"100\" step=\"0.01\" inputmode=\"decimal\" value=\"12.5\"",
            reloaded,
            StringComparison.Ordinal);
        Assert.Contains(
            "name=\"estimateDiscountOverall\" form=\"case-edit-form\" type=\"number\" min=\"0\" max=\"100\" step=\"0.01\" inputmode=\"decimal\" value=\"2.5\"",
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

        var html = await EnterEditModeAsync(client, caseId, "?section=estimate&estimate=new");
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
            new("expectedVersion", RecordingStores.CaseVersion.ToString(CultureInfo.InvariantCulture)),
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
            $"/Cases/{caseId:D}?handler=Save&section=estimate",
            new FormUrlEncodedContent(fields));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = Assert.Single(store.SavedEstimates);
        Assert.Equal(RepairerVatStatus.Unknown, saved.Details.Vat!.RepairerStatus);
        Assert.Equal(
            EstimateVatCategories.Parts | EstimateVatCategories.Materials,
            saved.Details.Vat.Categories);
        Assert.True(saved.Details.Vat.CategoriesOverridden);
        Assert.False(saved.Details.Vat.TreatmentPending);
        Assert.Equal(EstimateDiscounts.None, saved.Details.Discounts);

        var reloaded = await GetHtmlAsync(
            client, $"/Cases/{caseId:D}?section=estimate&estimate={store.LastCreatedEstimateId:D}");
        Assert.Contains("handler=SetCurrentEstimate", reloaded, StringComparison.Ordinal);
    }

    /// <summary>
    /// v28 P10: an unrecorded repairer VAT status no longer gates Use repair
    /// spec. The control is offered; the totals simply carry no VAT.
    /// </summary>
    [Fact]
    public async Task AnUnrecordedRepairerVatStatusDoesNotGateUseRepairSpec()
    {
        var caseId = Guid.NewGuid();
        var seeded = DraftSpecification(caseId);
        var draft = seeded with { Details = seeded.Details with { Vat = null } };
        var store = new RecordingStores(caseId) { CurrentDraft = draft };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        Assert.True(draft.Details.VatPolicy.TreatmentPending);
        var html = await EnterEditModeAsync(
            client, caseId, $"?section=estimate&estimate={draft.SpecificationId:D}");

        Assert.DoesNotContain("data-estimate-use-condition", html, StringComparison.Ordinal);
        Assert.Contains("handler=SetCurrentEstimate", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompareEstimatesShowsCanonicalTotalsForEveryPersistedEstimate()
    {
        var caseId = Guid.NewGuid();
        var draftBase = DraftSpecification(caseId);
        var draft = draftBase with
        {
            Details = draftBase.Details with { Name = "Repairer draft" },
        };
        var currentBase = DraftSpecification(caseId);
        var current = currentBase with
        {
            SpecificationId = Guid.NewGuid(),
            Version = 2,
            State = RepairSpecificationState.Accepted,
            Lines = [currentBase.Lines.Single() with { Price = 100.01m }],
            Details = currentBase.Details with { Name = "Engineer current" },
            IsCurrent = true,
        };
        current = current with
        {
            RecordedTotals = EstimateTotals.Compute(current),
            Lines = [current.Lines.Single() with { Price = 999m }],
        };
        var store = new RecordingStores(caseId)
        {
            CurrentDraft = draft,
            CurrentAccepted = current,
        };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}&dialog=compare-estimates");

        var start = html.IndexOf("data-dialog=\"compare-estimates-dialog\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var dialog = html[start..end];
        Assert.Contains("<h2 id=\"compare-estimates-dialog-title\" tabindex=\"-1\">Compare repair specs</h2>", dialog, StringComparison.Ordinal);
        Assert.Contains("Repairer draft", dialog, StringComparison.Ordinal);
        Assert.Contains("Engineer current", dialog, StringComparison.Ordinal);
        // The state reads as a chip: Current for the accepted current estimate.
        Assert.Contains(">Draft</span>", dialog, StringComparison.Ordinal);
        Assert.Contains(">Current</span>", dialog, StringComparison.Ordinal);
        Assert.Contains("&#xA3;620.20", dialog, StringComparison.Ordinal);
        Assert.Contains("&#xA3;124.04", dialog, StringComparison.Ordinal);
        Assert.Contains("&#xA3;744.24", dialog, StringComparison.Ordinal);
        Assert.Contains("&#xA3;100.01", dialog, StringComparison.Ordinal);
        Assert.Contains("&#xA3;20.00", dialog, StringComparison.Ordinal);
        Assert.Contains("&#xA3;120.01", dialog, StringComparison.Ordinal);
        Assert.DoesNotContain("Savings", dialog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Difference", dialog, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompareEstimatesIsUnavailableUntilTwoEstimatesExist()
    {
        var caseId = Guid.NewGuid();
        var draft = DraftSpecification(caseId);
        var store = new RecordingStores(caseId) { CurrentDraft = draft };
        using var baseFactory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var factory = Compose(baseFactory, store);
        using var client = CreateEngineerClient(factory);

        var html = await GetHtmlAsync(
            client,
            $"/Cases/{caseId:D}?section=estimate&estimate={draft.SpecificationId:D}&dialog=compare-estimates");

        Assert.DoesNotContain("compare-estimates-dialog", html, StringComparison.Ordinal);
        // v28 P9: Compare stays findable under More, greyed out until a second spec exists.
        Assert.Contains("disabled data-estimate-compare", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dialog=compare-estimates", html, StringComparison.Ordinal);
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

        var html = await EnterEditModeAsync(client, caseId);
        Assert.Contains("data-estimate-duplicate", html, StringComparison.Ordinal);
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
        // The entry claim and the reclaim that keeps the session (v25 F).
        Assert.Equal(2, store.LeaseClaims.Count);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=estimate");
        Assert.Contains("The repair spec was duplicated.", afterHtml, StringComparison.Ordinal);
        Assert.Contains("data-case-editing=\"true\"", afterHtml, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Glass's estimate review defect, through the real editor: the save used to
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
            new("expectedVersion", RecordingStores.CaseVersion.ToString(CultureInfo.InvariantCulture)),
            new("estimateName", details.Name),
            new("estimateLabourRate", details.LabourRate!.Value.ToString(CultureInfo.InvariantCulture)),
            new("estimateOtherCosts", details.OtherCosts!.Value.ToString(CultureInfo.InvariantCulture)),
            new("estimateVatPercent", details.VatPercent.ToString(CultureInfo.InvariantCulture)),
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
            fields.Add(new("lineMaterials", line.Materials?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
        }

        using var saveResponse = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save&section=estimate",
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
            new("Imported estimate", 52.50m, 110m, 20m,
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

    internal static RepairSpecificationVersion DraftSpecification(Guid caseId) => new(
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
        new("Estimate 1", null, null, 20m,
            Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)));

    internal static WebApplicationFactory<Program> Compose(
        IntakeWebApplicationFactory baseFactory, RecordingStores store) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetCaseVehicleSection>();
                services.RemoveAll<IGetCaseValuationSection>();
                services.RemoveAll<IGetCaseNotesSection>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<IRepairSpecificationStore>();
                services.RemoveAll<IScaleRepairSpecification>();
                services.RemoveAll<ISaveCaseWorkspace>();
                services.RemoveAll<IRepairSpecificationSnapshotStore>();
                services.RemoveAll<IAddCaseDocument>();
                services.RemoveAll<IGetCaseDocumentMetadata>();
                services.RemoveAll<IReadLogicalDocumentVersion>();
                services.RemoveAll<IAcquireCaseEditLease>();
                services.RemoveAll<IListCaseEstimates>();
                services.RemoveAll<ISaveEstimate>();
                services.RemoveAll<IDuplicateEstimate>();
                services.RemoveAll<IDiscardEstimate>();
                services.RemoveAll<ISetCurrentEstimate>();
                services.AddSingleton<IGetCase>(store);
                services.AddSingleton<IGetCasePageFrame>(store);
                services.AddSingleton<IGetCaseVehicleSection>(store);
                services.AddSingleton<IGetCaseValuationSection>(store);
                services.AddSingleton<IGetCaseNotesSection>(store);
                services.AddSingleton<IGetAssessmentAccess>(new MutableAssessmentAccess(store));
                services.AddSingleton<IGetAssessmentWorkspace>(store);
                services.AddSingleton<IRepairSpecificationStore>(store);
                services.AddSingleton<IScaleRepairSpecification>(store);
                services.AddSingleton<ISaveCaseWorkspace>(provider =>
                {
                    store.Clock = provider.GetRequiredService<TimeProvider>();
                    return store;
                });
                services.AddSingleton<IRepairSpecificationSnapshotStore>(store);
                services.AddSingleton<IAddCaseDocument>(store);
                services.AddSingleton<IGetCaseDocumentMetadata>(store);
                services.AddSingleton<IReadLogicalDocumentVersion>(store);
                services.AddSingleton<IAcquireCaseEditLease>(store);
                services.AddSingleton<IListCaseEstimates>(store);
                services.AddSingleton<ISaveEstimate>(provider =>
                {
                    store.Clock = provider.GetRequiredService<TimeProvider>();
                    return store;
                });
                services.AddSingleton<IDuplicateEstimate>(store);
                services.AddSingleton<IDiscardEstimate>(store);
                services.AddSingleton<ISetCurrentEstimate>(store);
            }));

    internal static HttpClient CreateEngineerClient(
        WebApplicationFactory<Program> factory,
        StaffRole role = StaffRole.Engineer)
    {
        var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role.ToString());
        return client;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139"),
        });

    /// <summary>
    /// Pass the lease token rendered by an edit page, or null when testing a read-mode upload.
    /// </summary>
    private static MultipartFormDataContent ImportForm(
        string antiforgeryToken,
        Guid caseId,
        string operationKey,
        byte[] pdfBytes,
        string? editLeaseToken = null,
        long expectedVersion = RecordingStores.CaseVersion,
        string source = "audatex-pdf",
        string fileName = "estimate.pdf",
        string mediaType = "application/pdf")
    {
        var file = new ByteArrayContent(pdfBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        var form = new MultipartFormDataContent
        {
            { new StringContent(antiforgeryToken), "__RequestVerificationToken" },
            { new StringContent(caseId.ToString("D")), "id" },
            { new StringContent(operationKey), "operationKey" },
            { new StringContent(expectedVersion.ToString(CultureInfo.InvariantCulture)), "expectedVersion" },
            { new StringContent(source), "source" },
            { file, "estimateFile", fileName },
        };
        if (editLeaseToken is not null)
        {
            form.Add(new StringContent(editLeaseToken), "editLeaseToken");
        }

        return form;
    }

    internal static FormUrlEncodedContent Form(
        string antiforgeryToken, params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        fields["__RequestVerificationToken"] = antiforgeryToken;
        return new(fields);
    }

    /// <summary>A form that preserves repeated keys (the editor's line rows).</summary>
    internal static IEnumerable<KeyValuePair<string, string>> NewEnumerable(
        params (string Name, string Value)[] values) =>
        values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value));

    internal static string NewOperationKey() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// v26: the Estimate section's controls render inside the page-wide edit
    /// session, so a test that reads them enters it the way the operator does
    /// — the ribbon's Edit Case claim — and reads the page again.
    /// </summary>
    internal static async Task<string> EnterEditModeAsync(
        HttpClient client, Guid caseId, string query = "?section=estimate")
    {
        var initial = await GetHtmlAsync(client, $"/Cases/{caseId:D}{query}");
        using var claim = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", caseId.ToString("D")),
                ("expectedVersion", InputValue(initial, "expectedVersion")),
                ("operationKey", InputValue(initial, "operationKey")),
                ("section", "estimate")));
        Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}{query}");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        return editing;
    }

    /// <summary>The first rendered input of that name, as the browser would post it.</summary>
    internal static string InputValue(string html, string name)
    {
        var tag = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(tag.Success, $"The page must render an input named {name}.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, $"The input {name} must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

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

    internal static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    internal static string AntiforgeryValue(string html)
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

    private sealed class MutableAssessmentAccess(RecordingStores store) : IGetAssessmentAccess
    {
        public Task<AssessmentAccessState?> ExecuteAsync(
            GetAssessmentAccessQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AssessmentAccessState?>(new(store.AssessmentState));
    }

    /// <summary>
    /// One recording fake for the substituted stores, so the tests can assert
    /// exactly what the page handed to each one.
    /// </summary>
    internal sealed class RecordingStores(Guid caseId, decimal? engineerValue = null, decimal? contractSum = null)
        : IGetCase, IGetCasePageFrame, IGetCaseVehicleSection, IGetCaseValuationSection,
          IGetCaseNotesSection, IGetAssessmentWorkspace, IRepairSpecificationStore, IAddCaseDocument,
          IGetCaseDocumentMetadata, IReadLogicalDocumentVersion,
          IAcquireCaseEditLease, IListCaseEstimates, ISaveEstimate, IDuplicateEstimate,
          IDiscardEstimate, ISetCurrentEstimate, IScaleRepairSpecification,
          IRepairSpecificationSnapshotStore, ISaveCaseWorkspace
    {
        public const long CaseVersion = 7;

        /// <summary>
        /// The lease the operator entered edit mode with, which the assessment's forms carry into
        /// each save. Only the import's second half still claims one of its own.
        /// </summary>
        public const string HeldLeaseToken = "lease-1";

        public CaseLifecycleState CaseState { get; set; } = CaseLifecycleState.Review;

        public CaseLifecycleState AssessmentState { get; set; } = CaseLifecycleState.ReportPreparation;

        public CaseArchive? CaseArchive { get; set; }

        private int leaseCounter;
        private int importedMutations;
        private int documentMutations;
        private readonly Dictionary<string, AddCaseDocumentResult> retainedByOperation = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (string Sha256, Guid EstimateId)> sourceReplayBindings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Guid> ordinaryImportOperations = new(StringComparer.Ordinal);
        private readonly Dictionary<Guid, byte[]> retainedBytes = [];
        private string? currentLeaseToken;

        public decimal? EngineerValue { get; } = engineerValue;

        public decimal? ContractSum { get; } = contractSum;
        public long WorkflowVersion => CaseVersion + documentMutations + importedMutations;

        public CaseEditLeaseSnapshot? ActiveLease { get; private set; }

        public string? CurrentLeaseToken => currentLeaseToken;

        public bool FailDocumentStorage { get; set; }

        public int? FailLeaseClaimAt { get; set; }

        public TimeProvider Clock { get; set; } = TimeProvider.System;

        public RepairSpecificationVersion? CurrentDraft { get; set; }

        public RepairSpecificationVersion? CurrentAccepted { get; set; }

        public CaseDataProjection? DataOverride { get; set; }
        public List<AddCaseDocumentCommand> DocumentCalls { get; } = [];
        public List<AddCaseDocumentCommand> AddedDocuments { get; } = [];
        public List<CaseFile> RetainedDocuments { get; } = [];
        public CaseFile? RetainedDocument { get; private set; }
        public int DocumentMetadataReads { get; private set; }

        public List<ClaimCaseEditLeaseRequest> LeaseClaims { get; } = [];

        public List<SaveEstimateRequest> SavedEstimates { get; } = [];
        public List<SaveEstimateRequest> SubmittedEstimates { get; } = [];
        public List<(string OperationKey, string Sha256, Guid EstimateId)> SourceReplayBindings { get; } = [];

        public List<DuplicateEstimateRequest> DuplicatedEstimates { get; } = [];

        public List<DiscardEstimateRequest> DiscardedEstimates { get; } = [];

        public List<SetCurrentEstimateRequest> SetCurrentRequests { get; } = [];

        public List<ScaleRepairSpecificationRequest> ScaleRequests { get; } = [];

        public List<SaveCaseWorkspaceRequest> CaseSaves { get; } = [];

        public List<RepairSpecificationSnapshot> Snapshots { get; } = [];

        public Guid LastCreatedEstimateId { get; private set; }

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
        {
            if (query.CaseId != caseId)
            {
                return Task.FromResult<CaseDetails?>(null);
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseState, null, null,
                null, null, null, null, null, WorkflowVersion);
            workflow = workflow with { Archive = CaseArchive };
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, new DateOnly(2026, 8, 1), "Email", DateTimeOffset.UtcNow);
            var documents = RetainedDocuments
                .GroupBy(file => file.Version.DocumentId)
                .Select(group => new CaseDocument(
                    group.Key,
                    caseId,
                    group.Select(file => file.Occurrence).ToArray(),
                    group.Select(file => file.Version).ToArray()))
                .ToArray();
            CaseDetails details = new(
                summary, workflow, ActiveLease,
                documents,
                null, CaseCustodyState.Pending, [], [])
            {
                Data = DataOverride ?? CreateData(workflow.Version)
            };
            return Task.FromResult<CaseDetails?>(details);
        }

        async Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            return details is null
                ? null
                : new(
                    CreateFrame(details),
                    details.Documents,
                    details.AvailableReportSentEvidence,
                    details.RecordNotes,
                    details.Data!);
        }

        async Task<CaseVehicleSection?> IGetCaseVehicleSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            return details is null
                ? null
                : new(
                    CreateFrame(details),
                    query.AssessmentWorkspace?.Data ?? query.Data ?? details.Data!,
                    null,
                    query.AssessmentWorkspace?.Assessment ?? CreateAssessment(details));
        }

        async Task<CaseValuationSection?> IGetCaseValuationSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            return details is null
                ? null
                : new(
                    CreateFrame(details),
                    query.AssessmentWorkspace?.Data ?? query.Data ?? details.Data!,
                    query.AssessmentWorkspace?.Assessment ?? CreateAssessment(details));
        }

        async Task<CaseNotesSection?> IGetCaseNotesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = await ExecuteAsync(new GetCaseQuery(query.CaseId, query.Actor), cancellationToken);
            return details is null ? null : new(CreateFrame(details), details.History);
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
            return AssessmentWorkspaceTestData.Create(
                details, CreateAssessment(details), CurrentDraft, CurrentAccepted);
        }

        private CaseAssessmentProjection CreateAssessment(CaseDetails details)
        {
            var fields = new List<AssessmentFieldValue>();
            if (EngineerValue is { } value)
            {
                fields.Add(new(
                    AssessmentVocabulary.ValueEngineer,
                    value.ToString(CultureInfo.InvariantCulture),
                    ActorKind.Staff,
                    "engineer-1",
                    DateTimeOffset.UtcNow,
                    "engineer-1",
                    DateTimeOffset.UtcNow));
            }
            if (ContractSum is { } sum)
            {
                fields.Add(new(
                    AssessmentVocabulary.Outcome,
                    "contract_repair",
                    ActorKind.Staff,
                    "engineer-1",
                    DateTimeOffset.UtcNow,
                    "engineer-1",
                    DateTimeOffset.UtcNow));
                fields.Add(new(
                    AssessmentVocabulary.SettlementContractSum,
                    sum.ToString(CultureInfo.InvariantCulture),
                    ActorKind.Staff,
                    "engineer-1",
                    DateTimeOffset.UtcNow,
                    "engineer-1",
                    DateTimeOffset.UtcNow));
            }

            return new(
                caseId,
                details.Summary.Reference,
                details.Workflow.Version,
                details.Workflow.State,
                null,
                fields,
                [],
                new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null));
        }

        private CaseDataProjection CreateData(long version) =>
            AssessmentWorkspaceTestData.Create(new CaseAssessmentProjection(
                caseId,
                "QDOS-2026-00042",
                version,
                CaseLifecycleState.Review,
                null,
                [],
                [],
                new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null))).Data;

        private static CaseSectionFrame CreateFrame(CaseDetails details) =>
            new(details.Summary, details.Workflow, details.ActiveEditLease);

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
        public Task RequireImportAuthorityAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken)
        {
            Assert.Equal(caseId, request.CaseId);
            if (request.ExpectedVersion != WorkflowVersion)
            {
                throw new CaseVersionConflictException(caseId, request.ExpectedVersion, WorkflowVersion);
            }
            if (ActiveLease is null || !string.Equals(currentLeaseToken, request.EditLeaseToken, StringComparison.Ordinal))
            {
                throw new CaseEditLeaseExpiredException(caseId, WorkflowVersion);
            }
            return Task.CompletedTask;
        }

        public Task<EstimateImportResult?> ProbeSourceHashReplayAsync(
            Guid ownerCaseId,
            string operationKey,
            string sourceSha256,
            CancellationToken cancellationToken)
        {
            Assert.Equal(caseId, ownerCaseId);
            if (!sourceReplayBindings.TryGetValue(operationKey, out var binding))
            {
                return Task.FromResult<EstimateImportResult?>(null);
            }

            if (!string.Equals(binding.Sha256, sourceSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new CaseOperationConflictException(caseId, operationKey);
            }

            return Task.FromResult<EstimateImportResult?>(new(binding.EstimateId));
        }

        public Task<EstimateImportResult> BindSourceHashReplayAsync(
            Guid ownerCaseId,
            string operationKey,
            string sourceSha256,
            Guid estimateId,
            ActionActor actor,
            CancellationToken cancellationToken)
        {
            Assert.Equal(caseId, ownerCaseId);
            if (sourceReplayBindings.TryGetValue(operationKey, out var binding))
            {
                if (!string.Equals(binding.Sha256, sourceSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CaseOperationConflictException(caseId, operationKey);
                }

                return Task.FromResult(new EstimateImportResult(binding.EstimateId));
            }

            if (ordinaryImportOperations.TryGetValue(operationKey, out var ordinaryEstimateId))
            {
                if (ordinaryEstimateId != estimateId)
                {
                    throw new CaseOperationConflictException(caseId, operationKey);
                }

                return Task.FromResult(new EstimateImportResult(estimateId));
            }

            sourceReplayBindings.Add(operationKey, (sourceSha256, estimateId));
            SourceReplayBindings.Add((operationKey, sourceSha256, estimateId));
            return Task.FromResult(new EstimateImportResult(estimateId));
        }

        public async Task<RepairSpecificationVersion> SaveImportedEstimateAsync(
            SaveEstimateRequest request, CancellationToken cancellationToken)
        {
            var result = await ExecuteAsync(request, cancellationToken);
            ordinaryImportOperations[request.OperationKey] = result.SpecificationId;
            importedMutations++;
            ActiveLease = null;
            currentLeaseToken = null;
            return result;
        }

        public Task<RepairSpecificationVersion> SaveEstimateAsync(
            SaveEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        /// <summary>
        /// Apply scales the saved spec: the page saved the Case, the spec with
        /// it, before it asked (one Save, 23 September 2026).
        /// </summary>
        public Task<RepairSpecificationVersion> ExecuteAsync(
            ScaleRepairSpecificationRequest request,
            CancellationToken cancellationToken)
        {
            var engineerValue = request.EngineerValue ?? EngineerValue
                ?? throw new InvalidOperationException("No confirmed Engineer's Value is available.");
            request = request with { EngineerValue = engineerValue };
            ScaleRequests.Add(request);
            var edited = CurrentDraft ?? throw new InvalidOperationException("No draft is selected.");
            var result = RepairSpecificationScaling.Scale(
                edited,
                engineerValue * request.TargetPercentOfValue / 100m,
                request.Floors);
            var scaled = edited with
            {
                Details = result.Details,
                Lines = Recorded(new SaveEstimateRequest(
                    caseId, request.ExpectedVersion, request.Actor, request.OperationKey, "Repair spec scaled",
                    request.EditLeaseToken, edited.SpecificationId, result.Details, result.Lines, edited.Source)),
            };
            CurrentDraft = scaled;
            Snapshots.Add(new(
                Guid.NewGuid(), caseId, scaled.SpecificationId, Snapshots.Count + 1,
                RepairSpecificationSnapshotKind.BeforeScaling, "Before scaling", request.Actor.SubjectId,
                DateTimeOffset.UtcNow, edited.Details, edited.Lines,
                EstimateTotals.Compute(edited).Printed.Gross, false));
            Snapshots.Add(new(
                Guid.NewGuid(), caseId, scaled.SpecificationId, Snapshots.Count + 1,
                RepairSpecificationSnapshotKind.Scaled, "Scaled", request.Actor.SubjectId,
                DateTimeOffset.UtcNow, scaled.Details, scaled.Lines,
                EstimateTotals.Compute(scaled).Printed.Gross, false));
            return Task.FromResult(scaled);
        }

        /// <summary>
        /// The one Case Save carries the editor's spec (23 September 2026).
        /// Core's policy checks the request as the real command does, and the
        /// spec is recorded through the editor routine the estimate command
        /// used, so a test reads it back exactly as it did from that command.
        /// </summary>
        async Task<SaveCaseWorkspaceResult> ISaveCaseWorkspace.ExecuteAsync(
            SaveCaseWorkspaceRequest request,
            CancellationToken cancellationToken)
        {
            request = CaseWorkspacePolicy.ValidateAndNormalize(request);
            CaseSaves.Add(request);
            RepairSpecificationVersion? written = null;
            if (request.Estimate is { } estimate)
            {
                written = await ExecuteAsync(
                    estimate.ToSaveEstimateRequest(request) with { Reason = "Case saved." },
                    cancellationToken);
            }

            var details = await ExecuteAsync(new GetCaseQuery(caseId, request.Actor), cancellationToken)
                ?? throw new KeyNotFoundException("The Case is unavailable.");
            return new(details.Data!, CreateAssessment(details), written, WasReplay: false);
        }

        public Task<RepairSpecificationSnapshot> FreezeAsync(
            FreezeRepairSpecificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairSpecificationSnapshot>> ListAsync(
            Guid ownerCaseId, Guid specificationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationSnapshot>>(
                Snapshots.Where(item => item.CaseId == ownerCaseId && item.SpecificationId == specificationId).ToArray());

        public Task<RepairSpecificationSnapshot?> GetAsync(
            Guid ownerCaseId, Guid snapshotId, CancellationToken cancellationToken) =>
            Task.FromResult<RepairSpecificationSnapshot?>(Snapshots.FirstOrDefault(item => item.Id == snapshotId));

        public Task<RepairSpecificationVersion> ScaleAsync(
            ScaleRepairSpecificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> RemoveScalingAsync(
            RemoveRepairSpecificationScalingRequest request, CancellationToken cancellationToken)
        {
            var latest = Snapshots
                .Where(item => item.CaseId == request.CaseId && item.SpecificationId == request.SpecificationId)
                .OrderByDescending(item => item.Number)
                .FirstOrDefault();
            if (latest?.Kind != RepairSpecificationSnapshotKind.Scaled)
            {
                throw new InvalidOperationException("The repair specification has no removable scaling.");
            }

            var beforeScaling = Snapshots
                .Where(item => item.CaseId == request.CaseId && item.SpecificationId == request.SpecificationId && item.Kind == RepairSpecificationSnapshotKind.BeforeScaling)
                .OrderByDescending(item => item.Number)
                .First();
            var restored = (CurrentDraft ?? throw new InvalidOperationException("No draft is selected.")) with
            {
                Details = beforeScaling.Details,
                Lines = beforeScaling.Lines,
            };
            CurrentDraft = restored;
            Snapshots.Add(new(
                Guid.NewGuid(), request.CaseId, request.SpecificationId, Snapshots.Count + 1,
                RepairSpecificationSnapshotKind.ScalingRemoved, "Scaling removed", request.Actor.SubjectId,
                DateTimeOffset.UtcNow, restored.Details, restored.Lines,
                EstimateTotals.Compute(restored).Printed.Gross, false));
            return Task.FromResult(restored);
        }

        public Task<RepairSpecificationVersion> RestoreSnapshotAsync(
            RestoreRepairSpecificationSnapshotRequest request, CancellationToken cancellationToken) =>
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
            Guid ownerCaseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>(
                new[] { CurrentAccepted, CurrentDraft }.Where(item => item is not null).ToArray()!);

        public Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
            Guid ownerCaseId, int? afterVersion, Guid? afterId, int fetchCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid ownerCaseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            ListEstimatesAsync(ownerCaseId, work, cancellationToken);

        public Task<RepairSpecificationVersion> ExecuteAsync(
            SaveEstimateRequest request,
            CancellationToken cancellationToken = default)
        {
            SubmittedEstimates.Add(request);
            if (request.ExpectedVersion != WorkflowVersion)
            {
                throw new CaseVersionConflictException(caseId, request.ExpectedVersion, WorkflowVersion);
            }
            request = EstimatePolicy.ApplyEditorEvidence(
                EstimatePolicy.ValidateSave(request), request.EstimateId is null ? null : CurrentDraft, Clock.GetUtcNow());
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
            var candidate = CurrentDraft ?? CurrentAccepted ?? DraftSpecification(caseId);
            if (candidate.State == RepairSpecificationState.Draft)
            {
                var totals = EstimateTotals.Compute(candidate);
                candidate = candidate with
                {
                    CalculationBasis = EstimatePolicy.BasisFor(totals),
                    RecordedTotals = totals,
                };
            }
            var madeCurrent = candidate with
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
            DocumentCalls.Add(command);
            var contentBytes = command.Content.ToArray();
            if (retainedByOperation.TryGetValue(command.OperationKey, out var replay))
            {
                var oldBytes = retainedBytes[replay.Version.Id];
                if (!oldBytes.AsSpan().SequenceEqual(contentBytes)
                    || !string.Equals(replay.Occurrence.SourceOccurrenceIdentity, command.SourceOccurrenceIdentity, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The operation key was already used for a different source.");
                }
                return Task.FromResult(replay with { IsReplay = true });
            }
            if (FailDocumentStorage)
            {
                throw new IOException("The document store is unavailable.");
            }
            if (command.ExpectedCaseVersion != WorkflowVersion)
            {
                throw new CaseVersionConflictException(caseId, command.ExpectedCaseVersion, WorkflowVersion);
            }
            if (ActiveLease is null || !string.Equals(currentLeaseToken, command.EditLeaseToken, StringComparison.Ordinal))
            {
                throw new CaseEditLeaseExpiredException(caseId, WorkflowVersion);
            }

            var version = new DocumentVersion(
                Guid.NewGuid(), Guid.NewGuid(), 1, command.FileName, command.MediaType,
                contentBytes.Length, Convert.ToHexStringLower(SHA256.HashData(contentBytes)),
                DocumentCustodyStatus.Confirmed, DateTimeOffset.UtcNow, command.Actor.SubjectId,
                true, false, null);
            var occurrence = new DocumentOccurrence(
                Guid.NewGuid(), command.CaseId, version.DocumentId, version.Id,
                command.SemanticRole, command.Source, command.SourceOccurrenceIdentity,
                DateTimeOffset.UtcNow, [], RetainedDocuments.Count + 1);
            var file = new CaseFile(occurrence, version);
            RetainedDocument = file;
            RetainedDocuments.Add(file);
            retainedBytes[version.Id] = contentBytes;
            AddedDocuments.Add(command);
            documentMutations++;
            ActiveLease = null;
            currentLeaseToken = null;
            var added = new AddCaseDocumentResult(occurrence, version, false);
            retainedByOperation.Add(command.OperationKey, added);
            return Task.FromResult(added);
        }

        public Task<CaseDocumentMetadata?> ExecuteAsync(GetCaseDocumentMetadataQuery query, CancellationToken cancellationToken)
        {
            DocumentMetadataReads++;
            var retained = RetainedDocuments.FirstOrDefault(file => query.CaseId == caseId
                && query.OccurrenceId == file.Occurrence.Id && query.VersionId == file.Version.Id);
            return Task.FromResult<CaseDocumentMetadata?>(retained is null
                ? null
                : new(caseId, retained.Occurrence.Id, retained.Version.DocumentId, retained.Version.Id,
                    retained.Version.FileName, retained.Version.MediaType, retained.Version.ContentLength, retained.Version.Sha256));
        }

        public Task<LogicalDocumentContent> OpenAsync(ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken)
        {
            var versionId = request.VersionId
                ?? throw new InvalidOperationException("A retained document version is required.");
            var bytes = retainedBytes[versionId];
            var file = RetainedDocuments.Single(item => item.Version.Id == versionId);
            return Task.FromResult(new LogicalDocumentContent(new MemoryStream(bytes), request.DocumentId,
                versionId, null, request.ExpectedSha256, bytes.Length,
                file.Version.FileName, file.Version.MediaType));
        }

        public Task<CaseEditLease> ExecuteAsync(
            ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken)
        {
            LeaseClaims.Add(request);
            if (FailLeaseClaimAt == LeaseClaims.Count)
            {
                throw new CaseEditLeaseConflictException(request.CaseId, WorkflowVersion);
            }
            if (request.ExpectedVersion != WorkflowVersion)
            {
                throw new CaseVersionConflictException(request.CaseId, request.ExpectedVersion, WorkflowVersion);
            }
            if (ActiveLease is not null
                && !CaseEditAuthority.IsHolder(ActiveLease.HolderKind, ActiveLease.Holder, request.Actor))
            {
                throw new CaseEditLeaseConflictException(request.CaseId, WorkflowVersion);
            }
            leaseCounter++;
            ActiveLease = new(request.Actor.SubjectId, request.Actor.Kind,
                DateTimeOffset.UtcNow.AddMinutes(5), request.OperationKey);
            currentLeaseToken = $"lease-{leaseCounter}";
            return Task.FromResult(new CaseEditLease(
                request.CaseId,
                currentLeaseToken,
                request.Actor.SubjectId,
                request.ExpectedVersion,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
}
