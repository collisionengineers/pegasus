using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// The attachment resolver reads a Case's files through the documents-only port
/// and a receipt's files from a receipt the caller already holds. Neither reads
/// the whole Case or the receipt a second time.
/// </summary>
public sealed class StaffMailAttachmentResolverTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public async Task ACaseListsItsConfirmedFilesFromTheDocumentsPortAlone()
    {
        var documents = new RecordingDocuments(Document("report.pdf", DocumentCustodyStatus.Confirmed),
            Document("pending.pdf", DocumentCustodyStatus.Pending));
        var resolver = new StaffMailAttachmentResolver(documents, new ThrowingIntake());

        var options = await resolver.ListCaseAsync(Staff, CaseId, CancellationToken.None);

        Assert.Equal(["report.pdf"], options.Select(option => option.FileName));
        Assert.Equal([CaseId], documents.Asked);
    }

    [Fact]
    public async Task ACaseFilesReadRefusesAnActorWithoutCaseworkBeforeReadingAnything()
    {
        var documents = new RecordingDocuments();
        var resolver = new StaffMailAttachmentResolver(documents, new ThrowingIntake());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => resolver.ListCaseAsync(
            ActionActor.SystemWorker("attachment-resolver-test"), CaseId, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => resolver.ListCaseAsync(
            Staff, Guid.Empty, CancellationToken.None));

        Assert.Empty(documents.Asked);
    }

    [Fact]
    public async Task AReceiptTheCallerHoldsIsListedWithoutReadingItAgain()
    {
        var receipt = Receipt(Attachment("photo.jpg", IncomingArtifactCustodyState.Confirmed),
            Attachment("unconfirmed.jpg", IncomingArtifactCustodyState.Unknown));
        var resolver = new StaffMailAttachmentResolver(new RecordingDocuments(), new ThrowingIntake());

        var options = await resolver.ListIntakeAsync(Staff, receipt, CancellationToken.None);

        Assert.Equal(["photo.jpg"], options.Select(option => option.FileName));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => resolver.ListIntakeAsync(
            ActionActor.SystemWorker("attachment-resolver-test"), receipt, CancellationToken.None));
    }

    private static CaseDocument Document(string fileName, DocumentCustodyStatus custody)
    {
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        return new(
            documentId,
            CaseId,
            [
                new DocumentOccurrence(
                    Guid.NewGuid(), CaseId, documentId, versionId, DocumentSemanticRole.Other,
                    DocumentSource.StaffUpload, fileName, DateTimeOffset.UnixEpoch, [])
            ],
            [
                new DocumentVersion(
                    versionId, documentId, 1, fileName, "application/pdf", 10, new string('a', 64),
                    custody, DateTimeOffset.UnixEpoch, "staff", true, false, null)
            ]);
    }

    private static IntakeAssetRecord Attachment(string fileName, IncomingArtifactCustodyState custody) => new(
        Guid.NewGuid(), fileName, fileName, "image/jpeg",
        IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment,
        10, new string('b', 64), $"{fileName}-key", null, null, null, null, custody);

    private static IntakeReceipt Receipt(params IntakeAssetRecord[] assets)
    {
        var id = Guid.NewGuid();
        var source = assets[0];
        return new(
            id,
            source.FileName,
            source.MediaType,
            source.ContentLength,
            source.ContentHash,
            new IntakeSourceIdentity(IntakeSourceChannel.Mailbox, id.ToString("N")),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            IntakeDecision.NeedsSorting,
            "Waiting for a person.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "intake_source_reader",
            "1",
            null,
            null,
            assets);
    }

    private sealed class RecordingDocuments(params CaseDocument[] documents) : ICaseDocumentQueries
    {
        public List<Guid> Asked { get; } = [];

        public Task<IReadOnlyList<CaseDocument>> ListAsync(Guid caseId, CancellationToken cancellationToken)
        {
            Asked.Add(caseId);
            return Task.FromResult<IReadOnlyList<CaseDocument>>(documents);
        }
    }

    private sealed class ThrowingIntake : IGetIntake
    {
        public Task<IntakeReceipt?> ExecuteAsync(GetIntakeQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The receipt was read again.");
    }
}
