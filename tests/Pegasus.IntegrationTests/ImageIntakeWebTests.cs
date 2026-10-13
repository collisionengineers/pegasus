using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using SkiaSharp;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Pegasus.IntegrationTests;

public sealed class ImageIntakeWebTests
{
    [Fact]
    public async Task StaffRegistersAnImageOnlyReceiptAndFindsItEverywhere()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        // The received-file page is gone (received file D2): Register images
        // lives on the Unidentified record the image-only receipt raised.
        var unidentifiedId = await OpenUnidentifiedIdAsync(factory, receiptId);
        var detailsBefore = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{unidentifiedId:D}");
        Assert.Contains("Register images", detailsBefore);
        Assert.Contains("No readable registration", detailsBefore);

        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var registerResponse = await client.PostAsync(
            $"/Unidentified/{unidentifiedId:D}?handler=RegisterImages",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["vehicleRegistration"] = "ab12 cde",
                ["reason"] = "Staff read the registration from the retained image."
            }));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        var detailsAfter = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{unidentifiedId:D}");
        Assert.DoesNotContain("handler=RegisterImages", detailsAfter, StringComparison.Ordinal);

        await using var receiptScope = factory.Services.CreateAsyncScope();
        var receipt = await receiptScope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.Equal(IntakeDecision.ImageIntakeRegistered, receipt.Decision);
        Assert.Equal("vehicle.png", receipt.SourceFileName);

        var indexByReference = await IntakeWebDriver.GetHtmlAsync(client, "/Search?query=AB12CDE-01&kind=images");
        Assert.Contains("AB12CDE-01", indexByReference);
        var indexByVrm = await IntakeWebDriver.GetHtmlAsync(client, "/Search?registration=AB12CDE&kind=images");
        Assert.Contains("AB12CDE-01", indexByVrm);

        var caseSearch = await IntakeWebDriver.GetHtmlAsync(client, "/Search?query=AB12CDE-01");
        Assert.Contains("AB12CDE-01", caseSearch);
        var caseSearchImagesOnly = await IntakeWebDriver.GetHtmlAsync(client, "/Search?kind=images");
        Assert.Contains("AB12CDE-01", caseSearchImagesOnly);

        await using var scope = factory.Services.CreateAsyncScope();
        var detail = await scope.ServiceProvider
            .GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        var imageIntakePage = await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{detail!.Record.Id:D}");
        Assert.Contains("AB12CDE-01", imageIntakePage);
        Assert.Contains("awaiting definitive instruction", imageIntakePage);
        Assert.Equal($"/Cases?tab=unassigned&selected={detail.Record.Id:D}", BackToCasesHref(imageIntakePage));
        // The attach decision lives on the Cases list; the original file opens
        // through the kept source route.
        Assert.Contains($"href=\"/Cases?tab=unassigned&amp;selected={detail.Record.Id:D}\" data-image-action=\"add-to-case\"", imageIntakePage, StringComparison.Ordinal);
        Assert.Contains($"href=\"/Received/{receiptId:D}/Source\"", imageIntakePage, StringComparison.Ordinal);
        Assert.DoesNotContain("View received item", imageIntakePage, StringComparison.Ordinal);
        Assert.DoesNotContain("Open in Box", imageIntakePage, StringComparison.Ordinal);
        // This receipt never opened a Triage, so the record has nothing to link to.
        Assert.DoesNotContain("Open Triage", imageIntakePage, StringComparison.Ordinal);
        Assert.DoesNotContain("data-record-kind", imageIntakePage, StringComparison.Ordinal);
    }

    /// <summary>
    /// A multi-file upload no registration was read from is one Unidentified
    /// item for the whole group. Register images on that item takes the
    /// registration and registers the group as one vehicle-images record,
    /// moving every member and resolving the item; a replayed post adds nothing.
    /// </summary>
    [Fact]
    public async Task StaffRegistersAnUnreadUploadGroupFromItsUnidentifiedItem()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64)),
                ("close-up.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64))
            ]);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);

        Guid unidentifiedId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
            var item = await scope.ServiceProvider
                .GetRequiredService<Pegasus.Core.Intake.Unidentified.IUnidentifiedStore>()
                .GetByOriginAsync(
                    Pegasus.Core.Intake.Unidentified.UnidentifiedOrigin.SubmissionGroup(groupId),
                    CancellationToken.None);
            var open = Assert.IsType<Pegasus.Core.Intake.Unidentified.UnidentifiedItem>(item);
            Assert.Equal(Pegasus.Core.Intake.Unidentified.UnidentifiedState.Open, open.State);
            unidentifiedId = open.Id;
        }

        var detailsBefore = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{unidentifiedId:D}");
        Assert.Contains("handler=RegisterImages", detailsBefore, StringComparison.Ordinal);
        Assert.Contains(
            "data-dialog-open=\"unidentified-register-dialog\" data-unidentified-action=\"register\"",
            detailsBefore,
            StringComparison.Ordinal);

        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client),
            ["operationKey"] = Guid.NewGuid().ToString("N"),
            ["vehicleRegistration"] = "ab12 cde",
            ["reason"] = "Staff read the registration from the uploaded images."
        };
        using (var registerResponse = await client.PostAsync(
                   $"/Unidentified/{unidentifiedId:D}?handler=RegisterImages",
                   new FormUrlEncodedContent(fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);
        }

        using (var replayResponse = await client.PostAsync(
                   $"/Unidentified/{unidentifiedId:D}?handler=RegisterImages",
                   new FormUrlEncodedContent(fields)))
        {
            Assert.Equal(HttpStatusCode.Redirect, replayResponse.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var group = await scope.ServiceProvider
                .GetRequiredService<IIntakeSubmissionGroupStore>()
                .GetAsync(groupId, CancellationToken.None);
            var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            var imageIntakes = scope.ServiceProvider.GetRequiredService<IImageIntakeQueries>();
            var recordIds = new HashSet<Guid>();
            foreach (var member in group!.Members)
            {
                var receipt = await receipts.GetAsync(member.ProcessedReceiptId!.Value, CancellationToken.None);
                Assert.Equal(IntakeDecision.ImageIntakeRegistered, receipt!.Decision);
                var detail = await imageIntakes.GetByOriginReceiptAsync(receipt.Id, CancellationToken.None);
                Assert.Equal("AB12CDE-01", detail!.Record.ImageIntakeReference);
                Assert.Equal(groupId, detail.Record.SubmissionGroupId);
                recordIds.Add(detail.Record.Id);
            }

            var recordId = Assert.Single(recordIds);
            var item = await scope.ServiceProvider
                .GetRequiredService<Pegasus.Core.Intake.Unidentified.IUnidentifiedStore>()
                .GetAsync(unidentifiedId, CancellationToken.None);
            Assert.Equal(Pegasus.Core.Intake.Unidentified.UnidentifiedState.Resolved, item!.State);
            Assert.Equal(
                Pegasus.Core.Intake.Unidentified.UnidentifiedResolutionTargetKind.ImageIntake,
                item.ResolutionTargetKind);
            Assert.Equal(recordId.ToString("N"), item.ResolutionTargetId);
        }

        var search = await IntakeWebDriver.GetHtmlAsync(client, "/Search?registration=AB12CDE&kind=images");
        Assert.Single(Regex.Matches(search, "AB12CDE-0\\d").Select(match => match.Value).Distinct());
    }

    /// <summary>
    /// A browser keeps an intake image for a week only when the address names
    /// the content it is answered with. An address without the hash, or with
    /// an old one, is never kept, so a changed image is never shown stale.
    /// </summary>
    [Fact]
    public async Task AnIntakeImageIsKeptByTheBrowserOnlyWhenItsAddressNamesItsContent()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        IntakeReceipt receipt;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            receipt = Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
                .GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None));
        }
        var asset = Assert.IsType<IntakeAssetRecord>(IntakeFileIdentity.SourceAsset(receipt));
        var assetRoute = $"/Received/{receiptId:D}/Asset/{asset.Id:D}";
        var named = $"{assetRoute}?v={asset.ContentHash}";

        // The record's gallery links the address that names the content.
        var unidentifiedId = await OpenUnidentifiedIdAsync(factory, receiptId);
        var record = await IntakeWebDriver.GetHtmlAsync(client, $"/Unidentified/{unidentifiedId:D}");
        Assert.Contains($"href=\"{named}\"", record, StringComparison.OrdinalIgnoreCase);
        // The tile itself is a rendering at tile size, even though nothing is
        // recorded on the image; only the link is the original.
        Assert.Contains(
            $"src=\"{assetRoute}?size=thumb&amp;v={asset.ContentHash}&amp;prep=0&amp;renderer={CaseDocumentThumbnails.RendererIdentity}\"",
            record,
            StringComparison.OrdinalIgnoreCase);

        using (var kept = await client.GetAsync(named))
        {
            Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
            var caching = kept.Headers.CacheControl!;
            Assert.True(caching.Private);
            Assert.Equal(TimeSpan.FromDays(7), caching.MaxAge);
            Assert.Contains(caching.Extensions, directive => directive.Name == "immutable");
            Assert.Equal($"\"{asset.ContentHash.ToLowerInvariant()}\"", kept.Headers.ETag!.Tag);
            // The whole file, byte for byte.
            Assert.Equal(
                Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
                await kept.Content.ReadAsByteArrayAsync());
        }
        foreach (var uncached in new[] { assetRoute, $"{assetRoute}?v={new string('0', 64)}" })
        {
            using var response = await client.GetAsync(uncached);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Null(response.Headers.CacheControl.MaxAge);
            Assert.Null(response.Headers.ETag);
        }

        // The receipt's own image route follows the same rule.
        using (var image = await client.GetAsync($"/Received/{receiptId:D}/Image?v={receipt.SourceHash}"))
        {
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal(TimeSpan.FromDays(7), image.Headers.CacheControl!.MaxAge);
            Assert.Equal($"\"{receipt.SourceHash.ToLowerInvariant()}\"", image.Headers.ETag!.Tag);
        }
        // The upload review addresses the image the same way, so the page's
        // refreshes while a photograph is processed reuse the browser's copy.
        var reviewImage = Pegasus.Web.Presentation.UploadReviewFile.ImageUrlOf(null, receipt);
        Assert.Equal($"/Received/{receiptId:D}/Image?v={receipt.SourceHash}", reviewImage);
        using (var image = await client.GetAsync(reviewImage))
        {
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal(TimeSpan.FromDays(7), image.Headers.CacheControl!.MaxAge);
            Assert.Equal($"\"{receipt.SourceHash.ToLowerInvariant()}\"", image.Headers.ETag!.Tag);
        }
        using (var image = await client.GetAsync($"/Received/{receiptId:D}/Image"))
        {
            Assert.True(image.Headers.CacheControl!.NoStore);
            Assert.Null(image.Headers.ETag);
        }
    }

    /// <summary>
    /// One asset download reads that one asset. It does not load the receipt's
    /// whole aggregate, whose tail is the Case link and the allocation attempts.
    /// </summary>
    [Fact]
    public async Task AnAssetDownloadReadsTheOneAssetNotTheReceiptAggregate()
    {
        var reads = new RecordingReadInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine(),
            commandInterceptor: reads);
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        IntakeAssetRecord asset;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            asset = Assert.IsType<IntakeAssetRecord>(IntakeFileIdentity.SourceAsset(
                Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
                    .GetRequiredService<IIntakeReceiptQueries>()
                    .GetAsync(receiptId, CancellationToken.None))));
        }

        reads.Reset();
        using var response = await client.GetAsync(
            $"/Received/{receiptId:D}/Asset/{asset.Id:D}?v={asset.ContentHash}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            await response.Content.ReadAsByteArrayAsync());
        var statements = reads.Statements;
        // The reads that authenticate the request (the account, its roles and
        // claims) depend on the account setup, not on the download, so they are
        // set aside. Nothing else in this pipeline reads the database, and the
        // queued provider work is idle, so what is left is the download's own.
        var download = statements
            .Where(sql => !sql.Contains("[AspNet", StringComparison.Ordinal))
            .ToArray();
        // Two reader commands, both on IntakeAssets: the one-asset read, then
        // the local custody reader's own read of that asset. Before this
        // change it was four: the receipt aggregate (one command with its six
        // includes; no split query is configured), the Case link lookup and
        // the allocation attempt lookup that follow it, then the custody
        // reader's read.
        Assert.Equal(2, download.Length);
        Assert.All(download, sql => Assert.Contains("[IntakeAssets]", sql, StringComparison.Ordinal));
        // Only the receipt aggregate reads these: its allocation attempt and
        // the three decisions it includes. Their absence means the aggregate
        // was not loaded.
        string[] aggregateOnly =
        [
            "[IntakeAllocationAttempts]",
            "[IntakeMailRouteDecisions]",
            "[IntakeMailClassificationDecisions]",
            "[IntakeCaseMatchDecisions]"
        ];
        Assert.DoesNotContain(
            statements,
            sql => aggregateOnly.Any(table => sql.Contains(table, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ConfidentMailboxReadFilesOnTheUnambiguousCaseWithoutRegistering()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "AUTO-WEB-01");
        var receiptId = await AllocationTestData.SubmitAndProcessAsync(
            services,
            new IntakeSource(
                "vehicle.png",
                "image/png",
                Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
                services.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, $"image-auto:{Guid.NewGuid():N}")),
            $"mailbox-image-auto:{Guid.NewGuid():N}");
        var receipt = await services
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        // The images join the matched Case: no Image intake and no record
        // folder are made for them (operator, 11 October 2026).
        Assert.Equal(caseId, receipt!.CurrentCaseId);
        Assert.NotEqual(IntakeDecision.ImageIntakeRegistered, receipt.Decision);
        Assert.Null(await services
            .GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None));
        Assert.Equal(0, await AllocationTestData.CountAsync(factory.Services, "ImageIntakes"));
        await using (var context = await factory.Database.CreateContextAsync())
        {
            Assert.False(await context.ExternalWorkItems.AsNoTracking()
                .AnyAsync(item => item.Kind == ExternalWorkKinds.CreateImageCaseCustody));
        }

        var suggestions = await services
            .GetRequiredService<IVrmSuggestionStore>()
            .ListForReceiptAsync(receiptId, CancellationToken.None);
        var suggestion = Assert.Single(suggestions);
        Assert.Equal(ImageVrmSuggestionDisposition.Confirmed, suggestion.Disposition);
    }

    /// <summary>
    /// A Tractable capture e-mail (FRD-02 EXT-17, operator 12 October 2026):
    /// its PDF's printed registration, not a plate read, files the photographs
    /// on the matching Case; every photograph on the images page is kept
    /// whatever its size and the letterhead logo and page icons are not; and
    /// the PDF's vehicle identity fills only what the Case left empty.
    /// </summary>
    [Fact]
    public async Task ACaptureEmailFilesItsPhotographsOnThePrintedRegistrationsCaseAndFillsItsGaps()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("ZZ99ZZZ"));
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "CAPTURE-WEB-01");
        var filledFields = new[]
        {
            CaseDataFieldNames.VehicleMake,
            CaseDataFieldNames.VehicleModel,
            CaseDataFieldNames.VehicleMileage,
            CaseDataFieldNames.IncidentDate
        };
        var seeded = await CurrentCaseDataAsync(factory, caseId, filledFields);

        var small = CaptureImage(800, 600, seed: 1, noisy: false);
        var large = CaptureImage(400, 533, seed: 2, noisy: true);
        Assert.True(small.Length < InstructionEvidenceImages.EmbeddedPhotographMinimumBytes);
        var email = IntakeTestEvidence.CreateEmail(
            "tractable.eml",
            "New completed lead",
            senderAddress: "noreply@tractable.ai",
            subject: "New completed lead: Book Test in today",
            attachments: [("LINE_LEVEL_ESTIMATE.pdf", "application/pdf", CapturePdf(small, large))]);
        var receiptId = await AllocationTestData.SubmitAndProcessAsync(
            services,
            new IntakeSource(
                email.FileName,
                email.MediaType,
                email.Content,
                services.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, $"capture:{Guid.NewGuid():N}")),
            $"mailbox-capture:{Guid.NewGuid():N}");

        var receipt = await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.Equal(caseId, receipt!.CurrentCaseId);
        Assert.Null(await services.GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None));
        // The printed registration paired the photographs: no plate was read.
        Assert.Empty(await services.GetRequiredService<IVrmSuggestionStore>()
            .ListForReceiptAsync(receiptId, CancellationToken.None));

        var embedded = receipt.AssetRecords
            .Where(asset => asset.Kind == IntakeAssetKind.EmbeddedImage)
            .ToArray();
        Assert.All(embedded, asset => Assert.Equal("image/jpeg", asset.MediaType));
        Assert.Equal(3, embedded.Length);
        var photographs = InstructionEvidenceImages.Select(receipt.AssetRecords);
        Assert.Equal(2, photographs.Count);
        Assert.Contains(photographs, photograph => photograph.ContentLength == small.Length);

        var after = await CurrentCaseDataAsync(factory, caseId, filledFields);
        var printed = new Dictionary<string, string>
        {
            [CaseDataFieldNames.VehicleMake] = "Toyota",
            [CaseDataFieldNames.VehicleModel] = "Prius",
            [CaseDataFieldNames.VehicleMileage] = "251525",
            [CaseDataFieldNames.IncidentDate] = "2026-07-17"
        };
        foreach (var field in filledFields)
        {
            Assert.Equal(seeded[field] ?? printed[field], after[field]);
        }

        await using var context = await factory.Database.CreateContextAsync();
        var vin = await context.CaseAssessmentFields.AsNoTracking()
            .SingleAsync(item => item.WorkId == caseId && item.FieldPath == AssessmentVocabulary.VehicleVin);
        Assert.Equal("JTDKB3FU703577122", vin.Value);
        Assert.Equal(VehicleCaptureFill.RecorderId, vin.RecordedBy);
    }

    private static async Task<Dictionary<string, string?>> CurrentCaseDataAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId,
        string[] fieldNames)
    {
        await using var context = await factory.Database.CreateContextAsync();
        var rows = await context.CaseDataFields.AsNoTracking()
            .Where(item => item.WorkId == caseId
                && fieldNames.Contains(item.FieldName)
                && (item.ValueKind == "fact" || item.ValueKind == "confirmed"))
            .ToListAsync();
        return fieldNames.ToDictionary(
            name => name,
            name => rows
                .Where(row => row.FieldName == name)
                .OrderBy(row => row.ValueKind == "confirmed" ? 0 : 1)
                .Select(row => row.Value)
                .FirstOrDefault());
    }

    /// <summary>
    /// The capture PDF's layout as the samples print it: a letterhead logo and
    /// a page icon beside the vehicle block, an estimate page, and the
    /// photograph page with its icon, a small photograph and one photograph
    /// printed twice.
    /// </summary>
    private static byte[] CapturePdf(byte[] small, byte[] large)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        void Lines(PdfPageBuilder page, params string[] lines)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                page.AddText(lines[index], 9, new PdfPoint(36, 760 - index * 14), font);
            }
        }

        var first = builder.AddPage(PageSize.A4);
        first.AddPng(CaptureImage(1016, 565, seed: 3, noisy: true, SKEncodedImageFormat.Png), new PdfRectangle(36, 790, 101, 826));
        first.AddPng(CaptureImage(70, 65, seed: 4, noisy: true, SKEncodedImageFormat.Png), new PdfRectangle(500, 20, 520, 38));
        Lines(
            first,
            "collisionengineers",
            "Accident Date: Fri Jul 17 2026 Submission date: Fri Jul 17 2026 Case ID: 9992bf95-964d-4d6f-93c7-fa6cdf1bea67",
            "Case Details",
            "Vehicle Information",
            "Producer Toyota",
            "Model Prius",
            "Type -",
            "Year of Production 2017",
            "VIN JTDKB3FU703577122",
            "Registration Number Ab12cde",
            "Registration Date -",
            "Mileage 251,525");

        var estimate = builder.AddPage(PageSize.A4);
        Lines(estimate, "Preliminary AI Estimate", "Total excluding tax 2,620.91");

        var images = builder.AddPage(PageSize.A4);
        Lines(images, "Submitted Vehicle Images");
        images.AddPng(CaptureImage(70, 65, seed: 4, noisy: true, SKEncodedImageFormat.Png), new PdfRectangle(500, 20, 520, 38));
        images.AddJpeg(small, new PdfRectangle(36, 420, 276, 600));
        images.AddJpeg(large, new PdfRectangle(300, 420, 540, 740));
        images.AddJpeg(large, new PdfRectangle(36, 60, 276, 380));
        return builder.Build();
    }

    private static byte[] CaptureImage(
        int width,
        int height,
        int seed,
        bool noisy,
        SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg)
    {
        using var bitmap = new SKBitmap(width, height);
        var random = new Random(seed);
        var flat = new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        bitmap.Pixels = Enumerable.Range(0, width * height)
            .Select(_ => noisy
                ? new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256))
                : flat)
            .ToArray();
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }

    /// <summary>
    /// The whole staff decision over HTTP: an Image Intake shows `Not known`
    /// until someone records a principal, offers only the active principals,
    /// and lets them be set, replaced and cleared again.
    /// </summary>
    [Fact]
    public async Task StaffSetsReplacesAndClearsTheImageIntakePrincipal()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var alpha = await ImageIntakeTestData.SeedPrincipalAsync(factory.Services, "ALPHA");
        var beta = await ImageIntakeTestData.SeedPrincipalAsync(factory.Services, "BETA");
        var retired = await ImageIntakeTestData.SeedPrincipalAsync(
            factory.Services,
            "GAMMA",
            isActive: false);
        var imageIntakeId = await RegisterImageIntakeForPrincipalAsync(factory, client);

        var initial = await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{imageIntakeId:D}");
        AssertPrincipalFact(initial, "Not known");
        Assert.Contains("Edit record", initial, StringComparison.Ordinal);
        Assert.DoesNotContain($"value=\"{alpha:D}\"", initial, StringComparison.Ordinal);

        var edit = await ClaimEditAsync(factory, client, imageIntakeId);
        Assert.Contains($"value=\"{alpha:D}\"", edit.Html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{beta:D}\"", edit.Html, StringComparison.Ordinal);
        Assert.DoesNotContain($"value=\"{retired:D}\"", edit.Html, StringComparison.Ordinal);
        // The empty option is a real selectable state, not a disabled prompt.
        Assert.Contains("<option value=\"\"", edit.Html, StringComparison.Ordinal);

        await PostPrincipalAsync(factory, client, imageIntakeId, alpha, edit);
        AssertPrincipalFact(
            await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{imageIntakeId:D}"),
            "ALPHA");

        await PostPrincipalAsync(factory, client, imageIntakeId, beta);
        AssertPrincipalFact(
            await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{imageIntakeId:D}"),
            "BETA");

        await PostPrincipalAsync(factory, client, imageIntakeId, null);
        AssertPrincipalFact(
            await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{imageIntakeId:D}"),
            "Not known");
    }

    /// <summary>
    /// Issue 832: the candidate table names each candidate's Principal without
    /// filtering the staff list, and the page says why automation is withheld
    /// when the recorded Principal has no candidate of its own.
    /// </summary>
    [Fact]
    public async Task TheCandidateTableShowsEachPrincipalAndExplainsWithheldAutomation()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var alpha = await ImageIntakeTestData.SeedPrincipalAsync(factory.Services, "ALPHA");
        // The Case exists before the manual upload registers, so pairing leaves
        // it a staff choice: the record stays Awaiting instruction and lists it.
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "WEB-PRINCIPAL-01");
        var imageIntakeId = await RegisterImageIntakeForPrincipalAsync(factory, client);
        Guid casePrincipal;
        await using (var context = await factory.Database.CreateContextAsync())
        {
            casePrincipal = await context.Cases.AsNoTracking()
                .Where(item => item.Id == caseId).Select(item => item.PrincipalId).SingleAsync();
        }

        var unknown = await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{imageIntakeId:D}");
        AssertCandidatePrincipal(unknown, QdosPrincipal.Code);
        Assert.DoesNotContain("data-image-automation-withheld", unknown, StringComparison.Ordinal);

        await PostPrincipalAsync(factory, client, imageIntakeId, alpha);
        var disagreeing = await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{imageIntakeId:D}");
        // Annotate, do not filter: the other Principal's Case stays listed.
        AssertCandidatePrincipal(disagreeing, QdosPrincipal.Code);
        Assert.Contains("data-image-automation-withheld=\"PrincipalDisagrees\"", disagreeing, StringComparison.Ordinal);
        Assert.Contains("belongs to the recorded Principal", disagreeing, StringComparison.Ordinal);

        await PostPrincipalAsync(factory, client, imageIntakeId, casePrincipal);
        var agreeing = await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{imageIntakeId:D}");
        AssertCandidatePrincipal(agreeing, QdosPrincipal.Code);
        Assert.DoesNotContain("data-image-automation-withheld", agreeing, StringComparison.Ordinal);
    }

    /// <summary>
    /// A record made from several uploaded photographs shows each photograph's
    /// receipt beside its origin. The page reads those receipts together (one
    /// whole-receipt read for three receipts, where it once read one each).
    /// </summary>
    [Fact]
    public async Task TheRecordPageReadsAGroupsReceiptsInOneStatement()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"),
            commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var png = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", png),
                ("close-up.png", "image/png", png),
                ("rear.png", "image/png", png)
            ]);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        Guid recordId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
            var detail = Assert.IsType<ImageIntakeDetail>(await scope.ServiceProvider
                .GetRequiredService<IImageIntakeQueries>()
                .GetByOriginReceiptAsync(IntakeWebDriver.ReceiptId(processed), CancellationToken.None));
            recordId = detail.Record.Id;
            var images = await scope.ServiceProvider
                .GetRequiredService<IImageIntakeQueries>()
                .ListImagesAsync(recordId, CancellationToken.None);
            Assert.Equal(3, images.Select(image => image.ReceiptId).Distinct().Count());
        }

        counter.Reset();
        var page = await IntakeWebDriver.GetHtmlAsync(client, $"/VehicleImages/{recordId:D}");

        Assert.Contains("overview.png", page, StringComparison.Ordinal);
        Assert.Equal(
            1,
            counter.CountContaining(
                "[IntakeReceipts]",
                "[IntakeAssets]",
                "[InstructionDrafts]",
                "[IntakeMailRouteDecisions]",
                "[IntakeManualAssociations]"));
    }

    private static void AssertCandidatePrincipal(string html, string expected)
    {
        var match = Regex.Match(html, "data-image-candidate-principal>(?<value>[^<]*)</td>");
        Assert.True(match.Success, "The candidate's Principal was not rendered.");
        Assert.Equal(expected, match.Groups["value"].Value.Trim());
    }

    private static async Task<Guid> RegisterImageIntakeForPrincipalAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client)
    {
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "principal-vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        var unidentifiedId = await OpenUnidentifiedIdAsync(factory, receiptId);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var registerResponse = await client.PostAsync(
            $"/Unidentified/{unidentifiedId:D}?handler=RegisterImages",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["operationKey"] = Guid.NewGuid().ToString("N"),
                ["vehicleRegistration"] = "ab12 cde",
                ["reason"] = "Staff read the registration from the retained image."
            }));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var detail = await scope.ServiceProvider
            .GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        return Assert.IsType<ImageIntakeDetail>(detail).Record.Id;
    }

    /// <summary>
    /// The open Unidentified item an image-only receipt with no readable
    /// registration raised: where Register images now lives (received file D2).
    /// </summary>
    private static async Task<Guid> OpenUnidentifiedIdAsync(IntakeWebApplicationFactory factory, Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var item = await scope.ServiceProvider
            .GetRequiredService<Pegasus.Core.Intake.Unidentified.IUnidentifiedStore>()
            .GetByOriginAsync(Pegasus.Core.Intake.Unidentified.UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None);
        var open = Assert.IsType<Pegasus.Core.Intake.Unidentified.UnidentifiedItem>(item);
        Assert.Equal(Pegasus.Core.Intake.Unidentified.UnidentifiedState.Open, open.State);
        return open.Id;
    }

    private static async Task PostPrincipalAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        Guid imageIntakeId,
        Guid? principalId,
        (long ExpectedVersion, string EditLeaseToken, string Html)? edit = null)
    {
        var currentEdit = edit ?? await ClaimEditAsync(factory, client, imageIntakeId);
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var response = await client.PostAsync(
            $"/VehicleImages/{imageIntakeId:D}?handler=Principal",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["principalId"] = principalId is { } id ? id.ToString("D") : string.Empty,
                ["expectedVersion"] = currentEdit.ExpectedVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                ["editLeaseToken"] = currentEdit.EditLeaseToken
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<(long ExpectedVersion, string EditLeaseToken, string Html)> ClaimEditAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        Guid imageIntakeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var detail = Assert.IsType<ImageIntakeDetail>(await scope.ServiceProvider
            .GetRequiredService<IImageIntakeQueries>()
            .GetAsync(imageIntakeId, CancellationToken.None));
        var antiforgeryToken = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var response = await client.PostAsync(
            $"/VehicleImages/{imageIntakeId:D}?handler=Edit",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = antiforgeryToken,
                ["expectedVersion"] = detail.LifecycleVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(
            html,
            "<input\\b(?=[^>]*\\bname=\"editLeaseToken\")[^>]*\\bvalue=\"(?<value>[^\"]*)\"[^>]*>",
            RegexOptions.CultureInvariant);
        Assert.True(token.Success, "The edit form must render its edit lease token.");
        return (
            detail.LifecycleVersion,
            WebUtility.HtmlDecode(token.Groups["value"].Value),
            html);
    }

    /// <summary>
    /// The Principal fact is always drawn. The absent state is exactly
    /// `Not known` — never an empty value and never one of the alternates the
    /// rest of the product does not use.
    /// </summary>
    private static void AssertPrincipalFact(string html, string expected)
    {
        // v26: the Principal reads in the record's ribbon.
        var match = Regex.Match(
            html,
            "data-image-principal>(?<value>[^<]*)</span>");
        Assert.True(match.Success, "The Principal fact was not rendered.");
        Assert.Equal(expected, match.Groups["value"].Value.Trim());
        if (expected == "Not known")
        {
            foreach (var alternate in new[] { "None", "Unknown", "Unassigned" })
            {
                Assert.NotEqual(alternate, match.Groups["value"].Value.Trim());
            }
        }
    }

    private static string BackToCasesHref(string html)
    {
        foreach (Match link in Regex.Matches(
            html,
            "<a\\b[^>]*href=\"(?<href>[^\"]*)\"[^>]*>(?<body>.*?)</a>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1)))
        {
            if (link.Groups["body"].Value.Contains("<span>Back to Cases</span>", StringComparison.Ordinal))
            {
                return WebUtility.HtmlDecode(link.Groups["href"].Value);
            }
        }

        Assert.Fail("The Back to Cases link must be rendered.");
        return string.Empty;
    }

    /// <summary>
    /// The text of every reader command a context sends. Contexts run inside
    /// <c>Task.WhenAll</c>, so the record is thread-safe.
    /// </summary>
    private sealed class RecordingReadInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> statements = new();

        public IReadOnlyCollection<string> Statements => [.. statements];

        public void Reset() => statements.Clear();

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            statements.Enqueue(command.CommandText);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            statements.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}

internal static class MultiFormatFixture
{
    public const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
}

internal static class ImageIntakeTestData
{
    /// <summary>
    /// Runs the queued Vehicle images custody the Worker would run: a
    /// registered record's photographs are read from its own folder, never
    /// from a holding copy (operator, 23 September 2026).
    /// </summary>
    public static Task ProcessImageCaseCustodyAsync(IntakeWebApplicationFactory factory) =>
        ProcessCustodyAsync(factory, ExternalWorkKinds.CreateImageCaseCustody);

    /// <summary>
    /// Runs every queued custody operation of one kind as the Worker would:
    /// the test host discards the queue publication.
    /// </summary>
    public static async Task ProcessCustodyAsync(IntakeWebApplicationFactory factory, string kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        Guid[] workIds;
        await using (var context = await factory.Database.CreateContextAsync())
        {
            workIds = await context.ExternalWorkItems.AsNoTracking()
                .Where(item => item.Kind == kind)
                .Select(item => item.Id)
                .ToArrayAsync();
        }
        Assert.NotEmpty(workIds);
        foreach (var workId in workIds)
        {
            await scope.ServiceProvider.GetRequiredService<IProcessQueuedCustody>()
                .ExecuteAsync(workId, CancellationToken.None);
        }
    }

    /// <summary>
    /// Inserts one principal with the given code and active flag, together
    /// with the organization and sequence lineage its foreign keys require.
    /// The remaining principal columns all carry database defaults, so this
    /// six-column insert is complete.
    /// </summary>
    public static async Task<Guid> SeedPrincipalAsync(
        IServiceProvider services,
        string code,
        bool isActive = true)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Organizations] ([Role], [Id], [Name], [Version])
            VALUES (N'principal', {organizationId}, {$"Fixture organization {code}"}, 0)
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [PrincipalSequenceLineages] ([Id], [CreatedAtUtc])
            VALUES ({lineageId}, {DateTimeOffset.UtcNow})
            """);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Principals]
                ([Id], [OrganizationId], [Code], [SequenceLineageId], [IsActive], [Version])
            VALUES ({principalId}, {organizationId}, {code}, {lineageId}, {isActive}, 0)
            """);
        return principalId;
    }

    /// <summary>
    /// Uploads a QDOS instruction email carrying the given registration and
    /// claim number, processes it, and promotes its allocated case: a real
    /// case (with a real origin receipt) reachable by case search.
    /// </summary>
    public static async Task<Guid> SeedInstructionCaseAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        string registration,
        string claimNumber)
    {
        var email = IntakeTestEvidence.CreateEmail(
            $"case-{claimNumber.ToLowerInvariant()}.eml",
            "Please see the attached instruction.",
            attachments:
            [
                ("instruction.pdf", "application/pdf",
                    IntakeTestEvidence.CreateDefinitiveQdosInstructionDocument(
                        claimantName: "Fixture Claimant", claimNumber: claimNumber, registration: registration))
            ]);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, email.FileName, email.MediaType, email.Content);
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receiptId, CancellationToken.None);
            Assert.NotNull(receipt);
            var allocation = await scope.ServiceProvider.GetRequiredService<IAllocateIntake>()
                .AttemptStaffCreateAsync(
                    new(
                        receipt!.Id,
                        receipt.Version,
                        ActionActor.Staff(
                            DevelopmentOfflineIdentity.AdministratorId,
                            [StaffRole.Administrator]),
                        $"image-intake-test-seed:{receipt.Id:N}",
                        receipt.MailClassificationDecision?.CaseType ?? CaseType.Inspection,
                        receipt.InstructionDraft?.SuggestedPrincipalCode ?? QdosPrincipal.Code,
                        new(InstructionComplete: true, ImagesComplete: true),
                        null,
                        receipt.InstructionDraft?.InspectionDate),
                    CancellationToken.None);
            Assert.Equal(IntakeAllocationProjectionStatus.Succeeded, allocation.State.Status);
        }
        return await PromoteAllocatedCaseAsync(
            factory.Services,
            receiptId,
            nameof(CaseLifecycleState.Review));
    }

    /// <summary>
    /// Finds the case that processing allocated for a receipt and moves it to
    /// the workflow state a test needs.
    /// </summary>
    public static async Task<Guid> PromoteAllocatedCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string workflowState)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<
                Pegasus.Infrastructure.Persistence.PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var connection = Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection(context.Database);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CaseId FROM CaseIntakeLinks WHERE IntakeReceiptId = @receiptId";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@receiptId";
        parameter.Value = originReceiptId;
        command.Parameters.Add(parameter);
        var caseId = await command.ExecuteScalarAsync();
        var caseIdValue = Assert.IsType<Guid>(caseId);

        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"UPDATE CaseWorkflows SET State = {workflowState} WHERE CaseId = {caseIdValue}");
        return caseIdValue;
    }

    public static async Task<Guid> SeedCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference,
        string workflowState)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<
                Pegasus.Infrastructure.Persistence.PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {$"Auto provider {reference}"}, {0L})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {"not_ready"}, {"pending"}, {originReceiptId}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {workflowState}, {0L}, {Guid.NewGuid()})");
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
            context.Database,
            $"INSERT INTO CaseDataSnapshots (WorkId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", System.Globalization.CultureInfo.InvariantCulture)}, {now}, {"image-intake-test-reader"}, {"1"}, {"image-intake-fixture"}, {1}, {reference}, {1}, {true}, {now})");
        return caseId;
    }
}
