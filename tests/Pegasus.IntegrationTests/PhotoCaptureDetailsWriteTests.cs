using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The typed details are written on the capture as it is now (FRD-28): an
/// upload that lands after a details request read the capture neither turns
/// the details away nor loses its own count, as when "Skip this step" on the
/// dashboard is sent while a photo is still uploading.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class PhotoCaptureDetailsWriteTests
{
    private static readonly ActionActor Issuer =
        ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);

    [Fact]
    public async Task DetailsReadBeforeAnItemArrivedAreRecordedAndTheItemStaysCounted()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var issued = await scope.ServiceProvider.GetRequiredService<IssuePhotoLink>().ExecuteAsync(
            new IssuePhotoLinkRequest(
                caseId,
                Issuer,
                CustomerName: null,
                Mobile: null,
                Email: null,
                IncidentDate: null,
                PhotoCaptureMode.Guided,
                QuickLinkFor: null,
                LiveCameraOnly: false,
                OriginalsInBackground: false,
                CaptureLimits.DefaultLifetimeDays,
                PhotoLinkTokenPurpose.Copy,
                OperationKey: Guid.NewGuid().ToString("N")),
            CancellationToken.None);
        var read = issued.Capture;

        // A photo lands after the details request read the capture.
        var added = await scope.ServiceProvider.GetRequiredService<IPhotoCaptureStore>().AddItemAsync(
            new NewPhotoCaptureItem(
                Guid.NewGuid(),
                read.Id,
                Guid.NewGuid(),
                PhotoCaptureItemKind.Overview,
                Area: null,
                Stage: null,
                Position: 1,
                PhotoCaptureStory.Group(PhotoCaptureItemKind.Overview, null, []),
                PhotoCaptureStory.Sequence(PhotoCaptureItemKind.Overview, null, 1),
                "001 01-01 Front.jpg",
                "image/jpeg",
                IntakeWebApplicationFactory.FixedUtcNow,
                LiveCamera: true,
                InReportOnArrival: true,
                new string('a', 64),
                1000,
                IntakeWebApplicationFactory.FixedUtcNow),
            CancellationToken.None);
        Assert.Equal(PhotoCaptureItemWrite.Created, added.Write);

        var recorded = await scope.ServiceProvider.GetRequiredService<RecordPhotoCaptureDetails>().ExecuteAsync(
            read,
            standingLink: null,
            new StandingCaptureForm(null, null, null, null, null, new PhotoCaptureDetails(NoKeys: true)),
            CancellationToken.None);

        Assert.True(recorded.Details.NoKeys);
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var row = await db.PhotoCaptures.AsNoTracking().SingleAsync(value => value.Id == read.Id);
        Assert.Equal(1, row.PhotoCount);
        Assert.Contains("\"noKeys\":true", row.DetailsJson, StringComparison.Ordinal);
    }
}
