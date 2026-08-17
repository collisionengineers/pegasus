using System.Globalization;
using System.Net.Http.Headers;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Custody page: custody retry, staff upload, logical removal, third-party vehicle evidence,
/// and the request-scoped upload links.
/// </summary>
public sealed partial class CaseDetailsWebTests
{
    private static readonly byte[] UploadBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46];

    [Fact]
    public async Task CustodyPageBindsRetryUploadRemovalThirdPartyEvidenceAndRequestLinks()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
        {
            Substitute<IRetryCaseCustody>(services, store);
            Substitute<IAddCaseDocument>(services, store);
            Substitute<ILogicallyRemoveDocument>(services, store);
            Substitute<IConfirmThirdPartyVehicleEvidence>(services, store);
            Substitute<ICreateRequestUploadLink>(services, store);
            Substitute<IRevokeRequestUploadLink>(services, store);
        });
        var occurrenceId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var uploadOperationId = Guid.NewGuid();

        using var retried = await workspace.PostAsync(
            "Custody?handler=RetryCustody",
            workspace.MutationForm("retry-custody", "Provider storage is back", ("targetKind", "CaseSource")));
        using var uploaded = await workspace.PostAsync(
            "Custody?handler=UploadDocument",
            UploadForm(workspace, uploadOperationId, "damage.jpg", "image/jpeg", UploadBytes));
        using var removed = await workspace.PostAsync(
            "Custody?handler=RemoveDocument",
            workspace.MutationForm("remove-document", "Duplicate scan", ("occurrenceId", occurrenceId.ToString("D"))));
        using var confirmed = await workspace.PostAsync(
            "Custody?handler=ConfirmThirdPartyVehicleEvidence",
            workspace.MutationForm("confirm-third-party", "Other vehicle in frame", ("occurrenceId", occurrenceId.ToString("D"))));
        using var linkCreated = await workspace.PostAsync(
            "Custody?handler=CreateRequestUploadLink",
            workspace.MutationForm("create-request-link", "Ask the claimant for images"));
        using var linkRevoked = await workspace.PostAsync(
            "Custody?handler=RevokeRequestUploadLink",
            workspace.MutationForm(
                "revoke-request-link",
                "Sent to the wrong address",
                ("requestId", requestId.ToString("D")),
                ("expectedRequestVersion", "2")));

        foreach (var response in new[] { retried, uploaded, removed, confirmed, linkCreated, linkRevoked })
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

        var upload = Assert.Single(store.DocumentUploads);
        AssertClaimant(workspace, upload.Actor);
        Assert.Equal(store.CaseVersion, upload.ExpectedCaseVersion);
        Assert.Equal(store.LeaseToken, upload.EditLeaseToken);
        Assert.Equal(uploadOperationId.ToString("N"), upload.OperationKey);
        Assert.Equal($"staff-upload:{uploadOperationId:N}", upload.SourceOccurrenceIdentity);
        Assert.Equal("damage.jpg", upload.FileName);
        Assert.Equal("image/jpeg", upload.MediaType);
        Assert.Equal(DocumentSemanticRole.Image, upload.SemanticRole);
        Assert.Equal(DocumentSource.StaffUpload, upload.Source);
        Assert.Equal(UploadBytes, upload.Content.ToArray());

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

        // The one-time secret is shown once, as the absolute link the claimant will open.
        using var secretShown = await workspace.PostAsync(
            "Custody?handler=CreateRequestUploadLink",
            workspace.MutationForm("create-request-link-2", "Second request"));
        AssertPrg(secretShown, store.CaseId);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains(
            $"https://localhost/Uploads/{store.RequestLinkSecrets[^1].Token}",
            html,
            StringComparison.Ordinal);
        Assert.Contains("Copy this secret now", html, StringComparison.Ordinal);

        await AssertRefusalKeepsEditModeAsync(
            workspace,
            "Custody?handler=RemoveDocument",
            workspace.MutationForm("remove-document-2", "Not this one", ("occurrenceId", occurrenceId.ToString("D"))));
    }

    [Fact]
    public async Task CustodyPageRefusesAnEmptyOrOversizedStaffUploadWithoutLeavingEditMode()
    {
        var store = new RecordingCaseDetailsStore();
        using var workspace = await EnterEditModeAsync(store, services =>
            Substitute<IAddCaseDocument>(services, store));

        using var refused = await workspace.PostAsync(
            "Custody?handler=UploadDocument",
            UploadForm(workspace, Guid.NewGuid(), "empty.jpg", "image/jpeg", []));
        AssertPrg(refused, store.CaseId);
        Assert.Empty(store.DocumentUploads);
        var html = await workspace.GetWorkspaceAsync();
        Assert.Contains("Choose a non-empty document of 10 MB or less", html, StringComparison.Ordinal);
        Assert.Equal(store.LeaseToken, InputValue(html, "editLeaseToken"));
    }

    private static MultipartFormDataContent UploadForm(
        LeasedWorkspace workspace,
        Guid operationId,
        string fileName,
        string mediaType,
        byte[] bytes)
    {
        var multipart = new MultipartFormDataContent
        {
            { new StringContent(workspace.AntiforgeryToken), "__RequestVerificationToken" },
            { new StringContent(workspace.Store.CaseId.ToString("D")), "id" },
            { new StringContent(workspace.Store.CaseVersion.ToString(CultureInfo.InvariantCulture)), "expectedVersion" },
            { new StringContent(operationId.ToString("N")), "operationKey" },
            { new StringContent(workspace.Store.LeaseToken), "editLeaseToken" },
            { new StringContent("Image"), "semanticRole" }
        };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
        multipart.Add(file, "upload", fileName);
        return multipart;
    }

    private sealed partial class RecordingCaseDetailsStore :
        IRetryCaseCustody,
        IAddCaseDocument,
        ILogicallyRemoveDocument,
        IConfirmThirdPartyVehicleEvidence,
        ICreateRequestUploadLink,
        IRevokeRequestUploadLink
    {
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
