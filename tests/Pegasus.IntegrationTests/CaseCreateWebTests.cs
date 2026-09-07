using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The create screen: the one caller of <c>IAcceptIntake</c>, and the only
/// route by which a hand-keyed item becomes a case.
/// </summary>
/// <remarks>
/// The hand-keyed path did not exist before. Extraction derives the
/// inspection-address suggestion from the receipt's own extracted candidates,
/// and the resolution store refused outright when there was no suggestion, so
/// an upload with no readable address had no route to a resolved address and
/// could not be accepted at all. Opening allocation to any registered
/// principal is meaningless without it.
/// </remarks>
[Trait("Category", "SqlServer")]
public sealed partial class CaseCreateWebTests
{
    private const string PrincipalCode = "HANDKEY";
    private static readonly DateTimeOffset RecordedAtUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor StaffActor = ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    /// <summary>
    /// CASE-003: a stale bookmark or a typed URL can reach this handler with
    /// no receiptId at all. Before the guard, LoadAsync passed Guid.Empty
    /// straight to IGetIntake, which throws — a 500 in production rather than
    /// the designed not-found page.
    /// </summary>
    [Fact]
    public async Task CreateWithNoReceiptIdReturnsNotFoundInsteadOfThrowing()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);

        using var response = await client.GetAsync("/Cases/Create");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateScreenDoesNotOfferManualAuditCreation()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);

        var form = await OpenCreateScreenAsync(client, receipt.Id);

        Assert.DoesNotContain("Standalone Audit", form.Html, StringComparison.Ordinal);
        Assert.DoesNotContain(">Audit</option>", form.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-standalone-audit-fields", form.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManualAuditPostIsRejectedWithoutCreatingACase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);

        var form = await OpenCreateScreenAsync(client, receipt.Id);
        var fields = KeyedFields();
        fields["CaseType"] = CaseType.Audit.ToString();

        using var response = await PostCreateAsync(client, form, fields);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Audits are created automatically from the retained Audit instruction and original report.",
            html,
            StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(factory.Services, "Cases"));
    }

    [Fact]
    public async Task HandKeyedCreateSuppliesTheAddressAndAllocatesTheCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var startingVersion = await ReadReceiptVersionAsync(factory.Services, receipt.Id);

        var form = await OpenCreateScreenAsync(client, receipt.Id);
        // Nothing was extracted, so every box is empty and there is no address
        // suggestion to fingerprint.
        Assert.Equal(string.Empty, form.Values["AddressSuggestionFingerprint"]);
        Assert.Contains("Nothing in this file said where the vehicle is", form.Html, StringComparison.Ordinal);

        using var response = await PostCreateAsync(client, form, KeyedFields());

        var caseId = AssertCaseRedirect(response);
        Assert.Equal(1, await CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await CountAsync(factory.Services, "CaseSequences"));
        Assert.Equal(1, await CountAsync(factory.Services, "CaseIntakeLinks"));

        // Three writes bump the receipt: the corrected draft, the supplied
        // address, and the acceptance itself.
        Assert.Equal(
            startingVersion + 3,
            await ReadReceiptVersionAsync(factory.Services, receipt.Id));

        // The address is retained as a resolution a person made, readable back
        // through the same snapshot the case data is built from.
        var snapshot = await ReadAddressSnapshotAsync(factory.Services, receipt.Id);
        Assert.Equal(InspectionAddressResolutionState.Supplied, snapshot.State);
        Assert.Equal("1 Example Street, Exampleton EX1 1EX", snapshot.ResolvedValue);
        Assert.Equal(DevelopmentOfflineIdentity.AdministratorId, snapshot.ResolvedByStaffId);
        Assert.Contains(
            "ext18-address-resolution/v1/",
            await ReadEvidenceJsonAsync(factory.Services, receipt.Id),
            StringComparison.Ordinal);

        // And it says so permanently, under its own event kind and the staff
        // subject who supplied it.
        var history = await ReadAddressHistoryAsync(factory.Services, receipt.Id);
        Assert.Equal("inspection_address_supplied", history.EventKind);
        Assert.Equal(
            DevelopmentOfflineIdentity.AdministratorId.ToString("D"),
            history.ActorSubjectId);

        // The case's own record of the address carries staff provenance, not
        // acceptance provenance: nobody extracted this.
        Assert.Equal(
            "staff_correction",
            await ReadInspectionAddressSourceKindAsync(factory.Services, caseId));
    }

    [Fact]
    public async Task CreateRefusesWhenAnIdentityCriticalFieldIsBlankAndLeavesTheReceiptUnchanged()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var startingVersion = await ReadReceiptVersionAsync(factory.Services, receipt.Id);

        var form = await OpenCreateScreenAsync(client, receipt.Id);
        var fields = KeyedFields();
        fields["VehicleRegistration"] = string.Empty;

        using var response = await PostCreateAsync(client, form, fields);
        var html = await response.Content.ReadAsStringAsync();

        // The check happens before anything is written. A correction that lands
        // and is then refused leaves the item blocked with no warning, which is
        // the trap this screen exists to close.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "Vehicle registration is needed before this item can become a case.",
            html,
            StringComparison.Ordinal);
        Assert.Equal(
            startingVersion,
            await ReadReceiptVersionAsync(factory.Services, receipt.Id));
        Assert.Equal(0, await CountEventsAsync(factory.Services, "intake_resolved"));
        Assert.Equal(0, await CountAsync(factory.Services, "Cases"));
    }

    /// <summary>
    /// Thin ordinary detail must not stop a reference being allocated.
    /// FRD-02 (intake and source identity): once safe processing establishes
    /// Principal and Case type, allocate the Case/PO and retain incomplete
    /// ordinary detail as `Not ready`. Refusing here instead left a real
    /// instruction with no case.
    /// </summary>
    [Fact]
    public async Task CreateAllocatesWhenOrdinaryDetailIsThin()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);

        var form = await OpenCreateScreenAsync(client, receipt.Id);
        var fields = KeyedFields();
        fields["VehicleMake"] = string.Empty;
        fields["VehicleModel"] = string.Empty;
        fields["VehicleMileage"] = string.Empty;
        fields["AccidentCircumstances"] = string.Empty;

        using var response = await PostCreateAsync(client, form, fields);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, await CountAsync(factory.Services, "Cases"));
        Assert.DoesNotContain(
            "is needed before this item can become a case",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task FinalCreateFailurePersistsAttemptAndOpensReasonedRecovery()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        var form = await OpenCreateScreenAsync(client, receipt.Id);

        using var response = await PostCreateAsync(client, form, KeyedFields());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Received/{receipt.Id:D}", response.Headers.Location?.OriginalString);
        Assert.Equal(0, await CountAsync(factory.Services, "Cases"));
        Assert.Equal(0, await CountAsync(factory.Services, "CaseSequences"));
        Assert.Equal(0, await CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await CountAsync(factory.Services, "IntakeAllocationAttempts"));
        await using var scope = factory.Services.CreateAsyncScope();
        var updated = Assert.IsType<IntakeReceipt>(
            await scope.ServiceProvider.GetRequiredService<IIntakeReceiptQueries>()
                .GetAsync(receipt.Id, CancellationToken.None));
        Assert.Equal(IntakeAllocationFailureKind.PrincipalUnavailable, updated.AllocationState?.FailureKind);
        Assert.True(updated.AllocationState?.CanRetry == true);
    }

    [Fact]
    public async Task RepeatedCreateSubmissionWithTheSameOperationIdAllocatesOneReference()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);

        var form = await OpenCreateScreenAsync(client, receipt.Id);
        using var first = await PostCreateAsync(client, form, KeyedFields());
        using var replay = await PostCreateAsync(client, form, KeyedFields());

        // The second post never reaches step 1: it reloads the item, sees the
        // case the first post allocated, and redirects to it from the
        // already-has-a-case guard. That is what makes one reference, allocated
        // once, and one correction the observable result of pressing the button
        // twice. (The replay of the steps themselves is what
        // CreateResumesAfterAMidSequenceFailureWithoutASecondCorrection covers,
        // where the first post allocated nothing.)
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal(AssertCaseRedirect(first), AssertCaseRedirect(replay));
        Assert.Equal(1, await CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await CountAsync(factory.Services, "CaseSequences"));
        Assert.Equal(1, await CountAsync(factory.Services, "CaseIntakeLinks"));
        Assert.Equal(1, await CountEventsAsync(factory.Services, "intake_resolved"));
    }

    [Fact]
    public async Task CreatePostUsesTheVersionRenderedInTheFormInsteadOfAReloadedVersion()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);

        var form = await OpenCreateScreenAsync(client, receipt.Id);
        var renderedVersion = long.Parse(
            form.Values["ExpectedReceiptVersion"],
            CultureInfo.InvariantCulture);
        Assert.Equal(
            await ReadReceiptVersionAsync(factory.Services, receipt.Id),
            renderedVersion);

        // The item moves underneath the operator between render and submit.
        await AdvanceReceiptVersionAsync(factory.Services, receipt.Id);
        using var response = await PostCreateAsync(client, form, KeyedFields());
        var html = await response.Content.ReadAsStringAsync();

        // Because the post claims the version it rendered rather than one it
        // reloads on the way in, the change is caught and nothing is allocated.
        // A reloaded version would have accepted silently against evidence the
        // operator never saw.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "This item changed while you were working.",
            html,
            StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(factory.Services, "Cases"));
        Assert.Equal(0, await CountAsync(factory.Services, "CaseIntakeLinks"));
    }

    [Fact]
    public async Task CreateResumesAfterAMidSequenceFailureWithoutASecondCorrection()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var failing = new FailFirstAddressResolutionStore();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IInspectionAddressResolutionStore>();
                services.AddSingleton<IInspectionAddressResolutionStore>(
                    provider => failing.Wrap(
                        ActivatorUtilities.CreateInstance<InspectionAddressResolutionStore>(provider)));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);

        var form = await OpenCreateScreenAsync(client, receipt.Id);
        using var failed = await PostCreateAsync(client, form, KeyedFields());
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        Assert.Equal(0, await CountAsync(factory.Services, "Cases"));

        // The same operation id and the same rendered version are re-submitted.
        // The correction replays rather than running again, so there is exactly
        // one correction in permanent history even though the operator pressed
        // the button twice.
        using var retried = await PostCreateAsync(client, form, KeyedFields());

        Assert.Equal(HttpStatusCode.Redirect, retried.StatusCode);
        _ = AssertCaseRedirect(retried);
        Assert.Equal(1, await CountAsync(factory.Services, "Cases"));
        Assert.Equal(1, await CountEventsAsync(factory.Services, "intake_resolved"));
    }

    [Fact]
    public async Task SupplyingAnAddressIsRefusedWhenExtractionFoundOne()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateReceiptWithExtractedAddressAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInspectionAddressResolutionStore>();

        // Where the document did say where the vehicle is, a person has to look
        // at that and accept or correct it. Letting a typed value quietly
        // displace extracted evidence is how an unexamined address gets onto a
        // case.
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.ResolveAsync(
                new(
                    receipt.Id,
                    receipt.Version,
                    null,
                    InspectionAddressStaffDecision.SupplyAddress,
                    "9 Somewhere Else, Elsewhereton",
                    StaffActor,
                    Guid.NewGuid(),
                    "supply-over-suggestion"),
                CancellationToken.None));

        Assert.Equal(
            "An extracted inspection-address suggestion must be accepted or corrected, not replaced.",
            refusal.Message);
    }

    [Fact]
    public async Task ASuppliedAddressCannotBeTheImageBasedAssessmentMode()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateDraftedReceiptAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInspectionAddressResolutionStore>();

        // The assessment mode is something an instruction says, never something
        // an operator can type their way into.
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.ResolveAsync(
                new(
                    receipt.Id,
                    receipt.Version,
                    null,
                    InspectionAddressStaffDecision.SupplyAddress,
                    "image based assessment",
                    StaffActor,
                    Guid.NewGuid(),
                    "supply-mode"),
                CancellationToken.None));
    }

    [Fact]
    public async Task ASuppliedAddressReplaysUnderItsOwnOperationIdentifier()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateDraftedReceiptAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInspectionAddressResolutionStore>();
        var operationId = Guid.NewGuid();
        InspectionAddressResolutionRequest Request(long version) => new(
            receipt.Id,
            version,
            null,
            InspectionAddressStaffDecision.SupplyAddress,
            "1 Example Street, Exampleton EX1 1EX",
            StaffActor,
            operationId,
            "supply-replay");

        var first = await store.ResolveAsync(Request(receipt.Version), CancellationToken.None);
        // A supplied resolution persists no fingerprint, so the replay check
        // must not compare one; if it did, every repeat would throw instead of
        // returning what it already wrote.
        var replay = await store.ResolveAsync(Request(receipt.Version), CancellationToken.None);

        Assert.Equal(InspectionAddressResolutionState.Supplied, first.State);
        Assert.Equal(InspectionAddressResolutionState.Supplied, replay.State);
        Assert.Equal(first.ResolvedValue, replay.ResolvedValue);
        Assert.Equal(first.ReceiptVersion, replay.ReceiptVersion);
    }

    [Fact]
    public async Task SuppliedAddressIsSupersededWhenAReevaluationProducesASuggestion()
    {
        using var factory = new IntakeWebApplicationFactory();
        var receipt = await CreateDraftedReceiptAsync(factory.Services);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInspectionAddressResolutionStore>();
        await store.ResolveAsync(
            new(
                receipt.Id,
                receipt.Version,
                null,
                InspectionAddressStaffDecision.SupplyAddress,
                "1 Example Street, Exampleton EX1 1EX",
                StaffActor,
                Guid.NewGuid(),
                "supply-superseded"),
            CancellationToken.None);
        var supplied = await store.GetAsync(receipt.Id, CancellationToken.None);
        Assert.Equal(InspectionAddressResolutionState.Supplied, supplied!.State);

        // A supplied address answers the absence of evidence. When a later
        // re-evaluation finds some, that absence is no longer true, so the
        // supplied value is superseded and staff are made to look again rather
        // than a case being created against a hand-typed address the source now
        // contradicts.
        await AddExtractedAddressCandidateAsync(factory.Services, receipt.Id);
        var reread = await store.GetAsync(receipt.Id, CancellationToken.None);

        Assert.Equal(InspectionAddressResolutionState.Suggested, reread!.State);
        Assert.Null(reread.ResolvedValue);
    }

    [Fact]
    public async Task CreateScreenRefusesAnItemThatAlreadyHasACase()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);
        await SeedPrincipalAsync(factory.Services, PrincipalCode);
        var form = await OpenCreateScreenAsync(client, receipt.Id);
        using var created = await PostCreateAsync(client, form, KeyedFields());
        var caseId = AssertCaseRedirect(created);

        // A reference is allocated once and never reused, so opening the screen
        // again shows the case rather than offering a second one.
        using var reopened = await client.GetAsync($"/Cases/Create?receiptId={receipt.Id}");

        Assert.Equal(HttpStatusCode.Redirect, reopened.StatusCode);
        Assert.Equal($"/Cases/{caseId:D}", reopened.Headers.Location!.OriginalString);
        Assert.Equal(1, await CountAsync(factory.Services, "Cases"));
    }

    [Fact]
    public async Task ReceivedItemNoLongerAcceptsOrResolvesAndLinksToTheCreateScreenInstead()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);

        using var details = await client.GetAsync($"/Received/{receipt.Id}");
        var html = await details.Content.ReadAsStringAsync();

        // Exactly one acceptance caller, in executable form.
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.DoesNotContain("handler=Accept", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Accept and allocate case reference", html, StringComparison.Ordinal);
        Assert.Contains($"/Cases/Create?receiptId={receipt.Id}", html, StringComparison.OrdinalIgnoreCase);

        foreach (var handler in new[] { "Accept", "AcceptAddress", "CorrectAddress" })
        {
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(html)
            });
            using var response = await client.PostAsync(
                $"/Received/{receipt.Id}?handler={handler}",
                content);
            // Razor Pages has no handler to match, so nothing is executed and
            // nothing succeeds. What matters is that the removed handlers can
            // no longer be reached, not which unsuccessful status the framework
            // picks for a name that does not exist.
            Assert.True(
                response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Redirect),
                $"Handler '{handler}' should no longer exist; it answered {(int)response.StatusCode}.");
        }

        Assert.Equal(0, await CountAsync(factory.Services, "Cases"));
        Assert.Equal(0, await CountEventsAsync(factory.Services, "intake_resolved"));
    }

    [Fact]
    public async Task CreateScreenIsReachableWhereverUploadIs()
    {
        // The received-item surface is gated; creating a case is not, because
        // it is a staff action in every runtime profile. The route sits outside
        // /Intake so it inherits no gate.
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            localIntakeEnabled: false);
        using var client = IntakeWebDriver.CreateClient(factory);
        var receipt = await CreateBareReceiptAsync(factory.Services);

        using var gated = await client.GetAsync($"/Received/{receipt.Id}");
        using var createScreen = await client.GetAsync($"/Cases/Create?receiptId={receipt.Id}");

        Assert.Equal(HttpStatusCode.NotFound, gated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, createScreen.StatusCode);
    }

    private static Dictionary<string, string> KeyedFields() => new()
    {
        ["Reason"] = "Keyed the instruction detail from the retained document.",
        ["PrincipalCode"] = PrincipalCode,
        ["CaseType"] = CaseType.Inspection.ToString(),
        ["ClaimantName"] = "Hand Keyed Claimant",
        ["ClaimNumber"] = "HK-2031-001",
        ["VehicleRegistration"] = "AB12CDE",
        ["VehicleMake"] = "Example Make",
        ["VehicleModel"] = "Example Model",
        ["VehicleMileage"] = "12345",
        ["AccidentCircumstances"] = "Keyed circumstances from the retained document.",
        ["DateOfIncident"] = "2031-03-04",
        ["InstructionDate"] = "2031-03-05",
        ["InspectionDate"] = "2031-03-20",
        ["InspectionAddress"] = "1 Example Street, Exampleton EX1 1EX",
        ["AddressChoice"] = nameof(Pegasus.Web.Pages.Cases.CreateModel.AddressChoiceKind.UseEnteredAddress),
        ["InstructionComplete"] = bool.TrueString,
        ["ImagesComplete"] = bool.TrueString,
        ["InstructionConfirmedByStaff"] = bool.TrueString,
        ["ImagesConfirmedByStaff"] = bool.TrueString
    };

    private static async Task<CreateForm> OpenCreateScreenAsync(HttpClient client, Guid receiptId)
    {
        using var response = await client.GetAsync($"/Cases/Create?receiptId={receiptId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        return new(
            html,
            new()
            {
                ["__RequestVerificationToken"] = AntiforgeryToken(html),
                ["ReceiptId"] = InputValue(html, "ReceiptId"),
                ["OperationId"] = InputValue(html, "OperationId"),
                ["ExpectedReceiptVersion"] = InputValue(html, "ExpectedReceiptVersion"),
                ["AddressSuggestionFingerprint"] = InputValue(html, "AddressSuggestionFingerprint")
            });
    }

    private static Task<HttpResponseMessage> PostCreateAsync(
        HttpClient client,
        CreateForm form,
        Dictionary<string, string> fields)
    {
        var payload = new Dictionary<string, string>(form.Values);
        foreach (var (key, value) in fields)
        {
            payload[key] = value;
        }

        return client.PostAsync("/Cases/Create?handler=Create", new FormUrlEncodedContent(payload));
    }

    private static Guid AssertCaseRedirect(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Redirect)
        {
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var summary = ValidationSummaryRegex().Matches(body)
                .Cast<Match>()
                .Select(match => match.Groups["text"].Value.Trim())
                .Where(text => text.Length > 0)
                .ToArray();
            Assert.Fail(
                $"Creating the case answered {(int)response.StatusCode} instead of redirecting. "
                + $"Messages: {string.Join(" | ", summary)}");
        }

        var location = response.Headers.Location!.OriginalString;
        Assert.True(
            Guid.TryParse(location.Split('/', StringSplitOptions.RemoveEmptyEntries).Last(), out var caseId),
            $"Creating a case should land on the case; it landed on '{location}'.");
        Assert.StartsWith("/Cases/", location, StringComparison.Ordinal);
        return caseId;
    }

    private static Task<IntakeReceipt> CreateBareReceiptAsync(IServiceProvider services) =>
        StoreReceiptAsync(services, fields: [], draft: null);

    /// <summary>
    /// A receipt that already carries a draft, for the store-level tests.
    /// </summary>
    /// <remarks>
    /// The resolution store refuses to resolve an address for a receipt with no
    /// instruction draft at all. That refusal is what forces the create screen
    /// to record the corrected draft before it touches the address, and these
    /// tests exercise the address step on its own.
    /// </remarks>
    private static Task<IntakeReceipt> CreateDraftedReceiptAsync(IServiceProvider services) =>
        StoreReceiptAsync(
            services,
            fields: [],
            draft: new(
                PrincipalCode,
                "Hand Keyed Claimant",
                "HK-2031-001",
                "AB12CDE",
                "Example Make",
                "Example Model",
                12345L,
                "Keyed circumstances from the retained document.",
                new DateOnly(2031, 3, 4),
                new DateOnly(2031, 3, 5),
                null));

    private static Task<IntakeReceipt> CreateReceiptWithExtractedAddressAsync(
        IServiceProvider services) =>
        StoreReceiptAsync(
            services,
            [
                new(
                    "Inspection address",
                    "4 Extracted Road, Extracton",
                    [new(
                        "4 Extracted Road, Extracton",
                        IntakeEvidenceSource.PdfContent,
                        "retained create-screen test evidence")],
                    IsDefaulted: false,
                    HasConflict: false)
            ],
            draft: null);

    /// <summary>
    /// A receipt that needs sorting and, by default, carries no extracted
    /// address evidence at all: the hand-keyed case.
    /// </summary>
    private static async Task<IntakeReceipt> StoreReceiptAsync(
        IServiceProvider services,
        IReadOnlyList<InstructionReviewField> fields,
        InstructionDraft? draft,
        IReadOnlyList<IntakeAssetRecord>? assets = null)
    {
        var token = Guid.NewGuid().ToString("N");
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        return await store.StoreAsync(
            new(
                "hand-keyed-instruction.pdf",
                "application/pdf",
                1,
                sourceHash,
                new(IntakeSourceChannel.ManualUpload, token),
                RecordedAtUtc,
                RecordedAtUtc,
                "Create screen test",
                IntakeDecision.NeedsSorting,
                "Needs staff sorting",
                [],
                fields,
                draft,
                [],
                null,
                null,
                "create_screen_test_reader",
                "1",
                "create_screen_test_policy",
                1,
                assets),
            CancellationToken.None);
    }

    private static async Task SeedPrincipalAsync(IServiceProvider services, string principalCode)
    {
        if (principalCode == QdosPrincipal.Code)
        {
            _ = await SeededPrincipals.QdosAsync(services);
            return;
        }

        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Hand-keyed provider"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {RecordedAtUtc})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO Principals
                (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version)
            VALUES
                ({principalId}, {organizationId}, {principalCode}, {lineageId}, NULL, NULL, {true}, {0L})
            """);
    }

    /// <summary>
    /// Puts an extracted inspection-address candidate on a receipt that had
    /// none, standing in for a re-evaluation under a policy that now finds one.
    /// </summary>
    private static async Task AddExtractedAddressCandidateAsync(
        IServiceProvider services,
        Guid receiptId)
    {
        const string fieldsJson =
            """
            {"version":1,"data":[{"name":"Inspection address","suggestedValue":"7 Later Lane, Latertown",
            "candidates":[{"value":"7 Later Lane, Latertown","source":"pdf_content",
            "sourceLabel":"retained create-screen test evidence"}],"isDefaulted":false,"hasConflict":false}]}
            """;
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var updated = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE IntakeReceipts SET FieldsJson = {fieldsJson.ReplaceLineEndings(string.Empty)} WHERE Id = {receiptId}");
        Assert.Equal(1, updated);
    }

    private static async Task<InspectionAddressResolutionSnapshot> ReadAddressSnapshotAsync(
        IServiceProvider services,
        Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInspectionAddressResolutionStore>();
        var snapshot = await store.GetAsync(receiptId, CancellationToken.None);
        Assert.NotNull(snapshot);
        return snapshot!;
    }

    private static Task<string> ReadEvidenceJsonAsync(IServiceProvider services, Guid receiptId) =>
        ScalarAsync<string>(
            services,
            $"SELECT EvidenceJson FROM IntakeReceipts WHERE Id = '{receiptId:D}'");

    private static async Task<(string EventKind, string ActorSubjectId)> ReadAddressHistoryAsync(
        IServiceProvider services,
        Guid receiptId)
    {
        var kind = await ScalarAsync<string>(
            services,
            $"""
            SELECT TOP 1 EventKind FROM ActionHistory
            WHERE AggregateId = '{receiptId:D}' AND EventKind LIKE 'inspection_address_%'
            """);
        var subject = await ScalarAsync<string>(
            services,
            $"""
            SELECT TOP 1 ActorSubjectId FROM ActionHistory
            WHERE AggregateId = '{receiptId:D}' AND EventKind LIKE 'inspection_address_%'
            """);
        return (kind, subject);
    }

    private static Task<string> ReadInspectionAddressSourceKindAsync(
        IServiceProvider services,
        Guid caseId) =>
        ScalarAsync<string>(
            services,
            $"""
            SELECT TOP 1 SourceKind FROM CaseDataFields
            WHERE CaseId = '{caseId:D}'
                AND FieldName = 'inspection_address'
                AND ValueKind = 'confirmed'
            """);

    private static Task<T> ReadCaseColumnAsync<T>(
        IServiceProvider services,
        Guid caseId,
        string columnName)
    {
        var allowed = columnName switch
        {
            "Type" or "Reference" or "AuditReference" or "StandaloneAuditAssessment" => columnName,
            _ => throw new ArgumentOutOfRangeException(nameof(columnName))
        };
        return ScalarAsync<T>(services, $"SELECT [{allowed}] FROM Cases WHERE Id = '{caseId:D}'");
    }

    private static Task<long> ReadReceiptVersionAsync(IServiceProvider services, Guid receiptId) =>
        ScalarAsync<long>(
            services,
            $"SELECT Version FROM IntakeReceipts WHERE Id = '{receiptId:D}'");

    private static async Task AdvanceReceiptVersionAsync(IServiceProvider services, Guid receiptId)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var updated = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE IntakeReceipts SET Version = Version + 1 WHERE Id = {receiptId}");
        Assert.Equal(1, updated);
    }

    /// <summary>
    /// How many permanent history rows of one kind the receipt carries.
    /// </summary>
    /// <remarks>
    /// Counting the whole table conflates the corrected draft with the
    /// acceptance, which is what a replay test most needs to tell apart.
    /// </remarks>
    private static Task<int> CountEventsAsync(IServiceProvider services, string eventType)
    {
        var allowed = eventType switch
        {
            "intake_resolved" or "intake_accepted" => eventType,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        return ScalarAsync<int>(
            services,
            $"SELECT COUNT(*) FROM IntakeMutationHistory WHERE EventType = '{allowed}'");
    }

    private static Task<int> CountAsync(IServiceProvider services, string tableName)
    {
        var allowed = tableName switch
        {
            "Cases" or "CaseIntakeLinks" or "CaseSequences" or "IntakeMutationHistory"
                or "IntakeAllocationAttempts" => tableName,
            _ => throw new ArgumentOutOfRangeException(nameof(tableName))
        };
        return ScalarAsync<int>(services, $"SELECT COUNT(*) FROM [{allowed}]");
    }

    private static async Task<T> ScalarAsync<T>(IServiceProvider services, string sql)
    {
        await using var scope = services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            var value = await command.ExecuteScalarAsync();
            Assert.NotNull(value);
            return (T)Convert.ChangeType(value!, typeof(T), CultureInfo.InvariantCulture);
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static string AntiforgeryToken(string html) =>
        InputValue(html, "__RequestVerificationToken");

    private static string InputValue(string html, string name)
    {
        var match = InputTagRegex().Matches(html)
            .Cast<Match>()
            .FirstOrDefault(candidate => string.Equals(
                WebUtility.HtmlDecode(candidate.Groups["name"].Value),
                name,
                StringComparison.Ordinal));
        Assert.True(match is not null, $"The create form must render input '{name}'.");
        return WebUtility.HtmlDecode(match!.Groups["value"].Value);
    }

    [GeneratedRegex(
        "<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InputTagRegex();

    [GeneratedRegex(
        "<li>(?<text>[^<]*)</li>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValidationSummaryRegex();

    private sealed record CreateForm(string Html, Dictionary<string, string> Values);

    /// <summary>
    /// Fails the first address resolution and then delegates, so a submit can
    /// be interrupted between the corrected draft and the address.
    /// </summary>
    private sealed class FailFirstAddressResolutionStore
    {
        private int attempts;

        public IInspectionAddressResolutionStore Wrap(IInspectionAddressResolutionStore inner) =>
            new Wrapper(this, inner);

        private sealed class Wrapper(
            FailFirstAddressResolutionStore owner,
            IInspectionAddressResolutionStore inner) : IInspectionAddressResolutionStore
        {
            public Task<InspectionAddressResolutionSnapshot?> GetAsync(
                Guid intakeReceiptId,
                CancellationToken cancellationToken) =>
                inner.GetAsync(intakeReceiptId, cancellationToken);

            public Task<InspectionAddressResolutionSnapshot> ResolveAsync(
                InspectionAddressResolutionRequest request,
                CancellationToken cancellationToken) =>
                Interlocked.Increment(ref owner.attempts) == 1
                    ? Task.FromException<InspectionAddressResolutionSnapshot>(
                        new InvalidOperationException("Injected mid-sequence failure."))
                    : inner.ResolveAsync(request, cancellationToken);
        }
    }
}
