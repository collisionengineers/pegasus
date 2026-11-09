using Pegasus.Core.PhotoCapture;

namespace Pegasus.Core.Tests.PhotoCapture;

internal static class PhotoCaptureTestData
{
    public static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    public static Core.PhotoCapture.PhotoCapture Capture(
        PhotoCaptureOrigin origin = PhotoCaptureOrigin.CaseLink,
        PhotoCaptureDestination destination = PhotoCaptureDestination.Case,
        DateTimeOffset? createdAtUtc = null,
        int lifetimeDays = 7,
        DateTimeOffset? openedAtUtc = null,
        DateTimeOffset? firstItemAtUtc = null,
        DateTimeOffset? lastItemAtUtc = null,
        DateTimeOffset? sentAtUtc = null,
        DateTimeOffset? settledAtUtc = null,
        DateTimeOffset? revokedAtUtc = null,
        int acceptedItemCount = 0,
        int reminderCount = 0,
        DateTimeOffset? lastRemindedAtUtc = null,
        DateTimeOffset? callLoggedAtUtc = null,
        int settledBatches = 0,
        PhotoCaptureDetails? details = null,
        string? note = null,
        string? customerName = "Sam Taylor",
        string? mobile = "07700 900123",
        DateOnly? incidentDate = null,
        long? mileage = null)
    {
        var created = createdAtUtc ?? Now.AddHours(-1);
        return new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            origin,
            StandingLinkId: origin == PhotoCaptureOrigin.CaseLink ? null : Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OrganizationId: null,
            CaseId: destination == PhotoCaptureDestination.Case ? Guid.Parse("33333333-3333-3333-3333-333333333333") : null,
            CaseReference: destination == PhotoCaptureDestination.Case ? "QDOS26001" : null,
            SubmissionGroupId: null,
            destination,
            RoutingReason: null,
            NormalizedRegistration: "RY65OFG",
            incidentDate,
            customerName,
            mobile,
            Email: null,
            details ?? (mileage is null ? PhotoCaptureDetails.Empty : new PhotoCaptureDetails(Mileage: mileage)),
            note,
            PhotoCaptureMode.Guided,
            LiveCameraOnly: false,
            OriginalsInBackground: true,
            IssuedByStaffId: null,
            created,
            created.AddDays(lifetimeDays),
            revokedAtUtc,
            openedAtUtc,
            firstItemAtUtc,
            lastItemAtUtc,
            sentAtUtc,
            settledAtUtc,
            settledBatches,
            acceptedItemCount,
            AcceptedBytes: acceptedItemCount * 1000L,
            reminderCount,
            lastRemindedAtUtc,
            callLoggedAtUtc,
            Version: 1);
    }

    public static PhotoCaptureItem Item(
        PhotoCaptureItemKind kind,
        int position = 1,
        string? area = null,
        PhotoCaptureStage? stage = null,
        int ordinal = 0,
        int batch = 1,
        Guid? intakeReceiptId = null) =>
        new(
            Guid.NewGuid(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.NewGuid(),
            kind,
            area,
            stage,
            position,
            batch,
            ordinal,
            PhotoCaptureStory.Group(kind, area, area is null ? [] : [area]),
            PhotoCaptureStory.Sequence(kind, stage, position),
            "name.jpg",
            kind == PhotoCaptureItemKind.Video ? "video/mp4" : "image/jpeg",
            Now,
            LiveCamera: false,
            PhotoCaptureStory.StartsInReport(kind),
            new string('a', 64),
            1000,
            StagedReceiptId: Guid.NewGuid(),
            intakeReceiptId,
            IntakeAssetId: null,
            OriginalReceived: false,
            Now);

    /// <summary>A complete capture's items: 8 overviews, one area through its stages, plate, dashboard.</summary>
    public static List<PhotoCaptureItem> CompleteItems(string area = "fd")
    {
        var items = new List<PhotoCaptureItem>();
        for (var position = 1; position <= PhotoCaptureStory.OverviewCount; position++)
        {
            items.Add(Item(PhotoCaptureItemKind.Overview, position));
        }

        items.Add(Item(PhotoCaptureItemKind.Damage, 1, area, PhotoCaptureStage.Overview));
        items.Add(Item(PhotoCaptureItemKind.Damage, 1, area, PhotoCaptureStage.Closer));
        items.Add(Item(PhotoCaptureItemKind.Damage, 1, area, PhotoCaptureStage.CloseUp));
        items.Add(Item(PhotoCaptureItemKind.NumberPlate));
        items.Add(Item(PhotoCaptureItemKind.Dashboard));
        return items;
    }
}
