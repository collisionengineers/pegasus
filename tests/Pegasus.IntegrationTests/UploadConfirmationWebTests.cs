using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// INTK-016: the confirmation surface's own staff decision — the case-search
/// suggestions behind the autocomplete, and adding uploaded material to a
/// case found there — exercised through the real Web host end to end. The
/// per-branch decision table itself is covered in
/// <see cref="UploadOutcomeQueriesTests"/>.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class UploadConfirmationWebTests
{
    [Fact]
    public async Task CaseSearchSuggestsMatchingCasesToStaff()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "SEARCH-001");
        var upload = await IntakeWebDriver.UploadAsync(
            client,
            "receipt-scoped-search.eml",
            "message/rfc822",
            IntakeTestEvidence.CreateEmail(
                "receipt-scoped-search.eml",
                "QDOS instruction\r\nClaimant Name: Search Claimant\r\nClaim Number: SEARCH-DOC\r\nVehicle Registration: ZZ99 ZZZ").Content);
        var stagedReceiptId = IntakeWebDriver.ReceiptId(upload);
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var receiptId = IntakeWebDriver.ReceiptId(processed);

        using var response = await client.GetAsync(
            $"/Upload/Status/{stagedReceiptId:D}?handler=CaseSearch&receiptId={receiptId:D}&term=AB12%20CDE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var suggestions = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.True(suggestions.GetArrayLength() >= 1);
        var match = suggestions.EnumerateArray().Single(item =>
            item.GetProperty("caseId").GetGuid() == caseId);
        Assert.False(string.IsNullOrWhiteSpace(match.GetProperty("reference").GetString()));
        Assert.Equal(
            "AB12CDE",
            match.GetProperty("registration").GetString()?.Replace(" ", "", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(match.GetProperty("stage").GetString()));

        // A term shorter than two characters returns nothing rather than the
        // whole case list.
        using var shortTerm = await client.GetAsync(
            $"/Upload/Status/{stagedReceiptId:D}?handler=CaseSearch&receiptId={receiptId:D}&term=A");
        Assert.Equal(HttpStatusCode.OK, shortTerm.StatusCode);
        Assert.Equal(
            0,
            JsonDocument.Parse(await shortTerm.Content.ReadAsStringAsync())
                .RootElement.GetArrayLength());
    }

    [Fact]
    public async Task CaseSearchIsStaffOnly()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var client = IntakeWebDriver.CreateClient(factory);

        using var anonymousRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Upload/Status/{Guid.NewGuid():D}?handler=CaseSearch&term=AB12");
        anonymousRequest.Headers.Add("X-Test-Anonymous", "1");
        using var anonymous = await client.SendAsync(anonymousRequest);
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Contains(
            "/Account/SignIn",
            anonymous.Headers.Location!.OriginalString,
            StringComparison.Ordinal);

        using var rolelessRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Upload/Group/{Guid.NewGuid():D}?handler=CaseSearch&term=AB12");
        rolelessRequest.Headers.Add("X-Test-Roleless", "1");
        using var roleless = await client.SendAsync(rolelessRequest);
        Assert.Equal(HttpStatusCode.Forbidden, roleless.StatusCode);
    }

    [Fact]
    public async Task AttachAddsAnUnmatchedInstructionUploadToTheChosenCaseAndReplaysSafely()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "ATTACH-CASE-01");

        var email = IntakeTestEvidence.CreateEmail(
            "unmatched-instruction.eml",
            "QDOS instruction\r\nClaimant Name: Attach Claimant\r\nClaim Number: ATTACH-DOC-01\r\nVehicle Registration: CD34 EFG");
        var upload = await IntakeWebDriver.UploadAsync(
            client, email.FileName, email.MediaType, email.Content);
        var stagedReceiptId = IntakeWebDriver.ReceiptId(upload);
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var receiptId = IntakeWebDriver.ReceiptId(processed);

        var statusPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Status/{stagedReceiptId:D}");
        Assert.Contains("Choose a case destination", statusPage, StringComparison.Ordinal);
        Assert.Contains("Add to an existing case", statusPage, StringComparison.Ordinal);
        Assert.Contains("Cancel", statusPage, StringComparison.Ordinal);
        var (receiptVersion, caseVersion) = await AttachmentVersionsAsync(factory, receiptId, caseId);
        var operationId = Guid.NewGuid();

        var redirect = await PostAttachAsync(
            client,
            $"/Upload/Status/{stagedReceiptId:D}?handler=Attach",
            receiptId,
            caseId: caseId,
            operationId: operationId,
            receiptVersion: receiptVersion,
            caseVersion: caseVersion);
        Assert.Equal(HttpStatusCode.Redirect, redirect);

        await AssertLinkedAsync(factory, receiptId, caseId);
        var afterPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Status/{stagedReceiptId:D}");
        Assert.Contains("This was added to case", afterPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Add to an existing case", afterPage, StringComparison.Ordinal);
        Assert.DoesNotContain("automatically associated", afterPage, StringComparison.Ordinal);
        // The case this upload now belongs to is reached only through the
        // association the attach recorded, not through an accepted case link
        // of its own: the status page still opens it.
        Assert.Contains(">Open case</a>", afterPage, StringComparison.Ordinal);
        Assert.Contains($"/Cases/Details/{caseId:D}", afterPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Open receipt", afterPage, StringComparison.Ordinal);

        // The same decision submitted again changes nothing and still reports
        // the same settled destination.
        var replay = await PostAttachAsync(
            client,
            $"/Upload/Status/{stagedReceiptId:D}?handler=Attach",
            receiptId,
            caseId: caseId,
            operationId: operationId,
            receiptVersion: receiptVersion,
            caseVersion: caseVersion);
        Assert.Equal(HttpStatusCode.Redirect, replay);
        await AssertLinkedAsync(factory, receiptId, caseId);
    }

    [Fact]
    public async Task AttachMergesARegisteredImageGroupIntoACaseTypedByReference()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);
        // The fixture case's registration does not match the images' VRM, so
        // automation abstains from associating and the group registers as a
        // new vehicle-image case awaiting the staff decision.
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY34 ZZZ", "IMAGE-MERGE-01");
        var caseReference = await CaseReferenceAsync(factory, caseId);

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
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var memberReceiptId = IntakeWebDriver.ReceiptId(processed);

        Guid originReceiptId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The members' drain order can leave one member's group outcome
            // pending for the Worker's reconcile sweep; run it as the Worker
            // would so the test observes the group's settled state.
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
            var detail = await scope.ServiceProvider
                .GetRequiredService<IImageIntakeQueries>()
                .GetByOriginReceiptAsync(memberReceiptId, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction, detail!.State);
            originReceiptId = detail.Record.Origin.ReceiptId;
        }

        var groupPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.Contains("registered as a new vehicle-image case", groupPage, StringComparison.Ordinal);
        Assert.Contains("Add to an existing case", groupPage, StringComparison.Ordinal);
        // The group card owns the confirmation and carries every actual
        // member receipt, not the registered image record's origin repeated
        // for each file.
        Assert.Equal(2, SplitOccurrences(GroupAttachForm(groupPage), "receiptVersions[").Count());

        // Typed input takes a server-rendered confirmation step before the
        // write, binding every member's reviewed receipt version and the
        // target Case version.
        var confirmation = await ConfirmGroupAttachAsync(
            factory, client, groupId, caseId, caseReference);
        Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var db = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            var association = await db.IntakeManualAssociations
                .SingleAsync(item => item.IntakeReceiptId == originReceiptId);
            Assert.Equal(nameof(ActorKind.Staff), association.ActorKind);
            Assert.Null(association.Reason);

            var detail = await scope.ServiceProvider
                .GetRequiredService<IImageIntakeQueries>()
                .GetByOriginReceiptAsync(memberReceiptId, CancellationToken.None);
            Assert.Equal(ImageInitiatedCaseState.MergedIntoInstructionCase, detail!.State);
            Assert.Equal(caseId, detail.MergedIntoCaseId);
        }

        await AssertLinkedAsync(factory, originReceiptId, caseId);
        var afterPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.Contains("This was added to case", afterPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Add to an existing case", afterPage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManualUploadWithAUniqueImageMatchStillRequiresStaffConfirmation()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "AUTO-ASSOC-01");

        var upload = await IntakeWebDriver.UploadAsync(
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var stagedReceiptId = IntakeWebDriver.ReceiptId(upload);
        _ = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);

        var caseReference = await CaseReferenceAsync(factory, caseId);
        var statusPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Status/{stagedReceiptId:D}");
        Assert.Contains("registered as a new vehicle-image case", statusPage, StringComparison.Ordinal);
        Assert.Contains("Add to an existing case", statusPage, StringComparison.Ordinal);
        Assert.Contains(caseReference, statusPage, StringComparison.Ordinal);
        Assert.DoesNotContain("automatically associated with case", statusPage, StringComparison.Ordinal);
        Assert.DoesNotContain($"/Cases/Details/{caseId:D}", statusPage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AttachWithoutAResolvableReferenceFailsClosed()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        var email = IntakeTestEvidence.CreateEmail(
            "unmatched-failclosed.eml",
            "QDOS instruction\r\nClaimant Name: Closed Claimant\r\nClaim Number: CLOSED-01\r\nVehicle Registration: EF56 GHJ");
        var upload = await IntakeWebDriver.UploadAsync(
            client, email.FileName, email.MediaType, email.Content);
        var stagedReceiptId = IntakeWebDriver.ReceiptId(upload);
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var receiptId = IntakeWebDriver.ReceiptId(processed);
        var (receiptVersion, _) = await AttachmentVersionsAsync(factory, receiptId, null);

        var operationId = Guid.NewGuid();
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var response = await client.PostAsync(
            $"/Upload/Status/{stagedReceiptId:D}?handler=Attach",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["receiptId"] = receiptId.ToString("D"),
                ["reference"] = "NO-SUCH-CASE",
                ["operationId"] = operationId.ToString("D"),
                ["receiptVersion"] = receiptVersion.ToString(CultureInfo.InvariantCulture)
            }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var failedPage = await response.Content.ReadAsStringAsync();
        Assert.Contains("No single viable case matched", failedPage, StringComparison.Ordinal);
        Assert.Contains("NO-SUCH-CASE", failedPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Reason for adding to this case", failedPage, StringComparison.Ordinal);
        Assert.Contains(operationId.ToString("D"), failedPage, StringComparison.Ordinal);

        // The failed typed first step is a recoverable form error, not a
        // redirect that drops the staff decision they need to correct.
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.Null(receipt!.CurrentCaseId);
    }

    private static async Task<string> CaseReferenceAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var workflow = await scope.ServiceProvider
            .GetRequiredService<ICaseWorkflowStore>()
            .GetAsync(caseId, CancellationToken.None);
        return workflow!.Identity.Reference;
    }

    [Fact]
    public async Task AnUndecidedGroupShowsOneSubmissionDecisionInsteadOfPerFileOffers()
    {
        // No readable VRM: automation abstains, both members go to
        // Unidentified, and the submission needs one staff decision.
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
        Guid[] stagedReceiptIds;
        await using (var lookupScope = factory.Services.CreateAsyncScope())
        {
            var group = await lookupScope.ServiceProvider
                .GetRequiredService<IIntakeSubmissionGroupStore>()
                .GetAsync(groupId)
                ?? throw new InvalidOperationException("The upload group was not persisted.");
            stagedReceiptIds = group.Members
                .OrderBy(member => member.Ordinal)
                .Select(member => member.StagedReceiptId)
                .ToArray();
        }

        // The normal test helper drains whichever same-due work item the
        // store chooses next. Dispatch both members first, then process their
        // recorded submission order so the rail's U references are stable.
        await using (var dispatchScope = factory.Services.CreateAsyncScope())
        {
            var dispatcher = new DispatchPendingIntakeWork(
                dispatchScope.ServiceProvider.GetRequiredService<IIntakeWorkStore>(),
                new IntakeWebDriver.NoOpIntakeWorkEnqueuer(),
                dispatchScope.ServiceProvider.GetRequiredService<TimeProvider>());
            Assert.Equal(2, await dispatcher.ExecuteAsync(2));
        }

        await using (var processScope = factory.Services.CreateAsyncScope())
        {
            var processor = IntakeWebDriver.CreateProcessor(processScope.ServiceProvider);
            await processor.ExecuteAsync(stagedReceiptIds[0]);
            await processor.ExecuteAsync(stagedReceiptIds[1]);
        }

        await using (var reconcileScope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(reconcileScope.ServiceProvider);
        }

        var groupPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.Contains("This submission", groupPage, StringComparison.Ordinal);
        Assert.Contains("Create a vehicle-image case", groupPage, StringComparison.Ordinal);
        Assert.Contains("Add to an existing case", groupPage, StringComparison.Ordinal);
        // Exactly one decision surface: no per-file offers, and the per-file
        // rows keep their state chips without action buttons.
        Assert.DoesNotContain(">Create a case<", groupPage, StringComparison.Ordinal);
        Assert.DoesNotContain(">Review<", groupPage, StringComparison.Ordinal);
        Assert.Single(SplitOccurrences(groupPage, "Add to an existing case"));
        // Image members render thumbnails through the inline image route.
        Assert.Contains("/Image", groupPage, StringComparison.Ordinal);
        Assert.Contains("upload-thumb", groupPage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterGroupCreatesOneVehicleImageCaseFromTheStaffTypedRegistration()
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
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        var processed = await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        var memberReceiptId = IntakeWebDriver.ReceiptId(processed);
        await using (var reconcileScope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(reconcileScope.ServiceProvider);
        }

        var redirect = await PostGroupHandlerAsync(
            client,
            $"/Upload/Group/{groupId:D}?handler=RegisterGroup",
            new Dictionary<string, string>
            {
                ["vehicleRegistration"] = "ab12 cde",
                ["reason"] = "Staff read the registration from the photographs."
            });
        Assert.Equal(HttpStatusCode.Redirect, redirect);

        await using var scope = factory.Services.CreateAsyncScope();
        var detail = await scope.ServiceProvider
            .GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(memberReceiptId, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.StartsWith("AB12CDE", detail!.Record.ImageIntakeReference, StringComparison.Ordinal);
        Assert.Equal(ImageInitiatedCaseState.AwaitingInstruction, detail.State);

        // Replay: the same decision posts again and still reports one
        // registration for the group.
        var replay = await PostGroupHandlerAsync(
            client,
            $"/Upload/Group/{groupId:D}?handler=RegisterGroup",
            new Dictionary<string, string>
            {
                ["vehicleRegistration"] = "AB12CDE",
                ["reason"] = "Staff read the registration from the photographs."
            });
        Assert.Equal(HttpStatusCode.Redirect, replay);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AttachGroupAddsEveryOpenMemberToTheChosenCase(bool interruptAfterFirstMember)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY34 ZZZ", "GROUP-ATTACH-01");
        var caseReference = await CaseReferenceAsync(factory, caseId);

        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64)),
                ("close-up.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64))
            ]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        await using (var reconcileScope = factory.Services.CreateAsyncScope())
        {
            await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(reconcileScope.ServiceProvider);
        }

        await using var linkScope = factory.Services.CreateAsyncScope();
        using var attachmentFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ILinkIntake>();
                services.AddSingleton<ILinkIntake>(new InterruptSecondLink(
                    linkScope.ServiceProvider.GetRequiredService<ILinkIntake>(), interruptAfterFirstMember));
            }));
        using var attachmentClient = attachmentFactory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var confirmation = await ConfirmGroupAttachAsync(
            factory, attachmentClient, groupId, caseId, caseReference);
        Assert.Equal(interruptAfterFirstMember ? HttpStatusCode.OK : HttpStatusCode.Redirect, confirmation.StatusCode);
        if (interruptAfterFirstMember)
        {
            Assert.Contains("1 file was completed before this stopped", confirmation.Body, StringComparison.Ordinal);
            Assert.Contains("Confirm and add the submission", confirmation.Body, StringComparison.Ordinal);
            Assert.Equal(2, SplitOccurrences(GroupAttachForm(confirmation.Body), "name=\"receiptVersions[").Count());
            var refreshedPage = await IntakeWebDriver.GetHtmlAsync(
                attachmentClient, $"/Upload/Group/{groupId:D}");
            Assert.DoesNotContain("Confirm and add the submission", refreshedPage, StringComparison.Ordinal);
            Assert.Contains("Review this file", refreshedPage, StringComparison.Ordinal);
            Assert.Contains("/Received/", refreshedPage, StringComparison.Ordinal);
        }
        var confirmationPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.DoesNotContain("could not be added", confirmationPage, StringComparison.Ordinal);
        Assert.DoesNotContain("No single case matched", confirmationPage, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing from this submission", confirmationPage, StringComparison.Ordinal);
        Assert.DoesNotContain("nothing left in this submission", confirmationPage, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"reason\"", confirmationPage, StringComparison.Ordinal);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var groups = scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
            var group = await groups.GetAsync(groupId, CancellationToken.None);
            Assert.NotNull(group);
            var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            var statuses = scope.ServiceProvider.GetRequiredService<IQueuedIntakeStatusQueries>();
            var linked = 0;
            foreach (var member in group!.Members)
            {
                var status = await statuses.GetAsync(member.StagedReceiptId, CancellationToken.None);
                Assert.NotNull(status);
                var receipt = await receipts.GetAsync(
                    status!.ProcessedReceiptId ?? status.StagedReceiptId, CancellationToken.None);
                Assert.NotNull(receipt);
                if (receipt!.CurrentCaseId == caseId)
                {
                    linked++;
                }
            }
            Assert.Equal(interruptAfterFirstMember ? 1 : 2, linked);
        }

        // Replay the original HTTP form after every member is complete.  The
        // handler must not discard this exact reviewed roster merely because
        // the page now has no open decision card.
        var replayFields = new Dictionary<string, string>
        {
            ["caseId"] = caseId.ToString("D"),
            ["reference"] = caseReference,
            ["operationId"] = confirmation.OperationId.ToString("D"),
            ["caseVersion"] = confirmation.CaseVersion.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var receiptVersion in confirmation.ReceiptVersions)
        {
            replayFields[$"receiptVersions[{receiptVersion.Key:D}]"] = receiptVersion.Value.ToString(CultureInfo.InvariantCulture);
        }
        var replay = await PostGroupHandlerAsync(
            attachmentClient, $"/Upload/Group/{groupId:D}?handler=AttachGroup", replayFields);
        Assert.Equal(HttpStatusCode.Redirect, replay);
        foreach (var receiptId in confirmation.ReceiptVersions.Keys)
        {
            await AssertLinkedAsync(factory, receiptId, caseId);
        }
        var groupItem = await linkScope.ServiceProvider.GetRequiredService<IUnidentifiedStore>()
            .GetByOriginAsync(UnidentifiedOrigin.SubmissionGroup(groupId), CancellationToken.None);
        Assert.NotNull(groupItem);
        Assert.Equal(UnidentifiedState.Resolved, groupItem!.State);
        Assert.Equal(UnidentifiedResolutionTargetKind.InstructionCase, groupItem.ResolutionTargetKind);
        Assert.Equal(caseId.ToString("N"), groupItem.ResolutionTargetId);
        var afterPage = await IntakeWebDriver.GetHtmlAsync(client, $"/Upload/Group/{groupId:D}");
        Assert.DoesNotContain("This submission", afterPage, StringComparison.Ordinal);
        Assert.Contains("Open case", afterPage, StringComparison.Ordinal);
        Assert.Contains($"/Cases/Details/{caseId:D}", afterPage, StringComparison.Ordinal);
        await using var db = await linkScope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var historyCount = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM IntakeMutationHistory").SingleAsync();
        Assert.Equal(HttpStatusCode.Redirect, await PostGroupHandlerAsync(
            attachmentClient, $"/Upload/Group/{groupId:D}?handler=AttachGroup", replayFields));
        Assert.Equal(historyCount, await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM IntakeMutationHistory").SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkingMemberWithAnOpenSiblingWithholdsTheGroupDecision(bool offerCreation)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var uploadClient = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(uploadClient);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            uploadClient,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64)),
                ("close-up.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64))
            ]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);

        IntakeSubmissionGroup group;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            group = (await scope.ServiceProvider
                .GetRequiredService<IIntakeSubmissionGroupStore>()
                .GetAsync(groupId, CancellationToken.None))!;
        }

        using var pageFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUploadOutcomeQueries>();
                services.AddSingleton<IUploadOutcomeQueries>(
                    new WorkingAndOpenGroupOutcomes(group.Members[0].StagedReceiptId, offerCreation));
            }));
        using var pageClient = pageFactory.CreateClient();

        var html = await IntakeWebDriver.GetHtmlAsync(pageClient, $"/Upload/Group/{groupId:D}");

        Assert.Contains("data-auto-refresh=\"2000\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"group-decision-title\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Add the submission to this case", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Create a vehicle-image case", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-case-search", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Add to an existing case", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Create a new case", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("processing")]
    [InlineData("failed")]
    [InlineData("missing-receipt")]
    [InlineData("missing-member")]
    [InlineData("omitted-ready-member")]
    [InlineData("different-case")]
    [InlineData("different-operation")]
    [InlineData("stale-member")]
    public async Task IncompleteGroupPostChangesNoAssociationOrHistory(string condition)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "XY34 ZZZ", "GROUP-READINESS-01");
        var firstEmail = IntakeTestEvidence.CreateEmail("first-instruction.eml",
            "QDOS instruction\r\nClaimant Name: Attach Claimant\r\nClaim Number: GROUP-DOC-01\r\nVehicle Registration: CD34 EFG");
        var secondEmail = IntakeTestEvidence.CreateEmail("second-instruction.eml",
            "QDOS instruction\r\nClaimant Name: Attach Claimant\r\nClaim Number: GROUP-DOC-02\r\nVehicle Registration: CD34 EFG");
        (string, string, byte[])[] files = condition is "different-case" or "different-operation" or "stale-member"
            ? [(firstEmail.FileName, firstEmail.MediaType, firstEmail.Content),
               (secondEmail.FileName, secondEmail.MediaType, secondEmail.Content)]
            : [("overview.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64)),
               ("close-up.png", "image/png", Convert.FromBase64String(MultiFormatFixture.TinyPngBase64))];
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(client,
            form.AntiforgeryToken, form.ExternalReceiptToken, files);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);
        await using var scope = factory.Services.CreateAsyncScope();
        await IntakeWebDriver.ReconcileGroupedImageIntakeAsync(scope.ServiceProvider);
        var group = (await scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>()
            .GetAsync(groupId, CancellationToken.None))!;
        var statuses = scope.ServiceProvider.GetRequiredService<IQueuedIntakeStatusQueries>();
        var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
        var before = new List<IntakeReceipt>();
        foreach (var member in group.Members)
        {
            var status = (await statuses.GetAsync(member.StagedReceiptId, CancellationToken.None))!;
            before.Add((await receipts.GetAsync(status.ProcessedReceiptId!.Value, CancellationToken.None))!);
        }
        var reviewedVersions = before.ToDictionary(receipt => receipt.Id, receipt => receipt.Version);
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var sibling = group.Members[1].StagedReceiptId;
        if (condition is "processing" or "failed")
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IntakeWorkItems SET State = {condition} WHERE StagedReceiptId = {sibling}");
        }
        else if (condition == "missing-receipt")
        {
            var unavailableReceipt = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IntakeWorkItems SET ProcessedReceiptId = {unavailableReceipt} WHERE StagedReceiptId = {sibling}");
        }
        else if (condition == "missing-member")
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IntakeSubmissionGroups SET ExpectedMemberCount = ExpectedMemberCount + 1 WHERE Id = {groupId}");
        }
        else if (condition == "stale-member")
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE IntakeReceipts SET Version = Version + 1 WHERE Id = {before[1].Id}");
        }
        else if (condition is "different-case" or "different-operation")
        {
            var priorCaseId = condition == "different-case"
                ? await ImageIntakeTestData.SeedInstructionCaseAsync(factory, client, "EF56 HJK", "GROUP-OTHER-01")
                : caseId;
            var actor = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
            var lease = await scope.ServiceProvider.GetRequiredService<IAcquireCaseEditLease>().ExecuteAsync(
                new(priorCaseId, await CaseVersionAsync(factory, priorCaseId), actor, "group-prior-decision-lease"),
                CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ILinkIntake>().ExecuteAsync(
                new(before[1].Id, priorCaseId, before[1].Version, lease.Version, lease.Token, actor,
                    "group-prior-decision", "Staff previously associated this separate instruction."));
        }
        before[1] = (await receipts.GetAsync(before[1].Id, CancellationToken.None))!;
        var caseVersion = await CaseVersionAsync(factory, caseId);
        Assert.Null(before[0].CurrentCaseId);
        if (condition is "different-case" or "different-operation" or "stale-member")
        {
            var actor = ActionActor.Staff(DevelopmentOfflineIdentity.AdministratorId, [StaffRole.Administrator]);
            var available = await scope.ServiceProvider.GetRequiredService<IIntakeAssociationDestinationQueries>()
                .GetAsync(before[0], caseId, actor, CancellationToken.None);
            Assert.NotNull(available);
            Assert.Equal(caseVersion, available.Version);
        }
        var historyBefore = await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM IntakeMutationHistory").SingleAsync();
        var fields = new Dictionary<string, string>
        {
            ["caseId"] = caseId.ToString("D"),
            ["reference"] = await CaseReferenceAsync(factory, caseId),
            ["operationId"] = Guid.NewGuid().ToString("D"),
            ["caseVersion"] = caseVersion.ToString(CultureInfo.InvariantCulture),
            [$"receiptVersions[{before[0].Id:D}]"] = reviewedVersions[before[0].Id].ToString(CultureInfo.InvariantCulture)
        };
        Assert.Equal(HttpStatusCode.OK, await PostGroupHandlerAsync(
            client, $"/Upload/Group/{groupId:D}?handler=AttachGroup", fields));
        if (condition != "omitted-ready-member")
        {
            fields[$"receiptVersions[{before[1].Id:D}]"] = reviewedVersions[before[1].Id].ToString(CultureInfo.InvariantCulture);
            Assert.Equal(HttpStatusCode.OK, await PostGroupHandlerAsync(
                client, $"/Upload/Group/{groupId:D}?handler=AttachGroup", fields));
        }
        foreach (var original in before)
        {
            var after = (await receipts.GetAsync(original.Id, CancellationToken.None))!;
            Assert.Equal(original.CurrentCaseId, after.CurrentCaseId);
            Assert.Equal(original.Version, after.Version);
            Assert.Equal(original.ManualAssociationOperationKey, after.ManualAssociationOperationKey);
        }
        Assert.Equal(caseVersion, await CaseVersionAsync(factory, caseId));
        Assert.Equal(historyBefore, await db.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM IntakeMutationHistory").SingleAsync());
    }

    private static string GroupAttachForm(string page)
    {
        var action = page.IndexOf("handler=AttachGroup", StringComparison.Ordinal);
        Assert.True(action >= 0, "The group attachment confirmation form is missing.");
        var start = page.LastIndexOf("<form", action, StringComparison.Ordinal);
        var end = page.IndexOf("</form>", action, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "The group attachment form is incomplete.");
        return page[start..end];
    }

    private static IEnumerable<int> SplitOccurrences(string haystack, string needle)
    {
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            yield return index;
            index += needle.Length;
        }
    }

    private static async Task<HttpStatusCode> PostGroupHandlerAsync(
        HttpClient client,
        string url,
        Dictionary<string, string> fields)
    {
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        fields["__RequestVerificationToken"] = token;
        using var response = await client.PostAsync(url, new FormUrlEncodedContent(fields));
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail("500: " + body[..Math.Min(body.Length, 4000)]);
        }
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> PostAttachAsync(
        HttpClient client,
        string url,
        Guid receiptId,
        Guid? caseId = null,
        string? reference = null,
        Guid? operationId = null,
        long? receiptVersion = null,
        long? caseVersion = null)
    {
        var token = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        var fields = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["receiptId"] = receiptId.ToString("D"),
            ["operationId"] = (operationId ?? Guid.NewGuid()).ToString("D")
        };
        if (receiptVersion is { } reviewedReceiptVersion)
        {
            fields["receiptVersion"] = reviewedReceiptVersion.ToString(CultureInfo.InvariantCulture);
        }
        if (caseVersion is { } reviewedCaseVersion)
        {
            fields["caseVersion"] = reviewedCaseVersion.ToString(CultureInfo.InvariantCulture);
        }
        if (caseId is { } chosen)
        {
            fields["caseId"] = chosen.ToString("D");
        }
        if (reference is not null)
        {
            fields["reference"] = reference;
        }

        using var response = await client.PostAsync(url, new FormUrlEncodedContent(fields));
        return response.StatusCode;
    }

    private static async Task<(long ReceiptVersion, long? CaseVersion)> AttachmentVersionsAsync(
        IntakeWebApplicationFactory factory,
        Guid receiptId,
        Guid? caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        if (caseId is null)
        {
            return (receipt!.Version, null);
        }

        var workflow = await scope.ServiceProvider
            .GetRequiredService<ICaseWorkflowStore>()
            .GetAsync(caseId.Value, CancellationToken.None);
        Assert.NotNull(workflow);
        return (receipt!.Version, workflow!.Version);
    }

    private static async Task<GroupAttachConfirmation> ConfirmGroupAttachAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        Guid groupId,
        Guid caseId,
        string reference)
    {
        var operationId = Guid.NewGuid();
        var receiptVersions = new Dictionary<Guid, long>();
        var fields = new Dictionary<string, string>
        {
            ["reference"] = reference,
            ["operationId"] = operationId.ToString("D")
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var group = await scope.ServiceProvider
                .GetRequiredService<IIntakeSubmissionGroupStore>()
                .GetAsync(groupId, CancellationToken.None);
            Assert.NotNull(group);
            var statuses = scope.ServiceProvider.GetRequiredService<IQueuedIntakeStatusQueries>();
            var receipts = scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>();
            foreach (var member in group!.Members)
            {
                var status = await statuses.GetAsync(member.StagedReceiptId, CancellationToken.None);
                Assert.NotNull(status);
                var receiptId = status!.ProcessedReceiptId ?? status.StagedReceiptId;
                var receipt = await receipts.GetAsync(receiptId, CancellationToken.None);
                Assert.NotNull(receipt);
                fields[$"receiptVersions[{receiptId:D}]"] = receipt!.Version.ToString(CultureInfo.InvariantCulture);
                receiptVersions[receiptId] = receipt.Version;
            }
        }

        var prepare = await PostGroupHandlerAsync(
            client, $"/Upload/Group/{groupId:D}?handler=AttachGroup", fields);
        Assert.Equal(HttpStatusCode.OK, prepare);

        var caseVersion = await CaseVersionAsync(factory, caseId);
        fields["caseId"] = caseId.ToString("D");
        fields["caseVersion"] = caseVersion.ToString(CultureInfo.InvariantCulture);
        fields["__RequestVerificationToken"] = await IntakeWebDriver.GetAntiforgeryTokenAsync(client);
        using var response = await client.PostAsync(
            $"/Upload/Group/{groupId:D}?handler=AttachGroup", new FormUrlEncodedContent(fields));
        var body = await response.Content.ReadAsStringAsync();
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        return new(response.StatusCode, operationId, receiptVersions, caseVersion, body);
    }

    private static async Task<long> CaseVersionAsync(
        IntakeWebApplicationFactory factory,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var workflow = await scope.ServiceProvider
            .GetRequiredService<ICaseWorkflowStore>()
            .GetAsync(caseId, CancellationToken.None);
        Assert.NotNull(workflow);
        return workflow!.Version;
    }

    private sealed record GroupAttachConfirmation(
        HttpStatusCode StatusCode,
        Guid OperationId,
        IReadOnlyDictionary<Guid, long> ReceiptVersions,
        long CaseVersion,
        string Body);

    private static async Task AssertLinkedAsync(
        IntakeWebApplicationFactory factory,
        Guid receiptId,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(receipt);
        Assert.Equal(caseId, receipt!.CurrentCaseId);
        Assert.NotNull(receipt.ManualAssociationVersion);
    }

    private sealed class InterruptSecondLink(ILinkIntake inner, bool interrupt) : ILinkIntake
    {
        private int _calls;

        public Task ExecuteAsync(LinkIntakeRequest request, CancellationToken cancellationToken = default)
        {
            if (interrupt && Interlocked.Increment(ref _calls) == 2)
            {
                throw new InvalidOperationException("Injected interruption after the first committed member.");
            }
            return inner.ExecuteAsync(request, cancellationToken);
        }
    }

    private sealed class WorkingAndOpenGroupOutcomes(Guid workingStagedReceiptId, bool offerCreation) : IUploadOutcomeQueries
    {
        public Task<UploadOutcomeView> BuildAsync(
            QueuedIntakeStatus status,
            Guid? submissionGroupId,
            ActionActor actor,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(status.StagedReceiptId == workingStagedReceiptId
                ? new UploadOutcomeView(
                    UploadOutcomeKind.Working,
                    "Processing",
                    "The submission is being processed.",
                    null,
                    null)
                : new UploadOutcomeView(
                    offerCreation ? UploadOutcomeKind.ReadyToCreate : UploadOutcomeKind.NeedsReview,
                    offerCreation ? "Choose a case destination" : "Needs review",
                    "This needs a staff decision.",
                    offerCreation
                        ? new("Create a new case", $"/Cases/Create?receiptId={status.ProcessedReceiptId:D}")
                        : new("Review", $"/Unidentified/{Guid.NewGuid():D}"),
                    null,
                    new(status.ProcessedReceiptId ?? status.StagedReceiptId, 0)));
    }
}
