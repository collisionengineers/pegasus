using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The rules ruled on the photo capture follow-ups (operator, 10 November
/// 2026): a typed mileage over a DVSA estimate (R843), a closed Case (R847),
/// the VIN once a link is handed on (R874, R808) and story order (R854).
/// </summary>
public sealed partial class PhotoCaptureWebTests
{
    private const string RulesNumberPlate =
        "kind=plate&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private const string RulesDashboard =
        "kind=dashboard&position=1&takenAt=2031-05-06T10%3A20%3A00.000Z&live=true&original=false";

    private const string RulesCannotTakeALink = "The Case cannot take a photo link.";

    /// <summary>
    /// A mileage typed on a photo link replaces the DVSA estimate in its own
    /// row, and the estimate's unit with it, so the Case holds one mileage
    /// fact; a later lookup, which fills only a field with no fact, leaves it
    /// (operator, 10 November 2026; R843). The fill's history keeps the
    /// estimate it replaced.
    /// </summary>
    [Fact]
    public async Task ATypedMileageReplacesTheDvsaEstimateAndALaterLookupLeavesIt()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await SeedCaseRegistrationAsync(factory, caseId, "RY65OFG");
        await SeedDvsaMileageEstimateAsync(factory, caseId, "145137");
        var link = await IssueLinkAsync(factory, caseId);
        using (var typed = await SendAsync(
            client,
            HttpMethod.Post,
            $"{Api}/details",
            link.Token,
            new StringContent(
                JsonSerializer.Serialize(new { details = new { mileage = 48250 } }),
                Encoding.UTF8,
                "application/json")))
        {
            Assert.Equal(HttpStatusCode.OK, typed.StatusCode);
        }

        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        await StandingCaptureDriver.CompleteAsync(client, link.Token.Value);
        await StandingCaptureDriver.SettleAsync(factory);

        await AssertTypedMileageAsync(factory, caseId);
        await AssertReplacedEstimateRecordedAsync(factory, caseId, "145137");

        await RecordDvsaLookupAsync(factory, caseId, "RY65OFG", miles: 52_000);

        await AssertTypedMileageAsync(factory, caseId);
    }

    /// <summary>
    /// Send on a closed Case shows "The Case cannot take a photo link." and no
    /// form, and a post issues nothing (operator, 10 November 2026; R847).
    /// </summary>
    [Fact]
    public async Task SendRefusesAClosedCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await MarkCaseClosedAsync(factory, caseId);
        var send = $"/PhotoCapture/Send/{caseId:D}";

        var opened = await IntakeWebDriver.GetHtmlAsync(client, send);
        Assert.Contains(RulesCannotTakeALink, opened, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"photo-link-send\"", opened, StringComparison.Ordinal);

        using var posted = await PostStaffFormAsync(client, send, new Dictionary<string, string>
        {
            ["OperationKey"] = NewOperationKey(),
            ["Mode"] = nameof(PhotoCaptureMode.Guided),
            ["Days"] = "7",
            ["LiveCameraOnly"] = "true",
            ["OriginalsInBackground"] = "false",
            ["Submit"] = Pegasus.Web.Pages.PhotoCapture.SendModel.ToCopy
        });
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        var answered = await posted.Content.ReadAsStringAsync();
        Assert.Contains(RulesCannotTakeALink, answered, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"photo-link-send\"", answered, StringComparison.Ordinal);
        Assert.Empty(await CaptureIdsAsync(factory, caseId));
    }

    /// <summary>
    /// A link sent before its Case closed keeps working (R847): it still takes
    /// a photo, and it can still be sent again.
    /// </summary>
    [Fact]
    public async Task ALinkSentBeforeTheCloseStillUploads()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        await MarkCaseClosedAsync(factory, caseId);

        using (var sent = await PutItemAsync(client, link.Token, Guid.NewGuid(), FirstOverview, MultiFormatFixture.Photograph(1)))
        {
            Assert.Equal(HttpStatusCode.Created, sent.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var reissued = await scope.ServiceProvider.GetRequiredService<PhotoLinkActions>().ReissueAsync(
            Issuer, link.Capture.Id, PhotoLinkTokenPurpose.Copy, isReminder: false, NewOperationKey(), CancellationToken.None);
        Assert.Equal(link.Capture.Id, reissued.Capture.Id);
    }

    /// <summary>
    /// A Bodyshop's quick Case link handed on to its customer asks no VIN:
    /// not when the customer presses Send, not on its list row, and not in
    /// the Case's story, while one kept by the bodyshop still owes it
    /// (operator, 10 November 2026; R874, R808).
    /// </summary>
    [Fact]
    public async Task AHandedOnBodyshopLinkIsCompleteWithoutAVin()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        await SeedCaseRegistrationAsync(factory, caseId, "AB12 CDE");
        var handedOn = await IssueQuickLinkAsync(factory, caseId, PhotoCaptureQuickLinkFor.Bodyshop);
        var kept = await IssueQuickLinkAsync(factory, caseId, PhotoCaptureQuickLinkFor.Bodyshop);
        string customerToken;
        using (var handOff = await SendAsync(
            client, HttpMethod.Post, $"{Api}/handoff", handedOn.Token, new StringContent("{}", Encoding.UTF8, "application/json")))
        {
            Assert.Equal(HttpStatusCode.Created, handOff.StatusCode);
            var url = (await JsonAsync(handOff)).GetProperty("url").GetString()
                ?? throw new InvalidOperationException("The hand-on answered no link.");
            customerToken = url[(url.LastIndexOf('/') + 1)..];
        }

        Assert.DoesNotContain(
            await MissingOnCompleteAsync(client, new CaptureLinkSecret(customerToken)),
            missing => missing.Code == "vin");
        Assert.Contains(await MissingOnCompleteAsync(client, kept.Token), missing => missing.Code == "vin");

        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IPhotoCaptureQueries>();
        var handedOnRow = await queries.GetRowAsync(handedOn.Capture.Id, CancellationToken.None);
        Assert.NotNull(handedOnRow);
        Assert.True(handedOnRow!.SentOnToCustomer);
        Assert.DoesNotContain("No VIN photo", handedOnRow.MissingItems);
        var keptRow = await queries.GetRowAsync(kept.Capture.Id, CancellationToken.None);
        Assert.Contains("No VIN photo", keptRow!.MissingItems);
        var stories = await queries.ListForCaseAsync(caseId, CancellationToken.None);
        Assert.True(Assert.Single(stories, story => story.Capture.Id == handedOn.Capture.Id).HandedOn);
        Assert.False(Assert.Single(stories, story => story.Capture.Id == kept.Capture.Id).HandedOn);
    }

    /// <summary>
    /// A photo link's photos nobody has ordered print in the link's story
    /// (overviews, then the number plate, then the dashboard) whatever order
    /// their uploads arrived in (operator, 10 November 2026; R854).
    /// </summary>
    [Fact]
    public async Task AnUnorderedPhotoLinkPrintsInItsStoryOrder()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        var dashboard = await SendAndDrainAsync(factory, client, link, RulesDashboard, seed: 1);
        var secondOverview = await SendAndDrainAsync(factory, client, link, SecondOverview, seed: 2);
        var plate = await SendAndDrainAsync(factory, client, link, RulesNumberPlate, seed: 3);
        var firstOverview = await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 4);
        await ArrivedInOrderAsync(factory, caseId, dashboard, secondOverview, plate, firstOverview);

        var printed = await PrintedFileNamesAsync(factory, caseId);

        string[] story = [firstOverview.FileName, secondOverview.FileName, plate.FileName, dashboard.FileName];
        Assert.Equal(story, printed);
    }

    /// <summary>
    /// The Case's preparation read ties each photo link photograph to its
    /// story in one batched read: the same statements for three photos as
    /// for one, and the capture items read at most once.
    /// </summary>
    [Fact]
    public async Task TheCasesPreparationReadDoesNotGrowWithAPhotoLinksPhotos()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory("Development", true, commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await AssociatedMailEvidenceIntegrationTests.SeedCaseAsync(factory);
        var link = await IssueLinkAsync(factory, caseId);
        await SendAndDrainAsync(factory, client, link, FirstOverview, seed: 1);
        var withOne = await PreparationReadAsync(factory, counter, caseId);
        await SendAndDrainAsync(factory, client, link, SecondOverview, seed: 2);
        await SendAndDrainAsync(factory, client, link, RulesNumberPlate, seed: 3);
        var withThree = await PreparationReadAsync(factory, counter, caseId);

        Assert.True(withOne.Count > 0, "The interceptor observed no commands at all.");
        Assert.True(
            withOne.Count == withThree.Count,
            $"The preparation read grew with the photos: {withOne.Count} commands for one and {withThree.Count} for three."
            + Environment.NewLine + "One photo:" + Environment.NewLine + withOne.Description
            + Environment.NewLine + "Three photos:" + Environment.NewLine + withThree.Description);
        Assert.True(withThree.StoryReads <= 1, withThree.Description);
    }

    private static async Task AssertTypedMileageAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var facts = await db.CaseDataFields.AsNoTracking()
            .Where(value => value.WorkId == caseId
                && value.ValueKind == CaseDataCodes.Fact
                && (value.FieldName == CaseDataFieldNames.VehicleMileage || value.FieldName == CaseDataFieldNames.VehicleMileageUnit))
            .ToListAsync();
        var mileage = Assert.Single(facts, value => value.FieldName == CaseDataFieldNames.VehicleMileage);
        Assert.Equal("48250", mileage.Value);
        Assert.Equal(CaseDataCodes.PhotoLink, mileage.SourceKind);
        Assert.Equal(PhotoCaptureCaseFields.PolicyKey, mileage.PolicyKey);
        var unit = Assert.Single(facts, value => value.FieldName == CaseDataFieldNames.VehicleMileageUnit);
        Assert.Equal(nameof(VehicleMileageUnit.Miles), unit.Value);
        Assert.Equal(CaseDataCodes.PhotoLink, unit.SourceKind);
    }

    /// <summary>
    /// The photo link's one fill event records, as its before value, the
    /// estimate's mileage and unit it replaced with their source and key.
    /// </summary>
    private static async Task AssertReplacedEstimateRecordedAsync(IntakeWebApplicationFactory factory, Guid caseId, string miles)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var aggregateId = caseId.ToString("D");
        var history = Assert.Single(await db.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateId == aggregateId && item.EventKind == EfPhotoCaptureCaseWriter.FilledEventType)
            .ToListAsync());
        Assert.NotNull(history.BeforeJson);
        using var before = JsonDocument.Parse(history.BeforeJson);
        var replaced = before.RootElement.GetProperty("facts").EnumerateArray()
            .ToDictionary(fact => fact.GetProperty("fieldName").GetString()!, fact => fact);
        Assert.Equal(2, replaced.Count);
        var mileage = replaced[CaseDataFieldNames.VehicleMileage];
        Assert.Equal(miles, mileage.GetProperty("value").GetString());
        Assert.Equal(CaseDataCodes.VehicleLookup, mileage.GetProperty("sourceKind").GetString());
        Assert.Equal(MotMileageEstimationPolicy.MethodKey, mileage.GetProperty("policyKey").GetString());
        var unit = replaced[CaseDataFieldNames.VehicleMileageUnit];
        Assert.Equal(nameof(VehicleMileageUnit.Miles), unit.GetProperty("value").GetString());
        Assert.Equal(CaseDataCodes.VehicleLookup, unit.GetProperty("sourceKind").GetString());
    }

    /// <summary>
    /// The Case's mileage is the DVSA estimate, as a lookup writes it: a fact
    /// and its unit under the estimate's own key, in place of the seeded
    /// confirmed mileage.
    /// </summary>
    private static async Task SeedDvsaMileageEstimateAsync(IntakeWebApplicationFactory factory, Guid caseId, string miles)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        db.CaseDataFields.RemoveRange(await db.CaseDataFields
            .Where(value => value.WorkId == caseId
                && (value.FieldName == CaseDataFieldNames.VehicleMileage || value.FieldName == CaseDataFieldNames.VehicleMileageUnit))
            .ToListAsync());
        var observation = Guid.NewGuid().ToString("D");
        db.AddRange(
            new CaseDataFieldEntity
            {
                WorkId = caseId, FieldName = CaseDataFieldNames.VehicleMileage, ValueKind = CaseDataCodes.Fact,
                ValueType = CaseDataCodes.Integer, Value = miles, SourceKind = CaseDataCodes.VehicleLookup,
                SourceIdentity = observation, SourceLabel = "offline-replay/fixture-v1",
                PolicyKey = MotMileageEstimationPolicy.MethodKey, PolicyVersion = MotMileageEstimationPolicy.MethodVersion
            },
            new CaseDataFieldEntity
            {
                WorkId = caseId, FieldName = CaseDataFieldNames.VehicleMileageUnit, ValueKind = CaseDataCodes.Fact,
                ValueType = CaseDataCodes.Text, Value = nameof(VehicleMileageUnit.Miles), SourceKind = CaseDataCodes.VehicleLookup,
                SourceIdentity = observation, SourceLabel = "offline-replay/fixture-v1",
                PolicyKey = MotMileageEstimationPolicy.MethodKey, PolicyVersion = MotMileageEstimationPolicy.MethodVersion
            });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// One DVLA and DVSA answer for the Case, recorded as the Worker records
    /// it (<c>VehicleLookupGapFillTests</c>): an MOT reading on the day it is
    /// looked up, so the answer carries a mileage it would fill an empty
    /// field with.
    /// </summary>
    private static async Task RecordDvsaLookupAsync(IntakeWebApplicationFactory factory, Guid caseId, string registration, long miles)
    {
        var now = IntakeWebApplicationFactory.FixedUtcNow;
        var workItemId = Guid.NewGuid();
        var operationKey = $"photo-link-lookup-{workItemId:N}";
        await using (var db = await (await factory.DatabaseAsync).CreateContextAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO ExternalWorkItems (Id, CaseId, Kind, OperationKey, State, AttemptCount, DueAtUtc) VALUES ({workItemId}, {caseId}, {ExternalWorkKinds.VehicleLookup}, {operationKey}, {"pending"}, {0}, {now})");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO VehicleLookupRequests (WorkItemId, CaseId, Registration, OperationKey, RequestFingerprint, RequestedByKind, RequestedBySubjectId, RequestedByRolesJson, RequestedAtUtc, ResultingCaseVersion) VALUES ({workItemId}, {caseId}, {registration}, {operationKey}, {new string('0', 64)}, {ActorKind.Automation.ToString()}, {"vehicle-lookup-reconciliation"}, {"[]"}, {now}, {0L})");
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var workStore = scope.ServiceProvider.GetRequiredService<IVehicleLookupWorkStore>();
        var claimed = Assert.IsType<VehicleLookupWorkItem>(
            await workStore.ClaimProcessingAsync(workItemId, now, TimeSpan.FromMinutes(5), CancellationToken.None));
        var result = new VehicleLookupResult(
            registration,
            VehicleLookupOutcome.Current,
            "offline-replay",
            "fixture-v1",
            $"photo-link-lookup-response-{workItemId:N}",
            now,
            now,
            now,
            new VehicleDetails("RENAULT", "CAPTUR", 2016, 1_461, "DIESEL"),
            [new MotTestObservation(DateOnly.FromDateTime(now.UtcDateTime), "PASSED", null, miles, VehicleMileageUnit.Miles)],
            null);
        await workStore.RecordOutcomeAsync(
            workItemId,
            claimed.LeaseToken!,
            VehicleLookupProcessedOutcome.Derive(result),
            VehicleLookupWorkState.Completed,
            null,
            now,
            CancellationToken.None);
    }

    /// <summary>Closes the Case as the Principal cancelling it does: a state the Case ribbon reads as closed.</summary>
    private static async Task MarkCaseClosedAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var workflow = await db.CaseWorkflows.SingleAsync(value => value.CaseId == caseId);
        workflow.State = nameof(CaseLifecycleState.PrincipalCancelled);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The items' images arrived on the Case one after another in the order
    /// given, a minute apart, as uploads that reach the Case at different
    /// times do; the test clock alone would give them all one time.
    /// </summary>
    private static async Task ArrivedInOrderAsync(IntakeWebApplicationFactory factory, Guid caseId, params PhotoCaptureItem[] items)
    {
        await using var db = await (await factory.DatabaseAsync).CreateContextAsync();
        var images = await (from occurrence in db.Set<DocumentOccurrenceEntity>()
                            join version in db.Set<DocumentVersionEntity>() on occurrence.VersionId equals version.Id
                            where occurrence.CaseId == caseId && occurrence.SemanticRole == DocumentSemanticRole.Image
                            select new { Occurrence = occurrence, version.FileName }).ToListAsync();
        for (var index = 0; index < items.Length; index++)
        {
            var fileName = items[index].FileName;
            Assert.Single(images, image => image.FileName == fileName).Occurrence.RecordedAtUtc =
                IntakeWebApplicationFactory.FixedUtcNow.AddMinutes(index);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>The file names of the report's images in the places they print.</summary>
    private static async Task<string[]> PrintedFileNamesAsync(IntakeWebApplicationFactory factory, Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var preparations = await scope.ServiceProvider.GetRequiredService<ICaseAssetPreparationQueries>()
            .ListForCaseAsync(caseId, CancellationToken.None);
        var names = preparations.ToDictionary(item => item.OccurrenceId, item => item.SourceFileName);
        return [.. CaseAssetPreparationPolicy.ForReport(preparations).Select(image => names[image.OccurrenceId])];
    }

    /// <summary>
    /// The Case's preparation read on its own: the commands it sent, how many
    /// of them read the capture items, and their text; every image it returns
    /// carries its story place.
    /// </summary>
    private static async Task<(int Count, int StoryReads, string Description)> PreparationReadAsync(
        IntakeWebApplicationFactory factory,
        SqlStatementCounter counter,
        Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<ICaseAssetPreparationQueries>();
        counter.Reset();
        var preparations = await queries.ListForCaseAsync(caseId, CancellationToken.None);
        var read = (counter.Count, counter.CountMentioning("[PhotoCaptureItems]"), counter.Describe());
        Assert.NotEmpty(preparations);
        Assert.All(preparations, item => Assert.NotNull(item.Story));
        return read;
    }
}
