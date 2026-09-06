using System.Globalization;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Custody page: custody retry, logical removal, third-party vehicle evidence,
/// and the request-scoped upload links.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    [Fact]
    public async Task CustodyPageBindsRetryRemovalThirdPartyEvidenceAndRequestLinks()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IRetryCaseCustody>(services, store);
            Substitute<ILogicallyRemoveDocument>(services, store);
            Substitute<IConfirmThirdPartyVehicleEvidence>(services, store);
            Substitute<ICreateRequestUploadLink>(services, store);
            Substitute<IRevokeRequestUploadLink>(services, store);
        });
        var occurrenceId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        using var retried = await workspace.PostAsync(
            "Custody?handler=RetryCustody",
            workspace.MutationForm("retry-custody", "Provider storage is back", ("targetKind", "CaseSource")));
        using var removed = await workspace.PostAsync(
            "Custody?handler=RemoveDocument",
            workspace.MutationForm("remove-document", "Duplicate scan", ("occurrenceId", occurrenceId.ToString("D"))));
        using var confirmed = await workspace.PostAsync(
            "Custody?handler=ConfirmThirdPartyVehicleEvidence",
            workspace.MutationForm("confirm-third-party", "Other vehicle in frame", ("occurrenceId", occurrenceId.ToString("D"))));
        using var linkCreated = await workspace.PostAsync(
            "Custody?handler=CreateRequestUploadLink",
            workspace.MutationForm("create-request-link", "Ask the claimant for images", ("recipient", "Claimant")));
        using var linkRevoked = await workspace.PostAsync(
            "Custody?handler=RevokeRequestUploadLink",
            workspace.MutationForm(
                "revoke-request-link",
                "Sent to the wrong address",
                ("requestId", requestId.ToString("D")),
                ("expectedRequestVersion", "2")));

        foreach (var response in new[] { retried, removed, confirmed, linkCreated, linkRevoked })
        {
            AssertPrg(response, store.CaseId);
        }

        var retry = Assert.Single(store.CustodyRetries);
        AssertClaimant(workspace, retry.Actor);
        Assert.Equal(store.CaseVersion, retry.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, retry.EditLeaseToken);
        Assert.Equal("retry-custody", retry.OperationKey);
        Assert.Equal("Provider storage is back", retry.Reason);
        Assert.Equal(CustodyTargetKind.CaseSource, retry.TargetKind);

        var removal = Assert.Single(store.DocumentRemovals);
        AssertClaimant(workspace, removal.Actor);
        Assert.Equal(occurrenceId, removal.OccurrenceId);
        Assert.Equal(store.CaseVersion, removal.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, removal.EditLeaseToken);
        Assert.Equal("remove-document", removal.OperationKey);
        Assert.Equal("Duplicate scan", removal.Reason);

        var confirmation = Assert.Single(store.ThirdPartyConfirmations);
        AssertClaimant(workspace, confirmation.Actor);
        Assert.Equal(occurrenceId, confirmation.OccurrenceId);
        Assert.Equal(store.CaseVersion, confirmation.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, confirmation.EditLeaseToken);
        Assert.Equal("confirm-third-party", confirmation.OperationKey);
        Assert.Equal("Other vehicle in frame", confirmation.Reason);

        var linkCreation = Assert.Single(store.RequestLinkCreations);
        AssertClaimant(workspace, linkCreation.Actor);
        Assert.Equal(store.CaseVersion, linkCreation.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, linkCreation.EditLeaseToken);
        Assert.Equal("create-request-link", linkCreation.OperationKey);

        var revocation = Assert.Single(store.RequestLinkRevocations);
        AssertClaimant(workspace, revocation.Actor);
        Assert.Equal(requestId, revocation.RequestId);
        Assert.Equal(2, revocation.ExpectedRequestVersion);
        Assert.Equal(store.CaseVersion, revocation.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, revocation.EditLeaseToken);
        Assert.Equal("revoke-request-link", revocation.OperationKey);
        Assert.Equal("Sent to the wrong address", revocation.Reason);

        // The one-time secret is shown once, as the absolute link the claimant will open; it
        // survives the revoke post because only the workspace reads it.
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains(
            $"https://localhost/Uploads/{Assert.Single(store.RequestLinkSecrets).Token}",
            html,
            StringComparison.Ordinal);
        Assert.Contains("Copy this secret now", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Custody?handler=RemoveDocument",
            workspace.MutationForm("remove-document-2", "Not this one", ("occurrenceId", occurrenceId.ToString("D"))));

        // The staff upload handler and its refusal path went with the "Retain
        // document" control: the file is already stored, so there was nothing
        // for a person to retain (DOCS-012).
    }

    /// <summary>
    /// EPIC-011 §1.8 Case Files: each live file is a row carrying its name, its
    /// type, size and source, its custody state, and the two things an operator
    /// does with it — Preview, which is the viewer's trigger, and Save as, which
    /// is the same authorised route asked to save instead of display (DOCS-011).
    /// </summary>
    [Fact]
    public async Task CaseFilesSectionDrawsEachLiveFileWithItsCustodyPreviewAndSaveAs()
    {
        var occurrenceId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var store = new RecordingCaseDetailsStore
        {
            CaseDocuments = [Document(occurrenceId, versionId, "instruction.pdf", "application/pdf")]
        };
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");
        var download =
            $"/Cases/{store.CaseId:D}/Documents/{occurrenceId:D}/Download?versionId={versionId:D}";

        Assert.Contains("instruction.pdf", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.Preview, html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.SaveAs, html, StringComparison.Ordinal);
        Assert.Contains($"data-download-href=\"{download}\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{download}&amp;inline=True", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-evidence-item", html, StringComparison.Ordinal);
        Assert.Contains("data-evidence-set", html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CustodyState(DocumentCustodyStatus.Confirmed), html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.AddEvidence, html, StringComparison.Ordinal);
        Assert.Contains(OperatorLabels.CaseWorkspace.OpenOperations, html, StringComparison.Ordinal);
    }

    /// <summary>
    /// docs/design/README.md "No explanatory copy and page economy": a
    /// read-only visit renders no empty-state panel and no prose about how the
    /// page works. Both sentences this section used to carry are gone, and a
    /// case with no upload request and no images draws neither panel.
    /// </summary>
    [Fact]
    public async Task CaseFilesSectionCarriesNoExplanatoryCopyOrEmptyStatePanels()
    {
        var store = new RecordingCaseDetailsStore();
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => Substitute<IGetCase>(services, store)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{store.CaseId:D}?section=files");

        Assert.DoesNotContain("Availability is not assumed", html, StringComparison.Ordinal);
        Assert.DoesNotContain("No vehicle images", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            OperatorLabels.CaseWorkspace.UploadRequestsPanel,
            html,
            StringComparison.Ordinal);
    }

    /// <summary>One live case file: a current, unremoved, custody-confirmed version.</summary>
    private static CaseDocument Document(
        Guid occurrenceId,
        Guid versionId,
        string fileName,
        string mediaType)
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
                    DocumentSemanticRole.Instruction,
                    DocumentSource.Intake,
                    "source-1",
                    recordedAtUtc,
                    null,
                    null)
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
                    DocumentCustodyStatus.Confirmed,
                    recordedAtUtc,
                    "staff",
                    IsCurrent: true,
                    IsLogicallyRemoved: false,
                    RemovalReason: null)
            ]);
    }

    private sealed partial class RecordingCaseDetailsStore :
        IRetryCaseCustody,
        IAddCaseDocument,
        ILogicallyRemoveDocument,
        IConfirmThirdPartyVehicleEvidence,
        ICreateRequestUploadLink,
        IRevokeRequestUploadLink
    {
        /// <summary>The case's documents, when a test supplies them.</summary>
        public IReadOnlyList<CaseDocument> CaseDocuments { get; init; } = [];

        /// <summary>The case's request-scoped upload links, when a test supplies them.</summary>
        public IReadOnlyList<CaseRequestUploadSummary> RequestUploadLinks { get; init; } = [];

        public List<RetryCaseCustodyRequest> CustodyRetries { get; } = [];
        public List<AddCaseDocumentCommand> DocumentUploads { get; } = [];
        public List<LogicallyRemoveDocumentCommand> DocumentRemovals { get; } = [];
        public List<ConfirmThirdPartyVehicleEvidenceCommand> ThirdPartyConfirmations { get; } = [];
        public List<CreateRequestUploadLinkCommand> RequestLinkCreations { get; } = [];
        public List<RequestUploadSecret> RequestLinkSecrets { get; } = [];
        public List<RevokeRequestUploadLinkCommand> RequestLinkRevocations { get; } = [];

        Task<RetryCaseCustodyResult> IRetryCaseCustody.ExecuteAsync(
            RetryCaseCustodyRequest request,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            CustodyRetries.Add(request);
            return Task.FromResult(new RetryCaseCustodyResult(
                RetryCaseCustodyOutcome.Pending,
                CaseVersion + 1,
                "Custody retry was queued."));
        }

        Task<AddCaseDocumentResult> IAddCaseDocument.ExecuteAsync(
            AddCaseDocumentCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            DocumentUploads.Add(command with { Content = command.Content.ToArray() });
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            return Task.FromResult(new AddCaseDocumentResult(
                new(
                    Guid.NewGuid(),
                    CaseId,
                    documentId,
                    versionId,
                    command.SemanticRole,
                    command.Source,
                    command.SourceOccurrenceIdentity,
                    _now,
                    null,
                    null),
                new(
                    versionId,
                    documentId,
                    1,
                    command.FileName,
                    command.MediaType,
                    command.Content.Length,
                    new string('c', 64),
                    DocumentCustodyStatus.Pending,
                    _now,
                    command.Actor.SubjectId,
                    true,
                    false,
                    null),
                IsReplay: false));
        }

        Task ILogicallyRemoveDocument.ExecuteAsync(
            LogicallyRemoveDocumentCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            DocumentRemovals.Add(command);
            return Task.CompletedTask;
        }

        Task IConfirmThirdPartyVehicleEvidence.ExecuteAsync(
            ConfirmThirdPartyVehicleEvidenceCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            ThirdPartyConfirmations.Add(command);
            return Task.CompletedTask;
        }

        Task<CreateRequestUploadLinkResult> ICreateRequestUploadLink.ExecuteAsync(
            CreateRequestUploadLinkCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            RequestLinkCreations.Add(command);
            var issue = RequestUploadToken.Create();
            RequestLinkSecrets.Add(issue.Secret);
            return Task.FromResult(new CreateRequestUploadLinkResult(
                new(
                    Guid.NewGuid(),
                    CaseId,
                    issue.TokenDigest,
                    RequestUploadStatus.Active,
                    _now,
                    _now.AddDays(7),
                    null,
                    0,
                    0,
                    "limits-v1",
                    1),
                issue.Secret,
                IsReplay: false));
        }

        Task IRevokeRequestUploadLink.ExecuteAsync(
            RevokeRequestUploadLinkCommand command,
            CancellationToken cancellationToken)
        {
            ThrowNextFailure();
            RequestLinkRevocations.Add(command);
            return Task.CompletedTask;
        }
    }
}
