using Pegasus.Core.Documents;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Tests.ImageIntake;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// The store is the one owner of an item's batch and the Case it declares,
/// decided under the capture's lock: the upload stages under the item's own
/// member token and declares the item's Case, never the capture as the
/// request read it, so a retry after a settle or after the capture reached a
/// Case stages what the store decided and is not refused as a different
/// submission.
/// </summary>
public sealed class ReceivePhotoCaptureItemTests
{
    private static readonly byte[] Pdf = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];

    [Fact]
    public async Task AnItemStagesUnderTheBatchAndCaseTheStoreGaveIt()
    {
        var read = Capture(PhotoCaptureOrigin.Website, PhotoCaptureDestination.VehicleImages);
        var declaredCase = Guid.NewGuid();
        var stored = Paperwork(batch: 2, ordinal: 4, declaredCase);
        var staged = new List<StreamedIntakeSource>();

        await Receive(stored, staged).ExecuteAsync(read, Request(stored), CancellationToken.None);

        var source = Assert.Single(staged);
        Assert.Equal(PhotoCaptureIntakeRules.MemberToken(read.Id, 2, 4), source.SourceIdentity.ExternalReceiptToken);
        Assert.Equal(declaredCase, source.DeclaredCaseId);
        Assert.Equal(DocumentSemanticRole.Other, source.DeclaredDocumentType);
    }

    [Fact]
    public async Task AnItemWithNoCaseDeclaresNeitherACaseNorADocumentType()
    {
        var read = Capture();
        var stored = Paperwork(batch: 1, ordinal: 0, declaredCaseId: null);
        var staged = new List<StreamedIntakeSource>();

        await Receive(stored, staged).ExecuteAsync(read, Request(stored), CancellationToken.None);

        var source = Assert.Single(staged);
        Assert.Null(source.DeclaredCaseId);
        Assert.Null(source.DeclaredDocumentType);
    }

    private static PhotoCaptureItem Paperwork(int batch, int ordinal, Guid? declaredCaseId) =>
        Item(PhotoCaptureItemKind.Paperwork, ordinal: ordinal, batch: batch) with
        {
            MediaType = "application/pdf",
            ContentLength = Pdf.Length,
            StagedReceiptId = null,
            DeclaredCaseId = declaredCaseId
        };

    private static ReceivePhotoCaptureItemRequest Request(PhotoCaptureItem item) =>
        new(
            item.ClientItemId,
            item.Kind,
            Area: null,
            Stage: null,
            item.Position,
            TakenAtUtc: null,
            LiveCamera: false,
            IsOriginal: false,
            "claim.pdf",
            item.MediaType,
            Pdf.Length,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(Pdf, writable: false)));

    /// <summary>The use case over a store that answers with <paramref name="stored"/> as a replay with no receipt, and an intake that records what it is asked to stage.</summary>
    private static ReceivePhotoCaptureItem Receive(PhotoCaptureItem stored, List<StreamedIntakeSource> staged)
    {
        var captures = AutomaticImageIntakeTests.Forwarder.For<IPhotoCaptureStore>(null);
        captures.Overrides[nameof(IPhotoCaptureStore.AddItemAsync)] =
            _ => Task.FromResult(new PhotoCaptureItemWriteResult(PhotoCaptureItemWrite.Replayed, stored));
        captures.Overrides[nameof(IPhotoCaptureStore.RecordItemReceiptAsync)] = _ => Task.CompletedTask;
        var intake = AutomaticImageIntakeTests.Forwarder.For<IIntakeSubmission>(null);
        intake.Overrides[nameof(IIntakeSubmission.ReceiveStreamedAsync)] = args =>
        {
            staged.Add((StreamedIntakeSource)args[0]!);
            return Task.FromResult(new ReceivedIntake(Guid.NewGuid(), IsDuplicate: false));
        };
        intake.Overrides[nameof(IIntakeSubmission.PublishCommittedAsync)] = _ => Task.CompletedTask;
        return new(
            (IPhotoCaptureStore)captures.Proxy,
            (IIntakeSubmission)intake.Proxy,
            originals: null!,
            new FixedClock());
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
