using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Web.Mcp;
using static Pegasus.IntegrationTests.AutomationMcpTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Caller evidence for the staff image acts the Automation Actor takes (R1041):
/// Register images and a reading's Dismiss on an Unidentified item and on a
/// received item, the Vehicle images record's Principal and Close with reason,
/// and the one decision on an upload of several files, each through the gated
/// /mcp host and the same commands as the staff pages
/// (<see cref="ImageIntakeWebTests"/>, <see cref="UploadConfirmationWebTests"/>).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class AutomationImageIntakeIngressTests
{
    [Fact]
    public async Task TheActorDismissesAReadingRegistersImagesAndClosesTheVehicleImagesRecord()
    {
        // A read below the automatic bar: the image goes to Unidentified with a
        // suggested reading, which is the reading Dismiss is offered on (R1092).
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE", confidence: 0.5));
        using var mcpFactory = WithAutomationMcp(factory);
        using var staff = IntakeWebDriver.CreateClient(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            mcpFactory,
            staff,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var reference = await UnidentifiedReferenceAsync(mcpFactory, IntakeWebDriver.ReceiptId(upload));
        var unreadReadingId = await RecordUnreadReadingAsync(mcpFactory, IntakeWebDriver.ReceiptId(upload));
        var principalId = await ImageIntakeTestData.SeedPrincipalAsync(mcpFactory.Services, "IMGMCP");

        var casesOnly = await RequestTokenAsync(client, "automation.cases");
        using (var denied = await PostMcpAsync(client, casesOnly, ToolCallPayload(1, "pegasus_unidentified_action", new
        {
            reference,
            action = "dismiss_reading",
            reason = "No plate is visible.",
            readingId = Guid.NewGuid(),
            operationKey = "mcp:dismiss-scope"
        })))
        {
            await AssertScopeDeniedAsync(denied, AutomationMcp.IntakeScope);
        }

        var token = await RequestTokenAsync(client, "automation.intake");
        Guid readingId;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_unidentified_get", new { reference })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.True(detail.GetProperty("canRegisterImages").GetBoolean());
            var readings = detail.GetProperty("readings").EnumerateArray().ToArray();
            var reading = Assert.Single(readings, candidate => candidate.GetProperty("outcome").GetString() == "Suggested");
            Assert.Equal("Pending", reading.GetProperty("disposition").GetString());
            Assert.True(reading.GetProperty("canDismiss").GetBoolean());
            readingId = reading.GetProperty("id").GetGuid();
            var unread = Assert.Single(readings, candidate => candidate.GetProperty("id").GetGuid() == unreadReadingId);
            Assert.Equal("Pending", unread.GetProperty("disposition").GetString());
            Assert.False(unread.GetProperty("canDismiss").GetBoolean());
        }

        // A pending reading Dismiss is not offered on is refused, and stays pending.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(30, "pegasus_unidentified_action", new
        {
            reference,
            action = "dismiss_reading",
            reason = "No plate is visible.",
            readingId = unreadReadingId,
            operationKey = "mcp:dismiss-unread"
        })))
        {
            Assert.Contains(
                "dismiss_reading is not offered on this reading; pegasus_unidentified_get lists the readings that offer it.",
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IVrmSuggestionStore>()
                .ListForReceiptAsync(IntakeWebDriver.ReceiptId(upload), CancellationToken.None);
            Assert.Equal(
                ImageVrmSuggestionDisposition.Pending,
                Assert.Single(stored, candidate => candidate.Id == unreadReadingId).Disposition);
        }

        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_unidentified_action", new
        {
            reference,
            action = "dismiss_reading",
            reason = "No plate is visible.",
            readingId = Guid.NewGuid(),
            operationKey = "mcp:dismiss-other"
        })))
        {
            Assert.Contains("The reading is not on this item.", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(4, "pegasus_unidentified_action", new
        {
            reference,
            action = "dismiss_reading",
            reason = "No plate is visible.",
            readingId,
            operationKey = "mcp:dismiss-reading"
        })))
        {
            var dismissed = await ReadStructuredContentAsync(response);
            Assert.Equal("Dismissed", dismissed.GetProperty("reading").GetProperty("disposition").GetString());
            Assert.False(dismissed.GetProperty("isReplay").GetBoolean());
        }

        // Register images needs the registration, as the record's dialog does.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_unidentified_action", new
        {
            reference,
            action = "register_images",
            reason = "The Actor read the registration from the image.",
            operationKey = "mcp:register-no-registration"
        })))
        {
            Assert.Contains("A normalized vehicle registration is required.", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        var imageIntakeId = Guid.Empty;
        foreach (var (rpcId, isReplay) in new[] { (6, false), (7, true) })
        {
            using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_unidentified_action", new
            {
                reference,
                action = "register_images",
                reason = "The Actor read the registration from the image.",
                vehicleRegistration = "ab12 cde",
                operationKey = "mcp:register-images"
            }));
            var registered = await ReadStructuredContentAsync(response);
            Assert.Equal(isReplay, registered.GetProperty("isReplay").GetBoolean());
            Assert.Equal("AB12CDE-01", registered.GetProperty("imageIntakeReference").GetString());
            Assert.Equal("Resolved", registered.GetProperty("item").GetProperty("state").GetString());
            imageIntakeId = registered.GetProperty("imageIntakeId").GetGuid();
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(8, "pegasus_image_intake_list", new { })))
        {
            var list = await ReadStructuredContentAsync(response);
            Assert.Contains(
                list.GetProperty("items").EnumerateArray(),
                item => item.GetProperty("id").GetGuid() == imageIntakeId);
        }

        long version;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(9, "pegasus_image_intake_get", new { imageIntakeId })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.Equal("AB12CDE-01", detail.GetProperty("reference").GetString());
            Assert.Equal("AwaitingInstruction", detail.GetProperty("state").GetString());
            Assert.Contains(
                detail.GetProperty("principalOptions").EnumerateArray(),
                option => option.GetProperty("principalId").GetGuid() == principalId);
            version = detail.GetProperty("version").GetInt64();
        }

        // With no lease token the change holds the record's edit scope for its one command.
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(10, "pegasus_image_intake_action", new
        {
            imageIntakeId,
            action = "set_principal",
            expectedVersion = version,
            principalId,
            operationKey = "mcp:image-principal"
        })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.Equal(principalId, detail.GetProperty("principalId").GetGuid());
            Assert.True(detail.GetProperty("version").GetInt64() > version);
        }

        // The record moved on, so a close at the version first read is refused.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(11, "pegasus_image_intake_action", new
        {
            imageIntakeId,
            action = "close",
            expectedVersion = version,
            reason = "The images belong to no instruction.",
            operationKey = "mcp:image-close-stale"
        })))
        {
            Assert.Contains("not expected version", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        string leaseToken;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(12, "pegasus_image_intake_get", new { imageIntakeId })))
        {
            version = (await ReadStructuredContentAsync(response)).GetProperty("version").GetInt64();
        }
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(13, "pegasus_edit_begin", new
        {
            recordKind = "ImageIntake",
            recordId = imageIntakeId,
            expectedVersion = version,
            operationKey = "mcp:image-lease"
        })))
        {
            var lease = await ReadStructuredContentAsync(response);
            Assert.Equal("AB12CDE-01", lease.GetProperty("reference").GetString());
            leaseToken = lease.GetProperty("editLeaseToken").GetString()!;
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(14, "pegasus_image_intake_action", new
        {
            imageIntakeId,
            action = "close",
            expectedVersion = version,
            reason = "The images belong to no instruction.",
            editLeaseToken = leaseToken,
            operationKey = "mcp:image-close"
        })))
        {
            var detail = await ReadStructuredContentAsync(response);
            Assert.Equal("StaffClosed", detail.GetProperty("state").GetString());
            Assert.Equal("The images belong to no instruction.", detail.GetProperty("closureReason").GetString());
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(15, "pegasus_edit_end", new
        {
            recordKind = "ImageIntake",
            recordId = imageIntakeId,
            editLeaseToken = leaseToken,
            operationKey = "mcp:image-lease-end"
        })))
        {
            Assert.Equal("ImageIntake", (await ReadStructuredContentAsync(response)).GetProperty("recordKind").GetString());
        }
    }

    [Fact]
    public async Task TheActorRegistersAReceivedItemsImagesAsItsMessageDoes()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var mcpFactory = WithAutomationMcp(factory);
        using var staff = IntakeWebDriver.CreateClient(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            mcpFactory,
            staff,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);
        var token = await RequestTokenAsync(client, "automation.intake");

        long version;
        using (var response = await PostMcpAsync(client, token, ToolCallPayload(1, "pegasus_intake_get", new { receiptId })))
        {
            version = (await ReadStructuredContentAsync(response)).GetProperty("version").GetInt64();
        }

        foreach (var (rpcId, arguments, refusal) in new (int, object, string)[]
        {
            (2, new { receiptId, action = "register_images", expectedReceiptVersion = version,
                vehicleRegistration = "CD34EFG", reason = "Read from the image.", claimantName = "Not here",
                operationKey = "mcp:register-field" },
                "register_images takes only vehicleRegistration and reason."),
            (3, new { receiptId, action = "register_images", expectedReceiptVersion = version + 1,
                vehicleRegistration = "CD34EFG", reason = "Read from the image.",
                operationKey = "mcp:register-stale" },
                "changed since it was read")
        })
        {
            using var refused = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_intake_action", arguments));
            Assert.Contains(refusal, await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        var imageIntakeId = Guid.Empty;
        foreach (var (rpcId, isReplay) in new[] { (4, false), (5, true) })
        {
            using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_intake_action", new
            {
                receiptId,
                action = "register_images",
                expectedReceiptVersion = version,
                vehicleRegistration = "cd34 efg",
                reason = "Read from the image.",
                operationKey = "mcp:register-received"
            }));
            var registered = await ReadStructuredContentAsync(response);
            Assert.Equal("register_images", registered.GetProperty("action").GetString());
            Assert.Equal(isReplay, registered.GetProperty("isReplay").GetBoolean());
            Assert.Equal("CD34EFG-01", registered.GetProperty("imageIntakeReference").GetString());
            var id = registered.GetProperty("imageIntakeId").GetGuid();
            Assert.True(imageIntakeId == Guid.Empty || imageIntakeId == id);
            imageIntakeId = id;
        }

        await using var scope = mcpFactory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.Equal(IntakeDecision.ImageIntakeRegistered, receipt!.Decision);
    }

    [Fact]
    public async Task TheActorDecidesAnUploadOfSeveralFilesAsItsPageDoes()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var mcpFactory = WithAutomationMcp(factory);
        using var staff = IntakeWebDriver.CreateClient(mcpFactory);
        using var client = mcpFactory.CreateClient();
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(staff);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            staff,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64)),
                ("close-up.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64))
            ]);
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        await IntakeWebDriver.ProcessQueuedAsync(mcpFactory, upload);
        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
        }

        var token = await RequestTokenAsync(client, "automation.intake");
        var group = await GetGroupAsync(client, token, groupId, rpcId: 1);
        Assert.Equal(2, group.GetProperty("members").GetArrayLength());
        Assert.False(group.GetProperty("isDiscarded").GetBoolean());
        var firstReceiptId = group.GetProperty("members")[0].GetProperty("receiptId").GetGuid();

        // Attach names every file of the upload; a roster missing one is refused.
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(2, "pegasus_upload_group_action", new
        {
            groupId,
            action = "attach",
            caseId = Guid.NewGuid(),
            expectedCaseVersion = 0,
            receiptVersions = new[] { new { receiptId = firstReceiptId, version = 0L } },
            operationKey = "mcp:upload-attach-partial"
        })))
        {
            Assert.Contains("This submission is incomplete or a file is not ready.", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        // Neither file is a possible match, so Leave declines no image match
        // here and is refused, writing nothing (R1092).
        Assert.False(group.GetProperty("canLeave").GetBoolean());
        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(3, "pegasus_upload_group_action", new
        {
            groupId,
            action = "leave",
            operationKey = "mcp:upload-leave"
        })))
        {
            Assert.Contains(
                "leave is not offered on this upload; pegasus_upload_group_get says whether it is.",
                await ReadErrorTextAsync(refused),
                StringComparison.Ordinal);
        }

        group = await GetGroupAsync(client, token, groupId, rpcId: 4);
        Assert.False(group.GetProperty("imageMatchDeclined").GetBoolean());
        Assert.True(group.GetProperty("canDiscard").GetBoolean());
        var groupVersion = group.GetProperty("version").GetInt64();
        var discardVersions = group.GetProperty("discardVersions").EnumerateArray()
            .Select(item => new
            {
                receiptId = item.GetProperty("receiptId").GetGuid(),
                version = item.GetProperty("version").GetInt64()
            })
            .ToArray();
        Assert.Equal(2, discardVersions.Length);

        using (var refused = await PostMcpAsync(client, token, ToolCallPayload(5, "pegasus_upload_group_action", new
        {
            groupId,
            action = "discard",
            expectedGroupVersion = groupVersion + 1,
            receiptVersions = discardVersions,
            operationKey = "mcp:upload-discard-stale"
        })))
        {
            Assert.Contains("This submission changed or is still being processed.", await ReadErrorTextAsync(refused), StringComparison.Ordinal);
        }

        using (var response = await PostMcpAsync(client, token, ToolCallPayload(6, "pegasus_upload_group_action", new
        {
            groupId,
            action = "discard",
            expectedGroupVersion = groupVersion,
            receiptVersions = discardVersions,
            operationKey = "mcp:upload-discard"
        })))
        {
            var discarded = await ReadStructuredContentAsync(response);
            Assert.True(discarded.GetProperty("group").GetProperty("isDiscarded").GetBoolean());
            Assert.False(discarded.GetProperty("group").GetProperty("canDiscard").GetBoolean());
        }

        await using (var scope = mcpFactory.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>()
                .GetAsync(groupId, CancellationToken.None);
            Assert.NotNull(stored!.Discard);
            Assert.Equal(ClientId, stored.Discard.ActorSubjectId);
        }
    }

    /// <summary>
    /// An uploaded image whose read matched a Case waits for staff; Leave
    /// declines the match so its images register, and leaving it again is
    /// answered as done (R1092).
    /// </summary>
    [Fact]
    public async Task TheActorLeavesAMatchedImageUploadUndecidedAsItsPageDoes()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var mcpFactory = WithAutomationMcp(factory);
        using var staff = IntakeWebDriver.CreateClient(mcpFactory);
        using var client = mcpFactory.CreateClient();
        _ = await ImageIntakeTestData.SeedInstructionCaseAsync(mcpFactory, staff, "AB12 CDE", "MCP-LEAVE-01");
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            mcpFactory,
            staff,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var groupId = await UploadGroupIdAsync(mcpFactory, IntakeWebDriver.ReceiptId(upload));
        var token = await RequestTokenAsync(client, "automation.intake");

        var group = await GetGroupAsync(client, token, groupId, rpcId: 1);
        Assert.Equal("PossibleMatch", Assert.Single(group.GetProperty("members").EnumerateArray()).GetProperty("outcome").GetString());
        Assert.True(group.GetProperty("canLeave").GetBoolean());
        Assert.False(group.GetProperty("imageMatchDeclined").GetBoolean());

        foreach (var (rpcId, key) in new[] { (2, "mcp:upload-leave"), (3, "mcp:upload-leave-again") })
        {
            using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_upload_group_action", new
            {
                groupId,
                action = "leave",
                operationKey = key
            }));
            var left = await ReadStructuredContentAsync(response);
            Assert.Equal("leave", left.GetProperty("action").GetString());
            Assert.True(left.GetProperty("group").GetProperty("imageMatchDeclined").GetBoolean());
        }
    }

    private static async Task<Guid> UploadGroupIdAsync(
        WebApplicationFactory<Program> factory,
        Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        var group = await scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>()
            .FindForMemberSourceAsync(receipt!.SourceIdentity, CancellationToken.None);
        return Assert.IsType<IntakeSubmissionGroup>(group).Id;
    }

    private static async Task<JsonElement> GetGroupAsync(HttpClient client, string token, Guid groupId, int rpcId)
    {
        using var response = await PostMcpAsync(client, token, ToolCallPayload(rpcId, "pegasus_upload_group_get", new { groupId }));
        return await ReadStructuredContentAsync(response);
    }

    /// <summary>
    /// A second, unread reading on the received item's image: one Dismiss is
    /// not offered on, as no plate was read from it.
    /// </summary>
    private static async Task<Guid> RecordUnreadReadingAsync(
        WebApplicationFactory<Program> factory,
        Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var suggestions = scope.ServiceProvider.GetRequiredService<IVrmSuggestionStore>();
        var read = Assert.Single(await suggestions.ListForReceiptAsync(receiptId, CancellationToken.None));
        var unread = await suggestions.RecordAsync(
            new ImageVrmSuggestionDraft(
                receiptId,
                read.IntakeAssetId,
                read.StorageKey,
                read.ContentHash,
                read.EngineKey,
                read.EngineVersion,
                read.ModelHashes,
                VrmRecognitionOutcomeKind.NoReadableResult,
                null,
                null,
                null,
                null,
                $"ingress-unread-reading:{receiptId:N}"),
            CancellationToken.None);
        return unread.Id;
    }

    private static async Task<string> UnidentifiedReferenceAsync(
        WebApplicationFactory<Program> factory,
        Guid receiptId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var item = await scope.ServiceProvider
            .GetRequiredService<IUnidentifiedStore>()
            .GetByOriginAsync(UnidentifiedOrigin.Receipt(receiptId), CancellationToken.None);
        var open = Assert.IsType<UnidentifiedItem>(item);
        Assert.Equal(UnidentifiedState.Open, open.State);
        return open.Reference;
    }
}
