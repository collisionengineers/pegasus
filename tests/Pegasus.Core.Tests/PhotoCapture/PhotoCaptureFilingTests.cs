using Pegasus.Core.PhotoCapture;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// When a batch whose record merged into a Case may record its reach: once
/// each of its kept files (paperwork, video) has filed on that Case or its
/// filing has ended for good, as a photograph holds its batch (operator,
/// 10 November 2026; R844). Photographs do not hold a reach: the record's
/// merge filed them.
/// </summary>
public sealed class PhotoCaptureFilingTests
{
    private static readonly Guid CaseId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Cutoff = Now.AddHours(-3);

    [Fact]
    public void AKeptFileWithNoReceiptHoldsTheReach()
    {
        var paperwork = Filing(PhotoCaptureItemKind.Paperwork, intakeReceipt: false, link: null);

        Assert.False(PhotoCaptureFiling.KeptFilesSettled([paperwork], CaseId, Cutoff));
    }

    [Fact]
    public void AnUnlinkedKeptFileHoldsTheReach()
    {
        var video = Filing(PhotoCaptureItemKind.Video, intakeReceipt: true, link: null);

        Assert.False(PhotoCaptureFiling.KeptFilesSettled([video], CaseId, Cutoff));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALinkedKeptFileHoldsTheReachUntilItHasFiled(bool started)
    {
        var paperwork = Filing(PhotoCaptureItemKind.Paperwork, intakeReceipt: true, LinkedHere(bySystem: false)) with
        {
            FilingStarted = started
        };

        Assert.False(PhotoCaptureFiling.KeptFilesSettled([paperwork], CaseId, Cutoff));
        Assert.True(PhotoCaptureFiling.KeptFilesSettled([paperwork with { FilingStarted = true, Filed = true }], CaseId, Cutoff));
    }

    [Fact]
    public void AKeptFileWhoseFilingEndedOrFailedDoesNotHoldTheReach()
    {
        var link = LinkedHere(bySystem: false);
        var exhausted = Filing(PhotoCaptureItemKind.Paperwork, intakeReceipt: true, link) with
        {
            FilingStarted = true,
            FilingExhaustedAtUtc = link.LinkedAtUtc.AddMinutes(30)
        };
        var failed = Filing(PhotoCaptureItemKind.Video, intakeReceipt: true, link) with { WorkFailed = true };
        var elsewhere = Filing(
            PhotoCaptureItemKind.Paperwork,
            intakeReceipt: true,
            new PhotoCaptureItemLink(Guid.NewGuid(), IsActive: true, BySystem: false, Now.AddMinutes(-5)));

        Assert.True(PhotoCaptureFiling.KeptFilesSettled([exhausted, failed, elsewhere], CaseId, Cutoff));
    }

    [Fact]
    public void APhotographDoesNotHoldTheReach()
    {
        var photograph = Filing(PhotoCaptureItemKind.Overview, intakeReceipt: true, link: null);

        Assert.True(PhotoCaptureFiling.KeptFilesSettled([photograph], CaseId, Cutoff));
    }

    private static PhotoCaptureItemLink LinkedHere(bool bySystem) =>
        new(CaseId, IsActive: true, bySystem, Now.AddMinutes(-5));

    private static PhotoCaptureItemFiling Filing(PhotoCaptureItemKind kind, bool intakeReceipt, PhotoCaptureItemLink? link) =>
        new(
            Item(kind, intakeReceiptId: intakeReceipt ? Guid.NewGuid() : null),
            WorkFailed: false,
            link,
            FilingStarted: false,
            Filed: false,
            FilingExhaustedAtUtc: null);
}
