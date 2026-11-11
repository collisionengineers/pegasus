using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;
using SkiaSharp;

using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Case video snapshot end-to-end evidence (operator, 11 November 2026; R964):
/// viewer toolbar gating on open Standard and Audit Cases, read-mode and
/// edit-mode filing without breaking a live edit lease, JPEG In report filing
/// with millisecond and ordinal collision disambiguation, and automatic
/// readiness completion into Review.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class VideoSnapshotWebTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task ViewerOffersTakeSnapshotOnOpenCasesAndOmitsItOnClosedArchivedAndPopOutViews()
    {
        var openStandard = await ReadCaseAsync(new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.Review
        });
        Assert.Contains("data-viewer-snapshot", openStandard, StringComparison.Ordinal);
        Assert.Contains("data-viewer-snapshot-form", openStandard, StringComparison.Ordinal);
        Assert.Contains("icon-aperture", openStandard, StringComparison.Ordinal);
        Assert.Contains(CaseWorkspaceLabels.Viewer.TakeSnapshot, openStandard, StringComparison.Ordinal);

        var openAudit = await ReadCaseAsync(new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportPreparation,
            SummaryCaseType = CaseType.Audit
        });
        Assert.Contains("data-viewer-snapshot", openAudit, StringComparison.Ordinal);

        var closed = await ReadCaseAsync(new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.PrincipalCancelled
        });
        Assert.DoesNotContain("data-viewer-snapshot", closed, StringComparison.Ordinal);
        Assert.DoesNotContain("data-viewer-snapshot-form", closed, StringComparison.Ordinal);

        var archived = await ReadCaseAsync(new RecordingCaseDetailsStore
        {
            State = CaseLifecycleState.ReportSent,
            Archive = new(DateTimeOffset.UtcNow, Administrator, "Archived after retention.")
        });
        Assert.DoesNotContain("data-viewer-snapshot", archived, StringComparison.Ordinal);
        Assert.DoesNotContain("data-viewer-snapshot-form", archived, StringComparison.Ordinal);

        var fixture = new PreparedImages();
        var windowStore = fixture.Store(CaseLifecycleState.ReportPreparation);
        using var workspace = await OpenEngineerWorkspaceAsync(
            windowStore,
            services => Substitute<ICaseAssetPreparationQueries>(services, windowStore));
        var popout = await GetHtmlAsync(workspace.Client, $"/Cases/{windowStore.CaseId:D}/Images");
        Assert.DoesNotContain("data-viewer-snapshot", popout, StringComparison.Ordinal);
        Assert.DoesNotContain("data-viewer-snapshot-form", popout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SnapshotVideoFilesDisambiguatedInReportImagesCompletesReadinessAndMovesEligibleCaseToReview()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(
            factory,
            new(InstructionComplete: true, ImagesComplete: false));
        var videoOccurrenceId = await AddConfirmedDocumentAsync(
            factory,
            caseId,
            "walkaround.mp4",
            "video/mp4",
            Mp4HeaderBytes(),
            DocumentSemanticRole.Other);
        await RecordVehicleMileageAsync(factory, caseId, 41_500);

        await using (var beforeContext = await CreateDbContextAsync(factory))
        {
            var workflow = await beforeContext.CaseWorkflows.AsNoTracking()
                .SingleAsync(value => value.CaseId == caseId);
            var caseRow = await beforeContext.Cases.AsNoTracking()
                .SingleAsync(value => value.Id == caseId);
            Assert.Equal(nameof(CaseLifecycleState.NotReady), workflow.State);
            Assert.False(caseRow.ImagesComplete);
        }

        using var client = CreateStaffClient(factory);
        var readModeHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        AssertCarriesNoLease(readModeHtml);
        var antiforgery = AntiforgeryValue(readModeHtml);

        var jpegBytes = SampleJpegBytes(new SKColor(40, 100, 180));
        using (var first = await PostSnapshotAsync(
            client, caseId, antiforgery, videoOccurrenceId, timestampMs: 14_250, "snap-read-1", jpegBytes))
        {
            AssertPrgToFilesImages(first, caseId);
        }

        var afterFirstHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files&tab=images");
        Assert.Contains(CaseWorkspaceLabels.Viewer.SnapshotAdded, afterFirstHtml, StringComparison.Ordinal);
        Assert.Contains("walkaround-frame-00m14s.jpg", afterFirstHtml, StringComparison.Ordinal);

        // Two further snapshots at the same timestamp disambiguate with ms, then " (2)".
        using (var second = await PostSnapshotAsync(
            client,
            caseId,
            AntiforgeryValue(afterFirstHtml),
            videoOccurrenceId,
            timestampMs: 14_250,
            "snap-read-2",
            SampleJpegBytes(new SKColor(180, 90, 40))))
        {
            AssertPrgToFilesImages(second, caseId);
        }

        using (var third = await PostSnapshotAsync(
            client,
            caseId,
            AntiforgeryValue(afterFirstHtml),
            videoOccurrenceId,
            timestampMs: 14_250,
            "snap-read-3",
            SampleJpegBytes(new SKColor(50, 160, 80))))
        {
            AssertPrgToFilesImages(third, caseId);
        }

        await using var afterContext = await CreateDbContextAsync(factory);
        var afterCase = await afterContext.Cases.AsNoTracking().SingleAsync(value => value.Id == caseId);
        var afterWorkflow = await afterContext.CaseWorkflows.AsNoTracking().SingleAsync(value => value.CaseId == caseId);
        Assert.True(afterCase.ImagesComplete);
        Assert.Equal(nameof(CaseLifecycleState.Review), afterWorkflow.State);

        var filedSnapshots = await (
                from occurrence in afterContext.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in afterContext.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                    && occurrence.SemanticRole == DocumentSemanticRole.Image
                orderby occurrence.Ordinal
                select new
                {
                    version.FileName,
                    version.MediaType,
                    version.CustodyStatus,
                    occurrence.Source,
                    occurrence.InReport
                })
            .ToListAsync();

        Assert.Equal(
            [
                "walkaround-frame-00m14s.jpg",
                "walkaround-frame-00m14s250ms.jpg",
                "walkaround-frame-00m14s250ms (2).jpg"
            ],
            filedSnapshots.Select(item => item.FileName).ToArray());
        Assert.All(filedSnapshots, item =>
        {
            Assert.Equal("image/jpeg", item.MediaType);
            Assert.Equal(DocumentCustodyStatus.Confirmed, item.CustodyStatus);
            Assert.Equal(DocumentSource.StaffUpload, item.Source);
            Assert.True(item.InReport);
        });
    }

    [Fact]
    public async Task SnapshotVideoPreservesLiveEditLeaseAndRefusesNonVideoClosedOrCorruptPayloads()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(factory);
        var movOccurrenceId = await AddConfirmedDocumentAsync(
            factory,
            caseId,
            "clip.mov",
            "video/quicktime",
            QuickTimeHeaderBytes(),
            DocumentSemanticRole.Other);
        var pdfOccurrenceId = await AddConfirmedDocumentAsync(
            factory,
            caseId,
            "instruction.pdf",
            "application/pdf",
            "%PDF-1.7\n"u8.ToArray(),
            DocumentSemanticRole.Instruction);

        using var client = CreateStaffClient(factory);
        var initial = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        using (var claim = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(initial),
                ("id", caseId.ToString("D")),
                ("expectedVersion", InputValue(initial, "expectedVersion")),
                ("operationKey", InputValue(initial, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        }

        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Contains("data-case-editing=\"true\"", editing, StringComparison.Ordinal);
        var leaseToken = InputValue(editing, "editLeaseToken");
        var versionBeforeSnapshot = InputValue(editing, "expectedVersion");
        Assert.False(string.IsNullOrWhiteSpace(leaseToken));

        using (var snapshotInEdit = await PostSnapshotAsync(
            client,
            caseId,
            AntiforgeryValue(editing),
            movOccurrenceId,
            timestampMs: 62_000,
            "snap-leased-1",
            SampleJpegBytes(new SKColor(120, 120, 200)),
            editLeaseToken: leaseToken))
        {
            AssertPrgToFilesImages(snapshotInEdit, caseId);
        }

        var afterSnapshot = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files&tab=images");
        Assert.Contains("data-case-editing=\"true\"", afterSnapshot, StringComparison.Ordinal);
        Assert.Equal(leaseToken, InputValue(afterSnapshot, "editLeaseToken"));
        Assert.Contains("clip-frame-01m02s.jpg", afterSnapshot, StringComparison.Ordinal);

        // The Case save sent with the lease token (even using the version loaded before
        // the snapshot) lands because Case filing advances the version under the lease (R727, R964).
        using (var save = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save",
            Form(
                AntiforgeryValue(editing),
                CaseSaveFields(editing, caseId, versionBeforeSnapshot, leaseToken, vehicleMake: "Toyota"))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        var afterSave = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        Assert.Equal("Toyota", InputValue(afterSave, "vehicleMake"));

        // Non-video occurrence is refused.
        using (var nonVideo = await PostSnapshotAsync(
            client,
            caseId,
            AntiforgeryValue(afterSave),
            pdfOccurrenceId,
            timestampMs: 1_000,
            "snap-refuse-pdf",
            SampleJpegBytes(new SKColor(10, 20, 30))))
        {
            AssertPrgToFilesImages(nonVideo, caseId);
        }
        var refusedPdfHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files&tab=images");
        Assert.Contains(CaseWorkspaceLabels.Viewer.SnapshotFailed, refusedPdfHtml, StringComparison.Ordinal);

        // Corrupt image payload is refused.
        using (var corrupt = await PostSnapshotAsync(
            client,
            caseId,
            AntiforgeryValue(refusedPdfHtml),
            movOccurrenceId,
            timestampMs: 2_000,
            "snap-refuse-corrupt",
            "not-a-jpeg"u8.ToArray()))
        {
            AssertPrgToFilesImages(corrupt, caseId);
        }
        var refusedCorruptHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files&tab=images");
        Assert.Contains(CaseWorkspaceLabels.Viewer.SnapshotFailed, refusedCorruptHtml, StringComparison.Ordinal);

        // Close the Case and verify snapshots are refused on closed Cases.
        await CloseCaseAsync(factory, caseId);
        using (var closedAttempt = await PostSnapshotAsync(
            client,
            caseId,
            AntiforgeryValue(refusedCorruptHtml),
            movOccurrenceId,
            timestampMs: 3_000,
            "snap-refuse-closed",
            SampleJpegBytes(new SKColor(90, 90, 90))))
        {
            AssertPrgToFilesImages(closedAttempt, caseId);
        }
        var refusedClosedHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=files&tab=images");
        Assert.Contains(CaseWorkspaceLabels.Viewer.SnapshotFailed, refusedClosedHtml, StringComparison.Ordinal);

        await using var context = await CreateDbContextAsync(factory);
        var imageCount = await context.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .CountAsync(value => value.CaseId == caseId && value.SemanticRole == DocumentSemanticRole.Image);
        Assert.Equal(1, imageCount);
    }

    [Fact]
    public async Task RetypingAConfirmedPngDocumentToImageCompletesCaseImages()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await AutomationMcpTestSupport.SeedAcceptedCaseAsync(
            factory,
            new(InstructionComplete: true, ImagesComplete: false));
        await RecordVehicleMileageAsync(factory, caseId, 28_900);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workflowBeforeAdd = await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("Missing Case workflow.");
        var addLease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(caseId, workflowBeforeAdd.Version, Administrator, $"retype-add-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var added = await services.GetRequiredService<IAddCaseDocument>().ExecuteAsync(
            new(
                caseId,
                "still-frame.png",
                "image/png",
                SamplePngBytes(),
                DocumentSemanticRole.Other,
                DocumentSource.StaffUpload,
                $"staff-other:{Guid.NewGuid():N}",
                Administrator,
                $"retype-add:{Guid.NewGuid():N}",
                addLease.Version,
                addLease.Token),
            CancellationToken.None);

        await using (var midContext = await CreateDbContextAsync(factory))
        {
            Assert.False((await midContext.Cases.AsNoTracking().SingleAsync(v => v.Id == caseId)).ImagesComplete);
        }

        var workflowBeforeRetype = await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("Missing Case workflow.");
        var retypeLease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(caseId, workflowBeforeRetype.Version, Administrator, $"retype-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        await services.GetRequiredService<ISetDocumentType>().ExecuteAsync(
            new(
                caseId,
                added.Occurrence.Id,
                added.Version.Id,
                DocumentSemanticRole.Image,
                Administrator,
                "Extracted frame from walkaround video.",
                $"retype-op:{Guid.NewGuid():N}",
                retypeLease.Version,
                retypeLease.Token),
            CancellationToken.None);

        await using var afterContext = await CreateDbContextAsync(factory);
        var afterCase = await afterContext.Cases.AsNoTracking().SingleAsync(v => v.Id == caseId);
        var afterWorkflow = await afterContext.CaseWorkflows.AsNoTracking().SingleAsync(v => v.CaseId == caseId);
        Assert.True(afterCase.ImagesComplete);
        Assert.Equal(nameof(CaseLifecycleState.Review), afterWorkflow.State);
    }

    private static HttpClient CreateStaffClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static Task<PegasusDbContext> CreateDbContextAsync(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync();

    private static async Task<Guid> AddConfirmedDocumentAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        string fileName,
        string mediaType,
        byte[] content,
        DocumentSemanticRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workflow = await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The seeded Case has no workflow.");
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(caseId, workflow.Version, Administrator, $"seed-doc-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        var result = await services.GetRequiredService<IAddCaseDocument>().ExecuteAsync(
            new(
                caseId,
                fileName,
                mediaType,
                content,
                role,
                DocumentSource.StaffUpload,
                $"seed-doc:{Guid.NewGuid():N}",
                Administrator,
                $"seed-doc-add:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token),
            CancellationToken.None);
        return result.Occurrence.Id;
    }

    private static async Task RecordVehicleMileageAsync(
        WebApplicationFactory<Program> factory,
        Guid caseId,
        int mileage)
    {
        using var client = CreateStaffClient(factory);
        var page = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        using (var claim = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ClaimLease",
            Form(
                AntiforgeryValue(page),
                ("id", caseId.ToString("D")),
                ("expectedVersion", InputValue(page, "expectedVersion")),
                ("operationKey", InputValue(page, "operationKey")))))
        {
            Assert.Equal(HttpStatusCode.Redirect, claim.StatusCode);
        }

        var editing = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        var leaseToken = InputValue(editing, "editLeaseToken");
        using (var save = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=Save",
            Form(
                AntiforgeryValue(editing),
                CaseSaveFields(
                    editing,
                    caseId,
                    InputValue(editing, "expectedVersion"),
                    leaseToken,
                    vehicleMileage: mileage.ToString(CultureInfo.InvariantCulture)))))
        {
            Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        }

        var saved = await GetHtmlAsync(client, $"/Cases/{caseId:D}");
        using var release = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=ReleaseLease",
            Form(
                AntiforgeryValue(saved),
                ("id", caseId.ToString("D")),
                ("operationKey", $"release:{Guid.NewGuid():N}"),
                ("editLeaseToken", InputValue(saved, "editLeaseToken"))));
        Assert.Equal(HttpStatusCode.Redirect, release.StatusCode);
    }

    private static async Task CloseCaseAsync(WebApplicationFactory<Program> factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workflow = await services.GetRequiredService<ICaseWorkflowQueries>()
            .GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The seeded Case has no workflow.");
        var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(caseId, workflow.Version, Administrator, $"close-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        await services.GetRequiredService<ICloseCase>().ExecuteAsync(
            new(
                caseId,
                lease.Version,
                Administrator,
                $"close-case:{Guid.NewGuid():N}",
                "Principal cancelled before inspection.",
                lease.Token,
                CaseClosureOutcome.PrincipalCancelled),
            CancellationToken.None);
    }

    private static void AssertPrgToFilesImages(HttpResponseMessage response, Guid caseId)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location?.OriginalString ?? string.Empty;
        Assert.StartsWith($"/Cases/{caseId:D}", location, StringComparison.Ordinal);
        Assert.Contains("section=files", location, StringComparison.Ordinal);
        Assert.EndsWith("#case-files-images", location, StringComparison.Ordinal);
    }

    private static Task<HttpResponseMessage> PostSnapshotAsync(
        HttpClient client,
        Guid caseId,
        string antiforgeryToken,
        Guid videoOccurrenceId,
        long timestampMs,
        string operationKey,
        byte[] payloadBytes,
        string editLeaseToken = "",
        string contentType = "image/jpeg")
    {
        var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(antiforgeryToken), "__RequestVerificationToken");
        multipart.Add(new StringContent(videoOccurrenceId.ToString("D")), "videoOccurrenceId");
        multipart.Add(new StringContent(timestampMs.ToString(CultureInfo.InvariantCulture)), "timestampMs");
        multipart.Add(new StringContent(operationKey), "operationKey");
        multipart.Add(new StringContent(editLeaseToken), "editLeaseToken");
        var fileContent = new ByteArrayContent(payloadBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent, "snapshot", "snapshot.jpg");
        return client.PostAsync($"/Cases/{caseId:D}/Custody?handler=SnapshotVideo", multipart);
    }

    private static (string Name, string Value)[] CaseSaveFields(
        string html,
        Guid caseId,
        string expectedVersion,
        string editLeaseToken,
        string? vehicleMake = null,
        string? vehicleMileage = null) =>
    [
        ("id", caseId.ToString("D")),
        ("expectedVersion", expectedVersion),
        ("operationKey", $"save:{Guid.NewGuid():N}"),
        ("editLeaseToken", editLeaseToken),
        ("reason", "Updated vehicle details during video snapshot test."),
        ("claimantName", InputValue(html, "claimantName")),
        ("claimantContactNumber", InputValue(html, "claimantContactNumber")),
        ("claimantAddress", InputValue(html, "claimantAddress")),
        ("claimNumber", InputValue(html, "claimNumber")),
        ("vehicleRegistration", InputValue(html, "vehicleRegistration")),
        ("vehicleMake", vehicleMake ?? InputValue(html, "vehicleMake")),
        ("vehicleModel", InputValue(html, "vehicleModel")),
        ("vehicleMileage", vehicleMileage ?? InputValue(html, "vehicleMileage")),
        ("vehicleMileageUnit", SelectOptionValue(html, "vehicleMileageUnit")),
        ("accidentCircumstances", TextareaContent(html, "accidentCircumstances")),
        ("incidentDate", InputValue(html, "incidentDate")),
        ("dueBy", InputValue(html, "dueBy")),
        ("claimSourceContactName", InputValue(html, "claimSourceContactName")),
        ("claimSourceContactTelephone", InputValue(html, "claimSourceContactTelephone")),
        ("claimSourceContactEmail", InputValue(html, "claimSourceContactEmail")),
        ("vatStatus", InputValue(html, "vatStatus")),
        ("inspectionDate", InputValue(html, "inspectionDate")),
        ("inspectionDeadline", InputValue(html, "inspectionDeadline")),
        ("inspectionAddress", InputValue(html, "inspectionAddress")),
        ("inspectionMode", InputValue(html, "inspectionMode")),
        ("storageLocation", InputValue(html, "storageLocation"))
    ];

    private static string SelectOptionValue(string html, string name)
    {
        var select = Regex.Match(
            html,
            $"<select[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>.*?</select>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        Assert.True(select.Success, $"Expected select '{name}'.");
        var selected = Regex.Match(
            select.Value,
            "<option[^>]*\\sselected(?:=\\\"[^\\\"]*\\\")?[^>]*value=\\\"(?<value>[^\\\"]*)\\\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (selected.Success)
        {
            return WebUtility.HtmlDecode(selected.Groups["value"].Value);
        }

        var valueFirst = Regex.Match(
            select.Value,
            "<option[^>]*value=\\\"(?<value>[^\\\"]*)\\\"[^>]*\\sselected",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return valueFirst.Success
            ? WebUtility.HtmlDecode(valueFirst.Groups["value"].Value)
            : "Miles";
    }

    private static string TextareaContent(string html, string name)
    {
        var match = Regex.Match(
            html,
            $"<textarea[^>]*name=\\\"{Regex.Escape(name)}\\\"[^>]*>(?<value>.*?)</textarea>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value) : string.Empty;
    }

    private static byte[] SampleJpegBytes(SKColor color)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(32, 24, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 92);
        return encoded.ToArray();
    }

    private static byte[] SamplePngBytes()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(16, 16, SKColorType.Rgba8888, SKAlphaType.Opaque));
        bitmap.Erase(new SKColor(60, 140, 210));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static byte[] Mp4HeaderBytes() =>
        [0x00, 0x00, 0x00, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0x00, 0x00, 0x02, 0x00];

    private static byte[] QuickTimeHeaderBytes() =>
        [0x00, 0x00, 0x00, 0x14, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'q', (byte)'t', (byte)' ', (byte)' ', 0x20, 0x05, 0x03, 0x00];
}
